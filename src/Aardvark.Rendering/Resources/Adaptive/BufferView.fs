namespace Aardvark.Rendering

open System
open FSharp.Data.Adaptive
open System.Runtime.InteropServices
open Aardvark.Base

/// Describes how elements of a buffer are accessed.
type BufferView =

    /// The underlying buffer.
    val Buffer : aval<IBuffer>

    /// The element type of the view. May differ from the element type of the underlying buffer (if it has one).
    val ElementType : Type

    /// The number of bytes between consecutive elements of the view in the underlying buffer.
    /// A stride of zero indicates a tightly packed layout.
    val Stride : int

    /// The start offset in bytes of the first element of the view in the underyling buffer.
    /// Ignored for single value buffers.
    val Offset : int

    /// Indicates whether integer values represent normalized fixed-point values.
    /// Only has an effect if the element type is integral and the buffer view is used for a floating-point based attribute.
    val Normalized : bool

    /// The value of the underlying buffer, if the underlying buffer is a single value buffer. None otherwise.
    val SingleValue : IAdaptiveValue option

    /// Indicates if the underlying buffer is a single value buffer.
    member this.IsSingleValue = Option.isSome this.SingleValue

    /// <summary>
    /// Creates a view for a buffer.
    /// </summary>
    /// <param name="buffer">The buffer for which to create the view.</param>
    /// <param name="elementType">The element type of the view.</param>
    /// <param name="offset">The start offset in bytes of the first element of the view in the buffer. Default is zero.</param>
    /// <param name="stride">The number of bytes between consecutive elements of the view in the buffer. A stride of zero indicates a tightly packed layout. Default is zero.</param>
    /// <param name="normalized">Indicates whether integer values represent normalized fixed-point values. Default is true.</param>
    new (buffer : aval<IBuffer>, elementType : Type,
        [<Optional; DefaultParameterValue(0)>] offset : int,
        [<Optional; DefaultParameterValue(0)>] stride : int,
        [<Optional; DefaultParameterValue(true)>] normalized : bool) =
        if isNull elementType then
            raise <| ArgumentNullException(nameof elementType, "Element type of buffer view cannot be null.")

        let singleValue =
            match buffer with
            | :? ISingleValueBuffer as nb -> Some nb.Value
            | _ -> None

        { Buffer      = buffer
          ElementType = elementType
          Stride      = stride
          Offset      = offset
          Normalized  = normalized
          SingleValue = singleValue }

    /// <summary>
    /// Creates a view for a single value buffer.
    /// </summary>
    /// <param name="buffer">The buffer for which to create the view.</param>
    /// <param name="elementType">The element type of the view or null for the content type of the single value buffer. Default is null.</param>
    /// <param name="normalized">Indicates whether integer values represent normalized fixed-point values. Default is true.</param>
    new(buffer : ISingleValueBuffer,
        [<Optional; DefaultParameterValue(null : Type)>] elementType : Type,
        [<Optional; DefaultParameterValue(true)>] normalized : bool) =
        let elementType = elementType ||? buffer.Value.ContentType
        BufferView(buffer, elementType, 0, 0, normalized)

    /// <summary>
    /// Creates a view for a backend buffer.
    /// </summary>
    /// <param name="buffer">The buffer for which to create the view.</param>
    /// <param name="elementType">The element type of the view.</param>
    /// <param name="offset">The start offset in bytes of the first element of the view in the buffer. Default is zero.</param>
    /// <param name="stride">The number of bytes between consecutive elements of the view in the buffer. A stride of zero indicates a tightly packed layout. Default is zero.</param>
    /// <param name="normalized">Indicates whether integer values represent normalized fixed-point values. Default is true.</param>
    new(buffer : aval<IBackendBuffer>, elementType : Type,
        [<Optional; DefaultParameterValue(0)>] offset : int,
        [<Optional; DefaultParameterValue(0)>] stride : int,
        [<Optional; DefaultParameterValue(true)>] normalized : bool) =
        BufferView(AdaptiveResource.cast<IBuffer> buffer, elementType, offset, stride, normalized)

    /// <summary>
    /// Creates a view for a buffer.
    /// </summary>
    /// <param name="buffer">The buffer for which to create the view.</param>
    /// <param name="elementType">The element type of the view.</param>
    /// <param name="offset">The start offset in bytes of the first element of the view in the buffer. Default is zero.</param>
    /// <param name="stride">The number of bytes between consecutive elements of the view in the buffer. A stride of zero indicates a tightly packed layout. Default is zero.</param>
    /// <param name="normalized">Indicates whether integer values represent normalized fixed-point values. Default is true.</param>
    new(buffer : IBuffer, elementType : Type,
        [<Optional; DefaultParameterValue(0)>] offset : int,
        [<Optional; DefaultParameterValue(0)>] stride : int,
        [<Optional; DefaultParameterValue(true)>] normalized : bool) =
        BufferView(AVal.constant buffer, elementType, offset, stride, normalized)

    /// <summary>
    /// Creates a view for an array.
    /// </summary>
    /// <param name="array">The array for which to create the view.</param>
    /// <param name="elementType">The element type of the view or null for the element type of the array. Default is null.</param>
    /// <param name="offset">The start offset in bytes of the first element of the view in the array. Default is zero.</param>
    /// <param name="stride">The number of bytes between consecutive elements of the view in the array. A stride of zero indicates a tightly packed layout. Default is zero.</param>
    /// <param name="normalized">Indicates whether integer values represent normalized fixed-point values. Default is true.</param>
    new(array : Array,
        [<Optional; DefaultParameterValue(null : Type)>] elementType : Type,
        [<Optional; DefaultParameterValue(0)>] offset : int,
        [<Optional; DefaultParameterValue(0)>] stride : int,
        [<Optional; DefaultParameterValue(true)>] normalized : bool) =
        let elementType = elementType ||? array.GetType().GetElementType()
        BufferView(ArrayBuffer array, elementType, offset, stride, normalized)

    override this.GetHashCode() =
        HashCode.Combine(
            this.Buffer.GetHashCode(),
            this.ElementType.GetHashCode(),
            this.Offset.GetHashCode(),
            this.Stride.GetHashCode(),
            this.Normalized.GetHashCode()
        )

    override this.Equals obj =
        Object.ReferenceEquals(this, obj) ||
            match obj with
            | :? BufferView as other ->
                 other.Buffer = this.Buffer && other.ElementType = this.ElementType &&
                 other.Offset = this.Offset && other.Stride = this.Stride && other.Normalized = this.Normalized
            | _ -> false

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module BufferView =

    let ofArray (array : Array) =
        BufferView(array)

    let download (startIndex : int) (count : int) (view : BufferView) : aval<Array> =
        match view.SingleValue with
        | Some value ->
            value.Accept {
                new IAdaptiveValueVisitor<_> with
                    member _.Visit(value) = value |> AVal.map (fun value -> Array.replicate count value :> Array)
            }

        | _ ->
            let offset = uint64 view.Offset + uint64 view.ElementType.CLRSize * uint64 startIndex
            view.Buffer |> AdaptiveResource.map (_.ToArray(view.ElementType, uint64 count, offset, uint64 view.Stride))