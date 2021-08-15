namespace Aardvark.Rendering.Raytracing

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open System

[<Struct>]
type VisibilityMask =
    val mutable Value : uint8

    new(value : uint8)  = { Value = value }
    new(value : int8)   = VisibilityMask(uint8 value)
    new(value : uint32) = VisibilityMask(uint8 value)
    new(value : int32)  = VisibilityMask(uint8 value)
    new(enabled : bool) = VisibilityMask(if enabled then Byte.MaxValue else 0uy)

    static member op_Explicit (m : VisibilityMask) : uint8 = m.Value

    static member All  = VisibilityMask(true)
    static member None = VisibilityMask(false)

[<RequireQualifiedAccess>]
type CullMode =
    /// No face culling.
    | Disabled

    /// Cull all back-facing triangles.
    | Enabled of front: WindingOrder

[<RequireQualifiedAccess>]
type GeometryMode =
    /// Do not override individual geometry flags.
    | Default

    /// Treat all geometries as if GeometryFlags.Opaque was specified.
    | Opaque

    /// Treat all geometries as if GeometryFlags.Opaque was not specified.
    | Transparent


type HitConfig = List<Symbol>

/// Interface for instances in a raytracing scene.
type ITraceInstance =

    /// The geometries of the instance.
    abstract member Geometry     : aval<IAccelerationStructure>

    /// The hit groups for each geometry of the instance.
    abstract member HitGroups    : aval<HitConfig>

    /// The transformation of the instance.
    abstract member Transform    : aval<Trafo3d>

    /// The cull mode of the instance. Only has an effect if TraceRay() is called with one of the cull flags.
    abstract member Culling      : aval<CullMode>

    /// Optionally overrides flags set in the geometry.
    abstract member GeometryMode : aval<GeometryMode>

    /// Visibility mask that is compared against the mask specified by TraceRay().
    abstract member Mask         : aval<VisibilityMask>

    /// Custom index provided in shaders.
    abstract member CustomIndex  : aval<uint32>

// Instance in a raytracing scene.
[<CLIMutable>]
type TraceInstance =
    {
        /// The geometries of the instance.
        Geometry     : aval<IAccelerationStructure>

        /// The hit groups for each geometry of the instance.
        HitGroups    : aval<HitConfig>

        /// The transformation of the instance.
        Transform    : aval<Trafo3d>

        /// The cull mode of the instance. Only has an effect if TraceRay() is called with one of the cull flags.
        Culling      : aval<CullMode>

        /// Optionally overrides flags set in the geometry.
        GeometryMode : aval<GeometryMode>

        /// Visibility mask that is compared against the mask specified by TraceRay().
        Mask         : aval<VisibilityMask>

        /// Custom index provided in shaders.
        CustomIndex  : aval<uint32>
    }

    interface ITraceInstance with
        member x.Geometry     = x.Geometry
        member x.HitGroups    = x.HitGroups
        member x.Transform    = x.Transform
        member x.Culling      = x.Culling
        member x.GeometryMode = x.GeometryMode
        member x.Mask         = x.Mask
        member x.CustomIndex  = x.CustomIndex

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module TraceInstance =

    let create (geometry : aval<IAccelerationStructure>) =
        { Geometry     = geometry
          HitGroups    = AVal.constant []
          Transform    = AVal.constant Trafo3d.Identity
          Culling      = AVal.constant CullMode.Disabled
          GeometryMode = AVal.constant GeometryMode.Default
          Mask         = AVal.constant VisibilityMask.All
          CustomIndex  = AVal.constant 0u }

    let create' (geometry : IAccelerationStructure) =
        geometry |> AVal.constant |> create


    let hitGroups (hitConfig : aval<HitConfig>) (inst : TraceInstance) =
        { inst with HitGroups = hitConfig }

    let hitGroups' (hitConfig : HitConfig) (inst : TraceInstance) =
        inst |> hitGroups (AVal.constant hitConfig)

    let hitGroup (group : aval<Symbol>) (inst : TraceInstance) =
        let groups = group |> AVal.map List.singleton
        inst  |> hitGroups groups

    let hitGroup' (group : Symbol) (inst : TraceInstance) =
        inst |> hitGroups' [group]


    let transform (trafo : aval<Trafo3d>) (inst : TraceInstance) =
        { inst with Transform = trafo }

    let transform' (trafo : Trafo3d) (inst : TraceInstance) =
        inst |> transform (AVal.constant trafo)


    let culling (mode : aval<CullMode>) (inst : TraceInstance) =
        { inst with Culling = mode }

    let culling' (mode : CullMode) (inst : TraceInstance) =
        inst |> culling (AVal.constant mode)


    let geometryMode (mode : aval<GeometryMode>) (inst : TraceInstance) =
        { inst with GeometryMode = mode }

    let geometryMode' (mode : GeometryMode) (inst : TraceInstance) =
        inst |> geometryMode (AVal.constant mode)


    let mask (value : aval<VisibilityMask>) (inst : TraceInstance) =
        { inst with Mask = value }

    let mask' (value : VisibilityMask) (inst : TraceInstance) =
        inst |> mask (AVal.constant value)


    let customIndex (index : aval<uint32>) (inst : TraceInstance) =
        { inst with CustomIndex = index }

    let customIndex' (index : uint32) (inst : TraceInstance) =
        inst |> customIndex (AVal.constant index)


