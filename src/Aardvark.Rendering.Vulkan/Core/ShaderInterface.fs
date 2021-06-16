namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open FShade.SpirV
open Aardvark.Base.Monads.Option



type ShaderUniformParameter =
    | UniformBlockParameter of FShade.GLSL.GLSLUniformBuffer
    | StorageBufferParameter of FShade.GLSL.GLSLStorageBuffer
    | ImageParameter of FShade.GLSL.GLSLImage
    | SamplerParameter of FShade.GLSL.GLSLSampler
    | AccelerationStructureParameter of FShade.GLSL.GLSLAccelerationStructure

    member x.Name =
        match x with
            | UniformBlockParameter b -> b.ubName
            | StorageBufferParameter b -> b.ssbName
            | ImageParameter i -> i.imageName
            | SamplerParameter i -> i.samplerName
            | AccelerationStructureParameter a -> a.accelName
            
    member x.DescriptorSet =
        match x with
            | UniformBlockParameter b -> b.ubSet
            | StorageBufferParameter b -> b.ssbSet
            | ImageParameter i -> i.imageSet
            | SamplerParameter b -> b.samplerSet
            | AccelerationStructureParameter a -> a.accelSet
            
    member x.Binding =
        match x with
            | UniformBlockParameter b -> b.ubBinding
            | StorageBufferParameter b -> b.ssbBinding
            | ImageParameter i -> i.imageBinding
            | SamplerParameter b -> b.samplerBinding
            | AccelerationStructureParameter a -> a.accelBinding

[<System.Flags>]
type TessellationFlags =
    | None                  = 0x0000
    /// Requests the tessellation primitive generator to divide edges into a collection of equal-sized segments.
    | SpacingEqual          = 0x0001
    /// Requests the tessellation primitive generator to divide edges into an even number of equal-length segments plus two additional shorter fractional segments.
    | SpacingFractionalEven = 0x0002
    /// Requests the tessellation primitive generator to divide edges into an odd number of equal-length segments plus two additional shorter fractional segments.
    | SpacingFractionalOdd  = 0x0004
    /// Requests the tessellation primitive generator to generate triangles in clockwise order.
    | VertexOrderCw         = 0x0008
    /// Requests the tessellation primitive generator to generate triangles in counter-clockwise order.
    | VertexOrderCcw        = 0x0010
    /// Requests the tessellation primitive generator to generate a point for each distinct vertex in the subdivided primitive, rather than to generate lines or triangles
    | OutputPoints          = 0x0020
    /// Requests the tessellation primitive generator to generate triangles.
    | OutputTriangles       = 0x0040
    /// Requests the tessellation primitive generator to generate quads.
    | OutputQuads           = 0x0080
    /// Requests the tessellation primitive generator to generate isolines.
    | OutputIsolines        = 0x0100

[<System.Flags>]
type GeometryFlags =
    | None                      = 0x0000
    /// Stage input primitive is points. 
    | InputPoints               = 0x0001
    /// Stage input primitive is lines. 
    | InputLines                = 0x0002
    /// Stage input primitive is lines adjacency.
    | InputLinesAdjacency       = 0x0004
    /// Stage input primitive is triangles.
    | InputTriangles            = 0x0008
    /// Geometry stage input primitive is triangles adjacency.
    | InputTrianglesAdjacency   = 0x0010
    /// Stage output primitive is points. 
    | OutputPoints              = 0x0020
    /// Stage output primitive is line strip.
    | OutputLineStrip           = 0x0040
    /// Stage output primitive is triangle strip.
    | OutputTriangleStrip       = 0x0080

[<System.Flags>]
type FragmentFlags =
    | None                      = 0x0000
    /// Pixels appear centered on whole-number pixel offsets. E.g., the coordinate (0.5, 0.5) appears to move to (0.0, 0.0).
    | PixelCenterInteger        = 0x0001
    /// Pixel coordinates appear to originate in the upper left, and increase toward the right and downward.
    | OriginUpperLeft           = 0x0002
    /// Pixel coordinates appear to originate in the lower left, and increase toward the right and upward.
    | OriginLowerLeft           = 0x0004
    /// Fragment tests are to be performed before fragment shader execution.
    | EarlyFragmentTests        = 0x0008
    /// This mode must be declared if this module potentially changes the fragment’s depth.
    | DepthReplacing            = 0x0010
    /// External optimizations may assume depth modifications will leave the fragment’s depth as greater than or equal to the fragment’s interpolated depth value (given by the z component of the FragCoord BuiltIn decorated variable).
    | DepthGreater              = 0x0020
    /// External optimizations may assume depth modifications leave the fragment’s depth less than the fragment’s interpolated depth value, (given by the z component of the FragCoord BuiltIn decorated variable).
    | DepthLess                 = 0x0040
    /// External optimizations may assume this stage did not modify the fragment’s depth. However, DepthReplacing mode must accurately represent depth modification.
    | DepthUnchanged            = 0x0080

type TessellationInfo =
    {
        /// Tessellation flags specifying execution aspects of the  shader
        flags           : TessellationFlags
        /// The number of vertices in the output patch produced by the tessellation control shader, which also specifies the number of times the tessellation control shader is invoked.
        inputPatchSize  : int
    }

type GeometryInfo =
    {
        flags           : GeometryFlags
        outputVertices  : int
        invocations     : int
    }

type FragmentInfo =
    {
        flags           : FragmentFlags
        discard         : bool
        sampleShading   : bool
    }
