namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.NativeInterop
open System
open System.Runtime.InteropServices

#nowarn "9"

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

        /// <summary>
        /// Copies draw calls while switching between indexed and non-indexed layout.
        /// </summary>
        /// <param name="src">The source address of the copy.</param>
        /// <param name="dst">The destination address of the copy.</param>
        /// <param name="stride">The number of bytes between consecutive draw calls.</param>
        /// <param name="count">The number of draw calls to copy.</param>
        static member inline ToggleIndexedCopy(src : nativeint, dst : nativeint, stride : nativeint, count : int) =
            let mutable src = src
            let mutable dst = dst

            for i = 1 to count do
                let pSrc = NativePtr.ofNativeInt<DrawCallInfo> src
                let pDst = NativePtr.ofNativeInt<DrawCallInfo> dst

                let mutable c = NativePtr.read pSrc
                DrawCallInfo.ToggleIndexed(&c)
                NativePtr.write pDst c

                &src += stride
                &dst += stride
    end

/// Type for describing buffers holding draw calls.
type IndirectBuffer =
    {
        /// The buffer holding the draw calls.
        Buffer  : IBuffer

        /// The number of draw calls in the buffer.
        Count   : int

        /// The offset in bytes into the buffer.
        Offset  : uint64

        /// The number of bytes between the beginning of two successive draw calls.
        Stride  : int

        /// True if the buffer contains indexed draw calls, false if it contains non-indexed draw calls.
        /// If an indexed indirect buffer is used to render non-indexed geometry (or vice versa), the layout of the draw calls is adjusted automatically.
        /// This automatic layout adjustment does not work for backend buffers.
        Indexed : bool
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module IndirectBuffer =

    /// <summary>
    /// Creates an indirect buffer from a buffer containing draw calls.
    /// If the buffer is a backend buffer, the data must be in the correct layout (i.e. indexed or non-indexed).
    /// </summary>
    /// <remarks>
    /// The <see cref="DrawCallInfo"/> struct follows the non-indexed layout by default.
    /// </remarks>
    /// <param name="indexed">True if the buffer contains indexed draw calls, false if it contains non-indexed draw calls.</param>
    /// <param name="offset">The offset in bytes into the buffer.</param>
    /// <param name="stride">The number of bytes between the beginning of two successive draw calls.</param>
    /// <param name="count">The number of draw calls.</param>
    /// <param name="buffer">The buffer containing the draw calls.</param>
    let inline ofBuffer (indexed : bool) (offset : uint64) (stride : int) (count : int) (buffer : IBuffer) =
        { Buffer  = buffer
          Count   = count
          Offset  = offset
          Stride  = stride
          Indexed = indexed }

    /// <summary>
    /// Creates an indirect buffer from an array of draw calls.
    /// </summary>
    /// <remarks>
    /// The <see cref="DrawCallInfo"/> struct follows the non-indexed layout by default.
    /// </remarks>
    /// <param name="indexed">True if the array contains indexed draw calls, false if it contains non-indexed draw calls.</param>
    /// <param name="first">The index of the first draw call in the array.</param>
    /// <param name="count">The number of draw calls.</param>
    /// <param name="calls">The array containing the draw calls.</param>
    let inline ofArray' (indexed : bool) (first : int) (count : int) (calls : DrawCallInfo[]) =
        if first < 0 || count < 0 || first + count > calls.Length then
            raise <| ArgumentOutOfRangeException(null, $"Draw call range exceeds input array (first = {first}, count = {count}, array length = {calls.Length})")

        let buffer = ArrayBuffer calls
        let offset = uint64 first * uint64 sizeof<DrawCallInfo>
        ofBuffer indexed offset sizeof<DrawCallInfo> count buffer

    /// <summary>
    /// Creates an indirect buffer from an array of draw calls.
    /// The draw calls are assumed to be in the non-indexed layout (default <see cref="DrawCallInfo"/> struct layout).
    /// </summary>
    /// <param name="calls">The array containing the draw calls.</param>
    let inline ofArray (calls : DrawCallInfo[]) =
        calls |> ofArray' false 0 calls.Length

    /// <summary>
    /// Creates an indirect buffer from a list of draw calls.
    /// </summary>
    /// <remarks>
    /// The <see cref="DrawCallInfo"/> struct follows the non-indexed layout by default.
    /// </remarks>
    /// <param name="indexed">True if the list contains indexed draw calls, false if it contains non-indexed draw calls.</param>
    /// <param name="first">The index of the first draw call in the list.</param>
    /// <param name="count">The number of draw calls.</param>
    /// <param name="calls">The list containing the draw calls.</param>
    let inline ofList' (indexed : bool) (first : int) (count : int) (calls : DrawCallInfo list) =
        calls |> List.toArray |> ofArray' indexed first count

    /// <summary>
    /// Creates an indirect buffer from a list of draw calls.
    /// The draw calls are assumed to be in the non-indexed layout (default <see cref="DrawCallInfo"/> struct layout).
    /// </summary>
    /// <param name="calls">The list containing the draw calls.</param>
    let inline ofList (calls : DrawCallInfo list) =
        calls |> List.toArray |> ofArray

    /// <summary>
    /// Creates an indirect buffer from a sequence of draw calls.
    /// </summary>
    /// <remarks>
    /// The <see cref="DrawCallInfo"/> struct follows the non-indexed layout by default.
    /// </remarks>
    /// <param name="indexed">True if the list contains indexed draw calls, false if it contains non-indexed draw calls.</param>
    /// <param name="first">The index of the first draw call in the sequence.</param>
    /// <param name="count">The number of draw calls.</param>
    /// <param name="calls">The sequence containing the draw calls.</param>
    let inline ofSeq' (indexed : bool) (first : int) (count : int) (calls : DrawCallInfo seq) =
        calls |> Seq.asArray |> ofArray' indexed first count

    /// <summary>
    /// Creates an indirect buffer from a sequence of draw calls.
    /// The draw calls are assumed to be in the non-indexed layout (default <see cref="DrawCallInfo"/> struct layout).
    /// </summary>
    /// <param name="calls">The sequence containing the draw calls.</param>
    let inline ofSeq (calls : DrawCallInfo seq) =
        calls |> Seq.asArray |> ofArray