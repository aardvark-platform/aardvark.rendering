namespace Aardvark.SceneGraph

// Texture atlas. Packs many small host textures into shared pages so the heap can
// sample per-object textures with one sampler instead of a bindless
// descriptor-indexed sampler array. This is the fallback for Vulkan 1.0, GL and
// MoltenVK: descriptor indexing (unbounded sampler arrays) needs Vulkan 1.2 and
// MoltenVK caps samplers at 16, so on those backends the atlas is how per-object
// textures scale.
//
// Layout (per packed texture):
//   * a 2-pixel gutter on every side: the inner ring (1 pixel) replicates the edge
//     texel (clamp), the outer ring (1 pixel) holds the opposite-edge texel (wrap).
//     The hardware bilinear filter at a sub-rect edge then straddles gutter cells,
//     never a neighbour; the repeat-mode +/-1 seam shift lands on the outer wrap ring.
//   * an embedded mip pyramid in the classic 1.5 x 1 layout: mip 0 owns the
//     (W+4) x (H+4) block; mips 1..N stack vertically in the column at x = W+4, each
//     with its own gutter and a 4-pixel gap between stacked slots. Mip selection is
//     done in the shader (the page texture itself is single-level), so the pyramid
//     is just data.
//
// This module is the pure CPU builder and layout math (no GPU, fully testable). The
// reactive refcounted pool lives in HeapAtlasPool.fs and the FShade sampling in
// HeapPool.fs.

open Aardvark.Base
open Aardvark.Geometry

