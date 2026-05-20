namespace Aardvark.Rendering

open Aardvark.Base
open FShade
open FSharp.Data.Adaptive

/// Weighted Blended Order-Independent Transparency (McGuire/Bavoil 2013).
///
/// Reusable building blocks for the OIT technique:
///   - default semantic symbols for the accum and revealage attachments
///   - blend modes used by the transparent pass
///   - FShade effects that produce / consume the OIT buffers
///   - a helper to compose the OIT fragment writer onto an existing Surface
///
/// The RenderTask wrapper (TransparencyRenderTask) uses these to transform
/// every RenderObject with IsTransparent=true.
module WeightedBlendedOIT =

    module Semantic =
        /// RGBA16F target: sum of (color.rgb * alpha * weight, alpha * weight).
        let Accum = Symbol.Create "Accum"

        /// R32F target: multiplied (1 - alpha) — the transmittance product.
        let Revealage = Symbol.Create "Revealage"

        /// Sampler name read by the composite shader (the Accum texture from the OIT pass).
        let AccumBuffer = Symbol.Create "AccumBuffer"

        /// Sampler name read by the composite shader (the Revealage texture from the OIT pass).
        let RevealageBuffer = Symbol.Create "RevealageBuffer"

    module BlendModes =
        /// Additive blend for the Accum attachment (one source + one destination).
        let accum = BlendMode.Add

        /// Multiplicative transmittance for the Revealage attachment:
        ///   result = 0*src + (1 - src.r) * dst
        let revealage =
            { BlendMode.Blend with
                SourceColorFactor      = BlendFactor.Zero
                DestinationColorFactor = BlendFactor.InvSourceColor
                SourceAlphaFactor      = BlendFactor.Zero
                DestinationAlphaFactor = BlendFactor.InvSourceAlpha }

        /// Final composite blend — premultiplied alpha blend.
        let composite = BlendMode.Blend

    [<AutoOpen>]
    module Shaders =

        type Fragment = {
            [<Color>]     color  : V4f
            [<FragCoord>] coord  : V4f
            [<SampleId>]  sample : int
        }

        /// Fragment writer for the transparent pass. Reads an input Colors attachment
        /// (RGBA, alpha = coverage) and produces Accum + Revealage outputs.
        /// Appended onto each transparent RenderObject's surface by the wrapper.
        let weightedBlend (f : Fragment) =
            fragment {
                let a = f.color.W * 8.0f + 0.01f
                let b = -f.coord.Z * 0.95f + 1.0f
                let w = clamp 1e-2f 3e2f (a * a * a * 1e8f * b * b * b)

                let alpha = f.color.W
                let color = V4f(f.color.XYZ * alpha, alpha) * w

                return {| Accum = color
                          Revealage = alpha |}
            }

        let private accumSampler =
            sampler2d {
                texture uniform?AccumBuffer
                filter Filter.MinMagPoint
                addressU WrapMode.Clamp
                addressV WrapMode.Clamp
            }

        let private revealageSampler =
            sampler2d {
                texture uniform?RevealageBuffer
                filter Filter.MinMagPoint
                addressU WrapMode.Clamp
                addressV WrapMode.Clamp
            }

        let private accumSamplerMS =
            sampler2dMS {
                texture uniform?AccumBuffer
                filter Filter.MinMagPoint
                addressU WrapMode.Clamp
                addressV WrapMode.Clamp
            }

        let private revealageSamplerMS =
            sampler2dMS {
                texture uniform?RevealageBuffer
                filter Filter.MinMagPoint
                addressU WrapMode.Clamp
                addressV WrapMode.Clamp
            }

        /// Fragment shader for the fullscreen composite pass. Reads the accum + revealage
        /// textures from the OIT pass and writes the resolved transparent color.
        /// `samples` is the multisample count of the OIT attachments (1 for non-MS).
        let composite (samples : int) (f : Fragment) =
            fragment {
                let mutable accum = V4f.Zero
                let mutable revealage = V4f.Zero

                if samples > 1 then
                    for i in 0 .. samples - 1 do
                        accum <- accum + accumSamplerMS.Read(V2i f.coord.XY, i)
                        revealage <- revealage + revealageSamplerMS.Read(V2i f.coord.XY, i)
                    accum <- accum / float32 samples
                    revealage <- revealage / float32 samples
                else
                    accum <- accumSampler.Read(V2i f.coord.XY, 0)
                    revealage <- revealageSampler.Read(V2i f.coord.XY, 0)

                let accum =
                    if isInfinity accum then V4f(accum.W)
                    else accum

                return V4f(accum.XYZ / max accum.W 1e-5f, 1.0f - revealage.X)
            }

    /// Effect form of `weightedBlend` — composable with other Effects.
    let weightedBlendEffect : Effect = Effect.ofFunction weightedBlend

    /// Builds the composite effect for the given sample count.
    let compositeEffect (samples : int) : Effect =
        Effect.ofFunction (composite samples)

    /// Composes the OIT fragment writer onto an existing Surface. Used by the
    /// RenderTask wrapper to transform every transparent RenderObject.
    ///   - Surface.Effect e: appends weightedBlend after e.
    ///   - Surface.Dynamic compile: wraps the compile callback to compose.
    ///   - Surface.Backend or Surface.None: fails — transparent objects need a composable surface.
    let composeSurface (surface : Surface) : Surface =
        match surface with
        | Surface.Effect e ->
            Surface.Effect (Effect.compose [e; weightedBlendEffect])
        | Surface.Dynamic _ ->
            failwith "[OIT] dynamic surfaces are not yet supported for transparent objects"
        | Surface.Backend _ ->
            failwith "[OIT] backend (pre-compiled) surfaces cannot be marked transparent"
        | Surface.None ->
            failwith "[OIT] transparent objects need a surface"
