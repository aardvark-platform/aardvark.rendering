namespace Aardvark.Rendering

open System
open FSharp.Data.Adaptive

[<AllowNullLiteral>]
type IResourceManager =

    [<Obsolete("To be removed.")>]
    abstract member CreateSurface : signature : IFramebufferSignature * surface : aval<ISurface> -> IResource<IBackendSurface>

    [<Obsolete("To be removed.")>]
    abstract member CreateBuffer : buffer : aval<IBuffer> -> IResource<IBackendBuffer>

    [<Obsolete("To be removed.")>]
    abstract member CreateTexture : texture : aval<ITexture> -> IResource<IBackendTexture>
