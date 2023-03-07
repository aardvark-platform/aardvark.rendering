namespace Aardvark.Rendering.GL

open System
open System.Runtime.InteropServices
open Aardvark.Base
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.GL

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
type AttributeBufferDescription =
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
        Buffer : Buffer
    }

[<RequireQualifiedAccess>]
type AttributeDescription =
    | Value  of value: obj
    | Buffer of description: AttributeBufferDescription

[<AutoOpen>]
module AttributeDescriptionExtensions =
    let private GL_BGRA = All.Bgra |> int

    let private dimensions =
        Dict.ofList [
            typeof<bool>,       1
            typeof<sbyte>,      1
            typeof<byte>,       1
            typeof<int16>,      1
            typeof<uint16>,     1
            typeof<int>,        1
            typeof<uint32>,     1
            typeof<int64>,      1
            typeof<uint64>,     1

            typeof<float32>,    1
            typeof<float>,      1

            typeof<V2i>,        2
            typeof<V2f>,        2
            typeof<V2d>,        2
            typeof<V3i>,        3
            typeof<V3f>,        3
            typeof<V3d>,        3
            typeof<V4i>,        4
            typeof<V4f>,        4
            typeof<V4d>,        4

            typeof<C3f>,        3
            typeof<C4f>,        4
            typeof<C4b>,        GL_BGRA
        ]

    let private baseTypes =
        Dict.ofList [
            typeof<bool>,       typeof<bool>
            typeof<sbyte>,      typeof<sbyte>
            typeof<byte>,       typeof<byte>
            typeof<int16>,      typeof<int16>
            typeof<uint16>,     typeof<uint16>
            typeof<int>,        typeof<int>
            typeof<uint32>,     typeof<uint32>
            typeof<int64>,      typeof<int64>
            typeof<uint64>,     typeof<uint64>

            typeof<float32>,    typeof<float32>
            typeof<float>,      typeof<float>

            typeof<V2i>,        typeof<int>
            typeof<V2f>,        typeof<float32>
            typeof<V2d>,        typeof<float>
            typeof<V3i>,        typeof<int>
            typeof<V3f>,        typeof<float32>
            typeof<V3d>,        typeof<float>
            typeof<V4i>,        typeof<int>
            typeof<V4f>,        typeof<float32>
            typeof<V4d>,        typeof<float>

            typeof<C3f>,        typeof<float32>
            typeof<C4b>,        typeof<byte>
            typeof<C4f>,        typeof<float32>

            typeof<M44f>,       typeof<float32>
        ]

    let internal glTypes =
        Dict.ofList [
            typeof<byte>, VertexAttribPointerType.UnsignedByte
            typeof<sbyte>, VertexAttribPointerType.Byte
            typeof<uint16>, VertexAttribPointerType.UnsignedShort
            typeof<int16>, VertexAttribPointerType.Short
            typeof<uint32>, VertexAttribPointerType.UnsignedInt
            typeof<int>, VertexAttribPointerType.Int

            typeof<float32>, VertexAttribPointerType.Float
            typeof<float>, VertexAttribPointerType.Double

            typeof<M44f>, VertexAttribPointerType.Float
        ]

    // some useful extensions for AttributeBufferDescription
    type AttributeBufferDescription with
        member x.ElementSize =
            Marshal.SizeOf(x.Type)

        member x.Dimension =
            match dimensions.TryGetValue x.Type with
            | (true, v) -> v
            | _ -> failf "could not find dimensions for: %A, valid are: %A" x.Type (dimensions |> Seq.toList)

        member x.BaseType =
            match baseTypes.TryGetValue x.Type with
            | (true, v) -> v
            | _ -> failf "could not find baseTypes for: %A, valid are: %A" x.Type (baseTypes |> Seq.toList)

        member x.VertexAttributeType =
            match glTypes.TryGetValue x.BaseType with
            | (true, v) -> v
            |  _ -> failf "could not find glTypes for: %A, valid are: %A" x.BaseType (glTypes |> Seq.toList)