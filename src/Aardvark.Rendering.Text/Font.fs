namespace Aardvark.Rendering.Text

open System
open System.Collections.Concurrent
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.FontProvider

type PathSegment = Aardvark.Base.Fonts.PathSegment
type Path        = Aardvark.Base.Fonts.Path
type Shape       = Aardvark.Base.Fonts.Shape
type Font        = Aardvark.Base.Fonts.Font
type CodePoint   = Aardvark.Base.Fonts.CodePoint
type Glyph       = Aardvark.Base.Fonts.Glyph

module DefaultFonts =

    /// https://www.fontsquirrel.com/fonts/hack
    module Hack =

        /// Contains the types provided by Aardvark.FontProvider
        module Types =
            let [<Literal>] private family = "Hack"
            type Regular    = FontSquirrelProvider<Family = family, Bold = false, Italic = false>
            type Bold       = FontSquirrelProvider<Family = family, Bold = true,  Italic = false>
            type Italic     = FontSquirrelProvider<Family = family, Bold = false, Italic = true>
            type BoldItalic = FontSquirrelProvider<Family = family, Bold = true,  Italic = true>

        let Regular    = Types.Regular.Font
        let Bold       = Types.Bold.Font
        let Italic     = Types.Italic.Font
        let BoldItalic = Types.BoldItalic.Font

    /// https://www.fontsquirrel.com/fonts/courier-prime
    module CourierPrime =

        /// Contains the types provided by Aardvark.FontProvider
        module Types =
            let [<Literal>] private family = "Courier Prime"
            type Regular    = FontSquirrelProvider<Family = family, Bold = false, Italic = false>
            type Bold       = FontSquirrelProvider<Family = family, Bold = true,  Italic = false>
            type Italic     = FontSquirrelProvider<Family = family, Bold = false, Italic = true>
            type BoldItalic = FontSquirrelProvider<Family = family, Bold = true,  Italic = true>

        let Regular    = Types.Regular.Font
        let Bold       = Types.Bold.Font
        let Italic     = Types.Italic.Font
        let BoldItalic = Types.BoldItalic.Font

    /// https://www.fontsquirrel.com/fonts/noto-sans
    module NotoSans =

        /// Contains the types provided by Aardvark.FontProvider
        module Types =
            let [<Literal>] private family = "Noto Sans"
            type Regular    = FontSquirrelProvider<Family = family, Bold = false, Italic = false>
            type Bold       = FontSquirrelProvider<Family = family, Bold = true,  Italic = false>
            type Italic     = FontSquirrelProvider<Family = family, Bold = false, Italic = true>
            type BoldItalic = FontSquirrelProvider<Family = family, Bold = true,  Italic = true>

        let Regular    = Types.Regular.Font
        let Bold       = Types.Bold.Font
        let Italic     = Types.Italic.Font
        let BoldItalic = Types.BoldItalic.Font

[<AutoOpen>]
module ShapeExtensions =

    type Aardvark.Base.Fonts.Shape with
        member x.IndexedGeometry =
            IndexedGeometry(
                Mode = IndexedGeometryMode.TriangleList,
                IndexedAttributes =
                    SymDict.ofList [
                        DefaultSemantic.Positions, x.Geometry.Positions :> System.Array
                        Path.Attributes.KLMKind, x.Geometry.Coordinates :> System.Array
                    ]
            )

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Glyph =
        let inline indexedGeometry (g : Glyph) = g.IndexedGeometry


module FontRenderingSettings =
    let mutable DisableSampleShading = false


