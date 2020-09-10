namespace Aardvark.Rendering

open System
open Aardvark.Base
open FSharp.Data.Adaptive

[<AllowNullLiteral>]
type IFramebufferSignature =
    abstract member Runtime : IFramebufferRuntime
    abstract member ColorAttachments : Map<int, Symbol * AttachmentSignature>
    abstract member DepthAttachment : Option<AttachmentSignature>
    abstract member StencilAttachment : Option<AttachmentSignature>

    abstract member LayerCount : int
    abstract member PerLayerUniforms : Set<string>

    abstract member IsAssignableFrom : other : IFramebufferSignature -> bool


and IFramebuffer =
    inherit IDisposable
    abstract member Signature : IFramebufferSignature
    abstract member Size : V2i
    abstract member GetHandle : IAdaptiveObject -> obj
    abstract member Attachments : Map<Symbol, IFramebufferOutput>


and IFramebufferRuntime =
    inherit ITextureRuntime

    abstract member DeviceCount : int

    abstract member CreateFramebufferSignature : attachments : SymbolDict<AttachmentSignature> * layers : int * perLayerUniforms : Set<string> -> IFramebufferSignature
    abstract member DeleteFramebufferSignature : IFramebufferSignature -> unit

    abstract member CreateFramebuffer : signature : IFramebufferSignature * attachments : Map<Symbol, IFramebufferOutput> -> IFramebuffer
    abstract member DeleteFramebuffer : IFramebuffer -> unit

    abstract member Clear : fbo : IFramebuffer * clearColors : Map<Symbol, C4f> * depth : Option<float> * stencil : Option<int> -> unit