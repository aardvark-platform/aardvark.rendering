namespace Aardvark.Rendering

open System
open Aardvark.Base
open System.Runtime.InteropServices

/// Describes the signature of a color attachment.
[<Struct; CLIMutable>]
type AttachmentSignature =
    {
        /// Name of the attachment.
        Name : Symbol

        /// Format of the attachment.
        Format : TextureFormat
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AttachmentSignature =
    let name (att : AttachmentSignature) = att.Name
    let format (att : AttachmentSignature) = att.Format

[<AllowNullLiteral>]
type IFramebufferSignature =
    inherit IDisposable
    abstract member Runtime : IFramebufferRuntime
    abstract member Samples : int
    abstract member ColorAttachments : Map<int, AttachmentSignature>
    abstract member DepthStencilAttachment : Option<TextureFormat>
    abstract member LayerCount : int
    abstract member PerLayerUniforms : Set<string>

and IFramebuffer =
    inherit IDisposable
    abstract member Signature : IFramebufferSignature
    abstract member Size : V2i
    abstract member Handle : uint64
    abstract member Attachments : Map<Symbol, IFramebufferOutput>


and IFramebufferRuntime =
    inherit ITextureRuntime

    abstract member DeviceCount : int

    /// Target depth range of shaders.
    /// The final depth will be mapped from [-1, 1] to the target range.
    abstract member ShaderDepthRange : Range1f

    /// Returns whether the runtime supports shader inputs for layered and multiviewport rendering.
    /// If false, shaders must use custom inputs.
    abstract member SupportsLayeredShaderInputs : bool

    ///<summary>Creates a framebuffer signature with the given attachment signatures.</summary>
    ///<param name="colorAttachments">The color attachment signatures. The keys determine the slot of the corrsponding attachment.</param>
    ///<param name="depthStencilAttachment">The optional depth-stencil attachment signature.</param>
    ///<param name="samples">The number of samples. Default is 1.</param>
    ///<param name="layers">The number of layers. Default is 1.</param>
    ///<param name="perLayerUniforms">The names of per-layer uniforms. Default is null.</param>
    abstract member CreateFramebufferSignature : colorAttachments : Map<int, AttachmentSignature> *
                                                 depthStencilAttachment : Option<TextureFormat> *
                                                 [<Optional; DefaultParameterValue(1)>] samples : int *
                                                 [<Optional; DefaultParameterValue(1)>] layers : int *
                                                 [<Optional; DefaultParameterValue(null : seq<string>)>] perLayerUniforms : seq<string> -> IFramebufferSignature

    ///<summary>Creates a framebuffer with the given attachments.</summary>
    ///<param name="signature">The signature of the framebuffer to create.</param>
    ///<param name="attachments">The attachments. Attachments with name DefaultSemantic.DepthStencil are used as depth-stencil attachment.</param>
    abstract member CreateFramebuffer : signature : IFramebufferSignature * attachments : Map<Symbol, IFramebufferOutput> -> IFramebuffer

    abstract member Copy : src : IFramebuffer * dst : IFramebuffer -> unit

    abstract member ReadPixels : src : IFramebuffer * attachment : Symbol * offset : V2i * size : V2i -> PixImage

    /// Clears the framebuffer with the given values.
    abstract member Clear : fbo : IFramebuffer * values : ClearValues -> unit