namespace Aardvark.Rendering

open Aardvark.Base
open FShade

/// FXAA 3.11 (Timothy Lottes / NVIDIA) — single-pass post-process
/// anti-aliasing. Used as the final pass after the A-buffer transparent
/// pipeline (which runs samples=1) to mask aliasing on geometry edges.
///
/// Algorithm (abridged):
///   1. Compute pixel luma (rec.709-ish green-weighted).
///   2. Compute local contrast from the 4-neighbourhood.
///   3. Early-out on low-contrast pixels.
///   4. Estimate edge direction from the 3×3 luma differences.
///   5. March along the edge in both directions until contrast drops.
///   6. Blend toward the off-edge neighbour by a weight derived from the
///      pixel's relative position along the edge span.
///
/// Clean-room port — small, no LUTs, no temporal data. Quality is well-known:
/// smooths jaggies, slightly blurs fine detail. Cost is roughly one
/// fullscreen pass with ~15 texture taps.
module FXAA =

    module Semantic =
        let FxaaInput = Symbol.Create "FxaaInput"

    type UniformScope with
        /// 1 / framebuffer size; reciprocal so the shader uses MULs.
        /// V2f (not V2d) — MoltenVK rejects double types in uniform buffers.
        member x.FxaaRcpFrame   : V2f       = x?FxaaRcpFrame

    /// FxaaInput sampler — bilinear, clamp-to-edge so the search march
    /// past the image bounds doesn't wrap.
    let private fxaaSampler =
        sampler2d {
            texture uniform?FxaaInput
            filter Filter.MinMagLinear
            addressU WrapMode.Clamp
            addressV WrapMode.Clamp
        }

    type Fragment = {
        [<Color>]     color : V4f
        [<FragCoord>] coord : V4f
    }

    [<ReflectedDefinition; Inline>]
    let private luma (rgb : V3f) =
        // Rec.709-ish; green dominates because the eye is most sensitive to it.
        rgb.Y * 1.963211f + rgb.X * 0.587f + rgb.Z * 0.114f

    [<Literal>]
    let private EdgeThreshold     = 0.166f      // 1/6 — culls low-contrast
    [<Literal>]
    let private EdgeThresholdMin  = 0.0833f     // 1/12 — absolute floor
    [<Literal>]
    let private SubpixelTrim      = 0.75f       // sub-pixel aliasing blend
    [<Literal>]
    let private SubpixelCap       = 0.75f
    [<Literal>]
    let private SearchSteps       = 12

    /// FXAA fragment shader. Pixel-rate; output is the post-processed colour.
    let frag (f : Fragment) =
        fragment {
            let rcp  = uniform.FxaaRcpFrame
            // Derive UV from FragCoord so we don't need vertex-shader UV plumbing.
            let uv   = V2f(f.coord.X * rcp.X, f.coord.Y * rcp.Y)

            // Sample 3×3 neighbourhood lumas (centre + 4 cardinal + 4 corner)
            let rgbM  = fxaaSampler.SampleLevel(uv, 0.0f).XYZ
            let rgbN  = fxaaSampler.SampleLevel(uv + V2f(0.0f, -rcp.Y), 0.0f).XYZ
            let rgbW  = fxaaSampler.SampleLevel(uv + V2f(-rcp.X, 0.0f), 0.0f).XYZ
            let rgbE  = fxaaSampler.SampleLevel(uv + V2f( rcp.X, 0.0f), 0.0f).XYZ
            let rgbS  = fxaaSampler.SampleLevel(uv + V2f(0.0f,  rcp.Y), 0.0f).XYZ

            let lumaM = luma rgbM
            let lumaN = luma rgbN
            let lumaW = luma rgbW
            let lumaE = luma rgbE
            let lumaS = luma rgbS

            let lumaMin   = min lumaM (min (min lumaN lumaW) (min lumaE lumaS))
            let lumaMax   = max lumaM (max (max lumaN lumaW) (max lumaE lumaS))
            let lumaRange = lumaMax - lumaMin

            // Early-out on smooth pixels.
            let mutable outRgb = rgbM
            if lumaRange >= max EdgeThresholdMin (lumaMax * EdgeThreshold) then
                let rgbNW = fxaaSampler.SampleLevel(uv + V2f(-rcp.X, -rcp.Y), 0.0f).XYZ
                let rgbNE = fxaaSampler.SampleLevel(uv + V2f( rcp.X, -rcp.Y), 0.0f).XYZ
                let rgbSW = fxaaSampler.SampleLevel(uv + V2f(-rcp.X,  rcp.Y), 0.0f).XYZ
                let rgbSE = fxaaSampler.SampleLevel(uv + V2f( rcp.X,  rcp.Y), 0.0f).XYZ
                let lumaNW = luma rgbNW
                let lumaNE = luma rgbNE
                let lumaSW = luma rgbSW
                let lumaSE = luma rgbSE

                // Sub-pixel aliasing — blend by 3×3 average vs centre.
                let lumaL = (lumaN + lumaW + lumaE + lumaS) * 0.25f
                let rangeL = abs (lumaL - lumaM)
                let blendL =
                    let n = max 0.0f (rangeL / lumaRange - SubpixelTrim) / (1.0f - SubpixelTrim)
                    min SubpixelCap n

                // Edge orientation from 3×3 luma differences (Sobel-ish).
                let edgeVert =
                    abs (0.25f * lumaNW - 0.5f * lumaN + 0.25f * lumaNE) +
                    abs (0.50f * lumaW  - 1.0f * lumaM + 0.50f * lumaE) +
                    abs (0.25f * lumaSW - 0.5f * lumaS + 0.25f * lumaSE)

                let edgeHorz =
                    abs (0.25f * lumaNW - 0.5f * lumaW + 0.25f * lumaSW) +
                    abs (0.50f * lumaN  - 1.0f * lumaM + 0.50f * lumaS) +
                    abs (0.25f * lumaNE - 0.5f * lumaE + 0.25f * lumaSE)

                let horzSpan = edgeHorz >= edgeVert
                let lengthSign = if horzSpan then -rcp.Y else -rcp.X

                let lumaA = if horzSpan then lumaS else lumaE
                let lumaB = if horzSpan then lumaN else lumaW
                let gradientA = abs (lumaA - lumaM)
                let gradientB = abs (lumaB - lumaM)
                let pairBigger = gradientA < gradientB
                let actualLengthSign = if pairBigger then -lengthSign else lengthSign

                let lumaOpp = if pairBigger then lumaB else lumaA
                let lumaAvg = (lumaM + lumaOpp) * 0.5f

                let mutable offN = uv
                let mutable offP = uv
                if horzSpan then
                    offN <- offN + V2f(0.0f, actualLengthSign * 0.5f)
                    offP <- offP + V2f(0.0f, actualLengthSign * 0.5f)
                else
                    offN <- offN + V2f(actualLengthSign * 0.5f, 0.0f)
                    offP <- offP + V2f(actualLengthSign * 0.5f, 0.0f)

                let stepDir = if horzSpan then V2f(rcp.X, 0.0f) else V2f(0.0f, rcp.Y)
                let mutable posN = offN - stepDir
                let mutable posP = offP + stepDir
                let mutable lumaEndN = luma (fxaaSampler.SampleLevel(posN, 0.0f).XYZ) - lumaAvg
                let mutable lumaEndP = luma (fxaaSampler.SampleLevel(posP, 0.0f).XYZ) - lumaAvg
                let mutable doneN = abs lumaEndN >= lumaRange * 0.25f
                let mutable doneP = abs lumaEndP >= lumaRange * 0.25f

                let mutable i = 0
                while i < SearchSteps && (not doneN || not doneP) do
                    if not doneN then
                        posN <- posN - stepDir
                        lumaEndN <- luma (fxaaSampler.SampleLevel(posN, 0.0f).XYZ) - lumaAvg
                        if abs lumaEndN >= lumaRange * 0.25f then doneN <- true
                    if not doneP then
                        posP <- posP + stepDir
                        lumaEndP <- luma (fxaaSampler.SampleLevel(posP, 0.0f).XYZ) - lumaAvg
                        if abs lumaEndP >= lumaRange * 0.25f then doneP <- true
                    i <- i + 1

                let dstN = if horzSpan then uv.X - posN.X else uv.Y - posN.Y
                let dstP = if horzSpan then posP.X - uv.X else posP.Y - uv.Y
                let directionN = dstN < dstP
                let dst = min dstN dstP
                let spanLength = dstN + dstP
                let pixelOffset = -dst / spanLength + 0.5f

                let lumaEnd  = if directionN then lumaEndN else lumaEndP
                let goodSpan = (lumaM - lumaAvg < 0.0f) <> (lumaEnd < 0.0f)
                let pixOff   = if goodSpan then pixelOffset else 0.0f
                let pixOff   = max pixOff blendL

                let mutable shifted = uv
                if horzSpan then shifted <- shifted + V2f(0.0f, pixOff * actualLengthSign)
                else             shifted <- shifted + V2f(pixOff * actualLengthSign, 0.0f)

                outRgb <- fxaaSampler.SampleLevel(shifted, 0.0f).XYZ

            return V4f(outRgb, 1.0f)
        }

    /// Effect form of the FXAA fullscreen pass.
    let effect : Effect = Effect.ofFunction frag
