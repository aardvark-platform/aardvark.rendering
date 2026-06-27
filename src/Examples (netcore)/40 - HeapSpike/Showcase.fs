namespace HeapSpike

// Flashy showcase: ~20k objects, several visually-distinct shaders (solid, bindless
// textured, toon, normal/bump, fresnel-rim) over shared geometry (box/sphere/torus).
// The input is a SCENEGRAPH driven by the DefaultCameraController; SPACE toggles
// between STANDARD (one draw call per object) and HEAP (the same scene's render
// objects collapsed to one indirect multidraw per effect). Shaders read the
// standard ModelViewProjTrafo — provided by the Sg in standard mode, synthesised
// by the heap's derived-uniform rule (ViewProjTrafo * ModelTrafo) in heap mode.

open Aardvark.Base
open Aardvark.Base.Ag
open Aardvark.Rendering
open Aardvark.Rendering.Text
open Aardvark.SceneGraph
open Aardvark.Application
open FSharp.Data.Adaptive

module Showcase =

    [<Literal>]
    let TexCount = 16

    module S =
        open FShade
        type V =
            { [<Position>]                                                 pos : V4f
              [<Normal>]                                                   n   : V3f
              [<Semantic("TexCoord")>]                                     tc  : V2f
              [<Color>]                                                    c   : V4f
              [<Semantic("WorldPos")>]                                     wp  : V3f }

        let shade (v : V) =
            vertex {
                let mvp : M44f = uniform?ModelViewProjTrafo
                let m   : M44f = uniform?ModelTrafo
                let col : V4f  = uniform?HeapColor
                return { v with pos = mvp * v.pos; n = m.TransformDir v.n
                                tc = v.pos.XY * 2.0f + V2f(0.6f, 0.6f); c = col; wp = (m * v.pos).XYZ }
            }

        let private L = V3f(0.4f, 0.7f, 0.6f) |> Vec.normalize

        let solid (v : V) =
            fragment {
                let nn = Vec.normalize v.n
                return V4f(v.c.XYZ * (0.2f + 0.8f * max 0.0f (Vec.dot nn L)), 1.0f)
            }

        // per-object texture: the heap auto-routes this single sampler to a bindless
        // per-type array (desktop Vulkan) or a shared atlas page (MoltenVK / Vk-1.0 / GL).
        let private diffuse =
            sampler2d {
                texture uniform?DiffuseTexture
                filter Filter.MinMagMipLinear
                addressU WrapMode.Wrap
                addressV WrapMode.Wrap
            }
        let textured (v : V) =
            fragment {
                let nn = Vec.normalize v.n
                let d = 0.35f + 0.65f * max 0.0f (Vec.dot nn L)
                return V4f(diffuse.Sample(v.tc).XYZ * d, 1.0f)
            }

        let toon (v : V) =
            fragment {
                let nn = Vec.normalize v.n
                let bands = floor (max 0.0f (Vec.dot nn L) * 4.0f) / 4.0f
                return V4f(v.c.XYZ * (0.25f + 0.8f * bands), 1.0f)
            }

        let bump (v : V) =
            fragment {
                let nn = Vec.normalize v.n
                let detail = V3f(sin (v.tc.X * 50.0f) * 0.45f, sin (v.tc.Y * 50.0f) * 0.45f, 1.0f) |> Vec.normalize
                let pn = Vec.normalize (nn + detail * 0.6f)
                let d = 0.2f + 0.8f * max 0.0f (Vec.dot pn L)
                let s = pown (max 0.0f (Vec.dot pn L)) 32
                return V4f(v.c.XYZ * d + V3f.III * (s * 0.6f), 1.0f)
            }

        let rim (v : V) =
            fragment {
                let nn = Vec.normalize v.n
                let cam : V3f = uniform?CameraLocation
                let view = Vec.normalize (cam - v.wp)
                let f = pown (1.0f - max 0.0f (Vec.dot nn view)) 3
                let d = 0.2f + 0.7f * max 0.0f (Vec.dot nn L)
                return V4f(v.c.XYZ * d + V3f(0.3f, 0.65f, 1.0f) * f, 1.0f)
            }

        // final stage that overrides output alpha to 0.5 — combined with Sg.transparent below
        // this exercises the OIT pipeline end-to-end and the heap's IsTransparent bucket partition.
        let setAlpha (v : Effects.Vertex) =
            fragment { return V4f(v.c.XYZ, 0.5f) }

        // five distinct effects (-> five buckets in heap mode), each with the alpha override
        // appended so every shader returns alpha=0.5
        let effects =
            [| solid; textured; toon; bump; rim |]
            |> Array.map (fun frag -> Effect.compose [ Effect.ofFunction shade; Effect.ofFunction frag; Effect.ofFunction setAlpha ])

    let private mkTexture (i : int) : ITexture =
        let cols = [| C3b(235,70,70); C3b(70,205,90); C3b(70,130,235); C3b(235,205,55)
                      C3b(215,70,215); C3b(55,215,215); C3b(235,150,55); C3b(190,190,190) |]
        let col = cols.[i % cols.Length]
        let img = PixImage<byte>(Col.Format.RGBA, V2i(64, 64))
        img.GetMatrix<C4b>().SetByIndex(fun (idx : int64) ->
            let x = int idx % 64
            let y = int idx / 64
            if ((x / 8) + (y / 8)) % 2 = 0 then C4b(col) else C4b(20uy, 20uy, 30uy, 255uy)) |> ignore
        PixTexture2d(img) :> ITexture

    // shared per-shape geometry (one BufferView set per shape -> heap dedups them)
    type private Shape = { pos : BufferView; nor : BufferView; idx : BufferView; count : int }
    let private mkShape (ig : IndexedGeometry) =
        let g = ig.ToIndexed()
        let pos = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let nor = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let idx = g.IndexArray |> unbox<int[]>
        { pos = BufferView(AVal.constant (ArrayBuffer pos :> IBuffer), typeof<V3f>)
          nor = BufferView(AVal.constant (ArrayBuffer nor :> IBuffer), typeof<V3f>)
          idx = BufferView(AVal.constant (ArrayBuffer idx :> IBuffer), typeof<int>)
          count = idx.Length }

    let run (record : bool) =
        Aardvark.Init()
        // VALIDATE=1 turns on the Vulkan validation layer + synchronization validation
        // (desktop only) to surface any sync hazards / VUID errors in our usage.
        // SHADERLOG=1 prints every compiled GLSL/SPIR-V shader (PrintShaderCode=true).
        use app =
            if System.Environment.GetEnvironmentVariable "VALIDATE" = "1" then
                let vcfg =
                    { Aardvark.Rendering.Vulkan.DebugConfig.Minimal with
                        ValidationLayer =
                            Some { Aardvark.Rendering.Vulkan.ValidationLayerConfig.Standard with
                                     SynchronizationValidation = true
                                     BestPracticesValidation   = true } }   // best-practices = control: if these fire, the config path (incl. sync-val) is live
                new Aardvark.Application.Slim.VulkanApplication(vcfg :> IDebugConfig)
            elif System.Environment.GetEnvironmentVariable "SHADERLOG" = "1" then
                let vcfg = { Aardvark.Rendering.Vulkan.DebugConfig.Minimal with PrintShaderCode = true }
                new Aardvark.Application.Slim.VulkanApplication(vcfg :> IDebugConfig)
            else
                new Aardvark.Application.Slim.VulkanApplication(false)
        let runtime = app.Runtime
        // MSAA is unreliable on MoltenVK (portability subset): an 8x multisampled swapchain
        // in the secondary-command-buffer render path can GPU-hang the machine. Use the same
        // capability proxy as the heap: full MSAA only on conformant desktop Vulkan, 1x else.
        // Override with SAMPLES=<n>.
        let samples =
            match System.Environment.GetEnvironmentVariable "SAMPLES" with
            | null | "" -> if runtime.SupportsUnboundedSamplerArrays then 8 else 1
            | s -> int s
        let win = app.CreateGameWindow(samples = samples)
        Log.line "showcase: MSAA samples = %d" samples

        // On backends without real bindless (MoltenVK / Vulkan 1.0 / GL) the heap auto-routes
        // per-object textures through a shared atlas page. FORCE_ATLAS=1 exercises that path
        // even on desktop Vulkan (where unbounded sampler arrays are available).
        if System.Environment.GetEnvironmentVariable "FORCE_ATLAS" = "1" then
            Heap.forceAtlas <- true
            Log.line "showcase: FORCE_ATLAS -> heap routes per-object textures through the atlas"

        // camera: DefaultCameraController interactively; smooth auto-orbit for recording
        let initialView = CameraView.lookAt (V3d(70.0, 70.0, 55.0)) V3d.Zero V3d.OOI
        // STATIC=1 -> fully constant camera (no win.Time dependency): isolates the
        // heap render loop's steady-state allocation from camera/animation churn.
        let isStatic = System.Environment.GetEnvironmentVariable "STATIC" = "1"
        let cameraView =
            // depend on win.Time so the window keeps re-rendering every frame, but
            // return the SAME camera object -> heap redraws with no new uploads.
            if isStatic then win.Time |> AVal.map (fun _ -> initialView)
            elif record then
                let sw = System.Diagnostics.Stopwatch.StartNew()
                win.Time |> AVal.map (fun _ ->
                    let t = sw.Elapsed.TotalSeconds
                    let r = 50.0 + 30.0 * sin (t * 0.22)
                    let a = t * 0.32
                    CameraView.lookAt (V3d(cos a * r, sin a * r, 20.0 + 14.0 * sin (t * 0.4))) V3d.Zero V3d.OOI)
            else
                DefaultCameraController.control win.Mouse win.Keyboard win.Time initialView
        let frustum = win.Sizes |> AVal.map (fun s -> Frustum.perspective 70.0 0.1 3000.0 (float s.X / float s.Y))
        let camLoc = cameraView |> AVal.map (fun cv -> V3f cv.Location)
        let textures : ITexture[] = Array.init TexCount mkTexture

        let shapes =
            [| mkShape (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.7)) C4b.White)
               mkShape (IndexedGeometryPrimitives.Sphere.solidSubdivisionSphere (Sphere3d(V3d.Zero, 0.45)) 2 C4b.White)
               mkShape (IndexedGeometryPrimitives.solidTorus (Torus3d(V3d.Zero, V3d.OOI, 0.4, 0.16)) C4b.White 16 12) |]
        let effects = S.effects
        let palette = [| C4f.Red; C4f.LawnGreen; C4f.DodgerBlue; C4f.Gold; C4f.Magenta; C4f.Cyan; C4f.Orange; C4f.HotPink |]

        let rnd = RandomSystem()
        let n =
            match System.Environment.GetEnvironmentVariable "N" with
            | null | "" -> if record then 40000 else 20000
            | s -> int s
        let span = 70.0
        let objSg (i : int) : ISg =
            let s = shapes.[i % shapes.Length]
            let p = V3d(rnd.UniformDouble() - 0.5, rnd.UniformDouble() - 0.5, rnd.UniformDouble() - 0.5) * span
            let model = Trafo3d.Rotation(rnd.UniformV3dDirection(), rnd.UniformDouble() * 6.2832) * Trafo3d.Translation p
            Sg.render IndexedGeometryMode.TriangleList (DrawCallInfo(FaceVertexCount = s.count, InstanceCount = 1))
            |> Sg.vertexBuffer DefaultSemantic.Positions s.pos
            |> Sg.vertexBuffer DefaultSemantic.Normals   s.nor
            |> Sg.indexBuffer s.idx
            |> Sg.trafo' model
            |> Sg.uniform' "HeapColor" (palette.[i % palette.Length].ToV4f())
            |> Sg.texture' (Symbol.Create "DiffuseTexture") textures.[i % TexCount]
            |> Sg.effect [ effects.[i % effects.Length] ]

        // THE INPUT: a scenegraph (camera + globals applied at the top). Sg.transparent marks
        // every leaf RO with IsTransparent=true so (a) the heap partitions them into transparent
        // buckets (see HeapPool.modeKey) and (b) TransparencyRenderTask routes them through OIT.
        let scene =
            Array.init n objSg
            |> Sg.ofArray
            |> Sg.uniform "CameraLocation" camLoc
            |> Sg.viewTrafo (cameraView |> AVal.map CameraView.viewTrafo)
            |> Sg.projTrafo (frustum    |> AVal.map Frustum.projTrafo)
            |> Sg.transparent

        // standard render objects (extracted from the scenegraph)
        let extract (sg : ISg) : aset<IRenderObject> =
            let dn = Sg.DynamicNode(AVal.constant sg) :> ISg
            dn?Runtime <- runtime
            dn?RenderObjects(Ag.Scope.Root)
        let stdRos = extract scene
        // heap: the SAME scene wrapped in Sg.heap — its render objects collapse
        // into buckets / indirect draws inside the graph (Heap.ofRenderObjects)
        let heapRos = extract (Sg.heap win.FramebufferSignature scene)
        heapRos |> ASet.toAVal |> AVal.force |> ignore

        let heapMode = AVal.init true
        let ros = heapMode |> ASet.bind (fun h -> if h then heapRos else stdRos)

        // live fps (counted in AfterRender, published twice a second)
        let fps = AVal.init 0.0
        let mutable frames = 0
        let fsw = System.Diagnostics.Stopwatch.StartNew()
        win.AfterRender.Add (fun () ->
            frames <- frames + 1
            if not isStatic && fsw.Elapsed.TotalSeconds >= 0.5 then
                transact (fun () -> fps.Value <- float frames / fsw.Elapsed.TotalSeconds)
                frames <- 0; fsw.Restart())

        // overlay text (aardvark.text, screen space) — mode / draw calls / fps
        let overlayText =
            (heapMode, fps) ||> AVal.map2 (fun h f ->
                sprintf "MODE: %s\n%d objects\n%d draw calls\n%.0f fps" (if h then "HEAP" else "STANDARD") n (if h then Heap.lastBucketCount else n) f)
        let buildOverlay () =
            let trafo =
                win.Sizes |> AVal.map (fun s ->
                    let border = V2d(30.0, 24.0) / V2d s
                    let pixels = 46.0 / float s.Y
                    Trafo3d.Scale pixels *
                    Trafo3d.Scale(float s.Y / float s.X, 1.0, 1.0) *
                    Trafo3d.Translation(-1.0 + border.X, 1.0 - border.Y - pixels, -1.0))
            let font = DefaultFonts.Hack.Regular
            let cfg =
                use __ = runtime.ContextLock
                runtime.PrepareGlyphs(font, [| for c in 0 .. 255 -> char c |])
                TextConfig.create font C4b.White TextAlignment.Left false RenderStyle.NoBoundary
            Sg.textWithConfig cfg overlayText
            |> Sg.trafo trafo
            |> Sg.viewTrafo (AVal.constant Trafo3d.Identity)
            |> Sg.projTrafo (AVal.constant Trafo3d.Identity)

        win.Keyboard.DownWithRepeats.Values.Add (fun k ->
            if k = Keys.Space then transact (fun () -> heapMode.Value <- not heapMode.Value))

        if record then
            win.Fullcreen <- true   // (sic) aardvark's spelling of the fullscreen toggle

        let mainTask = runtime.CompileRender(win.FramebufferSignature, ros)
        // NOOVERLAY=1 skips the text overlay (PrepareGlyphs) — isolates whether the glyph
        // upload is what wedges MoltenVK in the windowed Release path.
        win.RenderTask <-
            if System.Environment.GetEnvironmentVariable "NOOVERLAY" = "1" then mainTask
            else RenderTask.ofList [ mainTask; runtime.CompileRender(win.FramebufferSignature, buildOverlay()) ]
        Log.warn "Showcase: %d objects, %d effects (solid/textured/toon/bump/rim). SPACE = STANDARD <-> HEAP." n effects.Length
        win.Run()
