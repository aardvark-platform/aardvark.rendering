namespace Aardvark.Rendering

open System
open FSharp.Data.Adaptive
open System.Runtime.InteropServices
open Aardvark.Base

type BufferView(buffer : aval<IBuffer>, elementType : Type,
                [<Optional; DefaultParameterValue(0)>] offset : int,
                [<Optional; DefaultParameterValue(0)>] stride : int,
                [<Optional; DefaultParameterValue(true)>] normalized : bool) =
    let singleValue =
        match buffer with
        | :? ISingleValueBuffer as nb -> Some nb.Value
        | _ -> None

    member x.Buffer = buffer
    member x.ElementType = elementType
    member x.Stride = stride
    member x.Offset = offset
    member x.SingleValue = singleValue
    member x.IsSingleValue = Option.isSome singleValue

    /// Indicates whether integer values represent normalized fixed-point values.
    /// Only has an effect if the element type is integral and the buffer view is used for an floating-point based attribute.
    member x.Normalized = normalized

    new(buffer : ISingleValueBuffer,
        [<Optional; DefaultParameterValue(0)>] offset : int,
        [<Optional; DefaultParameterValue(0)>] stride : int,
        [<Optional; DefaultParameterValue(true)>] normalized : bool) =
        BufferView(buffer, buffer.Value.ContentType, offset, stride, normalized)

    new(buffer : aval<IBackendBuffer>, elementType : Type,
        [<Optional; DefaultParameterValue(0)>] offset : int,
        [<Optional; DefaultParameterValue(0)>] stride : int,
        [<Optional; DefaultParameterValue(true)>] normalized : bool) =
        BufferView(AdaptiveResource.cast<IBuffer> buffer, elementType, offset, stride, normalized)

    new(buffer : IBuffer, elementType : Type,
        [<Optional; DefaultParameterValue(0)>] offset : int,
        [<Optional; DefaultParameterValue(0)>] stride : int,
        [<Optional; DefaultParameterValue(true)>] normalized : bool) =
        BufferView(AVal.constant buffer, elementType, offset, stride, normalized)

    new(arr : System.Array,
        [<Optional; DefaultParameterValue(0)>] offset : int,
        [<Optional; DefaultParameterValue(0)>] stride : int,
        [<Optional; DefaultParameterValue(true)>] normalized : bool) =
        let t = arr.GetType().GetElementType()
        BufferView(ArrayBuffer arr, t, offset, stride, normalized)

    override x.GetHashCode() =
        HashCode.Combine(buffer.GetHashCode(), elementType.GetHashCode(), offset.GetHashCode(), stride.GetHashCode(), normalized.GetHashCode())

    override x.Equals o =
        Object.ReferenceEquals(x, o) ||
            match o with
            | :? BufferView as o ->
                 o.Buffer = buffer && o.ElementType = elementType && o.Offset = offset && o.Stride = stride && o.Normalized = normalized
            | _ -> false

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module BufferView =

    let ofArray (arr : Array) =
        BufferView(arr)

    let download (startIndex : int) (count : int) (view : BufferView) : aval<Array> =
        match view.SingleValue with
        | Some value ->
            value.Accept (
                { new IAdaptiveValueVisitor<_> with
                    member x.Visit(value) = value |> AVal.map (fun v -> Array.replicate count v :> Array)
                }
            )

        | _ ->
            let offset = uint64 view.Offset + uint64 view.ElementType.CLRSize * uint64 startIndex
            view.Buffer |> AdaptiveResource.map (_.ToArray(view.ElementType, uint64 count, offset, uint64 view.Stride))