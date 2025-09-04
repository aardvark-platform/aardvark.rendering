namespace Aardvark.Rendering.Tests

open System
open System.Reflection
open System.Runtime.InteropServices
open System.Text.RegularExpressions
open System.IO
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Expecto

module EmbeddedResource =

    let get (path : string) =
        let asm = Assembly.GetExecutingAssembly()
        let name = Regex.Replace(asm.ManifestModule.Name, @"\.(exe|dll)$", "", RegexOptions.IgnoreCase)
        let path = Regex.Replace(path, @"(\\|\/)", ".")
        let stream = asm.GetManifestResourceStream(name + "." + path)
        if stream <> null then stream
        else failwithf "Cannot open resource stream with name '%s'" path

    let loadPixImage<'T> (path : string) =
        use stream = get path
        PixImage.Load(stream).AsPixImage<'T>()

    let getTexture (textureParams : TextureParams) (path : string) =
        let openStream = fun () -> get path
        StreamTexture(openStream, textureParams) :> ITexture

module Rnd =

    let rng = RandomSystem(1)

    let uint8 : unit -> _  = rng.UniformUInt >> uint8
    let int8 : unit -> _   = rng.UniformInt >> int8
    let uint16 : unit -> _ = rng.UniformUInt >> uint16
    let int16 : unit -> _  = rng.UniformInt >> int16
    let uint32 : unit -> _ = rng.UniformUInt
    let int32 : unit -> _  = rng.UniformInt
    let uint64 : unit -> _ = rng.UniformULong
    let int64 : unit -> _  = rng.UniformLong
    let float32: unit -> _ = rng.UniformFloatClosed

    let c3b() = C3b(uint8(), uint8(), uint8())


