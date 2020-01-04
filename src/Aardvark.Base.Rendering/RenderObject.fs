namespace Aardvark.Base

open System
open Aardvark.Base.Incremental
open System.Runtime.InteropServices
open Aardvark.Base.Rendering
open Microsoft.FSharp.NativeInterop

#nowarn "9"

[<AllowNullLiteral>]
type ISurface = interface end

[<AllowNullLiteral>]
type IDisposableSurface =
    inherit ISurface
    inherit IDisposable


[<AllowNullLiteral>]
type IAttributeProvider =
    inherit IDisposable
    abstract member TryGetAttribute : name : Symbol -> Option<BufferView>
    abstract member All : seq<Symbol * BufferView>

[<AllowNullLiteral>]
type IUniformProvider =
    inherit IDisposable
    abstract member TryGetUniform : scope : Ag.Scope * name : Symbol -> Option<IMod>


type AttributeProvider private() =
    static let empty = 
        { new IAttributeProvider with
            member x.Dispose() = ()
            member x.All = Seq.empty
            member x.TryGetAttribute _ = None
        }

    static member Empty = empty

    static member onDispose (callback : unit -> unit) (a : IAttributeProvider) =
        { new IAttributeProvider with
            member x.Dispose() = callback(); a.Dispose()
            member x.All = a.All
            member x.TryGetAttribute(name : Symbol) = a.TryGetAttribute name
        }

    // Symbol / BufferView
    static member ofDict (values : SymbolDict<BufferView>) =
        { new IAttributeProvider with
            member x.Dispose() = ()
            member x.All = values |> SymDict.toSeq
            member x.TryGetAttribute(name : Symbol) =
                match values.TryGetValue name with
                    | (true, v) -> Some v
                    | _ -> None
        }
        
    static member ofMap (values : Map<Symbol, BufferView>) =
        { new IAttributeProvider with
            member x.Dispose() = ()
            member x.All = values |> Map.toSeq
            member x.TryGetAttribute(name : Symbol) = Map.tryFind name values
        }
        
    static member ofList (values : list<Symbol * BufferView>) =
        values |> SymDict.ofList |> AttributeProvider.ofDict

    static member ofSeq (values : seq<Symbol * BufferView>) =
        values |> SymDict.ofSeq |> AttributeProvider.ofDict
        

    // Symbol / Array
    static member ofDict (values : SymbolDict<Array>) =
        values |> SymDict.map (fun _ v -> BufferView.ofArray v) |> AttributeProvider.ofDict

    static member ofMap (values : Map<Symbol, Array>) =
        values |> Map.map (fun _ v -> BufferView.ofArray v) |> AttributeProvider.ofMap

    static member ofList (values : list<Symbol * Array>) =
        values |> List.map (fun (k,v) -> k,BufferView.ofArray v) |> AttributeProvider.ofList

    static member ofSeq (values : seq<Symbol * Array>) =
        values |> Seq.map (fun (k,v) -> k,BufferView.ofArray v) |> AttributeProvider.ofSeq

        
    // string / BufferView
    static member ofDict (values : Dict<string, BufferView>) =
        let d = SymbolDict<BufferView>()
        for (KeyValue(k,v)) in values do d.[Symbol.Create k] <- v
        AttributeProvider.ofDict d

    static member ofDict (values : System.Collections.Generic.Dictionary<string, BufferView>) =
        let d = SymbolDict<BufferView>()
        for (KeyValue(k,v)) in values do d.[Symbol.Create k] <- v
        AttributeProvider.ofDict d

    static member ofMap (values : Map<string, BufferView>) =
        let d = SymbolDict<BufferView>()
        for (KeyValue(k,v)) in values do d.[Symbol.Create k] <- v
        AttributeProvider.ofDict d

    static member ofSeq (values : seq<string * BufferView>) =
        let d = SymbolDict<BufferView>()
        for (k,v) in values do d.[Symbol.Create k] <- v
        AttributeProvider.ofDict d

    static member ofList (values : seq<string * BufferView>) =
        AttributeProvider.ofSeq values


    // string / Array
    static member ofDict (values : Dict<string, Array>) =
        let d = SymbolDict<BufferView>()
        for (KeyValue(k,v)) in values do d.[Symbol.Create k] <- BufferView.ofArray v
        AttributeProvider.ofDict d

    static member ofDict (values : System.Collections.Generic.Dictionary<string, Array>) =
        let d = SymbolDict<BufferView>()
        for (KeyValue(k,v)) in values do d.[Symbol.Create k] <- BufferView.ofArray v
        AttributeProvider.ofDict d

    static member ofMap (values : Map<string, Array>) =
        let d = SymbolDict<BufferView>()
        for (KeyValue(k,v)) in values do d.[Symbol.Create k] <- BufferView.ofArray v
        AttributeProvider.ofDict d

    static member ofSeq (values : seq<string * Array>) =
        let d = SymbolDict<BufferView>()
        for (k,v) in values do d.[Symbol.Create k] <- BufferView.ofArray v
        AttributeProvider.ofDict d

    static member ofList (values : seq<string * Array>) =
        AttributeProvider.ofSeq values

    // special
    static member ofIndexedGeometry (g : IndexedGeometry) =
        AttributeProvider.ofDict g.IndexedAttributes

