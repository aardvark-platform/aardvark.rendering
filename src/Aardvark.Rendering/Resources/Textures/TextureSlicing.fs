namespace Aardvark.Rendering

open Aardvark.Base

[<AutoOpen>]
module ``IBackendTexture Slicing Extensions`` =

    type private TextureRange(aspect : TextureAspect, tex : IBackendTexture, levels : Range1i, slices : Range1i) =
        interface ITextureRange with
            member x.Texture = tex
            member x.Aspect = aspect
            member x.Levels = levels
            member x.Slices = slices

    type private TextureLevel(aspect : TextureAspect, tex : IBackendTexture, level : int, slices : Range1i) =
        member x.Size =
            let v = tex.Size / (1 <<< level)
            V3i(max 1 v.X, max 1 v.Y, max 1 v.Z)

        interface ITextureRange with
            member x.Texture = tex
            member x.Aspect = aspect
            member x.Levels = Range1i(level, level)
            member x.Slices = slices

        interface IFramebufferOutput with
            member x.Runtime = tex.Runtime
            member x.Size = x.Size.XY
            member x.Format = unbox (int tex.Format)
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

        member x.Size =
            let v = tex.Size / (1 <<< level)
            V3i(max 1 v.X, max 1 v.Y, max 1 v.Z)

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
            member x.Format = unbox (int tex.Format)
            member x.Samples = tex.Samples
            member x.Size = x.Size.XY

    type private Range1i with
        member x.SubRange(min : Option<int>, max : Option<int>) =
            let cnt = 1 + x.Max - x.Min
            let min = defaultArg min 0
            let max = defaultArg max (cnt - 1)
            Range1i(x.Min + min, x.Min + max)

    type IBackendTexture with

        member x.GetSlice(aspect : TextureAspect, minLevel : Option<int>, maxLevel : Option<int>, minSlice : Option<int>, maxSlice : Option<int>) =
            let level = Range1i(defaultArg minLevel 0, defaultArg maxLevel (x.MipMapLevels - 1))
            let slice = Range1i(defaultArg minSlice 0, defaultArg maxSlice (x.Count - 1))
            TextureRange(aspect, x, level, slice) :> ITextureRange

        member x.GetSlice(aspect : TextureAspect, minLevel : Option<int>, maxLevel : Option<int>, slice : int) =
            let level = Range1i(defaultArg minLevel 0, defaultArg maxLevel (x.MipMapLevels - 1))
            TextureSlice(aspect, x, level, slice) :> ITextureSlice

        member x.GetSlice(aspect : TextureAspect, level : int, minSlice : Option<int>, maxSlice : Option<int>) =
            let slice = Range1i(defaultArg minSlice 0, defaultArg maxSlice (x.Count - 1))
            TextureLevel(aspect, x, level, slice) :> ITextureLevel

        member x.Item
            with get(aspect : TextureAspect, level : int, slice : int) = SubTexture(aspect, x, level, slice) :> ITextureSubResource

        member x.Item
            with get(aspect : TextureAspect, level : int) = TextureLevel(aspect, x, level, Range1i(0, x.Count - 1)) :> ITextureLevel

        member x.Item
            with get(aspect : TextureAspect) = TextureRange(aspect, x, Range1i(0, x.MipMapLevels - 1), Range1i(0, x.Count - 1)) :> ITextureRange

    type ITextureRange with
        member x.GetSlice(minLevel : Option<int>, maxLevel : Option<int>, minSlice : Option<int>, maxSlice : Option<int>) =
            let level = x.Levels.SubRange(minLevel, maxLevel)
            let slice = x.Slices.SubRange(minSlice, maxSlice)
            TextureRange(x.Aspect, x.Texture, level, slice) :> ITextureRange

        member x.GetSlice(minLevel : Option<int>, maxLevel : Option<int>, slice : int) =
            let level = x.Levels.SubRange(minLevel, maxLevel)
            let slice = x.Slices.Min + slice
            TextureSlice(x.Aspect, x.Texture, level, slice) :> ITextureSlice

        member x.GetSlice(level : int, minSlice : Option<int>, maxSlice : Option<int>) =
            let level = x.Levels.Min + level
            let slice = x.Slices.SubRange(minSlice, maxSlice)
            TextureLevel(x.Aspect, x.Texture, level, slice) :> ITextureLevel

        member x.Item
            with get(level : int, slice : int) = SubTexture(x.Aspect, x.Texture, x.Levels.Min + level, x.Slices.Min + slice) :> ITextureSubResource

        member x.Item
            with get(level : int) = TextureLevel(x.Aspect, x.Texture, x.Levels.Min + level, x.Slices) :> ITextureLevel

    type ITextureLevel with
        member x.GetSlice(minSlice : Option<int>, maxSlice : Option<int>) =
            let slice = x.Slices.SubRange(minSlice, maxSlice)
            TextureLevel(x.Aspect, x.Texture, x.Level, slice) :> ITextureRange

        member x.Item
            with get(slice : int) = SubTexture(x.Aspect, x.Texture,x.Level, x.Slices.Min + slice) :> ITextureSubResource

    type ITextureSlice with
        member x.GetSlice(minLevel : Option<int>, maxLevel : Option<int>) =
            let levels = x.Levels.SubRange(minLevel, maxLevel)
            TextureSlice(x.Aspect, x.Texture, levels, x.Slice) :> ITextureRange

        member x.Item
            with get(level : int) = SubTexture(x.Aspect, x.Texture, x.Levels.Min + level, x.Slice) :> ITextureSubResource