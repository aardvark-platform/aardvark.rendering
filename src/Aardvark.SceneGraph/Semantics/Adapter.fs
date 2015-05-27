namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Ag
open Aardvark.Base.AgHelpers
open Aardvark.SceneGraph
open Aardvark.Base.Rendering


[<AutoOpen>]
module AdapterSemantics =

    [<Semantic>]
    type AdapterSem() =
        member x.RenderJobs(a : Sg.AdapterNode) : aset<RenderJob> =
            a.Node?RenderJobs()

        member x.GlobalBoundingBox(a : Sg.AdapterNode) : IMod<Box3d> =
            a.Node?GlobalBoundingBox()

        member x.LocalBoundingBox(a : Sg.AdapterNode) : IMod<Box3d> =
            a.Node?LocalBoundingBox()