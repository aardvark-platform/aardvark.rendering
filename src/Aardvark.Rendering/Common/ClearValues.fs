namespace Aardvark.Rendering

open Aardvark.Base

[<Struct; CLIMutable>]
type ClearColor =
    {
        Float   : V4f
        Integer : V4i
    }

    static member Create(value : ClearColor) = value

    // Conversion from floating point values
    static member Create(value : V2f)          = { Float = V4f value; Integer = V4i value }
    static member Create(value : V3f)          = { Float = V4f value; Integer = V4i value }
    static member Create(value : V4f)          = { Float = V4f value; Integer = V4i value }
    static member Create(value : V2d)          = { Float = V4f value; Integer = V4i value }
    static member Create(value : V3d)          = { Float = V4f value; Integer = V4i value }
    static member Create(value : V4d)          = { Float = V4f value; Integer = V4i value }
    static member Create(value : C3f)          = { Float = V4f value; Integer = V4i (V4f value) }
    static member Create(value : C4f)          = { Float = V4f value; Integer = V4i (V4f value) }
    static member Create(value : C3d)          = { Float = V4f value; Integer = V4i (V4f value) }
    static member Create(value : C4d)          = { Float = V4f value; Integer = V4i (V4f value) }
    static member op_Implicit(value : V2f)     = ClearColor.Create(value)
    static member op_Implicit(value : V3f)     = ClearColor.Create(value)
    static member op_Implicit(value : V4f)     = ClearColor.Create(value)
    static member op_Implicit(value : V2d)     = ClearColor.Create(value)
    static member op_Implicit(value : V3d)     = ClearColor.Create(value)
    static member op_Implicit(value : V4d)     = ClearColor.Create(value)
    static member op_Implicit(value : C3f)     = ClearColor.Create(value)
    static member op_Implicit(value : C4f)     = ClearColor.Create(value)
    static member op_Implicit(value : C3d)     = ClearColor.Create(value)
    static member op_Implicit(value : C4d)     = ClearColor.Create(value)

    // Conversion from uint8 values
    static member Create(value : C3b)        = { Float = V4f (C4f value); Integer = V4i value }
    static member Create(value : C4b)        = { Float = V4f (C4f value); Integer = V4i value }
    static member op_Implicit(value : C3b)   = ClearColor.Create(value)
    static member op_Implicit(value : C4b)   = ClearColor.Create(value)

    // Conversion from uint16 values
    static member Create(value : C3us)        = { Float = V4f (C4f value); Integer = V4i value }
    static member Create(value : C4us)        = { Float = V4f (C4f value); Integer = V4i value }
    static member op_Implicit(value : C3us)   = ClearColor.Create(value)
    static member op_Implicit(value : C4us)   = ClearColor.Create(value)

    // Conversion from uint32 values
    static member Create(value : C3ui)        = { Float = V4f (C4f value); Integer = V4i(int value.R, int value.G, int value.B, int System.UInt32.MaxValue) }
    static member Create(value : C4ui)        = { Float = V4f (C4f value); Integer = V4i(int value.R, int value.G, int value.B, int value.A) }
    static member op_Implicit(value : C3ui)   = ClearColor.Create(value)
    static member op_Implicit(value : C4ui)   = ClearColor.Create(value)

    // Conversion from int32 values
    static member Create(value : V2i)        = { Float = V4f value; Integer = V4i value }
    static member Create(value : V3i)        = { Float = V4f value; Integer = V4i value }
    static member Create(value : V4i)        = { Float = V4f value; Integer = V4i value }
    static member op_Implicit(value : V2i)   = ClearColor.Create(value)
    static member op_Implicit(value : V3i)   = ClearColor.Create(value)
    static member op_Implicit(value : V4i)   = ClearColor.Create(value)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ClearColor =

    [<AutoOpen>]
    module private Aux =
        let inline createAux (_ : ^Z) (x : ^T) =
            ((^Z or ^T) : (static member Create : ^T -> ClearColor) x)

    let zero =
        { Float = V4f.Zero; Integer = V4i.Zero }

    let inline create (value : ^T) =
        createAux Unchecked.defaultof<ClearColor> value


