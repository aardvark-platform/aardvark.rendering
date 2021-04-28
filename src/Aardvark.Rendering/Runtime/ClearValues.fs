namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators

[<Struct; RequireQualifiedAccess>]
type ClearColors =
    | Varying of Values: aval<Map<Symbol, C4f>>
    | Uniform of Value: aval<C4f>

    member x.GetValues(signature : IFramebufferSignature) =
        match x with
        | Varying values -> values
        | Uniform value ->
            let attachments =
                signature.ColorAttachments |> Map.toList |> List.map (snd >> fst)

            value |> AVal.map (fun c -> attachments |> List.map (fun sem -> sem, c) |> Map.ofList)

    static member None =
        Varying ~~Map.empty


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ClearColors =

    let none = ClearColors.None

    let inline private cast (map : Map<Symbol, ^Color>) =
        if typeof< ^Color> = typeof<C4f> then
            unbox map
        else
            map |> Map.map (fun _ c -> c4f c)

    let inline ofMap (values : aval<Map<Symbol, ^Color>>) =
        let map = values |> AVal.map cast
        ClearColors.Varying map

    let inline ofMap' (values : Map<Symbol, ^Color>) =
        let map = values |> cast
        ClearColors.Varying ~~map

    let inline ofSeq (values : aval<seq<Symbol * ^Color>>) =
        let map = values |> AVal.map (Map.ofSeq >> cast)
        ClearColors.Varying map

    let inline ofSeq' (values : seq<Symbol * ^Color>) =
        let map = values |> Map.ofSeq |> cast
        ClearColors.Varying ~~map

    let inline ofList (values : aval<list<Symbol * ^Color>>) =
        let map = values |> AVal.map (Map.ofList >> cast)
        ClearColors.Varying map

    let inline ofList' (values : list<Symbol * ^Color>) =
        let map = values |> Map.ofList |> cast
        ClearColors.Varying ~~map

    let inline ofArray (values : aval<array<Symbol * ^Color>>) =
        let map = values |> AVal.map (Map.ofArray >> cast)
        ClearColors.Varying map

    let inline ofArray' (values : array<Symbol * ^Color>) =
        let map = values |> Map.ofArray |> cast
        ClearColors.Varying ~~map

    let inline uniform (value : aval< ^Color>) =
        ClearColors.Uniform (value |> AVal.mapNonAdaptive c4f)

    let inline uniform' (value : ^Color) =
        ClearColors.Uniform ~~(c4f value)

    let toMap (signature : IFramebufferSignature) (colors : ClearColors) =
        colors.GetValues(signature)


