namespace Aardvark.Application.OpenVR

open Aardvark.Base

open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.Rendering.GL
open Aardvark.Application
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics
open System.Runtime.InteropServices

module StereoShader =
    open FShade

    type Vertex = 
        {
            [<Layer>]           layer   : int
            [<Position>]        pos     : V4f
            [<WorldPosition>]   wp      : V4f
            [<Normal>]          n       : V3f
            [<BiNormal>]        b       : V3f
            [<Tangent>]         t       : V3f
            [<Color>]           c       : V4f
            [<TexCoord>]        tc      : V2f
        }

    let flip (v : Vertex) =
        vertex {
            let version : int = uniform?Version
            let zero = 1.0E-10f * float32 (version % 2)
            return { v with pos = V4f(1.0f, -1.0f, 1.0f + zero, 1.0f) * v.pos }
        }


    type HiddenVertex =
        {
            [<Position>]
            pos : V4f

            [<Semantic("EyeIndex"); Interpolation(InterpolationMode.Flat)>]
            eyeIndex : int

            [<Layer>]
            layer : int
        }

    let hiddenAreaFragment (t : HiddenVertex) =
        fragment {
            if t.layer <> t.eyeIndex then
                discard()

            return V4f.IIII
        }

type private DummyObject() =
    inherit AdaptiveObject()

