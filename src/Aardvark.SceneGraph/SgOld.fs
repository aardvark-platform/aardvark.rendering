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

module SgExtensions =

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



[<AutoOpen>]
module RuntimeExtensions =


    type IRuntime with

        member x.CompileRender (e : Sg.Environment) =
            let jobs : aset<RenderJob> = e?RenderJobs()
            x.CompileRender(jobs)

[<AutoOpen>]
module Semantics =
    open Sg

    [<AutoOpen>]
    module SemanticAccessors =
    
        type ISg with
            member x.RenderJobs() : aset<RenderJob> = x?RenderJobs()
            member x.ModelTrafo : IMod<Trafo3d> = x?ModelTrafo
            member x.ViewTrafo : IMod<Trafo3d> = x?ViewTrafo



    [<Semantic>]
    type UniformSem() =
        member x.Uniforms(e : Root) =
            e.Child?Uniforms <- ([] : list<IUniformProvider>)

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
            
            rj.AttributeScope <- scope 
            rj.Indices <- let index  = r?VertexIndexArray in if index = Aardvark.SceneGraph.Semantics.AttributeSemantics.EmptyIndex then null else index 
            
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

//    [<Semantic>]
//    type LodSem() =
//        member x.RenderJobs(node : LodNode) : aset<RenderJob> =
//
//            let mvTrafo = node?ModelViewTrafo()
//            aset {
//                let scope = Ag.getContext()
//
//                let! highSg,lowSg = node.High,node.Low
//
//                let lowJobs = lowSg?RenderJobs() : aset<RenderJob> 
//                let highJobs = highSg?RenderJobs() : aset<RenderJob>
//
//                //this parallel read is absolutely crucial for performance, since otherwise the 
//                //resulting set will no longer be referentially equal (cannot really be solved any other way)
//                //once more we see that adaptive code is extremely sensible.
//                let! camLocation,trafo = node?CameraLocation,mvTrafo
//
//                if node.ViewDecider { trafo = trafo; cameraPosition = camLocation; scope = scope } then 
//                    yield! highJobs
//                else    
//                    yield! lowJobs
//            }

    let private bbCache = ConditionalWeakTable<RenderJob, IMod<Box3d>>()
    type RenderJob with
        member x.GetBoundingBox() =
            match bbCache.TryGetValue x with
                | (true, v) -> v
                | _ ->
                    let v = 
                        match x.VertexAttributes.TryGetAttribute DefaultSemantic.Positions with
                            | Some v ->
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

//    [<Semantic>]
//    type CullNodeSem() =
//        member x.RenderJobs(c : ViewFrustumCullNode) :  aset<RenderJob>=
//            let intersectsFrustum (b : Box3d) (f : Trafo3d) =
//                b.IntersectsFrustum(f.Forward)
//            
//            aset {
//
//                let! child = c.Child
//                let jobs = child?RenderJobs() : aset<RenderJob>
//
//                let viewProjTrafo = c?ViewProjTrafo() : IMod<Trafo3d>
//
//                yield! jobs |> ASet.filterM (fun rj -> Mod.map2 intersectsFrustum (rj.GetBoundingBox()) viewProjTrafo)
////
////                for rj : RenderJob in jobs do
////                    let! viewProjTrafo = c?ViewProjTrafo() : Mod<Trafo3d>
////                    let! bb = rj.GetBoundingBox().Mod
////                    if intersectsFrustum bb viewProjTrafo 
////                    then yield rj
//            }


    [<Semantic>]
    type Derived() =

        let trueM = Mod.initConstant true
        let falseM = Mod.initConstant false

        member x.HasDiffuseColorTexture(sg : ISg) = 
            let uniforms : IUniformProvider list = sg?Uniforms 
            match uniforms |> List.tryPick (fun uniforms -> uniforms.TryGetUniform (Ag.getContext(), Symbol.Create("DiffuseColorTexture"))) with
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
                app.ASet |> ASet.map (fun sg -> sg.GlobalBoundingBox() ) 
                    |> ASet.toMod 
                    |> Mod.bind (fun (values : ISet<IMod<Box3d>>) -> (values |> Seq.map (fun a -> a :> IAdaptiveObject) |> Seq.toList) |> Mod.mapCustom (fun () -> Box3d ( values |> Seq.map Mod.force) ) )

            member x.GlobalBoundingBox(n : IApplicator) : IMod<Box3d> = 
                adaptive {
                    let! low = n.Child
                    1
                    return! low?GlobalBoundingBox()
                }

//            member x.GlobalBoundingBox(n : LodNode) : IMod<Box3d> = 
//                adaptive {
//                    let! low = n.Low
//                    return! low?GlobalBoundingBox()
//                }

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
                app.ASet |> ASet.map (fun sg -> sg?LocalBoundingBox() ) |> ASet.toMod |> Mod.map (fun (values : ISet<Box3d>) -> Box3d ( values ) )

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
                    1
                    return! low?LocalBoundingBox()
                }
//            member x.LocalBoundingBox(n : LodNode) : IMod<Box3d> = 
//                adaptive {
//                    let! low = d
//                    return! low?LocalBoundingBox()
//                }
    
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


            member x.NormalMatrix(s : ISg) : IMod<M33d> = 
                Mod.map (fun (t : Trafo3d) -> t.Backward.Transposed.UpperLeftM33()) s?ModelTrafo

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
