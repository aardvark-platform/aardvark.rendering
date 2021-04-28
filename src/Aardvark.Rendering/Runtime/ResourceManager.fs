namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive


[<AllowNullLiteral>]
type IResourceManager =
    abstract member CreateSurface : signature : IFramebufferSignature * surface : aval<ISurface> -> IResource<IBackendSurface>
    abstract member CreateBuffer : buffer : aval<IBuffer> -> IResource<IBackendBuffer>
    abstract member CreateTexture : texture : aval<ITexture> -> IResource<IBackendTexture>
