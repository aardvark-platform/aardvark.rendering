open Aardvark.Rendering.GL.Tests

open System
open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Rendering
open Aardvark.Rendering.GL
open OpenTK.Graphics.OpenGL4
open Aardvark.Application.WinForms

let testCompile() =
    use runtime = new Runtime()
    let ctx = new Context(runtime, false, Array.init 2 (fun _ -> ContextHandleOpenTK.create false), (fun () -> ContextHandleOpenTK.create false))
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
            Symbol.Create "ModelTrafo", AVal.constant (Trafo3d.Translation t) :> IAdaptiveValue
            Symbol.Create "ViewProjTrafo", AVal.constant Trafo3d.Identity :> IAdaptiveValue
        ]

    let attributes =
        AttributeProvider.ofList [
            DefaultSemantic.Positions, [| V3f(-1,-1,0); V3f(1,-1,0); V3f(1,1,0); V3f(1,-1,0); V3f(1,1,0); V3f(-1,1,0) |] :> Array
        ]

    let prototype =
        {
            Id = newId()
            AttributeScope = Ag.Scope.Root
            
            IsActive            = AVal.constant true
            RenderPass          = RenderPass.main
            
            DrawCalls           = Direct(AVal.constant [ callInfo ])
            Mode                = IndexedGeometryMode.TriangleList
        

            Surface             = Surface.Backend (surface :> ISurface)
                      
            DepthTest           = AVal.constant DepthTestMode.LessOrEqual
            DepthBias           = AVal.constant (DepthBiasState(0.0, 0.0, 0.0))
            CullMode            = AVal.constant CullMode.None
            FrontFace           = AVal.constant WindingOrder.CounterClockwise
            BlendMode           = AVal.constant BlendMode.None
            FillMode            = AVal.constant FillMode.Fill
            StencilMode         = AVal.constant StencilMode.Disabled
            
            Indices             = None
            InstanceAttributes  = AttributeProvider.Empty
            VertexAttributes    = attributes
            
            Uniforms            = uniforms V3d.Zero

            ConservativeRaster  = AVal.constant false
            Multisample         = AVal.constant true

            Activate            = fun () -> { new IDisposable with member x.Dispose() = () }
            WriteBuffers        = None
        }

    let framebuffer = runtime.CreateFramebuffer(signature, AVal.constant(V2i(1024, 1024)))
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

let testTextureCubeArray() =
    let app = new OpenGlApplication(false, true)
    let runtime = app.Runtime
    let texRt = runtime :> ITextureRuntime

    let cta = texRt.CreateTextureCubeArray(128, TextureFormat.Rgba8, 1, 1, 4)

    let cube0View = texRt.CreateTextureView(cta, Range1i(0,0), Range1i(0,5), false) // create TextureCube view of [0]
    let cube1View = texRt.CreateTextureView(cta, Range1i(0,0), Range1i(6,11), false) // create TextureCube view of [1]
    let cube0Face0View = texRt.CreateTextureView(cta, Range1i(0,0), Range1i(0,0), false) // create Texture2d view of cube[0].face[0]
    let cube0Face1View = texRt.CreateTextureView(cta, Range1i(0,0), Range1i(1,1), false) // create Texture2d view of cube[0].face[1]
    let cube1Face2View = texRt.CreateTextureView(cta, Range1i(0,0), Range1i(8,8), false) // create Texture2d view of cube[1].face[2]
    let cube2Face0To2View = texRt.CreateTextureView(cta, Range1i(0,0), Range1i(12,14), true) // create Texture2dArray of range 
    let cube2Face0To5View = texRt.CreateTextureView(cta, Range1i(0,0), Range1i(12,17), true) // create Texture2dArray of range spanning cube
    let cube0to1FacesView = texRt.CreateTextureView(cta, Range1i(0,0), Range1i(0,11), true) // create Texture2dArray of complete cube 0 & 1
    let cubeArrayFacesView = texRt.CreateTextureView(cta, Range1i(0,0), Range1i(0,23), true) // create Texture2dArray of complete TextureCubeArray
    //let cube0Cube1ArrayView = texRt.CreateTextureView(cta, Range1i(0,0), Range1i(0,11), true) // API does not allow to create TextureCubeArray sub-range
     
    texRt.DeleteTexture(cta)
    texRt.DeleteTexture(cube0View)
    texRt.DeleteTexture(cube1View)
    texRt.DeleteTexture(cube0Face0View)
    texRt.DeleteTexture(cube0Face1View)
    texRt.DeleteTexture(cube1Face2View)
    texRt.DeleteTexture(cube2Face0To2View)
    texRt.DeleteTexture(cube2Face0To5View)
    texRt.DeleteTexture(cube0to1FacesView)
    texRt.DeleteTexture(cubeArrayFacesView)
    


[<EntryPoint>]
let main args =
    Aardvark.Init()
    //testCompile()

    RadixSortTest.run()


    //testTextureCubeArray()

    //RenderingTests.``[GL] concurrent group change``()
    //RenderingTests.``[GL] memory leak test``()
    //MultipleStageAgMemoryLeakTest.run() |> ignore

    //PerformanceTests.PerformanceTest.runConsole()
    //Examples.PerformanceTest.run()
    //PerformanceTests.StartupPerformance.runConsole args
    //PerformanceTests.IsActiveFlagPerformance.run args
    //UseTest.bla()
    0
