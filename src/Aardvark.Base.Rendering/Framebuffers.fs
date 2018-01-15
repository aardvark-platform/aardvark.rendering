﻿namespace Aardvark.Base


open System
open Aardvark.Base.Incremental

type IBackendTextureOutputView =
    inherit IFramebufferOutput
    abstract member texture : IBackendTexture
    abstract member level : int
    abstract member slices : Range1i

type BackendTextureOutputView = { texture : IBackendTexture; level : int; slice : int } with
    interface IBackendTextureOutputView with
        member x.texture = x.texture
        member x.level = x.level
        member x.slices = 
            if x.slice < 0 then Range1i(0, x.texture.Count - 1)
            else Range1i(x.slice, x.slice)

    interface IFramebufferOutput with
        member x.Runtime = x.texture.Runtime
        member x.Format = TextureFormat.toRenderbufferFormat x.texture.Format
        member x.Samples = x.texture.Samples
        member x.Size = 
            let s = x.texture.Size.XY
            V2i(max 1 (s.X / (1 <<< x.level)), max 1 (s.Y / (1 <<< x.level)))

    interface ITextureLevel with
        member x.Texture = x.texture
        member x.Levels = Range1i(x.level, x.level)
        member x.Level = x.level
        member x.Slices = 
            if x.slice < 0 then Range1i(0, x.texture.Count - 1)
            else Range1i(x.slice, x.slice)
        member x.Size =
            let s = x.texture.Size
            let d = 1 <<< x.level
            V3i(max 1 (s.X / d), max 1 (s.Y / d), max 1 (s.Z / d))

        member x.Aspect =
            if TextureFormat.hasDepth x.texture.Format then TextureAspect.Depth
            else TextureAspect.Color
            

type AttachmentSignature = { format : RenderbufferFormat; samples : int }


type INativeTextureData = 
    abstract member Size : V3i
    abstract member SizeInBytes : int64
    abstract member Use : (nativeint -> 'a) -> 'a

[<AllowNullLiteral>]
type INativeTexture =
    inherit ITexture
    abstract member Dimension : TextureDimension
    abstract member Format : TextureFormat
    abstract member MipMapLevels : int
    abstract member Count : int
    abstract member Item : slice : int * level : int -> INativeTextureData with get

