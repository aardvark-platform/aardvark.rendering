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

    type AdaptiveGeometry =
        {
            mode             : IndexedGeometryMode
            faceVertexCount  : int
            vertexCount      : int
            indices          : Option<BufferView>
            uniforms         : Map<Symbol, IMod>
            vertexAttributes : Map<Symbol, BufferView>
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
        abstract member Set : Range1i * byte[] -> unit
        abstract member Add : Range1i * BufferView -> IDisposable
        abstract member Add : int * IMod -> IDisposable
        abstract member ElementType : Type

    type IManagedBuffer<'a when 'a : unmanaged> =
        inherit IManagedBuffer
        abstract member Count : int
        abstract member Item : int -> 'a with get, set
        abstract member Set : Range1i * 'a[] -> unit

    [<AutoOpen>]
    module private ManagedBufferImplementation =

        type ManagedBuffer<'a when 'a : unmanaged>(runtime : IRuntime) =
            inherit DirtyTrackingAdaptiveObject<ManagedBufferWriter>()
            static let asize = sizeof<'a>
            let store = runtime.CreateMappedBuffer()

            let bufferWriters = Dict<BufferView, ManagedBufferWriter<'a>>()
            let uniformWriters = Dict<IMod, ManagedBufferSingleWriter<'a>>()

            member x.Clear() =
                store.Resize 0

            member x.Add(range : Range1i, view : BufferView) =
                lock x (fun () ->
                    let count = range.Size + 1

                    let writer = 
                        bufferWriters.GetOrCreate(view, fun view ->
                            let remove w =
                                x.Dirty.Remove w |> ignore
                                bufferWriters.Remove view |> ignore

                            let data = BufferView.download 0 count view
                            let real : IMod<'a[]> = data |> PrimitiveValueConverter.convertArray view.ElementType
                            let w = new ManagedBufferWriter<'a>(remove, real, store)
                            x.Dirty.Add w |> ignore
                            w
                        )


                    if writer.AddRef range then
                        let min = (range.Min + count) * asize
                        if store.Capacity < min then
                            store.Resize(Fun.NextPowerOfTwo min)

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
 
                    let range = Range1i(index, index)
                    if writer.AddRef range then
                        let min = (index + 1) * asize
                        if store.Capacity < min then
                            store.Resize(Fun.NextPowerOfTwo min)
                            
                        lock writer (fun () -> 
                            if not writer.OutOfDate then
                                writer.Write(range)
                        )


                        
                    { new IDisposable with
                        member x.Dispose() =
                            writer.RemoveRef range |> ignore
                    }
                )

            member x.Set(range : Range1i, value : byte[]) =
                let count = range.Size + 1
                let e = (range.Min + count) * asize
                if store.Capacity < e then
                    store.Resize(Fun.NextPowerOfTwo e)

                let gc = GCHandle.Alloc(value, GCHandleType.Pinned)
                try
                    let ptr = gc.AddrOfPinnedObject()
                    let lv = value.Length
                    let mutable remaining = count * asize
                    let mutable offset = range.Min * asize
                    while remaining >= lv do
                        store.Write(ptr, offset, lv)
                        offset <- offset + lv
                        remaining <- remaining - lv

                    if remaining > 0 then
                        store.Write(ptr, offset, remaining)

                finally
                    gc.Free()

            member x.Set(index : int, value : 'a) =
                let e = (index + 1) * asize
                if store.Capacity < e then
                    store.Resize(Fun.NextPowerOfTwo e)

                let gc = GCHandle.Alloc(value, GCHandleType.Pinned)
                try store.Write(gc.AddrOfPinnedObject(), index * asize, asize)
                finally gc.Free()

            member x.Get(index : int) =
                let mutable res = Unchecked.defaultof<'a>
                store.Read(&&res |> NativePtr.toNativeInt, index * asize, sizeof<'a>)
                res

            member x.Set(range : Range1i, value : 'a[]) =
                let e = (range.Max + 1) * asize
                if store.Capacity < e then
                    store.Resize(Fun.NextPowerOfTwo e)

                let gc = GCHandle.Alloc(value, GCHandleType.Pinned)
                try store.Write(gc.AddrOfPinnedObject(), range.Min * asize, (range.Size + 1) * asize)
                finally gc.Free()

            member x.GetValue(caller : IAdaptiveObject) =
                x.EvaluateAlways' caller (fun dirty ->
                    for d in dirty do
                        d.Write(x)
                    store.GetValue(x)
                )

            member x.Capacity = store.Capacity
            member x.Count = store.Capacity / asize

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
                member x.Add(range : Range1i, view : BufferView) = x.Add(range, view)
                member x.Add(index : int, data : IMod) = x.Add(index, data)
                member x.Set(range : Range1i, value : byte[]) = x.Set(range, value)
                member x.Capacity = x.Capacity
                member x.ElementType = typeof<'a>

            interface IManagedBuffer<'a> with
                member x.Count = x.Count
                member x.Item
                    with get i = x.Get i
                    and set i v = x.Set(i,v)
                member x.Set(range : Range1i, value : 'a[]) = x.Set(range, value)

        and [<AbstractClass>] ManagedBufferWriter(remove : ManagedBufferWriter -> unit) =
            inherit AdaptiveObject()
            let mutable refCount = 0
            let targetRegions = ReferenceCountingSet<Range1i>()

            abstract member Write : Range1i -> unit
            abstract member Release : unit -> unit

            member x.AddRef(range : Range1i) : bool =
                lock x (fun () ->
                    targetRegions.Add range
                )

            member x.RemoveRef(range : Range1i) : bool = 
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
            static let asize = sizeof<'a>

            override x.Release() = ()

            override x.Write(target) =
                let v = data.GetValue(x)
                let gc = GCHandle.Alloc(v, GCHandleType.Pinned)
                try 
                    store.Write(gc.AddrOfPinnedObject(), target.Min * asize, v.Length * asize)
                finally 
                    gc.Free()

        and ManagedBufferSingleWriter<'a when 'a : unmanaged>(remove : ManagedBufferWriter -> unit, data : IMod<'a>, store : IMappedBuffer) =
            inherit ManagedBufferWriter(remove)
            static let asize = sizeof<'a>
            
            override x.Release() = ()

            override x.Write(target) =
                let v = data.GetValue(x)
                let gc = GCHandle.Alloc(v, GCHandleType.Pinned)
                try store.Write(gc.AddrOfPinnedObject(), target.Min * asize, asize)
                finally gc.Free()

    module ManagedBuffer =

        let private ctorCache = Dict<Type, ConstructorInfo>()

        let private ctor (t : Type) =
            ctorCache.GetOrCreate(t, fun t ->
                let tb = typedefof<ManagedBuffer<int>>.MakeGenericType [|t|]
                tb.GetConstructor(
                    BindingFlags.NonPublic ||| BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.Static ||| BindingFlags.CreateInstance,
                    Type.DefaultBinder,
                    [| typeof<IRuntime> |],
                    null
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

        let getVertexBuffer (sym : Symbol) =
            vertexBuffers.GetOrCreate(sym, fun sym -> 
                match Map.tryFind sym signature.vertexBufferTypes with
                    | Some t -> runtime |> ManagedBuffer.create t
                    | None -> failwithf "[Pool] cannot get attribute type for %A" sym
            )

        let getInstanceBuffer (sym : Symbol) =
            instanceBuffers.GetOrCreate(sym, fun sym ->
                match Map.tryFind sym signature.uniformTypes with
                    | Some t -> runtime |> ManagedBuffer.create t
                    | None -> failwithf "[Pool] cannot get attribute type for %A" sym
            )

        let vertexDisposables = Dictionary<BufferView, IDisposable>()

        member x.Add(g : AdaptiveGeometry) =
            let ds = List()
            let fvc = g.faceVertexCount
            let vertexCount = g.vertexCount
            
            let vertexPtr = vertexManager.Alloc(g.vertexAttributes, vertexCount)
            let vertexRange = Range1i(int vertexPtr.Offset, int vertexPtr.Offset + vertexCount - 1)
            for (k,t) in Map.toSeq signature.vertexBufferTypes do
                let target = getVertexBuffer k
                match Map.tryFind k g.vertexAttributes with
                    | Some v -> target.Add(vertexRange, v) |> ds.Add
                    | None -> target.Set(vertexRange, zero)

            let instancePtr = instanceManager.Alloc(g.uniforms, 1)
            let instanceIndex = int instancePtr.Offset
            for (k,t) in Map.toSeq signature.uniformTypes do
                let target = getInstanceBuffer k
                match Map.tryFind k g.uniforms with
                    | Some v -> target.Add(instanceIndex, v) |> ds.Add
                    | None -> target.Set(Range1i(instanceIndex, instanceIndex), zero)

            let isNew, indexPtr = indexManager.TryAlloc((g.indices, fvc), fvc)
            let indexRange = Range1i(int indexPtr.Offset, int indexPtr.Offset + fvc - 1)
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


    module Sg =
        type PoolNode(pool : ManagedPool, calls : aset<DrawCallInfo>) =
            interface ISg
            member x.Pool = pool
            member x.Calls = calls

        let pool (pool : ManagedPool) (calls : aset<DrawCallInfo>) =
            PoolNode(pool, calls) :> ISg

    [<Aardvark.Base.Ag.Semantic>]
    type PoolSem() =
        member x.RenderObjects(p : Sg.PoolNode) =
            aset {
                let pool = p.Pool
                let ro = Aardvark.SceneGraph.Semantics.RenderObject.create()

                ro.Mode <- Mod.constant IndexedGeometryMode.TriangleList
                ro.Indices <- Some pool.IndexBuffer
                ro.VertexAttributes <- pool.VertexAttributes
                ro.InstanceAttributes <- pool.InstanceAttributes
                ro.IndirectBuffer <- p.Calls |> ASet.toMod |> Mod.map (fun calls -> calls |> Seq.toArray |> ArrayBuffer :> IBuffer)
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
                        DefaultSemantic.Positions, typeof<V4f>
                        DefaultSemantic.Normals, typeof<V3f> 
                    ]
                uniformTypes = 
                    Map.ofList [ 
                        Sem.Hugo, typeof<M44f> 
                    ]
            }

        let geometry (pos : V3d) =  
            let trafo = Trafo3d.Scale 0.1 * Trafo3d.Translation pos
            Primitives.unitSphere 4
                |> AdaptiveGeometry.ofIndexedGeometry [
                    Sem.Hugo, Mod.constant trafo :> IMod
                ]

        let s = 20.0

        let renderset (geometries : aset<AdaptiveGeometry>) =
            let calls = geometries |> ASet.mapUse (fun v -> pool.Add v) |> ASet.map (fun c -> c.Call)
            Sg.pool pool calls

        let all =
            [    
                for x in -s / 2.0 .. s / 2.0 do
                    for y in -s / 2.0 .. s / 2.0 do
                        for z in -s / 2.0 .. s / 2.0 do
                            yield geometry (V3d(x,y,z))
                    
            ]

        let geometries =
            CSet.ofList all

        let initial = geometries.Count
        let random = Random()
        w.Keyboard.DownWithRepeats.Values.Add(fun k ->
            if k = Keys.X then
                for i in 1 .. (initial / 20) do
                    if geometries.Count > 0 then
                        let arr = geometries |> Seq.toArray
                        let idx = random.Next(arr.Length)

                        let g = arr.[idx]
                        transact (fun () ->
                            geometries.Remove g |> ignore
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

        renderset geometries
            |> Sg.fillMode mode


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


    
    let run () =

        Ag.initialize()
        Aardvark.Init()
        use app = new OpenGlApplication()
        use win = app.CreateSimpleRenderWindow(1)
        win.Text <- "Aardvark rocks \\o/"

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
            render |> Mod.map (fun l -> l |> List.mapi (fun i (_,g,_) -> rangeToInfo i g) |> List.toArray |> ArrayBuffer :> IBuffer)

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

//        let sg =
//            let test = 
//                Pooling.testSg win app.Runtime
//                |> Sg.effect [
//                    Shader.hugoShade |> toEffect
//                    DefaultSurfaces.trafo |> toEffect
//                    DefaultSurfaces.constantColor C4f.Red |> toEffect
//                    DefaultSurfaces.simpleLighting |> toEffect
//                ]
//            test

        let camera = Mod.map2 (fun v p -> { cameraView = v; frustum = p }) viewTrafo perspective
        let pickRay = Mod.map2 Camera.pickRay camera win.Mouse.Position
        let trafo = Mod.init Trafo3d.Identity
        let controlledAxis = Mod.map2 intersectController trafo pickRay

//        controlledAxis |> Mod.unsafeRegisterCallbackKeepDisposable (fun c ->
//            printfn "%A" c
//        ) |> ignore

        let mutable lastRay = pickRay.GetValue()
        let  moving = ref ControllerPart.None
        win.Mouse.Down.Values.Add (fun b ->
            if b = MouseButtons.Left then
                let c = controlledAxis.GetValue()
                lastRay <- pickRay.GetValue()
                moving := c
                printfn "down %A" c
        )

        win.Mouse.Move.Values.Add (fun m ->
            match !moving with
                | ControllerPart.None -> ()
                | p ->
                    printfn "move"
                    let t = trafo.GetValue()
                    let pickRay = pickRay.GetValue()
                    
                    let ray = pickRay.Transformed(t.Backward)
                    let last = lastRay.Transformed(t.Backward)

                    let delta = 
                        match p with
                            | ControllerPart.X -> 
                                V3d(ray.Intersect(Plane3d.ZPlane).X - last.Intersect(Plane3d.ZPlane).X, 0.0, 0.0)
                            | ControllerPart.Y -> 
                                V3d(0.0, ray.Intersect(Plane3d.ZPlane).Y - last.Intersect(Plane3d.ZPlane).Y, 0.0)
                            | _ -> 
                                V3d(0.0, 0.0, ray.Intersect(Plane3d.XPlane).Z - last.Intersect(Plane3d.XPlane).Z)
                    printfn "%A" delta
                    transact (fun () ->
                        trafo.Value <- t * Trafo3d.Translation(delta)
                    )

                    lastRay <- pickRay
        )
        win.Mouse.Up.Values.Add (fun b ->
            if b = MouseButtons.Left then
                moving := ControllerPart.None
        )

        let w = Event.ofObservable (win.Keyboard.KeyDown(Keys.W).Values) |> Event.map (constF ( V2d.OI))
        let a = Event.ofObservable (win.Keyboard.KeyDown(Keys.A).Values) |> Event.map (constF (-V2d.IO))
        let s = Event.ofObservable (win.Keyboard.KeyDown(Keys.S).Values) |> Event.map (constF (-V2d.OI))
        let d = Event.ofObservable (win.Keyboard.KeyDown(Keys.D).Values) |> Event.map (constF ( V2d.IO))
        let w' = Event.ofObservable (win.Keyboard.KeyUp(Keys.W).Values) |> Event.map (constF (-V2d.OI))
        let a' = Event.ofObservable (win.Keyboard.KeyUp(Keys.A).Values) |> Event.map (constF ( V2d.IO))
        let s' = Event.ofObservable (win.Keyboard.KeyUp(Keys.S).Values) |> Event.map (constF ( V2d.OI))
        let d' = Event.ofObservable (win.Keyboard.KeyUp(Keys.D).Values) |> Event.map (constF (-V2d.IO))

        let changeSpeed =
            Proc.any [
                Proc.ofEvent w; Proc.ofEvent a; Proc.ofEvent s; Proc.ofEvent d;
                Proc.ofEvent w'; Proc.ofEvent a'; Proc.ofEvent s'; Proc.ofEvent d';
            ]

        let wsad =     
            proc {  
                let mutable speed = V2d.Zero
                while true do
                    try
                        do! until [ changeSpeed ]

                        if speed = V2d.Zero then
                            do! Proc.never
                        else
                            for dt in Proc.dt do
                                do! State.modify (fun (s : CameraView) -> 
                                    let v = speed.X * s.Right + speed.Y * s.Forward
                                    CameraView.withLocation (s.Location + dt.TotalSeconds * 0.5 * v) s
                                )

                    with delta ->
                        speed <- speed + delta
            }

        let down = win.Mouse.Down.Values |> Event.ofObservable |> Event.filter (fun e -> e = MouseButtons.Left)
        let up = win.Mouse.Up.Values |> Event.ofObservable |> Event.filter (fun e -> e = MouseButtons.Left)
        let move = win.Mouse.Move.Values |> Event.ofObservable
        let look =
            proc {
                while true do
                    let! d = down
                    try
                        do! until [ Proc.ofEvent up ]
                        for (o, n) in move do
                            let delta = n.Position - o.Position
                            let! v = 
                                state {
                                    do! State.modify (fun (s : CameraView) ->
                                        let trafo =
                                            M44d.Rotation(s.Right, float delta.Y * -0.01) *
                                            M44d.Rotation(s.Sky, float delta.X * -0.01)

                                        let newForward = trafo.TransformDir s.Forward |> Vec.normalize
                                        s.WithForward(newForward)
                                    )
                                    return 1
                                }
                            return ()


                    with _ ->
                        ()
            }

        
        let scroll = win.Mouse.Scroll.Values |> Event.ofObservable
        let zoom =
            proc {
                let mutable speed = 0.0
                while true do
                    try
                        do! until [ Proc.ofEvent scroll ]

                        let rec scrooly () : Proc<_,unit> =
                            proc {
                                let! dt = Proc.dt
                                do! State.modify (fun (s : CameraView) -> 
                                    let v = speed * s.Forward
                                    let res = CameraView.withLocation (s.Location + dt.TotalSeconds *0.1 * v) s
                                    speed <- speed * Fun.Pow(0.004, dt.TotalSeconds)
                                    res
                                )

                                if abs speed > 0.5 then return! scrooly ()
                                else return! Proc.never
                            }

                        do! scrooly()

                    with delta ->
                        speed <- speed + delta
            }
            

        let all =
            Proc.par [ 
                look
                zoom
                wsad  
            ]

        let camera = Proc.toMod view all

        let sg =
            sg
                |> Sg.trafo trafo
                // viewTrafo () creates camera controls and returns IMod<ICameraView> which we project to its view trafo component by using CameraView.viewTrafo
                |> Sg.viewTrafo (camera |> Mod.map CameraView.viewTrafo ) 
                // perspective () connects a proj trafo to the current main window (in order to take account for aspect ratio when creating the matrices.
                // Again, perspective() returns IMod<Frustum> which we project to its matrix by mapping ofer Frustum.projTrafo.
                |> Sg.projTrafo (perspective |> Mod.map Frustum.projTrafo    )

        use task = app.Runtime.CompileRender(win.FramebufferSignature, { BackendConfiguration.ManagedOptimized with useDebugOutput = true }, sg)
        win.RenderTask <- task |> DefaultOverlays.withStatistics
        win.Run()

