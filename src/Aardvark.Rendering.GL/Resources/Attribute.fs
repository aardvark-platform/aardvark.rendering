namespace Aardvark.Rendering.GL

open System
open Aardvark.Rendering

/// <summary>
/// describes the frequency of an attribute
/// which can either be PerVertex or PerInstance(n)
/// where n is the number of instances to render before
/// the attribute-index is advanced during rendering
/// </summary>
type AttributeFrequency =
    | PerVertex
    | PerInstances of int

/// <summary>
/// defines an attribute description similar to
/// DirectX/OpenGl allowing to bind buffers with
/// a specific format/offset/stride/etc.
/// </summary>
type AttributeBuffer =
    {
        /// <summary>
        /// specifies the data type contained in the buffer.
        /// common types include: V3f, C4b, float, etc.
        /// </summary>
        Type : Type

        /// <summary>
        /// specifies the attribute's step frequency.
        /// can either be PerVertex meaning that consecutive
        /// values "belong" to consecutive vertices or
        /// PerInstances(N) meaning that each N instances the
        /// next value will be used.
        /// </summary>
        Frequency : AttributeFrequency

        /// <summary>
        /// specifies how fixed-point values shall be treated
        /// by the driver implementation. For details see:
        /// https://www.opengl.org/sdk/docs/man/html/glVertexAttribPointer.xhtml
        /// </summary>
        Normalized : bool

        /// <summary>
        /// specifies a byte-offset between consecutive values
        /// in the buffer. This especially means that 0 stands
        /// for thightly packed elements.
        /// </summary>
        Stride : int

        /// <summary>
        /// specifies offset to be used when binding the buffer.
        /// </summary>
        Offset : int

        /// <summary>
        /// the attribute data buffer.
        /// </summary>
        Buffer : GL.Buffer
    }

[<RequireQualifiedAccess>]
type Attribute =
    | Value  of value: obj * normalized: bool
    | Buffer of buffer: AttributeBuffer