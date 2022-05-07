namespace Aardvark.Rendering

open System
open System.Threading
open System.Runtime.InteropServices
open System.Collections.Concurrent
open Aardvark.Base
open FSharp.Data.Adaptive

module GeometrySetUtilities =

    type GeometryPacker(runtime : IBufferRuntime, attributeTypes : Map<Symbol, Type>) =
        inherit AVal.AbstractVal<RangeSet>()

        let manager = MemoryManager.createNop()
        let locations = ConcurrentDictionary<IndexedGeometry, managedptr>()
        let mutable buffers = ConcurrentDictionary<Symbol, IAdaptiveBuffer>()
        let elementSizes = attributeTypes |> Map.map (fun _ v -> nativeint(Marshal.SizeOf v)) |> Map.toSeq |> Dictionary.ofSeq
        let mutable ranges = RangeSet.empty


        let getElementSize (sem : Symbol) =
            elementSizes.[sem]

        let writeAttribute (sem : Symbol) (region : managedptr) (buffer : IAdaptiveBuffer) (source : IndexedGeometry) =
            match source.IndexedAttributes.TryGetValue(sem) with
                | (true, arr) ->
                    let elementSize = getElementSize sem
                    let cap = elementSize * manager.Capacity

                    buffer.Resize(cap)

                    buffer.Write(arr, region.Offset * elementSize, region.Size * elementSize)
                | _ ->
                    // TODO: write NullBuffer content or 0 here
                    ()

        let getBuffer (sem : Symbol) =
            let mutable isNew = false
            let result =
                buffers.GetOrAdd(sem, fun sem ->
                    isNew <- true
                    let elementSize = getElementSize sem
                    let b = AdaptiveBuffer(runtime, elementSize * manager.Capacity)
                    b.Acquire()
                    b :> IAdaptiveBuffer
                )

            if isNew then
                for (KeyValue(g,region)) in locations do
                    writeAttribute sem region result g

            result

        member private x.AddRange (ptr : managedptr) =
            let r = Range1i.FromMinAndSize(int ptr.Offset, int ptr.Size - 1)
            Interlocked.Change(&ranges, RangeSet.insert r) |> ignore
            transact (fun () -> x.MarkOutdated())

        member private x.RemoveRange (ptr : managedptr) =
            let r = Range1i.FromMinAndSize(int ptr.Offset, int ptr.Size - 1)
            Interlocked.Change(&ranges, RangeSet.remove r) |> ignore
            transact (fun () -> x.MarkOutdated())


        member x.Activate (g : IndexedGeometry) =
            match locations.TryGetValue g with
                | (true, ptr) -> x.AddRange ptr
                | _ -> ()

        member x.Deactivate (g : IndexedGeometry) =
            match locations.TryGetValue g with
                | (true, ptr) -> x.RemoveRange ptr
                | _ -> ()

        member x.Add (g : IndexedGeometry) =
            let mutable isNew = false

            let region =
                locations.GetOrAdd(g, fun g ->
                    let faceVertexCount =
                        if isNull g.IndexArray then
                            let att = g.IndexedAttributes.Values |> Seq.head
                            att.Length
                        else
                            g.IndexArray.Length

                    isNew <- true
                    manager.Alloc(nativeint faceVertexCount)
                )

            if isNew then
                for (KeyValue(sem, buffer)) in buffers do
                    writeAttribute sem region buffer g

                x.AddRange region
                true
            else
                false

        member x.Remove (g : IndexedGeometry) =
            match locations.TryRemove g with
                | (true, region) ->
                    x.RemoveRange region
                    manager.Free(region)
                    true
                | _ ->
                    false

        member x.GetBuffer (sem : Symbol) =
            getBuffer sem  :> aval<IBackendBuffer>

        member x.Dispose() =
            let old = Interlocked.Exchange(&buffers, ConcurrentDictionary())
            if old.Count > 0 then
                for q in old.Values do q.Release()
                old.Clear()

        override x.Compute(token) =
            //printfn "%A" ranges
            ranges