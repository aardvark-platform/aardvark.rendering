namespace Aardvark.Rendering

open System
open System.Runtime.InteropServices

type ArrayBuffer(data : Array) =
    let elementType = data.GetType().GetElementType()
    let mutable gchandle = Unchecked.defaultof<_>

    member x.Data = data
    member x.ElementType = elementType

    interface IBuffer

    interface INativeBuffer with
        member x.SizeInBytes = nativeint data.Length * nativeint (Marshal.SizeOf elementType)
        member x.Use (f : nativeint -> 'a) =
            let gc = GCHandle.Alloc(data, GCHandleType.Pinned)
            try f (gc.AddrOfPinnedObject())
            finally gc.Free()

        member x.Pin() =
            let gc = GCHandle.Alloc(data, GCHandleType.Pinned)
            gchandle <- gc
            gc.AddrOfPinnedObject()

        member x.Unpin() =
            gchandle.Free()
            gchandle <- Unchecked.defaultof<_>

    override x.GetHashCode() = data.GetHashCode()
    override x.Equals o =
        match o with
        | :? ArrayBuffer as o -> Object.ReferenceEquals(o.Data, data)
        | _ -> false