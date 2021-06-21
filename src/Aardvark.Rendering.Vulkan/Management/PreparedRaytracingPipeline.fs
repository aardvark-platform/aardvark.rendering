namespace Aardvark.Rendering.Vulkan.Raytracing

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open Aardvark.Rendering.Raytracing

open FSharp.Data.Adaptive

open System
open System.Runtime.CompilerServices

[<AutoOpen>]
module private PreparedRaytracingPipelineInternals =

    open System.Collections.Generic

    type HitConfigSceneReader(scene : amap<TraceObject, int>) =
        inherit AdaptiveObject()

        let reader = scene.GetReader()
        let objects = HashSet<TraceObject>()
        let mutable configs = Set.empty

        let set (o : TraceObject) =
            objects.Add(o) |> ignore

        let remove (o : TraceObject) =
            objects.Remove(o) |> ignore

        member x.Read(token : AdaptiveToken) =
            x.EvaluateIfNeeded token configs (fun token ->
                let deltas = reader.GetChanges(token)

                for op in deltas do
                    match op with
                    | o, Set _ -> set o
                    | o, Remove -> remove o

                for o in objects do
                    let cfg = o.HitGroups.GetValue(token)
                    configs <- configs |> Set.add cfg

                configs
            )

        member x.Dispose() =
            x.Outputs.Clear()

        interface IDisposable with
            member x.Dispose() = x.Dispose()


    type HitConfigPool(scenes : Map<Symbol, amap<TraceObject, int>>) =
        inherit AdaptiveObject()

        let mutable cache = Set.empty
        let readers = scenes |> Map.toList |> List.map (fun (_, map) -> new HitConfigSceneReader(map))

        member x.GetValue(token : AdaptiveToken) =
            x.EvaluateIfNeeded token cache (fun t ->
                cache <- readers |> List.map (fun r -> r.Read(t)) |> Set.unionMany
                cache
            )

        member x.Dispose() =
            readers |> List.iter (fun r -> r.Dispose())

        interface IDisposable with
            member x.Dispose() = x.Dispose()

        interface IAdaptiveValue with
            member x.IsConstant = false
            member x.ContentType = typeof<Set<HitConfig>>
            member x.GetValueUntyped(c) = x.GetValue(c) :> obj
            member x.Accept (v : IAdaptiveValueVisitor<'R>) = v.Visit x

        interface IAdaptiveValue<Set<HitConfig>> with
            member x.GetValue(c) = x.GetValue(c)


type PreparedRaytracingPipeline(device         : Device,
                                state          : RaytracingPipelineState,
                                resources      : list<IResourceLocation>,
                                program        : RaytracingProgram,
                                pipeline       : IResourceLocation<RaytracingPipeline>,
                                descriptorSets : IResourceLocation<DescriptorSetBinding>,
                                sbt            : IResourceLocation<ShaderBindingTable>,
                                hitConfigPool  : IDisposable) =

    inherit Resource(device)

    member x.State = state
    member x.Resources = resources
    member x.Program = program
    member x.Pipeline = pipeline
    member x.DescriptorSets = descriptorSets
    member x.ShaderBindingTable = sbt

    override x.Destroy() =
        program.Dispose()
        hitConfigPool.Dispose()

    member x.Update(token : AdaptiveToken) =
        for r in resources do r.Update(token) |> ignore

            
[<AbstractClass; Sealed; Extension>]
type DevicePreparedRaytracingPipelineExtensions private() =
            
    [<Extension>]
    static member PrepareRaytracingPipeline(this : ResourceManager, state : RaytracingPipelineState) =

        let resources = System.Collections.Generic.List<IResourceLocation>()

        let program = RaytracingProgram.ofEffect this.Device state.Effect
        let pipeline = this.CreateRaytracingPipeline(program, state.MaxRecursionDepth)

        let hitConfigPool = new HitConfigPool(state.Scenes)
        let shaderBindingTable = this.CreateShaderBindingTable(pipeline, hitConfigPool)

        let accelerationStructures =
            state.Scenes |> Map.map (fun _ objects ->
                this.CreateAccelerationStructure(objects, shaderBindingTable, AccelerationStructureUsage.Static) :> IAdaptiveValue
            )

        resources.AddRange(accelerationStructures |> Map.toList |> List.map (snd >> unbox))

        let uniforms =
            UniformProvider.ofMap accelerationStructures
            |> UniformProvider.union state.Uniforms

        let descriptorSetBinding =
            let sets, descriptorResources = this.CreateDescriptorSets(program.Layout, uniforms)
            resources.AddRange(descriptorResources)

            this.CreateDescriptorSetBinding(program.Layout, sets)

        resources.Add(descriptorSetBinding)

        new PreparedRaytracingPipeline(
            this.Device, state,
            CSharpList.toList resources,
            program, pipeline, 
            descriptorSetBinding,
            shaderBindingTable,
            hitConfigPool
        ) 