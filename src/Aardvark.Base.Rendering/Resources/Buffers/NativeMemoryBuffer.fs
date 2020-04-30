namespace Aardvark.Base

open System
open System.Runtime.InteropServices

type NativeMemoryBuffer(ptr : nativeint, sizeInBytes : int) =
    interface INativeBuffer with
        member x.SizeInBytes = sizeInBytes
        member x.Use f = f ptr
        member x.Pin() = ptr
        member x.Unpin() = ()

    member x.Ptr = ptr
    member x.SizeInBytes = sizeInBytes

    override x.GetHashCode() = HashCode.Combine(ptr.GetHashCode(),sizeInBytes)
    override x.Equals o =
        match o with
        | :? NativeMemoryBuffer as n ->
            n.Ptr = ptr && n.SizeInBytes = sizeInBytes
        | _ -> false
