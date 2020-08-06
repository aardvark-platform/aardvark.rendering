namespace Aardvark.SceneGraph

open Aardvark.Base
open Aardvark.Base.Ag
open Aardvark.Rendering

[<AutoOpen>]
module RuntimeSemantics =

    type Ag.Scope with
        member x.Runtime : IRuntime = x?Runtime