type ShapeCache(r : IRuntime) =
    static let cache = ConcurrentDictionary<IRuntime, Lazy<ShapeCache>>()

    let types =
        Map.ofList [
            DefaultSemantic.Positions, typeof<V3f>
            Path.Attributes.KLMKind, typeof<V4f>
        ]

    let pool = r.CreateGeometryPool(types)
    let ranges = ConcurrentDictionary<Shape, Range1i>()


    let surfaceCache = ConcurrentDictionary<IFramebufferSignature, Lazy<IBackendSurface>>()
    let boundarySurfaceCache = ConcurrentDictionary<IFramebufferSignature, Lazy<IBackendSurface>>()
    let billboardSurfaceCache = ConcurrentDictionary<IFramebufferSignature, Lazy<IBackendSurface>>()


    let pathShader =
        if FontRenderingSettings.DisableSampleShading then
            Path.Shader.pathFragmentNoSampleShading |> toEffect
        else
            Path.Shader.pathFragment |> toEffect

    let effect =
        FShade.Effect.compose [
            Path.Shader.pathVertex      |> toEffect
            //Path.Shader.pathTrafo       |> toEffect
            Path.Shader.depthBiasVs     |> toEffect
            pathShader
        ]

    let instancedEffect =
        FShade.Effect.compose [
            Path.Shader.pathVertexInstanced |> toEffect
            Path.Shader.depthBiasVs         |> toEffect
            pathShader
        ]

    let boundaryEffect =
        FShade.Effect.compose [
            Path.Shader.boundaryVertex  |> toEffect
            Path.Shader.boundary        |> toEffect
        ]

    let instancedBoundaryEffect =
        FShade.Effect.compose [
            DefaultSurfaces.instanceTrafo   |> toEffect
            Path.Shader.boundaryVertex      |> toEffect
            Path.Shader.boundary            |> toEffect
        ]

    let billboardEffect =
        FShade.Effect.compose [
            Path.Shader.pathVertexBillboard |> toEffect
            Path.Shader.depthBiasVs         |> toEffect
            pathShader
        ]

    let instancedBillboardEffect =
        FShade.Effect.compose [
            Path.Shader.pathVertexInstancedBillboard |> toEffect
            Path.Shader.depthBiasVs         |> toEffect
            pathShader
        ]

    let surface (s : IFramebufferSignature) =
        surfaceCache.GetOrAdd(s, fun s ->
            lazy (
                r.PrepareEffect(
                    s, [
                        Path.Shader.pathVertex      |> toEffect
                        pathShader
                    ]
                )
            )
        ).Value

    let boundarySurface (s : IFramebufferSignature) =
        boundarySurfaceCache.GetOrAdd(s, fun s ->
            lazy (
                r.PrepareEffect(
                    s, [
                        Path.Shader.boundaryVertex  |> toEffect
                        Path.Shader.boundary        |> toEffect
                    ]
                )
            )
        ).Value

    let billboardSurface (s : IFramebufferSignature) =
        billboardSurfaceCache.GetOrAdd(s, fun s ->
            lazy (
                r.PrepareEffect(
                    s, [
                        Path.Shader.pathVertexBillboard  |> toEffect
                        Path.Shader.boundary        |> toEffect
                    ]
                )
            )
        ).Value


    do
        r.OnDispose.Add(fun () ->
            for x in boundarySurfaceCache.Values do r.DeleteSurface x.Value
            for x in surfaceCache.Values do r.DeleteSurface x.Value
            for x in billboardSurfaceCache.Values do r.DeleteSurface x.Value
            pool.Dispose()
            ranges.Clear()
            cache.Clear()
        )

    let vertexBuffers =
        { new IAttributeProvider with
            member x.All = Seq.empty
            member x.TryGetAttribute(sem) =
                match pool.TryGetBufferView sem with
                    | Some bufferView ->
                        Some bufferView
                    | None ->
                        None

            member x.Dispose() = ()
        }

    static member GetOrCreateCache(r : IRuntime) =
        cache.GetOrAdd(r, fun r ->
            lazy (new ShapeCache(r))
        ).Value

    member x.Effect = effect
    member x.InstancedEffect = instancedEffect
    member x.BoundaryEffect = boundaryEffect
    member x.InstancedBoundaryEffect = instancedBoundaryEffect
    member x.BillboardEffect = billboardEffect
    member x.InstancedBillboardEffect = instancedBillboardEffect
    member x.Surface s = surface s
    member x.BoundarySurface s = boundarySurface s
    member x.BillboardSurface s = billboardSurface s
    member x.VertexBuffers = vertexBuffers

    member x.GetBufferRange(shape : Shape) =
        ranges.GetOrAdd(shape, fun shape ->
            let ptr = pool.Alloc(shape.IndexedGeometry)
            let last = ptr.Offset + ptr.Size - 1n |> int
            let first = ptr.Offset |> int
            Range1i(first, last)
        )

    member x.PrepareShaders(signature : IFramebufferSignature) =
        let _ = surface signature
        let _ = boundarySurface signature
        ()


    member x.Dispose() =
        pool.Dispose()
        ranges.Clear()

[<AbstractClass; Sealed; Extension>]
type PrepareFontExtensions private() =

    [<Extension>]
    static member PrepareGlyphs(runtime: IRuntime, font: Font, chars: seq<CodePoint>) =
        let cache = ShapeCache.GetOrCreateCache runtime

        for c in chars do
            cache.GetBufferRange (font.GetGlyph(c)) |> ignore

    [<Extension>]
    static member PrepareGlyphs(runtime: IRuntime, font: Font, chars: seq<char>) =
        let cache = ShapeCache.GetOrCreateCache runtime

        for c in chars do
            cache.GetBufferRange (font.GetGlyph(CodePoint c)) |> ignore

    [<Extension>]
    static member PrepareTextShaders(runtime: IRuntime, signature: IFramebufferSignature) =
        let cache = ShapeCache.GetOrCreateCache runtime
        cache.PrepareShaders signature
