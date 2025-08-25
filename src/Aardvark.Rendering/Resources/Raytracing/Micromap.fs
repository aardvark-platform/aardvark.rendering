namespace Aardvark.Rendering.Raytracing

open System
open System.Runtime.InteropServices
open Aardvark.Base

type MicromapFormat =
    /// Encode the opacity of a triangle with a single bit (transparent or opaque).
    | OpacityTwoState  = 1us

    /// Encode the opacity of a triangle with two bits (transparent, opaque, and two unknown states that are to be resolved in an anyhit shader).
    | OpacityFourState = 2us

/// Specifies the usage information used to build a micromap.
[<StructLayout(LayoutKind.Sequential, Size = 12)>]
type MicromapUsage =
    struct
        /// The number of triangles in the micromap with the given subdivision level and format.
        val mutable Count  : uint32

        /// The subdivision level of the usage.
        val mutable Level  : uint32

        /// The micromap data format of the usage.
        val mutable Format : MicromapFormat

        new (count, level, format) = { Count = count; Level = level; Format = format }
    end

/// Specifies the micromap format and data for a triangle.
[<StructLayout(LayoutKind.Sequential, Size = 8)>]
type MicromapTriangle =
    struct
        /// Offset in bytes of the start of the data for the triangle.
        val mutable Offset : uint32

        /// The subdivision level of the triangle.
        val mutable Level  : uint16

        /// The micromap data format of the triangle.
        val mutable Format : MicromapFormat

        new (offset, level, format) = { Offset = offset; Level = level; Format = format }
    end

/// Interface micromap build input data.
type IMicromapData =
    abstract member Data             : Array
    abstract member Indices          : Array
    abstract member UsageCounts      : MicromapUsage[]
    abstract member IndexUsageCounts : MicromapUsage[]
    abstract member Triangles        : MicromapTriangle[]

/// Base interface for micromaps.
/// Micromaps contain data for a mesh at a subtriangle level (e.g. opacity data).
[<AllowNullLiteral>]
type IMicromap =
    interface end

/// Interface for prepared micromaps.
type IBackendMicromap =
    inherit IMicromap
    inherit IDisposable
    abstract member SizeInBytes : uint64

/// Host-side representation for micromaps.
type Micromap =

    /// The data to build the micromap from.
    val Data : IMicromapData

    /// Indicates whether the micromap is compressed after building to reduce its memory footprint.
    val Compress : bool

    /// <summary>
    /// Creates a new micromap.
    /// </summary>
    /// <param name="data">The data to build the micromap from.</param>
    /// <param name="compress">If true, the micromap is compacted after building to reduce its memory footprint. Default is false.</param>
    new (data: IMicromapData, [<Optional; DefaultParameterValue(false)>] compress: bool) =
        { Data = data; Compress = compress }

    member inline private this.Equals(other: Micromap) =
        this.Data = other.Data && this.Compress = other.Compress

    override this.Equals(obj: obj) =
        match obj with
        | :? Micromap as other -> this.Equals(other)
        | _ -> false

    override this.GetHashCode() =
        HashCode.Combine(this.Data.GetHashCode(), this.Compress.GetHashCode())

    interface IEquatable<Micromap> with
        member this.Equals(other) = this.Equals(other)

    interface IMicromap