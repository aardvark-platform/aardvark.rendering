namespace Aardvark.SceneGraph

open System
open System.Runtime.InteropServices
open System.Collections.Generic

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Ag
open Aardvark.Base.Rendering

module Sg =

    [<AbstractClass>]
    type AbstractApplicator(child : IMod<ISg>) =
        interface IApplicator with
            member x.Child = child

        member x.Child = child

        new(child : ISg) = AbstractApplicator(Mod.constant child)

    type AdapterNode(node : obj) =
        interface ISg

        member x.Node = node

    type DynamicNode(child : IMod<ISg>) = inherit AbstractApplicator(child)

    type RenderNode(call : IMod<DrawCallInfo>, mode : IndexedGeometryMode) =
        interface ISg

        member x.Mode = mode
        member x.DrawCallInfo = call

        new(call : DrawCallInfo, mode : IndexedGeometryMode) = RenderNode(Mod.constant call, mode)
        new(count : int, mode : IndexedGeometryMode) = 
            let call = 
                DrawCallInfo(
                    FaceVertexCount = count,
                    InstanceCount = 1,
                    FirstIndex = 0,
                    FirstInstance = 0,
                    BaseVertex = 0
                )
            RenderNode(Mod.constant call, mode)

        new(count : IMod<int>, mode : IndexedGeometryMode) = 
            let call =
                count |> Mod.map (fun x ->
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

    type IndirectRenderNode(buffer : IMod<IIndirectBuffer>, mode : IndexedGeometryMode) =
        interface ISg

        member x.Mode = mode
        member x.Indirect = buffer

    type VertexAttributeApplicator(values : Map<Symbol, BufferView>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Values = values

        new(values : Map<Symbol, BufferView>, child : ISg)            = VertexAttributeApplicator(values, Mod.constant child)
        new(semantic : Symbol, value : BufferView, child : IMod<ISg>) = VertexAttributeApplicator(Map.ofList [semantic, value], child)
        new(semantic : Symbol, value : BufferView, child : ISg)       = VertexAttributeApplicator(Map.ofList [semantic, value], Mod.constant child)
        new(values : SymbolDict<BufferView>, child : ISg)             = VertexAttributeApplicator(values |> Seq.map (fun (KeyValue(k,v)) -> k,v) |> Map.ofSeq, Mod.constant child)

    type VertexIndexApplicator(value : BufferView, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Value = value

        new(value : BufferView, child : ISg)         = VertexIndexApplicator(value, Mod.constant child)

    type InstanceAttributeApplicator(values : Map<Symbol, BufferView>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Values = values

        new(values : Map<Symbol, BufferView>, child : ISg)            = InstanceAttributeApplicator(values, Mod.constant child)
        new(semantic : Symbol, value : BufferView, child : IMod<ISg>) = InstanceAttributeApplicator(Map.ofList [semantic, value], child)
        new(semantic : Symbol, value : BufferView, child : ISg)       = InstanceAttributeApplicator(Map.ofList [semantic, value], Mod.constant child)
        new(values : SymbolDict<BufferView>, child : ISg)             = InstanceAttributeApplicator(values |> Seq.map (fun (KeyValue(k,v)) -> k,v) |> Map.ofSeq, Mod.constant child)
 

    type OnOffNode(on : IMod<bool>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.IsActive = on

        new(on : IMod<bool>, child : ISg) = OnOffNode(on, Mod.constant child)

    type PassApplicator(pass : RenderPass, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Pass = pass

        new(pass : RenderPass, child : ISg) = PassApplicator(pass, Mod.constant child)

    type UniformApplicator(uniformHolder : IUniformProvider, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member internal x.Uniforms = uniformHolder
        
        member x.TryFindUniform (scope : Scope) (name : Symbol) =
            uniformHolder.TryGetUniform (scope,name)

        new(value : IUniformProvider, child : ISg) = UniformApplicator( value, Mod.constant child)
        new(name : string, value : IMod, child : ISg) = UniformApplicator( (new Providers.SimpleUniformHolder ([Symbol.Create name,value]) :> IUniformProvider), Mod.constant child)
        new(name : Symbol, value : IMod, child : ISg) = UniformApplicator( (new Providers.SimpleUniformHolder( [name,value]) :> IUniformProvider), Mod.constant child)
        new(name : Symbol, value : IMod, child : IMod<ISg>) = UniformApplicator( (new Providers.SimpleUniformHolder( [name,value]) :> IUniformProvider), child)
        new(map : Map<Symbol,IMod>, child : ISg) = UniformApplicator( (new Providers.SimpleUniformHolder( map) :> IUniformProvider), Mod.constant child)


    type SurfaceApplicator(surface : Surface, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Surface = surface

        new(value : Surface, child : ISg) = SurfaceApplicator(value, Mod.constant child)

        new(value : ISurface, child : ISg) = SurfaceApplicator(Surface.Backend value, Mod.constant child)
        new(value : ISurface, child : IMod<ISg>) = SurfaceApplicator(Surface.Backend value, child)

    type TextureApplicator(semantic : Symbol, texture : IMod<ITexture>, child : IMod<ISg>) =
        inherit UniformApplicator(semantic, texture :> IMod, child)

        member x.Texture = texture

        new(semantic : Symbol, texture : IMod<ITexture>, child : ISg) = TextureApplicator(semantic, texture, Mod.constant child)
        new(texture : IMod<ITexture>, child : ISg) = TextureApplicator(DefaultSemantic.DiffuseColorTexture, texture, child)
        new(texture : IMod<ITexture>, child : IMod<ISg>) = TextureApplicator(DefaultSemantic.DiffuseColorTexture,texture,child)


    type TrafoApplicator(trafo : IMod<Trafo3d>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Trafo = trafo

        new(value : IMod<Trafo3d>, child : ISg) = TrafoApplicator(value, Mod.constant child)
        new(value : Trafo3d, child : ISg) = TrafoApplicator(Mod.constant value, Mod.constant child)
    
    type ViewTrafoApplicator(trafo : IMod<Trafo3d>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.ViewTrafo = trafo

        new(value : IMod<Trafo3d>, child : ISg) = ViewTrafoApplicator(value, Mod.constant child)

    type ProjectionTrafoApplicator(trafo : IMod<Trafo3d>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.ProjectionTrafo = trafo

        new(value : IMod<Trafo3d>, child : ISg) = ProjectionTrafoApplicator(value, Mod.constant child)


    type DepthTestModeApplicator(mode : IMod<DepthTestMode>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mode = mode

        new(mode : IMod<DepthTestMode>, child : ISg) = DepthTestModeApplicator(mode, Mod.constant child)

     type DepthBiasApplicator(bias : IMod<DepthBiasState>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.State = bias

        new(state : IMod<DepthBiasState>, child : ISg) = DepthBiasApplicator(state, Mod.constant child)

    type CullModeApplicator(mode : IMod<CullMode>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mode = mode

        new(value : IMod<CullMode>, child : ISg) = CullModeApplicator(value, Mod.constant child)

    type FrontFaceApplicator(winding : IMod<WindingOrder>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.WindingOrder = winding

        new(winding : IMod<WindingOrder>, child : ISg) = FrontFaceApplicator(winding, Mod.constant child)

    type FillModeApplicator(mode : IMod<FillMode>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mode = mode

        new(value : IMod<FillMode>, child : ISg) = FillModeApplicator(value, Mod.constant child)
        new(value : FillMode, child : ISg) = FillModeApplicator(Mod.constant value, Mod.constant child)

    type StencilModeApplicator(mode : IMod<StencilMode>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mode = mode

        new(value : IMod<StencilMode>, child : ISg) = StencilModeApplicator(value, Mod.constant child)

    type BlendModeApplicator(mode : IMod<BlendMode>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.Mode = mode

        new(value : IMod<BlendMode>, child : ISg) = BlendModeApplicator(value, Mod.constant child)

    type RasterizerStateApplicator(state : IMod<RasterizerState>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        let depth = state |> Mod.map (fun s -> s.DepthTest)
        let bias = state |> Mod.map (fun s -> s.DepthBias)
        let cull = state |> Mod.map (fun s -> s.CullMode)
        let front = state |> Mod.map (fun s -> s.FrontFace)
        let fill = state |> Mod.map (fun s -> s.FillMode)
        let stencil = state |> Mod.map (fun s -> s.StencilMode)
        let blend = state |> Mod.map (fun s -> s.BlendMode)

        member x.RasterizerState = state
        member x.DepthTestMode = depth
        member x.DepthBias = bias
        member x.CullMode = cull
        member x.FrontFace = front
        member x.FillMode = fill
        member x.StencilMode = stencil
        member x.BlendMode = blend

        new(value : IMod<RasterizerState>, child : ISg) = RasterizerStateApplicator(value, Mod.constant child)

    type WriteBuffersApplicator(buffers : Option<Set<Symbol>>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)
        member x.WriteBuffers = buffers
        new(buffers : Option<Set<Symbol>>, child : ISg) = WriteBuffersApplicator(buffers, Mod.constant child)
        
    type ConservativeRasterApplicator(state : IMod<bool>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)
        member x.ConservativeRaster = state

    type MultisampleApplicator(state : IMod<bool>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)
        member x.Multisample = state


    type ColorWriteMaskApplicator(maskRgba : IMod<bool*bool*bool*bool>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)
        member x.MaskRgba = maskRgba

    type DepthWriteMaskApplicator(writeEnabled : IMod<bool>, child : IMod<ISg>) =
        inherit AbstractApplicator(child)
        member x.WriteEnabled = writeEnabled

    type Group(content : cset<ISg>) =

        interface IGroup with
            member x.Children = x.ASet

        member x.ASet : aset<ISg> = content :> aset<_>

        member x.Add v =
            transact (fun () ->
               content.Add v
            )

        member x.Remove v =
            transact (fun () ->
                content.Remove v
            )

        member x.Clear() =
            transact (fun () ->
                content.Clear()
            )

        member x.UnionWith v =
            transact (fun () ->
                content.UnionWith v
            )

        member x.ExceptWith v =
            transact (fun () ->
                content.ExceptWith v
            )

        member x.SymmetricExceptWith v =
            transact (fun () ->
                content.SymmetricExceptWith v
            )

        member x.IntersectWith v =
            transact (fun () ->
                content.IntersectWith v
            )


        member x.Count = content.Count

        interface System.Collections.IEnumerable with
            member x.GetEnumerator() = (content :> System.Collections.IEnumerable).GetEnumerator()

        interface IEnumerable<ISg> with
            member x.GetEnumerator() = (content :> seq<_>).GetEnumerator()

        interface ICollection<ISg> with
            member x.IsReadOnly = false
            member x.Add v = x.Add v |> ignore
            member x.Remove v = x.Remove v
            member x.Clear() = x.Clear()
            member x.Contains v = content.Contains v
            member x.Count = x.Count
            member x.CopyTo(arr, index) =
                let mutable id = index
                for e in content do
                    arr.[id] <- e
                    id <- id + 1

        interface ISet<ISg> with
            member x.Add v = x.Add v
            member x.UnionWith other = x.UnionWith other
            member x.IntersectWith other = x.IntersectWith other
            member x.ExceptWith other = x.ExceptWith other
            member x.SymmetricExceptWith other = x.SymmetricExceptWith other
            member x.IsSubsetOf other = (content :> ISet<ISg>).IsSubsetOf other
            member x.IsSupersetOf other = (content :> ISet<ISg>).IsSupersetOf other
            member x.IsProperSubsetOf other = (content :> ISet<ISg>).IsProperSubsetOf other
            member x.IsProperSupersetOf other = (content :> ISet<ISg>).IsProperSupersetOf other
            member x.Overlaps other = (content :> ISet<ISg>).Overlaps other
            member x.SetEquals other = (content :> ISet<ISg>).SetEquals other

        new() = Group(CSet.empty)

        new([<ParamArray>] items: ISg[]) = Group(items |> CSet.ofArray)

        new(items : seq<ISg>) = Group(items |> CSet.ofSeq)
        
    type Set(content : aset<ISg>) =

        interface IGroup with
            member x.Children = content

        member x.ASet = content

        new([<ParamArray>] items: ISg[]) = Set(items |> ASet.ofArray)

        new(items : seq<ISg>) = Set(items |> ASet.ofSeq)

    type AsyncLoadApplicator(fboSignature : IFramebufferSignature, child : IMod<ISg>) =
        inherit AbstractApplicator(child)

        member x.FramebufferSignature = fboSignature

    type OverlayNode(task : IRenderTask) =
        interface ISg
        member x.RenderTask = task

    type GeometrySet(geometries : aset<IndexedGeometry>, mode : IndexedGeometryMode, attributeTypes : Map<Symbol,Type>) =
        interface ISg
        member x.Geometries = geometries
        member x.Mode = mode
        member x.AttributeTypes = attributeTypes


module SceneGraphCompletenessCheck =
    open System.Text.RegularExpressions

    let semantics =
        [
            "RenderObjects"
            "GlobalBoundingBox"
            "LocalBoundingBox"
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

        let sgModule = typeof<Sg.Group>.DeclaringType

        for att in semantics do
            let semTypes = HashSet<Type>()
            for t in sgTypes do
                if t.DeclaringType = sgModule then
                    match t |> Ag.tryGetAttributeType att with
                        | Some attType ->
                            semTypes.Add attType |> ignore
                        | None ->
                            Log.warn "no semantic %A for type %s" att (prettyName t)

            if semTypes.Count > 1 then
                let allTypes = semTypes |> Seq.map prettyName |> String.concat "; "
                Log.warn "conflicting types for semantic functions %A [%s]" att allTypes


        ()

