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
    abstract member TryGetBufferView : Symbol -> Option<BufferView>

[<AbstractClass; Sealed; Extension>]
type IGeometryPoolExtensions private() =
    [<Extension>]
    static member Alloc(this : IGeometryPool, g : IndexedGeometry) =
        let fvc =
            if isNull g.IndexArray then
                match g.IndexedAttributes.Values |> Seq.tryHead with
                    | None -> 0
                    | Some a -> a.Length
            else
                g.IndexArray.Length

        this.Alloc(fvc, g)