type UniformProvider private() =

    static let empty = 
        { new IUniformProvider with
            member x.Dispose() = ()
            member x.TryGetUniform(_,_) = None
        }

    static member Empty = empty
    
    static member union (l : IUniformProvider) (r : IUniformProvider) =
        { new IUniformProvider with
            member x.Dispose() = l.Dispose(); r.Dispose()
            member x.TryGetUniform(scope : Ag.Scope, name : Symbol) =
                match l.TryGetUniform(scope, name) with
                    | Some m -> Some m
                    | None -> r.TryGetUniform(scope, name)
            
        }

    static member ofDict (values : SymbolDict<IMod>) =
        { new IUniformProvider with
            member x.Dispose() = ()
            member x.TryGetUniform(scope : Ag.Scope, name : Symbol) =
                match values.TryGetValue name with
                    | (true, v) -> Some v
                    | _ -> None
        }

    static member ofMap (values : Map<Symbol, IMod>) =
        { new IUniformProvider with
            member x.Dispose() = ()
            member x.TryGetUniform(scope : Ag.Scope, name : Symbol) = Map.tryFind name values
        }

    static member ofList (values : list<Symbol * IMod>) =
        values |> Map.ofList |> UniformProvider.ofMap

    static member ofSeq (values : seq<Symbol * IMod>) =
        values |> Map.ofSeq |> UniformProvider.ofMap

    
    static member ofDict (values : Dict<string, IMod>) =
        let d = SymbolDict<IMod>()
        for (KeyValue(k,v)) in values do d.[Symbol.Create k] <- v
        UniformProvider.ofDict d
    
    static member ofDict (values : System.Collections.Generic.Dictionary<string, IMod>) =
        let d = SymbolDict<IMod>()
        for (KeyValue(k,v)) in values do d.[Symbol.Create k] <- v
        UniformProvider.ofDict d

    static member ofMap (values : Map<string, IMod>) =
        let d = SymbolDict<IMod>()
        for (KeyValue(k,v)) in values do d.[Symbol.Create k] <- v
        UniformProvider.ofDict d

    static member ofList (values : list<string * IMod>) =
        UniformProvider.ofSeq values

    static member ofSeq (values : seq<string * IMod>) =
        let d = SymbolDict<IMod>()
        for (k,v) in values do d.[Symbol.Create k] <- v
        UniformProvider.ofDict d



[<AutoOpen>]
module private RenderObjectHelpers =
    let private nopDisposable = { new IDisposable with member x.Dispose() = () }
    let nopActivate () = nopDisposable

type IRenderObject =
    abstract member Id : int
    abstract member RenderPass : RenderPass
    abstract member AttributeScope : Ag.Scope

[<RequireQualifiedAccess>]
type Surface =
    | FShadeSimple of FShade.Effect
    | FShade of (FShade.EffectConfig -> FShade.EffectInputLayout * IMod<FShade.Imperative.Module>)
    | Backend of ISurface
    | None


