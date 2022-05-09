namespace Aardvark.Rendering

open Aardvark.Base

type NativeMemoryBuffer(ptr : nativeint, sizeInBytes : nativeint) =
    interface INativeBuffer with
        member x.SizeInBytes = sizeInBytes
        member x.Use f = f ptr
        member x.Pin() = ptr
        member x.Unpin() = ()

    member x.Ptr = ptr
    member x.SizeInBytes = sizeInBytes

    override x.GetHashCode() = HashCode.Combine(ptr.GetHashCode(), int64 sizeInBytes)
    override x.Equals o =
        match o with
        | :? NativeMemoryBuffer as n ->
            n.Ptr = ptr && n.SizeInBytes = sizeInBytes
        | _ -> false
