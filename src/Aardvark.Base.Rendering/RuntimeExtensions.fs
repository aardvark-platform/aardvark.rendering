namespace Aardvark.Base

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open System.Collections.Generic
open Aardvark.Base.Rendering
open System.Runtime.CompilerServices

[<AbstractClass; Sealed; Extension>]
type RuntimeExtensions private() =
    
    static let levelSize (level : int) (s : V2i) =
        V2i(max 1 (s.X / (1 <<< level)), max 1 (s.Y / (1 <<< level)))

    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, color : IMod<C4f>, depth : IMod<float>) =
        this.CompileClear(signature, color |> Mod.map Some, depth |> Mod.map Some)

    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, color : IMod<C4f>) =
        this.CompileClear(signature, color |> Mod.map Some, Mod.constant None)

    [<Extension>]
    static member CompileClear(this : IRuntime, signature : IFramebufferSignature, depth : IMod<float>) =
        this.CompileClear(signature, Mod.constant None, depth |> Mod.map Some)

    [<Extension>]
    static member Download(this : IRuntime, texture : IBackendTexture, level : int, slice : int, format : PixFormat) =
        let size = texture.Size.XY |> levelSize level
        let pi = PixImage.Create(format, int64 size.X, int64 size.Y)
        this.Download(texture, level, slice, pi)
        pi

    [<Extension>]
    static member Download(this : IRuntime, texture : IBackendTexture, level : int, format : PixFormat) =
        let size = texture.Size.XY |> levelSize level
        let pi = PixImage.Create(format, int64 size.X, int64 size.Y)
        this.Download(texture, level, 0, pi)
        pi

    [<Extension>]
    static member Download(this : IRuntime, texture : IBackendTexture, format : PixFormat) =
        let pi = PixImage.Create(format, int64 texture.Size.X, int64 texture.Size.Y)
        this.Download(texture, 0, 0, pi)
        pi

    [<Extension>]
    static member Download(this : IRuntime, texture : IBackendTexture, level : int, slice : int) =
        let pixFormat = TextureFormat.toDownloadFormat texture.Format
        RuntimeExtensions.Download(this, texture, level, slice, pixFormat)


    [<Extension>]
    static member Download(this : IRuntime, texture : IBackendTexture, level : int) =
        let pixFormat = TextureFormat.toDownloadFormat texture.Format
        RuntimeExtensions.Download(this, texture, level, 0, pixFormat)

    [<Extension>]
    static member Download(this : IRuntime, texture : IBackendTexture) =
        let pixFormat = TextureFormat.toDownloadFormat texture.Format
        RuntimeExtensions.Download(this, texture, 0, 0, pixFormat)


    [<Extension>]
    static member Upload(this : IRuntime, texture : IBackendTexture, level : int, source : PixImage) =
        this.Upload(texture, level, 0, source)

    [<Extension>]
    static member Upload(this : IRuntime, texture : IBackendTexture, source : PixImage) =
        this.Upload(texture, 0, 0, source)

    [<Extension>]
    static member CreateFramebufferSignature(this : IRuntime, l : seq<Symbol * AttachmentSignature>) =
        this.CreateFramebufferSignature(SymDict.ofSeq l)

    [<Extension>]
    static member CreateFramebufferSignature(this : IRuntime, l : list<Symbol * AttachmentSignature>) =
        this.CreateFramebufferSignature(SymDict.ofList l)

    [<Extension>]
    static member CreateFramebufferSignature(this : IRuntime, l : Map<Symbol, AttachmentSignature>) =
        this.CreateFramebufferSignature(SymDict.ofMap l)

[<AbstractClass; Sealed; Extension>]
type IBackendTextureExtensions private() =
    
    [<Extension>]
    static member GetOutputView(this : IBackendTexture, level : int, slice : int) =
        { texture = this; level = level; slice = slice } :> IFramebufferOutput

    [<Extension>]
    static member GetOutputView(this : IBackendTexture, level : int) =
        { texture = this; level = level; slice = 0 } :> IFramebufferOutput

    [<Extension>]
    static member GetOutputView(this : IBackendTexture) =
        { texture = this; level = 0; slice = 0 } :> IFramebufferOutput