[<CustomEquality>]
[<CustomComparison>]
type RenderObject =
    {
        Id : int
        mutable AttributeScope : Ag.Scope
                
        mutable IsActive            : IMod<bool>
        mutable RenderPass          : RenderPass
                
        mutable DrawCallInfos       : IMod<list<DrawCallInfo>>
        mutable IndirectBuffer      : IMod<IIndirectBuffer>
        mutable Mode                : IndexedGeometryMode
        

        mutable Surface             : Surface
                              
        mutable DepthTest           : IMod<DepthTestMode>
        mutable DepthBias           : IMod<DepthBiasState>
        mutable CullMode            : IMod<CullMode>
        mutable FrontFace           : IMod<WindingOrder>
        mutable BlendMode           : IMod<BlendMode>
        mutable FillMode            : IMod<FillMode>
        mutable StencilMode         : IMod<StencilMode>
                
        mutable Indices             : Option<BufferView>
        mutable InstanceAttributes  : IAttributeProvider
        mutable VertexAttributes    : IAttributeProvider
                
        mutable Uniforms            : IUniformProvider

        mutable ConservativeRaster  : IMod<bool>
        mutable Multisample         : IMod<bool>

        mutable Activate            : unit -> IDisposable
        mutable WriteBuffers        : Option<Set<Symbol>>

    }  
    interface IRenderObject with
        member x.Id = x.Id
        member x.RenderPass = x.RenderPass
        member x.AttributeScope = x.AttributeScope

    member x.Path = 
        if System.Object.ReferenceEquals(x.AttributeScope,Ag.emptyScope) 
        then "EMPTY" 
        else x.AttributeScope.Path

    static member Create() =
        { Id = newId()
          AttributeScope = Ag.emptyScope
          IsActive = null
          RenderPass = RenderPass.main
          DrawCallInfos = null
          IndirectBuffer = null

          Mode = IndexedGeometryMode.TriangleList
          Surface = Surface.None
          DepthTest = null
          DepthBias = null
          CullMode = null
          FrontFace = null
          BlendMode = null
          FillMode = null
          StencilMode = null
          Indices = None
          InstanceAttributes = null
          VertexAttributes = null
          Uniforms = null
          ConservativeRaster = null
          Multisample = null
          Activate = nopActivate
          WriteBuffers = None
        }

    static member Clone(org : RenderObject) =
        { org with Id = newId() }

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
          RenderPass = RenderPass.main
          DrawCallInfos = null
          IndirectBuffer = null
          Mode = IndexedGeometryMode.TriangleList
          Surface = Surface.None
          DepthTest = null
          DepthBias = null
          CullMode = null
          FrontFace = null
          BlendMode = null
          FillMode = null
          StencilMode = null
          Indices = None
          ConservativeRaster = null
          Multisample = null
          InstanceAttributes = emptyAttributes
          VertexAttributes = emptyAttributes
          Uniforms = emptyUniforms
          Activate = nopActivate
          WriteBuffers = None
        }


    type RenderObject with
        static member Empty =
            empty

        member x.IsValid =
            x.Id >= 0


type MultiRenderObject(children : list<IRenderObject>) =
    let first = 
        lazy ( 
            match children with
                | [] -> failwith "[MultiRenderObject] cannot be empty"
                | h::_ -> h
        )

    member x.Children = children

    interface IRenderObject with
        member x.Id = first.Value.Id
        member x.RenderPass = first.Value.RenderPass
        member x.AttributeScope = first.Value.AttributeScope

    override x.GetHashCode() = children.GetHashCode()
    override x.Equals o =
        match o with
            | :? MultiRenderObject as o -> children.Equals(o.Children)
            | _ -> false


type PipelineState =
    {
        depthTest           : IMod<DepthTestMode>
        depthBias           : IMod<DepthBiasState>
        cullMode            : IMod<CullMode>
        frontFace           : IMod<WindingOrder>
        blendMode           : IMod<BlendMode>
        fillMode            : IMod<FillMode>
        stencilMode         : IMod<StencilMode>
        multisample         : IMod<bool>
        writeBuffers        : Option<Set<Symbol>>
        globalUniforms      : IUniformProvider

        geometryMode        : IndexedGeometryMode
        vertexInputTypes    : Map<Symbol, Type>
        perGeometryUniforms : Map<string, Type>
    }
   
