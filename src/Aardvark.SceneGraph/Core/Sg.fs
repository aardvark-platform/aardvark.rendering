namespace Aardvark.SceneGraph

open System
open System.Runtime.InteropServices
open System.Collections.Generic

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Ag
open Aardvark.Base.Rendering

module Sg =

    [<AbstractClass>]
    type AbstractApplicator(child : aval<ISg>) =
        interface IApplicator with
            member x.Child = child

        member x.Child = child

        new(child : ISg) = AbstractApplicator(AVal.constant child)

    type AdapterNode(node : obj) =
        interface ISg

        member x.Node = node

    type DynamicNode(child : aval<ISg>) = inherit AbstractApplicator(child)

    type RenderNode(call : aval<DrawCallInfo>, mode : IndexedGeometryMode) =
        interface ISg

        member x.Mode = mode
        member x.DrawCallInfo = call

        new(call : DrawCallInfo, mode : IndexedGeometryMode) = RenderNode(AVal.constant call, mode)
        new(count : int, mode : IndexedGeometryMode) = 
            let call = 
                DrawCallInfo(
                    FaceVertexCount = count,
                    InstanceCount = 1,
                    FirstIndex = 0,
                    FirstInstance = 0,
                    BaseVertex = 0
                )
            RenderNode(AVal.constant call, mode)

        new(count : aval<int>, mode : IndexedGeometryMode) = 
            let call =
                count |> AVal.map (fun x ->
                    DrawCallInfo(
                        FaceVertexCount = x,
                        InstanceCount = 1,
                        FirstIndex = 0,
                        FirstInstance = 0,
                        BaseVertex = 0
                    )
                )
            RenderNode(call, mode)
    
    type RenderObjectNode(objects : aset<IRenderObject>) =
        interface ISg
        member x.Objects = objects

    type IndirectRenderNode(buffer : aval<IndirectBuffer>, mode : IndexedGeometryMode) =
        interface ISg

        member x.Mode = mode
        member x.Indirect = buffer

    type VertexAttributeApplicator(values : Map<Symbol, BufferView>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Values = values

        new(values : Map<Symbol, BufferView>, child : ISg)            = VertexAttributeApplicator(values, AVal.constant child)
        new(semantic : Symbol, value : BufferView, child : aval<ISg>) = VertexAttributeApplicator(Map.ofList [semantic, value], child)
        new(semantic : Symbol, value : BufferView, child : ISg)       = VertexAttributeApplicator(Map.ofList [semantic, value], AVal.constant child)
        new(values : SymbolDict<BufferView>, child : ISg)             = VertexAttributeApplicator(values |> Seq.map (fun (KeyValue(k,v)) -> k,v) |> Map.ofSeq, AVal.constant child)

    type VertexIndexApplicator(value : BufferView, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Value = value

        new(value : BufferView, child : ISg)         = VertexIndexApplicator(value, AVal.constant child)

    type InstanceAttributeApplicator(values : Map<Symbol, BufferView>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Values = values

        new(values : Map<Symbol, BufferView>, child : ISg)            = InstanceAttributeApplicator(values, AVal.constant child)
        new(semantic : Symbol, value : BufferView, child : aval<ISg>) = InstanceAttributeApplicator(Map.ofList [semantic, value], child)
        new(semantic : Symbol, value : BufferView, child : ISg)       = InstanceAttributeApplicator(Map.ofList [semantic, value], AVal.constant child)
        new(values : SymbolDict<BufferView>, child : ISg)             = InstanceAttributeApplicator(values |> Seq.map (fun (KeyValue(k,v)) -> k,v) |> Map.ofSeq, AVal.constant child)
 

    type OnOffNode(on : aval<bool>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.IsActive = on

        new(on : aval<bool>, child : ISg) = OnOffNode(on, AVal.constant child)

    type PassApplicator(pass : RenderPass, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Pass = pass

        new(pass : RenderPass, child : ISg) = PassApplicator(pass, AVal.constant child)

    type UniformApplicator(uniformHolder : IUniformProvider, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member internal x.Uniforms = uniformHolder
        
        member x.TryFindUniform (scope : Scope) (name : Symbol) =
            uniformHolder.TryGetUniform (scope,name)

        new(value : IUniformProvider, child : ISg) = UniformApplicator( value, AVal.constant child)
        new(name : string, value : IAdaptiveValue, child : ISg) = UniformApplicator( (new Providers.SimpleUniformHolder ([Symbol.Create name,value]) :> IUniformProvider), AVal.constant child)
        new(name : Symbol, value : IAdaptiveValue, child : ISg) = UniformApplicator( (new Providers.SimpleUniformHolder( [name,value]) :> IUniformProvider), AVal.constant child)
        new(name : Symbol, value : IAdaptiveValue, child : aval<ISg>) = UniformApplicator( (new Providers.SimpleUniformHolder( [name,value]) :> IUniformProvider), child)
        new(map : Map<Symbol,IAdaptiveValue>, child : ISg) = UniformApplicator( (new Providers.SimpleUniformHolder( map) :> IUniformProvider), AVal.constant child)


    type SurfaceApplicator(surface : Surface, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Surface = surface

        new(value : Surface, child : ISg) = SurfaceApplicator(value, AVal.constant child)

        new(value : ISurface, child : ISg) = SurfaceApplicator(Surface.Backend value, AVal.constant child)
        new(value : ISurface, child : aval<ISg>) = SurfaceApplicator(Surface.Backend value, child)

    type TextureApplicator(semantic : Symbol, texture : aval<ITexture>, child : aval<ISg>) =
        inherit UniformApplicator(semantic, texture :> IAdaptiveValue, child)

        member x.Texture = texture

        new(semantic : Symbol, texture : aval<ITexture>, child : ISg) = TextureApplicator(semantic, texture, AVal.constant child)
        new(texture : aval<ITexture>, child : ISg) = TextureApplicator(DefaultSemantic.DiffuseColorTexture, texture, child)
        new(texture : aval<ITexture>, child : aval<ISg>) = TextureApplicator(DefaultSemantic.DiffuseColorTexture,texture,child)


    type TrafoApplicator(trafo : aval<Trafo3d>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Trafo = trafo

        new(value : aval<Trafo3d>, child : ISg) = TrafoApplicator(value, AVal.constant child)
        new(value : Trafo3d, child : ISg) = TrafoApplicator(AVal.constant value, AVal.constant child)
    
    type ViewTrafoApplicator(trafo : aval<Trafo3d>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.ViewTrafo = trafo

        new(value : aval<Trafo3d>, child : ISg) = ViewTrafoApplicator(value, AVal.constant child)

    type ProjectionTrafoApplicator(trafo : aval<Trafo3d>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.ProjectionTrafo = trafo

        new(value : aval<Trafo3d>, child : ISg) = ProjectionTrafoApplicator(value, AVal.constant child)


    type DepthTestModeApplicator(mode : aval<DepthTestMode>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mode = mode

        new(mode : aval<DepthTestMode>, child : ISg) = DepthTestModeApplicator(mode, AVal.constant child)

     type DepthBiasApplicator(bias : aval<DepthBiasState>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.State = bias

        new(state : aval<DepthBiasState>, child : ISg) = DepthBiasApplicator(state, AVal.constant child)

    type CullModeApplicator(mode : aval<CullMode>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mode = mode

        new(value : aval<CullMode>, child : ISg) = CullModeApplicator(value, AVal.constant child)

    type FrontFaceApplicator(winding : aval<WindingOrder>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.WindingOrder = winding

        new(winding : aval<WindingOrder>, child : ISg) = FrontFaceApplicator(winding, AVal.constant child)

    type FillModeApplicator(mode : aval<FillMode>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mode = mode

        new(value : aval<FillMode>, child : ISg) = FillModeApplicator(value, AVal.constant child)
        new(value : FillMode, child : ISg) = FillModeApplicator(AVal.constant value, AVal.constant child)

    type StencilModeApplicator(mode : aval<StencilMode>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mode = mode

        new(value : aval<StencilMode>, child : ISg) = StencilModeApplicator(value, AVal.constant child)

    type BlendModeApplicator(mode : aval<BlendMode>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mode = mode

        new(value : aval<BlendMode>, child : ISg) = BlendModeApplicator(value, AVal.constant child)

    type RasterizerStateApplicator(state : aval<RasterizerState>, child : aval<ISg>) =
        inherit AbstractApplicator(child)

        let depth = state |> AVal.map (fun s -> s.DepthTest)
        let bias = state |> AVal.map (fun s -> s.DepthBias)
        let cull = state |> AVal.map (fun s -> s.CullMode)
        let front = state |> AVal.map (fun s -> s.FrontFace)
        let fill = state |> AVal.map (fun s -> s.FillMode)
        let stencil = state |> AVal.map (fun s -> s.StencilMode)
        let blend = state |> AVal.map (fun s -> s.BlendMode)

        member x.RasterizerState = state
        member x.DepthTestMode = depth
        member x.DepthBias = bias
        member x.CullMode = cull
        member x.FrontFace = front
        member x.FillMode = fill
        member x.StencilMode = stencil
        member x.BlendMode = blend

        new(value : aval<RasterizerState>, child : ISg) = RasterizerStateApplicator(value, AVal.constant child)

    type WriteBuffersApplicator(buffers : Option<Set<Symbol>>, child : aval<ISg>) =
        inherit AbstractApplicator(child)
        member x.WriteBuffers = buffers
        new(buffers : Option<Set<Symbol>>, child : ISg) = WriteBuffersApplicator(buffers, AVal.constant child)
        
    type ConservativeRasterApplicator(state : aval<bool>, child : aval<ISg>) =
        inherit AbstractApplicator(child)
        member x.ConservativeRaster = state

    type MultisampleApplicator(state : aval<bool>, child : aval<ISg>) =
        inherit AbstractApplicator(child)
        member x.Multisample = state


    type ColorWriteMaskApplicator(maskRgba : aval<bool*bool*bool*bool>, child : aval<ISg>) =
        inherit AbstractApplicator(child)
        member x.MaskRgba = maskRgba

    type DepthWriteMaskApplicator(writeEnabled : aval<bool>, child : aval<ISg>) =
        inherit AbstractApplicator(child)
        member x.WriteEnabled = writeEnabled

    type Set(content : aset<ISg>) =

        interface IGroup with
            member x.Children = content

        member x.ASet = content

        new([<ParamArray>] items: ISg[]) = Set(items |> ASet.ofArray)

        new(items : seq<ISg>) = Set(items |> ASet.ofSeq)

    type OverlayNode(task : IRenderTask) =
        interface ISg
        member x.RenderTask = task

    type GeometrySet(geometries : aset<IndexedGeometry>, mode : IndexedGeometryMode, attributeTypes : Map<Symbol,Type>) =
        interface ISg
        member x.Geometries = geometries
        member x.Mode = mode
        member x.AttributeTypes = attributeTypes

    type RenderObjectSet(set : aset<IRenderObject>) =
        interface ISg
        member x.Set = set


module SceneGraphCompletenessCheck =
    open System.Text.RegularExpressions

    let semantics =
        [
            "RenderObjects",        typeof<aset<IRenderObject>>
            "GlobalBoundingBox",    typeof<aval<Box3d>>
            "LocalBoundingBox",     typeof<aval<Box3d>>
        ]

    let genericNameRx = Regex @"(?<name>.*?)´[0-9]+"
    let cleanName (name : string) =
        let m = genericNameRx.Match name
        if m.Success then m.Groups.["name"].Value
        else name

    let intrisicNames =
        Dict.ofList [
            typeof<byte>, "byte"
            typeof<int8>, "int8"
            typeof<uint16>, "uint16"
            typeof<int16>, "int16"
            typeof<int>, "int"
            typeof<uint32>, "uint32"
            typeof<int64>, "int64"
            typeof<uint64>, "uint64"
            typeof<obj>, "obj"
        ]

    let rec prettyName (t : Type) =
        match intrisicNames.TryGetValue t with
            | (true, n) -> n
            | _ -> 
                if t.IsArray then 
                    sprintf "%s[]" (t.GetElementType() |> prettyName)
                elif t.IsGenericType then
                    let args = t.GetGenericArguments() |> Seq.map prettyName |> String.concat ","
                    sprintf "%s<%s>" (cleanName t.Name) args
                else
                    cleanName t.Name

    [<OnAardvarkInit>]
    let checkSemanticCompleteness() =
        let sgTypes = Introspection.GetAllClassesImplementingInterface(typeof<ISg>)

        let sgModule = typeof<Sg.Set>.DeclaringType

        for (att, expected) in semantics do
            for t in sgTypes do
                if t.DeclaringType = sgModule then
                    match Ag.hasSynRule t expected att with
                    | true -> ()
                    | false -> Log.warn "no semantic %A for type %s" att (prettyName t)

        ()

