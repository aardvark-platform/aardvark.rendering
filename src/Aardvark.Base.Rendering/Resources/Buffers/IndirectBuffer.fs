namespace Aardvark.Base

open System.Runtime.InteropServices

[<StructLayout(LayoutKind.Sequential)>]
type DrawCallInfo =
    struct
        val mutable public FaceVertexCount : int
        val mutable public InstanceCount : int
        val mutable public FirstIndex : int
        val mutable public FirstInstance : int
        val mutable public BaseVertex : int

        new(faceVertexCount : int) = {
            FaceVertexCount = faceVertexCount;
            InstanceCount = 1;
            FirstIndex = 0;
            FirstInstance = 0;
            BaseVertex = 0;
        }
    end

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