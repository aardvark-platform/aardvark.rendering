namespace Aardvark.Rendering.Raytracing

open System
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

[<Flags>]
type AccelerationStructureUsage =
    | None = 0

    /// Prioritize tracing performance over build times.
    | Static = 1

    /// Prioritize short build times over tracing performance.
    | Dynamic = 2

    /// Allow the acceleration structure to be updated rather than having to rebuild it from scratch.
    /// May incur a trace performance and memory overhead.
    | Update = 4

    /// Compact the acceleration structure after building to reduce its memory footprint.
    | Compact = 8

type IAccelerationStructure =
    inherit IDisposable

    abstract member Usage : AccelerationStructureUsage
    abstract member GeometryCount : int
    abstract member SizeInBytes : uint64
    abstract member Name : string with get, set

type IAccelerationStructureRuntime =

    ///<summary>
    /// Creates an acceleration structure from the given trace geometry.
    ///</summary>
    ///<param name="geometry">The geometry to build the acceleration structure from.</param>
    ///<param name="usage">The usage flags of the acceleration structure. Default is Static.</param>
    abstract member CreateAccelerationStructure : geometry: TraceGeometry *
                                                  [<Optional; DefaultParameterValue(AccelerationStructureUsage.Static)>] usage: AccelerationStructureUsage -> IAccelerationStructure

    ///<summary>
    /// Tries to update an acceleration structure with the given trace geometry.
    ///</summary>
    ///<param name="handle">The acceleration structure to update.</param>
    ///<param name="geometry">The geometry to update the acceleration structure from.</param>
    ///<returns>True if the acceleration structure could be updated, false otherwise.</returns>
    abstract member TryUpdateAccelerationStructure : handle: IAccelerationStructure * geometry: TraceGeometry -> bool

    /// <summary>
    /// Prepares the given micromap.
    /// The prepared micromap can be safely disposed after using it to build an accleration structure.
    /// </summary>
    /// <param name="micromap">The micromap to prepare.</param>
    abstract member PrepareMicromap : micromap: IMicromap -> IBackendMicromap