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

    and NativeBuffer(capacity : int, input : IMod) =
        inherit Mod.AbstractMod<IBuffer>()

        let mutable capacity = capacity
        let mutable mem = Marshal.AllocHGlobal capacity
        let readers = HashSet<RegionReader>()

        member x.Capacity = capacity
        member x.Ptr = mem
        member x.Resize newSize =
            if newSize > capacity then
                for r in readers do 
                    r.AddDirtyRegion (Range1i (capacity, newSize - 1))

            if newSize <> capacity then
                mem <- Marshal.ReAllocHGlobal(mem,nativeint newSize)
                capacity <- newSize
               

        // TODO: locking scheme
        member x.GetReader() =
            let r = RegionReader x
            readers.Add r |> ignore
            r

        member x.AddDirtyRegion(range : Range1i) =
            for r in readers do
                r.AddDirtyRegion range

        override x.Compute() =
            input.GetValue x |> ignore
            NativeMemoryBuffer(mem, capacity) :> IBuffer


    type PackingAttributeProvider(scope : Ag.Scope, attributeTypes : Map<Symbol, Type>, geometries : aset<IndexedGeometry>) =
        let mutable scope = scope
        let memoryManager = Aardvark.Base.MemoryManager.createNop()
        let reader = geometries.GetReader()
        let attributes = Dictionary<Symbol, int * Type * NativeBuffer>()
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
            for (sem,(es,et, ptr)) in attributes |> Dictionary.toSeq do
                ptr.Resize (es * memoryManager.Capacity)

            for (range,g) in dirtyRanges do
                for (sem,(elementSize, _, ptr)) in attributes |> Dictionary.toSeq do
                    match g.IndexedAttributes.TryGetValue sem with
                     | (true,v) ->
                        let gc = GCHandle.Alloc(v, GCHandleType.Pinned)
                        try
                            Marshal.Copy(gc.AddrOfPinnedObject(), ptr.Ptr + nativeint elementSize * range.Offset, elementSize * range.Size)
                        finally
                            gc.Free()
                     | _ ->
                        ()
                

        let drawCallInfos =
            Mod.custom (fun self ->
                update self

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
                        let target = NativeBuffer(0, drawCallInfos)
                        attributes.[s] <- (Marshal.SizeOf(t), t, target)
                        let view = BufferView(target, attributeTypes.[s])
                        cache.[s] <- view

                        Some view
             | None -> None

        interface IAttributeProvider with

            member x.Dispose() =
                ()

            member x.All =
                Seq.empty

            member x.TryGetAttribute(s : Symbol) = x.TryGetAttribute s
