namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open FSharp.Data.Adaptive

[<AutoOpen>]
module DelaySemantics =

    [<Rule>]
    type DelaySem() =
        member x.RenderObjects(n : Sg.DelayNode, scope : Ag.Scope) : aset<IRenderObject> =
            let sg = n.Generator scope
            sg.RenderObjects scope

        member x.GlobalBoundingBox(n : Sg.DelayNode, scope : Ag.Scope) : aval<Box3d> =
            let sg = n.Generator scope
            sg.GlobalBoundingBox(scope)

        member x.LocalBoundingBox(n : Sg.DelayNode, scope : Ag.Scope) : aval<Box3d> =
            let sg = n.Generator scope
            sg.LocalBoundingBox(scope)