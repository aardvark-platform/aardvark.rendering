﻿namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open Aardvark.Base.Ag
open Aardvark.Rendering
open Aardvark.SceneGraph
open FSharp.Data.Adaptive

[<AutoOpen>]
module AdapterSemantics =

    [<Rule>]
    type AdapterSem() =
        member x.RenderObjects(a : Sg.AdapterNode, scope : Ag.Scope) : aset<IRenderObject> =
            a.Node?RenderObjects(scope)

        member x.GlobalBoundingBox(a : Sg.AdapterNode, scope : Ag.Scope) : aval<Box3d> =
            a.Node?GlobalBoundingBox(scope) 

        member x.LocalBoundingBox(a : Sg.AdapterNode, scope : Ag.Scope) : aval<Box3d> =
            a.Node?LocalBoundingBox(scope)