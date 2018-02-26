namespace Examples

open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms


module TextureCombinationShader =
    open FShade
    open Aardvark.Base.Rendering.Effects

    [<LocalSize(X = 8, Y = 8)>]
    let textureComposer (weight : float) (blend : int) (inputL : FShade.Image2d<Formats.r32f>) (inputR : FShade.Image2d<Formats.r32f>) (output : FShade.Image2d<Formats.r32f>) =
        compute {
            let rc = getGlobalId().XY
            let size = inputR.Size // inputL.Size = inputR.Size = output.Size
            if rc.X < size.X && rc.Y < size.Y then
                let cl = inputL.[rc].X
                let cr = inputR.[rc].X * weight

                let c =
                    if cl < 0.0001 then cr
                    elif cr < 0.0001 then cl
                    elif blend = 1 then // Add
                        (cl + cr) |> clamp 0.0 1.0
                    elif blend = 2 then // Sub
                        (cl - cr) |> clamp 0.0 1.0
                    elif blend = 3 then // Min
                        (if cl < cr then cl else cr)
                    elif blend = 4 then // Max
                        (if cl > cr then cl else cr)
                    else // Normal
                        cr

                output.[rc] <- V4d(V3d(c), 1.0)
        }

module ImageProcessingShader =
    open FShade
    open Aardvark.Base.Rendering.Effects

    (********************************************************************************************************
     * Image Filter via compute shader
     * for now:
     * --> Box Filter
     * --> Gaussian Filter
     * --> Highpass Filter
     ********************************************************************************************************)
    [<LocalSize(X = 8, Y = 8)>]
    let box (radius : int) (input : FShade.Image2d<Formats.r32f>) (output : Image2d<Formats.r32f>) =
        compute {
            let rc = getGlobalId().XY
            let size = input.Size
            if rc.X < size.X && rc.Y < size.Y then
                let mutable c = V4d.Zero
                if input.[rc].X <> 0.0 then
                    
                    let r = radius |> float
                    let w = 1.0 / (r * r)

                    if (radius % 2) = 1 then // is odd
                        let h = radius / 2
                        for i in -h .. h do
                            for j in -h .. h do
                                let rcn = rc + V2i(i, j)
                                let c0 = input.[rcn]
                                c <- c + w * c0
                    else // is even
                        let h = radius / 2 - 1
                        for i in 0 .. (radius - 1) do
                            for j in 0 .. (radius - 1) do
                                let rc1 = rc + V2i(i - h, j - h)
                                let rc2 = rc1 - V2i(0, 1)
                                let rc3 = rc1 - V2i(1, 0)
                                let rc4 = rc1 - V2i(1, 1)
                                let c0 = (input.[rc1] + input.[rc2] + input.[rc3] + input.[rc4]) * 0.25
                                c <- c + w * c0
                else c <- input.[rc]
                output.[rc] <- c
        }


    [<LocalSize(X = 8, Y = 8)>]
    let gaussX (weights : float[]) (radius : int) (input : FShade.Image2d<Formats.r32f>) (output : Image2d<Formats.r32f>) =
        compute {
            let rc = getGlobalId().XY
            let size = input.Size

            if rc.X < size.X && rc.Y < size.Y then
                let mutable c = V4d.Zero
                if input.[rc].X <> 0.0 then

                    if (radius % 2) = 1 then // is odd
                        let h = radius / 2
                        for i in -h .. h do
                            let u = V2i(i, 0) // V2i(0, i)
                            let rcn = rc + u
                            let w = weights.[i + h]
                            let c0 = input.[rcn]
                            c <- c + w * c0
                    else // is even
                        let h = radius / 2 - 1
                        for i in 0 .. (radius - 1) do
                            let (i0,i1) = (i - h, i - h - 1)
                            let (u1,u2) = (V2i(i0, 0), V2i(i1, 0)) // else (V2i(0, i0), V2i(0, i1))
                            let rc1 = rc + u1
                            let rc2 = rc + u2
                            let c0 = (input.[rc1] + input.[rc2]) * 0.5
                            let w = weights.[i]
                            c <- c + w * c0
                else c <- input.[rc]
                output.[rc] <- c
        }

    [<LocalSize(X = 8, Y = 8)>]
    let gaussY (weights : float[]) (radius : int) (input : FShade.Image2d<Formats.r32f>) (output : Image2d<Formats.r32f>) =
        compute {
            let rc = getGlobalId().XY
            let size = input.Size
            if rc.X < size.X && rc.Y < size.Y then
                let mutable c = V4d.Zero
                if input.[rc].X <> 0.0 then
                    
                    if (radius % 2) = 1 then // is odd
                        let h = radius / 2
                        for i in -h .. h do
                            let u = V2i(0, i)
                            let rcn = rc + u
                            let w = weights.[i + h]
                            let c0 = input.[rcn]
                            c <- c + w * c0
                    else // is even
                        let h = radius / 2 - 1
                        for i in 0 .. (radius - 1) do
                            let (i0,i1) = (i - h, i - h - 1)
                            let (u1,u2) = (V2i(0, i0), V2i(0, i1))
                            let rc1 = rc + u1
                            let rc2 = rc + u2
                            let c0 = (input.[rc1] + input.[rc2]) * 0.5
                            let w = weights.[i]
                            c <- c + w * c0
                else c <- input.[rc]
                output.[rc] <- c
        }

    [<LocalSize(X = 8, Y = 8)>]
    let highPassX (weights : float[]) (radius : int) (input : FShade.Image2d<Formats.r32f>) (output : Image2d<Formats.r32f>) =
        compute {
            let rc = getGlobalId().XY
            let size = input.Size

            if rc.X < size.X && rc.Y < size.Y then
                let mutable c = V4d.Zero
                if input.[rc].X <> 0.0 then

                    if (radius % 2) = 1 then // is odd
                        let h = radius / 2
                        for i in -h .. h do
                            let u = V2i(i, 0)
                            let rcn = rc + u
                            let w = weights.[i + h]
                            let c0 = input.[rcn]
                            c <- c + w * c0
                    else // is even
                        let h = radius / 2 - 1
                        for i in 0 .. (radius - 1) do
                            let (i0,i1) = (i - h, i - h - 1)
                            let (u1,u2) = (V2i(i0, 0), V2i(i1, 0))
                            let rc1 = rc + u1
                            let rc2 = rc + u2
                            let c0 = (input.[rc1] + input.[rc2]) * 0.5
                            let w = weights.[i]
                            c <- c + w * c0
                    c <- c - V4d(V3d(0.5), 0.0)
                else c <- input.[rc]

                output.[rc] <- input.[rc] - c
        }

    [<LocalSize(X = 8, Y = 8)>]
    let highPassY (weights : float[]) (radius : int) (input : FShade.Image2d<Formats.r32f>) (output : Image2d<Formats.r32f>) =
        compute {
            let rc = getGlobalId().XY
            let size = input.Size

            if rc.X < size.X && rc.Y < size.Y then
                let mutable c = V4d.Zero
                if input.[rc].X <> 0.0 then

                    if (radius % 2) = 1 then // is odd
                        let h = radius / 2
                        for i in -h .. h do
                            let u = V2i(0, i)
                            let rcn = rc + u
                            let w = weights.[i + h]
                            let c0 = input.[rcn]
                            c <- c + w * c0
                    else // is even
                        let h = radius / 2 - 1
                        for i in 0 .. (radius - 1) do
                            let (i0,i1) = (i - h, i - h - 1)
                            let (u1,u2) = (V2i(0, i0), V2i(0, i1))
                            let rc1 = rc + u1
                            let rc2 = rc + u2
                            let c0 = (input.[rc1] + input.[rc2]) * 0.5
                            let w = weights.[i]
                            c <- c + w * c0
                    c <- c - V4d(V3d(0.5), 0.0)
                else c <- input.[rc]
                output.[rc] <- input.[rc] - c
        }
    (********************************************************************************************************)


    (********************************************************************************************************
     * Show my texture as gray image
     ********************************************************************************************************)
    let private diffuseSampler =
        sampler2d {
            texture uniform?DiffuseColorTexture
            filter Filter.MinMagLinear
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
        }

    let myDiffuseTexture (v : Vertex) =
        fragment {
            let c = diffuseSampler.Sample(v.tc).X
            return V4d(c, c, c, 1.0)
        }
    (********************************************************************************************************)


