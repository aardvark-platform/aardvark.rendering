namespace Aardvark.Rendering.CSharp

open Aardvark.Base
open Aardvark.Rendering
open System.Runtime.CompilerServices

[<AbstractClass; Sealed>]
type Clear private () =

    static member Empty = ClearValues.empty

    static member Color(value : ClearColor)  =
        ClearValues.empty |> ClearValues.color value

    static member Color(semantic : Symbol, value : ClearColor) =
        ClearValues.empty |> ClearValues.color' semantic value

    static member Colors(values : Map<Symbol, ClearColor>) = ClearValues.empty |> ClearValues.colors values
    static member Colors(values : Map<Symbol, C4f>)  = ClearValues.empty |> ClearValues.colors values
    static member Colors(values : Map<Symbol, V4f>)  = ClearValues.empty |> ClearValues.colors values
    static member Colors(values : Map<Symbol, C4d>)  = ClearValues.empty |> ClearValues.colors values
    static member Colors(values : Map<Symbol, V4d>)  = ClearValues.empty |> ClearValues.colors values
    static member Colors(values : Map<Symbol, C4b>)  = ClearValues.empty |> ClearValues.colors values
    static member Colors(values : Map<Symbol, C4us>) = ClearValues.empty |> ClearValues.colors values
    static member Colors(values : Map<Symbol, C4ui>) = ClearValues.empty |> ClearValues.colors values
    static member Colors(values : Map<Symbol, V4i>)  = ClearValues.empty |> ClearValues.colors values

    static member Colors(values : seq<Symbol * ClearColor>) = ClearValues.empty |> ClearValues.colors (Map.ofSeq values)
    static member Colors(values : seq<Symbol * C4f>)  = ClearValues.empty |> ClearValues.colors (Map.ofSeq values)
    static member Colors(values : seq<Symbol * V4f>)  = ClearValues.empty |> ClearValues.colors (Map.ofSeq values)
    static member Colors(values : seq<Symbol * C4d>)  = ClearValues.empty |> ClearValues.colors (Map.ofSeq values)
    static member Colors(values : seq<Symbol * V4d>)  = ClearValues.empty |> ClearValues.colors (Map.ofSeq values)
    static member Colors(values : seq<Symbol * C4b>)  = ClearValues.empty |> ClearValues.colors (Map.ofSeq values)
    static member Colors(values : seq<Symbol * C4us>) = ClearValues.empty |> ClearValues.colors (Map.ofSeq values)
    static member Colors(values : seq<Symbol * C4ui>) = ClearValues.empty |> ClearValues.colors (Map.ofSeq values)
    static member Colors(values : seq<Symbol * V4i>)  = ClearValues.empty |> ClearValues.colors (Map.ofSeq values)

    static member Depth(value : ClearDepth) =
        ClearValues.empty |> ClearValues.depth value

    static member Stencil(value : ClearStencil) =
        ClearValues.empty |> ClearValues.stencil value

[<Extension; AbstractClass; Sealed>]
type ClearValuesExtensions =

    [<Extension>]
    static member Color(values : ClearValues, color : ClearColor) =
        values |> ClearValues.color color

    [<Extension>]
    static member Color(values : ClearValues, semantic : Symbol, color : ClearColor) =
        values |> ClearValues.color' semantic color

    [<Extension>]
    static member Colors(values : ClearValues, colors : Map<Symbol, ClearColor>) = values |> ClearValues.colors colors

    [<Extension>]
    static member Colors(values : ClearValues, colors : Map<Symbol, C4f>)  = values |> ClearValues.colors colors

    [<Extension>]
    static member Colors(values : ClearValues, colors : Map<Symbol, V4f>)  = values |> ClearValues.colors colors

    [<Extension>]
    static member Colors(values : ClearValues, colors : Map<Symbol, C4d>)  = values |> ClearValues.colors colors

    [<Extension>]
    static member Colors(values : ClearValues, colors : Map<Symbol, V4d>)  = values |> ClearValues.colors colors

    [<Extension>]
    static member Colors(values : ClearValues, colors : Map<Symbol, C4b>)  = values |> ClearValues.colors colors

    [<Extension>]
    static member Colors(values : ClearValues, colors : Map<Symbol, C4us>) = values |> ClearValues.colors colors

    [<Extension>]
    static member Colors(values : ClearValues, colors : Map<Symbol, C4ui>) = values |> ClearValues.colors colors

    [<Extension>]
    static member Colors(values : ClearValues, colors : Map<Symbol, V4i>)  = values |> ClearValues.colors colors

    [<Extension>]
    static member Colors(values : ClearValues, colors : seq<Symbol * ClearColor>) = values |> ClearValues.colors (Map.ofSeq colors)

    [<Extension>]
    static member Colors(values : ClearValues, colors : seq<Symbol * C4f>)  = values |> ClearValues.colors (Map.ofSeq colors)

    [<Extension>]
    static member Colors(values : ClearValues, colors : seq<Symbol * V4f>)  = values |> ClearValues.colors (Map.ofSeq colors)

    [<Extension>]
    static member Colors(values : ClearValues, colors : seq<Symbol * C4d>)  = values |> ClearValues.colors (Map.ofSeq colors)

    [<Extension>]
    static member Colors(values : ClearValues, colors : seq<Symbol * V4d>)  = values |> ClearValues.colors (Map.ofSeq colors)

    [<Extension>]
    static member Colors(values : ClearValues, colors : seq<Symbol * C4b>)  = values |> ClearValues.colors (Map.ofSeq colors)

    [<Extension>]
    static member Colors(values : ClearValues, colors : seq<Symbol * C4us>) = values |> ClearValues.colors (Map.ofSeq colors)

    [<Extension>]
    static member Colors(values : ClearValues, colors : seq<Symbol * C4ui>) = values |> ClearValues.colors (Map.ofSeq colors)

    [<Extension>]
    static member Colors(values : ClearValues, colors : seq<Symbol * V4i>)  = values |> ClearValues.colors (Map.ofSeq colors)

    [<Extension>]
    static member Depth(values : ClearValues, depth : ClearDepth) =
        values |> ClearValues.depth depth

    [<Extension>]
    static member Stencil(values : ClearValues, stencil : ClearStencil) =
        values |> ClearValues.stencil stencil
