namespace Examples

open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms

module ImageProcessingShader =
    open FShade
    open Aardvark.Base.Rendering.Effects

    // Box Filter
    (********************************************************************************************************)
    [<LocalSize(X = 8, Y = 8)>]
    let box (radius : int) (input : FShade.Image2d<Formats.r32f>) (output : Image2d<Formats.r32f>) =
        compute {
            let rc = getGlobalId().XY
            let size = input.Size
            if rc.X < size.X && rc.Y < size.Y then
                let mutable c = V4d.Zero
                let hr = radius / 2
                let r = radius |> float
                let w = 1.0 / (r * r)
                for i in -hr .. hr do
                    for j in -hr .. hr do
                        let rcn = rc + V2i(i, j)
                        c <- c + w * input.[rcn]
                output.[rc] <- c
        }


    // Gauss Filter
    (********************************************************************************************************)

    [<LocalSize(X = 8, Y = 8)>]
    let gaussX (weights : float[]) (radius : int) (input : FShade.Image2d<Formats.r32f>) (output : Image2d<Formats.r32f>) =
        compute {
            let rc = getGlobalId().XY
            let size = input.Size

            if rc.X < size.X && rc.Y < size.Y then
                let mutable c = V4d.Zero 
                let halfRadius = radius / 2
                for i in -halfRadius .. halfRadius do
                    let rcn = rc + V2i(i, 0)
                    let w = weights.[i + halfRadius]
                    c <- c + w * input.[rcn]
                output.[rc] <- c
        }

    [<LocalSize(X = 8, Y = 8)>]
    let gaussY (weights : float[]) (radius : int) (input : FShade.Image2d<Formats.r32f>) (output : Image2d<Formats.r32f>) =
        compute {
            let rc = getGlobalId().XY
            let size = input.Size

            if rc.X < size.X && rc.Y < size.Y then
                let mutable c = V4d.Zero 
                let halfRadius = radius / 2
                for i in -halfRadius .. halfRadius do
                    let rcn = rc + V2i(0, i)
                    let w = weights.[i + halfRadius]
                    c <- c + w * input.[rcn]
                output.[rc] <- c
        }

    // HighPass Filter
    (********************************************************************************************************)
    [<LocalSize(X = 8, Y = 8)>]
    let highPassX (weights : float[]) (radius : int) (input : FShade.Image2d<Formats.r32f>) (output : Image2d<Formats.r32f>) =
        compute {
            let rc = getGlobalId().XY
            let size = input.Size

            if rc.X < size.X && rc.Y < size.Y then
                let mutable c = V4d.Zero 
                let halfRadius = radius / 2
                for i in -halfRadius .. halfRadius do
                    let rcn = rc + V2i(i, 0)
                    let w = weights.[i + halfRadius]
                    c <- c + w * input.[rcn]
                let res = input.[rc] - c + 0.5 // including an offset
                output.[rc] <- res //input.[rc] + res
        }

    [<LocalSize(X = 8, Y = 8)>]
    let highPassY (weights : float[]) (radius : int) (input : FShade.Image2d<Formats.r32f>) (output : Image2d<Formats.r32f>) =
        compute {
            let rc = getGlobalId().XY
            let size = input.Size

            if rc.X < size.X && rc.Y < size.Y then
                let mutable c = V4d.Zero 
                let halfRadius = radius / 2
                for i in -halfRadius .. halfRadius do
                    let rcn = rc + V2i(0, i)
                    let w = weights.[i + halfRadius]
                    c <- c + w * input.[rcn]
                let res = input.[rc] - c + 0.5
                output.[rc] <- res //input.[rc] + res
        }



    (********************************************************************************************************)
    let private diffuseSampler =
        sampler2d {
            texture uniform?DiffuseColorTexture
            filter Filter.MinMagMipLinear
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
        }

    let myDiffuseTexture (v : Vertex) =
        fragment {
            let c = diffuseSampler.Sample(v.tc).X
            return V4d(c, c, c, 1.0)
        }


