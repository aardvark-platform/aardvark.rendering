namespace Aardvark.SceneGraph

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Ag
open Aardvark.SceneGraph
open Aardvark.Base.Rendering
open Aardvark.SceneGraph.Internal

open System.Runtime.CompilerServices

[<AutoOpen>]
module RuntimeSemantics =

    type ISg with
        member x.Runtime : IRuntime = x?Runtime