module Helper =

    let ceilDiv (v : int) (d : int) =
        if v % d = 0 then v / d
        else 1 + v / d

    let TrafoDiffuse = 
        let effects = 
            Seq.ofList [
                DefaultSurfaces.trafo                  |> toEffect
                ImageProcessingShader.myDiffuseTexture |> toEffect
            ]
        let e = FShade.Effect.compose effects
        FShadeSurface.Get(e) :> ISurface


module ImageComposing =
    open Aardvark.Rendering.Vulkan
    open Helper

    type Composer(app : IApplication, size : V2i) =
        let rt = app.Runtime
        let irt = rt :> IComputeRuntime
        let composeShader = irt.CreateComputeShader(TextureCombinationShader.textureComposer)

        member x.Compose (delInput1 : bool, input1 : IBackendTexture) (delInput2: bool, input2 : IBackendTexture) (weight : float) (blend : int) =
            Report.BeginTimed("CreateTexture")
            let output = rt.CreateTexture(size, TextureFormat.R32f, 1, 1)
            Report.EndTimed() |> ignore

            Report.BeginTimed("Compose")
            let input = irt.NewInputBinding(composeShader)
            input.["inputL"] <- input1
            input.["inputR"] <- input2
            input.["output"] <- output
            input.["blend"]  <- blend
            input.["weight"] <- weight
            input.Flush()
            
            irt.Run [
                        ComputeCommand.TransformLayout(output,TextureLayout.ShaderWrite)
                        ComputeCommand.TransformLayout(input1,TextureLayout.ShaderRead)
                        ComputeCommand.TransformLayout(input2,TextureLayout.ShaderRead)
                        ComputeCommand.Bind composeShader
                        ComputeCommand.SetInput input
                        ComputeCommand.Dispatch(V2i(ceilDiv size.X 8, ceilDiv size.Y 8))
                        ComputeCommand.Sync output
                        ComputeCommand.TransformLayout(output,TextureLayout.ShaderRead)
                    ]
            if delInput1 then rt.DeleteTexture input1
            if delInput2 then rt.DeleteTexture input2
            input.Dispose()
            Report.EndTimed() |> ignore
            true, output

        member x.Dispose() =
            irt.DeleteComputeShader composeShader

        interface IDisposable with
            member x.Dispose() = x.Dispose()

