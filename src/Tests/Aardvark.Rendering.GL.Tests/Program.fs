open Aardvark.Rendering.GL.Tests

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.Rendering.GL
open OpenTK.Graphics.OpenGL4
open Aardvark.Application.WinForms

let testCompile() =
    use runtime = new Runtime()
    let ctx = new Context(runtime, false)
    runtime.Context <- ctx

    let signature =
        runtime.CreateFramebufferSignature(
            1,
            [
                DefaultSemantic.Colors, RenderbufferFormat.Rgba8
                DefaultSemantic.Depth, RenderbufferFormat.Depth24Stencil8
            ]
        )

    let callInfo = 
        DrawCallInfo(
            FaceVertexCount = 6,
            InstanceCount = 1
        )

    let surface =
        runtime.PrepareEffect(
            signature,
            [
                DefaultSurfaces.constantColor C4f.Red |> toEffect
            ]
        )

    let uniforms (t : V3d) =
        UniformProvider.ofList [
            Symbol.Create "ModelTrafo", Mod.constant (Trafo3d.Translation t) :> IMod
            Symbol.Create "ViewProjTrafo", Mod.constant Trafo3d.Identity :> IMod
        ]

    let attributes =
        AttributeProvider.ofList [
            DefaultSemantic.Positions, [| V3f(-1,-1,0); V3f(1,-1,0); V3f(1,1,0); V3f(1,-1,0); V3f(1,1,0); V3f(-1,1,0) |] :> Array
        ]

    let prototype =
        {
            Id = newId()
            AttributeScope = Ag.emptyScope
            
            IsActive            = Mod.constant true
            RenderPass          = RenderPass.main
            
            DrawCallInfos       = Mod.constant [ callInfo ]
            IndirectBuffer      = null
            Mode                = IndexedGeometryMode.TriangleList
        

            Surface             = Surface.Backend (surface :> ISurface)
                      
            DepthTest           = Mod.constant DepthTestMode.LessOrEqual
            CullMode            = Mod.constant CullMode.None
            BlendMode           = Mod.constant BlendMode.None
            FillMode            = Mod.constant FillMode.Fill
            StencilMode         = Mod.constant StencilMode.Disabled
            
            Indices             = None
            InstanceAttributes  = AttributeProvider.Empty
            VertexAttributes    = attributes
            
            Uniforms            = uniforms V3d.Zero

            ConservativeRaster  = Mod.constant false
            Multisample         = Mod.constant true

            Activate            = fun () -> { new IDisposable with member x.Dispose() = () }
            WriteBuffers        = None
        }

    let framebuffer = runtime.CreateFramebuffer(signature, Mod.constant(V2i(1024, 1024)))
    framebuffer.Acquire()

    let objects =
        Array.init (1 <<< 20) (fun i -> { RenderObject.Clone(prototype) with Uniforms = uniforms (V3d(i,0,0)) } :> IRenderObject )


    let fbo = framebuffer.GetValue()

    let set = ASet.ofArray (Array.take (1 <<< 16) objects)
    let commands = AList.ofArray (Array.take (1 <<< 16) objects |> Array.map RenderCommand.Render) 

//    Log.line "starting"
//    while true do
//        //let task = runtime.CompileRender(signature, BackendConfiguration.Default, set)
//        let task = runtime.Compile(signature, commands)
//        task.Run(RenderToken.Empty, fbo)
//        //task.Dispose()


    let log = @"C:\Users\Schorsch\Desktop\perfOld.csv"

    for cnt in 1000 .. 1000 .. 100000 do
        let set = ASet.ofArray (Array.take cnt objects) 
        //let commands = AList.ofArray (Array.take cnt objects |> Array.map RenderCommand.Render) 

        Log.startTimed "compile %d" cnt
        let sw = System.Diagnostics.Stopwatch.StartNew()
        //let task = runtime.Compile(signature, commands)
        let task = runtime.CompileRender(signature, BackendConfiguration.Default, set)
        task.Run(RenderToken.Empty, fbo)
        sw.Stop()
        Log.stop()
        task.Dispose()
        System.IO.File.AppendAllLines(log, [sprintf "%d:%d" cnt sw.MicroTime.TotalNanoseconds])


    ()

let clearTexture(runtime : Aardvark.Rendering.GL.Runtime, texture : IBackendTexture, color : C4f) =
    
    using runtime.Context.ResourceLock (fun _ ->

        // create temporary fbo
        let fbo = GL.GenFramebuffer()
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo)
        GL.FramebufferTexture(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, unbox<int> texture.Handle, 0)
        GL.Check "[GL] ClearTexture: could not create FramebufferTexture"
        
        // perform clear
        GL.ClearColor(color.R, color.G, color.B, color.A)
        GL.Clear(ClearBufferMask.ColorBufferBit)
        GL.Check "[GL] ClearTexture: could not clear texture"

        // unbind and delete of fbo
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0)
        GL.Check "[GL] ClearTexture: could not unbind Framebuffer"
        GL.DeleteFramebuffer(fbo)
        GL.Check "[GL] ClearTexture: could not delete FramebufferTexture"
    )

let testDownloadSlice() = 
    let app = new OpenGlApplication(false, true)
    let runtime = app.Runtime
    let texRt = runtime :> ITextureRuntime
    let tex = texRt.CreateTextureArray(V2i(222,333), TextureFormat.Rgba8, 5, 1, 5)

    clearTexture(runtime, tex, C4f.Red)

    // will produce error:
    //1: ERROR: [GL:65537] GL_INVALID_OPERATION error generated. Texture name does not refer to a texture object generated by OpenGL.
    //0: WARNING: InvalidOperation: could not GetTextureSubImage
    runtime.Download(tex, 0, 0).SaveAsImage("C:\\Debug\\slice0.bmp")

    clearTexture(runtime, tex, C4f.Blue)

    runtime.Download(tex, 0, 1).SaveAsImage("C:\\Debug\\slice1.bmp")

    ()

[<EntryPoint>]
let main args =
    Aardvark.Base.Ag.initialize()
    Aardvark.Init()
    //testCompile()

    testDownloadSlice()

    //RenderingTests.``[GL] concurrent group change``()
    //RenderingTests.``[GL] memory leak test``()
    //MultipleStageAgMemoryLeakTest.run() |> ignore

    //PerformanceTests.PerformanceTest.runConsole()
    //Examples.PerformanceTest.run()
    //PerformanceTests.StartupPerformance.runConsole args
    //PerformanceTests.IsActiveFlagPerformance.run args
    //UseTest.bla()
    0
