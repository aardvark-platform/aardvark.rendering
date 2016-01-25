namespace Aardvark.Base

open System
open Aardvark.Base.Incremental
open System.Runtime.InteropServices
open Aardvark.Base.Rendering

[<AllowNullLiteral>]
type ISurface = interface end


[<AllowNullLiteral>]
type IAttributeProvider =
    inherit IDisposable
    abstract member TryGetAttribute : name : Symbol -> Option<BufferView>
    abstract member All : seq<Symbol * BufferView>

[<AllowNullLiteral>]
type IUniformProvider =
    inherit IDisposable
    abstract member TryGetUniform : scope : Ag.Scope * name : Symbol -> Option<IMod>


module private RenderObjectIds =
    open System.Threading
    let mutable private currentId = 0
    let newId() = Interlocked.Increment &currentId

type IRenderObject =
    abstract member RenderPass : uint64
    abstract member AttributeScope : Ag.Scope

[<CustomEquality>]
[<CustomComparison>]
type RenderObject =
    {
        Id : int
        mutable AttributeScope : Ag.Scope
                
        mutable IsActive   : IMod<bool>
        mutable RenderPass : uint64
                
        mutable DrawCallInfo : IMod<list<DrawCallInfo>>
        mutable Mode         : IMod<IndexedGeometryMode>
        mutable Surface      : IMod<ISurface>
                
        mutable DepthTest    : IMod<DepthTestMode>
        mutable CullMode     : IMod<CullMode>
        mutable BlendMode    : IMod<BlendMode>
        mutable FillMode     : IMod<FillMode>
        mutable StencilMode  : IMod<StencilMode>
                
        mutable Indices            : IMod<Array>
        mutable InstanceAttributes : IAttributeProvider
        mutable VertexAttributes   : IAttributeProvider
                
        mutable Uniforms : IUniformProvider
    }  
    interface IRenderObject with
        member x.RenderPass = x.RenderPass
        member x.AttributeScope = x.AttributeScope

    member x.Path = 
        if System.Object.ReferenceEquals(x.AttributeScope,Ag.emptyScope) 
        then "EMPTY" 
        else x.AttributeScope.Path

    static member Create() =
        { Id = RenderObjectIds.newId()
          AttributeScope = Ag.emptyScope
          IsActive = null
          RenderPass = 0UL
          DrawCallInfo = null
          Mode = null
          Surface = null
          DepthTest = null
          CullMode = null
          BlendMode = null
          FillMode = null
          StencilMode = null
          Indices = null
          InstanceAttributes = null
          VertexAttributes = null
          Uniforms = null
        }

    static member Clone(org : RenderObject) =
        { org with Id = RenderObjectIds.newId() }

    override x.GetHashCode() = x.Id
    override x.Equals o =
        match o with
            | :? RenderObject as o -> x.Id = o.Id
            | _ -> false

    interface IComparable with
        member x.CompareTo o =
            match o with
                | :? RenderObject as o -> compare x.Id o.Id
                | _ -> failwith "uncomparable"

[<AutoOpen>]
module RenderObjectExtensions =

    let private emptyUniforms =
        { new IUniformProvider with
            member x.TryGetUniform (_,_) = None
            member x.Dispose() = ()
        }

    let private emptyAttributes =
        { new IAttributeProvider with
            member x.TryGetAttribute(name : Symbol) = None 
            member x.All = Seq.empty
            member x.Dispose() = ()
        }

    let private empty =
        { Id = -1
          AttributeScope = Ag.emptyScope
          IsActive = null
          RenderPass = 0UL
          DrawCallInfo = null
          Mode = null
          Surface = null
          DepthTest = null
          CullMode = null
          BlendMode = null
          FillMode = null
          StencilMode = null
          Indices = null
          InstanceAttributes = emptyAttributes
          VertexAttributes = emptyAttributes
          Uniforms = emptyUniforms
        }


    type RenderObject with
        static member Empty =
            empty

        member x.IsValid =
            x.Id >= 0