/// Type representing depth or stencil values used for clearing depth-stencil attachments of framebuffers.
[<Struct; StructuralEquality; NoComparison>]
type ClearValue<'T>(value : aval<'T option>) =

    new(value : aval<'T>) =
        ClearValue<'T>(value |> AVal.map Some)

    new(value : 'T) =
        ClearValue<'T>(~~(Some value))

    member x.Value = value

    static member None = ClearValue<'T>(~~None)


type ClearDepth   = ClearValue<float>

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ClearDepth =

    let inline option (value : aval< ^Value option>) =
        ClearDepth (value |> AVal.mapNonAdaptive (Option.map float))

    let inline value (value : aval< ^Value>) =
        ClearDepth (value |> AVal.mapNonAdaptive float)

    let inline value' (value : ^Value) =
        ClearDepth (float value)


type ClearStencil = ClearValue<int>

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ClearStencil =

    let inline option (value : aval< ^Value option>) =
        ClearStencil (value |> AVal.mapNonAdaptive (Option.map int))

    let inline value (value : aval< ^Value>) =
        ClearStencil (value |> AVal.mapNonAdaptive int)

    let inline value' (value : ^Value) =
        ClearStencil (int value)


/// Type representing color, depth, and stencil values used for clearing framebuffer attachments.
[<Struct>]
type ClearValues =
    {
        mutable Colors  : ClearColors
        mutable Depth   : ClearDepth
        mutable Stencil : ClearStencil
    }

    static member None =
        { Colors  = ClearColors.None
          Depth   = ClearDepth.None
          Stencil = ClearStencil.None }


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ClearValues =

    let none = ClearValues.None

    let create (colors : ClearColors) (depth : ClearDepth) (stencil : ClearStencil) =
        { Colors = colors; Depth = depth; Stencil = stencil }

    let ofColors (colors : ClearColors) =
        { ClearValues.None with Colors = colors }

    let ofDepth (depth : ClearDepth) =
        { ClearValues.None with Depth = depth }

    let ofStencil (stencil : ClearStencil) =
        { ClearValues.None with Stencil = stencil }

    let ofColorsAndDepth (colors : ClearColors) (depth : ClearDepth)  =
        { ClearValues.None with Colors = colors; Depth = depth }

    let ofColorsAndStencil (colors : ClearColors) (stencil : ClearStencil)  =
        { ClearValues.None with Colors = colors; Stencil = stencil }

    let ofDepthAndStencil (depth : ClearDepth) (stencil : ClearStencil)  =
        { ClearValues.None with Depth = depth; Stencil = stencil }

    let colors (colors : ClearColors) (values : ClearValues) =
        { values with Colors = colors }

    let depth (depth : ClearDepth) (values : ClearValues) =
        { values with Depth = depth }

    let stencil (stencil : ClearStencil) (values : ClearValues) =
        { values with Stencil = stencil }


[<AutoOpen>]
module ``Clear Utlities`` =

    /// Builder for computational expressions defining clear values
    type ClearValuesBuilder() =

        member x.Yield(()) =
            ClearValues.None

        [<CustomOperation("colors")>]
        member x.Colors(s : ClearValues, values : ClearColors) =
            s |> ClearValues.colors values

        member inline x.Colors(s : ClearValues, values : aval<Map<Symbol, ^Color>>) =
            s |> ClearValues.colors (ClearColors.ofMap values)

        member inline x.Colors(s : ClearValues, values : aval<seq<Symbol * ^Color>>) =
            s |> ClearValues.colors (ClearColors.ofSeq values)

        member inline x.Colors(s : ClearValues, values : aval<list<Symbol * ^Color>>) =
            s |> ClearValues.colors (ClearColors.ofList values)

        member inline x.Colors(s : ClearValues, values : aval<array<Symbol * ^Color>>) =
            s |> ClearValues.colors (ClearColors.ofArray values)

        member inline x.Colors(s : ClearValues, values : Map<Symbol, ^Color>) =
            s |> ClearValues.colors (ClearColors.ofMap' values)

        member inline x.Colors(s : ClearValues, values : seq<Symbol * ^Color>) =
            s |> ClearValues.colors (ClearColors.ofSeq' values)

        member inline x.Colors(s : ClearValues, values : list<Symbol * ^Color>) =
            s |> ClearValues.colors (ClearColors.ofList' values)

        member inline x.Colors(s : ClearValues, values : array<Symbol * ^Color>) =
            s |> ClearValues.colors (ClearColors.ofArray' values)

        [<CustomOperation("color")>]
        member inline x.Color(s : ClearValues, value : aval< ^Color>) =
            s |> ClearValues.colors (ClearColors.uniform value)

        member inline x.Color(s : ClearValues, value : ^Color) =
            s |> ClearValues.colors (ClearColors.uniform' value)

        [<CustomOperation("depth")>]
        member x.Depth(s : ClearValues, value : ClearDepth) =
            s |> ClearValues.depth value

        member inline x.Depth(s : ClearValues, value : aval< ^Value option>) =
            s |> ClearValues.depth (ClearDepth.option value)

        member inline x.Depth(s : ClearValues, value : aval< ^Value>) =
            s |> ClearValues.depth (ClearDepth.value value)

        member inline x.Depth(s : ClearValues, value : ^Value) =
            s |> ClearValues.depth (ClearDepth.value' value)

        [<CustomOperation("stencil")>]
        member x.Stencil(s : ClearValues, value : ClearStencil) =
            s |> ClearValues.stencil value

        member inline x.Stencil(s : ClearValues, value : aval< ^Value option>) =
            s |> ClearValues.stencil (ClearStencil.option value)

        member inline x.Stencil(s : ClearValues, value : aval< ^Value>) =
            s |> ClearValues.stencil (ClearStencil.value value)

        member inline x.Stencil(s : ClearValues, value : ^Value) =
            s |> ClearValues.stencil (ClearStencil.value' value)


    /// Computational expression to define clear values
    let clear = ClearValuesBuilder()