namespace Aardvark.SceneGraph

open Aardvark.Base
open FSharp.Data.Adaptive


[<AutoOpen>]
module Instancing =

    module Sg =
        let private convert (t : Trafo3d[]) =
            t |> Array.map (fun t -> M44f.op_Explicit t.Forward)

        type InstancingNode(count : aval<int>, uniforms : Map<string, BufferView>, child : aval<ISg>) =
            interface ISg
            member x.Count = count
            member x.Child = child
            member x.Uniforms = uniforms

        let instanced' (attributes : Map<string, System.Type * aval<System.Array>>) (sg : ISg) : ISg =
            if Map.isEmpty attributes then
                sg
            else
                let cnt = attributes |> Map.toSeq |> Seq.head |> snd |> snd |> AVal.map (fun a -> a.Length)
                let bufferViews = 
                    attributes |> Map.map (fun name (t,att) ->
                        let buffer = att |> AVal.map (fun a -> ArrayBuffer a :> IBuffer)
                        BufferView(buffer, t)
                    )
                InstancingNode(cnt, bufferViews, AVal.constant sg) :> ISg

        let instanced (trafos : aval<Trafo3d[]>) (sg : ISg) : ISg =
            let cnt = trafos |> AVal.map Array.length
            let view = BufferView(trafos |> AVal.map (fun t -> ArrayBuffer t :> IBuffer), typeof<Trafo3d>)
            InstancingNode(cnt, Map.ofList ["ModelTrafo", view], AVal.constant sg) :> ISg
            

namespace Aardvark.SceneGraph.Semantics
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Ag
open FSharp.Data.Adaptive
open Aardvark.SceneGraph