module AttributePacking =

    open System
    open System.Threading
    open System.Collections.Generic
    open System.Runtime.InteropServices

    type RegionReader(parent : NativeBuffer) =
        inherit AdaptiveObject()
        let mutable dirty = RangeSet.ofList [ Range1i(0,parent.Capacity - 1) ]

        member x.Update caller =
            x.EvaluateAlways caller (fun () ->
                parent.GetValue x |> ignore
                Interlocked.Exchange(&dirty, RangeSet.empty)
            )

        member x.AddDirtyRegion (range : Range1i) =
            Interlocked.Change(&dirty, RangeSet.insert range) |> ignore

    and NativeBuffer(capacity : int, input : IMod<unit>, manager : MemoryManager, elementType : Type) =
        inherit Mod.AbstractMod<IBuffer>()

        let elementSize = Marshal.SizeOf elementType

        let mutable capacity = capacity
        let mutable mem = Marshal.AllocHGlobal capacity
        let readers = HashSet<RegionReader>()

        let resize() =
            let newSize = manager.Capacity * elementSize
            if newSize > capacity then
                for r in readers do 
                    r.AddDirtyRegion (Range1i (capacity, newSize - 1))

            if newSize <> capacity then
                mem <- Marshal.ReAllocHGlobal(mem,nativeint newSize)
                capacity <- newSize
               
        member x.Capacity = capacity
        member x.Ptr = mem

        member x.ElementType = elementType
        member x.ElementSize = elementSize

        member x.Write(offset : nativeint, a : Array) =
            resize()
            let gc = GCHandle.Alloc(a, GCHandleType.Pinned)
            try
                Marshal.Copy(gc.AddrOfPinnedObject(), mem + nativeint elementSize * offset, elementSize * a.Length)
            finally
                gc.Free()

           

        // TODO: locking scheme
        member x.GetReader() =
            let r = RegionReader x
            readers.Add r |> ignore
            r

        member x.AddDirtyRegion(range : Range1i) =
            for r in readers do
                r.AddDirtyRegion range

        override x.Compute() =
            input.GetValue x
            NativeMemoryBuffer(mem, capacity) :> IBuffer




    type IRangeReader =
        inherit IAdaptiveObject
        abstract member GetDirtyRanges : IAdaptiveObject -> IBuffer * RangeSet

    type IAdaptiveBuffer =
        inherit IMod<IBuffer>
        abstract member GetReader : unit -> IRangeReader

    type AdaptiveBufferReader(buffer : AdaptiveBuffer) =
        inherit AdaptiveObject()

        let mutable dirty = RangeSet.empty
        let mutable initial = true

        member x.GetDirtyRanges(caller : IAdaptiveObject) =
            x.EvaluateAlways caller (fun () ->
                let b = buffer.GetValue x
                let dirty = Interlocked.Exchange(&dirty, RangeSet.empty)
                if initial then
                    initial <- false
                    b, RangeSet.ofList [Range1i(0, buffer.Capacity-1)]
                else
                    b, dirty
            )
    
        member x.AddDirty (r : Range1i) =
            Interlocked.Change(&dirty, RangeSet.insert r) |> ignore

        member x.RemoveDirty (r : Range1i) =
            Interlocked.Change(&dirty, RangeSet.remove r) |> ignore

        interface IRangeReader with
            member x.GetDirtyRanges(caller) = x.GetDirtyRanges(caller)


    and AdaptiveBuffer(sem : Symbol, elementType : Type, input : IMod<Dictionary<IndexedGeometry, managedptr> * int>) =
        inherit Mod.AbstractMod<IBuffer>()
        let readers = HashSet<AdaptiveBufferReader>()
        let elementSize = Marshal.SizeOf elementType

        let mutable storage = 0n
        let mutable myCapacity = 0
        let mutable dirtyGeometries = Dictionary()
        let readers = HashSet<AdaptiveBufferReader>()

        member x.Capacity = myCapacity

        member x.GetReader() =
            let r = AdaptiveBufferReader(x)
            readers.Add r |> ignore
            r :> IRangeReader

        member x.ElementType = elementType

        member x.AddGeometry(geometry : IndexedGeometry, range : Range1i) =
            if storage <> 0n then
                let range = Range1i(elementSize * range.Min, elementSize * range.Max + elementSize - 1)
                dirtyGeometries.[geometry] <- range
                for r in readers do r.AddDirty range

        member x.RemoveGeometry(geometry : IndexedGeometry) =
            if storage <> 0n then
                match dirtyGeometries.TryGetValue geometry with
                    | (true, range) ->
                        for r in readers do r.RemoveDirty range
                        dirtyGeometries.Remove geometry |> ignore
                    | _ ->
                        ()

        override x.Compute() =
            let ptrs, capacity = input.GetValue(x)
            let capacity = elementSize * capacity

            if storage = 0n then
                storage <- Marshal.AllocHGlobal capacity
                myCapacity <- capacity

                for (g, ptr) in Dictionary.toSeq ptrs do
                    match g.IndexedAttributes.TryGetValue sem with
                        | (true, arr) ->
                            let offset = ptr.Offset * nativeint elementSize
                            let size = ptr.Size * elementSize
                            let arraySize = arr.Length * elementSize

                            let gc = GCHandle.Alloc(arr, GCHandleType.Pinned)
                            try
                                Marshal.Copy(gc.AddrOfPinnedObject(), storage + offset, min size arraySize)
                            finally
                                gc.Free()
                        | _ ->
                            // TODO: what to do here? maybe respect NullBuffers here
                            ()

            else
                let dirty = dirtyGeometries |> Dictionary.toArray
                dirtyGeometries.Clear()

                if capacity <> myCapacity then
                    storage <- Marshal.ReAllocHGlobal(storage, nativeint capacity)
                    myCapacity <- capacity

                for (d, ptr) in dirty do
                    match d.IndexedAttributes.TryGetValue sem with
                        | (true, arr) ->
                            let offset = nativeint ptr.Min
                            let size = ptr.Size + 1
                            let arraySize = arr.Length * elementSize

                            let gc = GCHandle.Alloc(arr, GCHandleType.Pinned)
                            try
                                Marshal.Copy(gc.AddrOfPinnedObject(), storage + offset, min size arraySize)
                            finally
                                gc.Free()
                        | _ ->
                            ()

            NativeMemoryBuffer(storage, capacity) :> IBuffer


    type AdaptiveBufferLayout(set : aset<IndexedGeometry>, elementTypes : Map<Symbol, Type>) =
        inherit Mod.AbstractMod<Dictionary<IndexedGeometry, managedptr> * int>()

        let reader = set.GetReader()
        let manager = MemoryManager.createNop()
        let ptrs = Dictionary<IndexedGeometry, managedptr>()
        let mutable ranges = RangeSet.empty

        let buffers = Dictionary<Symbol, AdaptiveBuffer>()

        let vertexCount (i : IndexedGeometry) =
            match i.IndexArray with
             | null -> i.IndexedAttributes.Values |> Seq.head |> (fun s -> s.Length)
             | arr -> arr.Length

        

        member x.TryGetBuffer(sem : Symbol) =
            match buffers.TryGetValue(sem) with
                | (true, b) -> Some b
                | _ ->
                    match Map.tryFind sem elementTypes with
                        | Some et ->
                            let b = AdaptiveBuffer(sem, et, x)
                            buffers.[sem] <- b
                            Some b
                        | None ->
                            None


        member x.DrawCallInfos =
            let dummy = x |> Mod.map id |> Mod.map id |> Mod.map id |> Mod.map id |> Mod.map id |> Mod.map id |> Mod.map id

            Mod.custom (fun self ->
                let ptrs,_ = dummy.GetValue self
                let b = buffers.Values |> Seq.head
                let bb = b.GetValue self |> unbox<NativeMemoryBuffer>
                printfn "size = %A" bb.SizeInBytes
                ranges
                    |> Seq.toList
                    |> List.map (fun range ->
                        printfn "range = %A" range
                        DrawCallInfo(
                            FirstIndex = range.Min,
                            FaceVertexCount = range.Size + 1,
                            FirstInstance = 0,
                            InstanceCount = 1,
                            BaseVertex = 0
                        )
                    )
            )

        override x.Compute () =
            let deltas = reader.GetDelta(x)

            for d in deltas do
                match d with
                    | Add g ->
                        let ptr = g |> vertexCount |> manager.Alloc
                        ptrs.[g] <- ptr
                        ranges <- RangeSet.insert (Range1i.FromMinAndSize(int ptr.Offset, ptr.Size-1)) ranges
                        for b in buffers.Values do b.AddGeometry(g, Range1i.FromMinAndSize(int ptr.Offset, ptr.Size-1))

                    | Rem g ->
                        match ptrs.TryGetValue g with
                            | (true, ptr) ->
                                ranges <- RangeSet.remove (Range1i.FromMinAndSize(int ptr.Offset, ptr.Size-1)) ranges
                                for b in buffers.Values do b.RemoveGeometry(g)
                                manager.Free ptr
                                ptrs.Remove g |> ignore
                            | _ ->
                                ()

            ptrs, manager.Capacity
                            

        interface IAttributeProvider with

            member x.Dispose() =
                ()

            member x.All =
                Seq.empty

            member x.TryGetAttribute(s : Symbol) =
                match x.TryGetBuffer s with
                    | Some b -> BufferView(b, b.ElementType) |> Some
                    | None -> None



    type PackingAttributeProvider(scope : Ag.Scope, attributeTypes : Map<Symbol, Type>, geometries : aset<IndexedGeometry>) =
        let mutable scope = scope
        let memoryManager = Aardvark.Base.MemoryManager.createNop()
        let reader = geometries.GetReader()
        let attributes = Dictionary<Symbol, NativeBuffer>()
        let ranges = Dictionary<IndexedGeometry,managedptr>()
        let cache = Dictionary<Symbol, BufferView>()

        let vertexCount (i : IndexedGeometry) =
            match i.IndexArray with
             | null -> i.IndexedAttributes.Values |> Seq.head |> (fun s -> s.Length)
             | arr -> arr.Length

        let processDeltas (caller : IAdaptiveObject) =
            reader.GetDelta(caller) |> List.choose (fun d ->
                match d with
                 | Add v ->
                    let length = vertexCount v
                    let range = memoryManager.Alloc length
                    ranges.[v] <- range
                    Some (range, v)
                 | Rem v -> 
                    match ranges.TryGetValue v with 
                     | (true,ptr) -> 
                        memoryManager.Free ptr
                        ranges.Remove v |> ignore
                     | _ -> ()
                    None
            )

        let update (caller : IAdaptiveObject) =
            let dirtyRanges = processDeltas(caller)
             
            for (range,g) in dirtyRanges do
                for (sem,ptr) in attributes |> Dictionary.toSeq do
                    match g.IndexedAttributes.TryGetValue sem with
                     | (true,v) ->
                        ptr.Write(range.Offset, v)
                     | _ ->
                        ()
             
        let updater = Mod.custom update   

        let drawCallInfos =
            Mod.custom (fun self ->
                updater.GetValue self

                ranges.Values
                    |> Seq.toList
                    |> List.map (fun range ->
                        DrawCallInfo(
                            FirstIndex = int range.Offset,
                            FaceVertexCount = range.Size,
                            InstanceCount = 1,
                            BaseVertex = 0
                        )
                    )

            )


        member x.DrawCalls =
            drawCallInfos

        member x.TryGetAttribute s =
            match attributeTypes |> Map.tryFind s with
             | Some t -> 
                match cache.TryGetValue s with
                    | (true, view) -> Some view
                    | _ ->
                        let target = NativeBuffer(0, updater, memoryManager, t)
                        attributes.[s] <- target

                        let view = BufferView(target, t)
                        cache.[s] <- view

                        for (g) in reader.Content do
                            match ranges.TryGetValue g with
                                | (true, range) ->
                                    match g.IndexedAttributes.TryGetValue s with
                                        | (true,v) ->
                                            target.Write(range.Offset, v)
                                        | _ ->
                                            ()
                                | _ -> ()


                        Some view
             | None -> None

        interface IAttributeProvider with

            member x.Dispose() =
                ()

            member x.All =
                Seq.empty

            member x.TryGetAttribute(s : Symbol) = x.TryGetAttribute s
