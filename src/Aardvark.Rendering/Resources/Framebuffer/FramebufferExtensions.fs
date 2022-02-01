namespace Aardvark.Rendering

open Aardvark.Base
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System

[<AutoOpen>]
module IFramebufferRuntimeFSharpExtensions =

    // These extensions use SRTPs so MUST NOT be exposed to C#
    type IFramebufferRuntime with

        // ================================================================================================================
        // Clear
        // ================================================================================================================

        /// Clears the framebuffer with the given color.
        member inline x.Clear(fbo : IFramebuffer, color : ^Color) =
            let values = color |> (fun c -> clear { color c })
            x.Clear(fbo, values)

        /// Clears the framebuffer with the given colors.
        member inline x.Clear(fbo : IFramebuffer, colors : Map<Symbol, ^Color>) =
            let values = colors |> (fun c -> clear { colors c })
            x.Clear(fbo, values)

        /// Clears the framebuffer with the given colors.
        member inline x.Clear(fbo : IFramebuffer, colors : seq<Symbol * ^Color>) =
            let values = colors |> (fun c -> clear { colors c })
            x.Clear(fbo, values)

        /// Clears the framebuffer with the given color and depth.
        member inline x.Clear(fbo : IFramebuffer, color : ^Color, depth : ^Depth) =
            let values = (color, depth) ||> (fun c d -> clear { color c; depth d })
            x.Clear(fbo, values)

        /// Clears the framebuffer with the given colors and depth.
        member inline x.Clear(fbo : IFramebuffer, colors : Map<Symbol, ^Color>, depth : ^Depth) =
            let values = (colors, depth) ||> (fun c d -> clear { colors c; depth d })
            x.Clear(fbo, values)

        /// Clears the framebuffer with the given colors and depth.
        member inline x.Clear(fbo : IFramebuffer, colors : seq<Symbol * ^Color>, depth : ^Depth) =
            let values = (colors, depth) ||> (fun c d -> clear { colors c; depth d })
            x.Clear(fbo, values)

        /// Clears the framebuffer with the given color, depth and stencil values.
        member inline x.Clear(fbo : IFramebuffer, color : ^Color, depth : ^Depth, stencil : ^Stencil) =
            let values = (color, depth, stencil) |||> (fun c d s -> clear { color c; depth d; stencil s })
            x.Clear(fbo, values)

        /// Clears the framebuffer with the given colors, depth and stencil values.
        member inline x.Clear(fbo : IFramebuffer, colors : Map<Symbol, ^Color>, depth : ^Depth, stencil : ^Stencil) =
            let values = (colors, depth, stencil) |||> (fun c d s -> clear { colors c; depth d; stencil s })
            x.Clear(fbo, values)

        /// Clears the framebuffer with the given colors, depth and stencil values.
        member inline x.Clear(fbo : IFramebuffer, colors : seq<Symbol * ^Color>, depth : ^Depth, stencil : ^Stencil) =
            let values = (colors, depth, stencil) |||> (fun c d s -> clear { colors c; depth d; stencil s })
            x.Clear(fbo, values)

        /// Clears the framebuffer with the given depth value.
        member inline x.ClearDepth(fbo : IFramebuffer, depth : ^Depth) =
            let values = depth |> (fun d -> clear { depth d })
            x.Clear(fbo, values)

        /// Clears the framebuffer with the given stencil value.
        member inline x.ClearStencil(fbo : IFramebuffer, stencil : ^Stencil) =
            let values = stencil |> (fun s -> clear { stencil s })
            x.Clear(fbo, values)

        /// Clears the framebuffer with the given depth and stencil values.
        member inline x.ClearDepthStencil(fbo : IFramebuffer, depth : ^Depth, stencil : ^Stencil) =
            let values = (depth, stencil) ||> (fun d s -> clear { depth d; stencil s })
            x.Clear(fbo, values)


