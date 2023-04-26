namespace Aardvark.Rendering

open System
open Aardvark.Base

module ResourceValidation =

    module Textures =

        module Helpers =
            type TextureOrRenderbuffer() =
                static member inline GetDimension(t : IBackendTexture) = t.Dimension
                static member inline GetDimension(t : IRenderbuffer) = TextureDimension.Texture2D

                static member inline GetFormat(t : IBackendTexture) = t.Format
                static member inline GetFormat(t : IRenderbuffer) = t.Format

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

            let inline getFormatAux (_ : ^z) (t : ^Texture) =
                ((^z or ^Texture) : (static member GetFormat : ^Texture -> TextureFormat) (t))

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

        let inline getFormat (texture : ^Texture) =
            getFormatAux Unchecked.defaultof<Helpers.TextureOrRenderbuffer> texture

        let inline hasDepth (texture : ^Texture) =
            let format = getFormat texture
            format.HasDepth

        let inline hasStencil (texture : ^Texture) =
            let format = getFormat texture
            format.HasStencil

        let inline getSlices (texture : ^Texture) =
            getSlicesAux Unchecked.defaultof<Helpers.TextureOrRenderbuffer> texture

        let inline getLevels (texture : ^Texture) =
            getLevelsAux Unchecked.defaultof<Helpers.TextureOrRenderbuffer> texture

        let inline getSamples (texture : ^Texture) =
            getSamplesAux Unchecked.defaultof<Helpers.TextureOrRenderbuffer> texture

        let inline getSize (level : int) (texture : ^Texture) =
            getSizeAux Unchecked.defaultof<Helpers.TextureOrRenderbuffer> level texture

        let private isValidSize (size : V3i) = function
            | TextureDimension.Texture1D -> size.X >= 0 && size.YZ = V2i.II
            | TextureDimension.Texture2D -> Vec.allGreaterOrEqual size.XY 0 && size.Z = 1
            | TextureDimension.Texture3D -> Vec.allGreaterOrEqual size 0
            | TextureDimension.TextureCube -> size.X >= 0 && size.X = size.Y && size.Z = 1
            | _ -> true

        let private validSampleCounts = Set.ofList [ 1; 2; 4; 8; 16; 32; 64 ]

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
                    raise <| ArgumentException(message + ".")
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

        /// Raises an ArgumentException if the blit region (may have negative size) for the given texture is invalid.
        let inline validateBlitRegion (level : int) (region : Box3i) (texture : ^Texture) =
            let dimension = getDimension texture
            let size = getSize level texture

            let min = min region.Min region.Max
            let max = max region.Min region.Max

            if Vec.anyEqual region.Size 0 then
                Utils.failf dimension "blit region may not have a size of zero (is %A)" region

            if Vec.anySmaller min 0 || Vec.anyGreater max size then
                Utils.failf dimension "blit region out-of-bounds (region = %A, size = %A)" region size

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
        let inline validateUploadWindow (level : int) (offset : V3i) (windowSize : V3i) (texture : ^Texture) =
            let dimension = getDimension texture
            let size = getSize level texture
            let format = getFormat texture

            validateWindow level offset windowSize texture

            if format.IsCompressed && windowSize <> size then
                let mode = format.CompressionMode
                let blockSize = CompressionMode.blockSize mode

                let windowSize =
                    match dimension with
                    | TextureDimension.Texture1D -> windowSize.XOO
                    | TextureDimension.Texture2D | TextureDimension.TextureCube -> windowSize.XYO
                    | _ -> windowSize

                if windowSize.X % blockSize <> 0 ||  windowSize.Y % blockSize <> 0 || windowSize.Z % blockSize <> 0 then
                    Utils.failf dimension "window size must be aligned with compressed texture block size (size = %A, block size = %A)" windowSize blockSize

                let offset =
                    if dimension <> TextureDimension.Texture2D && dimension <> TextureDimension.TextureCube then
                        offset
                    else
                        V3i(offset.X, size.Y - (offset.Y + windowSize.Y), offset.Z)

                if offset.X % blockSize <> 0 || offset.Y % blockSize <> 0 || offset.Z % blockSize <> 0 then
                    Utils.failf dimension "window offset must be aligned with compressed texture block size (offset = %A, block size = %A)" offset blockSize

        /// Raises an ArgumentException if the window for the given texture is invalid.
        let inline validateWindow2D (level : int) (offset : V2i) (windowSize : V2i) (texture : ^Texture) =
            texture |> validateWindow level (V3i offset) (V3i (windowSize, 1))

        /// Raises an ArgumentException if the combination of parameters is invalid.
        let validateCreationParams (dimension : TextureDimension) (size : V3i) (levels : int) (samples : int) =
            let fail = Utils.fail dimension
            let failf fmt = Utils.failf dimension fmt

            if not (dimension |> isValidSize size) then
                match dimension with
                | TextureDimension.Texture1D -> failf "size %A is invalid (must be V3i(w, 1, 1) with w > 0)" size
                | TextureDimension.Texture2D -> failf "size %A is invalid (must be V3i(w, h, 1) with w, h > 0)" size
                | TextureDimension.Texture3D -> failf "size %A is invalid (must be V3i(w, h, d) with w, h, d > 0)" size
                | _ ->                          failf "size %A is invalid (must be V3i(w, w, 1) with w > 0)" size

            if levels < 1 then failf "levels must be greater than 0 (is %d)" levels
            if samples < 1 then failf "samples must be greater than 0 (is %d)" samples
            if samples > 1 then
                if levels > 1 then fail "multisampled textures cannot have mip maps"
                if dimension <> TextureDimension.Texture2D then fail "only 2D textures can be multisampled"
                if not <| validSampleCounts.Contains samples then
                    Utils.failf dimension "samples must be one of %A but got %d" validSampleCounts samples

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

        /// Raises an ArgumentException if the image format does not support mipmap generation.
        [<Obsolete>]
        let inline validateFormatForMipmapGeneration (texture : ^Texture) =
            let dimension = getDimension texture
            let format = getFormat texture

            if false then
                Utils.failf dimension "cannot generate mipmaps for textures with format %A" format

        /// Raises an ArgumentException if the texture is a null texture.
        let inline validateForPrepare (texture : ITexture) =
            match texture with
            | :? NullTexture -> raise <| ArgumentException("Cannot prepare a NullTexture")
            | _ -> ()

    module Buffers =

        module private Utils =
            let failf format =
                Printf.ksprintf (fun str ->
                     let message = sprintf "[Buffer] %s" str
                     raise <| ArgumentException(message + ".")
                 ) format

        /// Raises an ArgumentException if the given range is out of bounds for the given buffer.
        let validateRange (offset : nativeint) (sizeInBytes : nativeint) (buffer : IBackendBuffer) =
            if offset < 0n then Utils.failf "offset must not be negative"
            if sizeInBytes < 0n then Utils.failf "size must not be negative"
            let e = offset + sizeInBytes
            if e > buffer.SizeInBytes then
                Utils.failf "range out of bounds { offset = %A; size = %A } (size: %A)" offset sizeInBytes buffer.SizeInBytes

        /// Raises an ArgumentException if the given size is negative.
        let validateSize (sizeInBytes : nativeint) =
            if sizeInBytes < 0n then Utils.failf "size must not be negative"

    module Framebuffers =

        module private Utils =
            let failf format =
                Printf.ksprintf (fun str ->
                     let message = sprintf "[Framebuffer] %s" str
                     raise <| ArgumentException(message + ".")
                 ) format

        let validSampleCounts = Set.ofList [ 1; 2; 4; 8; 16; 32; 64 ]

        /// Raises and ArgumentException if the given signature parameters are invalid.
        let validateSignatureParams (colorAttachments : Map<int, AttachmentSignature>)
                                    (depthStencilAttachment : Option<TextureFormat>)
                                    (samples : int) (layers : int) =

            for KeyValue(slot, att) in colorAttachments do
                if slot < 0 then
                    Utils.failf "color attachment slot must not be negative (is %d for %A)" slot att.Name

            colorAttachments
            |> Map.iter (fun _ att ->
                if not att.Format.IsColorRenderable then
                    Utils.failf "format %A of color attachment %A is not color-renderable" att.Format att.Name
            )

            colorAttachments
            |> Map.toList
            |> List.groupBy (fun (_, att) -> att.Name)
            |> List.iter (fun (name, atts) ->
                if atts.Length > 1 then
                    let slots = atts |> List.map fst
                    Utils.failf "color attachments must not have the same name (attachments in slots %A have name %A)" slots name
            )

            match depthStencilAttachment with
            | Some fmt when not (fmt.HasDepth || fmt.HasStencil) ->
                Utils.failf "depth-stencil attachment format must be a depth, stencil, or combined depth-stencil format (got %A)" fmt

            | _ -> ()

            if not <| validSampleCounts.Contains samples then
                Utils.failf "samples must be one of %A but got %d" validSampleCounts samples

            if layers < 1 then
                Utils.failf "layers must be greater than zero"


        /// Raises and ArgumentException if the given attachments do not fit the signature.
        let validateAttachments (signature : IFramebufferSignature) (bindings : Map<Symbol, IFramebufferOutput>) =

            for KeyValue(_, att) in signature.ColorAttachments do
                match bindings |> Map.tryFind att.Name with
                | Some b ->
                    if b.Format <> att.Format then
                        Utils.failf "expected color attachment %A to have format %A, but has format %A" att.Name att.Format b.Format

                    if b.Samples <> signature.Samples then
                        Utils.failf "all attachments must have a sample count of %d (%A has %d)" signature.Samples att.Name b.Samples

                | _ ->
                    Utils.failf "missing color attachment %A with format %A" att.Name att.Format

            match signature.DepthStencilAttachment with
            | Some format ->
                match bindings |> Map.tryFind DefaultSemantic.DepthStencil with
                | Some b ->
                    if b.Format <> format then
                        Utils.failf "expected depth-stencil attachment to have format %A, but has format %A" format b.Format

                    if b.Samples <> signature.Samples then
                        Utils.failf "all attachments must have a sample count of %d (depth-stencil attachment has %d)" signature.Samples b.Samples

                | _ ->
                    Utils.failf "missing depth-stencil attachment with format %A" format

            | None ->
                ()