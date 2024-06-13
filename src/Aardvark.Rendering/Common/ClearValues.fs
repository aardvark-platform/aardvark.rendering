namespace Aardvark.Rendering

open System
open Aardvark.Base

/// Struct representing a clear color.
/// Holds a floating point and integer representation, one of which is used based on the format of the target.
[<Struct>]
type ClearColor =
    val Float   : V4f
    val Integer : V4i

    new (other : ClearColor) = { Float = other.Float; Integer = other.Integer}

    // Conversion from floating point values
    new (value : V2f) = { Float = V4f value; Integer = V4i value }
    new (value : V3f) = { Float = V4f value; Integer = V4i value }
    new (value : V4f) = { Float = V4f value; Integer = V4i value }
    new (value : V2d) = { Float = V4f value; Integer = V4i value }
    new (value : V3d) = { Float = V4f value; Integer = V4i value }
    new (value : V4d) = { Float = V4f value; Integer = V4i value }
    new (value : C3f) = { Float = V4f value; Integer = V4i (V4f value) }
    new (value : C4f) = { Float = V4f value; Integer = V4i (V4f value) }
    new (value : C3d) = { Float = V4f value; Integer = V4i (V4f value) }
    new (value : C4d) = { Float = V4f value; Integer = V4i (V4f value) }

    static member inline op_Implicit(value : V2f) = ClearColor(value)
    static member inline op_Implicit(value : V3f) = ClearColor(value)
    static member inline op_Implicit(value : V4f) = ClearColor(value)
    static member inline op_Implicit(value : V2d) = ClearColor(value)
    static member inline op_Implicit(value : V3d) = ClearColor(value)
    static member inline op_Implicit(value : V4d) = ClearColor(value)
    static member inline op_Implicit(value : C3f) = ClearColor(value)
    static member inline op_Implicit(value : C4f) = ClearColor(value)
    static member inline op_Implicit(value : C3d) = ClearColor(value)
    static member inline op_Implicit(value : C4d) = ClearColor(value)

    // Conversion from uint8 values
    new (value : C3b) = { Float = V4f (C4f value); Integer = V4i value }
    new (value : C4b) = { Float = V4f (C4f value); Integer = V4i value }

    static member inline op_Implicit(value : C3b) = ClearColor(value)
    static member inline op_Implicit(value : C4b) = ClearColor(value)

    // Conversion from uint16 values
    new (value : C3us) = { Float = V4f (C4f value); Integer = V4i value }
    new (value : C4us) = { Float = V4f (C4f value); Integer = V4i value }

    static member inline op_Implicit(value : C3us) = ClearColor(value)
    static member inline op_Implicit(value : C4us) = ClearColor(value)

    // Conversion from uint32 values
    new (value : C3ui) = { Float = V4f (C4f value); Integer = V4i(int value.R, int value.G, int value.B, int System.UInt32.MaxValue) }
    new (value : C4ui) = { Float = V4f (C4f value); Integer = V4i(int value.R, int value.G, int value.B, int value.A) }
    new (value : V2ui) = { Float = V4f value; Integer = V4i value }
    new (value : V3ui) = { Float = V4f value; Integer = V4i value }
    new (value : V4ui) = { Float = V4f value; Integer = V4i value }

    static member inline op_Implicit(value : C3ui) = ClearColor(value)
    static member inline op_Implicit(value : C4ui) = ClearColor(value)
    static member inline op_Implicit(value : V2ui) = ClearColor(value)
    static member inline op_Implicit(value : V3ui) = ClearColor(value)
    static member inline op_Implicit(value : V4ui) = ClearColor(value)

    // Conversion from int32 values
    new (value : V2i) = { Float = V4f value; Integer = V4i value }
    new (value : V3i) = { Float = V4f value; Integer = V4i value }
    new (value : V4i) = { Float = V4f value; Integer = V4i value }

    static member inline op_Implicit(value : V2i) = ClearColor(value)
    static member inline op_Implicit(value : V3i) = ClearColor(value)
    static member inline op_Implicit(value : V4i) = ClearColor(value)

/// Record holding color, depth and stencil values for clearing operations.
[<CLIMutable>]
type ClearValues =
    {
        /// Default clear color for any attachment.
        Color   : ClearColor option

        /// Per attachment clear colors, overriding the default clear color if present.
        Colors  : Map<Symbol, ClearColor>

        /// Depth clear value.
        Depth   : float32 option

        // Stencil clear value.
        Stencil : uint32 option
    }

    /// Returns the effective clear color for the given color attachment if there is any.
    member inline x.Item(semantic : Symbol) : ClearColor option =
        match x.Colors |> Map.tryFind semantic with
        | None -> x.Color
        | color -> color

