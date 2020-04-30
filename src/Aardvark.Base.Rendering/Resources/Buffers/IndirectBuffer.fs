namespace Aardvark.Base

/// native indirect buffers are supposed to have data layout as required by the graphics API, currently this is equal for OpenGL, Vulkan (and DX11)
/// Indexed:     { uint count; uint primCount; uint firstIndex; uint baseVertex; uint baseInstance; }
/// Non-indexed: { uint count; uint primCount; uint first; uint baseInstance; }
/// NOTE:
///  1. indexed or non-indexed only matters if directly an IBackendBuffer is used, ArrayBuffers will be uploaded according to if there are indirect present
///   -> constructor intended to be used with IBackendBuffer, other should use IndirectBuffer module
///  2. stride is actually hard-coded to 20 in glvm and vkvm
type IndirectBuffer(b : IBuffer, count : int, stride : int, indexed : bool) =
    member x.Buffer = b
    member x.Count = count
    member x.Stride = stride /// not supported, hardcoded to 20 in execution
    member x.Indexed = indexed

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module IndirectBuffer =

    let inline ofArray (indexed : bool) (arr : ^a[]) =
        IndirectBuffer(ArrayBuffer arr, arr.Length, sizeof< ^a>, indexed)

    let inline ofList (indexed : bool) (l : ^a list) =
        l |> List.toArray |> ofArray indexed