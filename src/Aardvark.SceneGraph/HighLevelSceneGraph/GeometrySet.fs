namespace Aardvark.SceneGraph

open System
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive

type GeometryInstance =
    {
        Geometry           : IndexedGeometry
        InstanceAttributes : Map<Symbol, IAdaptiveValue>
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module GeometryInstance =

    let ofIndexedGeometry (instanceAttributes : Map<Symbol, IAdaptiveValue>) (geometry : IndexedGeometry) =
        { Geometry = geometry
          InstanceAttributes = instanceAttributes }

[<AutoOpen>]
module GeometrySetSgExtensions =

    module Sg =
        type GeometrySetNode(signature : GeometrySignature, mode : IndexedGeometryMode, geometries : aset<GeometryInstance>) =
            interface ISg
            member x.Signature = signature
            member x.Mode = mode
            member x.Geometries = geometries

        /// Draws an adaptive set of indexed geometries with instance attributes.
        let geometrySetInstanced (signature : GeometrySignature) (mode : IndexedGeometryMode) (geometries : aset<GeometryInstance>) =
            GeometrySetNode(signature, mode, geometries) :> ISg

        /// Draws an adaptive set of indexed geometries.
        let geometrySet (mode : IndexedGeometryMode) (attributeTypes : Map<Symbol, Type>) (geometries : aset<IndexedGeometry>) =
            let signature =
                { IndexType              = typeof<int>
                  VertexAttributeTypes   = attributeTypes
                  InstanceAttributeTypes = Map.empty }

            let geometries =
                geometries |> ASet.map (GeometryInstance.ofIndexedGeometry Map.empty)

            geometries |> geometrySetInstanced signature mode


namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open FSharp.Data.Adaptive
open ``Pool Semantics``

module GeometrySetSemantics =

    [<AutoOpen>]
    module private Utilities =

        let trySub (b : Box3d) (d : Box3d) =
            if d.Min.AllGreater b.Min && d.Max.AllSmaller b.Max then
                Some b
            else
                None

        let computeBoundingBox (g : GeometryInstance) =
            match g.Geometry.IndexedAttributes.TryGetValue DefaultSemantic.Positions with
            | (true, arr) ->
                match arr with
                | :? array<V3f> as arr -> Box3f(arr) |> Box3d.op_Explicit
                | :? array<V4f> as arr -> Box3f(arr |> Array.map Vec.xyz) |> Box3d.op_Explicit
                | _ -> failwithf "unknown position-type: %A" arr
            | _ ->
                Box3d.Invalid

    [<Rule>]
    type GeometrySetSem() =

        member x.LocalBoundingBox(r : Sg.GeometrySetNode, scope : Ag.Scope) : aval<Box3d> =
            r.Geometries
            |> ASet.map computeBoundingBox
            |> ASet.foldHalfGroup (curry Box.Union) trySub Box3d.Invalid

        member x.GlobalBoundingBox(r : Sg.GeometrySetNode, scope : Ag.Scope) : aval<Box3d> =
            let l = r.LocalBoundingBox(scope)
            let t = scope.ModelTrafo
            AVal.map2 (fun (t : Trafo3d) (b : Box3d) -> b.Transformed(t)) t l

        member x.RenderObjects(node : Sg.GeometrySetNode, scope : Ag.Scope) : aset<IRenderObject> =
            let pool = scope.Runtime.CreateManagedPool node.Signature    // Disposed via activate of RO

            let calls =
                node.Geometries |> ASet.mapUse (fun g ->
                    let ag = g.Geometry |> AdaptiveGeometry.ofIndexedGeometry (Map.toList g.InstanceAttributes)
                    pool.Add ag
                )
                |> snd
                |> DrawCallBuffer.create pool.Runtime BufferStorage.Device

            let mutable ro = Unchecked.defaultof<RenderObject>
            ro <- RenderObject.ofScope scope
            ro.Mode <- node.Mode
            ro.Indices <- Some pool.IndexBuffer
            ro.VertexAttributes <- pool.VertexAttributes
            ro.InstanceAttributes <- pool.InstanceAttributes
            ro.DrawCalls <- Indirect calls
            ro.Activate <- fun () -> pool

            ASet.single (ro :> IRenderObject)