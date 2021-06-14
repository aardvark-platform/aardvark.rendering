namespace Aardvark.Rendering.Vulkan.Raytracing

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Raytracing
open Aardvark.Rendering.Vulkan

open FSharp.Data.Adaptive

open System.Runtime.CompilerServices


type SceneManager(manager : ResourceManager, instances : amap<Instance, int>) =
    inherit AdaptiveResource<AccelerationStructure>()

    
    //    let geometries = Dict<aval<Geometry[]>, IResourceLocation<AccelerationStructure>>()

    //    let reader = input.Indices.GetReader()

    //    let set (index : int) (inst : Instance) =
    //        if not <| geometries.Contains(inst.Geometry) then
    //            let accel = createBottomLevel inst.Geometry
    //            accel.Acquire()
    //            geometries.Add(inst.Geometry, accel)

    //    let remove (inst : Instance) =
    //        ()

    //    override x.Create() =
    //        ()

    //    override x.Destroy() =
    //        ()

    //    override x.GetData(token : AdaptiveToken) =
    //        let deltas = reader.GetChanges(token)

    //        for op in deltas do
    //            match op with
    //            | inst, Set i -> set i inst
    //            | inst, Remove -> remove inst

    override x.Create() =
        ()

    override x.Destroy() =
        ()

    override x.Compute(t, rt) =
        failwith ""