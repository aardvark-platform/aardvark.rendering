namespace Aardvark.Rendering.CSharp

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open System.Runtime.CompilerServices

[<Struct>]
type ClearDepth =
    val Value : float32

    new (other : ClearDepth) = { Value = other.Value}

    new (value : float32) = { Value = value }
    new (value : float)   = { Value = float32 value }
    new (value : decimal) = { Value = float32 value }

    static member op_Implicit(value : float32)  = ClearDepth value
    static member op_Implicit(value : float)    = ClearDepth value
    static member op_Implicit(value : decimal)  = ClearDepth value

    static member op_Implicit(value : ClearDepth) = value.Value
    static member op_Explicit(value : ClearDepth) = float value.Value
    static member op_Explicit(value : ClearDepth) = decimal value.Value

[<Struct>]
type ClearStencil =
    val Value : uint32

    new (other : ClearStencil) = { Value = other.Value}

    new (value : uint8)  = { Value = uint32 value }
    new (value : uint16) = { Value = uint32 value }
    new (value : uint32) = { Value = value }
    new (value : int8)   = { Value = uint32 value }
    new (value : int16)  = { Value = uint32 value }
    new (value : int32)  = { Value = uint32 value }

    static member op_Implicit(value : uint8)  = ClearStencil value
    static member op_Implicit(value : uint16) = ClearStencil value
    static member op_Implicit(value : uint32) = ClearStencil value
    static member op_Implicit(value : int8)   = ClearStencil value
    static member op_Implicit(value : int16)  = ClearStencil value
    static member op_Implicit(value : int32)  = ClearStencil value

    static member op_Implicit(value : ClearStencil) = value.Value
    static member op_Explicit(value : ClearStencil) = int8 value.Value
    static member op_Explicit(value : ClearStencil) = int16 value.Value
    static member op_Explicit(value : ClearStencil) = int32 value.Value
    static member op_Explicit(value : ClearStencil) = uint8 value.Value
    static member op_Explicit(value : ClearStencil) = uint16 value.Value

[<AbstractClass; Sealed; Extension>]
type ClearDepthStencilExtensions private () =

    [<Extension>]
    static member inline ToFloat32(depth : aval<ClearDepth>) =
        depth |> AVal.mapNonAdaptive (fun d -> d.Value)

    [<Extension>]
    static member inline ToUInt32(stencil : aval<ClearStencil>) =
        stencil |> AVal.mapNonAdaptive (fun d -> d.Value)

[<AbstractClass; Sealed>]
type Clear private () =

    static member Empty = ClearValues.empty

    static member Color(value : ClearColor)  =
        ClearValues.empty |> ClearValues.color value

    static member Color(semantic : Symbol, value : ClearColor) =
        ClearValues.empty |> ClearValues.colorAttachment semantic value

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
        ClearValues.empty |> ClearValues.depth value.Value

    static member Stencil(value : ClearStencil) =
        ClearValues.empty |> ClearValues.stencil value.Value

[<AbstractClass; Sealed; Extension>]
type ClearValuesExtensions =

    [<Extension>]
    static member Color(values : ClearValues, color : ClearColor) =
        values |> ClearValues.color color

    [<Extension>]
    static member Color(values : ClearValues, semantic : Symbol, color : ClearColor) =
        values |> ClearValues.colorAttachment semantic color

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
        values |> ClearValues.depth depth.Value

    [<Extension>]
    static member Stencil(values : ClearValues, stencil : ClearStencil) =
        values |> ClearValues.stencil stencil.Value
