namespace Aardvark.Rendering

open Aardvark.Base
open System.Runtime.InteropServices

/// Struct describing a draw call.
/// The layout is compatible with the layout required for non-indexed draw calls (barring the stride).
/// For indexed drawing, BaseVertex and FirstInstance need to be swapped.
//
// See (same for OpenGL and Direct3D):
// https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/VkDrawIndirectCommand.html
// https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/VkDrawIndexedIndirectCommand.html
[<StructLayout(LayoutKind.Sequential)>]
type DrawCallInfo =
    struct
        /// The number of vertices to draw.
        val mutable public FaceVertexCount : int

        /// The number of instances to draw.
        val mutable public InstanceCount : int

        /// The index of the first vertex to draw for non-indexed data, or the base index within the index buffer otherwise.
        val mutable public FirstIndex : int

        /// The instance ID of the first instance to draw.
        val mutable public FirstInstance : int

        /// The value added to the vertex index before indexing into the vertex buffer (indexed data only).
        val mutable public BaseVertex : int

        /// Creates a new DrawCallInfo for drawing a single geometry with
        /// the given face vertex count.
        new(faceVertexCount : int) = {
            FaceVertexCount = faceVertexCount;
            InstanceCount = 1;
            FirstIndex = 0;
            FirstInstance = 0;
            BaseVertex = 0;
        }

        /// Swaps the base vertex and first instance fields to switch between indexed and non-indexed drawing.
        static member inline ToggleIndexed(call : byref<DrawCallInfo>) =
            Fun.Swap(&call.BaseVertex, &call.FirstInstance)

    end

/// Type for describing buffers holding draw calls.
type IndirectBuffer =
    {
        /// The buffer holding the draw calls.
        Buffer  : IBuffer

        /// The number of draw calls in the buffer.
        Count   : int

        /// The number of bytes between the beginning of two successive draw calls.
        Stride  : int

        /// Determines if the buffer contains indexed draw calls.
        Indexed : bool
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module IndirectBuffer =

    /// Creates an indirect buffer of the given buffer.
    /// If the buffer is a backend buffer, the data must be in the correct layout (i.e. indexed or non-indexed).
    let inline ofBuffer (indexed : bool) (stride : int) (count : int) (buffer : IBuffer) =
        { Buffer  = buffer
          Count   = count
          Stride  = stride
          Indexed = indexed }

    /// Creates an indirect buffer of the given array.
    /// The indexed parameter determines the data layout of the draw calls (default is false).
    let inline ofArray' (indexed : bool) (calls : DrawCallInfo[]) =
        { Buffer  = ArrayBuffer calls
          Count   = calls.Length
          Stride  = sizeof<DrawCallInfo>
          Indexed = indexed }

    /// Creates an indirect buffer of the given array.
    /// The draw calls are assumed to be in the non-indexed layout.
    let inline ofArray (calls : DrawCallInfo[]) =
        calls |> ofArray' false

    /// Creates an indirect buffer of the given list.
    /// The indexed parameter determines the data layout of the draw calls (default is false).
    let inline ofList' (indexed : bool) (calls : DrawCallInfo list) =
        calls |> List.toArray |> ofArray' indexed

    /// Creates an indirect buffer of the given list.
    /// The draw calls are assumed to be in the non-indexed layout.
    let inline ofList (calls : DrawCallInfo list) =
        calls |> ofList' false