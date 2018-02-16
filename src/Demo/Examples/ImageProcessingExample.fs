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
            let size = inputL.Size // inputL.Size = inputR.Size = output.Size
            if rc.X < size.X && rc.Y < size.Y then
                let cl = inputL.[rc].X
                let cr = inputR.[rc].X

                let c =
                    if cr < 0.0001 then cl
                    elif cl < 0.0001 then cr
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

                output.[rc] <- V4d(c, c, c, 1.0)
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
                output.[rc] <- c
        }


    [<LocalSize(X = 8, Y = 8)>]
    let gaussX (weights : float[]) (radius : int) (input : FShade.Image2d<Formats.r32f>) (output : Image2d<Formats.r32f>) =
        compute {
            let rc = getGlobalId().XY
            let size = input.Size

            if rc.X < size.X && rc.Y < size.Y then
                let mutable c = V4d.Zero

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

                output.[rc] <- c
        }

    [<LocalSize(X = 8, Y = 8)>]
    let gaussY (weights : float[]) (radius : int) (input : FShade.Image2d<Formats.r32f>) (output : Image2d<Formats.r32f>) =
        compute {
            let rc = getGlobalId().XY
            let size = input.Size

            if rc.X < size.X && rc.Y < size.Y then
                let mutable c = V4d.Zero

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

                output.[rc] <- c
        }

    [<LocalSize(X = 8, Y = 8)>]
    let highPassX (weights : float[]) (radius : int) (input : FShade.Image2d<Formats.r32f>) (output : Image2d<Formats.r32f>) =
        compute {
            let rc = getGlobalId().XY
            let size = input.Size

            if rc.X < size.X && rc.Y < size.Y then
                let mutable c = V4d.Zero

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

                output.[rc] <- input.[rc] - c + 0.5
        }

    [<LocalSize(X = 8, Y = 8)>]
    let highPassY (weights : float[]) (radius : int) (input : FShade.Image2d<Formats.r32f>) (output : Image2d<Formats.r32f>) =
        compute {
            let rc = getGlobalId().XY
            let size = input.Size

            if rc.X < size.X && rc.Y < size.Y then
                let mutable c = V4d.Zero

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

                output.[rc] <- input.[rc] - c + 0.5
        }
    (********************************************************************************************************)


    (********************************************************************************************************
     * Show my texture as gray image
     ********************************************************************************************************)
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
    (********************************************************************************************************)

