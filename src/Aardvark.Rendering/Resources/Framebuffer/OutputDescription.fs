namespace Aardvark.Rendering

open Aardvark.Base

/// Describes the output target of a render task.
[<Struct>]
type OutputDescription =
    {
        /// The target framebuffer of the render task.
        Framebuffer : IFramebuffer

        /// <summary>
        /// The region of the framebuffer that will be rendered to.
        /// It determines the transformation from normalized device coordinates to framebuffer coordinates.
        /// Viewport.Min and Viewport.Max are the framebuffer coordinates of the viewport's lower left and upper right corners (exclusive) respectively.
        /// </summary>
        /// <remarks>
        /// The viewport can be set on a per render object basis, overriding the viewport in the output description.
        /// </remarks>
        /// <seealso cref="RenderObject.ViewportState" />
        /// <seealso cref="PipelineState.ViewportState" />
        Viewport    : Box2i

        /// <summary>
        /// The region of the framebuffer that can be modified by the render task.
        /// Fragments with coordinates outside of the scissor region will be discarded.
        /// Scissor.Min and Scissor.Max are the framebuffer coordinates of the scissor's lower left and upper right corners (exclusive) respectively.
        /// </summary>
        /// <remarks>
        /// The scissor can be set on a per render object basis, overriding the scissor in the output description.
        /// </remarks>
        /// <seealso cref="RenderObject.ViewportState" />
        /// <seealso cref="PipelineState.ViewportState" />
        Scissor     : Box2i
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module OutputDescription =

    /// <summary>
    /// Creates an output description from a framebuffer.
    /// The whole framebuffer is used as render target.
    /// </summary>
    /// <param name="framebuffer">The framebuffer to create the output description from.</param>
    let ofFramebuffer (framebuffer: IFramebuffer) =
        let viewport = Box2i.FromMinAndSize(V2i.OO, framebuffer.Size)
        { Framebuffer = framebuffer
          Viewport    = viewport
          Scissor     = viewport }