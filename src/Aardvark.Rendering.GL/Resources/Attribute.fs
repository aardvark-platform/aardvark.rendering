namespace Aardvark.Rendering.GL

open System
open System.Threading
open System.Collections.Concurrent
open System.Runtime.InteropServices
open Aardvark.Base
open OpenTK
open OpenTK.Platform
open OpenTK.Graphics
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
[<StructuralEquality; NoComparisonAttribute>]
type AttributeDescription = 
    { 
        /// <summary>
        /// specifies the data type contained in the buffer.
        /// common types include: V3f, C4b, float, etc.
        /// </summary>
        Type : Type; 

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
        Stride : int; 
        
        /// <summary>
        /// specifies offset to be used when binding the buffer.
        /// </summary>        
        Offset : int; 

        /// <summary>
        /// the buffer containing the attribute-data
        /// </summary>        
        Content : Either<Buffer, V4f>

    }

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
            typeof<C3f>,        3
            typeof<V3d>,        3
            typeof<V4i>,        4
            typeof<V4f>,        4
            typeof<C4f>,        4
            typeof<V4d>,        4
            typeof<M44f>,       16

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
            typeof<C3f>,        typeof<float32>
            typeof<V3d>,        typeof<float>
            typeof<V4i>,        typeof<int>
            typeof<V4f>,        typeof<float32>
            typeof<C4f>,        typeof<float32>
            typeof<V4d>,        typeof<float>

            typeof<C4b>,        typeof<byte>

            typeof<M44f>,       typeof<float>
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

    // some useful extensions for AttributeDescription
    type AttributeDescription with
        member x.ElementSize =
            Marshal.SizeOf(x.Type)

        member x.Dimension =
            dimensions.[x.Type]

        member x.BaseType =
            baseTypes.[x.Type]

        member x.VertexAttributeType =
            glTypes.[x.BaseType]

//
//[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
//module AttributeDescription =
//
//    let simple (t : Type) (buffer : Buffer) =
//        { Type = t; Frequency = PerVertex; Normalized = false; Stride = 0; Offset = 0; Buffer = buffer }
//
//    let normalized (t : Type) (buffer : Buffer) =
//        { Type = t; Frequency = PerVertex; Normalized = false; Stride = 0; Offset = 0; Buffer = buffer }
//