module ImageProcessing =
    open Aardvark.Rendering.Vulkan
    open Helper

    type Processor(app : IApplication, size : V2i) =

        let rt = app.Runtime
        let irt = rt :> IComputeRuntime

        let gaussShaderX : IComputeShader = irt.CreateComputeShader(ImageProcessingShader.gaussX)
        let gaussShaderY : IComputeShader = irt.CreateComputeShader(ImageProcessingShader.gaussY)
        let highPassX : IComputeShader = irt.CreateComputeShader(ImageProcessingShader.highPassX)
        let highPassY : IComputeShader = irt.CreateComputeShader(ImageProcessingShader.highPassY)
        let boxShader : IComputeShader = irt.CreateComputeShader(ImageProcessingShader.box)


        (*
         * Source for formulas: OpenCV documentation at
         * https://docs.opencv.org/2.4/modules/imgproc/doc/filtering.html?highlight=gaussianblur#gaussianblur
         * 16.02.2018
         *)
        let calculateGaussWeights (r) : float32[] =
            let rf = (r |> float32)
            let sf = 0.3f * ((rf - 1.0f) * 0.5f - 1.0f) + 0.8f
            let tf = -0.5f / (sf * sf)
            let hf = (rf - 1.0f) * 0.5f

            let res : float32[] =
                Array.init r
                    (fun i -> 
                        let x = (float32 i) - hf
                        exp (tf * x * x)
                    )
            let sum = Array.sum res
            let w = res |> Array.map (fun v -> (v / sum))
            w

        // commands for gauss or highpass
        let GaussHighPassFunc (weights : IBuffer<float32>) (delBuffer : bool) (shader : IComputeShader) (intex : IBackendTexture) (outtex : IBackendTexture) (radius : int) (deleteInput : bool) =
            Report.BeginTimed("Gauss or HighPass")
            let input = irt.NewInputBinding(shader)
            input.["input"]   <- intex
            input.["output"]  <- outtex
            input.["radius"]  <- radius
            input.["weights"] <- weights
            input.Flush()
                
            irt.Run [
                        ComputeCommand.TransformLayout(intex,TextureLayout.ShaderRead)
                        ComputeCommand.TransformLayout(outtex,TextureLayout.ShaderWrite)

                        ComputeCommand.Bind shader
                        ComputeCommand.SetInput input
                        ComputeCommand.Dispatch(V2i(ceilDiv size.X 8, ceilDiv size.Y 8))
                        ComputeCommand.Sync outtex
                        ComputeCommand.TransformLayout(outtex,TextureLayout.ShaderRead)
                    ]
            if deleteInput then rt.DeleteTexture intex
            if delBuffer then rt.DeleteBuffer (weights.Buffer)
            input.Dispose()
            Report.EndTimed() |> ignore
            true

        let BoxFunc (intex : IBackendTexture) (outtex : IBackendTexture) (radius : int) (deleteInput : bool) : (bool) =
            Report.BeginTimed("Box")
            let input = irt.NewInputBinding(boxShader)
            input.["input"]   <- intex
            input.["output"]  <- outtex
            input.["radius"]  <- radius
            input.Flush()
                
            irt.Run [
                        ComputeCommand.TransformLayout(intex,TextureLayout.ShaderRead)
                        ComputeCommand.TransformLayout(outtex,TextureLayout.ShaderWrite)

                        ComputeCommand.Bind boxShader
                        ComputeCommand.SetInput input
                        ComputeCommand.Dispatch(V2i(ceilDiv size.X 8, ceilDiv size.Y 8))
                        ComputeCommand.Sync outtex

                        ComputeCommand.TransformLayout(outtex,TextureLayout.ShaderRead)

                    ]
            if deleteInput then rt.DeleteTexture intex
            input.Dispose()
            Report.EndTimed() |> ignore

            true
            

        member x.GaussFilterProgram (radius : int) (delInput : bool, inputTex : IBackendTexture) = 
            let outputX = rt.CreateTexture(size, TextureFormat.R32f, 1, 1)
            let outputY = rt.CreateTexture(size, TextureFormat.R32f, 1, 1)
            let w = calculateGaussWeights radius
            let weights : IBuffer<float32> = rt.CreateBuffer<float32>(w)
            let delInput1 = delInput  |> GaussHighPassFunc weights false gaussShaderX inputTex outputX radius
            let delInput2 = delInput1 |> GaussHighPassFunc weights true  gaussShaderY outputX  outputY radius
            (delInput2, outputY)


        member x.HighPassFilterProgram (radius : int) (delInput : bool, inputTex : IBackendTexture) =
            let outputX = rt.CreateTexture(size, TextureFormat.R32f, 1, 1)
            let outputY = rt.CreateTexture(size, TextureFormat.R32f, 1, 1)
            let w = calculateGaussWeights radius
            let weights : IBuffer<float32> = rt.CreateBuffer<float32>(w)
            let delInput1 = delInput  |> GaussHighPassFunc weights false highPassX inputTex outputX radius
            let delInput2 = delInput1 |> GaussHighPassFunc weights true  highPassY outputX  outputY radius
            (delInput2, outputY)

        member x.BoxFilterProgram (radius : int) (delinput : bool, inputTex : IBackendTexture) =
            let output = rt.CreateTexture(size, TextureFormat.R32f, 1, 1)
            let delinput1 = delinput |> BoxFunc inputTex output radius
            (delinput1, output)



        member x.Dispose() =
            irt.DeleteComputeShader gaussShaderX
            irt.DeleteComputeShader gaussShaderY
            irt.DeleteComputeShader highPassX
            irt.DeleteComputeShader highPassY
            irt.DeleteComputeShader boxShader

        interface IDisposable with
            member x.Dispose() = x.Dispose()


