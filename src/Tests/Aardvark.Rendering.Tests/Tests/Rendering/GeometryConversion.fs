namespace Aardvark.Rendering.Tests.Rendering

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open FSharp.Data.Adaptive
open Expecto

module GeometryConversion =

    module private Cases =

        let private paddingKeys = Array.init 128 (fun i -> Symbol.Create $"GeometryConversionPadding{i}")
        let private scalar = Symbol.Create "GeometryConversionScalar"
        let private vector = Symbol.Create "GeometryConversionVector"
        let private ignored = Symbol.Create "GeometryConversionNull"
        let private externalName = "GeometryConversionExternal"

        let private positions kind count : Array =
            let values = Array.init count (fun i -> V3d(float i, -float i, float i * 0.25))
            match kind with
            | 0 -> values |> Array.map V3f :> Array
            | 1 -> values :> Array
            | 2 -> values |> Array.map (fun p -> V4f(V3f p, 1.0f)) :> Array
            | _ -> values |> Array.map (fun p -> V4d(p, 1.0)) :> Array

        // SymbolDict enumerates hash buckets, not insertion order. Select and
        // verify an actual ordering instead of guessing which attribute is first.
        let private attributes (positions : Array) (padding : Array) insertPositionsFirst paddingFirst =
            let found =
                seq {
                    for capacity in [0; 17; 67; 257] do
                        for key in paddingKeys do
                            let attributes = SymbolDict<Array>(capacity)
                            if insertPositionsFirst then attributes.[DefaultSemantic.Positions] <- positions
                            attributes.[key] <- padding
                            if not insertPositionsFirst then attributes.[DefaultSemantic.Positions] <- positions
                            yield attributes
                }
                |> Seq.tryFind (fun attributes ->
                    let first = attributes |> Seq.head
                    (first.Key <> DefaultSemantic.Positions) = paddingFirst)
            match found with
            | Some attributes ->
                let first = attributes |> Seq.head
                Expect.equal first.Value.Length (if paddingFirst then padding.Length else positions.Length) "Verified first attribute length"
                Expect.equal (first.Key <> DefaultSemantic.Positions) paddingFirst "Verified actual SymbolDict enumeration"
                attributes
            | None -> failtestf "Cannot construct verified enumeration: insert positions first=%b, padding first=%b" insertPositionsFirst paddingFirst

        let private geometry attributes indices =
            let singles = SymbolDict<obj>()
            singles.[scalar] <- box 41
            singles.[vector] <- box (V3d(1.25, -2.5, 3.75))
            singles.[ignored] <- null
            IndexedGeometry(Mode = IndexedGeometryMode.PointList, IndexedAttributes = attributes,
                            IndexArray = indices, SingleAttributes = singles)

        let private same expected actual context =
            Expect.isTrue (Object.ReferenceEquals(expected, actual)) context

        let private verify (source : IndexedGeometry) count context =
            let suppliedValue = AVal.init 99
            let supplied = Map.ofList [externalName, suppliedValue :> IAdaptiveValue]
            let snapshots = source.IndexedAttributes |> Seq.map (fun pair -> pair.Value, pair.Value.Clone() :?> Array) |> Seq.toArray
            let converted = Geometry.ofIndexedGeometry supplied source
            let calls = AVal.force converted.call
            Expect.equal calls [| DrawCallInfo(count) |] $"{context}: exact count and unchanged instance, index, base-vertex and first-instance fields"
            same calls (AVal.force converted.call) $"{context}: constant draw array identity"
            Expect.equal converted.vertexAttributes.Count source.IndexedAttributes.Count $"{context}: all vertex attributes retained"
            for KeyValue(name, data) in source.IndexedAttributes do
                let adaptive = converted.vertexAttributes.[name]
                let buffer = AVal.force adaptive :?> ArrayBuffer
                same data buffer.Data $"{context}, attribute={name}: source array identity"
                same buffer (AVal.force adaptive) $"{context}, attribute={name}: stable buffer identity"
                Expect.equal buffer.ElementType (data.GetType().GetElementType()) $"{context}, attribute={name}: element type"
            match source.IndexArray, converted.indices with
            | null, None -> ()
            | indices, Some view when not (isNull indices) ->
                let buffer = AVal.force view.Buffer :?> ArrayBuffer
                same indices buffer.Data $"{context}: index array identity"
                same buffer (AVal.force view.Buffer) $"{context}: stable index buffer identity"
                Expect.equal view.ElementType (indices.GetType().GetElementType()) $"{context}: index element type"
                Expect.equal (view.Offset, view.Stride, view.IsSingleValue, view.Normalized) (0, 0, false, true) $"{context}: index view metadata"
            | _ -> failtestf "%s: indexed/non-indexed selection changed" context
            Expect.equal converted.uniforms.Count 3 $"{context}: supplied and converted uniforms, null single attribute ignored"
            same suppliedValue converted.uniforms.[externalName] $"{context}: supplied adaptive identity"
            Expect.equal (converted.uniforms.[scalar.ToString()].GetValueUntyped AdaptiveToken.Top) (box 41) $"{context}: scalar single attribute"
            Expect.equal (converted.uniforms.[vector.ToString()].GetValueUntyped AdaptiveToken.Top) (box (V3d(1.25, -2.5, 3.75))) $"{context}: vector single attribute"
            transact (fun () -> suppliedValue.Value <- -7)
            Expect.equal (converted.uniforms.[externalName].GetValueUntyped AdaptiveToken.Top) (box -7) $"{context}: supplied adaptive updates"
            for data, snapshot in snapshots do
                Expect.sequenceEqual (data |> Seq.cast<obj>) (snapshot |> Seq.cast<obj>) $"{context}: input attributes unchanged"

        let paddedPositions() =
            for kind in [0; 1; 2; 3] do
                for count in [0; 1; 3; 8; 33] do
                    for extra in [1; 3; 17] do
                        for insertPositionsFirst in [false; true] do
                            for paddingFirst in [true; false] do
                                let data = positions kind count
                                let padding = Array.init (count + extra) float32 :> Array
                                let attributes = attributes data padding insertPositionsFirst paddingFirst
                                let source = geometry attributes null
                                let context = $"kind={kind}, positions={count}, padding={count + extra}, inserted first={insertPositionsFirst}, enumerates padding first={paddingFirst}"
                                Expect.isTrue source.IsValid $"{context}: padded input is valid"
                                verify source count context

        let indexedAndEqual() =
            for count in [0; 1; 3; 8; 33] do
                for insertPositionsFirst in [false; true] do
                    for paddingFirst in [false; true] do
                        let data = positions 0 count
                        let attributes = attributes data (Array.zeroCreate<float32> count) insertPositionsFirst paddingFirst
                        verify (geometry attributes null) count $"equal lengths={count}, inserted first={insertPositionsFirst}, padding first={paddingFirst}"
            for indexKind in [0; 1; 2; 3] do
                for count, indexCount in [0, 0; 1, 0; 3, 1; 3, 7; 8, 3; 8, 12] do
                    let values = Array.init indexCount (fun i -> i % count)
                    let indices : Array =
                        match indexKind with
                        | 0 -> values |> Array.map int16 :> Array
                        | 1 -> values |> Array.map uint16 :> Array
                        | 2 -> values :> Array
                        | _ -> values |> Array.map uint32 :> Array
                    for insertPositionsFirst in [false; true] do
                        let attributes = attributes (positions 0 count) (Array.zeroCreate<float32> (count + 7)) insertPositionsFirst true
                        let source = geometry attributes indices
                        Expect.isTrue source.IsValid "Indexed padded fixture is valid"
                        verify source indexCount $"index kind={indexKind}, positions={count}, indices={indexCount}, inserted first={insertPositionsFirst}"

        let private failure expected action context =
            let caught = try action(); None with error -> Some error
            match caught with
            | Some error -> Expect.equal (error.GetType()) expected context
            | None -> failtestf "%s: expected %O" context expected

        let fallbackAndEdges() =
            for count in [0; 1; 3; 8] do
                for reverse in [false; true] do
                    let attributes = SymbolDict<Array>()
                    let pairs = [paddingKeys.[0], Array.zeroCreate<int> count :> Array; paddingKeys.[1], Array.zeroCreate<byte> (count + 5) :> Array]
                    for key, data in (if reverse then List.rev pairs else pairs) do attributes.[key] <- data
                    let expected = (attributes.Values |> Seq.head).Length
                    verify (geometry attributes null) expected $"positionless count={count}, reverse insertion={reverse}, first length={expected}"
            failure typeof<ArgumentException> (fun () -> Geometry.ofIndexedGeometry Map.empty (geometry (SymbolDict<Array>()) null) |> ignore) "Non-indexed empty dictionary retains its error"
            failure typeof<NullReferenceException> (fun () -> Geometry.ofIndexedGeometry Map.empty (geometry null null) |> ignore) "Non-indexed null dictionary retains its error"
            failure typeof<ArgumentNullException> (fun () -> Geometry.ofIndexedGeometry Map.empty (geometry null ([| 0 |] :> Array)) |> ignore) "Indexed null dictionary retains its error"
            for count in [0; 3] do
                let source = geometry (SymbolDict<Array>()) (Array.zeroCreate<int> count)
                verify source count $"indexed empty attribute dictionary, index count={count}"

    let tests (target : TestTarget) =
        [
            "Positions determine non-indexed padded counts", Cases.paddedPositions
            "Indexed and equal-length counts remain intact", Cases.indexedAndEqual
            "Positionless fallback and dictionary errors",   Cases.fallbackAndEdges
        ]
        |> prepareCasesCpu "GeometryConversion" target
