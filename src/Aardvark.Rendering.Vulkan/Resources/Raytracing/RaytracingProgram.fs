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

    module private Logging =

        let logLines (code : string) =
            let lineCount = String.lineCount code
            let lineColumns = 1 + int (Fun.Log10 lineCount)
            let lineFormatLen = lineColumns + 3
            let sb = new System.Text.StringBuilder(code.Length + lineFormatLen * lineCount + 10)

            let fmtStr = "{0:" + lineColumns.ToString() + "} : "
            let mutable lineEnd = code.IndexOf('\n')
            let mutable lineStart = 0
            let mutable lineCnt = 1
            while (lineEnd >= 0) do
                sb.Clear() |> ignore
                let line = code.Substring(lineStart, lineEnd - lineStart)
                sb.Append(lineCnt.ToString().PadLeft(lineColumns)) |> ignore
                sb.Append(": ")  |> ignore
                sb.Append(line) |> ignore
                Report.Line("{0}", sb.ToString())
                lineStart <- lineEnd + 1
                lineCnt <- lineCnt + 1
                lineEnd <- code.IndexOf('\n', lineStart)
                ()

            let lastLine = code.Substring(lineStart)
            if lastLine.Length > 0 then
                sb.Clear() |> ignore
                sb.Append(lineCnt.ToString()) |> ignore
                sb.Append(": ")  |> ignore
                sb.Append(lastLine) |> ignore
                Report.Line("{0}", sb.ToString())

    // TODO: Caching
    let ofEffect (device : Device) (effect : FShade.RaytracingEffect) =

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

        Logging.logLines glsl.code

        let defines (stage : ShaderStage) (name : Option<Symbol>) (rayType : Option<Symbol>) =
            let stage = ShaderStage.toFShade stage

            match name, rayType with
            | Some n, Some rt -> [ sprintf "%A_%A_%A" stage n rt ]
            | Some n, _ -> [ sprintf "%A_%A" stage n ]
            | _ -> [ sprintf "%A" stage ]

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