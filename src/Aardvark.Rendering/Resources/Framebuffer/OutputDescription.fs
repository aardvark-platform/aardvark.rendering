namespace Aardvark.Rendering

open Aardvark.Base

[<Struct>]
type OutputDescription =
    {
        framebuffer : IFramebuffer
        viewport    : Box2i
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module OutputDescription =

    let ofFramebuffer (framebuffer : IFramebuffer) =
        {
            framebuffer = framebuffer
            viewport = Box2i.FromMinAndSize(V2i.OO, framebuffer.Size - V2i.II)
        }