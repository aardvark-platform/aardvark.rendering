namespace Aardvark.Rendering

open Aardvark.Base
open System

type NativeMemoryBuffer(ptr: nativeint, sizeInBytes: uint64) =
    member _.Ptr = ptr
    member _.SizeInBytes = sizeInBytes

    member inline this.Use([<InlineIfLambda>] action: nativeint -> 'T) =
        action this.Ptr

    member inline this.Equals(other: NativeMemoryBuffer) =
        this.Ptr = other.Ptr && this.SizeInBytes = other.SizeInBytes

    override _.GetHashCode() =
        HashCode.Combine(ptr.GetHashCode(), int64 sizeInBytes)

    override this.Equals obj =
        match obj with
        | :? NativeMemoryBuffer as other -> this.Equals other
        | _ -> false

    interface IEquatable<NativeMemoryBuffer> with
        member this.Equals(other) = this.Equals other

    interface INativeBuffer with
        member _.SizeInBytes = sizeInBytes
        member _.Use action = action ptr