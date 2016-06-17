namespace Aardvark.Base.Rendering

open Aardvark.Base
open Aardvark.Base.Incremental
open FShade
open Microsoft.FSharp.Quotations
open DefaultSurfaceVertex

module UniformColor =
 
    let uniformColor (c : IMod<C4f>) (v : Vertex) =
        let c = c |> Mod.map (fun col -> col.ToV4d())
        fragment {
            return !!c
        }

    let Effect (c : IMod<C4f>) = 
        toEffect (uniformColor c)

