namespace Aardvark.Rendering

open System
open Aardvark.Base
open FSharp.Data.Adaptive

module ResourceValidation =

    module Textures =

        let private isValidSize (size : V3i) = function
            | TextureDimension.Texture1D -> size.X >= 0
            | TextureDimension.Texture2D -> Vec.allGreaterOrEqual size.XY 0
            | TextureDimension.Texture3D -> Vec.allGreaterOrEqual size 0
            | TextureDimension.TextureCube -> size.X >= 0
            | _ -> true

        let private failf (dimension : TextureDimension) format =
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

        let private fail (dimension : TextureDimension) (msg : string) =
            failf dimension "%s" msg

        let private validateIndex (dimension : TextureDimension) (name : string) (index : int) (count : int) =
            if index < 0 then failf dimension "%s cannot be negative" name
            if index >= count then failf dimension "cannot access texture %s with index %d (texture has only %d)" name index count

        let private validateIndexRange (dimension : TextureDimension) (name : string) (baseIndex : int) (rangeCount : int) (count : int) =

            if baseIndex < 0 then failf dimension "base %s cannot be negative" name
            if rangeCount < 1 then failf dimension "%s count must be greater than zero" name
            if baseIndex + rangeCount - 1 >= count then
                failf dimension "cannot access texture %ss with index range [%d, %d] (texture has only %d)" name baseIndex (baseIndex + rangeCount - 1) count

        /// Raises an ArgumentException if the given slice of the texture is invalid.
        let validateSlice (slice : int) (texture : IBackendTexture) =
            validateIndex texture.Dimension "slice" slice texture.Slices

        /// Raises an ArgumentException if the given slice range of the texture is invalid.
        let validateSlices (baseSlice : int) (count : int) (texture : IBackendTexture) =
            validateIndexRange texture.Dimension "slice" baseSlice count texture.Slices

        /// Raises an ArgumentException if the given level of the texture is invalid.
        let validateLevel (level : int) (texture : IBackendTexture) =
            validateIndex texture.Dimension "level" level texture.MipMapLevels

        /// Raises an ArgumentException if the given level range of the texture is invalid.
        let validateLevels (baseLevel : int) (count : int) (texture : IBackendTexture) =
            validateIndexRange texture.Dimension "level" baseLevel count texture.MipMapLevels

        /// Raises an ArgumentException if the given levels of the textures do not match.
        let validateSizes (srcLevel : int) (dstLevel : int) (src : IBackendTexture) (dst : IBackendTexture) =
            let srcSize = src.GetSize(srcLevel)
            let dstSize = dst.GetSize(dstLevel)

            if srcSize <> dstSize then
                failf src.Dimension "sizes of texture levels do not match (%A, %A)" srcSize dstSize

        /// Raises an ArgumentException if neither of the following conditions apply:
        /// (1) both textures have the same number of samples,
        /// (2) dst is not multisampled.
        let validateSamplesForCopy (src : IBackendTexture) (dst : IBackendTexture) =
            if src.Samples <> dst.Samples && dst.Samples <> 1 then
                failf src.Dimension "samples of textures do not match and destination is multisampled (src = %d, dst = %d)" src.Samples dst.Samples

        /// Raises an ArgumentException if the window for the given texture is invalid.
        let validateWindow (level : int) (offset : V3i) (windowSize : V3i) (texture : IBackendTexture) =
            let size = texture.GetSize(level)

            if Vec.anySmaller offset 0 then
                failf texture.Dimension "offset cannot be negative (is %A)" offset

            if Vec.anySmaller windowSize 1 then
                failf texture.Dimension "window size must be greater than 0 (is %A)" windowSize

            if Vec.anyGreater (offset + windowSize) size then
                failf texture.Dimension "texture window (offset = %A, size = %A) exceeds size of texture level (%A)" offset windowSize size

        /// Raises an ArgumentException if the window for the given texture is invalid.
        let validateWindow2D (level : int) (offset : V2i) (windowSize : V2i) (texture : IBackendTexture) =
            texture |> validateWindow level (V3i offset) (V3i (windowSize, 1))

        /// Raises an ArgumentException if the combination of parameters is invalid.
        let validateCreationParams (dimension : TextureDimension) (size : V3i) (levels : int) (samples : int) =
            let fail = fail dimension

            if not (dimension |> isValidSize size) then
                fail "size must not be negative"

            if levels < 1 then fail "levels must be greater than 0"
            if samples < 1 then fail "samples must be greater than 0"
            if samples > 1 then
                if levels > 1 then fail "multisampled textures cannot have mip maps"
                if dimension <> TextureDimension.Texture2D then fail "only 2D textures can be multisampled"

        /// Raises an ArgumentException if the combination of parameters is invalid.
        let validateCreationParamsArray (dimension : TextureDimension) (size : V3i) (levels : int) (samples : int) (count : int) =
            let fail = fail dimension

            if count < 1 then fail "count must be greater than 0"
            if dimension = TextureDimension.Texture3D then fail "cannot be arrayed"
            validateCreationParams dimension size levels samples