[<AbstractClass; Sealed; Extension>]
type IFramebufferRuntimeExtensions private() =

    // ================================================================================================================
    // CreateFramebufferSignature
    // ================================================================================================================

    ///<summary>Creates a framebuffer signature with the given attachment signatures.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="attachments">The color and (optional) depth-stencil attachment signatures.</param>
    ///<param name="samples">The number of samples. Default is 1.</param>
    ///<param name="layers">The number of layers. Default is 1.</param>
    ///<param name="perLayerUniforms">The names of per-layer uniforms. Default is null.</param>
    [<Extension>]
    static member CreateFramebufferSignature(this : IFramebufferRuntime,
                                             attachments : Map<Symbol, TextureFormat>,
                                             [<Optional; DefaultParameterValue(1)>] samples : int,
                                             [<Optional; DefaultParameterValue(1)>] layers : int,
                                             [<Optional; DefaultParameterValue(null : seq<string>)>] perLayerUniforms : seq<string>) =
        let colorAttachments =
            attachments
            |> Map.toList
            |> List.filter (fst >> (<>) DefaultSemantic.DepthStencil)
            |> List.mapi (fun i (n, f) -> i, { Name = n; Format = f })
            |> Map.ofList

        let depthStencilAttachment =
            attachments |> Map.tryFind DefaultSemantic.DepthStencil

        this.CreateFramebufferSignature(colorAttachments, depthStencilAttachment, samples, layers, perLayerUniforms)

    ///<summary>Creates a framebuffer signature with the given attachment signatures.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="attachments">The color and (optional) depth-stencil attachment signatures.</param>
    ///<param name="samples">The number of samples. Default is 1.</param>
    ///<param name="layers">The number of layers. Default is 1.</param>
    ///<param name="perLayerUniforms">The names of per-layer uniforms. Default is null.</param>
    [<Extension>]
    static member CreateFramebufferSignature(this : IFramebufferRuntime,
                                             attachments : SymbolDict<TextureFormat>,
                                             [<Optional; DefaultParameterValue(1)>] samples : int,
                                             [<Optional; DefaultParameterValue(1)>] layers : int,
                                             [<Optional; DefaultParameterValue(null : seq<string>)>] perLayerUniforms : seq<string>) =
        let atts = attachments |> Seq.map (fun x -> x.Key, x.Value) |> Map.ofSeq
        this.CreateFramebufferSignature(atts, samples, layers, perLayerUniforms)

    ///<summary>Creates a framebuffer signature with the given attachment signatures.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="attachments">The color and (optional) depth-stencil attachment signatures. The order is not preserved.</param>
    ///<param name="samples">The number of samples. Default is 1.</param>
    ///<param name="layers">The number of layers. Default is 1.</param>
    ///<param name="perLayerUniforms">The names of per-layer uniforms. Default is null.</param>
    [<Extension>]
    static member CreateFramebufferSignature(this : IFramebufferRuntime,
                                             attachments : seq<Symbol * TextureFormat>,
                                             [<Optional; DefaultParameterValue(1)>] samples : int,
                                             [<Optional; DefaultParameterValue(1)>] layers : int,
                                             [<Optional; DefaultParameterValue(null : seq<string>)>] perLayerUniforms : seq<string>) =
        this.CreateFramebufferSignature(Map.ofSeq attachments, samples, layers, perLayerUniforms)

    [<Extension>]
    static member DeleteFramebufferSignature(this : IFramebufferRuntime, signature : IFramebufferSignature) =
        signature.Dispose()

    // ================================================================================================================
    // CreateFramebuffer
    // ================================================================================================================

    ///<summary>Creates a framebuffer with the given attachments.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="signature">The signature of the framebuffer to create.</param>
    ///<param name="attachments">The attachments. Attachments with name DefaultSemantic.DepthStencil are used as depth-stencil attachment.</param>
    [<Extension>]
    static member CreateFramebuffer(this : IFramebufferRuntime, signature : IFramebufferSignature, attachments : seq<Symbol * IFramebufferOutput>) =
        this.CreateFramebuffer(signature, Map.ofSeq attachments)

    [<Extension>]
    static member DeleteFramebuffer(this : IFramebufferRuntime, framebuffer : IFramebuffer) =
        framebuffer.Dispose()


