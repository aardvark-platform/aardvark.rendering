namespace Aardvark.Rendering.Tests

open System
open System.IO
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Expecto

[<AutoOpen>]
module PixData =

    let private rng = RandomSystem(1)

    module PixVolume =

        let random (size : V3i) =
            let pv = PixVolume<byte>(Col.Format.RGBA, size)
            pv.GetVolume<C4b>().SetByIndex(fun _ ->
                rng.UniformC4f() |> C4b
            ) |> ignore
            pv

        let compare (offset : V3i) (input : PixVolume<'T>) (output : PixVolume<'T>) =
            for x in 0 .. output.Size.X - 1 do
                for y in 0 .. output.Size.Y - 1 do
                    for z in 0 .. output.Size.Z - 1 do
                        for c in 0 .. output.ChannelCount - 1 do
                            let inputData = input.GetChannel(int64 c)
                            let outputData = output.GetChannel(int64 c)

                            let coord = V3i(x, y, z)
                            let coordInput = coord - offset
                            let ref =
                                if Vec.allGreaterOrEqual coordInput V3i.Zero && Vec.allSmaller coordInput input.Size then
                                    inputData.[coordInput]
                                else
                                    Unchecked.defaultof<'T>

                            Expect.equal outputData.[x, y, z] ref "PixVolume data mismatch"

    module PixImage =

        let toTexture (wantMipMaps : bool) (img : #PixImage) =
            PixTexture2d(PixImageMipMap [| img :> PixImage |], wantMipMaps) :> ITexture

        let solid (size : V2i) (color : C4b) =
            let pi = PixImage<byte>(Col.Format.RGBA, size)
            pi.GetMatrix<C4b>().SetByCoord(fun _ -> color) |> ignore
            pi

        let private randomGeneric<'T> (getValue : unit -> 'T) (format : Col.Format) (size : V2i) =
            let pi = PixImage<'T>(format, size)
            for c in pi.ChannelArray do
                c.SetByIndex(ignore >> getValue) |> ignore
            pi

        let random8ui' = randomGeneric (rng.UniformUInt >> uint8)
        let random8i' = randomGeneric (rng.UniformInt >> int8)
        let random16ui' = randomGeneric (rng.UniformUInt >> uint16)
        let random16i' = randomGeneric (rng.UniformInt >> int16)
        let random32ui' = randomGeneric rng.UniformUInt
        let random32i' = randomGeneric rng.UniformInt
        let random32f' = randomGeneric rng.UniformFloatClosed

        let random8ui   = random8ui' Col.Format.RGBA
        let random8i    = random8i' Col.Format.RGBA
        let random16ui  = random16ui' Col.Format.RGBA
        let random16i   = random16i' Col.Format.RGBA
        let random32ui  = random32ui' Col.Format.RGBA
        let random32i   = random32i' Col.Format.RGBA
        let random32f   = random32f' Col.Format.RGBA

        let random = random8ui

        let checkerboard (size : V2i) =
            let mutable colors = HashMap.empty

            let pi = PixImage<byte>(Col.Format.RGBA, size)
            pi.GetMatrix<C4b>().SetByCoord(fun (c : V2l) ->
                let c = c / 16L
                if (c.X + c.Y) % 2L = 0L then
                    C4b.White
                else
                    match colors |> HashMap.tryFind c with
                    | Some c -> c
                    | _ ->
                        let color = C4b(rng.UniformInt(256), rng.UniformInt(256), rng.UniformInt(256), 255)
                        colors <- colors |> HashMap.add c color
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

        let isColor (color : 'T[]) (pi : PixImage<'T>) =
            for c in 0 .. pi.ChannelCount - 1 do
                let data = pi.GetChannel(int64 c)

                for x in 0 .. pi.Size.X - 1 do
                    for y in 0 .. pi.Size.Y - 1 do
                        Expect.equal data.[x, y] color.[c] "PixImage data mismatch"

        let compareWithComparer (comparer : 'T -> 'T -> string -> unit)
                                (offset : V2i) (input : PixImage<'T>) (output : PixImage<'T>) =
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

                        comparer outputData.[x, y] ref "PixImage data mismatch"


        let compare (offset : V2i) (input : PixImage<'T>) (output : PixImage<'T>) =
            compareWithComparer Expect.equal offset input output

        let inline compareWithEpsilon (eps : ^T) (offset : V2i) (input : PixImage< ^T>) (output : PixImage< ^T>) =
            let comp a b =
                let diff = if a > b then a - b else b - a
                Expect.isLessThanOrEqual diff eps
            compareWithComparer comp offset input output

        let compare32f (offset : V2i) (accuracy : Accuracy) (input : PixImage<float32>) (output : PixImage<float32>) =
            let comp a b = Expect.floatClose accuracy (float a) (float b)
            compareWithComparer comp offset input output


[<AutoOpen>]
module ``Test Utilities`` =

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