[<ReferenceEquality>]
type Geometry =
    {
        vertexAttributes    : Map<Symbol, IMod<IBuffer>>
        indices             : Option<Aardvark.Base.BufferView>
        uniforms            : Map<string, IMod>
        call                : IMod<list<DrawCallInfo>>
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Geometry =
    let ofIndexedGeometry (uniforms : Map<string, IMod>) (g : IndexedGeometry) =
        let index, fvc = 
            match g.IndexArray with
                | null -> 
                    let anyAtt = g.IndexedAttributes.Values |> Seq.head
                    None, anyAtt.Length
                | index -> 
                    let buffer = Mod.constant (ArrayBuffer(index) :> IBuffer)
                    let view = BufferView(buffer, index.GetType().GetElementType())

                    Some view, index.Length

        let attributes = 
            g.IndexedAttributes |> SymDict.toMap |> Map.map (fun name att ->
                let buffer = Mod.constant (ArrayBuffer(att) :> IBuffer)
                buffer
            )

        let gUniforms =
            if isNull g.SingleAttributes then
                Map.empty
            else
                let tc = typedefof<ConstantMod<_>>
                g.SingleAttributes |> SymDict.toSeq |> Seq.choose (fun (name, value) ->
                    try
                        let vt = value.GetType()
                        let t = tc.MakeGenericType(vt)
                        let ctor = t.GetConstructor [| vt |]
                        Some (name.ToString(), ctor.Invoke([| value |]) |> unbox<IMod>)
                    with _ ->
                        None
                )
                |> Map.ofSeq

        let call =
            DrawCallInfo(FaceVertexCount = fvc, InstanceCount = 1)
            
        {
            vertexAttributes    = attributes
            indices             = index
            uniforms            = Map.union gUniforms uniforms
            call                = Mod.constant [call]
        }





[<RequireQualifiedAccess>]
type RuntimeCommand =
    | EmptyCmd
    | RenderCmd of objects : aset<IRenderObject>
    | OrderedCmd of commands : alist<RuntimeCommand>
    | IfThenElseCmd of condition : IMod<bool> * ifTrue : RuntimeCommand * ifFalse : RuntimeCommand
    | ClearCmd of colors : Map<Symbol, IMod<C4f>> * depth : Option<IMod<float>> * stencil : Option<IMod<uint32>>
    

    | DispatchCmd of shader : IComputeShader * groups : IMod<V3i> * arguments : Map<string, obj>
    | GeometriesCmd of surface : Surface * pipeline : PipelineState * geometries : aset<Geometry>
    | LodTreeCmd of surface : Surface * pipeline : PipelineState * geometries : LodTreeLoader<Geometry>
    | GeometriesSimpleCmd of effect : FShade.Effect * pipeline : PipelineState * geometries : aset<IndexedGeometry>

    static member Empty = RuntimeCommand.EmptyCmd
    

    static member Render(objects : aset<IRenderObject>) =
        RuntimeCommand.RenderCmd(objects)
        
    static member Dispatch(shader : IComputeShader, groups : IMod<V3i>, arguments : Map<string, obj>) =
        RuntimeCommand.DispatchCmd(shader, groups, arguments)
        
    static member Clear(colors : Map<Symbol, IMod<C4f>>, depth : Option<IMod<float>>, stencil : Option<IMod<uint32>>) =
        RuntimeCommand.ClearCmd(colors, depth, stencil)

    static member Ordered(commands : alist<RuntimeCommand>) =
        RuntimeCommand.OrderedCmd(commands)

    static member IfThenElse(condition : IMod<bool>, ifTrue : RuntimeCommand, ifFalse : RuntimeCommand) =
        RuntimeCommand.IfThenElseCmd(condition, ifTrue, ifFalse)

    static member Geometries(surface : Surface, pipeline : PipelineState, geometries : aset<Geometry>) =
        RuntimeCommand.GeometriesCmd(surface, pipeline, geometries)
        
    static member Geometries(surface : FShade.Effect, pipeline : PipelineState, geometries : aset<IndexedGeometry>) =
        RuntimeCommand.GeometriesSimpleCmd(surface, pipeline, geometries)

    static member Geometries(effects : FShade.Effect[], activeEffect : IMod<int>, pipeline : PipelineState, geometries : aset<Geometry>) =
        let surface = 
            Surface.FShade (fun cfg ->
                let modules = effects |> Array.map (FShade.Effect.toModule cfg)
                let signature = FShade.EffectInputLayout.ofModules modules
                let modules = modules |> Array.map (FShade.EffectInputLayout.apply signature)

                signature, activeEffect |> Mod.map (Array.get modules)
            )
        RuntimeCommand.GeometriesCmd(surface, pipeline, geometries)

    static member LodTree(surface : Surface, pipeline : PipelineState, geometries : LodTreeLoader<Geometry>) =
        RuntimeCommand.LodTreeCmd(surface, pipeline, geometries)

type CommandRenderObject(pass : RenderPass, scope : Ag.Scope, command : RuntimeCommand) =
    let id = newId()

    member x.Id = id
    member x.RenderPass = pass
    member x.AttributeScope = scope
    member x.Command = command
    
    interface IRenderObject with
        member x.Id = id
        member x.RenderPass = pass
        member x.AttributeScope = scope


    override x.GetHashCode() = id.GetHashCode()
    override x.Equals o =
        match o with
            | :? CommandRenderObject as o -> id = o.Id
            | _ -> false


type IAdaptiveBufferReader =
    inherit IAdaptiveObject
    inherit IDisposable
    abstract member GetDirtyRanges : AdaptiveToken -> INativeBuffer * RangeSet

type IAdaptiveBuffer =
    inherit IMod<IBuffer>
    abstract member GetReader : unit -> IAdaptiveBufferReader


type EffectDebugger private() =
    
    static let rec hookObject (o : IRenderObject) =
        match o with
            | :? RenderObject as o ->
                match o.Surface with
                    | Surface.FShadeSimple e ->
                        match FShade.EffectDebugger.register e with
                            | Some (:? IMod<FShade.Effect> as e) ->
                                e |> Mod.map (fun e -> { o with Id = newId(); Surface = Surface.FShadeSimple e } :> IRenderObject)
                            | _ ->
                                Mod.constant (o :> IRenderObject)
                    | _ ->
                        Mod.constant (o :> IRenderObject)

            | :? MultiRenderObject as m ->
                let mods = m.Children |> List.map hookObject

                if mods |> List.forall (fun m -> m.IsConstant) then
                    Mod.constant (m :> IRenderObject)
                else
                    mods |> Mod.mapN (fun ros -> MultiRenderObject(Seq.toList ros) :> IRenderObject)

            | _ ->
                Mod.constant o

    static member Hook (o : IRenderObject) = hookObject o
    static member Hook (set : aset<IRenderObject>) =
        match FShade.EffectDebugger.registerFun with
            | Some _ ->
                set |> ASet.mapM EffectDebugger.Hook
            | None ->
                set


module AttributePackingV2 =
    open System
    open System.Threading
    open System.Collections.Generic
    open System.Runtime.InteropServices
        
    type private AdaptiveBufferReader(b : IAdaptiveBuffer, remove : AdaptiveBufferReader -> unit) =
        inherit AdaptiveObject()

        let mutable realCapacity = 0
        let mutable dirtyCapacity = -1
        let mutable dirtyRanges = RangeSet.empty

        member x.AddDirty(r : Range1i) =
            if dirtyCapacity = realCapacity then
                Interlocked.Change(&dirtyRanges, RangeSet.insert r) |> ignore

        member x.RemoveDirty(r : Range1i) =
            if dirtyCapacity = realCapacity then
                Interlocked.Change(&dirtyRanges, RangeSet.remove r) |> ignore
            
        member x.Resize(cap : int) =
            if cap <> realCapacity then
                realCapacity <- cap
                dirtyRanges <- RangeSet.empty

        member x.GetDirtyRanges(token : AdaptiveToken) =
            x.EvaluateAlways token (fun token ->
                let buffer = b.GetValue token :?> INativeBuffer
                let dirtyCap = Interlocked.Exchange(&dirtyCapacity, buffer.SizeInBytes)

                if dirtyCap <> buffer.SizeInBytes then
                    buffer, RangeSet.ofList [Range1i(0, buffer.SizeInBytes-1)]
                else
                    let dirty = Interlocked.Exchange(&dirtyRanges, RangeSet.empty)
                    buffer, dirty
            )

        member x.Dispose() =
            if Interlocked.Exchange(&realCapacity, -1) >= 0 then
                b.RemoveOutput x
                remove x
                dirtyRanges <- RangeSet.empty
                dirtyCapacity <- -1

        interface IDisposable with
            member x.Dispose() = x.Dispose()

        interface IAdaptiveBufferReader with
            member x.GetDirtyRanges(token) = x.GetDirtyRanges(token)

    type private IndexFlat<'b when 'b : unmanaged>() =
        static member Copy(index : Array, src : nativeint, dst : nativeint) =
            let src = NativePtr.ofNativeInt<'b> src
            let mutable dst = dst

            match index with
                | :? array<int> as arr -> 
                    for i in arr do
                        NativePtr.write (NativePtr.ofNativeInt dst) (NativePtr.get src i)
                        dst <- dst + 4n
                | :? array<int16> as arr -> 
                    for i in arr do
                        NativePtr.write (NativePtr.ofNativeInt dst) (NativePtr.get src (int i))
                        dst <- dst + 2n
                | :? array<int8> as arr -> 
                    for i in arr do
                        NativePtr.write (NativePtr.ofNativeInt dst) (NativePtr.get src (int i))
                        dst <- dst + 1n
                | _ ->
                    failwith ""

    let private dictCache = System.Collections.Concurrent.ConcurrentDictionary<Type, Action<Array, nativeint, nativeint>>()

    let private copyIndexed (dt : Type, index : Array, src : nativeint, dst : nativeint) =
        let action = 
            dictCache.GetOrAdd(dt, fun dt ->
                let flat = typedefof<IndexFlat<int>>.MakeGenericType [| dt |]
                let meth = flat.GetMethod("Copy", Reflection.BindingFlags.NonPublic ||| Reflection.BindingFlags.Static )
                meth.CreateDelegate(typeof<Action<Array, nativeint, nativeint>>) |> unbox<Action<Array, nativeint, nativeint>>
            )
        action.Invoke(index, src, dst)

    type private AdaptiveBuffer(sem : Symbol, elementType : Type, updateLayout : IMod<Dictionary<IndexedGeometry, managedptr>>) =
        inherit Mod.AbstractMod<IBuffer>()

        let readers = HashSet<AdaptiveBufferReader>()
        let elementSize = Marshal.SizeOf elementType
        let mutable parentCapacity = 0
        let mutable storeCapacity = 0
        let mutable storage = 0n
        let writes = Dictionary<IndexedGeometry, managedptr>()

        let faceVertexCount (i : IndexedGeometry) =
            match i.IndexArray with
             | null -> i.IndexedAttributes.Values |> Seq.head |> (fun s -> s.Length)
             | arr -> arr.Length


        let removeReader (r : AdaptiveBufferReader) =
            lock readers (fun () -> readers.Remove r |> ignore)

        let iter (f : AdaptiveBufferReader -> unit) =
            lock readers (fun () ->
                for r in readers do f r
            )

        let write (g : IndexedGeometry) (offset : nativeint) =
            match g.IndexedAttributes.TryGetValue sem with
                | (true, data) ->
                    let dt = data.GetType().GetElementType()

                    if dt = elementType then
                        let count = faceVertexCount g
                        let arraySize = elementSize * count

                        #if DEBUG
                        let available = max 0 (storeCapacity - int offset)
                        if available < arraySize then
                            failwithf "writing out of bounds: { available = %A; real = %A }" available arraySize
                        #endif

                        if isNull g.IndexArray then
                            let gc = GCHandle.Alloc(data, GCHandleType.Pinned)
                            try Marshal.Copy(gc.AddrOfPinnedObject(), storage + offset, arraySize)
                            finally gc.Free()
                
                        else
                            let gc = GCHandle.Alloc(data, GCHandleType.Pinned)
                            try copyIndexed(dt, g.IndexArray, gc.AddrOfPinnedObject(), storage + offset) 
                            finally gc.Free()


                        let range = Range1i.FromMinAndSize(int offset, arraySize - 1)
                        iter (fun r -> r.AddDirty range)
                    else
                        failwithf "unexpected input-type: %A" dt   
                | _ ->
                    let count = faceVertexCount g
                    let clearSize = elementSize * count

                    // TODO: maybe respect NullBuffers here
                    Marshal.Set(storage + offset, 0, clearSize)

                    let range = Range1i.FromMinAndSize(int offset, clearSize - 1)
                    iter (fun r -> r.AddDirty range)

        member x.GetReader() =
            let r = new AdaptiveBufferReader(x, removeReader)
            lock readers (fun () -> readers.Add r |> ignore)
            r :> IAdaptiveBufferReader


        /// may only be called in layout-update
        member x.Resize(count : int) =
            lock writes (fun () ->
                let capacity = count * elementSize
                if parentCapacity <> capacity then
                    parentCapacity <- capacity
                    writes.Clear()
                    
            )

        /// may only be called in layout-update
        member x.Write(ig : IndexedGeometry, ptr : managedptr) =
            if storeCapacity = parentCapacity && storage <> 0n then
                lock writes (fun () -> writes.[ig] <- ptr)

        /// may only be called in layout-update
        member x.Remove(ig : IndexedGeometry) =
            if storeCapacity = parentCapacity && storage <> 0n then
                lock writes (fun () -> writes.Remove ig |> ignore)

        override x.Compute(token) =
            let layout = updateLayout.GetValue token

            if parentCapacity <> storeCapacity || storage = 0n then
                storeCapacity <- parentCapacity
                iter (fun r -> r.Resize storeCapacity)

                if storage <> 0n then
                    storage <- Marshal.ReAllocHGlobal(storage, nativeint parentCapacity)
                else
                    storage <- Marshal.AllocHGlobal(parentCapacity)

                let all = lock layout (fun () -> Dictionary.toArray layout)
                for (g, ptr) in all do
                    let offset = ptr.Offset * nativeint elementSize
                    write g offset
            else
                let dirty = 
                    lock writes (fun () ->
                        let res = Dictionary.toArray writes
                        writes.Clear()
                        res
                    )

                for (g, ptr) in dirty do
                    let offset = ptr.Offset * nativeint elementSize
                    write g offset

            NativeMemoryBuffer(storage, storeCapacity) :> IBuffer

        member x.ElementType = elementType

        interface IAdaptiveBuffer with
            member x.GetReader() = x.GetReader()

    type Packer(set : aset<IndexedGeometry>, elementTypes : Map<Symbol, Type>) =
        inherit Mod.AbstractMod<Dictionary<IndexedGeometry, managedptr>>()
        let shrinkThreshold = 0.5
        
        let lockObj = obj()
        let reader = set.GetReader()
        let buffers = SymbolDict<AdaptiveBuffer>()
        let mutable manager = MemoryManager.createNop()
        let mutable dataRanges = Dictionary<IndexedGeometry, managedptr>()
        

        let mutable drawRanges = RangeSet.empty

        let faceVertexCount (i : IndexedGeometry) =
            match i.IndexArray with
             | null -> i.IndexedAttributes.Values |> Seq.head |> (fun s -> s.Length)
             | arr -> arr.Length


        let write (g : IndexedGeometry) (ptr : managedptr) =
            lock lockObj (fun () ->
                for (sem, b) in SymDict.toSeq buffers do
                    b.Resize(int manager.Capacity)
                    b.Write(g, ptr)
            )

        let remove (g : IndexedGeometry) =
            lock lockObj (fun () ->
                for (sem, b) in SymDict.toSeq buffers do
                    b.Resize(int manager.Capacity)
                    b.Remove(g)
            )   
    
        let resize () =
            lock lockObj (fun () ->
                for (sem, b) in SymDict.toSeq buffers do
                    b.Resize(int manager.Capacity)
            )

        member private x.CreateBuffer (elementType : Type, sem : Symbol) =
            AdaptiveBuffer(sem, elementType, x)

        member private x.TryGetBuffer (sem : Symbol) =
            lock lockObj (fun () ->
                match buffers.TryGetValue sem with
                    | (true, b) -> Some b
                    | _ ->
                        match Map.tryFind sem elementTypes with
                            | Some et ->
                                let b = x.CreateBuffer(et, sem)
                                b.Resize(int manager.Capacity)
                                buffers.[sem] <- b
                                Some b
                            | _ ->
                                None
            )

        override x.Compute(token) =
            let deltas = reader.GetOperations token

            for d in deltas do
                match d with
                    | Add(_, g) ->
                        let ptr = g |> faceVertexCount |> nativeint |> manager.Alloc
                        lock dataRanges (fun () -> dataRanges.[g] <- ptr)
                        write g ptr


                        let r = Range1i.FromMinAndSize(int ptr.Offset, int ptr.Size - 1)
                        drawRanges <- RangeSet.insert r drawRanges


                    | Rem(_, g) ->
                        match dataRanges.TryGetValue g with
                            | (true, ptr) ->
                                dataRanges.Remove g |> ignore

                                let r = Range1i.FromMinAndSize(int ptr.Offset, int ptr.Size - 1)
                                drawRanges <- RangeSet.remove r drawRanges

                                remove g
                                manager.Free ptr

//                                if float manager.AllocatedBytes < shrinkThreshold * float manager.Capacity then
//                                    let mutable newDrawRanges = RangeSet.empty
//                                    let newManager = MemoryManager.createNop()
//                                    let newRanges = Dictionary.empty
//
//                                    for (g,ptr) in Dictionary.toList dataRanges do
//                                        let nptr = newManager.Alloc(ptr.Size)
//                                        newRanges.[g] <- nptr
//                                        let r = Range1i.FromMinAndSize(int nptr.Offset, nptr.Size - 1)
//                                        newDrawRanges <- RangeSet.insert r newDrawRanges
//                                    
//                                    manager.Dispose()
//                                    manager <- newManager
//                                    drawRanges <- newDrawRanges
//                                    dataRanges <- newRanges
//                                    resize()



                            | _ ->
                                ()
                        ()


            dataRanges

        member x.Dispose() =
            ()

        member x.AttributeProvider =
            { new IAttributeProvider with
                member __.TryGetAttribute(sem : Symbol) =
                    match x.TryGetBuffer sem with
                        | Some b -> BufferView(b, b.ElementType) |> Some
                        | None -> None

                member __.Dispose() =
                    x.Dispose()

                member x.All = Seq.empty
            }

        member x.DrawCallInfos =
            Mod.custom (fun self ->
                x.GetValue self |> ignore

                drawRanges
                    |> Seq.toList
                    |> List.map (fun range ->
                        DrawCallInfo(
                            FirstIndex = range.Min,
                            FaceVertexCount = range.Size + 1,
                            FirstInstance = 0,
                            InstanceCount = 1,
                            BaseVertex = 0
                        )
                    )
            )

module GeometrySetUtilities =
    open System.Collections.Concurrent
    open System.Threading
    open System.Collections.Generic

    type private AdaptiveBufferReader(b : IAdaptiveBuffer, remove : AdaptiveBufferReader -> unit) =
        inherit AdaptiveObject()

        let mutable realCapacity = 0
        let mutable dirtyCapacity = -1
        let mutable dirtyRanges = RangeSet.empty

        member x.AddDirty(r : Range1i) =
            if dirtyCapacity = realCapacity then
                Interlocked.Change(&dirtyRanges, RangeSet.insert r) |> ignore

        member x.RemoveDirty(r : Range1i) =
            if dirtyCapacity = realCapacity then
                Interlocked.Change(&dirtyRanges, RangeSet.remove r) |> ignore
            
        member x.Resize(cap : nativeint) =
            let cap = int cap
            if cap <> realCapacity then
                realCapacity <- cap
                dirtyRanges <- RangeSet.empty

        member x.GetDirtyRanges(token : AdaptiveToken) =
            x.EvaluateAlways token (fun token ->
                let buffer = b.GetValue token :?> INativeBuffer
                let dirtyCap = Interlocked.Exchange(&dirtyCapacity, buffer.SizeInBytes)

                if dirtyCap <> buffer.SizeInBytes then
                    buffer, RangeSet.ofList [Range1i(0, buffer.SizeInBytes-1)]
                else
                    let dirty = Interlocked.Exchange(&dirtyRanges, RangeSet.empty)
                    buffer, dirty
            )

        member x.Dispose() =
            if Interlocked.Exchange(&realCapacity, -1) >= 0 then
                b.RemoveOutput x
                remove x
                dirtyRanges <- RangeSet.empty
                dirtyCapacity <- -1

        interface IDisposable with
            member x.Dispose() = x.Dispose()

        interface IAdaptiveBufferReader with
            member x.GetDirtyRanges(caller) = x.GetDirtyRanges(caller)

    type ChangeableBuffer(initialCapacity : int) =
        inherit Mod.AbstractMod<IBuffer>()

        let rw = new ReaderWriterLockSlim()
        let mutable capacity = nativeint initialCapacity
        let mutable storage = if initialCapacity > 0 then Marshal.AllocHGlobal(capacity) else 0n
        let readers = HashSet<AdaptiveBufferReader>()

        let removeReader(r : AdaptiveBufferReader) =
            lock readers (fun () -> readers.Remove r |> ignore)

        let addDirty (r : Range1i) =
            let all = lock readers (fun () -> readers |> HashSet.toArray)
            all |> Array.iter (fun reader -> 
                reader.Resize capacity
                reader.AddDirty r
            )
            
        member x.Capacity = capacity

        member x.Resize(newCapacity : nativeint) =
            let changed = 
                ReaderWriterLock.write rw (fun () ->
                    if newCapacity = 0n then
                        if storage <> 0n then
                            Marshal.FreeHGlobal(storage)
                            storage <- 0n
                            capacity <- 0n
                            true
                        else
                            false
                    elif newCapacity <> capacity then
                        if storage = 0n then
                            capacity <- newCapacity
                            storage <- Marshal.AllocHGlobal(capacity)
                        else
                            capacity <- newCapacity
                            storage <- Marshal.ReAllocHGlobal(storage, capacity)

                        addDirty (Range1i(0, int capacity - 1))
                        true
                    else
                        false
                )

            if changed then transact (fun () -> x.MarkOutdated())

        member x.Write(offset : int, data : nativeint, sizeInBytes : int) =
            ReaderWriterLock.read rw (fun () ->
                Marshal.Copy(data, storage + nativeint offset, sizeInBytes)
            )

            addDirty (Range1i.FromMinAndSize(offset, sizeInBytes - 1))
            transact (fun () -> x.MarkOutdated()) 

        member x.Write(offset : int, data : Array, sizeInBytes : int) =
            ReaderWriterLock.read rw (fun () ->
                let gc = GCHandle.Alloc(data, GCHandleType.Pinned)
                try Marshal.Copy(gc.AddrOfPinnedObject(), storage + nativeint offset, sizeInBytes)
                finally gc.Free()
            )

            addDirty (Range1i.FromMinAndSize(offset, sizeInBytes - 1))
            transact (fun () -> x.MarkOutdated())

        member x.Dispose() =
            x.Resize(0n)
            rw.Dispose()
            lock readers (fun () -> readers.Clear())

        override x.Compute(token) =
            ReaderWriterLock.read rw (fun () ->
                NativeMemoryBuffer(storage, int capacity) :> IBuffer
            )

        member private x.GetReader() =
            let r = new AdaptiveBufferReader(x, removeReader)
            lock readers (fun () -> readers.Add r |> ignore)
            r :> IAdaptiveBufferReader

        interface IAdaptiveBuffer with
            member x.GetReader() = x.GetReader()

        new() = ChangeableBuffer(0)

    type GeometryPacker(attributeTypes : Map<Symbol, Type>) =
        inherit Mod.AbstractMod<RangeSet>()

        let manager = MemoryManager.createNop()
        let locations = ConcurrentDictionary<IndexedGeometry, managedptr>()
        let mutable buffers = ConcurrentDictionary<Symbol, ChangeableBuffer>()
        let elementSizes = attributeTypes |> Map.map (fun _ v -> nativeint(Marshal.SizeOf v)) |> Map.toSeq |> Dictionary.ofSeq
        let mutable ranges = RangeSet.empty


        let getElementSize (sem : Symbol) =
            elementSizes.[sem]

        let writeAttribute (sem : Symbol) (region : managedptr) (buffer : ChangeableBuffer) (source : IndexedGeometry) =
            match source.IndexedAttributes.TryGetValue(sem) with
                | (true, arr) ->
                    let elementSize = getElementSize sem
                    let cap = elementSize * (nativeint manager.Capacity)

                    if buffer.Capacity <> cap then 
                        buffer.Resize(cap)

                    buffer.Write(int (region.Offset * elementSize), arr, int region.Size * int elementSize)
                | _ ->
                    // TODO: write NullBuffer content or 0 here
                    ()

        let getBuffer (sem : Symbol) =
            let mutable isNew = false
            let result = 
                buffers.GetOrAdd(sem, fun sem ->
                    isNew <- true
                    let elementSize = getElementSize sem |> int
                    let b = ChangeableBuffer(elementSize * int manager.Capacity)
                    b
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
            getBuffer sem  :> IMod<IBuffer>

        member x.Dispose() =
            let old = Interlocked.Exchange(&buffers, ConcurrentDictionary())
            if old.Count > 0 then
                old.Values |> Seq.iter (fun b -> b.Dispose())
                old.Clear()

        override x.Compute(token) =
            //printfn "%A" ranges
            ranges

        
    


