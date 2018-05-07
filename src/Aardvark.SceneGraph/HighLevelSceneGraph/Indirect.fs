namespace Aardvark.SceneGraph

open System
open Aardvark.Base
open Aardvark.Base.Incremental


[<AutoOpen>]
module Indirect =

    type IndirectSignature =
        {
            mode : IndexedGeometryMode
            vertexTypes : Map<Symbol, Type>
            uniformTypes : Map<string, Type>
        }

    module Sg =
        type IndirectNode(signature : IndirectSignature, objects : aset<IndexedGeometry * Map<string, IMod>>) =
            interface ISg
            member x.Signature = signature
            member x.Objects = objects

        let indirect (signature : IndirectSignature) (objects : aset<IndexedGeometry * Map<string, IMod>>) =
            IndirectNode(signature, objects) :> ISg


namespace Aardvark.SceneGraph.Semantics

open System
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Ag
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open System.Collections.Generic
open System.Threading

module Indirect =
        
    type RefDict<'k, 'v>() =
        let store = Dict<'k, ref<int> * 'v>()

        member x.Create(key : 'k, f : 'k -> 'v) =
            let r, value = store.GetOrCreate(key, fun key -> (ref 0, f key))
            r := !r + 1
            value

        member x.TryRemove(key : 'k) =
            match store.TryGetValue key with
                | (true, (ref, value)) ->
                    if !ref = 1 then
                        store.Remove key |> ignore
                        true, value
                    else
                        ref := !ref - 1
                        false, value
                 
                | _ -> 
                    failwith "not there"
    
        member x.Clear() =
            store.Clear()

    type GeometryCache(runtime : IRuntime, vertexTypes : Map<Symbol, Type>) =
        let geometryCache = RefDict<IndexedGeometry, Management.Block<unit>>()
        let mutable pool = runtime.CreateGeometryPool(vertexTypes)

        member x.TryGetBufferView(sem) = pool.TryGetBufferView(sem)

        member x.Add(g : IndexedGeometry) =
            geometryCache.Create(g, fun _ -> pool.Alloc g)

        member x.Remove(g : IndexedGeometry) =
            let wasLast, ptr = geometryCache.TryRemove g
            if wasLast then pool.Free ptr
            ptr
        
        member x.Clear() =
            pool.Dispose()
            pool <- runtime.CreateGeometryPool(vertexTypes)
            geometryCache.Clear()

    type Writer(m : IMod, offset : nativeint, ptr : ref<nativeint>) = 
        inherit AdaptiveObject()

        member x.Write(token : AdaptiveToken) =
            x.EvaluateIfNeeded token () (fun token ->
                let value = m.GetValue token
                let dst = !ptr + offset
                Marshal.StructureToPtr(value, dst, false)
            )

    type UniformCache(runtime : IRuntime, uniformTypes : Map<string, Type>) =
        inherit AdaptiveObject()

        let dirty = HashSet<Writer>()
        let mutable manager = Aardvark.Base.Management.MemoryManager.createNop()
        let cache = RefDict<Map<string, IMod>, Management.Block<unit> * list<Writer>>()

        let mutable capacity = 0n
        let stores =
            uniformTypes |> Map.map (fun name t ->
                let s = nativeint (Marshal.SizeOf t)
                s, ref 0n
            )

        let fixCapacity() =
            if manager.Capactiy <> capacity then
                for (_,(es,ptr)) in Map.toSeq stores do
                    if manager.Capactiy = 0n then
                        if !ptr <> 0n then Marshal.FreeHGlobal(!ptr)
                        ptr := 0n

                    elif !ptr = 0n then
                        ptr := Marshal.AllocHGlobal(es * manager.Capactiy)

                    else
                        ptr := Marshal.ReAllocHGlobal(!ptr, es * manager.Capactiy)

                Log.warn "[Indirect] resize from: %A to %A" capacity manager.Capactiy
                capacity <- manager.Capactiy

        override x.InputChanged(t, o) =
            match o with    
                | :? Writer as w -> lock dirty (fun () -> dirty.Add w |> ignore)
                | _ -> ()

        member x.Add(values : Map<string, IMod>) =
            let block, writers =
                cache.Create(values, fun values ->
                    let slot = manager.Alloc(1n)
                    fixCapacity()

                    let writers =
                        lock dirty (fun () ->
                            Map.toList stores |> List.map (fun (name, (elementSize, ptr)) ->
                                let value = values.[name]
                                let writer = new Writer(value, slot.Offset * elementSize, ptr)
                                dirty.Add(writer) |> ignore
                                writer
                            )
                        )

                    transact x.MarkOutdated

                    slot, writers
                )
            block

        member x.Remove(values : Map<string, IMod>) =
            let wasLast, (ptr, writers) = cache.TryRemove values
            if wasLast then
                manager.Free ptr
                
                fixCapacity()

                lock dirty (fun () ->
                    for w in writers do
                        w.Outputs.Remove x |> ignore
                        dirty.Remove w |> ignore
                )

                transact x.MarkOutdated

            ptr
    
        member x.GetBuffers(token : AdaptiveToken) =
            x.EvaluateAlways token (fun token ->
                let dirty = 
                    lock dirty (fun () ->
                        let arr = HashSet.toArray dirty
                        dirty.Clear()
                        arr
                    )
                for d in dirty do d.Write(token)

                stores |> Map.map (fun name (es, ptr) ->
                    NativeMemoryBuffer(!ptr, int (es * capacity)) :> IBuffer
                )

            )
            
        member x.Clear() =
            stores |> Map.iter (fun _ (_,ptr) ->
                if !ptr <> 0n then Marshal.FreeHGlobal !ptr
                ptr := 0n
            )
            capacity <- 0n
            cache.Clear()
            manager.Dispose()
            manager <- Aardvark.Base.Management.MemoryManager.createNop()
            let mutable foo = 0
            for d in dirty do d.Outputs.Consume(&foo) |> ignore
            dirty.Clear()
            transact x.MarkOutdated

    [<Semantic>]
    type IndirectSem() =
        member x.RenderObjects(node : Sg.IndirectNode) : aset<IRenderObject> =
            let runtime : IRuntime = x?Runtime
            let mutable reader = node.Objects.GetReader()
            let geometryCache = GeometryCache(runtime, node.Signature.vertexTypes)
            let uniformCache = UniformCache(runtime, node.Signature.uniformTypes)
            let callCache = Dict<IndexedGeometry * Map<string, IMod>, DrawCallInfo>()

            let mutable refCount = 0
            let kill () =
                if Interlocked.Increment(&refCount) = 1 then
                    Log.warn "boot stuff"

                { new IDisposable with 
                    member x.Dispose () = 
                        if Interlocked.Decrement(&refCount) = 0 then
                            Log.warn "destroy stuff"
                            reader.Dispose()
                            reader <- node.Objects.GetReader()
                            geometryCache.Clear()
                            uniformCache.Clear()
                            callCache.Clear()
                    }

            let obj = RenderObject.create()
            let indirectAndInstanceBuffers =
                Mod.custom (fun token ->
                    
                    let ops = reader.GetOperations(token)
                    for op in ops do
                        match op with   
                            | Add(_,(g,u)) ->
                                let gptr = geometryCache.Add(g)
                                let uptr = uniformCache.Add(u)

                                let call =
                                    DrawCallInfo(
                                        FaceVertexCount = int gptr.Size,
                                        FirstIndex = int gptr.Offset,
                                        FirstInstance = int uptr.Offset,
                                        InstanceCount = int uptr.Size
                                    )

                                callCache.[(g,u)] <- call

                            | Rem(_,(g,u)) ->
                                let _ = geometryCache.Remove(g)
                                let _ = uniformCache.Remove(u)
                                
                                let call = 
                                    match callCache.TryRemove ((g,u)) with
                                        | (true, call) -> call
                                        | _ -> failwith "no call"

                                ()

                    let instanceBuffers = uniformCache.GetBuffers(token)
                    let indirect = callCache.Values |> Seq.toArray |> IndirectBuffer.ofArray
                    
                    indirect, instanceBuffers
                )

            let instanceProvider =
                node.Signature.uniformTypes |> Map.map (fun name t ->
                    BufferView(Mod.map (snd >> Map.find name) indirectAndInstanceBuffers, t)
                )
                |> AttributeProvider.ofMap

            let vertexProvider =
                { new IAttributeProvider with
                    member x.All = Seq.empty
                    member x.Dispose() = ()
                    member x.TryGetAttribute(sem) = geometryCache.TryGetBufferView sem
                }

            obj.IndirectBuffer <- Mod.map fst indirectAndInstanceBuffers
            obj.InstanceAttributes <- instanceProvider
            obj.VertexAttributes <- vertexProvider
            obj.Mode <- node.Signature.mode
            obj.Activate <- kill
            ASet.single(obj :> IRenderObject)