module Image = 
    open ImageProcessing
    open ImageComposing

    type Image(app : IApplication, quadSize : V2i) =

        let testFile1 = @"..\..\data\testTexture1.jpg"
        let testFile2 = @"..\..\data\testTexture2.jpg"
        let rt = app.Runtime
        ///////////////////////////////////////////////////////////////////////////////
        let mutable testsize = V2i(0,0)

        let loadTexture (file : string) : IBackendTexture =
            let img = PixImage.Create(file).ToPixImage<byte>(Col.Format.BGR)
            testsize <- img.Size
            let a = PixImage<float32>(Col.Format.Gray, testsize)
            let b = img.GetMatrix<C3b>()
            b.ForeachCoord(fun (c : V2l) ->
                let v = b.[c].ToC3f()
                a.Volume.[V3l(c,0L)] <- (v.R + v.G + v.B) / 3.0f
            )
            Report.BeginTimed("CreateTexture")
            let tex = rt.CreateTexture(testsize, TextureFormat.R32f, 1, 1)
            Report.EndTimed() |> ignore

            Report.BeginTimed("Upload")
            rt.Upload(tex, 0, 0, a) 
            Report.EndTimed() |> ignore
            tex

        let testFile3 = @"..\..\data\testTexture3.jpg"
        let testFile4 = @"..\..\data\testTexture4.jpg"
        let inputImage1 = testFile3 |> loadTexture
        let inputImage2 = testFile4 |> loadTexture
        ///////////////////////////////////////////////////////////////////////////////

        let indi = [| 0; 1; 2;  0; 2; 3;  0; 3; 4|]
        let uv1 =  [| V2f(0.0, 0.0); V2f(0.5, 0.0); V2f(0.9, 0.4); V2f(0.7, 0.8); V2f(0.2, 0.5)|]
        let uv2 =  [| V2f(0.3, 0.0); V2f(0.8, 0.1); V2f(0.5, 0.9); V2f(0.2, 0.5); V2f(0.1, 0.2)|]
        let uv3 =  [| V2f(0.0, 0.0); V2f(0.6, 0.1); V2f(0.7, 0.7); V2f(0.4, 0.7); V2f(0.2, 0.2)|]
        let uv4 =  [| V2f(0.5, 0.1); V2f(0.6, 0.1); V2f(0.7, 0.9); V2f(0.4, 0.3); V2f(0.4, 0.2)|]

        let s = testsize
        let xf = s.X |> float32
        let yf = s.Y |> float32
        let uv2set (v : V2f) = V2f(v.X * xf, v.Y * yf)

        let set1 = uv1 |> Array.map(uv2set)
        let set2 = uv2 |> Array.map(uv2set)
        let set3 = uv3 |> Array.map(uv2set)
        let set4 = uv4 |> Array.map(uv2set)
        

        let sign = rt.CreateFramebufferSignature [DefaultSemantic.Colors, {format = RenderbufferFormat.R32f; samples = 1}; ]
        let signDepth = rt.CreateFramebufferSignature [
                                    DefaultSemantic.Colors, { format = RenderbufferFormat.R32f; samples = 1};
                                    DefaultSemantic.Depth,  { format = RenderbufferFormat.Depth24Stencil8; samples = 1 }
                                ]

        let clear = rt.CompileClear(sign, C4f.Black |> Mod.constant, 1.0 |> Mod.constant)
        let clearDepth = rt.CompileClear(signDepth, C4f.Black |> Mod.constant, 1.0 |> Mod.constant)


        let sg (view : IMod<Trafo3d>) (proj : IMod<Trafo3d>) (tex : string) (set : V2f[]) (uv : V2f[]) =
            let s = Sg.draw (IndexedGeometryMode.TriangleList)
                    |> Sg.vertexAttribute DefaultSemantic.Positions (set |> Mod.constant)
                    |> Sg.vertexAttribute DefaultSemantic.DiffuseColorCoordinates (uv |> Mod.constant)
                    |> Sg.index (indi |> Mod.constant)
                    |> Sg.viewTrafo (view)
                    |> Sg.projTrafo (proj)
                    |> Sg.diffuseFileTexture' tex false
            Sg.SurfaceApplicator(Helper.TrafoDiffuse, s) :> ISg


        let sg' (view : IMod<Trafo3d>) (proj : IMod<Trafo3d>) (tex : ITexture) (set : V2f[]) (uv : V2f[]) =
            let s = Sg.draw (IndexedGeometryMode.TriangleList)
                    |> Sg.vertexAttribute DefaultSemantic.Positions (set |> Mod.constant)
                    |> Sg.vertexAttribute DefaultSemantic.DiffuseColorCoordinates (uv |> Mod.constant)
                    |> Sg.index (indi |> Mod.constant)
                    |> Sg.viewTrafo (view)
                    |> Sg.projTrafo (proj)
                    |> Sg.diffuseTexture' tex
            Sg.SurfaceApplicator(Helper.TrafoDiffuse, s) :> ISg

