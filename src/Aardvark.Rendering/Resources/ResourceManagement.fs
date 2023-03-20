namespace Aardvark.Rendering

open System
open System.Threading
open Aardvark.Base
open FSharp.Data.Adaptive

type ResourceInfo =
    struct
        val mutable public AllocatedSize : Mem
        val mutable public UsedSize : Mem

        static member Zero = ResourceInfo(Mem.Zero, Mem.Zero)

        static member (+) (l : ResourceInfo, r : ResourceInfo) =
            ResourceInfo(
                l.AllocatedSize + r.AllocatedSize,
                l.UsedSize + r.UsedSize
            )

        static member (-) (l : ResourceInfo, r : ResourceInfo) =
            ResourceInfo(
                l.AllocatedSize - r.AllocatedSize,
                l.UsedSize - r.UsedSize
            )

        static member (*) (l : ResourceInfo, r : int) =
            ResourceInfo(
                l.AllocatedSize * r,
                l.UsedSize * r
            )

        static member (*) (l : ResourceInfo, r : float) =
            ResourceInfo(
                l.AllocatedSize * r,
                l.UsedSize * r
            )

        static member (*) (l : int, r : ResourceInfo) =
            ResourceInfo(
                l * r.AllocatedSize,
                l * r.UsedSize
            )

        static member (*) (l : float, r : ResourceInfo) =
            ResourceInfo(
                l * r.AllocatedSize,
                l * r.UsedSize
            )

        static member (/) (l : ResourceInfo, r : int) =
            ResourceInfo(
                l.AllocatedSize / r,
                l.UsedSize / r
            )

        static member (/) (l : ResourceInfo, r : float) =
            ResourceInfo(
                l.AllocatedSize / r,
                l.UsedSize / r
            )


        new(a,u) = { AllocatedSize = a; UsedSize = u }
        new(s) = { AllocatedSize = s; UsedSize = s }
    end

/// Unique ID for resources.
[<Struct; StructuredFormatDisplay("{AsString}")>]
type ResourceId private (value : int) =
    static let mutable currentId = 0

    static member New() = ResourceId(Interlocked.Increment(&currentId))
    static member op_Explicit(id : ResourceId) = id.Value

    member private x.Value = value
    member private x.AsString = x.ToString()
    override x.ToString() = string value

type IResource =
    inherit IAdaptiveObject
    inherit IDisposable
    abstract member Id : ResourceId
    abstract member AddRef : unit -> unit
    abstract member RemoveRef : unit -> unit
    abstract member Update : token : AdaptiveToken * rt : RenderToken -> unit
    abstract member Kind : ResourceKind
    abstract member IsDisposed : bool
    abstract member Info : ResourceInfo
    abstract member HandleType : Type

type IResource<'h> =
    inherit IResource
    abstract member Handle : aval<'h>

type IResource<'h, 'v when 'v : unmanaged> =
    inherit IResource<'h>
    abstract member Pointer : nativeptr<'v>