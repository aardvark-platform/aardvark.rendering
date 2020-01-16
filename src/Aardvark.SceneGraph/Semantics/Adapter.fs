namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Ag
open Aardvark.SceneGraph
open Aardvark.Base.Rendering


[<AutoOpen>]
module AdapterSemantics =

    [<Semantic>]
    type AdapterSem() =
        member x.RenderObjects(a : Sg.AdapterNode) : aset<IRenderObject> =
            a.Node?RenderObjects()

        member x.GlobalBoundingBox(a : Sg.AdapterNode) : aval<Box3d> =
            a.Node?GlobalBoundingBox()

        member x.LocalBoundingBox(a : Sg.AdapterNode) : aval<Box3d> =
            a.Node?LocalBoundingBox()