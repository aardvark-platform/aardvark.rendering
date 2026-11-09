namespace Aardvark.Rendering.Tests.Rendering

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Expecto

module ClearValueTests =
    module private Cases =
        let private inputs =
            let channels =
                [| float Int32.MinValue; float (Int32.MinValue + 1); -16777219.0; -16777217.0
                   -2.0000001; -2.0; Math.BitIncrement(-2.0)
                   Math.BitDecrement(-1.0); -1.0; Math.BitIncrement(-1.0); -0.25; -0.0
                   0.0; 0.25; 0.99999999; Math.BitDecrement(1.0); 1.0; Math.BitIncrement(1.0)
                   1.99999999; Math.BitDecrement(2.0); 2.0; Math.BitIncrement(2.0)
                   16777215.0; 16777216.0; 16777217.0; 16777219.0; 2147483600.0; float Int32.MaxValue |]
            [ for i in 0 .. channels.Length - 1 do
                yield V4d(channels.[i], channels.[(i + 5) % channels.Length], channels.[(i + 11) % channels.Length], channels.[(i + 17) % channels.Length])
              for channel in 0 .. 3 do
                for value in channels do
                    let mutable v = V4d(3.75, -4.75, 5.25, 6.5)
                    v.[channel] <- value
                    yield v ]

        let private checkColor context (integer : V4i) (floating : V4f) (actual : ClearColor) =
            Expect.equal actual.Integer integer (context + ": integer channels")
            for channel in 0 .. 3 do
                Expect.equal (BitConverter.SingleToInt32Bits actual.Float.[channel]) (BitConverter.SingleToInt32Bits floating.[channel]) $"{context}: float channel={channel}"

        let private collect (failures : ResizeArray<string>) context action =
            try action() with error -> failures.Add(context + ": " + error.Message)

        let constructors() =
            let failures = ResizeArray<string>()
            for value in inputs do
                let rgb = C3d(value.X, value.Y, value.Z)
                collect failures $"C3d={rgb}" (fun () -> checkColor "constructor" (V4i(V4d rgb)) (V4f rgb) (ClearColor rgb))
                let rgba = C4d(value.X, value.Y, value.Z, value.W)
                collect failures $"C4d={rgba}" (fun () -> checkColor "constructor" (V4i value) (V4f rgba) (ClearColor rgba))
            Expect.isEmpty failures (String.concat "\n" failures)

        let inline private delegated failures context (value : ^T) integer floating =
            let semantic = Sym.ofString "DoubleColor"
            collect failures (context + ": conversion helper") (fun () -> checkColor context integer floating (ClearValues.Conversion.toColor value))
            let routes =
                [ "ofColor", ClearValues.ofColor value
                  "color helper", ClearValues.empty |> ClearValues.color value
                  "attachment helper", ClearValues.empty |> ClearValues.colorAttachment semantic value
                  "colors helper", ClearValues.empty |> ClearValues.colors (Map.ofList [semantic, value])
                  "default builder", clear { color value }
                  "attachment builder", clear { color semantic value }
                  "map builder", clear { colors (Map.ofList [semantic, value]) }
                  "sequence builder", clear { colors [semantic, value] } ]
            for route, values in routes do
                let label = $"{context}, route={route}"
                collect failures label (fun () ->
                    let actual = ClearValues.tryGetColor semantic values
                    Expect.isSome actual (label + ": effective color")
                    checkColor label integer floating actual.Value
                    Expect.isNone values.Depth (label + ": no depth")
                    Expect.isNone values.Stencil (label + ": no stencil"))

        let delegation() =
            let failures = ResizeArray<string>()
            for value in inputs do
                let rgb = C3d(value.X, value.Y, value.Z)
                let rgba = C4d(value.X, value.Y, value.Z, value.W)
                let i3, f3 = V4i(V4d rgb), V4f rgb
                let i4, f4 = V4i value, V4f rgba
                collect failures $"C3d implicit={rgb}" (fun () -> checkColor "implicit conversion" i3 f3 (ClearColor.op_Implicit rgb))
                collect failures $"C4d implicit={rgba}" (fun () -> checkColor "implicit conversion" i4 f4 (ClearColor.op_Implicit rgba))
                delegated failures $"C3d={rgb}" rgb i3 f3
                delegated failures $"C4d={rgba}" rgba i4 f4
            Expect.isEmpty failures (String.concat "\n" failures)

        let compatibility() =
            for value in [-17.75; -1.0; -0.0; 0.0; 0.25; 0.5; 1.0; 17.75;
                          Double.NaN; Double.NegativeInfinity; Double.PositiveInfinity;
                          float Int32.MinValue - 1024.0; float Int32.MaxValue + 1024.0; -Double.MaxValue; Double.MaxValue] do
                let rgb, rgba = C3d(value), C4d(value, -value, 0.5, 0.25)
                checkColor $"compatible C3d={rgb}" (V4i(V4f rgb)) (V4f rgb) (ClearColor rgb)
                checkColor $"compatible C4d={rgba}" (V4i(V4f rgba)) (V4f rgba) (ClearColor rgba)
            let d2, d3, d4 = V2d(16777217.0, -1.99999999), V3d(16777219.0, -16777217.0, 0.99999999), V4d(3.75, -4.75, 16777217.0, 2147483647.0)
            let f2, f3, f4 = V2f(2.75f, -3.75f), V3f(2.75f, -3.75f, 4.5f), V4f(2.75f, -3.75f, 4.5f, 0.5f)
            let c3, c4 = C3f(2.75f, -3.75f, 4.5f), C4f(2.75f, -3.75f, 4.5f, 0.5f)
            let b3, b4 = C3b(17uy, 31uy, 63uy), C4b(17uy, 31uy, 63uy, 127uy)
            let us3, us4 = C3us(17us, 1023us, 65535us), C4us(17us, 1023us, 65535us, 123us)
            let ui3, ui4 = C3ui(17u, 31u, UInt32.MaxValue), C4ui(17u, 31u, UInt32.MaxValue, 127u)
            let i2, i3, i4 = V2i(-1, 2), V3i(-1, 2, 3), V4i(-1, 2, 3, Int32.MaxValue)
            let u2, u3, u4 = V2ui(1u, 2u), V3ui(1u, 2u, 3u), V4ui(1u, 2u, 3u, UInt32.MaxValue)
            let controls =
                [ "V2d", ClearColor d2, V4i d2, V4f d2
                  "V3d", ClearColor d3, V4i d3, V4f d3
                  "V4d", ClearColor d4, V4i d4, V4f d4
                  "V2f", ClearColor f2, V4i f2, V4f f2
                  "V3f", ClearColor f3, V4i f3, V4f f3
                  "V4f", ClearColor f4, V4i f4, V4f f4
                  "C3f", ClearColor c3, V4i(V4f c3), V4f c3
                  "C4f", ClearColor c4, V4i(V4f c4), V4f c4
                  "C3b", ClearColor b3, V4i b3, V4f(C4f b3)
                  "C4b", ClearColor b4, V4i b4, V4f(C4f b4)
                  "C3us", ClearColor us3, V4i us3, V4f(C4f us3)
                  "C4us", ClearColor us4, V4i us4, V4f(C4f us4)
                  "C3ui", ClearColor ui3, V4i(17, 31, -1, -1), V4f(C4f ui3)
                  "C4ui", ClearColor ui4, V4i(17, 31, -1, 127), V4f(C4f ui4)
                  "V2i", ClearColor i2, V4i i2, V4f i2
                  "V3i", ClearColor i3, V4i i3, V4f i3
                  "V4i", ClearColor i4, i4, V4f i4
                  "V2ui", ClearColor u2, V4i u2, V4f u2
                  "V3ui", ClearColor u3, V4i u3, V4f u3
                  "V4ui", ClearColor u4, V4i u4, V4f u4 ]
            for label, actual, integer, floating in controls do
                checkColor label integer floating actual
                checkColor (label + ": copy constructor") integer floating (ClearColor actual)

    let tests target =
        [
            "Double constructors retain integer precision",     Cases.constructors
            "Conversions and builders retain double precision", Cases.delegation
            "Other constructors and range policies",            Cases.compatibility
        ]
        |> prepareCasesCpu "ClearValues" target