[<AutoOpen>]
module IFramebufferSignatureExtensions =

    let private colorsAssignableTo (mine : Map<int, AttachmentSignature>) (other : Map<int, AttachmentSignature>) =
        mine |> Map.forall (fun slot mine ->
            match Map.tryFind slot other with
            | Some other -> mine = other
            | _ -> false
        )

    let private depthAssignableTo (mine : Option<TextureFormat>) (other : Option<TextureFormat>) =
        match mine, other with
        | Some mine, Some other -> mine = other
        | Some _, None -> false
        | None, _ -> true

    type IFramebufferSignature with

        /// Returns the number of color attachment slots used by the signature
        /// (i.e. the maximum slot + 1 or 0 if there are no color attachments).
        member x.ColorAttachmentSlots =
            match x.ColorAttachments |> Map.toArray |> Array.tryLast with
            | Some (slot, _) -> slot + 1
            | _ -> 0

    [<AbstractClass; Sealed; Extension>]
    type IFramebufferSignatureExtensions private() =

        /// Returns whether the signature contains an attachment with given name.
        /// If semantic is DefaultSemantic.DepthStencil, returns if a depth-stencil attachment is present.
        [<Extension>]
        static member Contains(this : IFramebufferSignature, semantic : Symbol) =
            if semantic = DefaultSemantic.DepthStencil then
                this.DepthStencilAttachment.IsSome
            else
                this.ColorAttachments |> Map.exists (fun _ att -> att.Name = semantic)

        /// Gets the semantics of the signature attachments as a set.
        /// If a depth-stencil attachment is present, the returned set contains DefaultSemantic.DepthStencil.
        [<Extension>]
        static member GetSemantics(this : IFramebufferSignature) =
            let colors =
                this.ColorAttachments
                |> Map.toList
                |> List.map (snd >> AttachmentSignature.name)
                |> Set.ofList

            if this.DepthStencilAttachment.IsSome then
                colors |> Set.add DefaultSemantic.DepthStencil
            else
                colors

        /// Checks if the signature is compatible with the given signature (i.e. they are equivalent).
        [<Extension>]
        static member IsCompatibleWith (this : IFramebufferSignature, other : IFramebufferSignature) =
            if LanguagePrimitives.PhysicalEquality this other then
                true
            else
                this.Samples = other.Samples &&
                this.LayerCount = other.LayerCount &&
                this.PerLayerUniforms = other.PerLayerUniforms &&
                this.ColorAttachments = other.ColorAttachments &&
                this.DepthStencilAttachment = other.DepthStencilAttachment

        /// Check if the output with the signature is assignable to the given framebuffer.
        /// Each color and depth output must have a counter part in the framebuffer, layer count and uniforms,
        /// and sample count must match.
        [<Extension>]
        static member IsAssignableTo (this : IFramebufferSignature, fbo : IFramebuffer) =
            if LanguagePrimitives.PhysicalEquality this fbo.Signature then
                true
            else
                this.Samples = fbo.Signature.Samples &&
                this.LayerCount = fbo.Signature.LayerCount &&
                this.PerLayerUniforms = fbo.Signature.PerLayerUniforms &&
                colorsAssignableTo this.ColorAttachments fbo.Signature.ColorAttachments &&
                depthAssignableTo this.DepthStencilAttachment fbo.Signature.DepthStencilAttachment

        /// Creates a framebuffer of the given signature and with the given attachments.
        [<Extension>]
        static member CreateFramebuffer (this : IFramebufferSignature, attachments : Map<Symbol, IFramebufferOutput>) =
            this.Runtime.CreateFramebuffer(this, attachments)

        /// Creates a framebuffer of the given signature and with the given attachments.
        [<Extension>]
        static member CreateFramebuffer (this : IFramebufferSignature, attachments : seq<Symbol * IFramebufferOutput>) =
            this.Runtime.CreateFramebuffer(this, attachments)