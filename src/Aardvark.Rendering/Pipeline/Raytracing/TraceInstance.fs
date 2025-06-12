namespace Aardvark.Rendering.Raytracing

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open System

[<Struct>]
type VisibilityMask =
    val Value : uint8

    new(value : uint8)  = { Value = value }
    new(value : int8)   = VisibilityMask(uint8 value)
    new(value : uint32) = VisibilityMask(uint8 value)
    new(value : int32)  = VisibilityMask(uint8 value)
    new(enabled : bool) = VisibilityMask(if enabled then Byte.MaxValue else 0uy)

    static member op_Explicit (m : VisibilityMask) : uint8 = m.Value

    static member op_Explicit (m : VisibilityMask) : uint32 = uint32 m.Value

    static member All  = VisibilityMask(true)
    static member None = VisibilityMask(false)

type GeometryMode =
    /// Do not override individual geometry flags.
    | Default = 0

    /// Treat all geometries as if GeometryFlags.Opaque was specified.
    | Opaque = 1

    /// Treat all geometries as if GeometryFlags.Opaque was not specified.
    | Transparent = 2


/// Interface for instances in a raytracing scene.
type ITraceInstance =

    /// The geometries of the instance.
    abstract member Geometry     : aval<IAccelerationStructure>

    /// The hit groups for each geometry of the instance.
    abstract member HitGroups    : aval<Symbol[]>

    /// The transformation of the instance.
    abstract member Transform    : aval<Trafo3d>

    /// The winding order of triangles considered to be front-facing, or None if back-face culling is to be disabled for the instance.
    /// Only has an effect if TraceRay() is called with one of the cull flags.
    abstract member FrontFace    : aval<WindingOrder voption>

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
        HitGroups    : aval<Symbol[]>

        /// The transformation of the instance.
        Transform    : aval<Trafo3d>

        /// The winding order of triangles considered to be front-facing, or None if back-face culling is to be disabled for the instance.
        /// Only has an effect if TraceRay() is called with one of the cull flags.
        FrontFace    : aval<WindingOrder voption>

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
        member x.FrontFace    = x.FrontFace
        member x.GeometryMode = x.GeometryMode
        member x.Mask         = x.Mask
        member x.CustomIndex  = x.CustomIndex

