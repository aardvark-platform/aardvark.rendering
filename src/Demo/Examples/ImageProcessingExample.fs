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

    // Shader that can be used for Gauss or for Box-Filter
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

    // HighPass Filter that uses either a Gauss or a Box filter
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
                let res = input.[rc] - c + 0.5
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

    type Processor(radius : int) =

        let app = new VulkanApplication()
        let rt = app.Runtime
        let irt = rt :> IComputeRuntime

        let testFile = @"c:/users/schimkowitsch/Pictures/Frans Hals - Laughing Cavalier.png"

        let _radius = radius
        let rr = (1.0 / (float radius)) |> float32
        let sigma = 6.0
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


        let calculateWeights (r) : float32[] =
            [| for i in 0..(r-1) do yield (1.0 / (float r)) |> float32 |] // simple box shader
//            let half = radius / 2
//            let res =
//                [|
//                    for i in 0 .. (radius-1) do
//                        let x = abs (i - half)
//                        yield exp (-float (x * x)) / (2.0 * sigma * sigma)
//                |]
//            let sum = Array.sum res
//            res |> Array.map (fun v -> (v / sum) |> float32)
        
        let weightsBuffer = rt.CreateBuffer<float32>(radius |> calculateWeights)

        let executeProgram2(f : unit -> unit, inputTex : IBackendTexture, shader : IComputeShader, radius : int, useOutputTexture : bool, deleteInputTex : bool) =
            let output = 
                if useOutputTexture then outputImage
                else rt.CreateTexture(size, TextureFormat.R32f, 1, 1, 1)

            let input = irt.NewInputBinding(shader)
            let w = calculateWeights radius
            let wBuffer : IBuffer<float32> = rt.CreateBuffer<float32>(w) // gets destroyed 
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


        member x.ExecuteProgram(radius : int) =
            let f0 : unit -> unit = (fun _ -> ())
            let fX, res1 = executeProgram2(f0, inputImage, gaussShaderX, radius, false, false)
            let fY, res2 = executeProgram2(fX, res1, gaussShaderY, radius, true, true)
            fY()
            (res2 :> ITexture)

        member x.GetApp() = app

        new () = new Processor(15)

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
        let radius = Mod.init 15
        let tex = radius |> Mod.map(fun r -> proc.ExecuteProgram r)

        use app = proc.GetApp()
        let win = app.CreateSimpleRenderWindow(samples = 1)

        win.Keyboard.KeyDown(Keys.U).Values.Subscribe(fun _ -> transact(fun _ -> radius.Value <- if radius.Value < 55 then radius.Value + 5 else radius.Value)) |> ignore
        win.Keyboard.KeyDown(Keys.J).Values.Subscribe(fun _ -> transact(fun _ -> radius.Value <- if radius.Value > 5 then radius.Value - 5 else radius.Value)) |> ignore

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

