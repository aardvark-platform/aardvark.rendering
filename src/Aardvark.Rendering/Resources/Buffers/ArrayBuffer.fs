namespace Aardvark.Rendering

open Aardvark.Base
open System

type ArrayBuffer(data: Array) =
    let elementType = data.GetType().GetElementType()

    member _.Data = data
    member _.ElementType = elementType
    member _.SizeInBytes = uint64 data.Length * uint64 elementType.CLRSize

    member inline this.Use([<InlineIfLambda>] action: nativeint -> 'T) =
        this.Data |> NativeInt.pin action

    member inline this.Equals(other: ArrayBuffer) =
        obj.ReferenceEquals(this.Data, other.Data)

    override _.GetHashCode() = data.GetHashCode()

    override this.Equals(obj) =
        match obj with
        | :? ArrayBuffer as other -> this.Equals other
        | _ -> false

    interface IEquatable<ArrayBuffer> with
        member this.Equals(other) = this.Equals other

    interface INativeBuffer with
        member this.SizeInBytes = this.SizeInBytes
        member this.Use(action) = this.Use action