[<AutoOpen>]
module TraceInstanceFSharp =
    open FSharp.Data.Adaptive.Operators

    type TraceInstance with

        /// Creates an empty trace instance from the given acceleration structure.
        static member inline ofAccelerationStructure (geometry : aval<IAccelerationStructure>) =
            { Geometry     = geometry
              HitGroups    = AVal.constant Array.empty
              Transform    = AVal.constant Trafo3d.Identity
              FrontFace    = AVal.constant ValueNone
              GeometryMode = AVal.constant GeometryMode.Default
              Mask         = AVal.constant VisibilityMask.All
              CustomIndex  = AVal.constant 0u }

        /// Creates an empty trace instance from the given acceleration structure.
        static member inline ofAccelerationStructure (geometry : IAccelerationStructure) =
            TraceInstance.ofAccelerationStructure ~~geometry

        /// Sets hit groups for the given trace instance.
        static member inline hitGroups (hitConfig : aval<Symbol[]>) =
            fun (inst : TraceInstance) -> { inst with HitGroups = hitConfig }

        /// Sets hit groups for the given trace instance.
        static member inline hitGroups (hitConfig : Symbol[]) =
            TraceInstance.hitGroups ~~hitConfig

        /// Sets a hit group for the given trace instance with a single geometry.
        static member inline hitGroup (group : aval<Symbol>) =
            let groups = group |> AVal.map Array.singleton
            TraceInstance.hitGroups groups

        /// Sets a hit group for the given trace instance with a single geometry.
        static member inline hitGroup (group : Symbol) =
            TraceInstance.hitGroups [|group|]

        /// Sets hit groups for the given trace instance.
        static member inline hitGroups (hitConfig : aval<string[]>) =
            fun (inst : TraceInstance) -> { inst with HitGroups = hitConfig |> AVal.map (Array.map Sym.ofString) }

        /// Sets hit groups for the given trace instance.
        static member inline hitGroups (hitConfig : string[]) =
            TraceInstance.hitGroups ~~hitConfig

        /// Sets a hit group for the given trace instance with a single geometry.
        static member inline hitGroup (group : aval<string>) =
            let groups = group |> AVal.map Array.singleton
            TraceInstance.hitGroups groups

        /// Sets a hit group for the given trace instance with a single geometry.
        static member inline hitGroup (group : string) =
            TraceInstance.hitGroups [|group|]

        /// Sets the transform for the given trace instance.
        static member inline transform (trafo : aval<Trafo3d>) =
            fun (inst : TraceInstance) -> { inst with Transform = trafo }

        /// Sets the transform for the given trace instance.
        static member inline transform (trafo : Trafo3d) =
            TraceInstance.transform ~~trafo

        /// Sets the winding order of triangles considered to be front-facing, or None if back-face culling is to be disabled for the given instance.
        /// Only has an effect if TraceRay() is called with one of the cull flags.
        static member inline frontFace (front : aval<WindingOrder voption>) =
            fun (inst : TraceInstance) -> { inst with FrontFace = front }

        /// Sets the winding order of triangles considered to be front-facing, or None if back-face culling is to be disabled for the given instance.
        /// Only has an effect if TraceRay() is called with one of the cull flags.
        static member inline frontFace (front : aval<WindingOrder option>) =
            TraceInstance.frontFace (front |> AVal.mapNonAdaptive Option.toValueOption)

        /// Sets the winding order of triangles considered to be front-facing for the given instance.
        /// Only has an effect if TraceRay() is called with one of the cull flags.
        static member inline frontFace (front : aval<WindingOrder>) =
            TraceInstance.frontFace (front |> AVal.mapNonAdaptive ValueSome)

        /// Sets the winding order of triangles considered to be front-facing, or None if back-face culling is to be disabled for the given instance.
        /// Only has an effect if TraceRay() is called with one of the cull flags.
        static member inline frontFace (front : WindingOrder voption) =
            TraceInstance.frontFace ~~front

        /// Sets the winding order of triangles considered to be front-facing, or None if back-face culling is to be disabled for the given instance.
        /// Only has an effect if TraceRay() is called with one of the cull flags.
        static member inline frontFace (front : WindingOrder option) =
            TraceInstance.frontFace ~~front

        /// Sets the winding order of triangles considered to be front-facing for the given instance.
        /// Only has an effect if TraceRay() is called with one of the cull flags.
        static member inline frontFace (front : WindingOrder) =
            TraceInstance.frontFace (ValueSome front)

        /// Sets the geometry mode for the given trace instance.
        static member inline geometryMode (mode : aval<GeometryMode>) =
            fun (inst : TraceInstance) -> { inst with GeometryMode = mode }

        /// Sets the geometry mode for the given trace instance.
        static member inline geometryMode (mode : GeometryMode) =
            TraceInstance.geometryMode ~~mode

        /// Sets the visibility mask for the given trace instance.
        static member inline mask (value : aval<VisibilityMask>) =
            fun (inst : TraceInstance) -> { inst with Mask = value }

        /// Sets the visibility mask for the given trace instance.
        static member inline mask (value : aval<uint8>) =
            TraceInstance.mask (value |> AVal.mapNonAdaptive VisibilityMask)

        /// Sets the visibility mask for the given trace instance.
        static member inline mask (value : aval<int8>) =
            TraceInstance.mask (value |> AVal.mapNonAdaptive VisibilityMask)

        /// Sets the visibility mask for the given trace instance.
        static member inline mask (value : aval<uint32>) =
            TraceInstance.mask (value |> AVal.mapNonAdaptive VisibilityMask)

        /// Sets the visibility mask for the given trace instance.
        static member inline mask (value : aval<int32>) =
            TraceInstance.mask (value |> AVal.mapNonAdaptive VisibilityMask)

        /// Sets the visibility mask for the given trace instance.
        static member inline mask (value : VisibilityMask) =
            TraceInstance.mask ~~value

        /// Sets the visibility mask for the given trace instance.
        static member inline mask (value : uint8) =
            TraceInstance.mask (VisibilityMask value)

        /// Sets the visibility mask for the given trace instance.
        static member inline mask (value : int8) =
            TraceInstance.mask (VisibilityMask value)

        /// Sets the visibility mask for the given trace instance.
        static member inline mask (value : uint32) =
            TraceInstance.mask (VisibilityMask value)

        /// Sets the visibility mask for the given trace instance.
        static member inline mask (value : int32) =
            TraceInstance.mask (VisibilityMask value)

        /// Sets the custom index for the given trace instance.
        static member inline customIndex (index : aval<uint32>) =
            fun (inst : TraceInstance) -> { inst with CustomIndex = index }

        /// Sets the custom index for the given trace instance.
        static member inline customIndex (index : aval<int32>) =
            TraceInstance.customIndex (index |> AVal.mapNonAdaptive uint32)

        /// Sets the custom index for the given trace instance.
        static member inline customIndex (index : uint32) =
            TraceInstance.customIndex ~~index

        /// Sets the custom index for the given trace instance.
        static member inline customIndex (index : int32) =
            TraceInstance.customIndex (uint32 index)