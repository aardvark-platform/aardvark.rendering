namespace HeapSpike

// Headless benchmark: classic (N render objects) vs heap (N -> B bucket ROs)
// across an N-sweep. Same inputs, same shader; the heap path additionally
// runs Heap.ofRenderObjects. Measures:
//   * setup:  compile + first render (build command stream + GPU resources)
//   * frame:  avg ms/frame with EVERY object's model trafo changing each
//             frame (classic -> N uniform-buffer updates + N draws; heap ->
//             one arena upload + B indirect draws)
//   * draws:  draw-call count reaching the command stream (N vs B)

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open FSharp.Data.Adaptive
open Aardvark.Application
open FShade
open System.Diagnostics

module Bench =

    let run () =
        Aardvark.Init()
        use app = new Aardvark.Rendering.Vulkan.HeadlessVulkanApplication()
        let runtime = app.Runtime

        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8
            ]

        let size = AVal.constant (V2i(1024, 1024))

        // shared geometry
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.6)) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let posBV = BufferView(AVal.constant (ArrayBuffer(positions) :> IBuffer), typeof<V3f>)
        let norBV = BufferView(AVal.constant (ArrayBuffer(normals)   :> IBuffer), typeof<V3f>)
        let idxBV = BufferView(AVal.constant (ArrayBuffer(index)     :> IBuffer), typeof<int>)
        let vattrs = AttributeProvider.ofList [ DefaultSemantic.Positions, posBV; DefaultSemantic.Normals, norBV ]

        // fixed camera
        let view = CameraView.lookAt (V3d(0.0, -1.0, 1.0) * 90.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 5000.0 1.0 |> Frustum.projTrafo
        let viewProj = AVal.constant (view * proj)

        let effect = Effect.compose [ Effect.ofFunction Shaders.shade; Effect.ofFunction Shaders.shadeFrag ]
        let palette = [| C4f.Red; C4f.LawnGreen; C4f.DodgerBlue; C4f.Gold; C4f.Magenta; C4f.Cyan |]

        let trafoAt (s : int) (i : int) (phase : float) =
            let x = i % s
            let y = i / s
            (Trafo3d.Translation(float (x - s/2) * 1.2, float (y - s/2) * 1.2, 0.0) * Trafo3d.RotationZ phase).Forward
            |> M44f.op_Explicit

        let buildInputs (n : int) =
            let s = int (ceil (sqrt (float n)))
            let trafos = Array.init n (fun i -> AVal.init (trafoAt s i 0.0))
            let ros =
                Array.init n (fun i ->
                    let ro = RenderObject()
                    ro.Surface   <- Surface.Effect effect
                    ro.Mode      <- IndexedGeometryMode.TriangleList
                    ro.VertexAttributes <- vattrs
                    ro.Indices   <- Some idxBV
                    ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
                    ro.Uniforms  <-
                        UniformProvider.ofList [
                            Symbol.Create "HeapModelTrafo", (trafos.[i] :> IAdaptiveValue)
                            Symbol.Create "HeapColor",      (AVal.constant (palette.[i % palette.Length].ToV4f()) :> IAdaptiveValue)
                            Symbol.Create "ViewProjTrafo",  (viewProj :> IAdaptiveValue)
                        ]
                    ro :> IRenderObject)
            ros, trafos, s

        // dirtyPerFrame = how many objects' model trafos change each frame.
        let measure (transform : aset<IRenderObject> -> aset<IRenderObject>) (n : int) (dirtyPerFrame : int) =
            let ros, trafos, s = buildInputs n
            use task = runtime.CompileRender(signature, transform (ASet.ofArray ros))
            let tex = RenderTask.renderToColor size task
            tex |> AVal.force |> ignore   // build

            let dirty = min dirtyPerFrame n
            let bump (f : int) =
                transact (fun () ->
                    for j in 0 .. dirty - 1 do
                        let i = (f * dirty + j) % n
                        trafos.[i].Value <- trafoAt s i (float f * 0.02))

            for f in 1 .. 3 do bump f; tex |> AVal.force |> ignore   // warmup

            let k = 60
            let sw = Stopwatch.StartNew()
            for f in 1 .. k do bump (f + 3); tex |> AVal.force |> ignore
            sw.Stop()
            sw.Elapsed.TotalMilliseconds / float k

        let heap = Heap.ofRenderObjects (runtime.CreateHeapStorage())

        printfn ""
        printfn "  N    | ALL changed/frame      | 16 changed/frame       | draws"
        printfn "       | classic   heap  speedup| classic   heap  speedup| C -> H"
        printfn "-------+------------------------+------------------------+--------"
        for n in [ 128; 512; 2048; 8192 ] do
            let cFull = measure id n n
            let hFull = measure heap n n
            let cSparse = measure id n 16
            let hSparse = measure heap n 16
            let b = Heap.lastBucketCount
            printfn "%6d | %7.2f %6.2f  %5.1fx | %7.2f %6.2f  %5.1fx | %d -> %d"
                n cFull hFull (cFull / hFull) cSparse hSparse (cSparse / hSparse) n b
        printfn ""