[<AutoOpen>]
module TraceInstanceBuilder =

    type GeometryMustBeSpecified = GeometryMustBeSpecified

    type TraceInstanceBuilder() =
        member x.Yield(_) = GeometryMustBeSpecified

        [<CustomOperation("geometry")>]
        member x.Geometry(_ : GeometryMustBeSpecified, accelerationStructure : aval<IAccelerationStructure>) =
            TraceInstance.create accelerationStructure

        member x.Geometry(_ : GeometryMustBeSpecified, accelerationStructure : IAccelerationStructure) =
            TraceInstance.create' accelerationStructure

        [<CustomOperation("hitGroups")>]
        member x.HitGroups(o : TraceInstance, g : aval<HitConfig>) =
            o |> TraceInstance.hitGroups g

        member x.HitGroups(o : TraceInstance, g : HitConfig) =
            o |> TraceInstance.hitGroups' g

        [<CustomOperation("hitGroup")>]
        member x.HitGroup(o : TraceInstance, g : aval<Symbol>) =
            o |> TraceInstance.hitGroup g

        member x.HitGroup(o : TraceInstance, g : Symbol) =
            o |> TraceInstance.hitGroup' g

        [<CustomOperation("transform")>]
        member x.Transform(o : TraceInstance, t : aval<Trafo3d>) =
            o |> TraceInstance.transform t

        member x.Transform(o : TraceInstance, t : Trafo3d) =
            o |> TraceInstance.transform' t

        [<CustomOperation("culling")>]
        member x.Culling(o : TraceInstance, m : aval<CullMode>) =
            o |> TraceInstance.culling m

        member x.Culling(o : TraceInstance, m : CullMode) =
            o |> TraceInstance.culling' m

        [<CustomOperation("geometryMode")>]
        member x.GeometryMode(o : TraceInstance, m : aval<GeometryMode>) =
            o |> TraceInstance.geometryMode m

        member x.GeometryMode(o : TraceInstance, m : GeometryMode) =
            o |> TraceInstance.geometryMode' m

        [<CustomOperation("mask")>]
        member x.Mask(o : TraceInstance, m : aval<VisibilityMask>) =
            o |> TraceInstance.mask m

        member x.Mask(o : TraceInstance, m : aval<int8>) =
            o |> TraceInstance.mask (m |> AVal.mapNonAdaptive VisibilityMask)

        member x.Mask(o : TraceInstance, m : aval<uint8>) =
            o |> TraceInstance.mask (m |> AVal.mapNonAdaptive VisibilityMask)

        member x.Mask(o : TraceInstance, m : aval<int32>) =
            o |> TraceInstance.mask (m |> AVal.mapNonAdaptive VisibilityMask)

        member x.Mask(o : TraceInstance, m : aval<uint32>) =
            o |> TraceInstance.mask (m |> AVal.mapNonAdaptive VisibilityMask)

        member x.Mask(o : TraceInstance, m : VisibilityMask) =
            o |> TraceInstance.mask' m

        member x.Mask(o : TraceInstance, m : int8) =
            o |> TraceInstance.mask' (VisibilityMask m)

        member x.Mask(o : TraceInstance, m : uint8) =
            o |> TraceInstance.mask' (VisibilityMask m)

        member x.Mask(o : TraceInstance, m : int32) =
            o |> TraceInstance.mask' (VisibilityMask m)

        member x.Mask(o : TraceInstance, m : uint32) =
            o |> TraceInstance.mask' (VisibilityMask m)

        [<CustomOperation("customIndex")>]
        member x.CustomIndex(o : TraceInstance, i : aval<uint32>) =
            o |> TraceInstance.customIndex i

        member x.CustomIndex(o : TraceInstance, i : uint32) =
            o |> TraceInstance.customIndex' i

        member x.CustomIndex(o : TraceInstance, i : aval<int32>) =
            o |> TraceInstance.customIndex (i |> AVal.mapNonAdaptive uint32)

        member x.CustomIndex(o : TraceInstance, i : int32) =
            o |> TraceInstance.customIndex' (uint32 i)

        member x.Run(h : TraceInstance) =
            h

    let traceInstance = TraceInstanceBuilder()