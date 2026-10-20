namespace Aardvark.Rendering

open Aardvark.Base
open System

[<AutoOpen>]
module ``IBackendTexture Slicing Extensions`` =

    type IBackendTexture with

        /// Returns the number of texture slices.
        member x.Slices =
            if x.Dimension = TextureDimension.TextureCube then
                x.Count * 6
            else
                x.Count

        /// <summary> Gets the size of the given texture level.
        /// <paramref name="level"/> is clamped to [0, levels - 1].
        /// </summary>
        /// <param name="level">The texture level.</param>
        member x.GetSize(level : int) =
            let level = level |> clamp 0 (x.MipMapLevels - 1)
            Fun.MipmapLevelSize(x.Size, level)

    type private TextureRange(aspect : TextureAspect, tex : IBackendTexture, levels : Range1i, slices : Range1i) =
        interface ITextureRange with
            member x.Texture = tex
            member x.Aspect = aspect
            member x.Levels = levels
            member x.Slices = slices

    type private TextureLevel(aspect : TextureAspect, tex : IBackendTexture, level : int, slices : Range1i) =
        member x.Size = tex.GetSize(level)

        interface ITextureRange with
            member x.Texture = tex
            member x.Aspect = aspect
            member x.Levels = Range1i(level, level)
            member x.Slices = slices

        interface IFramebufferOutput with
            member x.Runtime = tex.Runtime
            member x.Size = x.Size.XY
            member x.Format = tex.Format
            member x.Samples = tex.Samples

        interface ITextureLevel with
            member x.Level = level
            member x.Size = x.Size

    type private TextureSlice(aspect : TextureAspect, tex : IBackendTexture, levels : Range1i, slice : int) =
        interface ITextureRange with
            member x.Texture = tex
            member x.Aspect = aspect
            member x.Levels = levels
            member x.Slices = Range1i(slice, slice)

        interface ITextureSlice with
            member x.Slice = slice

    type private SubTexture(aspect : TextureAspect, tex : IBackendTexture, level : int, slice : int) =

        member x.Size = tex.GetSize(level)

        interface ITextureRange with
            member x.Texture = tex
            member x.Aspect = aspect
            member x.Levels = Range1i(level, level)
            member x.Slices = Range1i(slice, slice)

        interface ITextureSubResource with
            member x.Slice = slice
            member x.Level = level
            member x.Size = x.Size

        interface IFramebufferOutput with
            member x.Runtime = tex.Runtime
            member x.Format = tex.Format
            member x.Samples = tex.Samples
            member x.Size = x.Size.XY

    type private Range1i with
        member x.SubRange(index : int) =
            let last = int64 x.Max - int64 x.Min
            if index < 0 || int64 index > last then
                raise <| ArgumentException($"Texture index {index} is outside parent-relative range [0, {last}].")
            int (int64 x.Min + int64 index)

        member x.SubRange(min : Option<int>, max : Option<int>) =
            let min = x.SubRange(defaultArg min 0)
            let max = match max with Some index -> x.SubRange(index) | None -> x.Max
            if min > max then
                raise <| ArgumentException("Texture selection has reversed endpoints.")
            Range1i(min, max)

    type IBackendTexture with

        member x.GetSlice(aspect : TextureAspect, minLevel : Option<int>, maxLevel : Option<int>, minSlice : Option<int>, maxSlice : Option<int>) =
            let level = Range1i(defaultArg minLevel 0, defaultArg maxLevel (x.MipMapLevels - 1))
            let slice = Range1i(defaultArg minSlice 0, defaultArg maxSlice (x.Slices - 1))
            TextureRange(aspect, x, level, slice) :> ITextureRange

        member x.GetSlice(aspect : TextureAspect, minLevel : Option<int>, maxLevel : Option<int>, slice : int) =
            let level = Range1i(defaultArg minLevel 0, defaultArg maxLevel (x.MipMapLevels - 1))
            TextureSlice(aspect, x, level, slice) :> ITextureSlice

        member x.GetSlice(aspect : TextureAspect, level : int, minSlice : Option<int>, maxSlice : Option<int>) =
            let slice = Range1i(defaultArg minSlice 0, defaultArg maxSlice (x.Slices - 1))
            TextureLevel(aspect, x, level, slice) :> ITextureLevel

        member x.Item
            with get(aspect : TextureAspect, level : int, slice : int) = SubTexture(aspect, x, level, slice) :> ITextureSubResource

        member x.Item
            with get(aspect : TextureAspect, level : int) = TextureLevel(aspect, x, level, Range1i(0, x.Slices - 1)) :> ITextureLevel

        member x.Item
            with get(aspect : TextureAspect) = TextureRange(aspect, x, Range1i(0, x.MipMapLevels - 1), Range1i(0, x.Slices - 1)) :> ITextureRange

    type ITextureRange with
        /// <summary>Selects inclusive, parent-relative level and slice ranges. Omitted endpoints retain the corresponding parent boundary.</summary>
        /// <exception cref="T:System.ArgumentException">The selection is reversed or escapes the immediate parent range.</exception>
        member x.GetSlice(minLevel : Option<int>, maxLevel : Option<int>, minSlice : Option<int>, maxSlice : Option<int>) =
            let level = x.Levels.SubRange(minLevel, maxLevel)
            let slice = x.Slices.SubRange(minSlice, maxSlice)
            TextureRange(x.Aspect, x.Texture, level, slice) :> ITextureRange

        /// <summary>Selects an inclusive, parent-relative level range and slice. Omitted endpoints retain the corresponding parent boundary.</summary>
        /// <exception cref="T:System.ArgumentException">The selection is reversed or escapes the immediate parent range.</exception>
        member x.GetSlice(minLevel : Option<int>, maxLevel : Option<int>, slice : int) =
            let level = x.Levels.SubRange(minLevel, maxLevel)
            let slice = x.Slices.SubRange(slice)
            TextureSlice(x.Aspect, x.Texture, level, slice) :> ITextureSlice

        /// <summary>Selects a parent-relative level and inclusive slice range. Omitted endpoints retain the corresponding parent boundary.</summary>
        /// <exception cref="T:System.ArgumentException">The selection is reversed or escapes the immediate parent range.</exception>
        member x.GetSlice(level : int, minSlice : Option<int>, maxSlice : Option<int>) =
            let level = x.Levels.SubRange(level)
            let slice = x.Slices.SubRange(minSlice, maxSlice)
            TextureLevel(x.Aspect, x.Texture, level, slice) :> ITextureLevel

        /// <summary>Selects a subresource using parent-relative level and slice indices.</summary>
        /// <exception cref="T:System.ArgumentException">An index is outside the immediate parent range.</exception>
        member x.Item
            with get(level : int, slice : int) = SubTexture(x.Aspect, x.Texture, x.Levels.SubRange(level), x.Slices.SubRange(slice)) :> ITextureSubResource

        /// <summary>Selects a parent-relative level, retaining the parent's slices.</summary>
        /// <exception cref="T:System.ArgumentException">The level index is outside the immediate parent range.</exception>
        member x.Item
            with get(level : int) = TextureLevel(x.Aspect, x.Texture, x.Levels.SubRange(level), x.Slices) :> ITextureLevel

    type ITextureLevel with
        /// <summary>Selects an inclusive, parent-relative slice range. Omitted endpoints retain the corresponding parent boundary.</summary>
        /// <exception cref="T:System.ArgumentException">The selection is reversed or escapes the immediate parent range.</exception>
        member x.GetSlice(minSlice : Option<int>, maxSlice : Option<int>) =
            let slice = x.Slices.SubRange(minSlice, maxSlice)
            TextureLevel(x.Aspect, x.Texture, x.Level, slice) :> ITextureRange

        /// <summary>Selects a subresource using a parent-relative slice index.</summary>
        /// <exception cref="T:System.ArgumentException">The slice index is outside the immediate parent range.</exception>
        member x.Item
            with get(slice : int) = SubTexture(x.Aspect, x.Texture, x.Level, x.Slices.SubRange(slice)) :> ITextureSubResource

    type ITextureSlice with
        /// <summary>Selects an inclusive, parent-relative level range. Omitted endpoints retain the corresponding parent boundary.</summary>
        /// <exception cref="T:System.ArgumentException">The selection is reversed or escapes the immediate parent range.</exception>
        member x.GetSlice(minLevel : Option<int>, maxLevel : Option<int>) =
            let levels = x.Levels.SubRange(minLevel, maxLevel)
            TextureSlice(x.Aspect, x.Texture, levels, x.Slice) :> ITextureRange

        /// <summary>Selects a subresource using a parent-relative level index.</summary>
        /// <exception cref="T:System.ArgumentException">The level index is outside the immediate parent range.</exception>
        member x.Item
            with get(level : int) = SubTexture(x.Aspect, x.Texture, x.Levels.SubRange(level), x.Slice) :> ITextureSubResource