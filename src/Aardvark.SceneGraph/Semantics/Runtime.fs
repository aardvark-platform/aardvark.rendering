namespace Aardvark.SceneGraph

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Ag
open Aardvark.SceneGraph
open Aardvark.Base.Rendering
open Aardvark.SceneGraph.Internal

open System.Runtime.CompilerServices

[<AutoOpen>]
module RuntimeSemantics =

    type Ag.Scope with
        member x.Runtime : IRuntime = x?Runtime