module ImageProcessing =
    open Aardvark.Rendering.Vulkan

    let ceilDiv (v : int) (d : int) =
        if v % d = 0 then v / d
        else 1 + v / d

    type Processor() =

        let app = new VulkanApplication()
        let rt = app.Runtime
        let irt = rt :> IComputeRuntime

        let testFile = @"..\..\data\testTexture1.jpg"

        let mutable size = V2i(0,0)

        let loadTexture (file : string) : IBackendTexture =
            let img = PixImage.Create(file).ToPixImage<byte>(Col.Format.BGR)
            size <- img.Size
            let a = PixImage<float32>(Col.Format.Gray, size)
            let b = img.GetMatrix<C3b>()
            
            b.ForeachCoord(fun (c : V2l) ->
                let v = b.[c].ToC3f()
                a.Volume.[V3l(c,0L)] <- (v.R + v.G + v.B) / 3.0f
            )

            let tex = rt.CreateTexture(size, TextureFormat.R32f, 1, 1, 1)
            rt.Upload(tex, 0, 0, a) 
            tex

        let inputImage = testFile |> loadTexture
        let outputImage = rt.CreateTexture(size, TextureFormat.R32f, 1, 1, 1)

        let gaussShaderX : IComputeShader = irt.CreateComputeShader(ImageProcessingShader.gaussX)
        let gaussShaderY : IComputeShader = irt.CreateComputeShader(ImageProcessingShader.gaussY)
        let highPassX : IComputeShader = irt.CreateComputeShader(ImageProcessingShader.highPassX)
        let highPassY : IComputeShader = irt.CreateComputeShader(ImageProcessingShader.highPassY)

        (*
         * Calculating Sigma depending on the kernel size
         * Refactored calculation of weights
         * 
         * Source for formulas: OpenCV documentation at:
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


        let executeProgram2(f : unit -> unit, inputTex : IBackendTexture, shader : IComputeShader, radius : int, useOutputTexture : bool, deleteInputTex : bool) =
            let output = 
                if useOutputTexture then outputImage
                else rt.CreateTexture(size, TextureFormat.R32f, 1, 1, 1)

            let input = irt.NewInputBinding(shader)
            let w = calculateGaussWeights radius
            let wBuffer : IBuffer<float32> = rt.CreateBuffer<float32>(w)
            input.["input"]   <- inputTex
            input.["output"]  <- output
            input.["weights"] <- wBuffer
            input.["radius"]  <- radius
            input.Flush()
            let f : unit -> unit =
                (fun _ ->
                        f()
                        irt.Run [
                                ComputeCommand.Bind shader
                                ComputeCommand.SetInput input
                                ComputeCommand.Dispatch(V2i(ceilDiv size.X 8, ceilDiv size.Y 8))
                                ComputeCommand.Sync output
                            ]
                        if deleteInputTex then rt.DeleteTexture inputTex
                        rt.DeleteBuffer (wBuffer.Buffer)
                        input.Dispose()
                )
            (f, output)


        member x.GetFilteredImage(radius : int) =
            let f0 : unit -> unit = (fun _ -> ())
            let fX, res1 = executeProgram2(f0, inputImage, gaussShaderX, radius, false, false)
            let fY, res2 = executeProgram2(fX, res1, gaussShaderY, radius, true, true)
            fY()
            (res2 :> ITexture)

        member x.GetUnfilteredImage() = inputImage :> ITexture
        member x.GetApp() = app



        member x.Dispose() = 
            irt.DeleteComputeShader gaussShaderX
            irt.DeleteComputeShader gaussShaderY
            irt.DeleteComputeShader highPassX
            irt.DeleteComputeShader highPassY
            rt.DeleteTexture inputImage
            rt.DeleteTexture outputImage

        interface IDisposable with
            member x.Dispose() = x.Dispose()


module ImageProcessingExample = 
    open ImageProcessing

    let run () = 
    
        let proc = new Processor()
        let radius = Mod.init 5
        let useFilter = Mod.init true
        let tex = Mod.map2(fun r uf ->
                                if uf then
                                    proc.GetFilteredImage(r)
                                else
                                    proc.GetUnfilteredImage()
                          ) radius useFilter

        use app = proc.GetApp()
        let win = app.CreateSimpleRenderWindow(samples = 1)

        win.Keyboard.KeyDown(Keys.U).Values.Subscribe(fun _ -> transact(fun _ -> radius.Value <- if radius.Value < 55 then radius.Value + 2 else radius.Value)) |> ignore
        win.Keyboard.KeyDown(Keys.J).Values.Subscribe(fun _ -> transact(fun _ -> radius.Value <- if radius.Value > 1 then radius.Value - 2 else radius.Value)) |> ignore
        win.Keyboard.KeyDown(Keys.O).Values.Subscribe(fun _ -> transact(fun _ -> useFilter.Value <- (not useFilter.Value))) |> ignore
        
        let initialView = CameraView.LookAt(V3d(2.0,2.0,2.0), V3d.Zero, V3d.OOI)
        let frustum = win.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 50.0 (float s.X / float s.Y))

        let cameraView = DefaultCameraController.control win.Mouse win.Keyboard win.Time initialView

        let quadSg =
            let quad =
                IndexedGeometry(
                    Mode = IndexedGeometryMode.TriangleList,
                    IndexArray = ([|0;1;2; 0;2;3|] :> System.Array),
                    IndexedAttributes =
                        SymDict.ofList [
                            DefaultSemantic.Positions,                  [| V3f(-1,-1,0); V3f(1,-1,0); V3f(1,1,0); V3f(-1,1,0) |] :> Array
                            DefaultSemantic.Normals,                    [| V3f.OOI; V3f.OOI; V3f.OOI; V3f.OOI |] :> Array
                            DefaultSemantic.DiffuseColorCoordinates,    [| V2f.OO; V2f.IO; V2f.II; V2f.OI |] :> Array
                        ]
                )
                
            quad |> Sg.ofIndexedGeometry

        let sg =
            quadSg
                |> Sg.effect [
                        DefaultSurfaces.trafo                  |> toEffect
                        ImageProcessingShader.myDiffuseTexture |> toEffect
                    ]
                |> Sg.diffuseTexture (tex)
                |> Sg.viewTrafo (cameraView  |> Mod.map CameraView.viewTrafo )
                |> Sg.projTrafo (frustum |> Mod.map Frustum.projTrafo    )


        let renderTask = app.Runtime.CompileRender(win.FramebufferSignature, sg)

        win.RenderTask <- renderTask
        win.Run()
        0