[<CLIMutable>]
type ClearColors =
    {
        Default     : Option<ClearColor>
        Attachments : Map<Symbol, ClearColor>
    }

    member x.Item(semantic : Symbol) =
        match x.Attachments |> Map.tryFind semantic with
        | None -> x.Default
        | color -> color

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ClearColors =

    let empty =
        { Default     = None
          Attachments = Map.empty }

    /// Creates clear colors for a single color.
    let inline single (color : ^T) =
        { empty with Default = color |> ClearColor.create |> Some }

    /// Tries to get the clear color for the given attachment.
    let tryGet (semantic : Symbol) (colors : ClearColors) =
        colors.[semantic]

    /// Sets a default clear color.
    let inline set (color : ^Color) (colors : ClearColors) =
        { colors with Default = color |> ClearColor.create |> Some }

    /// Sets a clear color for the attachment with the given name.
    let inline set' (semantic : Symbol) (color : ^Color) (colors : ClearColors) =
        { colors with Attachments = colors.Attachments |> Map.add semantic (ClearColor.create color) }

    /// Sets clear colors for the given attachments.
    let inline setMany (values : Map<Symbol, ^Color>) (colors : ClearColors) =
        (colors, values) ||> Map.fold (fun s sem c -> s |> set' sem c)


[<Struct; CLIMutable>]
type ClearDepth =
    { Value : float32 }

    static member Create(value : ClearDepth) = value

    static member Create(value : float32)       = { Value = value }
    static member op_Implicit(value : float32)  = ClearDepth.Create(value)

    static member Create(value : float)         = { Value = float32 value }
    static member op_Implicit(value : float)    = ClearDepth.Create(value)

    static member Create(value : decimal)       = { Value = float32 value }
    static member op_Implicit(value : decimal)  = ClearDepth.Create(value)

    static member op_Implicit(value : ClearDepth) = value.Value
    static member op_Explicit(value : ClearDepth) = float value.Value
    static member op_Explicit(value : ClearDepth) = decimal value.Value

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ClearDepth =

    [<AutoOpen>]
    module private Aux =
        let inline createAux (_ : ^Z) (x : ^T) =
            ((^Z or ^T) : (static member Create : ^T -> ClearDepth) x)

    let inline create (value : ^T) =
        createAux Unchecked.defaultof<ClearDepth> value


[<Struct; CLIMutable>]
type ClearStencil =
    { Value : uint32 }

    static member Create(value : ClearStencil) = value

    static member Create(value : uint8)       = { Value = uint32 value }
    static member op_Implicit(value : uint8)  = ClearStencil.Create(value)

    static member Create(value : uint16)      = { Value = uint32 value }
    static member op_Implicit(value : uint16) = ClearStencil.Create(value)

    static member Create(value : uint32)      = { Value = value }
    static member op_Implicit(value : uint32) = ClearStencil.Create(value)

    static member Create(value : int8)        = { Value = uint32 value }
    static member op_Implicit(value : int8)   = ClearStencil.Create(value)

    static member Create(value : int16)       = { Value = uint32 value }
    static member op_Implicit(value : int16)  = ClearStencil.Create(value)

    static member Create(value : int32)       = { Value = uint32 value }
    static member op_Implicit(value : int32)  = ClearStencil.Create(value)

    static member op_Implicit(value : ClearStencil) = value.Value
    static member op_Explicit(value : ClearStencil) = int8 value.Value
    static member op_Explicit(value : ClearStencil) = int16 value.Value
    static member op_Explicit(value : ClearStencil) = int32 value.Value
    static member op_Explicit(value : ClearStencil) = uint8 value.Value
    static member op_Explicit(value : ClearStencil) = uint16 value.Value


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ClearStencil =

    [<AutoOpen>]
    module private Aux =
        let inline createAux (_ : ^Z) (x : ^T) =
            ((^Z or ^T) : (static member Create : ^T -> ClearStencil) x)

    let inline create (value : ^T) =
        createAux Unchecked.defaultof<ClearStencil> value

[<CLIMutable>]
type ClearValues =
    {
        Colors  : ClearColors
        Depth   : Option<ClearDepth>
        Stencil : Option<ClearStencil>
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ClearValues =

    let empty =
        { Colors  = ClearColors.empty
          Depth   = None
          Stencil = None }

    /// Sets a default clear color.
    let inline color (color : ^Color) (values : ClearValues) =
        { values with Colors = values.Colors |> ClearColors.set color }

    /// Sets a clear color for the attachment with the given name.
    let inline color' (semantic : Symbol) (color : ^Color) (values : ClearValues) =
        { values with Colors = values.Colors |> ClearColors.set' semantic color }

    /// Sets clear colors for the given attachments.
    let inline colors (colors : Map<Symbol, ^Color>) (values : ClearValues) =
        { values with Colors = values.Colors |> ClearColors.setMany colors }

    /// Sets a depth clear value.
    let inline depth (value : ^Depth) (values : ClearValues) =
        { values with Depth = Some (ClearDepth.create value) }

    /// Sets a stencil clear value.
    let inline stencil (value : ^Stencil) (values : ClearValues) =
        { values with Stencil = Some (ClearStencil.create value) }

[<AutoOpen>]
module ``Clear Utlities`` =

    /// Builder for computational expressions defining clear values
    type ClearValuesBuilder() =

        member x.Yield(()) =
            ClearValues.empty

        [<CustomOperation("color")>]
        member inline x.Color(s : ClearValues, color : ^Color) =
            s |> ClearValues.color color

        member inline x.Color(s : ClearValues, (semantic : Symbol, color : ^Color)) =
            s |> ClearValues.color' semantic color

        [<CustomOperation("colors")>]
        member inline x.Colors(s : ClearValues, colors : Map<Symbol, ^Color>) =
            s |> ClearValues.colors colors

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