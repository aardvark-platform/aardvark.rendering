namespace Aardvark.Rendering.Effects

open Aardvark.Base
open Aardvark.Rendering
open FShade

type Vertex = {
    [<Position>]        pos     : V4f
    [<WorldPosition>]   wp      : V4f
    [<Normal>]          n       : V3f
    [<BiNormal>]        b       : V3f
    [<Tangent>]         t       : V3f
    [<Color>]           c       : V4f
    [<TexCoord>]        tc      : V2f
}