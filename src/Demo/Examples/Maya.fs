#if INTERACTIVE
#I @"../../../bin/Debug"
#I @"../../../bin/Release"
#load "LoadReferences.fsx"
#else
namespace Examples
#endif

open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.Interactive
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Rendering.NanoVg
open Aardvark.Base.Monads.State

#nowarn "9"
#nowarn "51"

module Pooling =
    open System.Threading
    open System.Collections.Generic
    open System.Runtime.InteropServices
    open System.Runtime.CompilerServices
    open System.Reflection
    open Microsoft.FSharp.NativeInterop

    [<ReferenceEquality; NoComparison>]
    type AdaptiveGeometry =
        {
            mode             : IndexedGeometryMode
            faceVertexCount  : int
            vertexCount      : int
            indices          : Option<BufferView>
            uniforms         : Map<Symbol,IMod>
            vertexAttributes : Map<Symbol,BufferView>
        }

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module AdaptiveGeometry =

        let ofIndexedGeometry (uniforms : list<Symbol * IMod>) (ig : IndexedGeometry) =
            let anyAtt = (ig.IndexedAttributes |> Seq.head).Value

            let faceVertexCount, index =
                match ig.IndexArray with
                    | null -> anyAtt.Length, None
                    | index -> index.Length, Some (BufferView.ofArray index)

            let vertexCount =
                anyAtt.Length
                
    
            {
                mode = ig.Mode
                faceVertexCount = faceVertexCount
                vertexCount = vertexCount
                indices = index
                uniforms = Map.ofList uniforms
                vertexAttributes = ig.IndexedAttributes |> SymDict.toMap |> Map.map (fun _ -> BufferView.ofArray)
            }

    type GeometrySignature =
        {
            mode                : IndexedGeometryMode
            indexType           : Type
            vertexBufferTypes   : Map<Symbol, Type>
            uniformTypes        : Map<Symbol, Type>
        }



    type Attributes = Map<Symbol, BufferView>
    type Uniforms = Map<Symbol, IMod>

    type IManagedBufferWriter =
        inherit IAdaptiveObject
        abstract member Write : IAdaptiveObject -> unit

    type IManagedBuffer =
        inherit IDisposable
        inherit IMod<IBuffer>
        abstract member Clear : unit -> unit
        abstract member Capacity : int
        abstract member Set : Range1l * byte[] -> unit
        abstract member Add : Range1l * BufferView -> IDisposable
        abstract member Add : int * IMod -> IDisposable
        abstract member ElementType : Type

    type IManagedBuffer<'a when 'a : unmanaged> =
        inherit IManagedBuffer
        abstract member Count : int
        abstract member Item : int -> 'a with get, set
        abstract member Set : Range1l * 'a[] -> unit

    [<AutoOpen>]
    module private ManagedBufferImplementation =

        type ManagedBuffer<'a when 'a : unmanaged>(runtime : IRuntime) =
            inherit DirtyTrackingAdaptiveObject<ManagedBufferWriter>()
            static let asize = sizeof<'a> |> nativeint
            let store = runtime.CreateMappedBuffer()

            let bufferWriters = Dict<BufferView, ManagedBufferWriter<'a>>()
            let uniformWriters = Dict<IMod, ManagedBufferSingleWriter<'a>>()

            member x.Clear() =
                store.Resize 0n

            member x.Add(range : Range1l, view : BufferView) =
                lock x (fun () ->
                    let count = range.Size + 1L

                    let writer = 
                        bufferWriters.GetOrCreate(view, fun view ->
                            let remove w =
                                x.Dirty.Remove w |> ignore
                                bufferWriters.Remove view |> ignore

                            let data = BufferView.download 0 (int count) view
                            let real : IMod<'a[]> = data |> PrimitiveValueConverter.convertArray view.ElementType
                            let w = new ManagedBufferWriter<'a>(remove, real, store)
                            x.Dirty.Add w |> ignore
                            w
                        )


                    if writer.AddRef range then
                        let min = nativeint(range.Min + count) * asize
                        if store.Capacity < min then
                            store.Resize(Fun.NextPowerOfTwo(int64 min) |> nativeint)

                        lock writer (fun () -> 
                            if not writer.OutOfDate then
                                writer.Write(range)
                        )

                    { new IDisposable with
                        member x.Dispose() =
                            writer.RemoveRef range |> ignore
                    }
                )

            member x.Add(index : int, data : IMod) =
                lock x (fun () ->
                    let mutable isNew = false
                    let writer =
                        uniformWriters.GetOrCreate(data, fun data ->
                            isNew <- true
                            let remove w =
                                x.Dirty.Remove w |> ignore
                                uniformWriters.Remove data |> ignore

                            let real : IMod<'a> = data |> PrimitiveValueConverter.convertValue
                            let w = new ManagedBufferSingleWriter<'a>(remove, real, store)
                            x.Dirty.Add w |> ignore
                            w
                        )
 
                    let range = Range1l(int64 index, int64 index)
                    if writer.AddRef range then
                        let min = nativeint (index + 1) * asize
                        if store.Capacity < min then
                            store.Resize(Fun.NextPowerOfTwo(int64 min) |> nativeint)
                            
                        lock writer (fun () -> 
                            if not writer.OutOfDate then
                                writer.Write(range)
                        )


                        
                    { new IDisposable with
                        member x.Dispose() =
                            writer.RemoveRef range |> ignore
                    }
                )

            member x.Set(range : Range1l, value : byte[]) =
                let count = range.Size + 1L
                let e = nativeint(range.Min + count) * asize
                if store.Capacity < e then
                    store.Resize(Fun.NextPowerOfTwo(int64 e) |> nativeint)

                let gc = GCHandle.Alloc(value, GCHandleType.Pinned)
                try
                    let ptr = gc.AddrOfPinnedObject()
                    let lv = value.Length |> nativeint
                    let mutable remaining = nativeint count * asize
                    let mutable offset = nativeint range.Min * asize
                    while remaining >= lv do
                        store.Write(ptr, offset, lv)
                        offset <- offset + lv
                        remaining <- remaining - lv

                    if remaining > 0n then
                        store.Write(ptr, offset, remaining)

                finally
                    gc.Free()

            member x.Set(index : int, value : 'a) =
                let e = nativeint (index + 1) * asize
                if store.Capacity < e then
                    store.Resize(Fun.NextPowerOfTwo(int64 e) |> nativeint)

                let gc = GCHandle.Alloc(value, GCHandleType.Pinned)
                try store.Write(gc.AddrOfPinnedObject(), nativeint index * asize, asize)
                finally gc.Free()

            member x.Get(index : int) =
                let mutable res = Unchecked.defaultof<'a>
                store.Read(&&res |> NativePtr.toNativeInt, nativeint index * asize, asize)
                res

            member x.Set(range : Range1l, value : 'a[]) =
                let e = nativeint(range.Max + 1L) * asize
                if store.Capacity < e then
                    store.Resize(Fun.NextPowerOfTwo(int64 e) |> nativeint)

                let gc = GCHandle.Alloc(value, GCHandleType.Pinned)
                try store.Write(gc.AddrOfPinnedObject(), nativeint range.Min * asize, nativeint(range.Size + 1L) * asize)
                finally gc.Free()

            member x.GetValue(caller : IAdaptiveObject) =
                x.EvaluateAlways' caller (fun dirty ->
                    for d in dirty do
                        d.Write(x)
                    store.GetValue(x)
                )

            member x.Capacity = store.Capacity
            member x.Count = store.Capacity / asize |> int

            member x.Dispose() =
                store.Dispose()

            interface IDisposable with
                member x.Dispose() = x.Dispose()

            interface IMod with
                member x.IsConstant = false
                member x.GetValue c = x.GetValue c :> obj

            interface IMod<IBuffer> with
                member x.GetValue c = x.GetValue c

            interface IManagedBuffer with
                member x.Clear() = x.Clear()
                member x.Add(range : Range1l, view : BufferView) = x.Add(range, view)
                member x.Add(index : int, data : IMod) = x.Add(index, data)
                member x.Set(range : Range1l, value : byte[]) = x.Set(range, value)
                member x.Capacity = x.Capacity |> int
                member x.ElementType = typeof<'a>

            interface IManagedBuffer<'a> with
                member x.Count = x.Count
                member x.Item
                    with get i = x.Get i
                    and set i v = x.Set(i,v)
                member x.Set(range : Range1l, value : 'a[]) = x.Set(range, value)

        and [<AbstractClass>] ManagedBufferWriter(remove : ManagedBufferWriter -> unit) =
            inherit AdaptiveObject()
            let mutable refCount = 0
            let targetRegions = ReferenceCountingSet<Range1l>()

            abstract member Write : Range1l -> unit
            abstract member Release : unit -> unit

            member x.AddRef(range : Range1l) : bool =
                lock x (fun () ->
                    targetRegions.Add range
                )

            member x.RemoveRef(range : Range1l) : bool = 
                lock x (fun () ->
                    targetRegions.Remove range |> ignore
                    if targetRegions.Count = 0 then
                        x.Release()
                        remove x
                        let mutable foo = 0
                        x.Outputs.Consume(&foo) |> ignore
                        true
                    else
                        false
                )

            member x.Write(caller : IAdaptiveObject) =
                x.EvaluateIfNeeded caller () (fun () ->
                    for r in targetRegions do
                        x.Write(r)
                )

            interface IManagedBufferWriter with
                member x.Write c = x.Write c

        and ManagedBufferWriter<'a when 'a : unmanaged>(remove : ManagedBufferWriter -> unit, data : IMod<'a[]>, store : IMappedBuffer) =
            inherit ManagedBufferWriter(remove)
            static let asize = sizeof<'a> |> nativeint

            override x.Release() = ()

            override x.Write(target) =
                let v = data.GetValue(x)
                let gc = GCHandle.Alloc(v, GCHandleType.Pinned)
                try 
                    store.Write(gc.AddrOfPinnedObject(), nativeint target.Min * asize, nativeint v.Length * asize)
                finally 
                    gc.Free()

        and ManagedBufferSingleWriter<'a when 'a : unmanaged>(remove : ManagedBufferWriter -> unit, data : IMod<'a>, store : IMappedBuffer) =
            inherit ManagedBufferWriter(remove)
            static let asize = sizeof<'a> |> nativeint
            
            override x.Release() = ()

            override x.Write(target) =
                let v = data.GetValue(x)
                let gc = GCHandle.Alloc(v, GCHandleType.Pinned)
                try store.Write(gc.AddrOfPinnedObject(), nativeint target.Min * asize, asize)
                finally gc.Free()

    module ManagedBuffer =

        let private ctorCache = Dict<Type, ConstructorInfo>()

        let private ctor (t : Type) =
            lock ctorCache (fun () ->
                ctorCache.GetOrCreate(t, fun t ->
                    let tb = typedefof<ManagedBuffer<int>>.MakeGenericType [|t|]
                    tb.GetConstructor(
                        BindingFlags.NonPublic ||| BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.Static ||| BindingFlags.CreateInstance,
                        Type.DefaultBinder,
                        [| typeof<IRuntime> |],
                        null
                    )
                )
            )

        let create (t : Type) (runtime : IRuntime) =
            let ctor = ctor t
            ctor.Invoke [| runtime |] |> unbox<IManagedBuffer>


    type private LayoutManager<'a>() =
        let manager = MemoryManager.createNop()
        let store = Dict<'a, managedptr>()
        let cnts = Dict<managedptr, 'a * ref<int>>()


        member x.Alloc(key : 'a, size : int) =
            match store.TryGetValue key with
                | (true, v) -> 
                    let _,r = cnts.[v]
                    Interlocked.Increment &r.contents |> ignore
                    v
                | _ ->
                    let v = manager.Alloc size
                    let r = ref 1
                    cnts.[v] <- (key,r)
                    store.[key] <- (v)
                    v


        member x.TryAlloc(key : 'a, size : int) =
            match store.TryGetValue key with
                | (true, v) -> 
                    let _,r = cnts.[v]
                    Interlocked.Increment &r.contents |> ignore
                    false, v
                | _ ->
                    let v = manager.Alloc size
                    let r = ref 1
                    cnts.[v] <- (key,r)
                    store.[key] <- (v)
                    true, v

        member x.Free(value : managedptr) =
            match cnts.TryGetValue value with
                | (true, (k,r)) ->
                    if Interlocked.Decrement &r.contents = 0 then
                        manager.Free value
                        cnts.Remove value |> ignore
                        store.Remove k |> ignore
                | _ ->
                    ()


    type ManagedDrawCall(call : DrawCallInfo, release : IDisposable) =
        member x.Call = call
        
        member x.Dispose() = release.Dispose()
        interface IDisposable with
            member x.Dispose() = release.Dispose()

    type ManagedPool(runtime : IRuntime, signature : GeometrySignature) =
        static let zero : byte[] = Array.zeroCreate 128
        let mutable count = 0
        let indexManager = LayoutManager<Option<BufferView> * int>()
        let vertexManager = LayoutManager<Attributes>()
        let instanceManager = LayoutManager<Uniforms>()

        let indexBuffer = new ManagedBuffer<int>(runtime) :> IManagedBuffer<int>
        let vertexBuffers = signature.vertexBufferTypes |> Map.toSeq |> Seq.map (fun (k,t) -> k, ManagedBuffer.create t runtime) |> SymDict.ofSeq
        let instanceBuffers = signature.uniformTypes |> Map.toSeq |> Seq.map (fun (k,t) -> k, ManagedBuffer.create t runtime) |> SymDict.ofSeq
        let vertexDisposables = Dictionary<BufferView, IDisposable>()


        let vertexBufferTypes = Map.toArray signature.vertexBufferTypes
        let uniformTypes = Map.toArray signature.uniformTypes

        member x.Runtime = runtime

        member x.Add(g : AdaptiveGeometry) =
            let ds = List()
            let fvc = g.faceVertexCount
            let vertexCount = g.vertexCount
            
            
            let vertexPtr = vertexManager.Alloc(g.vertexAttributes, vertexCount)
            let vertexRange = Range1l(int64 vertexPtr.Offset, int64 vertexPtr.Offset + int64 vertexCount - 1L)
            for (k,t) in vertexBufferTypes do
                let target = vertexBuffers.[k]
                match Map.tryFind k g.vertexAttributes with
                    | Some v -> target.Add(vertexRange, v) |> ds.Add
                    | None -> target.Set(vertexRange, zero)
            


            let instancePtr = instanceManager.Alloc(g.uniforms, 1)
            let instanceIndex = int instancePtr.Offset
            for (k,t) in uniformTypes do
                let target = instanceBuffers.[k]
                match Map.tryFind k g.uniforms with
                    | Some v -> target.Add(instanceIndex, v) |> ds.Add
                    | None -> target.Set(Range1l(int64 instanceIndex, int64 instanceIndex), zero)

            let isNew, indexPtr = indexManager.TryAlloc((g.indices, fvc), fvc)
            let indexRange = Range1l(int64 indexPtr.Offset, int64 indexPtr.Offset + int64 fvc - 1L)
            match g.indices with
                | Some v -> indexBuffer.Add(indexRange, v) |> ds.Add
                | None -> if isNew then indexBuffer.Set(indexRange, Array.init fvc id)

            count <- count + 1

            let disposable =
                { new IDisposable with
                    member __.Dispose() = 
                        lock x (fun () ->
                            count <- count - 1
                            if count = 0 then 
                                for b in vertexBuffers.Values do b.Clear()
                                for b in instanceBuffers.Values do b.Clear()
                                indexBuffer.Clear() 
                            for d in ds do d.Dispose()
                            vertexManager.Free vertexPtr
                            instanceManager.Free instancePtr
                            indexManager.Free indexPtr
                        )
                }

            let call =
                DrawCallInfo(
                    FaceVertexCount = fvc,
                    FirstIndex = int indexPtr.Offset,
                    FirstInstance = int instancePtr.Offset,
                    InstanceCount = 1,
                    BaseVertex = int vertexPtr.Offset
                )

            
            new ManagedDrawCall(call, disposable)

        member x.VertexAttributes =
            { new IAttributeProvider with
                member x.Dispose() = ()
                member x.All = Seq.empty
                member x.TryGetAttribute(sem : Symbol) =
                    match vertexBuffers.TryGetValue sem with
                        | (true, v) -> Some (BufferView(v, v.ElementType))
                        | _ -> None
            }

        member x.InstanceAttributes =
            { new IAttributeProvider with
                member x.Dispose() = ()
                member x.All = Seq.empty
                member x.TryGetAttribute(sem : Symbol) =
                    match instanceBuffers.TryGetValue sem with
                        | (true, v) -> Some (BufferView(v, v.ElementType))
                        | _ -> None
            }

        member x.IndexBuffer =
            BufferView(indexBuffer, indexBuffer.ElementType)

    type DrawCallBuffer(runtime : IRuntime, indexed : bool) =
        inherit Mod.AbstractMod<IIndirectBuffer>()

        let indices = Dict<DrawCallInfo, int>()
        let calls = List<DrawCallInfo>()
        let store = runtime.CreateMappedIndirectBuffer(indexed)

        

        let add(call : DrawCallInfo) =
            if indices.ContainsKey call then 
                false
            else
                store.Resize(Fun.NextPowerOfTwo (calls.Count + 1))
                let count = calls.Count
                indices.[call] <- count
                calls.Add call
                store.Count <- calls.Count
                store.[count] <- call
                true

        let remove(call : DrawCallInfo) =
            match indices.TryRemove call with
                | (true, index) ->
                    if calls.Count = 1 then
                        calls.Clear()
                        store.Resize(0)
                    elif index = calls.Count-1 then
                        calls.RemoveAt index
                    else
                        let lastIndex = calls.Count - 1
                        let last = calls.[lastIndex]
                        indices.[last] <- index
                        calls.[index] <- last
                        store.[index] <- last
                        calls.RemoveAt lastIndex
                        
                    store.Count <- calls.Count
                    true
                | _ ->
                    false

        member x.Add (call : ManagedDrawCall) =
            if add call.Call then
                transact (fun () -> x.MarkOutdated())
                true
            else
                false

        member x.Remove(call : ManagedDrawCall) =
            if remove call.Call then
                transact (fun () -> x.MarkOutdated())
                true
            else
                false

        interface ILockedResource with
            member x.AddLock l = store.AddLock l
            member x.RemoveLock l = store.RemoveLock l

        override x.Compute() =
//            let sw = System.Diagnostics.Stopwatch()
//            sw.Start()
//            let delta = using runtime.ContextLock (fun _ -> r.GetDelta x)
//            sw.Stop()
//            printfn "update %A" (sw.MicroTime / List.length delta)

//            for d in delta do
//                match d with
//                    | Add v -> add v.Call |> ignore
//                    | Rem v -> remove v.Call |> ignore

            // RO add/remove           -> 13.5µs
            // pool.Add/Remove         -> 30µs      (primitive geometries)
            // DrawCallInfo Add/Remove -> 0.45µs

            store.GetValue x

    [<AbstractClass; Sealed; Extension>]
    type IRuntimePoolExtensions private() =

        [<Extension>]
        static member CreateManagedPool(this : IRuntime, signature : GeometrySignature) =
            new ManagedPool(this, signature)

        [<Extension>]
        static member CreateManagedBuffer<'a when 'a : unmanaged>(this : IRuntime) : IManagedBuffer<'a> =
            new ManagedBuffer<'a>(this) :> IManagedBuffer<'a>

        [<Extension>]
        static member CreateManagedBuffer(this : IRuntime, elementType : Type) : IManagedBuffer =
            this |> ManagedBuffer.create elementType


    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Optimizer =
        open Aardvark.Base.Monads.Option

        type RenderObjectSignature =
            {
                IsActive            : IMod<bool>
                RenderPass          : RenderPass
                Mode                : IndexedGeometryMode
                Surface             : IResource<IBackendSurface>
                DepthTest           : IMod<DepthTestMode>
                CullMode            : IMod<CullMode>
                BlendMode           : IMod<BlendMode>
                FillMode            : IMod<FillMode>
                StencilMode         : IMod<StencilMode>
                WriteBuffers        : Option<Set<Symbol>>
                Uniforms            : Map<Symbol, IMod>
                Geometry            : GeometrySignature
            }

        let sw = System.Diagnostics.Stopwatch()

        let modType =
            let cache = 
                Cache<Type, Type>(fun t ->
                    match t with
                        | ModOf t -> t
                        | _ -> failwith ""
                )

            cache.Invoke

        let private tryDecomposeRO (runtime : IRuntime) (signature : IFramebufferSignature) (ro : RenderObject) =
            option {
                let! surface =
                    if ro.Surface.IsConstant then
                        let s = ro.Surface |> Mod.force
                        runtime.ResourceManager.CreateSurface(signature, Mod.constant s) |> Some
                    else
                        None
                sw.Start()

                let! mode =
                    if ro.Mode.IsConstant then Some (Mod.force ro.Mode)
                    else None


                let! drawCall =
                    if ro.DrawCallInfos.IsConstant then
                        match ro.DrawCallInfos |> Mod.force with
                            | [call] when call.InstanceCount = 1 && call.BaseVertex = 0 && call.FirstInstance = 0 && call.FirstIndex = 0 -> 
                                Some call
                            | _ -> None
                    else
                        None
          
                let surf = surface.Handle |> Mod.force

                let mutable attributes = []
                let mutable uniforms = []

                
                for (n,_) in surf.Inputs do
                    let n = Symbol.Create n
                    match ro.VertexAttributes.TryGetAttribute n with
                        | Some v ->
                            attributes <- (n, v, v.ElementType) :: attributes
                        | None ->
                            match ro.Uniforms.TryGetUniform(ro.AttributeScope, n) with
                                | Some v ->
                                    uniforms <- (n, v, v.GetType() |> modType) :: uniforms
                                | _ ->
                                    ()

                let realUniforms =
                    surf.Uniforms
                        |> List.map (fun (n,_) -> Symbol.Create n)
                        |> List.choose (fun n -> match ro.Uniforms.TryGetUniform(ro.AttributeScope, n) with | Some v -> Some (n, v) | _ -> None)
                        |> Map.ofList

                

                let signature = 
                    {
                        mode = mode
                        indexType = match ro.Indices with | Some i -> i.ElementType | _ -> typeof<int>
                        vertexBufferTypes = attributes |> List.map (fun (n,_,t) -> (n,t)) |> Map.ofList
                        uniformTypes = uniforms |> List.map (fun (n,_,t) -> (n,t)) |> Map.ofList
                    }

                let! vertexCount =
                    match ro.Indices with
                        | None -> Some drawCall.FaceVertexCount
                        | Some i ->
                            let (_,v,_) = attributes |> List.head 
                            let b = Mod.force v.Buffer
                            match b with
                                | :? ArrayBuffer as b -> Some b.Data.Length
                                | :? INativeBuffer as b -> Some (b.SizeInBytes / (Marshal.SizeOf v.ElementType))
                                | _ -> None

                let geometry =
                    {
                        mode             = mode
                        faceVertexCount  = drawCall.FaceVertexCount
                        vertexCount      = vertexCount
                        indices          = ro.Indices
                        uniforms         = uniforms |> List.map (fun (n,v,_) -> (n,v)) |> Map.ofList
                        vertexAttributes = attributes |> List.map (fun (n,v,_) -> (n,v)) |> Map.ofList
                    }

                let roSignature =
                    {
                        IsActive            = ro.IsActive
                        RenderPass          = ro.RenderPass
                        Mode                = mode
                        Surface             = surface
                        DepthTest           = ro.DepthTest
                        CullMode            = ro.CullMode
                        BlendMode           = ro.BlendMode
                        FillMode            = ro.FillMode
                        StencilMode         = ro.StencilMode
                        WriteBuffers        = ro.WriteBuffers
                        Uniforms            = realUniforms
                        Geometry            = signature
                    }

                sw.Stop()
                return roSignature, geometry

            }

        let tryDecompose (runtime : IRuntime) (signature : IFramebufferSignature) (ro : IRenderObject) =
            match ro with
                | :? RenderObject as ro -> tryDecomposeRO runtime signature ro
                | _ -> None

        let optimize (runtime : IRuntime) (signature : IFramebufferSignature) (objects : aset<IRenderObject>) : aset<IRenderObject> =
            
            let pools = Dict<GeometrySignature, ManagedPool>()
            let calls = Dict<RenderObjectSignature, DrawCallBuffer * IRenderObject>()
            let disposables = Dict<IRenderObject, IDisposable>()

            let reader = objects.GetReader()
            ASet.custom (fun hugo ->
                
                let output = List<_>()
                let total = System.Diagnostics.Stopwatch()
                total.Start()
                let deltas = reader.GetDelta hugo
                total.Stop()
                printfn "pull: %A" total.MicroTime

                total.Restart()
                sw.Reset()
                for d in deltas do
                    match d with
                        | Add ro ->
                            let res = tryDecompose runtime signature ro
                            match res with
                                | Some (signature, o) ->

                                    let pool = pools.GetOrCreate(signature.Geometry, fun v -> runtime.CreateManagedPool v)

                                    let call = pool.Add o

                                    let callBuffer =
                                        match calls.TryGetValue signature with
                                            | (true, (calls, _)) -> 
                                                calls
                                            | _ ->
                                                let buffer = DrawCallBuffer(runtime, true)
                                                let ro =
                                                    {
                                                        RenderObject.Create() with
                                                            AttributeScope      = Ag.emptyScope
                
                                                            IsActive            = signature.IsActive
                                                            RenderPass          = signature.RenderPass
                                                            DrawCallInfos       = null
                                                            IndirectBuffer      = buffer
                                                            Mode                = Mod.constant signature.Mode
                                                            Surface             = Mod.constant (signature.Surface.Handle |> Mod.force :> ISurface)
                                                            DepthTest           = signature.DepthTest
                                                            CullMode            = signature.CullMode
                                                            BlendMode           = signature.BlendMode
                                                            FillMode            = signature.FillMode
                                                            StencilMode         = signature.StencilMode
                
                                                            Indices             = Some pool.IndexBuffer
                                                            InstanceAttributes  = pool.InstanceAttributes
                                                            VertexAttributes    = pool.VertexAttributes
                
                                                            Uniforms            = UniformProvider.ofMap signature.Uniforms

                                                            Activate            = fun () -> { new IDisposable with member x.Dispose() = () }
                                                            WriteBuffers        = signature.WriteBuffers
                                                    }

                                                calls.[signature] <- (buffer, ro :> IRenderObject)

                                                output.Add(Add (ro :> IRenderObject))

                                                buffer

                                    callBuffer.Add call |> ignore
                                    disposables.[ro] <- 
                                        { new IDisposable with
                                            member x.Dispose() = 
                                                signature.Surface.Dispose()
                                                call.Dispose()
                                                callBuffer.Remove call |> ignore
                                        }

                                | None ->
                                    output.Add (Add ro)
                                    
                        | Rem ro ->
                            match disposables.TryRemove ro with
                                | (true, d) -> d.Dispose()
                                | _ -> output.Add (Rem ro)
                total.Stop()

                printfn "total:     %A" total.MicroTime
                printfn "grounding: %A" sw.MicroTime

                output |> CSharpList.toList
            )




    module Sg =
        type PoolNode(pool : ManagedPool, calls : aset<ManagedDrawCall>) =
            interface ISg
            member x.Pool = pool
            member x.Calls = calls

        let pool (pool : ManagedPool) (calls : aset<ManagedDrawCall>) =
            PoolNode(pool, calls) :> ISg

    [<Aardvark.Base.Ag.Semantic>]
    type PoolSem() =
        member x.RenderObjects(p : Sg.PoolNode) =
            aset {
                let pool = p.Pool
                let ro = Aardvark.SceneGraph.Semantics.RenderObject.create()


                let r = p.Calls.GetReader()
                let calls =
                    let buffer = DrawCallBuffer(pool.Runtime, true)
                    Mod.custom (fun self ->
                        let deltas = r.GetDelta self
                        for d in deltas do
                            match d with
                                | Add v -> buffer.Add v |> ignore
                                | Rem v -> buffer.Remove v |> ignore

                        buffer.GetValue()
                    )

                ro.Mode <- Mod.constant IndexedGeometryMode.TriangleList
                ro.Indices <- Some pool.IndexBuffer
                ro.VertexAttributes <- pool.VertexAttributes
                ro.InstanceAttributes <- pool.InstanceAttributes
                ro.IndirectBuffer <- calls // |> ASet.toMod |> Mod.map (fun calls -> calls |> Seq.toArray |> ArrayBuffer :> IBuffer)
                //ro.DrawCallInfos <- p.Calls |> ASet.toMod |> Mod.map Seq.toList
                yield ro :> IRenderObject
                    
            }

    module Sem =
        let Hugo = Symbol.Create "Hugo"

    let testSg (w : IRenderControl) (r : IRuntime) =
        
        let pool =
            r.CreateManagedPool {
                mode = IndexedGeometryMode.TriangleList
                indexType = typeof<int>
                vertexBufferTypes = 
                    Map.ofList [ 
                        DefaultSemantic.Positions, typeof<V3f>
                        DefaultSemantic.Normals, typeof<V3f> 
                    ]
                uniformTypes = 
                    Map.ofList [ 
                        Sem.Hugo, typeof<M44f> 
                    ]
            }

        let geometry (pos : V3d) =  
            let trafo = Trafo3d.Scale 0.1 * Trafo3d.Translation pos
            Sg.unitSphere 0 (Mod.constant C4b.Red)
                |> Sg.uniform "Hugo" (Mod.constant (trafo.Forward |> M44f.op_Explicit))
//                |> AdaptiveGeometry.ofIndexedGeometry [
//                    Sem.Hugo, Mod.constant (trafo.Forward |> M44f.op_Explicit) :> IMod
//                ]

        let s = 10.0 //50.0
//
//        let renderset (geometries : aset<_>) =
//            let calls = geometries |> ASet.mapUse pool.Add //|> ASet.map (fun c -> c.Call)
//            Sg.pool pool calls

        let all =
            [    
                for x in -s / 2.0 .. s / 2.0 do
                    for y in -s / 2.0 .. s / 2.0 do
                        for z in -s / 2.0 .. s / 2.0 do
                            yield geometry(V3d(x,y,z))
                    
            ]

        Log.line "count: %A" (List.length all)

        let geometries =
            CSet.ofList all

        let initial = geometries.Count
        let random = Random()
        w.Keyboard.DownWithRepeats.Values.Add(fun k ->
            if k = Keys.X then
                if geometries.Count > 0 then
                    let remove = geometries.RandomOrder() |> Seq.atMost 1024 |> Seq.toList
                    transact (fun () ->
                        geometries.ExceptWith remove
                    )

            if k = Keys.T then
                transact (fun () ->
                    geometries.Clear()
                )

            if k = Keys.R then
                transact (fun () ->
                    geometries.Clear()
                    geometries.UnionWith all
                )
                
        )

        let mode = Mod.init FillMode.Fill
        w.Keyboard.KeyDown(Keys.K).Values.Add (fun () ->
            transact (fun () ->
                mode.Value <- 
                    match mode.Value with
                        | FillMode.Fill -> FillMode.Line
                        | _ -> FillMode.Fill
            )
        )

        Sg.set geometries
            |> Sg.fillMode mode


module Controller =
    open Aardvark.Base.Incremental.Operators
    type StartStop<'a> = { start : Event<'a>; stop : Event<'a> }
    type Config =
        {
            look        : StartStop<unit>
            pan         : StartStop<unit>
            zoom        : StartStop<unit>
            forward     : IMod<bool>
            backward    : IMod<bool>
            right       : IMod<bool>
            left        : IMod<bool>
            move        : Event<PixelPosition * PixelPosition>
            scroll      : Event<float>
        }

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Config =
        let wsad (rc : IRenderControl) =
            let lDown       = rc.Mouse.Down.Values |> Event.ofObservable |> Event.filter (fun m -> m = MouseButtons.Left) |> Event.ignore
            let lUp         = rc.Mouse.Up.Values |> Event.ofObservable |> Event.filter (fun m -> m = MouseButtons.Left) |> Event.ignore
            let mDown       = rc.Mouse.Down.Values |> Event.ofObservable |> Event.filter (fun m -> m = MouseButtons.Middle) |> Event.ignore
            let mUp         = rc.Mouse.Up.Values |> Event.ofObservable |> Event.filter (fun m -> m = MouseButtons.Middle) |> Event.ignore
            let rDown       = rc.Mouse.Down.Values |> Event.ofObservable |> Event.filter (fun m -> m = MouseButtons.Right) |> Event.ignore
            let rUp         = rc.Mouse.Up.Values |> Event.ofObservable |> Event.filter (fun m -> m = MouseButtons.Right) |> Event.ignore
            
            let scroll      = rc.Mouse.Scroll.Values |> Event.ofObservable
            let move        = rc.Mouse.Move.Values |> Event.ofObservable

            {
                look        = { start = lDown; stop = lUp }
                pan         = { start = mDown; stop = mUp }
                zoom        = { start = rDown; stop = rUp }
                forward     = rc.Keyboard.IsDown(Keys.W)
                backward    = rc.Keyboard.IsDown(Keys.S)
                right       = rc.Keyboard.IsDown(Keys.D)
                left        = rc.Keyboard.IsDown(Keys.A)
                move        = move
                scroll      = scroll
            }

    let move (c : Config) = 
        proc {  
            let! speed = 
                (c.forward   %?  V2d.OI %. V2d.OO) %+
                (c.backward  %? -V2d.OI %. V2d.OO) %+
                (c.left      %? -V2d.IO %. V2d.OO) %+ 
                (c.right     %?  V2d.IO %. V2d.OO)

            if speed <> V2d.Zero then
                for dt in Proc.dt do
                    do! fun (cam : CameraView) -> 
                        let direction = 
                            speed.X * cam.Right + 
                            speed.Y * cam.Forward

                        let delta = 0.5 * direction * dt.TotalSeconds

                        cam.WithLocation(cam.Location + delta)
        }

    let look (c : Config) =
        Proc.startStop (Proc.ofEvent c.look.start) (Proc.ofEvent c.look.stop) {
            for (o, n) in c.move do
                let delta = n.Position - o.Position
                do! fun (s : CameraView) ->
                    let trafo =
                        M44d.Rotation(s.Right, float delta.Y * -0.01) *
                        M44d.Rotation(s.Sky, float delta.X * -0.01)

                    let newForward = trafo.TransformDir s.Forward |> Vec.normalize
                    s.WithForward(newForward)
        }


    let scroll (c : Config) =
        proc {
            let mutable speed = 0.0
            while true do
                try
                    do! until [ Proc.ofEvent c.scroll ]

                    let interpolate =
                        proc {
                            let! dt = Proc.dt
                            do! fun (s : CameraView) -> 
                                let v = speed * s.Forward
                                let res = CameraView.withLocation (s.Location + dt.TotalSeconds *0.1 * v) s
                                speed <- speed * Fun.Pow(0.004, dt.TotalSeconds)
                                res

                            if abs speed > 0.5 then 
                                do! self
                            else 
                                do! Proc.never
                        }

                    do! interpolate

                with delta ->
                    speed <- speed + delta
        }
            
    let pan (c : Config) =
        proc {
            while true do
                let! d = c.pan.start
                try
                    do! until [ Proc.ofEvent c.pan.stop ]
                    for (o, n) in c.move do
                        let delta = n.Position - o.Position
                        do! State.modify (fun (s : CameraView) ->
                            let step = 0.05 * (s.Down * float delta.Y + s.Right * float delta.X)
                            s.WithLocation(s.Location + step)
                        )


                with _ ->
                    ()
        }

    let zoom (c : Config) =
        proc {
            while true do
                let! d = c.zoom.start
                try
                    do! until [ Proc.ofEvent c.zoom.stop ]
                    for (o, n) in c.move do
                        let delta = n.Position - o.Position
                        do! State.modify (fun (s : CameraView) ->
                            let step = -0.05 * (s.Forward * float delta.Y)
                            s.WithLocation(s.Location + step)
                        )


                with _ ->
                    ()
        }
    
    let control (c : Config) =
        Proc.par [ look c; scroll c; move c; pan c; zoom c ]


module Maya = 

    module Shader =
        open FShade

        type HugoVertex = 
            {
                [<Semantic("Hugo")>] m : M44d
                [<Position>] p : V4d
            }

        let hugoShade (v : HugoVertex) =
            vertex {
                return { v 
                    with 
                        p = v.m * v.p 
                }
            }

        type Vertex = 
            {
                [<Semantic("ThingTrafo")>] m : M44d
                [<Semantic("ThingNormalTrafo")>] nm : M33d
                [<Position>] p : V4d
                [<Normal>] n : V3d
            }

        let thingTrafo (v : Vertex) =
            vertex {
                return { v 
                    with 
                        p = v.m * v.p 
                        n = v.nm * v.n
                }
            }

    [<Flags>]
    type ControllerPart =
        | None = 0x00
        | X = 0x01 
        | Y = 0x02 
        | Z = 0x04

    let radius = 0.025

    let intersectController (trafo : Trafo3d) (r : Ray3d) =
        let innerRay = r.Transformed(trafo.Backward)

        let mutable res = ControllerPart.None

        if innerRay.GetMinimalDistanceTo(Line3d(V3d.Zero, V3d.IOO)) < radius then
            res <- res ||| ControllerPart.X

        if innerRay.GetMinimalDistanceTo(Line3d(V3d.Zero, V3d.OIO)) < radius then
            res <- res ||| ControllerPart.Y

        if innerRay.GetMinimalDistanceTo(Line3d(V3d.Zero, V3d.OOI)) < radius then
            res <- res ||| ControllerPart.Z

        res

    open Aardvark.SceneGraph.Semantics
    
    let run () =

        Ag.initialize()
        Aardvark.Init()
        use app = new OpenGlApplication()
        use win = app.CreateGameWindow()
        //use win = app.CreateSimpleRenderWindow(1)
        //win.VSync <- OpenTK.VSyncMode.On
        //win.Text <- "Aardvark rocks \\o/"

        let view = CameraView.LookAt(V3d(2.0,2.0,2.0), V3d.Zero, V3d.OOI)
        let perspective = 
            win.Sizes 
              |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 50.0 (float s.X / float s.Y))


        let viewTrafo = DefaultCameraController.control win.Mouse win.Keyboard win.Time view

      

        let pool        = GeometryPool.create()
        let box         = pool.Add Primitives.unitBox.Flat
        let cone        = pool.Add (Primitives.unitCone 16).Flat
        let cylinder    = pool.Add (Primitives.unitCylinder 16).Flat


        let scaleCylinder = Trafo3d.Scale(radius, radius, 1.0)

        let render = 
            Mod.init [
                scaleCylinder * Trafo3d.FromOrthoNormalBasis(V3d.OOI, V3d.OIO, V3d.IOO), cylinder, C4b.Red
                scaleCylinder * Trafo3d.FromOrthoNormalBasis(V3d.IOO, V3d.OOI, V3d.OIO), cylinder, C4b.Green

                scaleCylinder, cylinder, C4b.Blue
            ]

        let drawCallInfos = 
            let rangeToInfo (i : int) (r : Range1i) =
                DrawCallInfo(
                    FaceVertexCount = r.Size + 1, 
                    FirstIndex = r.Min, 
                    InstanceCount = 1, 
                    FirstInstance = i
                )
            render |> Mod.map (fun l -> l |> List.mapi (fun i (_,g,_) -> rangeToInfo i g) |> IndirectBuffer.ofList)

        let trafos =
            let buffer = render |> Mod.map (fun v -> v |> List.map (fun (t,_,_) -> t.Forward |> M44f.op_Explicit) |> List.toArray |> ArrayBuffer :> IBuffer) 
            BufferView(buffer, typeof<M44f>)

        let normalTrafos =
            let buffer = render |> Mod.map (fun v -> v |> List.map (fun (t,_,_) -> t.Backward.Transposed.UpperLeftM33() |> M33f.op_Explicit) |> List.toArray |> ArrayBuffer :> IBuffer) 
            BufferView(buffer, typeof<M33f>)


        let colors =
            let buffer = render |> Mod.map (fun v -> v |> List.map (fun (_,_,c) -> c) |> List.toArray |> ArrayBuffer :> IBuffer) 
            BufferView(buffer, typeof<C4b>)

        let trafo = Symbol.Create "ThingTrafo"
        let normalTrafo = Symbol.Create "ThingNormalTrafo"
        let color = DefaultSemantic.Colors

        let pos = BufferView(pool.GetBuffer DefaultSemantic.Positions, typeof<V3f>)
        let n = BufferView(pool.GetBuffer DefaultSemantic.Normals, typeof<V3f>)

        let sg =
            Sg.air {
                do! Air.BindEffect [
                        Shader.thingTrafo |> toEffect
                        DefaultSurfaces.trafo |> toEffect
                        DefaultSurfaces.vertexColor |> toEffect
                        DefaultSurfaces.simpleLighting |> toEffect
                    ]

                do! Air.BindVertexBuffers [
                        DefaultSemantic.Positions, pos
                        DefaultSemantic.Normals, n
                    ]

                do! Air.BindInstanceBuffers [
                        normalTrafo, normalTrafos
                        trafo, trafos
                        color, colors
                    ]

                do! Air.Toplogy IndexedGeometryMode.TriangleList
                do! Air.DrawIndirect drawCallInfos
            }

        let sg =
            let test = 
                Pooling.testSg win app.Runtime
                |> Sg.effect [
                    Shader.hugoShade |> toEffect
                    DefaultSurfaces.trafo |> toEffect
                    DefaultSurfaces.constantColor C4f.Red |> toEffect
                    DefaultSurfaces.simpleLighting |> toEffect
                ]
            test

        
        let camera = Mod.map2 (fun v p -> { cameraView = v; frustum = p }) viewTrafo perspective
        let pickRay = Mod.map2 Camera.pickRay camera win.Mouse.Position
        let trafo = Mod.init Trafo3d.Identity
        let controlledAxis = Mod.map2 intersectController trafo pickRay

//        controlledAxis |> Mod.unsafeRegisterCallbackKeepDisposable (fun c ->
//            printfn "%A" c
//        ) |> ignore

//        let mutable lastRay = pickRay.GetValue()
//        let  moving = ref ControllerPart.None
//        win.Mouse.Down.Values.Add (fun b ->
//            if b = MouseButtons.Left then
//                let c = controlledAxis.GetValue()
//                lastRay <- pickRay.GetValue()
//                moving := c
//                printfn "down %A" c
//        )
//
//        win.Mouse.Move.Values.Add (fun m ->
//            match !moving with
//                | ControllerPart.None -> ()
//                | p ->
//                    printfn "move"
//                    let t = trafo.GetValue()
//                    let pickRay = pickRay.GetValue()
//                    
//                    let ray = pickRay.Transformed(t.Backward)
//                    let last = lastRay.Transformed(t.Backward)
//
//                    let delta = 
//                        match p with
//                            | ControllerPart.X -> 
//                                V3d(ray.Intersect(Plane3d.ZPlane).X - last.Intersect(Plane3d.ZPlane).X, 0.0, 0.0)
//                            | ControllerPart.Y -> 
//                                V3d(0.0, ray.Intersect(Plane3d.ZPlane).Y - last.Intersect(Plane3d.ZPlane).Y, 0.0)
//                            | _ -> 
//                                V3d(0.0, 0.0, ray.Intersect(Plane3d.XPlane).Z - last.Intersect(Plane3d.XPlane).Z)
//                    printfn "%A" delta
//                    transact (fun () ->
//                        trafo.Value <- t * Trafo3d.Translation(delta)
//                    )
//
//                    lastRay <- pickRay
//        )
//        win.Mouse.Up.Values.Add (fun b ->
//            if b = MouseButtons.Left then
//                moving := ControllerPart.None
//        )


        let wsad = Controller.Config.wsad win
        
        let sepp = win.Keyboard.KeyDown(Keys.Y).Values |> Event.ofObservable |> Proc.ofEvent
        let switchMode = win.Keyboard.KeyDown(Keys.P).Values |> Event.ofObservable |> Proc.ofEvent
        let isActive = 
            switchMode |> Proc.fold (fun a () -> not a) true

        let all =
            let inner = Controller.control wsad 
            proc {
                let! active = isActive
                if active then
                    do! inner
            }
//            let switchMode = win.Keyboard.KeyDown(Keys.X).Values |> Event.ofObservable |> Proc.ofEvent
//            Proc.whenever 
//                switchMode 
//                true
//                (fun () active -> not active)
//                (fun active -> 
//                    if active then Controller.control wsad 
//                    else Proc.never
//                )

        let rand = Random()

        let sleepMs = ref 0
        let mutable cnt = 0
        let adjust (t : Time) =
            cnt <- cnt + 1
            sleepMs := rand.Next(1, 40)

            t + MicroTime(TimeSpan.FromMilliseconds (20.0 + float !sleepMs))

        let cam = Proc.toMod adjust view all

//        let camera = Mod.init view
//        let runner =
//            async {
//                do! Async.SwitchToNewThread()
//                while true do
//                    let v = Mod.force cam
//                    transact (fun () -> camera.Value <- v)
//                    do! Async.Sleep 1
//            }
//        Async.Start runner


        //let camera = view |> DefaultCameraController.control win.Mouse win.Keyboard win.Time

        let sg =
            sg
                |> Sg.trafo trafo
                // viewTrafo () creates camera controls and returns IMod<ICameraView> which we project to its view trafo component by using CameraView.viewTrafo
                |> Sg.viewTrafo (cam |> Mod.map CameraView.viewTrafo ) 
                // perspective () connects a proj trafo to the current main window (in order to take account for aspect ratio when creating the matrices.
                // Again, perspective() returns IMod<Frustum> which we project to its matrix by mapping ofer Frustum.projTrafo.
                |> Sg.projTrafo (perspective |> Mod.map Frustum.projTrafo    )
                |> Sg.uniform "LightLocation" (Mod.constant (V3d.III * 10.0))
        
        let objects = sg.RenderObjects() |> Pooling.Optimizer.optimize app.Runtime win.FramebufferSignature

        
        use task = app.Runtime.CompileRender(win.FramebufferSignature, { BackendConfiguration.NativeOptimized with useDebugOutput = false }, objects)
        

        
        let busywait(wanted : int) = 
            let sw = System.Diagnostics.Stopwatch()
            sw.Start()
            while int sw.Elapsed.TotalMilliseconds < wanted do ()
            sw.Stop()

        let busywait(wanted : int) =
            System.Threading.Thread.Sleep(wanted)

        let task = 
            RenderTask.ofList [
                task
                //RenderTask.custom (fun (self, o) -> busywait !sleepMs; FrameStatistics.Zero)
            ]
        
        win.RenderTask <- task |> DefaultOverlays.withStatistics
        win.Run()

