namespace Aardvark.Rendering.Vulkan.Raytracing

#nowarn "9"

open System
open System.IO
open System.Runtime.CompilerServices

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan

type RaytracingStageInfo =
    { Index  : uint32
      Module : ShaderModule }

type RaytracingProgram (device : Device,
                        pipelineLayout : PipelineLayout,
                        shaderBindingTableLayout : FShade.ShaderBindingTableLayout,
                        stages : ShaderGroup<RaytracingStageInfo> list,
                        shader : FShade.GLSL.GLSLShader) =
    inherit CachedResource(device)

    member x.Groups = stages
    member x.ShaderBindingTableLayout = shaderBindingTableLayout
    member x.PipelineLayout = pipelineLayout
    member x.Shader = shader

    [<Obsolete>]
    member x.Glsl = shader.code

    [<Obsolete("Use PipelineLayout")>]
    member x.Layout = pipelineLayout

    override x.Destroy() =
        stages |> List.iter (
            ShaderGroup.iter (fun _ _ _ s -> s.Module.Dispose())
        )
        pipelineLayout.Dispose()

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RaytracingProgram =

    module private ShaderSlot =
        let ofStage (name : Option<Symbol>) (ray : Option<Symbol>) (stage : ShaderStage) =
            match stage, name, ray with
            | ShaderStage.RayGeneration, _, _           -> FShade.ShaderSlot.RayGeneration
            | ShaderStage.Miss, Some n, _               -> FShade.ShaderSlot.Miss n
            | ShaderStage.Callable, Some n, _           -> FShade.ShaderSlot.Callable n
            | ShaderStage.AnyHit, Some n, Some r        -> FShade.ShaderSlot.AnyHit (n, r)
            | ShaderStage.ClosestHit, Some n, Some r    -> FShade.ShaderSlot.ClosestHit (n, r)
            | ShaderStage.Intersection, Some n, Some r  -> FShade.ShaderSlot.Intersection (n, r)
            | _ ->
                failwithf "Invalid raytracing shader slot (stage = %A, name = %A, ray = %A)" stage name ray


    let private create (device : Device) (shader : FShade.GLSL.GLSLShader) (layout : FShade.ShaderBindingTableLayout) (stages : ShaderGroup<RaytracingStageInfo> list) =
        let shaders =
            stages
            |> List.collect ShaderGroup.toList
            |> List.map (fun i -> i.Module)
            |> List.toArray

        let pipelineLayout = device.CreatePipelineLayout(shader.iface, shaders, 1, Set.empty)

        new RaytracingProgram(device, pipelineLayout, layout, stages, shader)

    let private compileEffect (device : Device) (effect : FShade.RaytracingEffect) =
        let toGeneralGroups (map : Map<Symbol, FShade.Shader>) =
            map |> Map.toList |> List.map (fun (n, s) ->
                ShaderGroup.General { Name = Some n; Stage = ShaderStage.ofFShade s.shaderStage; Value = s}
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
            [ yield ShaderGroup.General { Name = None; Stage = ShaderStage.RayGeneration; Value = effect.RayGenerationShader }
              yield! effect.MissShaders |> toGeneralGroups
              yield! effect.CallableShaders |> toGeneralGroups
              yield! hitGroups ]

        let glsl =
            effect |> FShade.RaytracingEffect.toModule |> FShade.Backends.ModuleCompiler.compileGLSLRaytracing

        if device.DebugConfig.PrintShaderCode then
            ShaderCodeReporting.logLines glsl.code

        let stages =
            let mutable index = 0

            groups |> List.map (ShaderGroup.map (fun name ray stage shader ->
                inc &index
                let slot = stage |> ShaderSlot.ofStage name ray
                let mdl = ShaderModule.ofGLSLWithTarget GLSLang.Target.SPIRV_1_4 slot glsl device
                { Index = uint32 (index - 1); Module = mdl }
            ))

        stages |> create device glsl effect.ShaderBindingTableLayout


    module private FileCache =

        module private Pickling =

            [<AutoOpen>]
            module private Binary =

                type RaytracingStageBinary =
                    { Index  : uint32
                      Binary : byte[] }

                type ShaderGroupsBinary =
                    ShaderGroup<RaytracingStageBinary> list

                type RaytracingProgramBinary =
                    { Shader : FShade.GLSL.GLSLShader
                      Groups : ShaderGroupsBinary
                      Layout : FShade.ShaderBindingTableLayout }

                module RaytracingStageInfo =

                    let toBinary (info : RaytracingStageInfo) =
                        { Index  = info.Index
                          Binary = info.Module.SpirV }

                    let ofBinary (device : Device) (slot : FShade.ShaderSlot) (binary : RaytracingStageBinary) =
                        { Index  = binary.Index
                          Module = binary.Binary |> ShaderModule.ofBinary device slot }

                module ShaderGroupsBinary =

                    let ofStageInfos (groups : ShaderGroup<RaytracingStageInfo> list) : ShaderGroupsBinary =
                        groups |> List.map (
                            ShaderGroup.map (fun _ _ _ -> RaytracingStageInfo.toBinary)
                        )

                    let toStageInfos (device : Device) (binary : ShaderGroupsBinary) =
                        binary |> List.map (
                            ShaderGroup.map (fun name rayType stage ->
                                let slot = stage |> ShaderSlot.ofStage name rayType
                                RaytracingStageInfo.ofBinary device slot
                            )
                        )

                module RaytracingProgram =

                    let toBinary (program : RaytracingProgram) =
                        { Shader = program.Shader
                          Groups = ShaderGroupsBinary.ofStageInfos program.Groups
                          Layout = program.ShaderBindingTableLayout }

                    let ofBinary (device : Device) (binary : RaytracingProgramBinary) =
                        let stages = binary.Groups |> ShaderGroupsBinary.toStageInfos device
                        stages |> create device binary.Shader binary.Layout


            let toByteArray (program : RaytracingProgram) =
                let binary = RaytracingProgram.toBinary program
                ShaderProgram.pickler.Pickle binary

            let ofByteArray (device : Device) (reference : RaytracingProgram option) (data : byte[]) =
                let binary : RaytracingProgramBinary = ShaderProgram.pickler.UnPickle data

                reference |> Option.iter (fun program ->
                    if binary <> RaytracingProgram.toBinary program then
                        failwith "differs from recompiled reference"
                )

                RaytracingProgram.ofBinary device binary


        let private tryGetCacheFile (device : Device) (id : string) =
            device.ShaderCachePath |> Option.map (fun prefix ->
                let hash = ShaderProgram.hashFileName id
                Path.combine [prefix; hash + ".rtx"]
            )

        let tryRead (device : Device) (effect : FShade.RaytracingEffect) =
            tryGetCacheFile device effect.Id
            |> Option.bind (fun file ->
                if File.Exists file then
                    try
                        let data = File.readAllBytes file

                        let reference =
                            if device.DebugConfig.VerifyShaderCacheIntegrity then
                                Some <| compileEffect device effect
                            else
                                None

                        try
                            data |> Pickling.ofByteArray device reference |> Some
                        finally
                            reference |> Option.iter Disposable.dispose
                    with
                    | exn ->
                        Log.warn "[Vulkan] Failed to read from raytracing program file cache '%s': %s" file exn.Message
                        None
                else
                    None
            )

        let write (device : Device) (id : string) (program : RaytracingProgram) =
            tryGetCacheFile device id
            |> Option.iter (fun file ->
                try
                    let binary = Pickling.toByteArray program
                    binary |> File.writeAllBytes file
                with
                | exn ->
                    Log.warn "[Vulkan] Failed to write to raytracing program file cache '%s': %s" file exn.Message
            )


    let private cache = Symbol.Create "RaytracingProgramCache"

    let ofEffect (device : Device) (effect : FShade.RaytracingEffect) =
        device.GetCached(cache, effect, fun effect ->
            match effect |> FileCache.tryRead device with
            | Some program ->
                program

            | _ ->
                let program = effect |> compileEffect device
                program |> FileCache.write device effect.Id
                program
        )


[<AbstractClass; Sealed; Extension>]
type DeviceRaytracingProgramExtensions private() =

    [<Extension>]
    static member CreateRaytracingProgram(this : Device, effect : FShade.RaytracingEffect) =
        RaytracingProgram.ofEffect this effect