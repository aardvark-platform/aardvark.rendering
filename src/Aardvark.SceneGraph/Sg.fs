namespace Aardvark.SceneGraph

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Ag
open Aardvark.Base.AgHelpers
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Aardvark.Base.Rendering

type ISg = 
    interface end

type IApplicator =
    inherit ISg
    abstract member Child : IMod<ISg>

type IEventUniformHolder =
    abstract member TryGetUniform : Scope * Symbol * [<Out>] result : byref<IEvent> -> bool

type IUniformHolder =
    abstract member TryFindUniform : Scope -> Symbol -> Option<IMod>

module private Uniforms =
    type EventUniformAdapter(e : IEventUniformHolder) =
        interface IUniformHolder with
            member x.TryFindUniform scope name =
                match e.TryGetUniform (scope, name) with
                    | (true,e) -> e.Mod |> Some
                    | _ -> None

    type SimpleUniformHolder(values : Map<Symbol, IMod>) =
        interface IUniformHolder with
            member x.TryFindUniform scope name = Map.tryFind name values

        new (l : list<Symbol * IMod>) = SimpleUniformHolder(Map.ofList l)

    type ScopeDependentUniformHolder(values : Map<Symbol, Scope -> IMod>) =
        let cache = Dictionary<Scope * Symbol, Option<IMod>>()

        interface IUniformHolder with
            member x.TryFindUniform scope name = 
                match cache.TryGetValue((scope,name)) with
                    | (true, v) -> v
                    | _ ->
                        let v =
                            match Map.tryFind name values with
                                | Some f -> f scope |> Some
                                | None -> None
                        cache.[(scope,name)] <- v
                        v

        new(l) = ScopeDependentUniformHolder(Map.ofList l)

    type SimpleEventUniformHolder(values : Dictionary<Symbol, IEvent>) =
        interface IEventUniformHolder with
            member x.TryGetUniform(scope : Scope, name : Symbol, result : byref<IEvent>) = 
                match values.TryGetValue name with
                    | (true, v) -> 
                        result <- v
                        true
                    | _ ->
                        false

