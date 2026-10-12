namespace Aardvark.Rendering.Tests.Rendering

open System
open System.Collections.Generic
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open Expecto

module Blending =

    module private Semantic =
        let Output0 = Sym.ofString "Output0"
        let Output1 = Sym.ofString "Output1"
        let Output2 = Sym.ofString "Output2"

    module private BlendMode =
        let ColorMulAlphaAdd =
            { BlendMode.Blend with
                SourceColorFactor = BlendFactor.Zero
                SourceAlphaFactor = BlendFactor.One
                DestinationColorFactor = BlendFactor.SourceColor
                DestinationAlphaFactor = BlendFactor.One }

        let DisabledWithConstant =
            { Enabled                = false
              SourceColorFactor      = BlendFactor.ConstantColor
              SourceAlphaFactor      = BlendFactor.ConstantAlpha
              DestinationColorFactor = BlendFactor.Zero
              DestinationAlphaFactor = BlendFactor.Zero
              ColorOperation         = BlendOperation.Add
              AlphaOperation         = BlendOperation.Add}

    module private Shader =
        open FShade

        let inline output0 (c : ^Color) (v : Effects.Vertex) =
            let c = v4f c
            fragment {
                return {| Output0 = c |}
            }

        let inline output1 (c : ^Color) (v : Effects.Vertex) =
            let c = v4f c
            fragment {
                return {| Output1 = c |}
            }

        let inline output2 (c : ^Color) (v : Effects.Vertex) =
            let c = v4f c
            fragment {
                return {| Output2 = c |}
            }

    module private Equality =

        let private comparer = EqualityComparer<BlendMode>.Default

        let private configurations =
            [| BlendMode.Blend; BlendMode.Add; BlendMode.Multiply
               { BlendMode.Blend with ColorOperation = BlendOperation.Minimum; AlphaOperation = BlendOperation.Maximum }
               { BlendMode.Blend with
                    SourceColorFactor = enum<BlendFactor> -1
                    SourceAlphaFactor = enum<BlendFactor> 999
                    DestinationColorFactor = enum<BlendFactor> Int32.MaxValue
                    DestinationAlphaFactor = enum<BlendFactor> Int32.MinValue
                    ColorOperation = enum<BlendOperation> 103
                    AlphaOperation = enum<BlendOperation> -7 } |]

        let private differences (mode : BlendMode) =
            [| "SourceColorFactor", { mode with SourceColorFactor = enum<BlendFactor> (int mode.SourceColorFactor ^^^ 1) }
               "SourceAlphaFactor", { mode with SourceAlphaFactor = enum<BlendFactor> (int mode.SourceAlphaFactor ^^^ 1) }
               "DestinationColorFactor", { mode with DestinationColorFactor = enum<BlendFactor> (int mode.DestinationColorFactor ^^^ 1) }
               "DestinationAlphaFactor", { mode with DestinationAlphaFactor = enum<BlendFactor> (int mode.DestinationAlphaFactor ^^^ 1) }
               "ColorOperation", { mode with ColorOperation = enum<BlendOperation> (int mode.ColorOperation ^^^ 1) }
               "AlphaOperation", { mode with AlphaOperation = enum<BlendOperation> (int mode.AlphaOperation ^^^ 1) } |]

        let private check context expected (left : BlendMode) (right : BlendMode) =
            for reverse in [false; true] do
                let a, b = if reverse then right, left else left, right
                let context = $"{context}, reverse={reverse}, left={a}, right={b}"
                Expect.equal (a.Equals b) expected $"{context}: typed equality"
                Expect.equal ((a :> IEquatable<BlendMode>).Equals b) expected $"{context}: IEquatable delegation"
                Expect.equal (a.Equals(box b)) expected $"{context}: object overload"
                Expect.equal (Object.Equals(box a, box b)) expected $"{context}: object equality"
                Expect.equal (comparer.Equals(a, b)) expected $"{context}: generic equality"
                Expect.equal (a = b) expected $"{context}: FSharp equality"
                if expected then
                    Expect.equal (a.GetHashCode()) (b.GetHashCode()) $"{context}: equal-value hash consistency"
                    Expect.equal (hash a) (hash b) $"{context}: FSharp hash consistency"

        let enabledConfiguration (_ : IRuntime) =
            for mode in configurations do
                let copy = { mode with Enabled = true }
                check "identical enabled configuration" true mode copy
                for field, different in differences mode do
                    check $"differing {field}" false mode different
                Expect.isFalse (mode.Equals(null : obj)) $"{mode}: null is not a blend mode"
                Expect.isFalse (mode.Equals(box 1)) $"{mode}: another object type is not a blend mode"

        let disabledAndMixed (_ : IRuntime) =
            for first in configurations do
                for second in configurations do
                    let disabled = { first with Enabled = false }
                    let otherDisabled = { second with Enabled = false }
                    check "disabled configurations are ignored" true disabled otherDisabled
                    check "enabled versus disabled" false first otherDisabled
                    Expect.equal (disabled.GetHashCode()) 0 $"{disabled}: disabled hash remains zero"
                check "default disabled mode" true BlendMode.None { first with Enabled = false }

        let adaptiveAssignments (_ : IRuntime) =
            for mode in configurations do
                let value = cval mode
                let mutable evaluations = 0
                let mapped = value |> AVal.map (fun m -> evaluations <- evaluations + 1; m)
                AVal.force mapped |> ignore
                for i in 1 .. 100 do
                    transact (fun () -> value.Value <- { mode with Enabled = true })
                    Expect.equal (AVal.force mapped) mode $"{mode}, assignment={i}: equal adaptive value"
                Expect.equal evaluations 1 $"{mode}: identical assignments do not invalidate"
                for field, different in differences mode do
                    let before = evaluations
                    transact (fun () -> value.Value <- different)
                    Expect.equal (AVal.force mapped) different $"{mode}, {field}: changed adaptive value"
                    Expect.equal evaluations (before + 1) $"{mode}, {field}: differing field invalidates"
                    transact (fun () -> value.Value <- mode)
                    AVal.force mapped |> ignore

        [<MethodImpl(MethodImplOptions.NoInlining)>]
        let private typedComparisons (pairs : struct (BlendMode * BlendMode * bool)[]) rounds =
            let mutable equal = 0
            for _ in 1 .. rounds do
                for struct (left, right, _) in pairs do
                    if left.Equals right then equal <- equal + 1
            equal

        [<MethodImpl(MethodImplOptions.NoInlining)>]
        let private genericComparisons (pairs : struct (BlendMode * BlendMode * bool)[]) rounds =
            let mutable equal = 0
            for _ in 1 .. rounds do
                for struct (left, right, _) in pairs do
                    if comparer.Equals(left, right) then equal <- equal + 1
            equal

        let comparisonAllocations (_ : IRuntime) =
            let mode = BlendMode.Blend
            let pairs =
                Array.append
                    [| struct (mode, { mode with Enabled = true }, true)
                       struct (BlendMode.None, BlendMode.DisabledWithConstant, true)
                       struct (mode, BlendMode.None, false)
                       struct (BlendMode.None, mode, false) |]
                    (differences mode |> Array.map (fun (_, different) -> struct (mode, different, false)))
            // Debug retains the existing enabled-state match tuple. Disabled
            // comparisons isolate that unchanged overhead in both configurations.
            let controls = pairs |> Array.map (fun struct (a, b, _) -> struct ({ a with Enabled = false }, { b with Enabled = false }, true))
            let rounds = 10000
            let expected = rounds * (pairs |> Array.sumBy (fun struct (_, _, equal) -> if equal then 1 else 0))
            typedComparisons pairs rounds |> ignore
            genericComparisons pairs rounds |> ignore
            typedComparisons controls rounds |> ignore
            genericComparisons controls rounds |> ignore
            let beforeTyped = GC.GetAllocatedBytesForCurrentThread()
            let typed = typedComparisons pairs rounds
            let typedBytes = GC.GetAllocatedBytesForCurrentThread() - beforeTyped
            let beforeGeneric = GC.GetAllocatedBytesForCurrentThread()
            let generic = genericComparisons pairs rounds
            let genericBytes = GC.GetAllocatedBytesForCurrentThread() - beforeGeneric
            let beforeTypedControl = GC.GetAllocatedBytesForCurrentThread()
            let typedControl = typedComparisons controls rounds
            let typedControlBytes = GC.GetAllocatedBytesForCurrentThread() - beforeTypedControl
            let beforeGenericControl = GC.GetAllocatedBytesForCurrentThread()
            let genericControl = genericComparisons controls rounds
            let genericControlBytes = GC.GetAllocatedBytesForCurrentThread() - beforeGenericControl
            Expect.equal typed expected "typed comparison results"
            Expect.equal generic expected "generic comparison results"
            Expect.equal typedControl (rounds * controls.Length) "typed disabled-control results"
            Expect.equal genericControl (rounds * controls.Length) "generic disabled-control results"
            Expect.equal (typedBytes, genericBytes) (typedControlBytes, genericControlBytes) $"typed and generic comparisons, pairs={pairs.Length}, rounds={rounds}: allocated bytes match disabled controls"

    module Cases =

        let globalBlend (mode : BlendMode) (colorOp : float32 -> float32 -> float32) (alphaOp : float32 -> float32 -> float32) (runtime : IRuntime) =
            let clearColor = C4f(0.1, 0.2, 0.3, 0.4)
            let blendedColor = C4f(0.1, 0.1, 0.1, 0.1)
            let expectedColor =
                C4f(
                    colorOp clearColor.R blendedColor.R,
                    colorOp clearColor.G blendedColor.G,
                    colorOp clearColor.B blendedColor.B,
                    alphaOp clearColor.A blendedColor.A
                )

            use signature =
                runtime.CreateFramebufferSignature([
                    DefaultSemantic.Colors, TextureFormat.Rgba32f
                ])

            use task =
                Sg.fullScreenQuad
                |> Sg.shader {
                    do! DefaultSurfaces.constantColor blendedColor
                }
                |> Sg.blendMode' mode
                |> Sg.compile runtime signature

            let output =
                let clear = clear { color clearColor }
                task |> RenderTask.renderToColorWithClear (~~V2i(256)) clear

            output.Acquire()

            try
                let result = output.GetValue().Download().AsPixImage<float32>()
                result |> PixImage.isColor32f Accuracy.medium (expectedColor.ToArray())
            finally
                output.Release()

        let perAttachmentBlend (runtime : IRuntime) =
            let clearColor = C4f(0.1, 0.2, 0.3, 0.4)
            let blendedColor0 = C4f(0.1, 0.1, 0.1, 0.1)
            let blendedColor1 = C4f.White
            let blendedColor2 = C4f(0.5, 0.5, 0.5, 0.5)
            let blendConstant = C4f(0.3, 0.2, 0.4, 0.5)
            let expectedColor0 = clearColor + blendedColor0
            let expectedColor1 = C4f.White
            let expectedColor2 = clearColor * blendedColor2

            use signature =
                runtime.CreateFramebufferSignature([
                    Semantic.Output0, TextureFormat.Rgba32f
                    Semantic.Output1, TextureFormat.Rgba32f
                    Semantic.Output2, TextureFormat.Rgba32f
                ])

            let modes =
                Map.ofList [
                    Semantic.Output0, BlendMode.Add
                    Semantic.Output1, BlendMode.DisabledWithConstant
                    Semantic.Output2, BlendMode.Multiply
                ]

            use task =
                Sg.fullScreenQuad
                |> Sg.shader {
                    do! Shader.output0 blendedColor0
                    do! Shader.output1 blendedColor1
                    do! Shader.output2 blendedColor2
                }
                |> Sg.blendModes' modes
                |> Sg.blendConstant' blendConstant
                |> Sg.compile runtime signature

            let r0, r1, r2 =
                let fbo = runtime.CreateFramebuffer(signature, ~~V2i(256))
                let clear = clear { color clearColor }
                let output = task |> RenderTask.renderToWithClear fbo clear
                let o0 = output.GetOutputTexture(Semantic.Output0)
                let o1 = output.GetOutputTexture(Semantic.Output1)
                let o2 = output.GetOutputTexture(Semantic.Output2)
                o0, o1, o2

            r0.Acquire(); r1.Acquire(); r2.Acquire()

            try
                let p0 = r0.GetValue().Download().AsPixImage<float32>()
                p0 |> PixImage.isColor (expectedColor0.ToArray())

                let p1 = r1.GetValue().Download().AsPixImage<float32>()
                p1 |> PixImage.isColor (expectedColor1.ToArray())

                let p2 = r2.GetValue().Download().AsPixImage<float32>()
                p2 |> PixImage.isColor (expectedColor2.ToArray())

            finally
                r0.Release(); r1.Release(); r2.Release()

    let tests (target: TestTarget) =
        [
            if target = TestTarget.Cpu then
                "Equality.Enabled configuration fields",   Equality.enabledConfiguration
                "Equality.Disabled and mixed states",      Equality.disabledAndMixed
                "Equality.Adaptive identical assignments", Equality.adaptiveAssignments
                "Equality.Typed and generic allocations",  Equality.comparisonAllocations

            if target.IsGpu then
                "Global (add)",                       Cases.globalBlend BlendMode.Add (+) (+)
                "Global (color multiply, alpha add)", Cases.globalBlend BlendMode.ColorMulAlphaAdd (*) (+)
                "Per attachment",                     Cases.perAttachmentBlend
        ]
        |> prepareCases "Blending" target