module Array =

    let random gen size =
        Array.init size (ignore >> gen)

    let randomInts =
        random (fun _ -> (Rnd.rng.UniformInt 32000) - 16000)

    let randomV4fs =
        random Rnd.rng.UniformV4f

    let strided (delta : int) (arr : 'T[]) =
        [0 .. delta .. arr.Length - 1]
        |> List.map (fun i -> arr.[i])
        |> Array.ofList

    let subvector (start : int) (delta : int) (count : int) (arr : 'T[]) =
        let upper = min arr.Length (start + count * delta)
        [start .. delta .. (upper - 1)]
        |> List.map (fun i -> arr.[i])
        |> Array.ofList

[<AutoOpen>]
module PixData =

    module PixVolume =

        let private randomGeneric<'T> (getValue : unit -> 'T) (format : Col.Format) (size : V3i) =
            let pi = PixVolume<'T>(format, size)
            for c in pi.ChannelArray do
                c.SetByIndex(ignore >> getValue) |> ignore
            pi

        let random8ui'  = randomGeneric Rnd.uint8
        let random8i'   = randomGeneric Rnd.int8
        let random16ui' = randomGeneric Rnd.uint16
        let random16i'  = randomGeneric Rnd.int16
        let random32ui' = randomGeneric Rnd.uint32
        let random32i'  = randomGeneric Rnd.int32
        let random32f'  = randomGeneric Rnd.float32

        let random8ui   = random8ui' Col.Format.RGBA
        let random8i    = random8i' Col.Format.RGBA
        let random16ui  = random16ui' Col.Format.RGBA
        let random16i   = random16i' Col.Format.RGBA
        let random32ui  = random32ui' Col.Format.RGBA
        let random32i   = random32i' Col.Format.RGBA
        let random32f   = random32f' Col.Format.RGBA

        let compareWithComparer (comparer : 'T -> 'T -> string -> unit)
                                (offset : V3i) (input : PixVolume<'T>) (output : PixVolume<'T>) =
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

                            let message =
                                let t = if c < 4 then "color" else "alpha"
                                $"PixVolume {t} data mismatch at [{x}, {y}, {z}]"

                            comparer outputData.[x, y, z] ref message

        let compare (offset : V3i) (input : PixVolume<'T>) (output : PixVolume<'T>) =
            compareWithComparer Expect.equal offset input output

        let inline compareWithEpsilon (eps : ^T) (offset : V3i) (input : PixVolume< ^T>) (output : PixVolume< ^T>) =
            let comp a b =
                let diff = if a > b then a - b else b - a
                Expect.isLessThanOrEqual diff eps
            compareWithComparer comp offset input output

        let compare32f (offset : V3i) (accuracy : Accuracy) (input : PixVolume<float32>) (output : PixVolume<float32>) =
            let comp a b = Expect.floatClose accuracy (float a) (float b)
            compareWithComparer comp offset input output

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

        let random8ui'  = randomGeneric Rnd.uint8
        let random8i'   = randomGeneric Rnd.int8
        let random16ui' = randomGeneric Rnd.uint16
        let random16i'  = randomGeneric Rnd.int16
        let random32ui' = randomGeneric Rnd.uint32
        let random32i'  = randomGeneric Rnd.int32
        let random32f'  = randomGeneric Rnd.float32

        let random8ui   = random8ui' Col.Format.RGBA
        let random8i    = random8i' Col.Format.RGBA
        let random16ui  = random16ui' Col.Format.RGBA
        let random16i   = random16i' Col.Format.RGBA
        let random32ui  = random32ui' Col.Format.RGBA
        let random32i   = random32i' Col.Format.RGBA
        let random32f   = random32f' Col.Format.RGBA

        let checkerboard (size : V2i) =
            let mutable colors = HashMap.empty

            let pi = PixImage<byte>(Col.Format.RGBA, size)
            pi.GetMatrix<C4b>().SetByCoord(fun (c : V2l) ->
                let c = c / 11L
                if (c.X + c.Y) % 2L = 0L then
                    C4b.White
                else
                    match colors |> HashMap.tryFind c with
                    | Some c -> c
                    | _ ->
                        let color = C4b(Rnd.c3b())
                        colors <- colors |> HashMap.add c color
                        color
            ) |> ignore
            pi

        let cropped (region : Box2i) (img : PixImage<'T>) =
            PixImage<'T>(
                img.Format,
                img.Volume.SubVolume(V3i(region.Min, 0), V3i(region.Size, img.ChannelCount))
            )

        let resized (size : V2i) (img : PixImage<'T>) =
            let result = PixImage<'T>(img.Format, size)

            for c in 0L .. img.Volume.Size.Z - 1L do
                let src = img.Volume.SubXYMatrixWindow(c)
                let dst = result.Volume.SubXYMatrixWindow(c)

                if typeof<'T> = typeof<uint8> then
                    let src = unbox<Matrix<uint8>> src
                    let dst = unbox<Matrix<uint8>> dst
                    dst.SetScaledCubic(src)

                elif typeof<'T> = typeof<uint16> then
                    let src = unbox<Matrix<uint16>> src
                    let dst = unbox<Matrix<uint16>> dst
                    dst.SetScaledCubic(src)

                else
                    failwithf "Unsupported type %A" typeof<'T>

            result

        let transformed (trafo : ImageTrafo) (img : PixImage<'T>) =
            img.Transformed(trafo).AsPixImage<'T>()

        let private desktopPath =
            Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop)

        let saveToDesktop (fileName : string) (img : #PixImage) =
            let dir = Path.combine [desktopPath; "UnitTests"]
            Directory.CreateDirectory(dir) |> ignore
            img.Save(Path.combine [dir; fileName])

        let isColorWithComparer (comparer : 'T -> 'T -> string -> unit)
                                (color : 'T[]) (pi : PixImage<'T>) =
            for c in 0 .. (min color.Length pi.ChannelCount) - 1 do
                let data = pi.GetChannel(int64 c)

                for x in 0 .. pi.Size.X - 1 do
                    for y in 0 .. pi.Size.Y - 1 do
                        comparer data.[x, y] color.[c] $"PixImage data mismatch in channel {c} at ({x}, {y})"

        let isColor (color : 'T[]) (pi : PixImage<'T>) =
            isColorWithComparer Expect.equal color pi

        let inline isColorWithDist (maxDist : int64) (color : 'T[]) (pi : PixImage<'T>) =
            isColorWithComparer (fun x y -> Expect.isLessThanOrEqual (abs (int64 x - int64 y)) maxDist) color pi

        let isColor32f (accuracy : Accuracy) (color : float32[]) (pi : PixImage<float32>) =
            let comp a b = Expect.floatClose accuracy (float a) (float b)
            isColorWithComparer comp color pi

        let isColor64f (accuracy : Accuracy) (color : float[]) (pi : PixImage<float>) =
            let comp a b = Expect.floatClose accuracy a b
            isColorWithComparer comp color pi

        let inline rootMeanSquaredError (input : PixImage<'T>) (output : PixImage<'T>) =
            sqrt (PixImage.meanSquaredError input output)

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

                        let message =
                            let t = if c < 4 then "color" else "alpha"
                            $"PixImage {t} data mismatch at [{x}, {y}]"

                        comparer outputData.[x, y] ref message


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
module ``Expecto Extensions`` =
    open FsCheck

    type ZeroOne = ZeroOne of float

    type Generators() =
        static member V2d =
            { new Arbitrary<V2d>() with
                override x.Generator =
                    gen {
                        let! (NormalFloat a) = Arb.generate<NormalFloat>
                        let! (NormalFloat b)= Arb.generate<NormalFloat>
                        return V2d(a,b)
                    }
            }

        static member ZeroOne =
            { new Arbitrary<ZeroOne>() with
                override x.Generator =
                    gen {
                        let! (NormalFloat a) = Arb.generate<NormalFloat>
                        return ZeroOne (abs a % 1.0)
                    }
            }

    module Expect =

        let inline v4dClose (accuracy : Accuracy) (actual : ^T1) (expected : ^T2) (message : string) =
            let actual = v4d actual
            let expected = v4d expected
            Expect.floatClose accuracy actual.X expected.X message
            Expect.floatClose accuracy actual.Y expected.Y message
            Expect.floatClose accuracy actual.Z expected.Z message
            Expect.floatClose accuracy actual.W expected.W message

        let inline approxEqualAux< ^a, ^b when (^a or ^b) : (static member ApproximateEquals : ^a * ^a * float -> bool)> (_ : ^ b) (a : 'a) (b : 'a) (eps : float) =
            (((^a or ^b) : (static member ApproximateEquals : ^a * ^a * float -> bool) (a, b, eps)))

        let inline approxEquals a b eps msg =
            if not (approxEqualAux Unchecked.defaultof<Fun> a b eps) then
                Expect.equal a b msg

        let inline relativeApproxEquals (a : float) (b : float) eps msg =
            let scale = max (abs a) (abs b)
            if not (Fun.IsTiny(scale, eps)) then
                let ra = a / scale
                let rb = b / scale

                if not (Fun.ApproximateEquals(ra, rb, eps)) then
                    Expect.equal a b msg

    module ExpectoOverrides =

        let private config =
            {
                FsCheckConfig.defaultConfig with
                    arbitrary = [ typeof<Generators> ] @ FsCheckConfig.defaultConfig.arbitrary
                    maxTest = 100000
            }

        let testProperty a b =
            testPropertyWithConfig config a b


    // Utility to run Expecto tests synchronously in the current thread.
    // Prints a summary at the end.
    let runTestsSynchronously (stopOnFail : bool) (tests : Test) =
        let failed = ResizeArray<string>()
        let skipped = ResizeArray<string>()
        let mutable passed = 0

        let tests = Test.toTestCodeList tests
        let count = tests.Length

        let printSummary() =
            let print fmt =
                Printf.kprintf (fun str ->
                    if failed.Count = 0 then Log.line "%s" str
                    else Log.error "%s" str
                ) fmt

            Report.Line()
            print $"Total: {count}, Passed: {passed}, Failed: {failed.Count}, Skipped: {skipped.Count}"

            if failed.Count > 0 then
                Report.Line()
                print "Failed tests:"
            for t in failed do
                print "  %s" t

            if skipped.Count > 0 then
                Report.Line()
                print "Skipped tests:"
            for t in skipped do
                print "  %s" t

        let rec run (index : int) (tests : FlatTest list) =
            match tests with
            | t :: rem ->
                let name = t.fullName "."
                let title = $"({index + 1} / {count}) {name}"
                let mutable success = false

                if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
                    try Console.Title <- title
                    with _ -> ()

                Report.Begin title

                match t.test with
                | Sync f ->
                    try
                        f()
                        success <- true
                        inc &passed
                    with
                    | :? IgnoreException as exn ->
                        success <- true
                        Log.line $"Test skipped: {exn.Message}"
                        skipped.Add name

                    | exn ->
                        Log.error "%A" exn
                        failed.Add name
                | _ ->
                    Log.error "Test not supported"
                    skipped.Add name

                Report.End(if success then " - passed" else " - failed") |> ignore

                if success || not stopOnFail then
                    run (index + 1) rem

            | _ ->
                ()

        run 0 tests
        printSummary()
        if failed.Count = 0 then 0 else 1

[<AutoOpen>]
module ``RenderTo Utilities`` =
    open Aardvark.SceneGraph
    open FSharp.Data.Adaptive.Operators

    module private Shader =
        open FShade

        let diffuseSamplerMS =
            sampler2dMS {
                texture uniform?DiffuseColorTexture
                filter Filter.MinMagMipLinear
                addressU WrapMode.Wrap
                addressV WrapMode.Wrap
            }

        type Fragment = {
            [<FragCoord>] coord : V4f
        }

        let resolveSingle (f : Fragment) =
            fragment {
                return diffuseSamplerMS.Read(V2i f.coord.XY, 0)
            }

    type RenderTo =
        {
            Task : IRenderTask
            Disposable : IDisposable
        }

        member x.Dispose() =
            x.Task.Dispose()
            x.Disposable.Dispose()

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    let renderQuadTo (runtime : IRuntime) (attachments : seq<Symbol * TextureFormat>) (samples : int)=
        let signature =
             runtime.CreateFramebufferSignature(attachments, samples)

        let task =
            let drawCall =
                DrawCallInfo(
                    FaceVertexCount = 4,
                    InstanceCount = 1
                )

            let positions = [| V3f(-0.5,-0.5,0.0); V3f(0.5,-0.5,0.0); V3f(-0.5,0.5,0.0); V3f(0.5,0.5,0.0) |]

            let stencilMode =
                { StencilMode.None with
                    Pass = StencilOperation.Replace;
                    Reference = 3 }

            drawCall
            |> Sg.render IndexedGeometryMode.TriangleStrip
            |> Sg.vertexAttribute DefaultSemantic.Positions (AVal.constant positions)
            |> Sg.stencilMode' stencilMode
            |> Sg.shader {
                do! DefaultSurfaces.trafo
            }
            |> Sg.compile runtime signature

        { Task = task
          Disposable = signature }

    let renderQuadToDepthStencil (runtime : IRuntime) (format : TextureFormat) (samples : int)
                                 (clear : ClearValues) (size : V2i) (f : IBackendTexture -> 'T) =
        use task =
            let atts = [ DefaultSemantic.DepthStencil, format ]
            renderQuadTo runtime atts samples

        let buffer =
            task.Task |> RenderTask.renderToDepthWithClear (AVal.constant size) clear

        buffer.Acquire()

        try
            f <| buffer.GetValue()
        finally
            buffer.Release()

    let resolveAndDownloadDepth (texture : IBackendTexture) =

        if texture.Samples > 1 then
            let runtime = texture.Runtime :?> IRuntime
            use signature = runtime.CreateFramebufferSignature([ DefaultSemantic.Colors, TextureFormat.R32f])

            use task =
                Sg.fullScreenQuad
                |> Sg.diffuseTexture' texture
                |> Sg.shader {
                    do! Shader.resolveSingle
                }
                |> Sg.compile runtime signature

            let size = texture.Size.XY
            let buffer = task |> RenderTask.renderToColor ~~size
            buffer.Acquire()

            try
                let depth = buffer.GetValue().Download().AsPixImage<float32>()
                depth.GetChannel(0L)

            finally
                buffer.Release()

        else
            texture.DownloadDepth()

    module Expect =

        module private Matrix =

            let private comp (f : 'T -> 'T -> bool) (matrix : Matrix<'T>) =
                let mutable res = matrix.[0, 0]

                for y = 0 to int matrix.Size.Y - 1 do
                    for x = 0 to int matrix.Size.X - 1 do
                        if f matrix.[x, y] res then
                            res <- matrix.[x, y]

                res

            let min (matrix : Matrix<'T>) = comp (<) matrix
            let max (matrix : Matrix<'T>) = comp (>) matrix

        let validDepthResult (depthResult : Matrix<float32>) (accuracy : Accuracy) (expectedSize : V2i) (expectedMin : float) (expectedMax : float) =
            let min = float <| Matrix.min depthResult
            let max = float <| Matrix.max depthResult

            Expect.equal (V2i depthResult.Size) expectedSize "Unexpected depth texture size"
            Expect.isLessThan min 1.0 "All depth one"
            Expect.floatClose accuracy min expectedMin "Unexpected min depth value"
            Expect.floatClose accuracy max expectedMax "Unexpected max depth value"

        let validStencilResult (stencilResult : Matrix<int>) (expectedSize : V2i) (expectedMin : int) (expectedMax : int) =
            let min = Matrix.min stencilResult
            let max = Matrix.max stencilResult

            Expect.equal (V2i stencilResult.Size) expectedSize "Unexpected stencil texture size"
            Expect.equal min expectedMin "Unexpected min stencil value"
            Expect.equal max expectedMax "Unexpected max stencil value"


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

    let requireFeatures (check: Vulkan.DeviceFeatures -> bool) (message: string) (runtime: IRuntime) =
        match runtime with
        | :? Vulkan.Runtime as runtime when not <| check runtime.Device.EnabledFeatures -> skiptest message
        | _ -> ()