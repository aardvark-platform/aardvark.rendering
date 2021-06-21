namespace Aardvark.Rendering.Vulkan.Raytracing

open System.Runtime.CompilerServices

#nowarn "9"

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan

type RaytracingStageInfo =
    { Index  : uint32
      Module : ShaderModule }

type RaytracingProgram(device : Device, effect : FShade.RaytracingEffect,
                       stages : ShaderGroup<RaytracingStageInfo> list,
                       pipelineLayout : PipelineLayout) =
    inherit CachedResource(device)

    member x.Effect = effect
    member x.Groups = stages
    member x.Layout = pipelineLayout

    override x.Destroy() =
        stages |> List.iter (
            ShaderGroup.iter (fun _ _ _ s -> s.Module.Dispose())
        )
        x.Layout.Dispose()

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RaytracingProgram =

    // TODO: Caching
    let ofEffect (device : Device) (effect : FShade.RaytracingEffect) =

        let toGeneralGroups (map : Map<Symbol, FShade.Shader>) =
            map |> Map.toList |> List.map (fun (n, s) ->
                ShaderGroup.General { Name = n; Stage = ShaderStage.ofFShade s.shaderStage; Value = s}
            )

        let hitGroups =
            effect.HitGroups |> Map.toList |> List.collect (fun (n, g) ->
                g.PerRayType |> Map.toList |> List.map (fun (rayType, entry) ->
                    ShaderGroup.HitGroup {
                        Name = n;
                        RayType = rayType;
                        AnyHit = entry.AnyHit;
                        ClosestHit = entry.ClosestHit;
                        Intersection = entry.Intersection
                    }
                )
            )

        let groups =
            [ yield! effect.RayGenerationShaders |> toGeneralGroups
              yield! effect.MissShaders |> toGeneralGroups
              yield! effect.CallableShaders |> toGeneralGroups
              yield! hitGroups ]
  
        let glsl =
            effect |> FShade.RaytracingEffect.toModule |> FShade.Backends.ModuleCompiler.compileGLSLRaytracing

        let defines (stage : ShaderStage) (name : Symbol) (rayType : Option<Symbol>) =
            let stage = ShaderStage.toFShade stage

            match rayType with
            | Some rt -> [ sprintf "%A_%A_%A" stage name rt ]
            | _ -> [ sprintf "%A_%A" stage name ]

        let stages =
            let mutable index = 0

            groups |> List.map (ShaderGroup.map (fun name rayType stage shader ->
                inc &index
                let def = defines stage name rayType
                let mdl = ShaderModule.ofGLSLWithTarget GLSLang.Target.SPIRV_1_4 stage def glsl device
                { Index = uint32 (index - 1); Module = mdl }
            ))

        let shaders =
            stages
            |> List.collect ShaderGroup.toList
            |> List.map (fun i -> i.Module.[i.Module.Stage])
            |> List.toArray

        let pipelineLayout = device.CreatePipelineLayout(shaders, 1, Set.empty)

        new RaytracingProgram(device, effect, stages, pipelineLayout)


[<AbstractClass; Sealed; Extension>]
type DeviceRaytracingProgramExtensions private() =

    [<Extension>]
    static member CreateRaytracingProgram(this : Device, effect : FShade.RaytracingEffect) =
        RaytracingProgram.ofEffect this effect