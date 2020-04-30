namespace Aardvark.Base

open System
open FSharp.Data.Adaptive
open System.Runtime.InteropServices
open Aardvark.Base.Rendering
open Microsoft.FSharp.NativeInterop

#nowarn "9"

[<AutoOpen>]
module private RenderObjectHelpers =
    let private nopDisposable = { new IDisposable with member x.Dispose() = () }
    let nopActivate () = nopDisposable

type IRenderObject =
    abstract member Id : int
    abstract member RenderPass : RenderPass
    abstract member AttributeScope : Ag.Scope

[<CustomEquality>]
[<CustomComparison>]
type RenderObject =
    {
        Id : int
        mutable AttributeScope : Ag.Scope

        mutable IsActive            : aval<bool>
        mutable RenderPass          : RenderPass

        mutable DrawCalls           : DrawCalls
        mutable Mode                : IndexedGeometryMode


        mutable Surface             : Surface

        mutable DepthTest           : aval<DepthTestMode>
        mutable DepthBias           : aval<DepthBiasState>
        mutable CullMode            : aval<CullMode>
        mutable FrontFace           : aval<WindingOrder>
        mutable BlendMode           : aval<BlendMode>
        mutable FillMode            : aval<FillMode>
        mutable StencilMode         : aval<StencilMode>

        mutable Indices             : Option<BufferView>
        mutable InstanceAttributes  : IAttributeProvider
        mutable VertexAttributes    : IAttributeProvider

        mutable Uniforms            : IUniformProvider

        mutable ConservativeRaster  : aval<bool>
        mutable Multisample         : aval<bool>

        mutable Activate            : unit -> IDisposable
        mutable WriteBuffers        : Option<Set<Symbol>>

    }
    interface IRenderObject with
        member x.Id = x.Id
        member x.RenderPass = x.RenderPass
        member x.AttributeScope = x.AttributeScope

    member x.Path =
        if System.Object.ReferenceEquals(x.AttributeScope,Ag.Scope.Root)
        then "EMPTY"
        else string x.AttributeScope

    static member Create() =
        { Id = newId()
          AttributeScope = Ag.Scope.Root
          IsActive = Unchecked.defaultof<_>
          RenderPass = RenderPass.main
          DrawCalls = Unchecked.defaultof<_>

          Mode = IndexedGeometryMode.TriangleList
          Surface = Surface.None
          DepthTest = Unchecked.defaultof<_>
          DepthBias = Unchecked.defaultof<_>
          CullMode = Unchecked.defaultof<_>
          FrontFace = Unchecked.defaultof<_>
          BlendMode = Unchecked.defaultof<_>
          FillMode = Unchecked.defaultof<_>
          StencilMode = Unchecked.defaultof<_>
          Indices = None
          InstanceAttributes = Unchecked.defaultof<_>
          VertexAttributes = Unchecked.defaultof<_>
          Uniforms = Unchecked.defaultof<_>
          ConservativeRaster = Unchecked.defaultof<_>
          Multisample = Unchecked.defaultof<_>
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
          AttributeScope = Ag.Scope.Root
          IsActive = Unchecked.defaultof<_>
          RenderPass = RenderPass.main
          DrawCalls = Unchecked.defaultof<_>
          Mode = IndexedGeometryMode.TriangleList
          Surface = Surface.None
          DepthTest = Unchecked.defaultof<_>
          DepthBias = Unchecked.defaultof<_>
          CullMode = Unchecked.defaultof<_>
          FrontFace = Unchecked.defaultof<_>
          BlendMode = Unchecked.defaultof<_>
          FillMode = Unchecked.defaultof<_>
          StencilMode = Unchecked.defaultof<_>
          Indices = None
          ConservativeRaster = Unchecked.defaultof<_>
          Multisample = Unchecked.defaultof<_>
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
        depthTest           : aval<DepthTestMode>
        depthBias           : aval<DepthBiasState>
        cullMode            : aval<CullMode>
        frontFace           : aval<WindingOrder>
        blendMode           : aval<BlendMode>
        fillMode            : aval<FillMode>
        stencilMode         : aval<StencilMode>
        multisample         : aval<bool>
        writeBuffers        : Option<Set<Symbol>>
        globalUniforms      : IUniformProvider

        geometryMode        : IndexedGeometryMode
        vertexInputTypes    : Map<Symbol, Type>
        perGeometryUniforms : Map<string, Type>
    }

