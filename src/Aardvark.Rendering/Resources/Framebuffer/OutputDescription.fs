namespace Aardvark.Rendering

open Aardvark.Base

type OutputDescription =
    {
        framebuffer : IFramebuffer
        images      : Map<Symbol, BackendTextureOutputView>
        viewport    : Box2i
        overrides   : Map<string, obj>
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module OutputDescription =

    let ofFramebuffer (framebuffer : IFramebuffer) =
        {
            framebuffer = framebuffer
            images = Map.empty
            viewport = Box2i.FromMinAndSize(V2i.OO, framebuffer.Size - V2i.II)
            overrides = Map.empty
        }