namespace Aardvark.Rendering

open System.Runtime.InteropServices

[<Struct; StructLayout(LayoutKind.Sequential); CLIMutable>]
type DrawCallInfo =
    {
        FaceVertexCount : int
        InstanceCount   : int
        FirstIndex      : int
        FirstInstance   : int
        BaseVertex      : int
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DrawCallInfo =

    let empty =
        {
            FaceVertexCount = 0
            InstanceCount   = 0
            FirstIndex      = 0
            FirstInstance   = 0
            BaseVertex      = 0
        }

/// native indirect buffers are supposed to have data layout as required by the graphics API, currently this is equal for OpenGL, Vulkan (and DX11)
/// Indexed:     { uint count; uint primCount; uint firstIndex; uint baseVertex; uint baseInstance; }
/// Non-indexed: { uint count; uint primCount; uint first; uint baseInstance; }
/// NOTE: indexed or non-indexed only matters if directly an IBackendBuffer is used, ArrayBuffers will be uploaded according to if there are indices present
///        -> constructor intended to be used with IBackendBuffer, other should use IndirectBuffer module
type IndirectBuffer(b : IBuffer, count : int, stride : int, indexed : bool) =
    member x.Buffer = b
    member x.Count = count
    member x.Stride = stride
    member x.Indexed = indexed

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module IndirectBuffer =

    let inline ofArray (indexed : bool) (arr : DrawCallInfo[]) =
        IndirectBuffer(ArrayBuffer arr, arr.Length, sizeof<DrawCallInfo>, indexed)

    let inline ofList (indexed : bool) (l : DrawCallInfo list) =
        l |> List.toArray |> ofArray indexed