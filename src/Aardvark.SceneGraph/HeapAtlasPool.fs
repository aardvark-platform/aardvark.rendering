namespace Aardvark.SceneGraph

// HeapAtlasPool — reactive, refcounted texture-atlas pool with LRU eviction and dedup.
// Builds on HeapAtlas (pure CPU pack/layout) but maintains GPU pages across frames and only
// uploads the gutter+mip pyramid for newly-acquired textures via ITextureRuntime.Upload's
// sub-rect form (level/slice/offset/size) — no per-frame whole-page rebuild.
//
// API:
//   pool.Acquire(tex, pix)  : Acquisition * pageId    -- dedup + refcount++; new entries land
//                                                       on a free rect / open packer slot /
//                                                       new page / LRU-evict-and-retry.
//   pool.Release(tex)                                 -- refcount--; at 0 entry is LRU-evictable
//                                                       (slot stays cached for reuse).
//   pool.Pages              : aval<IBackendTexture[]> -- current GPU page array (grows on
//                                                       new-page; cval-transacted).
//   pool.Dispose()                                    -- frees all GPU pages.
//
// LRU: entries with RefCount=0 are linked into a list (front = oldest); eviction frees their
// reserved rect to the page's free-rect list. (The underlying TexturePacking<int> is immutable;
// we don't try to remove from it — the free-list overrides for placement and re-acquires of a
// later same-or-smaller texture land on the freed rect.)
//
// Fragmentation is accepted as a v1 trade-off: when an evicted rect is reused by a strictly
// smaller incoming rect, the residual area is currently dropped (no rect-split). Periodic
// compaction (full repack) can be added later if needed.

open Aardvark.Base
open Aardvark.Geometry
open Aardvark.Rendering
open FSharp.Data.Adaptive
open System.Collections.Generic

[<AllowNullLiteral>]
type private AtlasEntry =
    val mutable Acquisition : HeapAtlas.Acquisition
    val mutable ReservedRect : Box2i
    val mutable RefCount : int
    val mutable LastUsedTick : int64
    val mutable LruNode : LinkedListNode<ITexture>     // null when not in LRU (RefCount > 0)
    new (a, r, t) = { Acquisition = a; ReservedRect = r; RefCount = 1; LastUsedTick = t; LruNode = null }