type OpenGlVRApplicationLayered(debug: IDebugConfig, adjustSize: V2i -> V2i,
                                [<Optional; DefaultParameterValue(1)>] samples: int)  =
    inherit VrRenderer(adjustSize)

    let app = new Aardvark.Application.Slim.OpenGlApplication(true, debug)
    let runtime = app.Runtime
    let ctx = runtime.Context

    let mutable dTex = Unchecked.defaultof<Texture>
    let mutable cTex = Unchecked.defaultof<Texture>
    let mutable fbo = Unchecked.defaultof<IFramebuffer>
    let mutable info = Unchecked.defaultof<VrRenderInfo>
    let mutable fTexl = Unchecked.defaultof<Texture>
    let mutable fTexr = Unchecked.defaultof<Texture>

    let start = System.DateTime.Now
    let sw = System.Diagnostics.Stopwatch.StartNew()
    let time = AVal.custom(fun _ -> start + sw.Elapsed)
   
    let framebufferSignature = 
        runtime.CreateFramebufferSignature(
            SymDict.ofList [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8
            ],
            samples,
            2,
            Set.ofList [
                "ViewTrafo"; "ProjTrafo"; 
                "ModelViewTrafo"; "ViewProjTrafo"; 
                "ModelViewProjTrafo"; 
                "ViewTrafoInv"; "ProjTrafoInv"; 
                "ModelViewTrafoInv"; "ViewProjTrafoInv"; 
                "ModelViewProjTrafoInv"
            ]
        )
        
    let caller = DummyObject()

    let version = AVal.init 0
    let tex = AVal.custom (fun _ -> fTexl :> ITexture)
    
    let keyboard = new EventKeyboard()
    let mouse = new EventMouse(false)
    
    let beforeRender = Event<unit>()
    let afterRender = Event<unit>()
    let mutable loaded = false
    
    //let renderCtx = ContextHandleOpenTK.create debug


    let clearColor = AVal.init C4f.Black
    let mutable clearTask = RenderTask.empty
    let mutable hiddenTask = RenderTask.empty
    let mutable userTask = RenderTask.empty

    let compileHidden (m : IndexedGeometry) =
        let writeStencil =
            { StencilMode.None with
                Pass = StencilOperation.Replace
                Fail = StencilOperation.Replace
                DepthFail = StencilOperation.Replace
                Comparison = ComparisonFunction.Always
                Reference = 1 }

        let sg =
            Sg.ofIndexedGeometry m
                |> Sg.shader {
                    do! StereoShader.hiddenAreaFragment
                }
                |> Sg.stencilMode' writeStencil
                |> Sg.writeBuffers' (Set.ofList [WriteBuffer.Stencil])

        hiddenTask <- runtime.CompileRender(framebufferSignature, sg.RenderObjects(Ag.Scope.Root))
        hiddenTask.Name <- "Window Task (Hidden)"
        
    let compileClear () =
        clearTask <- runtime.CompileClear(framebufferSignature, clearColor, AVal.constant 1.0)
        clearTask.Name <- "Window Task (Clear)"

    member x.Version = version :> aval<_>
    member x.Texture = tex
    
    member x.FramebufferSignature = framebufferSignature
    member x.Runtime = app.Runtime
    member x.Sizes = AVal.constant x.DesiredSize
    member x.Samples = samples
    member x.Time = time

    
    member x.RenderTask
        with set (t : IRenderTask) =
            if isNull t.Name then t.Name <- "Window Task"
            userTask <- t
        and get () = userTask

    override x.Use(f : unit -> 'a) =
        Operators.using ctx.ResourceLock (fun _ -> f())

    //override x.Handedness 
    //    with get() = Trafo3d.FromBasis(V3d.IOO, -V3d.OOI, -V3d.OIO, V3d.Zero)

    override x.OnLoad(i : VrRenderInfo) =
        //renderCtx.MakeCurrent()
        //ctx.CurrentContextHandle <- Some renderCtx
        Operators.using ctx.ResourceLock (fun _ ->

            info <- i

            if loaded then
                ctx.Delete (unbox<Framebuffer> fbo)
                ctx.Delete fTexl
                ctx.Delete fTexr
                ctx.Delete dTex
                ctx.Delete cTex
            else
                compileHidden x.HiddenAreaMesh
                compileClear()


            let nTex = ctx.CreateTexture2DArray(info.framebufferSize, 2, 1, TextureFormat.Rgba8, samples)
            nTex.Name <- "Color Attachment (Window)"

            let nDepth = ctx.CreateTexture2DArray(info.framebufferSize, 2, 1, TextureFormat.Depth24Stencil8, samples)
            nDepth.Name <- "Depth / Stencil Attachment (Window)"

            let nfTexl = ctx.CreateTexture2D(info.framebufferSize, 1, TextureFormat.Rgba8, 1)
            nfTexl.Name <- "Resolved Left Color Attachment (Window)"

            let nfTexr = ctx.CreateTexture2D(info.framebufferSize, 1, TextureFormat.Rgba8, 1)
            nfTexr.Name <- "Resolved Right Color Attachment (Window)"

            let nFbo =
                runtime.CreateFramebuffer(
                    framebufferSignature,
                    [
                        DefaultSemantic.Colors, nTex.[TextureAspect.Color, 0, *] :> IFramebufferOutput
                        DefaultSemantic.DepthStencil, nDepth.[TextureAspect.Depth, 0, *] :> IFramebufferOutput
                    ]
                )

            dTex <- nDepth
            cTex <- nTex
            fTexl <- nfTexl
            fTexr <- nfTexr
            fbo <- nFbo


            let lTex = VrTexture.OpenGL(fTexl.Handle)
            let rTex = VrTexture.OpenGL(fTexr.Handle)
            loaded <- true
        

            lTex,rTex
        )

    override x.Render() =
        if loaded then
            Operators.using ctx.ResourceLock (fun _ ->
                ctx.PushDebugGroup("Swapchain")
  
                let output = OutputDescription.ofFramebuffer fbo

                caller.EvaluateAlways AdaptiveToken.Top (fun t ->
                    clearTask.Run(t, RenderToken.Empty, output)
                    hiddenTask.Run(t, RenderToken.Empty, output)
                    userTask.Run(t, RenderToken.Empty, output) 
                )

                GL.Sync()

                if samples > 1 then
                    runtime.ResolveMultisamples(cTex.[TextureAspect.Color, 0, 0], fTexl, V2i.Zero, V2i.Zero, cTex.Size.XY)
                    runtime.ResolveMultisamples(cTex.[TextureAspect.Color, 0, 1], fTexr, V2i.Zero, V2i.Zero, cTex.Size.XY)
                    ctx.PopDebugGroup()
                else
                    //runtime.Copy(cTex.[TextureAspect.Color, 0, *], fTex.[TextureAspect.Color, 0, *])
                    ctx.PopDebugGroup()
                    failwith "not implemented"
            )
                
        transact (fun () -> time.MarkOutdated(); version.Value <- version.Value + 1)


    override x.Release() =
        clearTask.Dispose()
        hiddenTask.Dispose()
        userTask.Dispose()

        clearTask <- RenderTask.empty
        hiddenTask <- RenderTask.empty
        userTask <- RenderTask.empty

        ctx.Delete (unbox<Framebuffer> fbo)
        ctx.Delete dTex
        ctx.Delete cTex
        ctx.Delete fTexl
        ctx.Delete fTexr

        app.Dispose()

        
        Log.warn "[GL] TODO: check cleanup"
        ()
        
    member x.BeforeRender = beforeRender
    member x.AfterRender = afterRender

    member x.SubSampling
        with get() = 1.0
        and set (v : float) =
            let adjust (s : V2i) : V2i = max V2i.II (V2i (V2d s * v))
            base.AdjustSize <- adjust

    interface IRenderTarget with
        member x.Runtime = app.Runtime :> IRuntime
        member x.Sizes = AVal.constant x.DesiredSize
        member x.Samples = samples
        member x.FramebufferSignature = x.FramebufferSignature
        member x.RenderTask
            with get() = x.RenderTask
            and set t = x.RenderTask <- t
        member x.SubSampling
            with get() = x.SubSampling
            and set v = x.SubSampling <- v
        member x.Time = time
        member x.BeforeRender = beforeRender.Publish
        member x.AfterRender = afterRender.Publish

    interface IRenderControl with
        member x.Cursor
            with get() = Cursor.Default
            and set c = ()

        member x.Keyboard = keyboard :> IKeyboard
        member x.Mouse = mouse :> IMouse

    interface IRenderWindow with
        member x.Run() = x.Run()

    new(debug: IDebugConfig, [<Optional; DefaultParameterValue(1)>] samples: int) =
        new OpenGlVRApplicationLayered(debug, id, samples)

    new(debug: bool, adjustSize: V2i -> V2i, [<Optional; DefaultParameterValue(1)>] samples: int) =
        new OpenGlVRApplicationLayered(DebugLevel.ofBool debug, adjustSize, samples)

    new([<Optional; DefaultParameterValue(false)>] debug: bool, [<Optional; DefaultParameterValue(1)>] samples: int) =
        new OpenGlVRApplicationLayered(debug, id, samples)