module Instancing = 
    open Microsoft.FSharp.Quotations


    module Effect =
        open FShade
        open FShade.Imperative

        let private cache = 
            System.Runtime.CompilerServices.ConditionalWeakTable<Effect, System.Collections.Generic.Dictionary<Set<string>, Effect>>()

        let private getOrCreate (uniforms : Set<string>) (e : Effect) (create : Set<string> -> Effect -> Effect) =
            lock cache (fun () ->
                match cache.TryGetValue e with
                    | (true, c) ->
                        match c.TryGetValue uniforms with
                            | (true, e) -> 
                                e
                            | _ ->
                                let res = create uniforms e
                                c.[uniforms] <- res
                                res
                    | _ ->
                        let c = System.Collections.Generic.Dictionary<Set<string>, Effect>()
                        let res = create uniforms e
                        c.[uniforms] <- res
                        cache.Add(e, c)
                        res
            )

        let inlineTrafo (uniforms : Set<string>) (e : Effect) =
            getOrCreate uniforms e (fun uniforms e ->
                let hasTrafo = Set.contains "ModelTrafo" uniforms
                e |> Effect.substituteUniforms (fun name typ index ->
                    match index with
                        | None ->
                            match name with
                                | "ModelTrafo"
                                | "ModelViewTrafo"
                                | "ModelViewProjTrafo" when hasTrafo ->
                                    let o : Expr<M44d> = Expr.ReadInput(ParameterKind.Uniform, typ, name) |> Expr.Cast
                                    let n : Expr<M44d> = Expr.ReadInput(ParameterKind.Input, typ, string DefaultSemantic.InstanceTrafo) |> Expr.Cast

                                    Some <@@  %o * %n @@>
                                                
                                | "ModelTrafoInv"
                                | "ModelViewTrafoInv"
                                | "ModelViewProjTrafoInv" when hasTrafo ->
                                    let o : Expr<M44d> = Expr.ReadInput(ParameterKind.Uniform, typ, name) |> Expr.Cast
                                    let n : Expr<M44d> = Expr.ReadInput(ParameterKind.Input, typ, string DefaultSemantic.InstanceTrafoInv) |> Expr.Cast

                                    Some <@@  %n * %o @@>

                                | "NormalMatrix" when hasTrafo ->
                                    let o : Expr<M33d> = Expr.ReadInput(ParameterKind.Uniform, typ, name) |> Expr.Cast
                                    let n : Expr<M44d> = Expr.ReadInput(ParameterKind.Input, typ, string DefaultSemantic.InstanceTrafoInv) |> Expr.Cast
                                                    
                                    Some <@@ %o * (m33d %n).Transposed @@>


                                | _ ->
                                    if Set.contains name uniforms then
                                        let e = Expr.ReadInput(ParameterKind.Input, typ, name)
                                        Some e
                                    else
                                        None
                        | _ ->
                            None
                )
            )
       

    [<Rule>]
    type InstancingSem() =

        static let bufferToM44f (inverse : bool) (b : IBuffer) =
            match b with
            | :? ArrayBuffer as b ->
                match b.Data with
                | :? array<M44f> as arr -> 
                    if inverse then arr |> Array.map (fun m -> m.Inverse)
                    else arr
                | :? array<M44d> as arr ->
                    if inverse then arr |> Array.map (fun m -> m.Inverse |> M44f.op_Explicit)
                    else arr |> Array.map M44f.op_Explicit
                | :? array<Trafo3d> as arr ->
                    if inverse then arr |> Array.map (fun m -> m.Backward |> M44f.op_Explicit)
                    else arr |> Array.map (fun m -> m.Forward |> M44f.op_Explicit)
                | arr ->
                    failwithf "unknown trafo type: %A" (arr.GetType().GetElementType())
            | _ ->
                failwithf "unknown buffer type: %A" b

        static let mergeFW (inner : Trafo3d) (instances : IBuffer) =
            bufferToM44f false instances
            |> Array.map (fun i ->
                M44d.op_Explicit i * inner.Forward |> M44f.op_Explicit
            ) 
            |> ArrayBuffer 
            :> IBuffer

        static let mergeBW (inner : Trafo3d) (instances : IBuffer) =
            bufferToM44f true instances 
            |> Array.map (fun i ->
                inner.Backward * M44d.op_Explicit i |> M44f.op_Explicit
            ) 
            |> ArrayBuffer 
            :> IBuffer

        static let instanceTrafoCache = 
            BinaryCache<aval<Trafo3d>, aval<IBuffer>, BufferView * BufferView>(fun inner instances ->
                let fw = AVal.map2 mergeFW inner instances
                let bw = AVal.map2 mergeBW inner instances
                BufferView(fw, typeof<M44f>), BufferView(bw, typeof<M44f>)
            )

        static let rec applyTrafos (uniforms : Map<string,BufferView>) (model : aval<Trafo3d>) (cnt : aval<int>) (o : IRenderObject) =
            
            match o with
                | :? RenderObject as o ->
                    let semantics = uniforms |> Map.toSeq |> Seq.map fst |> Set.ofSeq
                    let hasTrafo = Set.contains "ModelTrafo" semantics

                    let newEffect = 
                        match o.Surface with
                            | Surface.FShadeSimple e ->
                                Effect.inlineTrafo semantics e
                            | s ->
                                failwithf "[Sg] cannot instance object with surface: %A" s
                                
                    let newCall =
                        match o.DrawCalls with
                            | Direct dir -> 
                                Direct(
                                    AVal.map2 (fun l cnt -> 
                                        l |> List.map (fun (c : DrawCallInfo) ->
                                            if c.InstanceCount > 1 || c.FirstInstance <> 0 then
                                                failwithf "[Sg] cannot instance drawcall with %d instances" c.InstanceCount
                                            
                                            DrawCallInfo(
                                                FaceVertexCount = c.FaceVertexCount,
                                                FirstIndex = c.FirstIndex,
                                                FirstInstance = 0,
                                                InstanceCount = cnt,
                                                BaseVertex = c.BaseVertex
                                            )
                                        )
                                    ) dir cnt)
                            | _ ->
                                failwith "[Sg] cannot instance object with indirect buffer"
                                

                    let objectModel =
                        match o.Uniforms.TryGetUniform(Ag.Scope.Root, Symbol.Create "ModelTrafo") with
                            | Some (:? aval<Trafo3d> as inner) -> inner
                            | _ -> AVal.constant Trafo3d.Identity

                    let uniforms =
                        match Map.tryFind "ModelTrafo" uniforms with
                            | Some m ->
                                let fw, bw = instanceTrafoCache.Invoke(objectModel, m.Buffer)
                                uniforms
                                |> Map.remove "ModelTrafo"
                                |> Map.add "InstanceTrafo" fw
                                |> Map.add "InstanceTrafoInv" bw
                            | None ->
                                uniforms


                    let att =
                        { new IAttributeProvider with
                            member x.Dispose() = 
                                o.InstanceAttributes.Dispose()

                            member x.TryGetAttribute sem =
                                match Map.tryFind (string sem) uniforms with
                                    | Some v -> Some v
                                    | _ -> o.InstanceAttributes.TryGetAttribute sem

                            member x.All = Seq.empty
                        }

                    let vatt =
                        { new IAttributeProvider with
                            member x.Dispose() = 
                                o.VertexAttributes.Dispose()

                            member x.TryGetAttribute sem =
                                if Map.containsKey (string sem) uniforms then None
                                else o.VertexAttributes.TryGetAttribute sem

                            member x.All = Seq.empty
                        }

                    let newUniforms =
                        if hasTrafo then
                            UniformProvider.ofList [
                                "ModelTrafo", model :> IAdaptiveValue
                            ]
                        else 
                            UniformProvider.Empty

                    { o with 
                        Id = newId()
                        Surface = Surface.FShadeSimple newEffect
                        InstanceAttributes = att  
                        VertexAttributes = vatt  
                        DrawCalls = newCall
                        Uniforms = UniformProvider.union newUniforms o.Uniforms
                    } :> IRenderObject

                | :? MultiRenderObject as o ->
                    o.Children |> List.map (applyTrafos uniforms model cnt) |> MultiRenderObject :> IRenderObject

                | o ->
                    failwithf "[Sg] cannot instance object: %A" o

        member x.RenderObjects(n : Sg.InstancingNode, scope : Ag.Scope) : aset<IRenderObject> =
            let model : list<aval<Trafo3d>> = scope.ModelTrafoStack
            let model = TrafoSemantics.flattenStack model

            n.Child |> ASet.bind (fun c ->
                let objects : aset<IRenderObject> = c.RenderObjects(scope)
                objects |> ASet.map (fun ro ->
                    applyTrafos n.Uniforms model n.Count ro 
                )
            )

        member x.ModelTrafoStack(n : Sg.InstancingNode) : unit =
            n.Child?ModelTrafoStack <- List.empty<aval<Trafo3d>>