//        let runBaseTask (view : IMod<Trafo3d>) (proj : IMod<Trafo3d>) (texture : int) =
//            if texture = 1 then
//                let f = (fun () -> 
//                    let task = rt.CompileRender(sign, sg view proj testFile1 set1 uv1)
//                    clear.Run(null, fbo1 |> OutputDescription.ofFramebuffer)
//                    task.Run(null, fbo1 |> OutputDescription.ofFramebuffer)
//                    false)
//                f, tex1
//            else
//                let f = (fun () -> 
//                    let task = rt.CompileRender(sign, sg view proj testFile2 set2 uv2)
//                    clear.Run(null, fbo2 |> OutputDescription.ofFramebuffer)
//                    task.Run(null, fbo2 |> OutputDescription.ofFramebuffer)
//                    false)
//                f, tex2
        


        let processor = new Processor(app, testsize)
        let composer = new Composer(app, testsize)

        let bla (f : unit -> bool, t : IBackendTexture) =
            f() |> ignore
            t


        member x.Test2 (view) (proj) =
            let tex = rt.CreateTexture((s), TextureFormat.R32f, 1, 1)
            let dep = rt.CreateRenderbuffer((s), RenderbufferFormat.Depth24Stencil8, 1)
            let fbo = rt.CreateFramebuffer(
                                    signDepth,
                                    Map.ofList[
                                        DefaultSemantic.Colors, ({texture = tex; slice = 0; level = 0} :> IFramebufferOutput)
                                        DefaultSemantic.Depth, (dep :> IFramebufferOutput)
                                    ])
            let uvs = Mod.init ([|V2f(0.0, 0.0); V2f(1.0, 0.0); V2f(1.0, 1.0); V2f(0.0, 1.0)|])
            let x = s.X |> float
            let y = s.Y |> float
            let z = 0.0
            let pos = Mod.init ([|V3f(0.0, 0.0, z); V3f(x, 0.0, z); V3f(x, y, z); V3f(0.0, y, z)|])

            let fileTexture = FileTexture(testFile4, false)

            let tmp =
                Sg.draw (IndexedGeometryMode.TriangleList)
                |> Sg.viewTrafo (view)
                |> Sg.projTrafo (proj)
                |> Sg.vertexAttribute DefaultSemantic.Positions ([|V3f(0.0, 0.0, z); V3f(x, 0.0, z); V3f(x, y, z); V3f(0.0, y, z)|] |> Mod.constant)
                |> Sg.vertexAttribute DefaultSemantic.DiffuseColorCoordinates uvs
                |> Sg.index ([|0; 1; 2; 0; 2; 3|] |> Mod.constant)
                |> Sg.diffuseTexture' fileTexture
                |> Sg.effect [
                    DefaultSurfaces.trafo |> toEffect
                    DefaultSurfaces.diffuseTexture |> toEffect
                ]

            let taskDepth = rt.CompileRender(signDepth, tmp)
            clearDepth.Run(null, fbo |> OutputDescription.ofFramebuffer) |> ignore
            taskDepth.Run(null, fbo |> OutputDescription.ofFramebuffer) |> ignore

            let finalTex = rt.CreateTexture(s, TextureFormat.R32f, 1, 1)
            let nFbo = rt.CreateFramebuffer(sign, Map.ofList[ DefaultSemantic.Colors, ({texture = finalTex; slice = 0; level = 0} :> IFramebufferOutput)])
            let tmp2 = sg' view proj tex set4 uv4
            let nTask = rt.CompileRender(sign, tmp2)
            clear.Run(null, nFbo |> OutputDescription.ofFramebuffer)
            nTask.Run(null, nFbo |> OutputDescription.ofFramebuffer)

            finalTex


        member x.Test (view) (proj) : IBackendTexture =
            let t = testFile4

            Report.BeginTimed("CreateTexture + FBO")
            let tex1 = rt.CreateTexture(s, TextureFormat.R32f, 1, 1)
            let fbo1 = rt.CreateFramebuffer(sign, Map.ofList[DefaultSemantic.Colors, ({texture = tex1; slice = 0; level = 0} :> IFramebufferOutput)])
            Report.EndTimed() |> ignore
            Report.BeginTimed("Render Texture 1")
            let task = rt.CompileRender(sign, sg view proj t set1 uv1)
            clear.Run(null, fbo1 |> OutputDescription.ofFramebuffer)
            task.Run(null, fbo1 |> OutputDescription.ofFramebuffer)
            Report.EndTimed() |> ignore

            Report.BeginTimed("CreateTexture + FBO")
            let tex2 = rt.CreateTexture(s, TextureFormat.R32f, 1, 1)
            let fbo2 = rt.CreateFramebuffer(sign, Map.ofList[DefaultSemantic.Colors, ({texture = tex2; slice = 0; level = 0} :> IFramebufferOutput)])
            Report.EndTimed() |> ignore
            Report.BeginTimed("Render Texture 2")
            let task = rt.CompileRender(sign, sg view proj t set2 uv2)
            clear.Run(null, fbo2 |> OutputDescription.ofFramebuffer)
            task.Run(null, fbo2 |> OutputDescription.ofFramebuffer)
            Report.EndTimed() |> ignore

            Report.BeginTimed("CreateTexture + FBO")
            let tex3 = rt.CreateTexture(s, TextureFormat.R32f, 1, 1)
            let fbo3 = rt.CreateFramebuffer(sign, Map.ofList[DefaultSemantic.Colors, ({texture = tex3; slice = 0; level = 0} :> IFramebufferOutput)])
            Report.EndTimed() |> ignore
            Report.BeginTimed("Render Texture 3")
            let task = rt.CompileRender(sign, sg view proj t set3 uv3)
            clear.Run(null, fbo3 |> OutputDescription.ofFramebuffer)
            task.Run(null, fbo3 |> OutputDescription.ofFramebuffer)
            Report.EndTimed() |> ignore

            Report.BeginTimed("CreateTexture + FBO")
            let tex4 = rt.CreateTexture(s, TextureFormat.R32f, 1, 1)
            let fbo4 = rt.CreateFramebuffer(sign, Map.ofList[DefaultSemantic.Colors, ({texture = tex4; slice = 0; level = 0} :> IFramebufferOutput)])
            Report.EndTimed() |> ignore
            Report.BeginTimed("Render Texture 4")
            let task = rt.CompileRender(sign, sg view proj t set4 uv4)
            clear.Run(null, fbo4 |> OutputDescription.ofFramebuffer)
            task.Run(null, fbo4 |> OutputDescription.ofFramebuffer)
            Report.EndTimed() |> ignore

            let r01 = (false, tex1) // r11 //
            let r02 = (false, tex2) // l00 //
            let r03 = (false, tex3) // r22 // 
            let r04 = (false, tex4) // r33 //

            let l01 = r01
            let l11 = processor.GaussFilterProgram 5 l01

            let l00_ = r02
            let r00_ = r03
            let l11_ = composer.Compose l00_ r00_ 0.5 2 // Sub
            let r11 = processor.GaussFilterProgram 10 l11_

            let l2 = composer.Compose l11 r11 1.0 1 // Add
            let r2 = r04

            let delInput, output = composer.Compose l2 r2 1.0 3 // Min

            Report.BeginTimed("Delete Textures + FBOs")
            rt.DeleteTexture tex1
            rt.DeleteTexture tex2
            rt.DeleteTexture tex3
            rt.DeleteTexture tex4
            rt.DeleteFramebuffer fbo1
            rt.DeleteFramebuffer fbo2
            rt.DeleteFramebuffer fbo3
            rt.DeleteFramebuffer fbo4
            Report.EndTimed() |> ignore

            output


        member x.Dispose() =

            rt.DeleteFramebufferSignature sign

        interface IDisposable with
            member x.Dispose() = x.Dispose()




