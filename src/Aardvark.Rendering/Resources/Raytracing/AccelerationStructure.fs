namespace Aardvark.Rendering.Raytracing

open System
open System.Runtime.InteropServices

type AccelerationStructureUsage =
    /// Favor fast tracing over fast building.
    | Static = 0

    /// Favor fast building over fast tracing.
    | Dynamic = 1

type IAccelerationStructure =
    inherit IDisposable

    abstract member Usage : AccelerationStructureUsage
    abstract member GeometryCount : int
    abstract member Name : string with get, set

type IAccelerationStructureRuntime =

    ///<summary>
    /// Creates an acceleration structure from the given trace geometry.
    ///</summary>
    ///<param name="geometry">The geometry to build the acceleration structure from.</param>
    ///<param name="usage">The usage flag of the acceleration structure. Default is AccelerationStructureUsage.Static.</param>
    ///<param name="allowUpdate">Determines if the acceleration structure may be updated, instead of requiring a rebuild. Default is true.</param>
    abstract member CreateAccelerationStructure : geometry: TraceGeometry *
                                                  [<Optional; DefaultParameterValue(AccelerationStructureUsage.Static)>] usage: AccelerationStructureUsage *
                                                  [<Optional; DefaultParameterValue(true)>] allowUpdate: bool -> IAccelerationStructure

    ///<summary>
    /// Tries to update an acceleration structure with the given trace geometry.
    ///</summary>
    ///<param name="handle">The acceleration structure to update.</param>
    ///<param name="geometry">The geometry to update the acceleration structure from.</param>
    ///<returns>True if the acceleration structure could be updated, false otherwise.</returns>
    abstract member TryUpdateAccelerationStructure : handle: IAccelerationStructure * geometry: TraceGeometry -> bool