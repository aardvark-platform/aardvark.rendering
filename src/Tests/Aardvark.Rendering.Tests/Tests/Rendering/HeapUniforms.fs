namespace Aardvark.Rendering.Tests.Rendering

// Heap per-object uniform equivalence: render the SAME RenderObject set two ways —
// (1) classic (each RO's per-draw uniforms in its own UBO) and (2) through
// `Heap.ofRenderObjects` (N -> 1 bucket, uniforms gathered from the arena SSBO) —
// and assert pixel equality. Every object carries a DIFFERENT uniform value, so a
// silent shared-global collapse (one member's value smeared over the bucket) shows
// up as a mismatch. Covers the bit-exact integral leaves and the composite (record)
// walker. Set up like the Vulkan Uniforms tests (prepareCases / headless backend).

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open Aardvark.Application
open FSharp.Data.Adaptive
open FShade
open Expecto

module HeapUniforms =

    // a record uniform with f32 leaves (class record — matches the Uniforms tests'
    // MyRecord; struct-record UBO writing is unrelated-broken in the classic path)
    type HeapRec =
        { RA : V3f
          RB : float32
          RC : V2f }

    module HeapRec =
        [<ReflectedDefinition>]
        let toV3f (r : HeapRec) = r.RA + V3f(r.RB, r.RB, r.RB) + V3f(r.RC.X, r.RC.Y, 0.0f)

    // a record CONTAINING a record + a fixed array -> the walker must recurse
    type HeapNest =
        { NA : HeapRec
          NB : Arr<N<2>, V2f> }

    module HeapNest =
        [<ReflectedDefinition>]
        let toV3f (x : HeapNest) = HeapRec.toV3f x.NA + V3f(x.NB.[0].X, x.NB.[0].Y, x.NB.[1].X)

    // a discriminated union uniform (FShade lowers DUs to tag + per-case payload;
    // the heap packs the tag + active case's fields and rebuilds the case in-shader)
    type HeapDU =
        | Solid of V3f
        | Scaled of float32 * V2f

    module private Sh =
        type UniformScope with
            member x.HeapModelTrafo : M44f   = x?HeapModelTrafo
            member x.ViewProjTrafo  : M44f   = x?ViewProjTrafo
            member x.HeapBigId      : int    = x?HeapBigId
            member x.HeapUId        : uint   = x?HeapUId
            member x.HeapIVec       : V4i    = x?HeapIVec
            member x.HeapTint       : C4f    = x?HeapTint
            member x.HeapFlag       : bool   = x?HeapFlag
            member x.HeapRecord     : HeapRec = x?HeapRecord
            member x.HeapVArr       : Arr<N<3>, V3f>     = x?HeapVArr
            member x.HeapRecArr     : Arr<N<2>, HeapRec> = x?HeapRecArr
            member x.HeapIdx        : int                = x?HeapIdx
            member x.HeapNest       : HeapNest           = x?HeapNest
            member x.HeapDU         : HeapDU             = x?HeapDU
            member x.HeapColor      : V4f               = x?HeapColor
            member x.ModelViewProjTrafo : M44f          = x?ModelViewProjTrafo

        type Vertex =
            { [<Position>] pos : V4f
              [<Normal>]   n   : V3f }

        let vtx (v : Vertex) =
            vertex {
                let m  = uniform.HeapModelTrafo
                let vp = uniform.ViewProjTrafo
                return { v with pos = vp * (m * v.pos); n = m.TransformDir v.n }
            }

        [<ReflectedDefinition>]
        let private lit (n : V3f) =
            let l = Vec.normalize (V3f(1.0f, 2.0f, 3.0f))
            0.25f + 0.75f * max 0.0f (Vec.dot (Vec.normalize n) l)

        // exercises int (> 2^24) / uint / V4i / C4f / bool leaf gathers
        let leafFrag (v : Vertex) =
            fragment {
                let id   = uniform.HeapBigId
                let u    = uniform.HeapUId
                let iv   = uniform.HeapIVec
                let tint = uniform.HeapTint
                let flag = uniform.HeapFlag
                let r = float32 (id % 256) / 255.0f
                let g = float32 (int (u % 256u)) / 255.0f
                let b = float32 (((iv.X + iv.Y + iv.Z + iv.W) % 256 + 256) % 256) / 255.0f
                let c = V3f(r, g, b) * V3f(tint.R, tint.G, tint.B)
                let c = if flag then c else c * 0.5f
                return V4f(c * lit v.n, 1.0f)
            }

        // exercises the composite (record) walker
        let recordFrag (v : Vertex) =
            fragment {
                return V4f(HeapRec.toV3f uniform.HeapRecord * lit v.n, 1.0f)
            }

        // fixed array uniform (constant index) -> Arr gather (NewFixedArray)
        let arrFrag (v : Vertex) =
            fragment {
                let s = uniform.HeapVArr.[0] + uniform.HeapVArr.[1] + uniform.HeapVArr.[2]
                return V4f(s * lit v.n, 1.0f)
            }

        // array-of-record uniform -> nested composite (Arr of record)
        let recordArrFrag (v : Vertex) =
            fragment {
                let s = HeapRec.toV3f uniform.HeapRecArr.[0] + HeapRec.toV3f uniform.HeapRecArr.[1]
                return V4f(s * lit v.n, 1.0f)
            }

        // DYNAMIC array index (runtime i) -> the constructed fixed array is indexed live
        let dynArrFrag (v : Vertex) =
            fragment {
                let i = uniform.HeapIdx
                return V4f(uniform.HeapVArr.[i] * lit v.n, 1.0f)
            }

        // record-of-(record + array) -> the walker recurses through both
        let nestFrag (v : Vertex) =
            fragment {
                return V4f(HeapNest.toV3f uniform.HeapNest * lit v.n, 1.0f)
            }

        // discriminated-union uniform -> tag-driven rebuild + in-shader match
        let duFrag (v : Vertex) =
            fragment {
                let c =
                    match uniform.HeapDU with
                    | Solid col      -> col
                    | Scaled (s, uv) -> V3f(s, uv.X, uv.Y)
                return V4f(c * lit v.n, 1.0f)
            }

        // plain per-object colour — used by the churn / value-update behaviour tests
        let colFrag (v : Vertex) =
            fragment {
                return V4f(uniform.HeapColor.XYZ * lit v.n, 1.0f)
            }

        // a DISTINCT effect (swizzled colour) — lands in its own bucket
        let colFragB (v : Vertex) =
            fragment {
                return V4f(uniform.HeapColor.ZYX * lit v.n, 1.0f)
            }

        // reads the DERIVED ModelViewProjTrafo (heap composes it per-object in fp64
        // from the supplied Model/View/Proj Trafo3d constituents)
        let mvpVtx (v : Vertex) =
            vertex {
                return { v with pos = uniform.ModelViewProjTrafo * v.pos }
            }

    // textures: a vertex carrying tex-coord + a per-object texture id (flat)
    type TexVertex =
        { [<Position>]                                                pos : V4f
          [<Normal>]                                                  n   : V3f
          [<Semantic("TexCoord")>]                                    tc  : V2f
          [<Semantic("TexId"); Interpolation(InterpolationMode.Flat)>] ti : int }

    module private TexSh =
        type UniformScope with
            member x.HeapModelTrafo : M44f = x?HeapModelTrafo
            member x.ViewProjTrafo  : M44f = x?ViewProjTrafo
            member x.HeapTexIndex   : int  = x?HeapTexIndex

        let vClassic (v : TexVertex) =
            vertex {
                let m = uniform.HeapModelTrafo
                let vp = uniform.ViewProjTrafo
                return { v with pos = vp * (m * v.pos); n = m.TransformDir v.n; tc = v.pos.XY + V2f(0.5f, 0.5f) }
            }

        let vHeap (v : TexVertex) =
            vertex {
                let m = uniform.HeapModelTrafo
                let vp = uniform.ViewProjTrafo
                let ti : int = uniform.HeapTexIndex
                return { v with pos = vp * (m * v.pos); n = m.TransformDir v.n; tc = v.pos.XY + V2f(0.5f, 0.5f); ti = ti }
            }

        // classic: one conventional per-object sampler2d
        let private diffuse =
            sampler2d { texture uniform?DiffuseTexture; filter Filter.MinMagMipLinear; addressU WrapMode.Wrap; addressV WrapMode.Wrap }
        // heap: ONE unbounded bindless sampler array, indexed by the per-object id
        let private textures =
            sampler2d { textureArray uniform?Textures -1; filter Filter.MinMagMipLinear; addressU WrapMode.Wrap; addressV WrapMode.Wrap }

        [<ReflectedDefinition>]
        let private litTex (n : V3f) =
            let l = Vec.normalize (V3f(1.0f, 2.0f, 3.0f))
            0.35f + 0.65f * max 0.0f (Vec.dot (Vec.normalize n) l)

        let fClassic (v : TexVertex) =
            fragment { return V4f(diffuse.Sample(v.tc).XYZ * litTex v.n, 1.0f) }

        let fHeap (v : TexVertex) =
            fragment { return V4f(textures.[v.ti].Sample(v.tc).XYZ * litTex v.n, 1.0f) }

    // AUTO-bindless Sampler2dArray: a single per-object sampler2dArray (supplied as
    // aval<ITexture>); the heap rewrites it into its per-type heapTex2dArray. Same
    // effect drives classic and heap — the heap collapses + auto-manages the array.
    module private ArrSh =
        // (HeapModelTrafo / ViewProjTrafo UniformScope members come from TexSh, file-wide)
        let private arr =
            sampler2dArray { texture uniform?DiffuseArray; filter Filter.MinMagMipLinear; addressU WrapMode.Wrap; addressV WrapMode.Wrap }
        [<ReflectedDefinition>]
        let private litA (n : V3f) =
            let l = Vec.normalize (V3f(1.0f, 2.0f, 3.0f))
            0.35f + 0.65f * max 0.0f (Vec.dot (Vec.normalize n) l)
        let vert (v : TexVertex) =
            vertex {
                let m : M44f = uniform?HeapModelTrafo
                let vp : M44f = uniform?ViewProjTrafo
                return { v with pos = vp * (m * v.pos); n = m.TransformDir v.n; tc = v.pos.XY + V2f(0.5f, 0.5f) }
            }
        let frag (v : TexVertex) =
            fragment { return V4f(arr.Sample(v.tc, 0).XYZ * litA v.n, 1.0f) }

    // a vertex carrying a per-vertex INT attribute (ITag, flat) and a per-vertex
    // MATRIX attribute (MX) — both storage-decoded by the heap.
    type AttrV =
        { [<Position>]                                                pos : V4f
          [<Normal>]                                                  n   : V3f
          [<Color>]                                                   c   : V4f
          [<Semantic("ITag"); Interpolation(InterpolationMode.Flat)>] tag : int
          [<Semantic("MX")>]                                          mx  : M44f }

    module private AttrSh =
        type UniformScope with
            member x.HeapModelTrafo : M44f = x?HeapModelTrafo
            member x.ViewProjTrafo  : M44f = x?ViewProjTrafo

        let vtx (v : AttrV) =
            vertex {
                let m  = uniform.HeapModelTrafo
                let vp = uniform.ViewProjTrafo
                // colour DERIVED from the int + matrix attributes, so a decode error shows
                let fromMat = (v.mx * V4f(0.3f, 0.4f, 0.5f, 1.0f)).XYZ
                let fromTag = float32 (((v.tag % 11) + 11) % 11) / 10.0f
                let col = V3f(fromTag, 0.45f, 0.3f) + fromMat * 0.3f
                return { v with pos = vp * (m * v.pos); n = m.TransformDir v.n; c = V4f(col, 1.0f) }
            }

        let frag (v : AttrV) =
            fragment {
                let l = Vec.normalize (V3f(1.0f, 2.0f, 3.0f))
                let d = 0.25f + 0.75f * max 0.0f (Vec.dot (Vec.normalize v.n) l)
                return V4f(v.c.XYZ * d, 1.0f)
            }

    module private Harness =
        let private g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.6)) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>

        let private bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let private vattrs =
            AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>
                                       DefaultSemantic.Normals,   bv normals   typeof<V3f> ]

        let private view = CameraView.lookAt (V3d(0.0, -1.0, 1.0) * 18.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let private proj = Frustum.perspective 70.0 0.1 5000.0 1.0 |> Frustum.projTrafo
        let viewProj = AVal.constant (view * proj) :> IAdaptiveValue

        let grid (n : int) =
            let s = int (ceil (sqrt (float n)))
            Array.init n (fun i -> i, V3d(float (i % s - s / 2) * 1.2, float (i / s - s / 2) * 1.2, 0.0))

        let mkRO (uniforms : list<Symbol * IAdaptiveValue>) (effect : Effect) =
            let ro = RenderObject()
            ro.Surface          <- Surface.Effect effect
            ro.Mode             <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- vattrs
            ro.Indices          <- Some (bv index typeof<int>)
            ro.DrawCalls        <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
            ro.Uniforms         <- UniformProvider.ofList uniforms
            ro :> IRenderObject

        // RO with EXPLICIT (per-object) geometry — for heterogeneous-mesh buckets
        let mkROGeom (ps : V3f[]) (ns : V3f[]) (ix : int[]) (uniforms : list<Symbol * IAdaptiveValue>) (effect : Effect) =
            let ro = RenderObject()
            ro.Surface          <- Surface.Effect effect
            ro.Mode             <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <- AttributeProvider.ofList [ DefaultSemantic.Positions, bv ps typeof<V3f>
                                                              DefaultSemantic.Normals,   bv ns typeof<V3f> ]
            ro.Indices          <- Some (bv ix typeof<int>)
            ro.DrawCalls        <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = ix.Length, InstanceCount = 1) |])
            ro.Uniforms         <- UniformProvider.ofList uniforms
            ro :> IRenderObject

        let private renderPix (runtime : IRuntime) (signature : IFramebufferSignature) (objs : aset<IRenderObject>) =
            use task = runtime.CompileRender(signature, objs)
            let out = task |> RenderTask.renderToColor (AVal.constant (V2i(256)))
            out.Acquire()
            try out.GetValue().Download().AsPixImage<uint8>()
            finally out.Release()

        /// max per-channel delta and # non-background pixels between classic & heap
        let private compare (a : PixImage<uint8>) (b : PixImage<uint8>) =
            let am = a.GetMatrix<C4b>()
            let bm = b.GetMatrix<C4b>()
            let mutable maxDelta = 0
            let mutable nNonBg = 0L
            am.ForeachCoord(fun (c : V2l) ->
                let p = am.[c]
                let q = bm.[c]
                let d = max (max (abs (int p.R - int q.R)) (abs (int p.G - int q.G))) (abs (int p.B - int q.B))
                if d > maxDelta then maxDelta <- d
                if p.R <> 0uy || p.G <> 0uy || p.B <> 0uy then nNonBg <- nNonBg + 1L)
            maxDelta, nNonBg, int64 am.Size.X * int64 am.Size.Y

        /// render `n` objects (each with a per-object uniform from `perObj`) classic &
        /// heap; assert pixel-equal with real coverage. Skips on backends without heap.
        let equivalence (frag : Sh.Vertex -> Microsoft.FSharp.Quotations.Expr<V4f>) (perObj : int -> list<Symbol * IAdaptiveValue>) (n : int) (runtime : IRuntime) =
            if not (Heap.isSupported runtime) then
                skiptest "heap path unsupported on this backend"
            // the GL command compiler does not handle the heap's custom HeapRenderObject
            // ("bad object"); the heap render path is Vulkan in practice.
            if runtime.GetType().FullName.Contains "Aardvark.Rendering.GL" then
                skiptest "heap render-object path is Vulkan-only"

            use signature =
                runtime.CreateFramebufferSignature [
                    DefaultSemantic.Colors, TextureFormat.Rgba8
                    DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]

            let effect = Effect.compose [ Effect.ofFunction Sh.vtx; Effect.ofFunction frag ]
            let objs =
                grid n
                |> Array.map (fun (i, p) ->
                    let common =
                        [ Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                          Symbol.Create "ViewProjTrafo",  viewProj ]
                    mkRO (common @ perObj i) effect)

            let classic = renderPix runtime signature (ASet.ofArray objs)
            let heap    = renderPix runtime signature (Heap.ofRenderObjects signature (ASet.ofArray objs))
            let maxDelta, nNonBg, total = compare classic heap
            Expect.isLessThanOrEqual maxDelta 1 (sprintf "classic vs heap max channel delta (%d buckets)" Heap.lastBucketCount)
            Expect.isGreaterThan nNonBg 100L "scene rendered blank — nothing to compare"
            ignore total

        let private skipUnlessHeapVulkan (runtime : IRuntime) =
            if not (Heap.isSupported runtime) then skiptest "heap path unsupported on this backend"
            if runtime.GetType().FullName.Contains "Aardvark.Rendering.GL" then skiptest "heap render-object path is Vulkan-only"

        let private colEffect = Effect.compose [ Effect.ofFunction Sh.vtx; Effect.ofFunction Sh.colFrag ]
        let private sig256 (runtime : IRuntime) =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]

        // deterministic cube `id` -> a placed, coloured RenderObject (colour from `col`)
        let private cube (id : int) (col : IAdaptiveValue) =
            let s = 6
            let p = V3d(float (id % s - s / 2) * 1.2, float (id / s % s - s / 2) * 1.2, 0.0)
            mkRO [ Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                   Symbol.Create "HeapColor",      col
                   Symbol.Create "ViewProjTrafo",  viewProj ] colEffect

        /// INCREMENTAL membership: a live heap render task fed by a changeable set;
        /// after each add/remove transact the heap frame must match a fresh classic
        /// render of the SAME membership. Exercises free-list slot reuse, region
        /// ref-count dedup, arena growth and indirect-buffer rebuild.
        let churn (runtime : IRuntime) =
            skipUnlessHeapVulkan runtime
            use signature = sig256 runtime
            let byId = System.Collections.Generic.Dictionary<int, IRenderObject>()
            let cubes = cset<IRenderObject>()
            let colOf id = AVal.constant (V4f(0.25f + 0.05f * float32 (id % 11), 0.3f + 0.04f * float32 (id % 7), 0.6f, 1.0f)) :> IAdaptiveValue
            let add (ids : int list) = transact (fun () -> for id in ids do let ro = cube id (colOf id) in byId.[id] <- ro; cubes.Add ro |> ignore)
            let remove (ids : int list) = transact (fun () -> for id in ids do match byId.TryGetValue id with | true, ro -> cubes.Remove ro |> ignore; byId.Remove id |> ignore | _ -> ())

            let heapObjs = Heap.ofRenderObjects signature (cubes :> aset<IRenderObject>)
            use heapTask = runtime.CompileRender(signature, heapObjs)
            let heapOut = heapTask |> RenderTask.renderToColor (AVal.constant (V2i(256)))
            heapOut.Acquire()
            try
                let check (label : string) =
                    let heapPix    = heapOut.GetValue().Download().AsPixImage<uint8>()
                    let classicPix = renderPix runtime signature (ASet.ofList (List.ofSeq byId.Values))
                    let maxDelta, nNonBg, _ = compare classicPix heapPix
                    Expect.isLessThanOrEqual maxDelta 1 (sprintf "churn[%s] heap vs classic (%d live, %d buckets)" label byId.Count Heap.lastBucketCount)
                    Expect.isGreaterThan nNonBg 50L (sprintf "churn[%s] rendered blank" label)
                add [0..9];                       check "init-10"
                remove [1; 3; 5; 7];              check "remove-4"
                add [10; 11; 12; 13; 14];         check "add-5 (free-list reuse + grow)"
                remove [0; 2; 4; 6; 8; 10; 12; 14]; check "remove-8"
                add [1; 3; 5; 15; 16];            check "re-add"
            finally
                heapOut.Release()

        /// PER-DRAW VALUE update: live heap & classic tasks share the SAME mutable
        /// colour cvals; after each value change the heap re-pack must still match
        /// classic. Exercises the RegionWriter re-evaluation path (not membership).
        let valueUpdate (runtime : IRuntime) =
            skipUnlessHeapVulkan runtime
            use signature = sig256 runtime
            let cols = Array.init 6 (fun _ -> AVal.init (V4f(0.3f, 0.3f, 0.6f, 1.0f)))
            let objs = Array.init 6 (fun i -> cube i (cols.[i] :> IAdaptiveValue))
            let set = ASet.ofArray objs
            use heapTask    = runtime.CompileRender(signature, Heap.ofRenderObjects signature set)
            use classicTask = runtime.CompileRender(signature, set)
            let heapOut    = heapTask    |> RenderTask.renderToColor (AVal.constant (V2i(256)))
            let classicOut = classicTask |> RenderTask.renderToColor (AVal.constant (V2i(256)))
            heapOut.Acquire(); classicOut.Acquire()
            try
                let check (label : string) =
                    let heapPix    = heapOut.GetValue().Download().AsPixImage<uint8>()
                    let classicPix = classicOut.GetValue().Download().AsPixImage<uint8>()
                    let maxDelta, nNonBg, _ = compare classicPix heapPix
                    Expect.isLessThanOrEqual maxDelta 1 (sprintf "valueUpdate[%s] heap vs classic" label)
                    Expect.isGreaterThan nNonBg 50L (sprintf "valueUpdate[%s] rendered blank" label)
                check "initial"
                transact (fun () -> cols |> Array.iteri (fun i c -> c.Value <- V4f(0.1f * float32 i, 0.7f, 0.2f, 1.0f)))
                check "all colours changed"
                transact (fun () -> cols.[0].Value <- V4f(0.9f, 0.1f, 0.1f, 1.0f))
                check "single colour changed"
            finally
                heapOut.Release(); classicOut.Release()

        /// BUCKETING: two distinct effects -> the heap must split into 2 buckets, and
        /// the combined result still matches a classic render of the same objects.
        let bucketing (runtime : IRuntime) =
            skipUnlessHeapVulkan runtime
            use signature = sig256 runtime
            let effA = Effect.compose [ Effect.ofFunction Sh.vtx; Effect.ofFunction Sh.colFrag ]
            let effB = Effect.compose [ Effect.ofFunction Sh.vtx; Effect.ofFunction Sh.colFragB ]
            let col i = AVal.constant (V4f(0.2f + 0.05f * float32 (i % 9), 0.6f, 0.3f, 1.0f)) :> IAdaptiveValue
            let objs =
                grid 16
                |> Array.map (fun (i, p) ->
                    let e = if i % 2 = 0 then effA else effB
                    mkRO [ Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                           Symbol.Create "HeapColor",      col i
                           Symbol.Create "ViewProjTrafo",  viewProj ] e)
            let classic = renderPix runtime signature (ASet.ofArray objs)
            let heap    = renderPix runtime signature (Heap.ofRenderObjects signature (ASet.ofArray objs))
            let buckets = Heap.lastBucketCount
            let maxDelta, nNonBg, _ = compare classic heap
            Expect.equal buckets 2 (sprintf "two distinct effects should give 2 buckets, got %d" buckets)
            Expect.isLessThanOrEqual maxDelta 1 "bucketing heap vs classic"
            Expect.isGreaterThan nNonBg 100L "bucketing rendered blank"

        /// DERIVED uniforms: the heap composes ModelViewProjTrafo per-object in fp64
        /// from supplied Model/View/Proj Trafo3d; that must match a classic render fed
        /// the SAME MVP precomposed in double then downcast.
        let derived (runtime : IRuntime) =
            skipUnlessHeapVulkan runtime
            use signature = sig256 runtime
            let viewT = AVal.constant view :> IAdaptiveValue
            let projT = AVal.constant proj :> IAdaptiveValue
            let eff = Effect.compose [ Effect.ofFunction Sh.mvpVtx; Effect.ofFunction Sh.colFrag ]
            let col i = AVal.constant (V4f(0.3f, 0.4f + 0.03f * float32 (i % 7), 0.6f, 1.0f)) :> IAdaptiveValue
            // heap: supply the CONSTITUENTS -> heap derives ModelViewProjTrafo (fp64 compose)
            let heapObjs =
                grid 12
                |> Array.map (fun (i, p) ->
                    mkRO [ Symbol.Create "ModelTrafo", (AVal.constant (Trafo3d.Translation p) :> IAdaptiveValue)
                           Symbol.Create "ViewTrafo",  viewT
                           Symbol.Create "ProjTrafo",  projT
                           Symbol.Create "HeapColor",  col i ] eff)
            // classic: supply the SAME MVP precomposed in double (proj.F * view.F * model.F)
            let classicObjs =
                grid 12
                |> Array.map (fun (i, p) ->
                    let mvp = M44f.op_Explicit (proj.Forward * view.Forward * (Trafo3d.Translation p).Forward)
                    mkRO [ Symbol.Create "ModelViewProjTrafo", (AVal.constant mvp :> IAdaptiveValue)
                           Symbol.Create "HeapColor",          col i ] eff)
            let classicPix = renderPix runtime signature (ASet.ofArray classicObjs)
            let heapPix    = renderPix runtime signature (Heap.ofRenderObjects signature (ASet.ofArray heapObjs))
            let maxDelta, nNonBg, _ = compare classicPix heapPix
            Expect.isLessThanOrEqual maxDelta 1 (sprintf "derived MVP: heap fp64-compose vs classic precomposed (%d buckets)" Heap.lastBucketCount)
            Expect.isGreaterThan nNonBg 100L "derived rendered blank"

        /// RESOURCE RECLAMATION on a LIVE task: live arena bytes track membership,
        /// the extent stays bounded under churn (free-list reuse — no leak), removing
        /// ALL objects tears the bucket down, and the heap recovers on re-add.
        let resourceReclaim (runtime : IRuntime) =
            skipUnlessHeapVulkan runtime
            use signature = sig256 runtime
            let byId = System.Collections.Generic.Dictionary<int, IRenderObject>()
            let cubes = cset<IRenderObject>()
            let colOf id = AVal.constant (V4f(0.3f + 0.03f * float32 (id % 9), 0.5f, 0.7f, 1.0f)) :> IAdaptiveValue
            let add (ids : int list) = transact (fun () -> for id in ids do let ro = cube id (colOf id) in byId.[id] <- ro; cubes.Add ro |> ignore)
            let remove (ids : int list) = transact (fun () -> for id in ids do match byId.TryGetValue id with | true, ro -> cubes.Remove ro |> ignore; byId.Remove id |> ignore | _ -> ())

            let heapObjs = Heap.ofRenderObjects signature (cubes :> aset<IRenderObject>)
            use heapTask = runtime.CompileRender(signature, heapObjs)
            let heapOut = heapTask |> RenderTask.renderToColor (AVal.constant (V2i(256)))
            heapOut.Acquire()
            try
                let render () = heapOut.GetValue().Download() |> ignore

                add [0 .. 15]; render ()
                let liveFull   = Heap.lastArenaLiveBytes
                let extentFull = Heap.lastArenaBytes
                Expect.isGreaterThan liveFull 0 "arena must hold live bytes when populated"

                // remove half -> live bytes must drop (regions freed, not just hidden)
                remove [0; 1; 2; 3; 4; 5; 6; 7]; render ()
                Expect.isLessThan Heap.lastArenaLiveBytes liveFull "live bytes must drop when objects are removed"

                // steady-count churn -> extent stays bounded (freed slots reused, no leak)
                for _ in 1 .. 24 do
                    remove [8; 9; 10; 11]
                    add    [8; 9; 10; 11]
                render ()
                Expect.isLessThanOrEqual Heap.lastArenaBytes (extentFull * 2) "arena extent must stay bounded under churn (no slot leak)"

                // remove EVERYTHING -> the bucket is torn down (all GPU regions released)
                remove (List.ofSeq byId.Keys); render ()
                Expect.equal Heap.lastBucketCount 0 "removing all objects must release the bucket"

                // ...and the heap recovers: re-add and it matches a fresh classic render
                add [20; 21; 22]; render ()
                let heapPix    = heapOut.GetValue().Download().AsPixImage<uint8>()
                let classicPix = renderPix runtime signature (ASet.ofList (List.ofSeq byId.Values))
                let maxDelta, nNonBg, _ = compare classicPix heapPix
                Expect.isLessThanOrEqual maxDelta 1 "heap must recover after full drain + re-add"
                Expect.isGreaterThan nNonBg 50L "recovered scene rendered blank"
            finally
                heapOut.Release()

        // distinct per-index checker texture (same content addressed by both paths)
        let private mkTexture (i : int) : ITexture =
            let cols = [| C3b(230, 60, 60); C3b(60, 200, 60); C3b(60, 120, 230); C3b(230, 200, 40)
                          C3b(210, 60, 210); C3b(40, 210, 210); C3b(230, 140, 40); C3b(180, 180, 180) |]
            let col = cols.[i % cols.Length]
            let img = PixImage<byte>(Col.Format.RGBA, V2i(64, 64))
            img.GetMatrix<C4b>().SetByIndex(fun (idx : int64) ->
                let x = int idx % 64
                let y = int idx / 64
                if ((x / 8) + (y / 8)) % 2 = 0 then C4b(col) else C4b.White) |> ignore
            PixTexture2d(img) :> ITexture

        /// BINDLESS per-object textures: a heap object indexes ONE unbounded sampler
        /// array (descriptor indexing) vs a classic per-object sampler2d. Same texture
        /// content addressed both ways -> identical image.
        let textures (runtime : IRuntime) =
            skipUnlessHeapVulkan runtime
            if not runtime.SupportsUnboundedSamplerArrays then
                skiptest "bindless sampler arrays unsupported (descriptor indexing)"
            use signature = sig256 runtime
            let texCount = 16
            let texArray : ITexture[] = Array.init texCount mkTexture
            let texArrayU = AVal.constant (texArray |> Array.mapi (fun i t -> i, AVal.constant t)) :> IAdaptiveValue
            let effC = Effect.compose [ Effect.ofFunction TexSh.vClassic; Effect.ofFunction TexSh.fClassic ]
            let effH = Effect.compose [ Effect.ofFunction TexSh.vHeap;    Effect.ofFunction TexSh.fHeap ]
            let common (p : V3d) =
                [ Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                  Symbol.Create "ViewProjTrafo",  viewProj ]
            let classicObjs = grid 16 |> Array.map (fun (i, p) -> mkRO (common p @ [ Symbol.Create "DiffuseTexture", (AVal.constant texArray.[i % texCount] :> IAdaptiveValue) ]) effC)
            let heapObjs    = grid 16 |> Array.map (fun (i, p) -> mkRO (common p @ [ Symbol.Create "HeapTexIndex", (AVal.constant (i % texCount) :> IAdaptiveValue); Symbol.Create "Textures", texArrayU ]) effH)
            let classicPix = renderPix runtime signature (ASet.ofArray classicObjs)
            let heapPix    = renderPix runtime signature (Heap.ofRenderObjects signature (ASet.ofArray heapObjs))
            let maxDelta, nNonBg, _ = compare classicPix heapPix
            Expect.isLessThanOrEqual maxDelta 1 (sprintf "bindless heap textures vs classic per-object sampler (%d buckets)" Heap.lastBucketCount)
            Expect.isGreaterThan nNonBg 100L "textured scene rendered blank"

        // a distinct single-layer 2d-ARRAY texture per index (checker, colour i).
        // Returns a backend texture the CALLER owns and must delete.
        let private mkArrayTexture (runtime : IRuntime) (i : int) : IBackendTexture =
            let cols = [| C3b(230, 60, 60); C3b(60, 200, 60); C3b(60, 120, 230); C3b(230, 200, 40)
                          C3b(210, 60, 210); C3b(40, 210, 210); C3b(230, 140, 40); C3b(180, 180, 180) |]
            let col = cols.[i % cols.Length]
            let img = PixImage<byte>(Col.Format.RGBA, V2i(64, 64))
            img.GetMatrix<C4b>().SetByIndex(fun (idx : int64) ->
                let x = int idx % 64
                let y = int idx / 64
                if ((x / 8) + (y / 8)) % 2 = 0 then C4b(col) else C4b.White) |> ignore
            let t = runtime.CreateTexture2DArray(V2i(64, 64), TextureFormat.Rgba8, levels = 1, samples = 1, count = 1)
            t.Upload(img, slice = 0)
            t

        /// AUTO-bindless Sampler2dArray: a per-object single sampler2dArray, same effect
        /// classic vs heap — the heap must collapse the ROs and rewrite the sampler into
        /// its heapTex2dArray (descriptor-indexed), pixel-identical to a classic render.
        let textureArray (runtime : IRuntime) =
            skipUnlessHeapVulkan runtime
            if not runtime.SupportsUnboundedSamplerArrays then
                skiptest "bindless sampler arrays unsupported (descriptor indexing)"
            use signature = sig256 runtime
            let texCount = 8
            let texArray : IBackendTexture[] = Array.init texCount (mkArrayTexture runtime)
            try
                let eff = Effect.compose [ Effect.ofFunction ArrSh.vert; Effect.ofFunction ArrSh.frag ]
                let common (p : V3d) =
                    [ Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                      Symbol.Create "ViewProjTrafo",  viewProj ]
                let objs = grid 16 |> Array.map (fun (i, p) -> mkRO (common p @ [ Symbol.Create "DiffuseArray", (AVal.constant (texArray.[i % texCount] :> ITexture) :> IAdaptiveValue) ]) eff)
                let classic = renderPix runtime signature (ASet.ofArray objs)
                let heap    = renderPix runtime signature (Heap.ofRenderObjects signature (ASet.ofArray objs))
                Expect.equal Heap.lastBucketCount 1 "Sampler2dArray ROs must collapse to one heap bucket (auto-bindless)"
                let maxDelta, nNonBg, _ = compare classic heap
                Expect.isLessThanOrEqual maxDelta 1 "Sampler2dArray bindless heap vs classic"
                Expect.isGreaterThan nNonBg 100L "array-textured scene rendered blank"
            finally
                texArray |> Array.iter runtime.DeleteTexture

        /// HETEROGENEOUS geometry: distinct meshes (different vertex/index counts) in
        /// ONE bucket -> per-allocation-headed arena ranges, decoded per object. Must
        /// match a classic render of the same per-object meshes.
        let heterogeneousGeometry (runtime : IRuntime) =
            skipUnlessHeapVulkan runtime
            use signature = sig256 runtime
            let mesh (gg : IndexedGeometry) =
                let gi = gg.ToIndexed()
                gi.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>,
                gi.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>,
                gi.IndexArray |> unbox<int[]>
            let meshes =
                [| mesh (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.6)) C4b.White)
                   mesh (IndexedGeometryPrimitives.Sphere.solidPhiThetaSphere (Sphere3d(V3d.Zero, 0.38)) 8 C4b.White)
                   mesh (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.42)) C4b.White) |]
            let col i = AVal.constant (V4f(0.25f + 0.05f * float32 (i % 9), 0.55f, 0.35f, 1.0f)) :> IAdaptiveValue
            let mk i (p : V3d) =
                let (ps, ns, ix) = meshes.[i % meshes.Length]
                mkROGeom ps ns ix
                    [ Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                      Symbol.Create "HeapColor",      col i
                      Symbol.Create "ViewProjTrafo",  viewProj ] colEffect
            let objs = grid 12 |> Array.map (fun (i, p) -> mk i p)
            let classic = renderPix runtime signature (ASet.ofArray objs)
            let heap    = renderPix runtime signature (Heap.ofRenderObjects signature (ASet.ofArray objs))
            let maxDelta, nNonBg, _ = compare classic heap
            Expect.isLessThanOrEqual maxDelta 1 (sprintf "heterogeneous-mesh heap vs classic (%d buckets)" Heap.lastBucketCount)
            Expect.isGreaterThan nNonBg 100L "heterogeneous geometry rendered blank"

        /// TEXTURE ATLAS fallback: force the Sampler2d atlas path (the non-descriptor-
        /// indexing route) and compare to a classic per-object sampler. Solid tiles +
        /// interior tex-coords -> atlas sampling is exact.
        let texturesAtlas (runtime : IRuntime) =
            skipUnlessHeapVulkan runtime
            use signature = sig256 runtime
            let prev = Heap.forceAtlas
            Heap.forceAtlas <- true
            try
                let texCount = 8
                let mkSolid (i : int) : ITexture =
                    let cols = [| C4b(230, 60, 60, 255); C4b(60, 200, 60, 255); C4b(60, 120, 230, 255); C4b(230, 200, 40, 255)
                                  C4b(210, 60, 210, 255); C4b(40, 210, 210, 255); C4b(230, 140, 40, 255); C4b(180, 180, 180, 255) |]
                    let img = PixImage<byte>(Col.Format.RGBA, V2i(32, 32))
                    img.GetMatrix<C4b>().SetByIndex(fun (_ : int64) -> cols.[i % cols.Length]) |> ignore
                    PixTexture2d(img) :> ITexture
                let texArray : ITexture[] = Array.init texCount mkSolid
                let texArrayU = AVal.constant (texArray |> Array.mapi (fun i t -> i, AVal.constant t)) :> IAdaptiveValue
                let effC = Effect.compose [ Effect.ofFunction TexSh.vClassic; Effect.ofFunction TexSh.fClassic ]
                let effH = Effect.compose [ Effect.ofFunction TexSh.vHeap;    Effect.ofFunction TexSh.fHeap ]
                let common (p : V3d) =
                    [ Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                      Symbol.Create "ViewProjTrafo",  viewProj ]
                let classicObjs = grid 12 |> Array.map (fun (i, p) -> mkRO (common p @ [ Symbol.Create "DiffuseTexture", (AVal.constant texArray.[i % texCount] :> IAdaptiveValue) ]) effC)
                let heapObjs    = grid 12 |> Array.map (fun (i, p) -> mkRO (common p @ [ Symbol.Create "HeapTexIndex", (AVal.constant (i % texCount) :> IAdaptiveValue); Symbol.Create "Textures", texArrayU ]) effH)
                let classicPix = renderPix runtime signature (ASet.ofArray classicObjs)
                let heapPix    = renderPix runtime signature (Heap.ofRenderObjects signature (ASet.ofArray heapObjs))
                let maxDelta, nNonBg, _ = compare classicPix heapPix
                Expect.isLessThanOrEqual maxDelta 4 (sprintf "atlas-fallback heap vs classic (%d buckets)" Heap.lastBucketCount)
                Expect.isGreaterThan nNonBg 100L "atlas textured rendered blank"
            finally
                Heap.forceAtlas <- prev

        /// INT + MATRIX vertex attributes: storage-decoded by the heap (int via the
        /// int arena view, M44f row-wise) must match a classic per-vertex-attribute
        /// render. Colour is derived from both, so a decode error is visible.
        let attributes (runtime : IRuntime) =
            skipUnlessHeapVulkan runtime
            use signature = sig256 runtime
            let nv = positions.Length
            let itag = Array.init nv (fun i -> (i * 7) % 13)
            let mx =
                Array.init nv (fun i ->
                    let s = 0.5f + 0.05f * float32 (i % 5)
                    M44f(s, 0.1f, 0.0f, 0.0f,  0.0f, s, 0.0f, 0.0f,  0.0f, 0.0f, s, 0.0f,  0.2f, 0.3f, 0.1f, 1.0f))
            let mk (p : V3d) =
                let ro = RenderObject()
                ro.Surface          <- Surface.Effect (Effect.compose [ Effect.ofFunction AttrSh.vtx; Effect.ofFunction AttrSh.frag ])
                ro.Mode             <- IndexedGeometryMode.TriangleList
                ro.VertexAttributes <- AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>
                                                                  DefaultSemantic.Normals,   bv normals   typeof<V3f>
                                                                  Symbol.Create "ITag",      bv itag typeof<int>
                                                                  Symbol.Create "MX",        bv mx   typeof<M44f> ]
                ro.Indices          <- Some (bv index typeof<int>)
                ro.DrawCalls        <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
                ro.Uniforms         <- UniformProvider.ofList [ Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                                                                Symbol.Create "ViewProjTrafo",  viewProj ]
                ro :> IRenderObject
            let objs = grid 12 |> Array.map (fun (_, p) -> mk p)
            let classic = renderPix runtime signature (ASet.ofArray objs)
            let heap    = renderPix runtime signature (Heap.ofRenderObjects signature (ASet.ofArray objs))
            // must actually COLLAPSE (else a pass-through would pass without exercising
            // the int/matrix attribute decode at all)
            Expect.equal Heap.lastBucketCount 1 "int+matrix-attr ROs must collapse to one heap bucket (not pass through)"
            let maxDelta, nNonBg, _ = compare classic heap
            Expect.isLessThanOrEqual maxDelta 1 "int + matrix vertex attributes heap vs classic"
            Expect.isGreaterThan nNonBg 100L "attribute scene rendered blank"

    module Cases =
        let leaves : IRuntime -> unit =
            Harness.equivalence Sh.leafFrag (fun i ->
                [ // > 2^24 and not a multiple of 8 -> the old float round-trip corrupts id%256
                  Symbol.Create "HeapBigId", (AVal.constant (100_000_003 + i * 101) :> IAdaptiveValue)
                  Symbol.Create "HeapUId",   (AVal.constant (uint (50_000 + i * 37)) :> IAdaptiveValue)
                  Symbol.Create "HeapIVec",  (AVal.constant (V4i(i, i * 2, i * 3, i * 5)) :> IAdaptiveValue)
                  Symbol.Create "HeapTint",  (AVal.constant (C4f(0.3f + 0.02f * float32 i, 0.9f, 0.4f, 1.0f)) :> IAdaptiveValue)
                  Symbol.Create "HeapFlag",  (AVal.constant (i % 2 = 0) :> IAdaptiveValue) ]) 16

        let record : IRuntime -> unit =
            Harness.equivalence Sh.recordFrag (fun i ->
                let r = { RA = V3f(0.1f * float32 i, 0.7f, 0.3f); RB = 0.05f * float32 (i % 5); RC = V2f(0.2f, 0.4f + 0.01f * float32 i) }
                [ Symbol.Create "HeapRecord", (AVal.constant r :> IAdaptiveValue) ]) 16

        let v3fArray : IRuntime -> unit =
            Harness.equivalence Sh.arrFrag (fun i ->
                let a : V3f[] = [| V3f(0.1f * float32 (i % 9), 0.2f, 0.3f)
                                   V3f(0.4f, 0.07f * float32 (i % 7), 0.2f)
                                   V3f(0.05f, 0.15f, 0.06f * float32 (i % 5)) |]
                [ Symbol.Create "HeapVArr", (AVal.constant a :> IAdaptiveValue) ]) 16

        let recordArray : IRuntime -> unit =
            Harness.equivalence Sh.recordArrFrag (fun i ->
                let a : HeapRec[] =
                    [| { RA = V3f(0.1f * float32 (i % 8), 0.3f, 0.2f); RB = 0.03f * float32 (i % 5); RC = V2f(0.1f, 0.2f) }
                       { RA = V3f(0.2f, 0.05f * float32 (i % 6), 0.1f); RB = 0.02f * float32 (i % 4); RC = V2f(0.15f, 0.05f) } |]
                [ Symbol.Create "HeapRecArr", (AVal.constant a :> IAdaptiveValue) ]) 16

        let dynArray : IRuntime -> unit =
            Harness.equivalence Sh.dynArrFrag (fun i ->
                let a : V3f[] = [| V3f(0.7f, 0.1f, 0.2f); V3f(0.1f, 0.7f, 0.2f); V3f(0.2f, 0.1f, 0.7f) |]
                [ Symbol.Create "HeapVArr", (AVal.constant a :> IAdaptiveValue)
                  Symbol.Create "HeapIdx",  (AVal.constant (i % 3) :> IAdaptiveValue) ]) 16

        let nested : IRuntime -> unit =
            Harness.equivalence Sh.nestFrag (fun i ->
                let n : HeapNest =
                    { NA = { RA = V3f(0.1f * float32 (i % 7), 0.4f, 0.2f); RB = 0.04f * float32 (i % 5); RC = V2f(0.1f, 0.2f) }
                      NB = Arr<N<2>, V2f>([| V2f(0.2f, 0.05f * float32 (i % 6)); V2f(0.1f, 0.15f) |]) }
                [ Symbol.Create "HeapNest", (AVal.constant n :> IAdaptiveValue) ]) 16

        let union : IRuntime -> unit =
            Harness.equivalence Sh.duFrag (fun i ->
                let du = if i % 2 = 0 then Solid (V3f(0.1f * float32 (i % 9), 0.6f, 0.2f))
                         else Scaled (0.05f * float32 (i % 5), V2f(0.3f, 0.4f))
                [ Symbol.Create "HeapDU", (AVal.constant du :> IAdaptiveValue) ]) 16

    let tests (backend : Backend) =
        [ "Integral / C4f / bool leaves", Cases.leaves
          "Record",                       Cases.record
          "V3f array",                    Cases.v3fArray
          "Record array",                 Cases.recordArray
          "Dynamic array index",          Cases.dynArray
          "Nested record",                Cases.nested
          "Discriminated union",          Cases.union
          "Churn (incremental add/remove)", Harness.churn
          "Per-draw value update",          Harness.valueUpdate
          "Bucketing (2 effects)",          Harness.bucketing
          "Derived ModelViewProjTrafo",     Harness.derived
          "Resource reclamation",           Harness.resourceReclaim
          "Bindless textures",              Harness.textures
          "Bindless Sampler2dArray",        Harness.textureArray
          "Heterogeneous geometry",         Harness.heterogeneousGeometry
          "Texture atlas fallback",         Harness.texturesAtlas
          "Int + matrix attributes",        Harness.attributes ]
        |> prepareCases backend "Heap uniforms"
