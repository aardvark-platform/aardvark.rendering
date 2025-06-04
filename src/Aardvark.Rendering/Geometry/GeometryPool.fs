namespace Aardvark.Rendering

open System
open Aardvark.Base
open System.Runtime.CompilerServices

type IGeometryPool =
    inherit IDisposable
    abstract member Count : int
    abstract member UsedMemory : Mem
    abstract member Alloc : int * IndexedGeometry -> Management.Block<unit>
    abstract member Free : Management.Block<unit> -> unit
    abstract member TryGetBufferView : Symbol -> BufferView voption

[<AbstractClass; Sealed; Extension>]
type IGeometryPoolExtensions private() =
    [<Extension>]
    static member Alloc(this : IGeometryPool, g : IndexedGeometry) =
        this.Alloc(g.FaceVertexCount, g)