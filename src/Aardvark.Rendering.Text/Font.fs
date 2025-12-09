namespace Aardvark.Rendering.Text

open System
open System.Collections.Concurrent
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Rendering

type PathSegment = Aardvark.Base.Fonts.PathSegment
type Path        = Aardvark.Base.Fonts.Path
type Shape       = Aardvark.Base.Fonts.Shape
type Font        = Aardvark.Base.Fonts.Font
type FontStyle   = Aardvark.Base.Fonts.FontStyle
type CodePoint   = Aardvark.Base.Fonts.CodePoint
type Glyph       = Aardvark.Base.Fonts.Glyph

module DefaultFonts =

    module Types =

        /// https://www.fontsquirrel.com/fonts/hack
        type Hack internal() =
            let regular    = lazy Font.LoadFromAssembly("hack.zip", "Hack-Regular.ttf")
            let bold       = lazy Font.LoadFromAssembly("hack.zip", "Hack-Bold.ttf")
            let italic     = lazy Font.LoadFromAssembly("hack.zip", "Hack-Italic.ttf")
            let boldItalic = lazy Font.LoadFromAssembly("hack.zip", "Hack-BoldItalic.ttf")

            static let instance = Hack()
            static member internal Instance = instance

            member _.Regular         : Font = regular.Value
            member _.Bold            : Font = bold.Value
            member _.Italic          : Font = italic.Value
            member _.BoldItalic      : Font = boldItalic.Value

        /// https://www.fontsquirrel.com/fonts/courier-prime
        type CourierPrime internal() =
            let regular    = lazy Font.LoadFromAssembly("courier-prime.zip", "Courier Prime.ttf")
            let bold       = lazy Font.LoadFromAssembly("courier-prime.zip", "Courier Prime Bold.ttf")
            let italic     = lazy Font.LoadFromAssembly("courier-prime.zip", "Courier Prime Italic.ttf")
            let boldItalic = lazy Font.LoadFromAssembly("courier-prime.zip", "Courier Prime Bold Italic.ttf")

            static let instance = CourierPrime()
            static member internal Instance = instance

            member _.Regular         : Font = regular.Value
            member _.Bold            : Font = bold.Value
            member _.Italic          : Font = italic.Value
            member _.BoldItalic      : Font = boldItalic.Value

        /// https://www.fontsquirrel.com/fonts/noto-sans
        [<Sealed>]
        type NotoSans internal() =
            let regular    = lazy Font.LoadFromAssembly("noto-sans.zip", "NotoSans-Regular.ttf")
            let bold       = lazy Font.LoadFromAssembly("noto-sans.zip", "NotoSans-Bold.ttf")
            let italic     = lazy Font.LoadFromAssembly("noto-sans.zip", "NotoSans-Italic.ttf")
            let boldItalic = lazy Font.LoadFromAssembly("noto-sans.zip", "NotoSans-BoldItalic.ttf")

            static let instance = NotoSans()
            static member internal Instance = instance

            member _.Regular         : Font = regular.Value
            member _.Bold            : Font = bold.Value
            member _.Italic          : Font = italic.Value
            member _.BoldItalic      : Font = boldItalic.Value

    /// https://www.fontsquirrel.com/fonts/hack
    [<Sealed; AbstractClass>]
    type Hack =
        static member Regular    : Font = Types.Hack.Instance.Regular
        static member Bold       : Font = Types.Hack.Instance.Bold
        static member Italic     : Font = Types.Hack.Instance.Italic
        static member BoldItalic : Font = Types.Hack.Instance.BoldItalic

    /// https://www.fontsquirrel.com/fonts/courier-prime
    [<Sealed; AbstractClass>]
    type CourierPrime internal() =
        static member Regular    : Font = Types.CourierPrime.Instance.Regular
        static member Bold       : Font = Types.CourierPrime.Instance.Bold
        static member Italic     : Font = Types.CourierPrime.Instance.Italic
        static member BoldItalic : Font = Types.CourierPrime.Instance.BoldItalic

    /// https://www.fontsquirrel.com/fonts/noto-sans
    [<Sealed; AbstractClass>]
    type NotoSans internal() =
        static member Regular    : Font = Types.NotoSans.Instance.Regular
        static member Bold       : Font = Types.NotoSans.Instance.Bold
        static member Italic     : Font = Types.NotoSans.Instance.Italic
        static member BoldItalic : Font = Types.NotoSans.Instance.BoldItalic

[<AutoOpen>]
module DefaultFontsExtensions =

    type Fonts.Font with
        /// https://www.fontsquirrel.com/fonts/hack
        static member Hack = DefaultFonts.Types.Hack.Instance

        /// https://www.fontsquirrel.com/fonts/courier-prime
        static member CourierPrime = DefaultFonts.Types.CourierPrime.Instance

        /// https://www.fontsquirrel.com/fonts/noto-sans
        static member NotoSans = DefaultFonts.Types.NotoSans.Instance

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


    let surfaceCache = ConcurrentDictionary<FramebufferLayout, Lazy<IBackendSurface>>()
    let boundarySurfaceCache = ConcurrentDictionary<FramebufferLayout, Lazy<IBackendSurface>>()
    let billboardSurfaceCache = ConcurrentDictionary<FramebufferLayout, Lazy<IBackendSurface>>()


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
        surfaceCache.GetOrAdd(s.Layout, fun _ ->
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
        boundarySurfaceCache.GetOrAdd(s.Layout, fun _ ->
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
        billboardSurfaceCache.GetOrAdd(s.Layout, fun _ ->
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
            member x.TryGetAttribute(sem) = pool.TryGetBufferView sem
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