type AtlasPool(runtime : IRuntime, pageSize : int, maxPages : int) =
    let pages      = ResizeArray<IBackendTexture>()
    let packings   = ResizeArray<TexturePacking<int>>()
    let freeRects  = ResizeArray<List<Box2i>>()
    let entries    = Dictionary<ITexture, AtlasEntry>(HashIdentity.Reference)
    let lru        = LinkedList<ITexture>()                                      // front = oldest evictable
    let pagesCval  = cval (Array.empty<IBackendTexture>)
    let mutable tickCounter = 0L
    let mutable nextPackKey = 0

    let publishPages () = transact (fun () -> pagesCval.Value <- pages.ToArray())

    let tryAddPage () =
        if pages.Count >= maxPages then false
        else
            let tex = runtime.CreateTexture2D(V2i(pageSize, pageSize), TextureFormat.Rgba8, levels = 1, samples = 1)
            pages.Add tex
            packings.Add (TexturePacking<int>.Empty(V2i(pageSize, pageSize), false))
            freeRects.Add (List<Box2i>())
            publishPages()
            true

    /// Best-fit search over free rects across all pages. Returns (pageIdx, freeRectIdx, rect).
    let bestFitFree (rs : V2i) =
        let mutable best : ValueOption<struct (int * int * Box2i)> = ValueNone
        let mutable bestArea = System.Int32.MaxValue
        for pi in 0 .. freeRects.Count - 1 do
            let frs = freeRects.[pi]
            for fi in 0 .. frs.Count - 1 do
                let r = frs.[fi]
                let sz = r.Size
                if sz.X >= rs.X && sz.Y >= rs.Y then
                    let area = sz.X * sz.Y
                    if area < bestArea then
                        bestArea <- area
                        best <- ValueSome (struct (pi, fi, r))
        best

    /// Try to find a slot for `rs`: free-rect best-fit, then packer.TryAdd on existing pages.
    /// Returns Some (pageIdx, rectTopLeft) or None (caller must add a page or evict).
    let tryPlace (rs : V2i) : (int * V2i) option =
        match bestFitFree rs with
        | ValueSome (struct (pi, fi, r)) ->
            // Take the freed rect; for v1 we drop the residual (fragmentation accepted).
            freeRects.[pi].RemoveAt fi
            Some (pi, r.Min)
        | ValueNone ->
            let mutable placed = None
            let mutable pi = 0
            while placed.IsNone && pi < packings.Count do
                let k = nextPackKey
                match packings.[pi].TryAdd(k, rs) with
                | Some np ->
                    nextPackKey <- nextPackKey + 1
                    packings.[pi] <- np
                    placed <- Some (pi, np.Used.[k].Min)
                | None -> pi <- pi + 1
            placed

    /// Evict the oldest RefCount=0 entry: free its reserved rect on its page and drop from
    /// `entries`. Returns true if anything was evicted.
    let evictOne () =
        if lru.Count = 0 then false
        else
            let node = lru.First
            let tex = node.Value
            let e = entries.[tex]
            freeRects.[e.Acquisition.PageId].Add e.ReservedRect
            entries.Remove tex |> ignore
            lru.RemoveFirst()
            true

    /// Render the gutter-extended mip pyramid for one texture into a rect-sized PixImage,
    /// matching HeapAtlas.build's per-texture rendering. Returned image is exactly the
    /// reserved-rect size and is ready to upload to the page sub-rect.
    let renderGutteredPyramid (pix : PixImage<byte>) (w : int) (h : int) (numMips : int) (rs : V2i) =
        let img = PixImage<byte>(Col.Format.RGBA, rs)
        let dst = img.GetMatrix<C4b>()
        let mutable prev = pix
        for k in 0 .. numMips - 1 do
            let mk = HeapAtlas.mipSize w h k
            let cur = if k = 0 then pix else HeapAtlas.downscale prev mk.X mk.Y
            let bo = HeapAtlas.mipOffset w h k
            HeapAtlas.blitExtended dst bo.X bo.Y (cur.GetMatrix<C4b>()) mk.X mk.Y
            prev <- cur
        img

    /// Acquire a slot for `tex` (PixImage `pix`). Dedups by reference: a second Acquire of the
    /// same ITexture bumps the refcount, doesn't re-upload. Returns the placement + page index.
    member x.Acquire(tex : ITexture, pix : PixImage<byte>) : HeapAtlas.Acquisition * int =
        tickCounter <- tickCounter + 1L
        match entries.TryGetValue tex with
        | true, e ->
            e.RefCount <- e.RefCount + 1
            e.LastUsedTick <- tickCounter
            if not (isNull e.LruNode) then
                lru.Remove e.LruNode
                e.LruNode <- null
            e.Acquisition, e.Acquisition.PageId
        | _ ->
            let w, h = int pix.Size.X, int pix.Size.Y
            let numMips = HeapAtlas.defaultMipCount w h
            let rs = HeapAtlas.reservedSize true numMips w h

            let rec place () =
                match tryPlace rs with
                | Some pr -> pr
                | None ->
                    if tryAddPage() then
                        // retry on the freshly added empty page
                        let pi = packings.Count - 1
                        let k = nextPackKey
                        match packings.[pi].TryAdd(k, rs) with
                        | Some np ->
                            nextPackKey <- nextPackKey + 1
                            packings.[pi] <- np
                            (pi, np.Used.[k].Min)
                        | None -> failwithf "AtlasPool: %A doesn't fit a %d² page" rs pageSize
                    elif evictOne() then place()
                    else failwithf "AtlasPool: out of space (maxPages=%d, all live)" maxPages

            let pageIdx, topLeft = place()
            let reservedRect = Box2i(topLeft, topLeft + rs)
            let originPx = topLeft + V2i(HeapAtlas.Gutter, HeapAtlas.Gutter)
            let acq : HeapAtlas.Acquisition =
                { PageId = pageIdx; OriginPx = originPx; SizePx = V2i(w, h); NumMips = numMips }

            // sub-rect upload (no whole-page rebuild)
            let gutteredPix = renderGutteredPyramid pix w h numMips rs
            runtime.Upload(pages.[pageIdx], gutteredPix, level = 0, slice = 0, offset = topLeft, size = rs)

            let entry = AtlasEntry(acq, reservedRect, tickCounter)
            entries.[tex] <- entry
            acq, pageIdx

    /// Decrement the refcount for `tex`. At zero the entry becomes LRU-evictable; the slot is
    /// kept in cache so a re-Acquire is free, but space can be reclaimed if needed.
    member x.Release(tex : ITexture) =
        match entries.TryGetValue tex with
        | true, e ->
            if e.RefCount <= 0 then failwithf "AtlasPool.Release: refcount already 0 for %A" tex
            e.RefCount <- e.RefCount - 1
            if e.RefCount = 0 then
                e.LruNode <- lru.AddLast tex
        | _ -> failwithf "AtlasPool.Release: texture %A not in pool" tex

    /// Non-mutating lookup: if `tex` is currently held in the pool, return its
    /// acquisition + page index. Doesn't bump refcount or touch LRU.
    member x.TryGet(tex : ITexture) : (HeapAtlas.Acquisition * int) voption =
        match entries.TryGetValue tex with
        | true, e -> ValueSome (e.Acquisition, e.Acquisition.PageId)
        | _ -> ValueNone

    /// Current GPU page textures. Changes (transactionally) when a new page is added.
    member x.Pages : aval<IBackendTexture[]> = pagesCval :> aval<_>

    member x.PageCount = pages.Count
    member x.EntryCount = entries.Count
    member x.MaxPages = maxPages
    member x.PageSize = pageSize

    member x.Dispose() =
        for t in pages do runtime.DeleteTexture t
        pages.Clear()
        packings.Clear()
        freeRects.Clear()
        entries.Clear()
        lru.Clear()
        transact (fun () -> pagesCval.Value <- Array.empty)

    interface System.IDisposable with
        member x.Dispose() = x.Dispose()
