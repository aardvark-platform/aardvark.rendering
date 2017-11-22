namespace Aardvark.SceneGraph

open Aardvark.Base
open Aardvark.Base.Incremental


[<AutoOpen>]
module Instancing =

    module Sg =
        let private convert (t : Trafo3d[]) =
            t |> Array.map (fun t -> M44f.op_Explicit t.Forward)

        type InstancingNode(trafos : IMod<M44f[]>, child : IMod<ISg>) =
            interface ISg

            member x.Trafos = trafos
            member x.Child = child


        let instanced (trafos : IMod<Trafo3d[]>) (sg : ISg) : ISg =
            InstancingNode(Mod.map convert trafos, Mod.constant sg) :> ISg

namespace Aardvark.SceneGraph.Semantics
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Ag
open Aardvark.Base.Incremental
open Aardvark.SceneGraph


module Instancing = 
    [<Semantic>]
    type InstancingSem() =

        static let merge (inner : Trafo3d) (instances : M44f[]) =
            instances 
            |> Array.map (fun i ->
                M44d.op_Explicit i * inner.Forward |> M44f.op_Explicit
            ) 
            |> ArrayBuffer 
            :> IBuffer

        static let instanceTrafoCache = 
            Cache2<IMod<Trafo3d>, IMod<M44f[]>, BufferView>(Ag.emptyScope, fun inner instances ->
                let data = Mod.map2 merge inner instances
                BufferView(data, typeof<M44f>)
            )

        static let rec applyTrafos (model : IMod<Trafo3d>) (m : IMod<M44f[]>) (o : IRenderObject) =
            match o with
                | :? RenderObject as o ->
                    let newSurface = 
                        match o.Surface with
                            | Surface.FShadeSimple e ->
                                FShade.Effect.compose [
                                    FShade.Effect.ofFunction DefaultSurfaces.instanceTrafo
                                    e
                                ]
                            | s ->
                                failwithf "[Sg] cannot instance object with surface: %A" s

                    let newCall =
                        match o.IndirectBuffer with
                            | null ->
                                Mod.map2 (fun l (m : M44f[]) -> 
                                    l |> List.map (fun (c : DrawCallInfo) ->
                                        if c.InstanceCount <> 1 || c.FirstInstance <> 0 then
                                            failwithf "[Sg] cannot instance drawcall with %d instances" c.InstanceCount

                                        DrawCallInfo(
                                            FaceVertexCount = c.FaceVertexCount,
                                            FirstIndex = c.FirstIndex,
                                            FirstInstance = 0,
                                            InstanceCount = m.Length,
                                            BaseVertex = c.BaseVertex
                                        )
                                    )
                                ) o.DrawCallInfos m
                            | _ ->
                                failwith "[Sg] cannot instance object with indirect buffer"
                                

                    let objectModel =
                        match o.Uniforms.TryGetUniform(Ag.emptyScope, Symbol.Create "ModelTrafo") with
                            | Some (:? IMod<Trafo3d> as inner) -> inner
                            | _ -> Mod.constant Trafo3d.Identity

                    //let buffer = m |> Mod.map2 (fun v -> ArrayBuffer(v) :> IBuffer) 
                    let view = instanceTrafoCache.Invoke(objectModel, m) //BufferView(buffer, typeof<M44f>)

                    let att =
                        { new IAttributeProvider with
                            member x.Dispose() = o.InstanceAttributes.Dispose()
                            member x.TryGetAttribute sem =
                                if sem = DefaultSemantic.InstanceTrafo then Some view
                                else o.InstanceAttributes.TryGetAttribute sem
                            member x.All = Seq.empty
                        }

                    let newUniforms =
                        UniformProvider.ofList [
                            "ModelTrafo", model :> IMod
                        ]

                    { o with 
                        Id = newId()
                        Surface = Surface.FShadeSimple newSurface
                        InstanceAttributes = att  
                        DrawCallInfos = newCall
                        Uniforms = UniformProvider.union newUniforms o.Uniforms
                    } :> IRenderObject

                | :? MultiRenderObject as o ->
                    o.Children |> List.map (applyTrafos model m) |> MultiRenderObject :> IRenderObject

                | o ->
                    failwithf "[Sg] cannot instance object: %A" o

        member x.RenderObjects(n : Sg.InstancingNode) : aset<IRenderObject> =
            let model : list<IMod<Trafo3d>> = n?ModelTrafoStack
            let model = TrafoSemantics.flattenStack model

            n.Child |> ASet.bind (fun c ->
                let objects : aset<IRenderObject> = c?RenderObjects()
                objects |> ASet.map (fun ro ->
                    applyTrafos model n.Trafos ro 
                )
            )

        member x.ModelTrafoStack(n : Sg.InstancingNode) : unit =
            n.Child?ModelTrafoStack <- List.empty<IMod<Trafo3d>>