[<ReferenceEquality>]
type Geometry =
    {
        vertexAttributes    : Map<Symbol, aval<IBuffer>>
        indices             : Option<Aardvark.Base.BufferView>
        uniforms            : Map<string, IAdaptiveValue>
        call                : aval<list<DrawCallInfo>>
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Geometry =
    let ofIndexedGeometry (uniforms : Map<string, IAdaptiveValue>) (g : IndexedGeometry) =
        let index, fvc =
            match g.IndexArray with
                | null ->
                    let anyAtt = g.IndexedAttributes.Values |> Seq.head
                    None, anyAtt.Length
                | index ->
                    let buffer = AVal.constant (ArrayBuffer(index) :> IBuffer)
                    let view = BufferView(buffer, index.GetType().GetElementType())

                    Some view, index.Length

        let attributes =
            g.IndexedAttributes |> SymDict.toMap |> Map.map (fun name att ->
                let buffer = AVal.constant (ArrayBuffer(att) :> IBuffer)
                buffer
            )

        let gUniforms =
            if isNull g.SingleAttributes then
                Map.empty
            else
                let tc = typedefof<ConstantVal<_>>
                g.SingleAttributes |> SymDict.toSeq |> Seq.choose (fun (name, value) ->
                    try
                        let vt = value.GetType()
                        let t = tc.MakeGenericType(vt)
                        let ctor = t.GetConstructor [| vt |]
                        Some (name.ToString(), ctor.Invoke([| value |]) |> unbox<IAdaptiveValue>)
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
            call                = AVal.constant [call]
        }


[<RequireQualifiedAccess>]
type RuntimeCommand =
    | EmptyCmd
    | RenderCmd of objects : aset<IRenderObject>
    | OrderedCmd of commands : alist<RuntimeCommand>
    | IfThenElseCmd of condition : aval<bool> * ifTrue : RuntimeCommand * ifFalse : RuntimeCommand
    | ClearCmd of colors : Map<Symbol, aval<C4f>> * depth : Option<aval<float>> * stencil : Option<aval<uint32>>


    | DispatchCmd of shader : IComputeShader * groups : aval<V3i> * arguments : Map<string, obj>
    | GeometriesCmd of surface : Surface * pipeline : PipelineState * geometries : aset<Geometry>
    | LodTreeCmd of surface : Surface * pipeline : PipelineState * geometries : LodTreeLoader<Geometry>
    | GeometriesSimpleCmd of effect : FShade.Effect * pipeline : PipelineState * geometries : aset<IndexedGeometry>

    static member Empty = RuntimeCommand.EmptyCmd

    static member Render(objects : aset<IRenderObject>) =
        RuntimeCommand.RenderCmd(objects)

    static member Dispatch(shader : IComputeShader, groups : aval<V3i>, arguments : Map<string, obj>) =
        RuntimeCommand.DispatchCmd(shader, groups, arguments)

    static member Clear(colors : Map<Symbol, aval<C4f>>, depth : Option<aval<float>>, stencil : Option<aval<uint32>>) =
        RuntimeCommand.ClearCmd(colors, depth, stencil)

    static member Ordered(commands : alist<RuntimeCommand>) =
        RuntimeCommand.OrderedCmd(commands)

    static member IfThenElse(condition : aval<bool>, ifTrue : RuntimeCommand, ifFalse : RuntimeCommand) =
        RuntimeCommand.IfThenElseCmd(condition, ifTrue, ifFalse)

    static member Geometries(surface : Surface, pipeline : PipelineState, geometries : aset<Geometry>) =
        RuntimeCommand.GeometriesCmd(surface, pipeline, geometries)

    static member Geometries(surface : FShade.Effect, pipeline : PipelineState, geometries : aset<IndexedGeometry>) =
        RuntimeCommand.GeometriesSimpleCmd(surface, pipeline, geometries)

    static member Geometries(effects : FShade.Effect[], activeEffect : aval<int>, pipeline : PipelineState, geometries : aset<Geometry>) =
        let surface =
            Surface.FShade (fun cfg ->
                let modules = effects |> Array.map (FShade.Effect.toModule cfg)
                let signature = FShade.EffectInputLayout.ofModules modules
                let modules = modules |> Array.map (FShade.EffectInputLayout.apply signature)

                signature, activeEffect |> AVal.map (Array.get modules)
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

type EffectDebugger private() =

    static let rec hookObject (o : IRenderObject) =
        match o with
            | :? RenderObject as o ->
                match o.Surface with
                    | Surface.FShadeSimple e ->
                        match FShade.EffectDebugger.register e with
                            | Some (:? aval<FShade.Effect> as e) ->
                                e |> AVal.map (fun e -> { o with Id = newId(); Surface = Surface.FShadeSimple e } :> IRenderObject)
                            | _ ->
                                AVal.constant (o :> IRenderObject)
                    | _ ->
                        AVal.constant (o :> IRenderObject)

            | :? MultiRenderObject as m ->
                let mods = m.Children |> List.map hookObject

                if mods |> List.forall (fun m -> m.IsConstant) then
                    AVal.constant (m :> IRenderObject)
                else
                    AVal.custom (fun t ->
                        mods |> List.map (fun m -> m.GetValue t) |> MultiRenderObject :> IRenderObject
                    )

            | _ ->
                AVal.constant o

    static member Hook (o : IRenderObject) = hookObject o
    static member Hook (set : aset<IRenderObject>) =
        match FShade.EffectDebugger.registerFun with
            | Some _ ->
                set |> ASet.mapA EffectDebugger.Hook
            | None ->
                set

module GeometrySetUtilities =
    open System.Collections.Concurrent
    open System.Threading
    open System.Collections.Generic

    type GeometryPacker(attributeTypes : Map<Symbol, Type>) =
        inherit AVal.AbstractVal<RangeSet>()

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
            getBuffer sem  :> aval<IBuffer>

        member x.Dispose() =
            let old = Interlocked.Exchange(&buffers, ConcurrentDictionary())
            if old.Count > 0 then
                old.Values |> Seq.iter (fun b -> b.Dispose())
                old.Clear()

        override x.Compute(token) =
            //printfn "%A" ranges
            ranges