module HeapAtlas =

    /// Atlas page edge length (pixels). Pages are square.
    [<Literal>]
    let PageSize = 4096
    /// Max independent pages per format (the number of page samplers the shader
    /// switches over).
    [<Literal>]
    let MaxPagesPerFormat = 8
    /// Gutter padding per side (1 pixel inner clamp plus 1 pixel outer wrap).
    [<Literal>]
    let Gutter = 2
    /// Vertical gap between stacked mip slots (2 pixel bottom plus 2 pixel top gutter).
    [<Literal>]
    let MipGap = 4

    /// Where a packed texture ended up: page index, mip-0 interior top-left and size
    /// (in atlas pixels, not normalized), and how many mip levels are stored.
    type Acquisition =
        {
            PageId   : int
            OriginPx : V2i
            SizePx   : V2i
            NumMips  : int
        }

    /// Full mip chain down to 1 x 1: floor(log2(max(w,h))) + 1.
    let defaultMipCount (w : int) (h : int) =
        let m = max w h
        if m <= 1 then 1
        else int (floor (log (float m) / log 2.0)) + 1

    /// Mip-k pixel size: halve each level, floor at 1.
    let mipSize (w : int) (h : int) (k : int) =
        V2i(max 1 (w >>> k), max 1 (h >>> k))

    /// Mip-k interior offset from the mip-0 interior, inside the pyramid.
    /// k = 0 gives (0, 0). Otherwise the mips live in the column at x = W+4 and stack
    /// down with a MipGap between them: y_k = sum over j = 1..k-1 of (max(1, H>>j) + MipGap).
    let mipOffset (w : int) (h : int) (k : int) : V2i =
        if k = 0 then V2i.Zero
        else
            let mutable y = 0
            for j in 1 .. k - 1 do
                y <- y + (max 1 (h >>> j)) + MipGap
            V2i(w + MipGap, y)

    /// Reserved rect size for a (w,h) source: gutters always, plus the mip column
    /// when mipped. The shader's pyramid walk assumes exactly this layout.
    let reservedSize (wantsMips : bool) (numMips : int) (w : int) (h : int) : V2i =
        let rw =
            if wantsMips then (w + 4) + (max 1 (w >>> 1)) + 4
            else w + 4
        let mutable rh = h + 4
        if wantsMips then
            let mutable stacked = 0
            for k in 1 .. numMips - 1 do
                stacked <- stacked + (max 1 (h >>> k)) + 4
            rh <- max rh stacked
        V2i(rw, rh)

    /// Blit a w x h source matrix into `dst` as a (w+4) x (h+4) gutter-extended block
    /// whose top-left is (ox, oy). Per-axis source mapping for a cell at offset
    /// dx in [-2 .. w+1] (the interior is 0..w-1): -2 reads w-1 (outer wrap), -1 reads 0
    /// (inner clamp), w reads w-1 (inner clamp), w+1 reads 0 (outer wrap), else dx.
    /// Same for y; corners follow the two axes independently.
    let blitExtended (dst : Matrix<byte, C4b>) (ox : int) (oy : int) (src : Matrix<byte, C4b>) (w : int) (h : int) =
        let mutable dst = dst
        let inline sX dx = if dx = -2 then w - 1 elif dx = -1 then 0 elif dx = w then w - 1 elif dx = w + 1 then 0 else dx
        let inline sY dy = if dy = -2 then h - 1 elif dy = -1 then 0 elif dy = h then h - 1 elif dy = h + 1 then 0 else dy
        for dy in -2 .. h + 1 do
            let sy = sY dy
            for dx in -2 .. w + 1 do
                let sx = sX dx
                dst.[int64 (ox + dx + 2), int64 (oy + dy + 2)] <- src.[int64 sx, int64 sy]

    /// 2 x 2 box-average downscale of a source image to (dw, dh), edge-clamped.
    /// Used to build mip k from mip k-1.
    let downscale (src : PixImage<byte>) (dw : int) (dh : int) : PixImage<byte> =
        let s = src.GetMatrix<C4b>()
        let sw = int src.Size.X
        let sh = int src.Size.Y
        let dstImg = PixImage<byte>(Col.Format.RGBA, V2i(dw, dh))
        let mutable d = dstImg.GetMatrix<C4b>()
        for y in 0 .. dh - 1 do
            let sy0 = y * 2
            let sy1 = min (sy0 + 1) (sh - 1)
            for x in 0 .. dw - 1 do
                let sx0 = x * 2
                let sx1 = min (sx0 + 1) (sw - 1)
                let a = s.[int64 sx0, int64 sy0]
                let b = s.[int64 sx1, int64 sy0]
                let c = s.[int64 sx0, int64 sy1]
                let e = s.[int64 sx1, int64 sy1]
                let inline avg (p : byte) (q : byte) (r : byte) (t : byte) =
                    byte ((int p + int q + int r + int t + 2) / 4)
                d.[int64 x, int64 y] <- C4b(avg a.R b.R c.R e.R, avg a.G b.G c.G e.G, avg a.B b.B c.B e.B, avg a.A b.A c.A e.A)
        dstImg

    /// Pack `textures` (keyed) into one or more atlas pages and render each one's
    /// gutter-extended mip pyramid into the page image. Returns the page images and
    /// a per-key Acquisition (page, mip-0 interior origin and size, mip count). Pure
    /// CPU; the reactive pool uses the same per-texture rendering. `pageSize` is a
    /// parameter for tests.
    let build (pageSize : int) (wantsMips : bool) (textures : (int * PixImage<byte>)[]) : PixImage<byte>[] * Map<int, Acquisition> =
        // Pack: keep a growing list of immutable packings (one per page). The key's
        // reserved rect (gutter plus pyramid) is what the packer places.
        let packings = System.Collections.Generic.List<TexturePacking<int>>()
        let pageOf = System.Collections.Generic.Dictionary<int, int>()
        let info = System.Collections.Generic.Dictionary<int, struct(int * int * int * PixImage<byte>)>() // key -> (w,h,numMips,img)

        for key, img in textures do
            let w = int img.Size.X
            let h = int img.Size.Y
            let numMips = if wantsMips then defaultMipCount w h else 1
            let rs = reservedSize wantsMips numMips w h
            info.[key] <- struct(w, h, numMips, img)

            let mutable placed = false
            let mutable pi = 0
            while not placed && pi < packings.Count do
                match packings.[pi].TryAdd(key, rs) with
                | Some np -> packings.[pi] <- np; pageOf.[key] <- pi; placed <- true
                | None -> pi <- pi + 1
            if not placed then
                if packings.Count >= MaxPagesPerFormat then
                    failwithf "HeapAtlas: exceeded %d pages" MaxPagesPerFormat
                let empty = TexturePacking<int>.Empty(V2i(pageSize, pageSize), false)
                match empty.TryAdd(key, rs) with
                | Some np -> packings.Add np; pageOf.[key] <- packings.Count - 1
                | None -> failwithf "HeapAtlas: %A doesn't fit a %d^2 page" rs pageSize

        // Render each page.
        let atlases = Array.init packings.Count (fun _ -> PixImage<byte>(Col.Format.RGBA, V2i(pageSize, pageSize)))
        let mats = atlases |> Array.map _.GetMatrix<C4b>()
        let mutable acq = Map.empty

        for key, _ in textures do
            let pi = pageOf.[key]
            let struct(w, h, numMips, img) = info.[key]
            let rect = packings.[pi].Used.[key]   // Box2i, inclusive min = reserved-rect top-left
            let dst = mats.[pi]
            // mip 0..N-1: blit each level's gutter-extended block at rect.Min + mipOffset.
            let mutable prev = img
            for k in 0 .. numMips - 1 do
                let mk = mipSize w h k
                let cur = if k = 0 then img else downscale prev mk.X mk.Y
                let bo = rect.Min + mipOffset w h k
                blitExtended dst bo.X bo.Y (cur.GetMatrix<C4b>()) mk.X mk.Y
                prev <- cur
            acq <- Map.add key { PageId = pi; OriginPx = rect.Min + V2i(Gutter, Gutter); SizePx = V2i(w, h); NumMips = numMips } acq

        atlases, acq
