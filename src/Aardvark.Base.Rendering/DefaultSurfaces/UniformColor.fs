namespace Aardvark.Base.Rendering.Effects

open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open FShade
open DefaultSurfaceVertex

module UniformColor =
 
    let internal uniformColor (c : IMod<C4f>) (v : Vertex) =
        let c = c |> Mod.map (fun col -> col.ToV4d())
        fragment {
            return !!c
        }

    let Effect (c : IMod<C4f>) = 
        toEffect (uniformColor c)