module Sg =

    [<AbstractClass>]
    type AbstractApplicator(child : IMod<ISg>) =
        interface IApplicator with
            member x.Child = child

        member x.Child = child

        new(child : ISg) = AbstractApplicator(Mod.initConstant child)

    type AdapterNode(node : obj) =
        interface ISg

        member x.Node = node



    type RenderNode(call : IMod<DrawCallInfo>) =
        interface ISg

        member x.DrawCallInfo = call

        new(call : IEvent<DrawCallInfo>) = RenderNode(Mod.fromEvent call)
        new(call : DrawCallInfo) = RenderNode(Mod.initConstant call)
    
    type VertexAttributeApplicator(values : Map<Symbol, BufferView>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Values = values

        new(values : Map<Symbol, BufferView>, child : ISg) = VertexAttributeApplicator(values, Mod.initConstant child)
        new(semantic : Symbol, value : BufferView, child : IMod<ISg>) = VertexAttributeApplicator(Map.ofList [semantic, value], child)
        new(semantic : Symbol, value : BufferView, child : ISg) = VertexAttributeApplicator(Map.ofList [semantic, value], Mod.initConstant child)
        new(values : SymbolDict<BufferView>, child : ISg) = VertexAttributeApplicator(values |> Seq.map (fun (KeyValue(k,v)) -> k,v) |> Map.ofSeq, Mod.initConstant child)

    type VertexIndexApplicator(value : IMod<Array>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Value = value

        new(value : IMod<Array>, child : ISg) = VertexIndexApplicator(value, Mod.initConstant child)
        new(value : IEvent<Array>, child : IMod<ISg>) = VertexIndexApplicator(Mod.fromEvent value, child)
        new(value : IEvent<Array>, child : ISg) = VertexIndexApplicator(Mod.fromEvent value, Mod.initConstant child)

    type InstanceAttributeApplicator(values : Map<Symbol, BufferView>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Values = values

        new(values : Map<Symbol, BufferView>, child : ISg) = InstanceAttributeApplicator(values, Mod.initConstant child)
        new(semantic : Symbol, value : BufferView, child : IMod<ISg>) = InstanceAttributeApplicator(Map.ofList [semantic, value], child)
        new(semantic : Symbol, value : BufferView, child : ISg) = InstanceAttributeApplicator(Map.ofList [semantic, value], Mod.initConstant child)
        new(values : SymbolDict<BufferView>, child : ISg) = InstanceAttributeApplicator(values |> Seq.map (fun (KeyValue(k,v)) -> k,v) |> Map.ofSeq, Mod.initConstant child)
 

    type OnOffNode(on : IMod<bool>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.IsActive = on

        new(on : IMod<bool>, child : ISg) = OnOffNode(on, Mod.initConstant child)
        new(on : IEvent<bool>, child : IMod<ISg>) = OnOffNode(Mod.fromEvent on, child)
        new(on : IEvent<bool>, child : ISg) = OnOffNode(Mod.fromEvent on, Mod.initConstant child)

    type PassApplicator(pass : IMod<uint64>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Pass = pass

        new(pass : IMod<uint64>, child : ISg) = PassApplicator(pass, Mod.initConstant child)
        new(pass : IEvent<uint64>, child : IMod<ISg>) = PassApplicator(Mod.fromEvent pass, child)
        new(pass : IEvent<uint64>, child : ISg) = PassApplicator(Mod.fromEvent pass, Mod.initConstant child)

    type UniformApplicator private(holder : Either<IEventUniformHolder, IUniformHolder>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        let uniformHolder = 
            match holder with 
                | Left e -> Uniforms.EventUniformAdapter(e) :> IUniformHolder
                | Right h -> h

        member internal x.Uniforms = uniformHolder
        
        member x.TryGetUniform(scope : Scope, name : Symbol, [<Out>] result : byref<IEvent>) =
            match holder with
                | Left e -> e.TryGetUniform(scope, name, &result)
                | Right h -> 
                    match h.TryFindUniform scope name with
                        | Some u ->
                            result <- u.Event
                            true
                        | None ->
                            false

        member x.TryFindUniform (scope : Scope) (name : Symbol) =
            uniformHolder.TryFindUniform scope name


        new(value : IUniformHolder, child : IMod<ISg>) = UniformApplicator(Right value, child)
        new(value : IUniformHolder, child : ISg) = UniformApplicator(Right value, Mod.initConstant child)
        new(value : IEventUniformHolder, child : IMod<ISg>) = UniformApplicator(Left value, child)
        new(value : IEventUniformHolder, child : ISg) = UniformApplicator(Left value, Mod.initConstant child)

        new(name : string, value : IMod, child : ISg) = UniformApplicator(Right ((Uniforms.SimpleUniformHolder [Symbol.Create name,value]) :> IUniformHolder), Mod.initConstant child)
        new(name : string, value : IMod, child : IMod<ISg>) = UniformApplicator(Right ((Uniforms.SimpleUniformHolder [Symbol.Create name,value]) :> IUniformHolder), child)
        new(name : string, value : IEvent, child : ISg) = UniformApplicator(Right ((Uniforms.SimpleUniformHolder [Symbol.Create name,value.Mod]) :> IUniformHolder), Mod.initConstant child)
        new(name : string, value : IEvent, child : IMod<ISg>) = UniformApplicator(Right ((Uniforms.SimpleUniformHolder [Symbol.Create name,value.Mod]) :> IUniformHolder), child)
        new(name : Symbol, value : IMod, child : ISg) = UniformApplicator(Right ((Uniforms.SimpleUniformHolder [name,value]) :> IUniformHolder), Mod.initConstant child)
        new(name : Symbol, value : IMod, child : IMod<ISg>) = UniformApplicator(Right ((Uniforms.SimpleUniformHolder [name,value]) :> IUniformHolder), child)
        new(name : Symbol, value : IEvent, child : ISg) = UniformApplicator(Right ((Uniforms.SimpleUniformHolder [name,value.Mod]) :> IUniformHolder), Mod.initConstant child)
        new(name : Symbol, value : IEvent, child : IMod<ISg>) = UniformApplicator(Right ((Uniforms.SimpleUniformHolder [name,value.Mod]) :> IUniformHolder), child)

        new(map : Map<Symbol,IMod>, child : ISg) = UniformApplicator(Right ((Uniforms.SimpleUniformHolder map) :> IUniformHolder), Mod.initConstant child)


    type SurfaceApplicator(surface : IMod<ISurface>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Surface = surface

        new(value : IMod<ISurface>, child : ISg) = SurfaceApplicator(value, Mod.initConstant child)

        new(value : IEvent<ISurface>, child : IMod<ISg>) = SurfaceApplicator(Mod.fromEvent value, child)
        new(value : IEvent<ISurface>, child : ISg) = SurfaceApplicator(Mod.fromEvent value, Mod.initConstant child)
        new(value : ISurface, child : ISg) = SurfaceApplicator(Mod.initConstant value, Mod.initConstant child)

    type TextureApplicator(semantic : Symbol, texture : IMod<ITexture>, child : IMod<ISg>) =
        inherit UniformApplicator(semantic, texture :> IMod, child)

        member x.Texture = texture

        new(semantic : Symbol, texture : IMod<ITexture>, child : ISg) = TextureApplicator(semantic, texture, Mod.initConstant child)
        new(semantic : Symbol, texture : IEvent<ITexture>, child : IMod<ISg>) = TextureApplicator(semantic, Mod.fromEvent texture, child)
        new(semantic : Symbol, texture : IEvent<ITexture>, child : ISg) = TextureApplicator(semantic, Mod.fromEvent texture, Mod.initConstant child)
        new(texture : IMod<ITexture>, child : ISg) = TextureApplicator(DefaultSemantic.DiffuseColorTexture, texture, child)
        new(texture : IEvent<ITexture>, child : IMod<ISg>) = TextureApplicator(DefaultSemantic.DiffuseColorTexture,  texture, child)
        new(texture : IEvent<ITexture>, child : ISg) = TextureApplicator(DefaultSemantic.DiffuseColorTexture, texture, child)
        new(texture : IMod<ITexture>, child : IMod<ISg>) = TextureApplicator(DefaultSemantic.DiffuseColorTexture,texture,child)


    type TrafoApplicator(trafo : IMod<Trafo3d>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Trafo = trafo

        new(value : IMod<Trafo3d>, child : ISg) = TrafoApplicator(value, Mod.initConstant child)
        new(value : IEvent<Trafo3d>, child : IMod<ISg>) = TrafoApplicator(Mod.fromEvent value, child)
        new(value : IEvent<Trafo3d>, child : ISg) = TrafoApplicator(Mod.fromEvent value, Mod.initConstant child)
        new(value : Trafo3d, child : ISg) = TrafoApplicator(Mod.initConstant value, Mod.initConstant child)
    
    type ViewTrafoApplicator(trafo : IMod<Trafo3d>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.ViewTrafo = trafo

        new(value : IMod<Trafo3d>, child : ISg) = ViewTrafoApplicator(value, Mod.initConstant child)
        new(value : IEvent<Trafo3d>, child : IMod<ISg>) = ViewTrafoApplicator(Mod.fromEvent value, child)
        new(value : IEvent<Trafo3d>, child : ISg) = ViewTrafoApplicator(Mod.fromEvent value, Mod.initConstant child)

    type ProjectionTrafoApplicator(trafo : IMod<Trafo3d>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.ProjectionTrafo = trafo

        new(value : IMod<Trafo3d>, child : ISg) = ProjectionTrafoApplicator(value, Mod.initConstant child)
        new(value : IEvent<Trafo3d>, child : IMod<ISg>) = ProjectionTrafoApplicator(Mod.fromEvent value, child)
        new(value : IEvent<Trafo3d>, child : ISg) = ProjectionTrafoApplicator(Mod.fromEvent value, Mod.initConstant child)


    type DepthTestModeApplicator(mode : IMod<DepthTestMode>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mode = mode

        new(value : IMod<DepthTestMode>, child : ISg) = DepthTestModeApplicator(value, Mod.initConstant child)
        new(value : IEvent<DepthTestMode>, child : IMod<ISg>) = DepthTestModeApplicator(Mod.fromEvent value, child)
        new(value : IEvent<DepthTestMode>, child : ISg) = DepthTestModeApplicator(Mod.fromEvent value, Mod.initConstant child)

    type CullModeApplicator(mode : IMod<CullMode>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mode = mode

        new(value : IMod<CullMode>, child : ISg) = CullModeApplicator(value, Mod.initConstant child)
        new(value : IEvent<CullMode>, child : IMod<ISg>) = CullModeApplicator(Mod.fromEvent value, child)
        new(value : IEvent<CullMode>, child : ISg) = CullModeApplicator(Mod.fromEvent value, Mod.initConstant child)

    type FillModeApplicator(mode : IMod<FillMode>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mode = mode

        new(value : IMod<FillMode>, child : ISg) = FillModeApplicator(value, Mod.initConstant child)
        new(value : IEvent<FillMode>, child : IMod<ISg>) = FillModeApplicator(Mod.fromEvent value, child)
        new(value : IEvent<FillMode>, child : ISg) = FillModeApplicator(Mod.fromEvent value, Mod.initConstant child)
        new(value : FillMode, child : ISg) = FillModeApplicator(Mod.initConstant value, Mod.initConstant child)

    type StencilModeApplicator(mode : IMod<StencilMode>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mode = mode

        new(value : IMod<StencilMode>, child : ISg) = StencilModeApplicator(value, Mod.initConstant child)
        new(value : IEvent<StencilMode>, child : IMod<ISg>) = StencilModeApplicator(Mod.fromEvent value, child)
        new(value : IEvent<StencilMode>, child : ISg) = StencilModeApplicator(Mod.fromEvent value, Mod.initConstant child)

    type BlendModeApplicator(mode : IMod<BlendMode>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mode = mode

        new(value : IMod<BlendMode>, child : ISg) = BlendModeApplicator(value, Mod.initConstant child)
        new(value : IEvent<BlendMode>, child : IMod<ISg>) = BlendModeApplicator(Mod.fromEvent value, child)
        new(value : IEvent<BlendMode>, child : ISg) = BlendModeApplicator(Mod.fromEvent value, Mod.initConstant child)

    type RasterizerStateApplicator(state : IMod<RasterizerState>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        let depth = state |> Mod.map (fun s -> s.DepthTest)
        let cull = state |> Mod.map (fun s -> s.CullMode)
        let fill = state |> Mod.map (fun s -> s.FillMode)
        let stencil = state |> Mod.map (fun s -> s.StencilMode)
        let blend = state |> Mod.map (fun s -> s.BlendMode)

        member x.RasterizerState = state
        member x.DepthTestMode = depth
        member x.CullMode = cull
        member x.FillMode = fill
        member x.StencilMode = stencil
        member x.BlendMode = blend

        new(value : IMod<RasterizerState>, child : ISg) = RasterizerStateApplicator(value, Mod.initConstant child)
        new(value : IEvent<RasterizerState>, child : IMod<ISg>) = RasterizerStateApplicator(Mod.fromEvent value, child)
        new(value : IEvent<RasterizerState>, child : ISg) = RasterizerStateApplicator(Mod.fromEvent value, Mod.initConstant child)


    type Group(elements : seq<ISg>) =
        let aset = cset(elements)

        interface ISg

        member x.ASet : aset<ISg> = aset :> aset<_>


        member x.Add v =
            transact (fun () ->
               aset.Add v
            )

        member x.Remove v =
            transact (fun () ->
                aset.Remove v
            )

        member x.Clear() =
            transact (fun () ->
                aset.Clear()
            )

        member x.UnionWith v =
            transact (fun () ->
                aset.UnionWith v
            )

        member x.ExceptWith v =
            transact (fun () ->
                aset.ExceptWith v
            )

        member x.SymmetricExceptWith v =
            transact (fun () ->
                aset.SymmetricExceptWith v
            )

        member x.Count = aset.Count

        new() = Group(Seq.empty)
        
    type Set(content : aset<ISg>) =

        interface ISg
        member x.ASet = content


    type Environment (runtime : IRuntime, viewTrafo : IMod<Trafo3d>, projTrafo : IMod<Trafo3d>, viewSize : IMod<V2i>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Runtime = runtime
        member x.ViewTrafo = viewTrafo
        member x.ProjTrafo = projTrafo
        member x.ViewSize = viewSize

        member x.Scene = child

        new(runtime : IRuntime, viewTrafo : IEvent<Trafo3d>, projTrafo : IEvent<Trafo3d>, viewSize : IEvent<V2i>, child : ISg) =
            Environment(runtime, Mod.fromEvent viewTrafo, Mod.fromEvent projTrafo, Mod.fromEvent viewSize, Mod.initConstant child)
        new(runtime : IRuntime, viewTrafo : IEvent<Trafo3d>, projTrafo : IEvent<Trafo3d>, viewSize : IEvent<V2i>, child : IMod<ISg>) =
            Environment(runtime, Mod.fromEvent viewTrafo, Mod.fromEvent projTrafo, Mod.fromEvent viewSize, child)
        new(runtime : IRuntime, viewTrafo : IMod<Trafo3d>, projTrafo : IMod<Trafo3d>, viewSize : IMod<V2i>, child : ISg) =
            Environment(runtime, viewTrafo, projTrafo, viewSize, Mod.initConstant child)

    type LodScope = { trafo : Trafo3d; cameraPosition : V3d; scope : Scope}

    type LodNode(viewDecider : (LodScope -> bool), 
                 low : IMod<ISg>, high : IMod<ISg>) =
        interface ISg

        member x.Low = low
        member x.High = high
        member x.ViewDecider = viewDecider
        member val Name = "" with get, set

        new(viewDecider : System.Func<LodScope, bool>, low : ISg, high : ISg) = LodNode((fun t -> viewDecider.Invoke(t)), Mod.initConstant low, Mod.initConstant high)


    type ViewFrustumCullNode(sg : IMod<ISg>) =
        interface IApplicator with
            member x.Child = sg
        member x.Child = sg

        new(s : ISg) = ViewFrustumCullNode(Mod.initConstant s)
        new(s : IEvent<ISg>) = ViewFrustumCullNode(Mod.fromEvent  s)

    type ScopedDynamism(f : Scope -> IMod<ISg>) =
        member x.F = f

    type VisibleBB(color : IMod<C4b>, child : IMod<ISg>, renderBoth : bool) =
        interface IApplicator with
            member x.Child = child
        member x.Child = child
        member x.Color = color
        member x.RenderBoth = renderBoth

        new (color : C4b, child : ISg, renderBoth : bool) = VisibleBB (Mod.initConstant color, Mod.initConstant child, renderBoth)


    type TaggedGraph(name : string, sg : IMod<ISg>) =
        interface IApplicator with
            member x.Child = sg
        member x.Child = sg
        member x.Tag = name

        new(name : string, child : ISg) = TaggedGraph (name, Mod.initConstant child)


    type FramebufferDescription = { colorFormat : IMod<PixFormat>; depthFormat : IMod<PixFormat>; resolution : IMod<V2i>; samples : int }

    [<CompiledName("Named")>]
    let named (name : string) (child : ISg) = TaggedGraph(name,child)

    [<CompiledName("GeometryToSg")>]
    let ofIndexedGeometry (g : IndexedGeometry) =
        let attributes = 
            g.IndexedAttributes |> Seq.map (fun (KeyValue(k,v)) -> 
                let t = v.GetType().GetElementType()
                let view = BufferView(ArrayBuffer(Mod.initConstant v), t)

                k, view
            ) |> Map.ofSeq
        

        let index, faceVertexCount =
            if g.IsIndexed then
                g.IndexArray, g.IndexArray.Length
            else
                null, g.IndexedAttributes.[DefaultSemantic.Positions].Length

        let call = 
            DrawCallInfo(
                FaceVertexCount = faceVertexCount,
                FirstIndex = 0,
                InstanceCount = 1,
                FirstInstance = 0,
                Mode = g.Mode
            )

        let sg = VertexAttributeApplicator(attributes, RenderNode(call)) :> ISg
        if index <> null then
            let converteIndex = if index |> unbox<array<int>> |> Array.max < int System.UInt16.MaxValue then (index |> unbox<array<int>> |> Array.map uint16) :> System.Array else index
            VertexIndexApplicator(Mod.initConstant converteIndex, sg) :> ISg
        else
            sg


    let instancedGeometry (trafos : IMod<Trafo3d[]>) (g : IndexedGeometry) =
        let vertexAttributes = 
            g.IndexedAttributes |> Seq.map (fun (KeyValue(k,v)) -> 
                let t = v.GetType().GetElementType()
                let view = BufferView(ArrayBuffer(Mod.initConstant v), t)

                k, view
            ) |> Map.ofSeq

        let index, faceVertexCount =
            if g.IsIndexed then
                g.IndexArray, g.IndexArray.Length
            else
                null, g.IndexedAttributes.[DefaultSemantic.Positions].Length

        let call = trafos |> Mod.map (fun t ->
                DrawCallInfo(
                    FaceVertexCount = faceVertexCount,
                    FirstIndex = 0,
                    InstanceCount = t.Length,
                    FirstInstance = 0,
                    Mode = g.Mode
                )
            )

        let sg = VertexAttributeApplicator(vertexAttributes, RenderNode(call)) :> ISg
        
        let sg =
            if index <> null then
                VertexIndexApplicator(Mod.initConstant index, sg) :> ISg
            else
                sg

        let m44Trafos = trafos |> Mod.map (fun a -> a |> Array.map (fun (t : Trafo3d) -> (M44f.op_Explicit t.Forward).Transposed) :> Array)
        let m44View = BufferView(ArrayBuffer m44Trafos, typeof<M44f>)

        InstanceAttributeApplicator([DefaultSemantic.InstanceTrafo, m44View] |> Map.ofList, sg) :> ISg

    [<CompiledName("NormalizedTo")>]
    let normalizeTo (box : Box3d) (this : ISg) =

        let getBoxScale (fromBox : Box3d) (toBox : Box3d) : float =
            let fromSize = fromBox.Size
            let toSize = toBox.Size
            let factor = toSize / fromSize

            let mutable smallest = factor.X

            if factor.Y < smallest then
                smallest <- factor.Y
            if factor.Z < smallest then
                smallest <- factor.Z

            smallest

        let bb = this?GlobalBoundingBox() : IMod<Box3d>

        printfn "normalizing from: %A" ( bb.GetValue() )

        let transformBox (sbox : Box3d) = Trafo3d.Translation(-sbox.Center) * Trafo3d.Scale(getBoxScale sbox box) * Trafo3d.Translation(box.Center)

        TrafoApplicator(Mod.map transformBox bb, this) :> ISg

    [<CompiledName("Normalized")>]
    let normalize sg = sg |> normalizeTo ( Box3d( V3d(-1,-1,-1), V3d(1,1,1) ) ) 

[<AutoOpen>]
module RuntimeExtensions =


    type IRuntime with

        member x.CompileRender (e : Sg.Environment) =
            let jobs : aset<RenderJob> = e?RenderJobs()
            x.CompileRender(jobs)

[<AutoOpen>]
module Semantics =
    open Sg

    module private Providers =
        
        type AttributeProvider(scope : Scope, attName : string) =
            let mutable scope = scope
            let mutable cache : Option<Map<Symbol, BufferView>> = None

            let getMap() =
                match cache with
                    | Some c -> c
                    | None -> 
                        match scope.TryGetAttributeValue attName with
                            | Success map ->
                                cache <- Some map
                                map
                            | Error e ->
                                failwithf "could not get atttribute map %A for %A" attName scope

            interface IAttributeProvider with

                member x.Dispose() =
                    cache <- None
                    scope <- emptyScope

                member x.All =
                    getMap() |> Map.toSeq

                member x.TryGetAttribute(s : Symbol, result : byref<BufferView>) =
                    let m = getMap()

                    match Map.tryFind s m with
                        | Some v ->
                            result <- v
                            true
                        | _ -> false

        type SimpleAttributeProvider(ig : IndexedGeometry) =
            let mutable cache = SymbolDict<BufferView>()

            
            member x.Dispose() = cache.Clear()

            member x.TryGetAttribute(s : Symbol, [<Out>] result : byref<BufferView>) =
                match cache.TryGetValue s with
                    | (true, v) ->
                        result <- v
                        true
                    | _ ->
                        match ig.IndexedAttributes.TryGetValue s with
                            | (true, att) -> 
                                let m = Mod.initConstant att

                                let t = att.GetType().GetElementType()
                                let v = BufferView(ArrayBuffer m, t)

                                cache.[s] <- v
                                result <- v
                                true
                            | _ -> 
                                false
                                
            member x.All =
                seq {
                    for k in ig.IndexedAttributes.Keys do
                        match x.TryGetAttribute(k) with
                            | (true, att) -> yield k, att
                            | _ -> ()
                }

            interface IAttributeProvider with
                member x.TryGetAttribute(key, v) = x.TryGetAttribute(key, &v)
                member x.All = x.All
                member x.Dispose() = x.Dispose()

        type UniformProvider(scope : Scope, uniforms : list<IUniformHolder>) =
            let mutable scope = scope
            let mutable cache = SymbolDict<IMod>()

            interface IUniformProvider with

                member x.Dispose() =
                    cache.Clear()
                    scope <- emptyScope

                member x.TryGetUniform(s : Symbol, result : byref<IMod>) =
                    match cache.TryGetValue s with
                        | (true, m) -> 
                            result <- m
                            true

                        | _ -> 
                            match uniforms |> List.tryPick (fun u -> u.TryFindUniform scope s) with
                                | Some u -> 
                                    let cs = u
                                    cache.Add(s, cs)
                                    result <- cs
                                    true
                                | None -> 
                                    match scope.TryGetAttributeValue (s.ToString()) with
                                        | Success (v : IMod) -> 
                                            let cs = v
                                            cache.Add(s, cs)
                                            result <- cs
                                            true
                                        | _ ->
                                            false

    [<AutoOpen>]
    module SemanticAccessors =
    
        type ISg with
            member x.RenderJobs() : aset<RenderJob> = x?RenderJobs()
            member x.ModelTrafo : IMod<Trafo3d> = x?ModelTrafo
            member x.ViewTrafo : IMod<Trafo3d> = x?ViewTrafo

    [<Semantic>]
    type TrafoSem() =
        static let rootTrafo = Mod.initConstant Trafo3d.Identity

        
        let memo = MemoCache(false)
        let (<*>) a b = 
            if a = rootTrafo then b
            elif b = rootTrafo then a
            else memo.Memoized2 (fun a b -> Mod.map2 (fun a b -> Trafo3d.op_Multiply(a,b)) a b) a b

        let inverse t =
            if t = rootTrafo then t
            else Mod.map (fun (t : Trafo3d) -> t.Inverse) t

        static member RootTrafo = rootTrafo


        member x.ModelTrafo(e : Root) = 
            e.Child?ModelTrafo <- rootTrafo

        member x.ModelTrafo(t : TrafoApplicator) =
            t.Child?ModelTrafo <- t.Trafo <*> t?ModelTrafo


        member x.ViewTrafo(v : ViewTrafoApplicator) =
            v.Child?ViewTrafo <- v.ViewTrafo

        member x.ProjTrafo(p : ProjectionTrafoApplicator) =
            p.Child?ProjTrafo <- p.ProjectionTrafo

        member x.ViewTrafo(e : Environment) =
            e.Child?ViewTrafo <- e.ViewTrafo

        member x.ProjTrafo(e : Environment) =
            e.Child?ProjTrafo <- e.ProjTrafo


        member x.ModelTrafoInv(s : ISg) =
            s?ModelTrafo |> inverse

        member x.ViewTrafoInv(s : ISg) =
            s?ViewTrafo |> inverse

        member x.ProjTrafoInv(s : ISg) =
            s?ProjTrafo |> inverse


        member x.ModelViewTrafo(s : ISg) =
            s?ModelTrafo <*> s?ViewTrafo

        member x.ViewProjTrafo(s : ISg) =
            s?ViewTrafo <*> s?ProjTrafo

        member x.ModelViewProjTrafo(s : ISg) =
            s?ModelTrafo <*> s?ViewProjTrafo()


        member x.ModelViewTrafoInv(s : ISg) =
            s?ModelViewTrafo() |> inverse
        
        member x.ViewProjTrafoInv(s : ISg) =
            s?ViewProjTrafo() |> inverse

        member x.ModelViewProjTrafoInv(s : ISg) =
            s?ModelViewProjTrafo() |> inverse

    [<Semantic>]
    type AttributeSem() =
        static let emptyIndex : IMod<Array> = Mod.initConstant null
        static let zero = Mod.initConstant 0

        let (~%) (m : Map<Symbol, BufferView>) = m

        let union (l : Map<Symbol, BufferView>) (r : Map<Symbol, BufferView>) =
            let mutable result = l
            for (k,v) in r |> Map.toSeq do
                result <- Map.add k v result

            result

        static member EmptyIndex = emptyIndex

        member x.FaceVertexCount (root : Root) =
            root.Child?FaceVertexCount <- zero

        member x.FaceVertexCount (app : VertexIndexApplicator) =
            app.Child?FaceVertexCount <- app.Value |> Mod.map (fun a -> a.Length)

        member x.FaceVertexCount (app : VertexAttributeApplicator) =
            let res : IMod<int> = app?FaceVertexCount

            if res <> zero then
                app.Child?FaceVertexCount <- res
            else
                match Map.tryFind DefaultSemantic.Positions app.Values with
                    | Some pos ->
                        match pos.Buffer with
                            | :? ArrayBuffer as ab ->
                                app.Child?FaceVertexCount <- ab.Data |> Mod.map (fun a -> a.Length - pos.Offset)
            
                            | _ -> app.Child?FaceVertexCount <- zero
                    | _ -> app.Child?FaceVertexCount <- zero

        member x.InstanceAttributes(root : Root) = 
            root.Child?InstanceAttributes <- %Map.empty

        member x.VertexIndexArray(e : Root) =
            e.Child?VertexIndexArray <- emptyIndex

        member x.VertexIndexArray(v : VertexIndexApplicator) =
            v.Child?VertexIndexArray <- v.Value

        member x.VertexAttributes(e : Root) =
            e.Child?VertexAttributes <- %Map.empty

        member x.VertexAttributes(v : VertexAttributeApplicator) =
            v.Child?VertexAttributes <- union v?VertexAttributes v.Values

        member x.InstanceAttributes(v : InstanceAttributeApplicator) =
            v.Child?InstanceAttributes <- union v?InstanceAttributes v.Values

    [<Semantic>]
    type SurfaceSem() =
        let emptySurface : IMod<ISurface> = Mod.initConstant null

        member x.Surface(e : Environment) =
            e.Scene?Surface <- emptySurface

        member x.Surface(s : SurfaceApplicator) =
            s.Child?Surface <- s.Surface

    [<Semantic>]
    type UniformSem() =
        member x.Uniforms(e : Root) =
            e.Child?Uniforms <- ([] : list<IUniformHolder>)

        member x.Uniforms(u : UniformApplicator) =
            u.Child?Uniforms <- u.Uniforms :: u?Uniforms

    [<Semantic>]
    type ModeSem() =
        let defaultDepth = Mod.initConstant DepthTestMode.LessOrEqual
        let defaultCull = Mod.initConstant CullMode.None
        let defaultFill = Mod.initConstant FillMode.Fill
        let defaultStencil = Mod.initConstant StencilMode.Disabled
        let defaultBlend = Mod.initConstant BlendMode.None

        member x.DepthTestMode(e : Root) =
            e.Child?DepthTestMode <- defaultDepth

        member x.CullMode(e : Root) =
            e.Child?CullMode <- defaultCull

        member x.FillMode(e : Root) =
            e.Child?FillMode <- defaultFill

        member x.StencilMode(e : Root) =
            e.Child?StencilMode <- defaultStencil

        member x.BlendMode(e : Root) =
            e.Child?BlendMode <- defaultBlend


        member x.DepthTestMode(a : DepthTestModeApplicator) =
            a.Child?DepthTestMode <- a.Mode

        member x.CullMode(a : CullModeApplicator) =
            a.Child?CullMode <- a.Mode

        member x.FillMode(a : FillModeApplicator) =
            a.Child?FillMode <- a.Mode

        member x.StencilMode(a : StencilModeApplicator) =
            a.Child?StencilMode <- a.Mode

        member x.BlendMode(a : BlendModeApplicator) =
            a.Child?BlendMode <- a.Mode



        member x.DepthTestMode(a : RasterizerStateApplicator) =
            a.Child?DepthTestMode <- a.DepthTestMode

        member x.CullMode(a : RasterizerStateApplicator) =
            a.Child?CullMode <- a.CullMode

        member x.FillMode(a : RasterizerStateApplicator) =
            a.Child?FillMode <- a.FillMode

        member x.StencilMode(a : RasterizerStateApplicator) =
            a.Child?StencilMode <- a.StencilMode

        member x.BlendMode(a : RasterizerStateApplicator) =
            a.Child?BlendMode <- a.BlendMode

    [<Semantic>]
    type ActiveAndPassSem() =
        let t = Mod.initConstant true
        let defaultPass = Mod.initConstant 0UL

        let (<&>) (a : IMod<bool>) (b : IMod<bool>) =
            if a = t then b
            elif b = t then a
            else Mod.map2 (&&) a b

        member x.IsActive(r : Root) =
            r.Child?IsActive <- t

        member x.IsActive(o : OnOffNode) =
            o.Child?IsActive <- o?IsActive <&> o.IsActive

        member x.RenderPass(e : Root) =
            e.Child?RenderPass <- defaultPass

        member x.RenderPass(p : PassApplicator) =
            p.Child?RenderPass <- p.Pass

    [<Semantic>]
    type RenderJobSem() =

        let (~%) (v : aset<'a>) = v

        member x.RenderJobs(a : IApplicator) : aset<RenderJob> =
            aset {
                let! c = a.Child
                yield! %c?RenderJobs()
            }

        member x.RenderJobs(g : Group) : aset<RenderJob> =
            aset {
                for c in g.ASet do
                    yield! %c?RenderJobs()
            }

        member x.RenderJobs(set : Set) : aset<RenderJob> =
            aset {
                for c in set.ASet do
                    yield! %c?RenderJobs()
            }

        member x.RenderJobs(r : RenderNode) : aset<RenderJob> =
            let scope = Ag.getContext()
            let rj = RenderJob.Create(scope.Path)
            
            rj.AttributeScope <- scope :> obj
            rj.Indices <- let index  = r?VertexIndexArray in if index = AttributeSem.EmptyIndex then null else index 
            
            let active : IMod<bool> = r?IsActive
            
            rj.IsActive <- r?IsActive
            rj.RenderPass <- r?RenderPass
            
            
            rj.Uniforms <- new Providers.UniformProvider(scope, r?Uniforms)
            rj.VertexAttributes <- new Providers.AttributeProvider(scope, "VertexAttributes")
            rj.InstanceAttributes <- new Providers.AttributeProvider(scope, "InstanceAttributes")
            
            rj.DepthTest <- r?DepthTestMode
            rj.CullMode <- r?CullMode
            rj.FillMode <- r?FillMode
            rj.StencilMode <- r?StencilMode
            rj.BlendMode <- r?BlendMode
              
            rj.Surface <- r?Surface
            
            let callInfo =
                adaptive {
                    let! info = r.DrawCallInfo
                    if info.FaceVertexCount < 0 then
                        let! (count : int) = scope?FaceVertexCount
                        return 
                            DrawCallInfo(
                                FirstIndex = info.FirstIndex,
                                FirstInstance = info.FirstInstance,
                                InstanceCount = info.InstanceCount,
                                FaceVertexCount = count,
                                Mode = info.Mode
                            )
                    else
                        return info
                }

            rj.DrawCallInfo <- callInfo

            ASet.single rj

    [<Semantic>]
    type LodSem() =
        member x.RenderJobs(node : LodNode) : aset<RenderJob> =

            let mvTrafo = node?ModelViewTrafo()
            aset {
                let scope = Ag.getContext()

                let! highSg,lowSg = node.High,node.Low

                let lowJobs = lowSg?RenderJobs() : aset<RenderJob> 
                let highJobs = highSg?RenderJobs() : aset<RenderJob>

                //this parallel read is absolutely crucial for performance, since otherwise the 
                //resulting set will no longer be referentially equal (cannot really be solved any other way)
                //once more we see that adaptive code is extremely sensible.
                let! camLocation,trafo = node?CameraLocation,mvTrafo

                if node.ViewDecider { trafo = trafo; cameraPosition = camLocation; scope = scope } then 
                    yield! highJobs
                else    
                    yield! lowJobs
            }

    let private bbCache = ConditionalWeakTable<RenderJob, IMod<Box3d>>()
    type RenderJob with
        member x.GetBoundingBox() =
            match bbCache.TryGetValue x with
                | (true, v) -> v
                | _ ->
                    let v = 
                        match x.VertexAttributes.TryGetAttribute DefaultSemantic.Positions with
                            | (true, v) ->
                                match v.Buffer with
                                    | :? ArrayBuffer as pos ->
                                        let trafo : IMod<Trafo3d> = x.AttributeScope?ModelTrafo

                                        Mod.map2 (fun arr trafo ->
                                            let box = Box3f.op_Explicit (Box3f(arr |> unbox<V3f[]>))
                                            box
                                        ) pos.Data trafo

                                    | _ ->
                                        failwithf "invalid positions in renderjob: %A" x
                            | _ ->
                                failwithf "no positions in renderjob: %A" x
                    bbCache.Add(x,v)
                    v

    [<Semantic>]
    type CullNodeSem() =
        member x.RenderJobs(c : ViewFrustumCullNode) :  aset<RenderJob>=
            let intersectsFrustum (b : Box3d) (f : Trafo3d) =
                b.IntersectsFrustum(f.Forward)
            
            aset {

                let! child = c.Child
                let jobs = child?RenderJobs() : aset<RenderJob>

                let viewProjTrafo = c?ViewProjTrafo() : IMod<Trafo3d>

                yield! jobs |> ASet.filterM (fun rj -> Mod.map2 intersectsFrustum (rj.GetBoundingBox()) viewProjTrafo)
//
//                for rj : RenderJob in jobs do
//                    let! viewProjTrafo = c?ViewProjTrafo() : Mod<Trafo3d>
//                    let! bb = rj.GetBoundingBox().Mod
//                    if intersectsFrustum bb viewProjTrafo 
//                    then yield rj
            }


    [<Semantic>]
    type Derived() =

        let trueM = Mod.initConstant true
        let falseM = Mod.initConstant false

        member x.HasDiffuseColorTexture(sg : ISg) = 
            let uniforms : IUniformHolder list = sg?Uniforms 
            match uniforms |> List.tryPick (fun uniforms -> uniforms.TryFindUniform (Ag.getContext()) (Symbol.Create("DiffuseColorTexture"))) with
                | None -> match tryGetAttributeValue sg "DiffuseColorTexture" with
                                | Success v -> trueM
                                | _ -> falseM
                | Some _ -> trueM

        member x.ViewportSize(e : Sg.Environment) = e.Child?ViewportSize <- e.ViewSize
          
        member x.RcpViewportSize(e : ISg) = e?ViewportSize |> Mod.map (fun (s : V2i) -> 1.0 / (V2d s))


    [<AutoOpen>]
    module BoundingBoxes =

        type ISg with
            member x.GlobalBoundingBox() : IMod<Box3d> = x?GlobalBoundingBox()
            member x.LocalBoundingBox() : IMod<Box3d> = x?LocalBoundingBox()

        let globalBoundingBox (sg : ISg) = sg.GlobalBoundingBox()
        let localBoundingBox (sg : ISg) = sg.LocalBoundingBox()

        [<Semantic>]
        type GlobalBoundingBoxSem() =

            let boxFromArray (v : V3d[]) = if v.Length = 0 then Box3d.Invalid else Box3d v

            member x.GlobalBoundingBox(node : RenderNode) : IMod<Box3d> =
                let scope = Ag.getContext()
                let va = node?VertexAttributes
                let positions : BufferView = 
                    match Map.tryFind DefaultSemantic.Positions va with
                        | Some v -> v
                        | _ -> failwith "no positions specified"

                match positions.Buffer with
                    | :? ArrayBuffer as ab ->
                        let positions = ab.Data

                        adaptive {
                            let! positions = positions
                            let positions = positions |> unbox<V3f[]>
                            let! (trafo : Trafo3d) = node?ModelTrafo
                            match node?VertexIndexArray with
                                | a when a = AttributeSem.EmptyIndex -> 
                                     return positions |> Array.map (fun p -> trafo.Forward.TransformPos(V3d p)) |> boxFromArray
                                | indices ->
                                        let! indices = indices
                                        let filteredPositions = if indices.GetType().GetElementType() = typeof<uint16> 
                                                                then indices |> unbox<uint16[]> |> Array.map (fun i -> positions.[int i])
                                                                else indices |> unbox<int[]> |> Array.map (fun i -> positions.[i])
                                        return filteredPositions |> Array.map (fun p -> trafo.Forward.TransformPos(V3d p)) |> boxFromArray
                        }
                    | _ ->
                        failwithf "unknown IBuffer for positions: %A" positions.Buffer

            member x.GlobalBoundingBox(app : Group) : IMod<Box3d> =
                app.ASet |> ASet.map (fun sg -> sg?GlobalBoundingBox() ) |> ASet.toMod |> Mod.map (fun (values : ReferenceCountingSet<Box3d>) -> Box3d ( values ) )

            member x.GlobalBoundingBox(n : IApplicator) : IMod<Box3d> = 
                adaptive {
                    let! low = n.Child
                    return! low?GlobalBoundingBox()
                }
            member x.GlobalBoundingBox(n : LodNode) : IMod<Box3d> = 
                adaptive {
                    let! low = n.Low
                    return! low?GlobalBoundingBox()
                }

        [<Semantic>]
        type LocalBoundingBoxSem() =

            let boxFromArray (v : V3f[]) = if v.Length = 0 then Box3d.Invalid else Box3d (Box3f v)
            let transform (bb : Box3d) (t : Trafo3d) = bb.Transformed t

            member x.LocalBoundingBox(node : RenderNode) : IMod<Box3d> =
                let scope = Ag.getContext()
                let va = node?VertexAttributes
                let positions : BufferView = 
                    match Map.tryFind DefaultSemantic.Positions va with
                        | Some v -> v
                        | _ -> failwith "no positions specified"

                match positions.Buffer with
                    | :? ArrayBuffer as ab ->
                        let positions = ab.Data
                        adaptive {
                            let! positions = positions
                            let positions = positions |> unbox<V3f[]>
                            match node?VertexIndexArray with
                                | a when a = AttributeSem.EmptyIndex -> 
                                     return positions |> boxFromArray
                                | indices ->
                                        let! indices = indices
                                        let indices = indices |> unbox<int[]>
                                        return indices |> Array.map (fun (i : int) -> positions.[i]) |> boxFromArray
                        }
                    | _ ->
                        failwithf "unknown IBuffer for positions: %A" positions.Buffer

            member x.LocalBoundingBox(app : Group) : IMod<Box3d> =
                app.ASet |> ASet.map (fun sg -> sg?LocalBoundingBox() ) |> ASet.toMod |> Mod.map (fun (values : ReferenceCountingSet<Box3d>) -> Box3d ( values ) )

            member x.LocalBoundingBox(app : TrafoApplicator) : IMod<Box3d> =  
                adaptive {
                    let! c = app.Child
                    let! bb = c?LocalBoundingBox() : IMod<Box3d>
                    let! trafo = app.Trafo
                    return transform bb trafo
                }
            member x.LocalBoundingBox(n : IApplicator) : IMod<Box3d> = 
                adaptive {
                    let! low = n.Child
                    return! low?LocalBoundingBox()
                }
            member x.LocalBoundingBox(n : LodNode) : IMod<Box3d> = 
                adaptive {
                    let! low = n.Low
                    return! low?LocalBoundingBox()
                }
    
    [<AutoOpen>]
    module DefaultValues =

        [<Semantic>]
        type DefaultValues() =

            let getViewPosition (viewTrafo : Trafo3d) = viewTrafo.GetViewPosition()

            member x.LightLocations(e : Environment) =
                e.Child?LightLocations <- [| Mod.map getViewPosition e.ViewTrafo |]

            member x.LightLocation(e : Environment) =
                e.Child?LightLocation <- Mod.map getViewPosition e.ViewTrafo 

            member x.CameraLocation(e : Environment) =
                e.Child?CameraLocation <- Mod.map getViewPosition e.ViewTrafo

            member x.CameraLocation(e : ViewTrafoApplicator) =
                e.Child?CameraLocation <- Mod.map getViewPosition e.ViewTrafo


            member x.NormalMatrix(s : ISg) : IMod<M44d> = 
                Mod.map (fun (t : Trafo3d) -> t.Backward.Transposed) s?ModelTrafo

            member x.Runtime(e : Environment) =
                e.Child?Runtime <- e.Runtime


    [<Semantic>]
    type AdapterSemantics() =
        member x.RenderJobs(a : AdapterNode) : aset<RenderJob> =
            a.Node?RenderJobs()

        member x.GlobalBoundingBox(a : AdapterNode) : IMod<Box3d> =
            a.Node?GlobalBoundingBox()

        member x.LocalBoundingBox(a : AdapterNode) : IMod<Box3d> =
            a.Node?LocalBoundingBox()