module ImageProcessing =
    open Aardvark.Rendering.Vulkan

    let ceilDiv (v : int) (d : int) =
        if v % d = 0 then v / d
        else 1 + v / d

    type Processor() =

        let app = new VulkanApplication()
        let rt = app.Runtime
        let irt = rt :> IComputeRuntime

        let testFile1 = @"..\..\data\testTexture1.jpg"
        let testFile2 = @"..\..\data\testTexture2.jpg"

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

        let inputImage = testFile1 |> loadTexture
        let mutable outputImage : IBackendTexture = rt.CreateTexture(size, TextureFormat.R32f, 1, 1, 1)

        (********************************************************************************************************
         * Filter Stuff
         ********************************************************************************************************)
        // Filter Shader
        let gaussShaderX : IComputeShader = irt.CreateComputeShader(ImageProcessingShader.gaussX)
        let gaussShaderY : IComputeShader = irt.CreateComputeShader(ImageProcessingShader.gaussY)
        let highPassX : IComputeShader = irt.CreateComputeShader(ImageProcessingShader.highPassX)
        let highPassY : IComputeShader = irt.CreateComputeShader(ImageProcessingShader.highPassY)
        let boxShader : IComputeShader = irt.CreateComputeShader(ImageProcessingShader.box)



        (*
         * Refactored calculation of weights
         * incl. calculating Sigma depending on the kernel size
         * 
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
        let GaussHighPassFunc (id : string) (shader : IComputeShader) (intex : IBackendTexture) (outtex : IBackendTexture) (radius : int) (f : unit -> bool) : (unit -> bool) = 
            (fun () ->
                let w = calculateGaussWeights radius
                let weights : IBuffer<float32> = rt.CreateBuffer<float32>(w)

                let input = irt.NewInputBinding(shader)
                input.["input"]   <- intex
                input.["output"]  <- outtex
                input.["radius"]  <- radius
                input.["weights"] <- weights
                input.Flush()
                let deleteInput = f()
                printfn "Calling function: %A" id
                irt.Run [
                            ComputeCommand.Bind shader
                            ComputeCommand.SetInput input
                            ComputeCommand.Dispatch(V2i(ceilDiv size.X 8, ceilDiv size.Y 8))
                            ComputeCommand.Sync outtex
                        ]
                if deleteInput then rt.DeleteTexture intex
                rt.DeleteBuffer (weights.Buffer)
                input.Dispose()
                true
            )

        let BoxFunc (id : string) (intex : IBackendTexture) (outtex : IBackendTexture) (radius : int) (f : unit -> bool) : (unit -> bool) =
            (fun () ->
                let input = irt.NewInputBinding(boxShader)
                input.["input"]   <- intex
                input.["output"]  <- outtex
                input.["radius"]  <- radius
                input.Flush()
                let deleteInput = f()
                printfn "Calling function: %A" id
                irt.Run [
                            ComputeCommand.Bind boxShader
                            ComputeCommand.SetInput input
                            ComputeCommand.Dispatch(V2i(ceilDiv size.X 8, ceilDiv size.Y 8))
                            ComputeCommand.Sync outtex
                        ]
                if deleteInput then rt.DeleteTexture intex
                input.Dispose()
                true
            )

        let GaussFilterProgram (radius : int) (f0 : unit -> bool) (inputTex : IBackendTexture) = 
            let outputX = rt.CreateTexture(size, TextureFormat.R32f, 1, 1, 1)
            let outputY = rt.CreateTexture(size, TextureFormat.R32f, 1, 1, 1)
            let fx = f0 |> GaussHighPassFunc ("gauss X") gaussShaderX inputTex outputX radius // inputTex gets deleted or not depeding on f0
            let fy = fx |> GaussHighPassFunc ("gauss Y") gaussShaderY outputX outputY radius // outputX gets deleted here! --> outputY gets deleted when fy is called
            (fy, outputY)

        let HighPassFilterProgram (radius : int) (f0 : unit -> bool) (inputTex : IBackendTexture) =
            let outputX = rt.CreateTexture(size, TextureFormat.R32f, 1, 1, 1)
            let outputY = rt.CreateTexture(size, TextureFormat.R32f, 1, 1, 1)
            let fx = f0 |> GaussHighPassFunc ("highpass X") highPassX inputTex outputX radius // inputTex gets deleted or not depeding on f0
            let fy = fx |> GaussHighPassFunc ("highpass Y") highPassY outputX outputY radius // outputX gets deleted here! --> outputY gets deleted when fy is called
            (fy, outputY)

        let BoxFilterProgram (radius : int) (f0 : unit -> bool) (inputTex : IBackendTexture) =
            let output = rt.CreateTexture(size, TextureFormat.R32f, 1, 1, 1)
            let f1 = f0 |> BoxFunc ("box") inputTex output radius
            (f1, output)


        let Programm (radius : int) (func) = 
            let f0 : unit -> bool = 
                (fun _ ->
                    printfn "Calling function: f0";
                    false
                )
            let f1, output = func radius f0 inputImage
            f1() |> ignore
            rt.DeleteTexture outputImage
            outputImage <- output
            (outputImage :> ITexture)

        member x.GetBoxFilteredImage(radius : int) = BoxFilterProgram |> Programm radius 
        member x.GetGaussFilteredImage(radius : int) = GaussFilterProgram |> Programm radius
        member x.GetHighPassFilteredImage(radius : int) = HighPassFilterProgram |> Programm radius

        



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
        let useFilter = Mod.init true
        let chooseFilter = Mod.init 0
        let ucFilter = Mod.map2 (fun u c -> (u, c)) useFilter chooseFilter
        let radius = Mod.init 5
        let tex = Mod.map2(fun r (uf, cf) ->
                                if uf then
                                    if cf = 0 then proc.GetGaussFilteredImage(r)
                                    elif cf = 1 then proc.GetHighPassFilteredImage(r)
                                    else proc.GetBoxFilteredImage(r)
                                else
                                    proc.GetUnfilteredImage()
                          ) radius ucFilter

        use app = proc.GetApp()
        let win = app.CreateSimpleRenderWindow(samples = 1)

        win.Keyboard.KeyDown(Keys.U).Values.Subscribe(fun _ -> transact(fun _ -> radius.Value <- if radius.Value < 55 then radius.Value + 1 else radius.Value
                                                                                 printfn "%A" radius.Value)) |> ignore
        win.Keyboard.KeyDown(Keys.J).Values.Subscribe(fun _ -> transact(fun _ -> radius.Value <- if radius.Value > 1 then radius.Value - 1 else radius.Value
                                                                                 printfn "%A" radius.Value)) |> ignore
        win.Keyboard.KeyDown(Keys.O).Values.Subscribe(fun _ -> transact(fun _ -> useFilter.Value <- (not useFilter.Value))) |> ignore
        win.Keyboard.KeyDown(Keys.T).Values.Subscribe(fun _ ->
                                                        transact(fun _ ->
                                                                    let mutable x = chooseFilter.Value + 1
                                                                    if x > 2 then x <- 0
                                                                    chooseFilter.Value <-x
                                                                )) |> ignore

        
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

