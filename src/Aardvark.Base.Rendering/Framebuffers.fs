namespace Aardvark.Base


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
        member x.Format = TextureFormat.toRenderbufferFormat x.texture.Format
        member x.Samples = x.texture.Samples
        member x.Size = 
            let s = x.texture.Size.XY
            V2i(max 1 (s.X / (1 <<< x.level)), max 1 (s.Y / (1 <<< x.level)))

[<AutoOpen>]
module ``Texture Range Extensions`` =

    type private TexLayers(texture : IBackendTexture, level : int, slices : Range1i) =
        member x.texture = texture
        member x.level = level
        member x.slices = slices

        interface IBackendTextureOutputView with
            member x.texture = texture
            member x.level = level
            member x.slices = slices

        interface IFramebufferOutput with
            member x.Format = TextureFormat.toRenderbufferFormat texture.Format
            member x.Samples = texture.Samples
            member x.Size = 
                let s = x.texture.Size.XY
                V2i(max 1 (s.X / (1 <<< level)), max 1 (s.Y / (1 <<< level)))

        

    type IBackendTexture with
        member x.Item
            with get (level : int) = TexLayers(x, level, Range1i(0, x.Count - 1)) :> IBackendTextureOutputView
        
        member x.Item
            with get (level : int, slice : int) =  { texture = x; level = level; slice = slice }
        
        member x.GetSlice(level : int, min : Option<int>, max : Option<int>) =
            let min = defaultArg min 0
            let max = defaultArg max (x.Count - 1)
            TexLayers(x, level, Range1i(min, max)) :> IBackendTextureOutputView

    type IBackendTextureOutputView with
        member x.Item
            with get (slice : int) = { texture = x.texture; level = x.level; slice = x.slices.Min + slice }
        
        member x.GetSlice(min : Option<int>, max : Option<int>) =
            let min = defaultArg min 0
            let max = defaultArg max x.slices.Max
            TexLayers(x.texture, x.level, Range1i(min, max)) :> IBackendTextureOutputView


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

