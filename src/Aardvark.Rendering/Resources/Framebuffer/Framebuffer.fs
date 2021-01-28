namespace Aardvark.Rendering

open System
open Aardvark.Base
open FSharp.Data.Adaptive

type FramebufferLayout =
    {
        ColorAttachments : Map<int, Symbol * AttachmentSignature>
        DepthAttachment : Option<AttachmentSignature>
        StencilAttachment : Option<AttachmentSignature>
        LayerCount : int
        PerLayerUniforms : Set<string>
    }

[<AllowNullLiteral>]
type IFramebufferSignature =
    abstract member Runtime : IFramebufferRuntime
    abstract member ColorAttachments : Map<int, Symbol * AttachmentSignature>
    abstract member DepthAttachment : Option<AttachmentSignature>
    abstract member StencilAttachment : Option<AttachmentSignature>

    abstract member LayerCount : int
    abstract member PerLayerUniforms : Set<string>


and IFramebuffer =
    inherit IDisposable
    abstract member Signature : IFramebufferSignature
    abstract member Size : V2i
    abstract member GetHandle : IAdaptiveObject -> obj
    abstract member Attachments : Map<Symbol, IFramebufferOutput>


and IFramebufferRuntime =
    inherit ITextureRuntime

    abstract member DeviceCount : int

    /// Creates a framebuffer signature with the given attachment signatures.
    abstract member CreateFramebufferSignature : attachments : Map<Symbol, AttachmentSignature> * layers : int * perLayerUniforms : Set<string> -> IFramebufferSignature

    /// Deletes the given framebuffer signature.
    abstract member DeleteFramebufferSignature : IFramebufferSignature -> unit

    /// Creates a framebuffer of the given signature and with the given attachments.
    abstract member CreateFramebuffer : signature : IFramebufferSignature * attachments : Map<Symbol, IFramebufferOutput> -> IFramebuffer

    /// Deletes the given framebuffer.
    abstract member DeleteFramebuffer : IFramebuffer -> unit

    /// Clears the given color attachments, and (optionally) the depth and stencil attachments to the specified values.
    abstract member Clear : fbo : IFramebuffer * clearColors : Map<Symbol, C4f> * depth : Option<float> * stencil : Option<int> -> unit