module ImageProcessingExample = 
    open ImageProcessing

    let run () = 
        let app = new VulkanApplication(true)

        let useFilter = Mod.init true
        let chooseFilter = Mod.init 0
        let ucFilter = Mod.map2 (fun u c -> (u, c)) useFilter chooseFilter
        let radius = Mod.init 5

        
        

//        let tex = Mod.map2(fun r (uf, cf) ->
//                                if uf then
//                                    if cf = 0 then proc.GetGaussFilteredImage(r)
//                                    elif cf = 1 then proc.GetHighPassFilteredImage(r)
//                                    else proc.GetBoxFilteredImage(r)
//                                else
//                                    proc.GetUnfilteredImage()
//                          ) radius ucFilter

        let win = app.CreateSimpleRenderWindow(samples = 1)

//        win.Keyboard.KeyDown(Keys.U).Values.Subscribe(fun _ -> transact(fun _ -> radius.Value <- if radius.Value < 55 then radius.Value + 1 else radius.Value
//                                                                                 printfn "%A" radius.Value)) |> ignore
//        win.Keyboard.KeyDown(Keys.J).Values.Subscribe(fun _ -> transact(fun _ -> radius.Value <- if radius.Value > 1 then radius.Value - 1 else radius.Value
//                                                                                 printfn "%A" radius.Value)) |> ignore
//        win.Keyboard.KeyDown(Keys.O).Values.Subscribe(fun _ -> transact(fun _ -> useFilter.Value <- (not useFilter.Value))) |> ignore
//        win.Keyboard.KeyDown(Keys.T).Values.Subscribe(fun _ ->
//                                                        transact(fun _ ->
//                                                                    let mutable x = chooseFilter.Value + 1
//                                                                    if x > 2 then x <- 0
//                                                                    chooseFilter.Value <-x
//                                                                )) |> ignore

        
        let initialView = CameraView.LookAt(V3d(2.0,2.0,2.0), V3d.Zero, V3d.OOI)
        let proj = win.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 50.0 (float s.X / float s.Y)) |> Mod.map Frustum.projTrafo
        let view = DefaultCameraController.control win.Mouse win.Keyboard win.Time initialView |> Mod.map CameraView.viewTrafo

        let x = 1920
        let y = 1080

        let xh = x / 2
        let yh = y / 2
        let imageTest = new Image.Image(app, V2i(x, y))

        let orthoview = Mod.constant(CameraView.lookAt (V3d(xh, yh, 20)) (V3d(xh, yh, 0)) V3d.OIO |> CameraView.viewTrafo)
        let orthoproj = Mod.constant (Frustum.ortho (Box3d (-xh |> float, -yh |> float, -10.0, xh |> float, yh |> float, 30.0)) |> Frustum.orthoTrafo)

        let tex = imageTest.Test2 orthoview orthoproj :> ITexture


        let quadSg =
            let quad =
                let x = x
                let y = y
                IndexedGeometry(
                    Mode = IndexedGeometryMode.TriangleList,
                    IndexArray = ([|0;1;2; 0;2;3|] :> System.Array),
                    IndexedAttributes =
                        SymDict.ofList [
                            DefaultSemantic.Positions,                  [| V3f(0,0,0); V3f(x,0,0); V3f(x,y,0); V3f(0,y,0) |] :> Array
                            DefaultSemantic.Normals,                    [| V3f.OOI; V3f.OOI; V3f.OOI; V3f.OOI |] :> Array
                            DefaultSemantic.DiffuseColorCoordinates,    [| V2f.OO; V2f.IO; V2f.II; V2f.OI |] :> Array
                        ]
                )
            quad |> Sg.ofIndexedGeometry

        let sg =
            let s = quadSg
                    |> Sg.diffuseTexture' (tex)
                    |> Sg.viewTrafo (orthoview)
                    |> Sg.projTrafo (orthoproj)
            Sg.SurfaceApplicator(Helper.TrafoDiffuse, s) :> ISg

        let renderTask = app.Runtime.CompileRender(win.FramebufferSignature, sg)

        win.RenderTask <- renderTask
        win.Run()
        0

