namespace Aardvark.Base.Rendering.Effects

open Aardvark.Base
open Aardvark.Base.Rendering
open FShade
open Microsoft.FSharp.Quotations

type Vertex = {
    [<Position>]        pos     : V4d
    [<WorldPosition>]   wp      : V4d
    [<Normal>]          n       : V3d
    [<BiNormal>]        b       : V3d
    [<Tangent>]         t       : V3d
    [<Color>]           c       : V4d
    [<TexCoord>]        tc      : V2d
}