[<AutoOpen>]
module ClearValuesFSharp =

    module ClearValues =

        module Conversion =

            [<Sealed>]
            type Converter private () =
                static member inline ToClearColor(value : ClearColor) = value

                // Conversion from floating point values
                static member inline ToClearColor(value : V2f)  = ClearColor value
                static member inline ToClearColor(value : V3f)  = ClearColor value
                static member inline ToClearColor(value : V4f)  = ClearColor value
                static member inline ToClearColor(value : V2d)  = ClearColor value
                static member inline ToClearColor(value : V3d)  = ClearColor value
                static member inline ToClearColor(value : V4d)  = ClearColor value
                static member inline ToClearColor(value : C3f)  = ClearColor value
                static member inline ToClearColor(value : C4f)  = ClearColor value
                static member inline ToClearColor(value : C3d)  = ClearColor value
                static member inline ToClearColor(value : C4d)  = ClearColor value

                // Conversion from uint8 values
                static member inline ToClearColor(value : C3b)  = ClearColor value
                static member inline ToClearColor(value : C4b)  = ClearColor value

                // Conversion from uint16 values
                static member inline ToClearColor(value : C3us) = ClearColor value
                static member inline ToClearColor(value : C4us) = ClearColor value

                // Conversion from uint32 values
                static member inline ToClearColor(value : C3ui) = ClearColor value
                static member inline ToClearColor(value : C4ui) = ClearColor value
                static member inline ToClearColor(value : V2ui) = ClearColor value
                static member inline ToClearColor(value : V3ui) = ClearColor value
                static member inline ToClearColor(value : V4ui) = ClearColor value

                // Conversion from int32 values
                static member inline ToClearColor(value : V2i)  = ClearColor value
                static member inline ToClearColor(value : V3i)  = ClearColor value
                static member inline ToClearColor(value : V4i)  = ClearColor value

                // Conversion to depth
                static member inline ToDepth(value : float32) = value
                static member inline ToDepth(value : float)   = float32 value
                static member inline ToDepth(value : decimal) = float32 value

                // Conversion to stencil
                static member inline ToStencil(value : uint8)  = uint32 value
                static member inline ToStencil(value : uint16) = uint32 value
                static member inline ToStencil(value : uint32) = value
                static member inline ToStencil(value : int8)   = uint32 value
                static member inline ToStencil(value : int16)  = uint32 value
                static member inline ToStencil(value : int32)  = uint32 value

            [<AutoOpen>]
            module private Aux =
                let inline colorAux (_ : ^Z) (x : ^T)   = ((^Z or ^T) : (static member ToClearColor : ^T -> ClearColor) x)
                let inline depthAux (_ : ^Z) (x : ^T)   = ((^Z or ^T) : (static member ToDepth : ^T -> float32) x)
                let inline stencilAux (_ : ^Z) (x : ^T) = ((^Z or ^T) : (static member ToStencil : ^T -> uint32) x)

            let inline toColor (value : ^T)   = colorAux Unchecked.defaultof<Converter> value
            let inline toDepth (value : ^T)   = depthAux Unchecked.defaultof<Converter> value
            let inline toStencil (value : ^T) = stencilAux Unchecked.defaultof<Converter> value

        /// Empty clear values.
        let empty =
            { Color   = None
              Colors  = Map.empty
              Depth   = None
              Stencil = None }

        /// Creates clear values of a single color.
        let inline ofColor (color : ^T) =
            { empty with Color = color |> Conversion.toColor |> Some }

        /// Returns the effective clear color for the given color attachment if there is any.
        let inline tryGetColor (semantic : Symbol) (values : ClearValues) =
            values.[semantic]

        /// Sets a default clear color.
        let inline color (color : ^Color) (values : ClearValues) =
            { values with Color = Some <| Conversion.toColor color }

        /// Sets a clear color for the color attachment with the given name.
        let inline colorAttachment (semantic : Symbol) (color : ^Color) (values : ClearValues) =
            { values with Colors = values.Colors |> Map.add semantic (Conversion.toColor color) }

        /// Sets clear colors for the given color attachments.
        let inline colors (colors : Map<Symbol, ^Color>) (values : ClearValues) =
            let colors = (values.Colors, colors) ||> Map.fold (fun s sem c -> s |> Map.add sem (Conversion.toColor c))
            { values with Colors = colors }

        /// Sets a depth clear value.
        let inline depth (value : ^Depth) (values : ClearValues) =
            { values with Depth = Some (Conversion.toDepth value) }

        /// Sets a stencil clear value.
        let inline stencil (value : ^Stencil) (values : ClearValues) =
            { values with Stencil = Some (Conversion.toStencil value) }

[<AutoOpen>]
module ``Clear Utlities`` =

    /// Builder for computational expressions defining clear values
    type ClearValuesBuilder() =

        member x.Yield(()) =
            ClearValues.empty

        [<CustomOperation("color")>]
        member inline x.Color(s : ClearValues, color : ^Color) =
            s |> ClearValues.color color

        [<CustomOperation("color")>]
        member inline x.Color(s : ClearValues, semantic : Symbol, color : ^Color) =
            s |> ClearValues.colorAttachment semantic color

        [<CustomOperation("colors")>]
        member inline x.Colors(s : ClearValues, colors : Map<Symbol, ^Color>) =
            s |> ClearValues.colors colors

        [<CustomOperation("colors")>]
        member inline x.Colors(s : ClearValues, colors : seq<Symbol * ^Color>) =
            s |> ClearValues.colors (Map.ofSeq colors)

        [<CustomOperation("depth")>]
        member inline x.Depth(s : ClearValues, value : ^Depth) =
            s |> ClearValues.depth value

        [<CustomOperation("stencil")>]
        member inline x.Stencil(s : ClearValues, value : ^Stencil) =
            s |> ClearValues.stencil value

    /// Computational expression to define clear values
    let clear = ClearValuesBuilder()