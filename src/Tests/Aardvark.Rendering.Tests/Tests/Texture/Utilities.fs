namespace Aardvark.Rendering.Tests.Texture

open System
open System.IO
open System.Reflection
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Application
open Aardvark.SceneGraph
open Expecto

module Window =

    let create (testBackend : Backend) =
        IntrospectionProperties.CustomEntryAssembly <- Assembly.GetAssembly(typeof<ISg>)
        Aardvark.Init()

        GL.Config.CheckErrors <- true
        GL.Config.UsePixelUnpackBuffers <- true

        window {
            display Display.Mono
            samples 1
            backend testBackend
            debug true
        }

    let createUse f backend =
        use win = create backend
        f win.Runtime


module PixImage =

    let toTexture (wantMipMaps : bool) (img : #PixImage) =
        PixTexture2d(PixImageMipMap [| img :> PixImage |], wantMipMaps) :> ITexture

    let solid (size : V2i) (color : C4b) =
        let pi = PixImage<byte>(Col.Format.RGBA, size)
        pi.GetMatrix<C4b>().SetByCoord(fun _ -> color) |> ignore
        pi

    let checkerboard (color : C4b) =
        let pi = PixImage<byte>(Col.Format.RGBA, V2i.II * 256)
        pi.GetMatrix<C4b>().SetByCoord(fun (c : V2l) ->
            let c = c / 16L
            if (c.X + c.Y) % 2L = 0L then
                C4b.White
            else
                color
        ) |> ignore
        pi

    let resized (size : V2i) (img : PixImage<byte>) =
        let result = PixImage<byte>(img.Format, size)

        for c in 0L .. img.Volume.Size.Z - 1L do
            let src = img.Volume.SubXYMatrixWindow(c)
            let dst = result.Volume.SubXYMatrixWindow(c)
            dst.SetScaledCubic(src)

        result

    let private desktopPath =
        Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop)

    let saveToDesktop (fileName : string) (img : #PixImage) =
        let dir = Path.combine [desktopPath; "UnitTests"]
        Directory.CreateDirectory(dir) |> ignore
        img.SaveAsImage(Path.combine [dir; fileName])

    let compare (offset : V2i) (input : PixImage<'T>) (output : PixImage<'T>) =
        for x in 0 .. output.Size.X - 1 do
            for y in 0 .. output.Size.Y - 1 do
                for c in 0 .. output.ChannelCount - 1 do
                    let inputData = input.GetChannel(int64 c)
                    let outputData = output.GetChannel(int64 c)

                    let coord = V2i(x, y)
                    let coordInput = coord - offset
                    let ref =
                        if Vec.allGreaterOrEqual coordInput V2i.Zero && Vec.allSmaller coordInput input.Size then
                            inputData.[coordInput]
                        else
                            Unchecked.defaultof<'T>

                    Expect.equal outputData.[x, y] ref "PixImage data mismatch"


[<AutoOpen>]
module ``Texture Test Utilities`` =

    type IBackendTexture with
        member x.Clear(color : C4b) = x.Runtime.Upload(x, PixImage.solid x.Size.XY color)

    type UnexpectedPassException() =
        inherit Exception("Expected an exception but none occurred")

    let shouldThrowArgExn (message : string) (f : unit -> unit) =
        try
            f()
            raise <| UnexpectedPassException()
        with
        | :? ArgumentException as exn -> Expect.stringContains exn.Message message "Unexpected expection message"
        | :? UnexpectedPassException -> failwith "Expected ArgumentException but got none"
        | exn -> failwithf "Expected ArgumentException but got %A" <| exn.GetType()

    let testColors = [|
            C4b.BurlyWood
            C4b.Crimson
            C4b.DarkOrange
            C4b.ForestGreen
            C4b.AliceBlue
            C4b.Indigo

            C4b.Cornsilk
            C4b.Cyan
            C4b.FireBrick
            C4b.Coral
            C4b.Chocolate
            C4b.LawnGreen
        |]

    let prepareCases (backend : Backend) (name : string) (cases : List<string * (IRuntime -> unit)>) =
        cases |> List.map (fun (name, test) ->
            testCase name (fun () -> Window.createUse test backend)
        )
        |> testList name