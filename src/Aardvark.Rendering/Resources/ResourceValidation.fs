namespace Aardvark.Rendering

open System
open Aardvark.Base
open FSharp.Data.Adaptive

module ResourceValidation =

    module Textures =

        module Helpers =
            type TextureOrRenderbuffer() =
                static member inline GetDimension(t : IBackendTexture) = t.Dimension
                static member inline GetDimension(t : IRenderbuffer) = TextureDimension.Texture2D

                static member inline HasDepth(t : IBackendTexture) = t.Format.HasDepth
                static member inline HasDepth(t : IRenderbuffer) = t.Format.HasDepth

                static member inline HasStencil(t : IBackendTexture) = t.Format.HasStencil
                static member inline HasStencil(t : IRenderbuffer) = t.Format.HasStencil

                static member inline GetSlices(t : IBackendTexture) = t.Slices
                static member inline GetSlices(t : IRenderbuffer) = 1

                static member inline GetLevels(t : IBackendTexture) = t.MipMapLevels
                static member inline GetLevels(t : IRenderbuffer) = 1

                static member inline GetSamples(t : IBackendTexture) = t.Samples
                static member inline GetSamples(t : IRenderbuffer) = t.Samples

                static member inline GetSize(t : IBackendTexture, level : int) = t.GetSize(level)
                static member inline GetSize(t : IRenderbuffer, level : int) = V3i(t.Size, 1)

        [<AutoOpen>]
        module private Aux =
            let inline getDimensionAux (_ : ^z) (t : ^Texture) =
                ((^z or ^Texture) : (static member GetDimension : ^Texture -> TextureDimension) (t))

            let inline hasDepthAux (_ : ^z) (t : ^Texture) =
                ((^z or ^Texture) : (static member HasDepth : ^Texture -> bool) (t))

            let inline hasStencilAux (_ : ^z) (t : ^Texture) =
                ((^z or ^Texture) : (static member HasStencil: ^Texture -> bool) (t))

            let inline getSlicesAux (_ : ^z) (t : ^Texture) =
                ((^z or ^Texture) : (static member GetSlices : ^Texture -> int) (t))

            let inline getLevelsAux (_ : ^z) (t : ^Texture) =
                ((^z or ^Texture) : (static member GetLevels : ^Texture -> int) (t))

            let inline getSamplesAux (_ : ^z) (t : ^Texture) =
                ((^z or ^Texture) : (static member GetSamples : ^Texture -> int) (t))

            let inline getSizeAux (_ : ^z) (level : int) (t : ^Texture) =
                ((^z or ^Texture) : (static member GetSize : ^Texture * int -> V3i) (t, level))

        let inline getDimension (texture : ^Texture) =
            getDimensionAux Unchecked.defaultof<Helpers.TextureOrRenderbuffer> texture

        let inline hasDepth (texture : ^Texture) =
            hasDepthAux Unchecked.defaultof<Helpers.TextureOrRenderbuffer> texture

        let inline hasStencil (texture : ^Texture) =
            hasStencilAux Unchecked.defaultof<Helpers.TextureOrRenderbuffer> texture

        let inline getSlices (texture : ^Texture) =
            getSlicesAux Unchecked.defaultof<Helpers.TextureOrRenderbuffer> texture

        let inline getLevels (texture : ^Texture) =
            getLevelsAux Unchecked.defaultof<Helpers.TextureOrRenderbuffer> texture

        let inline getSamples (texture : ^Texture) =
            getSamplesAux Unchecked.defaultof<Helpers.TextureOrRenderbuffer> texture

        let inline getSize (level : int) (texture : ^Texture) =
            getSizeAux Unchecked.defaultof<Helpers.TextureOrRenderbuffer> level texture

        let private isValidSize (size : V3i) = function
            | TextureDimension.Texture1D -> size.X >= 0
            | TextureDimension.Texture2D -> Vec.allGreaterOrEqual size.XY 0
            | TextureDimension.Texture3D -> Vec.allGreaterOrEqual size 0
            | TextureDimension.TextureCube -> size.X >= 0
            | _ -> true

        module Utils =
            let failf (dimension : TextureDimension) format =
                let name =
                    match dimension with
                    | TextureDimension.Texture1D -> "Texture1D"
                    | TextureDimension.Texture2D -> "Texture2D"
                    | TextureDimension.Texture3D -> "Texture3D"
                    | TextureDimension.TextureCube -> "TextureCube"
                    | _ -> ""

                Printf.ksprintf (fun str ->
                    let message = sprintf "[%s] %s" name str
                    raise <| ArgumentException(message)
                ) format

            let fail (dimension : TextureDimension) (msg : string) =
                failf dimension "%s" msg

            let validateIndex (dimension : TextureDimension) (name : string) (index : int) (count : int) =
                if index < 0 then failf dimension "%s cannot be negative" name
                if index >= count then failf dimension "cannot access texture %s with index %d (texture has only %d)" name index count

            let validateIndexRange (dimension : TextureDimension) (name : string) (baseIndex : int) (rangeCount : int) (count : int) =
                if baseIndex < 0 then failf dimension "base %s cannot be negative" name
                if rangeCount < 1 then failf dimension "%s count must be greater than zero" name
                if baseIndex + rangeCount - 1 >= count then
                    failf dimension "cannot access texture %ss with index range [%d, %d] (texture has only %d)" name baseIndex (baseIndex + rangeCount - 1) count

        /// Raises an ArgumentException if the given slice of the texture is invalid.
        let inline validateSlice (slice : int) (texture : ^Texture) =
            let dimension = getDimension texture
            let slices = getSlices texture
            Utils.validateIndex dimension "slice" slice slices

        /// Raises an ArgumentException if the given slice range of the texture is invalid.
        let inline validateSlices (baseSlice : int) (count : int) (texture : ^Texture) =
            let dimension = getDimension texture
            let slices = getSlices texture
            Utils.validateIndexRange dimension "slice" baseSlice count slices

        /// Raises an ArgumentException if the given level of the texture is invalid.
        let inline validateLevel (level : int) (texture : ^Texture) =
            let dimension = getDimension texture
            let levels = getLevels texture
            Utils.validateIndex dimension "level" level levels

        /// Raises an ArgumentException if the given level range of the texture is invalid.
        let inline validateLevels (baseLevel : int) (count : int) (texture : ^Texture) =
            let dimension = getDimension texture
            let levels = getLevels texture
            Utils.validateIndexRange dimension "level" baseLevel count levels

        let validateSizes' (dimension : TextureDimension) (srcSize : V3i) (dstSize : V3i) =
            if srcSize <> dstSize then
                Utils.failf dimension "sizes of texture levels do not match (%A, %A)" srcSize dstSize

        /// Raises an ArgumentException if the given levels of the textures do not match.
        let inline validateSizes (srcLevel : int) (dstLevel : int) (src : ^T1) (dst : ^T2) =
            let srcSize = getSize srcLevel src
            let dstSize = getSize dstLevel dst
            validateSizes' (getDimension src) srcSize dstSize

        let validateSamplesForCopy' (dimension : TextureDimension) (srcSamples : int) (dstSamples : int) =
            if srcSamples <> dstSamples && dstSamples <> 1 then
                Utils.failf dimension "samples of textures do not match and destination is multisampled (src = %d, dst = %d)" srcSamples dstSamples

        /// Raises an ArgumentException if neither of the following conditions apply:
        /// (1) both textures have the same number of samples,
        /// (2) dst is not multisampled.
        let inline validateSamplesForCopy (src : ^T1) (dst : ^T2) =
            let srcSamples = getSamples src
            let dstSamples = getSamples dst
            validateSamplesForCopy' (getDimension src) srcSamples dstSamples

        /// Raises an ArgumentException if the window for the given texture is invalid.
        let inline validateWindow (level : int) (offset : V3i) (windowSize : V3i) (texture : ^Texture) =
            let dimension = getDimension texture
            let size = getSize level texture

            if Vec.anySmaller offset 0 then
                Utils.failf dimension "offset cannot be negative (is %A)" offset

            if Vec.anySmaller windowSize 1 then
                Utils.failf dimension "window size must be greater than 0 (is %A)" windowSize

            if Vec.anyGreater (offset + windowSize) size then
                Utils.failf dimension "texture window (offset = %A, size = %A) exceeds size of texture level (%A)" offset windowSize size

        /// Raises an ArgumentException if the window for the given texture is invalid.
        let inline validateWindow2D (level : int) (offset : V2i) (windowSize : V2i) (texture : ^Texture) =
            texture |> validateWindow level (V3i offset) (V3i (windowSize, 1))

        /// Raises an ArgumentException if the combination of parameters is invalid.
        let validateCreationParams (dimension : TextureDimension) (size : V3i) (levels : int) (samples : int) =
            let fail = Utils.fail dimension

            if not (dimension |> isValidSize size) then
                fail "size must not be negative"

            if levels < 1 then fail "levels must be greater than 0"
            if samples < 1 then fail "samples must be greater than 0"
            if samples > 1 then
                if levels > 1 then fail "multisampled textures cannot have mip maps"
                if dimension <> TextureDimension.Texture2D then fail "only 2D textures can be multisampled"

        /// Raises an ArgumentException if the combination of parameters is invalid.
        let validateCreationParamsArray (dimension : TextureDimension) (size : V3i) (levels : int) (samples : int) (count : int) =
            let fail = Utils.fail dimension

            if count < 1 then fail "count must be greater than 0"
            if dimension = TextureDimension.Texture3D then fail "cannot be arrayed"
            validateCreationParams dimension size levels samples

        /// Raises an ArgumentException if the image does not have a depth format.
        let inline validateDepthFormat (texture : ^Texture) =
            let dimension = getDimension texture

            if not <| hasDepth texture then
                Utils.failf dimension "image does not have a depth component"

        /// Raises an ArgumentException if the image does not have a stencil format.
        let inline validateStencilFormat (texture : ^Texture) =
            let dimension = getDimension texture

            if not <| hasStencil texture then
                Utils.failf dimension "image does not have a stencil component"