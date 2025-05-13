namespace Aardvark.Rendering.Vulkan.Raytracing

#nowarn "9"

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

    override x.Destroy() =
        stages |> List.iter (
            ShaderGroup.iter (fun _ _ _ s -> s.Module.Dispose())
        )
        pipelineLayout.Dispose()

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RaytracingProgram =
    open System.Collections.Generic

    type private ShaderSlot = FShade.ShaderSlot
    type private HitGroupSlot = (struct (Symbol * Symbol))

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
        let pipelineLayout = device.CreatePipelineLayout(shader.iface, 1, Set.empty)
        new RaytracingProgram(device, pipelineLayout, layout, stages, shader)

    let private compileEffect (device : Device) (effect : FShade.RaytracingEffect) =
        let general = List<GeneralShader<_>>()
        let hitGroups = Dictionary<HitGroupSlot, HitGroup<_>>()

        let getHitGroup ((name, ray) as slot : HitGroupSlot) =
            match hitGroups.TryGetValue slot with
            | (true, group) -> group
            | _ ->
                { Name         = name
                  RayType      = ray
                  AnyHit       = None
                  ClosestHit   = None
                  Intersection = None }

        for KeyValue(slot, shader) in effect.Shaders do
            if slot.Stage <> shader.Stage then
                failf "Shader in slot %A has stage %A" slot.Stage shader.Stage

            match slot with
            | ShaderSlot.RayGeneration ->
                general.Add <| {
                    Name  = None
                    Stage = ShaderStage.RayGeneration
                    Value = shader
                }

            | ShaderSlot.Miss name | ShaderSlot.Callable name ->
                general.Add <| {
                    Name  = Some name
                    Stage = ShaderStage.ofFShade slot.Stage
                    Value = shader
                }

            | ShaderSlot.AnyHit (name, ray) ->
                let k = struct (name, ray)
                let g = getHitGroup k
                hitGroups.[k] <- { g with AnyHit = Some shader }

            | ShaderSlot.ClosestHit (name, ray) ->
                let k = struct (name, ray)
                let g = getHitGroup k
                hitGroups.[k] <- { g with ClosestHit = Some shader }

            | ShaderSlot.Intersection (name, ray) ->
                let k = struct (name, ray)
                let g = getHitGroup k
                hitGroups.[k] <- { g with Intersection = Some shader }

            | _ ->
                failf "Invalid shader with slot %A" slot

        let groups =
            let g = general |> Seq.toList |> List.map ShaderGroup.General
            let h = hitGroups.Values |> Seq.toList |> List.map ShaderGroup.HitGroup
            g @ h

        let glsl =
            try
                effect |> FShade.RaytracingEffect.toModule |> FShade.Backends.ModuleCompiler.compileGLSLRaytracing
            with exn ->
                Log.error "%s" exn.Message
                reraise()

        if device.DebugConfig.PrintShaderCode then
            glsl.code |> ShaderCodeReporting.logLines "Compiling shader"

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

                type RaytracingProgramBinary =
                    { Shader : FShade.GLSL.GLSLShader
                      Groups : ShaderGroup<RaytracingStageBinary> list
                      Layout : FShade.ShaderBindingTableLayout }

                module RaytracingStageInfo =

                    let toBinary (info : RaytracingStageInfo) =
                        { Index  = info.Index
                          Binary = info.Module.SpirV }

                    let ofBinary (device : Device) (slot : FShade.ShaderSlot) (binary : RaytracingStageBinary) =
                        { Index  = binary.Index
                          Module = binary.Binary |> ShaderModule.ofBinary device slot }

                module ShaderGroups =

                    let toBinary (groups : ShaderGroup<RaytracingStageInfo> list) =
                        groups |> List.map (
                            ShaderGroup.map (fun _ _ _ -> RaytracingStageInfo.toBinary)
                        )

                    let ofBinary (device : Device) (binary : ShaderGroup<RaytracingStageBinary> list) =
                        binary |> List.map (
                            ShaderGroup.map (fun name rayType stage ->
                                let slot = stage |> ShaderSlot.ofStage name rayType
                                RaytracingStageInfo.ofBinary device slot
                            )
                        )

                module RaytracingProgramBinary =
                    open FShade.GLSL
                    open ShaderProgram.ShaderProgramData

                    type BinaryWriter with
                        member inline x.Write(value : RaytracingStageBinary) =
                            x.Write value.Index
                            x.Write value.Binary.Length
                            x.Write value.Binary

                        member inline private x.Write(value : Option<'T>, write : 'T -> unit) =
                            match value with
                            | Some sym -> x.Write true; write sym
                            | None -> x.Write false

                        member inline x.Write(value : Option<Symbol>) =
                            x.Write(value, x.Write)

                        member inline x.Write(value : Option<RaytracingStageBinary>) =
                            x.Write(value, x.Write)

                        member inline x.Write(value : ShaderGroup<RaytracingStageBinary>) =
                            match value with
                            | ShaderGroup.General g ->
                                x.Write 0uy
                                x.Write g.Name
                                x.Write (uint8 g.Stage)
                                x.Write g.Value

                            | ShaderGroup.HitGroup g ->
                                x.Write 1uy
                                x.Write g.Name
                                x.Write g.RayType
                                x.Write g.AnyHit
                                x.Write g.ClosestHit
                                x.Write g.Intersection

                        member inline x.Write(value : Map<Symbol, int>) =
                            x.Write value.Count
                            value |> Map.iter (fun key value ->
                                x.Write key
                                x.Write value
                            )

                        member inline x.Write(value : FShade.ShaderBindingTableLayout) =
                            x.Write value.RayOffsets
                            x.Write value.MissIndices
                            x.Write value.CallableIndices

                    type BinaryReader with
                        member inline x.ReadRaytracingStageBinary() =
                            let index = x.ReadUInt32()

                            let binary =
                                let count = x.ReadInt32()
                                x.ReadBytes count

                            { Index = index
                              Binary = binary }

                        member inline private x.ReadOption(read : unit -> 'T) =
                            if x.ReadBoolean() then Some <| read()
                            else None

                        member inline x.ReadSymOption() =
                            x.ReadOption x.ReadSym

                        member inline x.ReadRaytracingStageBinaryOption() =
                            x.ReadOption x.ReadRaytracingStageBinary

                        member inline x.ReadShaderGroupBinary() =
                            match x.ReadByte() with
                            | 0uy ->
                                ShaderGroup.General {
                                    Name  = x.ReadSymOption()
                                    Stage = x.ReadByte() |> int |> unbox<ShaderStage>
                                    Value = x.ReadRaytracingStageBinary()
                                }

                            | 1uy ->
                                ShaderGroup.HitGroup {
                                    Name         = x.ReadSym()
                                    RayType      = x.ReadSym()
                                    AnyHit       = x.ReadRaytracingStageBinaryOption()
                                    ClosestHit   = x.ReadRaytracingStageBinaryOption()
                                    Intersection = x.ReadRaytracingStageBinaryOption()
                                }

                            | id ->
                                raise <| InvalidDataException($"{id} is not a valid ShaderGroup identifier.")

                        member inline x.ReadSymIntMap() =
                            let count = x.ReadInt32()

                            List.init count (fun _ ->
                                x.ReadSym(), x.ReadInt32()
                            )
                            |> Map.ofList

                        member inline x.ReadShaderBindingTableLayout() =
                            { FShade.RayOffsets      = x.ReadSymIntMap()
                              FShade.MissIndices     = x.ReadSymIntMap()
                              FShade.CallableIndices = x.ReadSymIntMap() }


                    let serialize (dst : Stream) (data : RaytracingProgramBinary) =
                        use w = new BinaryWriter(dst, System.Text.Encoding.UTF8, true)

                        w.WriteType data

                        GLSLShader.serialize dst data.Shader

                        w.WriteType data.Groups
                        w.Write data.Groups.Length
                        data.Groups |> List.iter w.Write

                        w.WriteType data.Layout
                        w.Write data.Layout

                    let deserialize (src : Stream) =
                        use r = new BinaryReader(src, System.Text.Encoding.UTF8, true)

                        r.ReadType<RaytracingProgramBinary>()

                        let shader = GLSLShader.deserialize src

                        r.ReadType<ShaderGroup<RaytracingStageBinary> list>()
                        let groups =
                            let count = r.ReadInt32()
                            List.init count (ignore >> r.ReadShaderGroupBinary)

                        r.ReadType<FShade.ShaderBindingTableLayout>()
                        let layout = r.ReadShaderBindingTableLayout()

                        { Shader = shader
                          Groups = groups
                          Layout = layout }

                    let pickle (data : RaytracingProgramBinary) =
                        use ms = new MemoryStream()
                        serialize ms data
                        ms.ToArray()

                    let unpickle (data : byte[]) =
                        use ms = new MemoryStream(data)
                        deserialize ms

                module RaytracingProgram =

                    let toBinary (program : RaytracingProgram) =
                        { Shader = program.Shader
                          Groups = ShaderGroups.toBinary program.Groups
                          Layout = program.ShaderBindingTableLayout }

                    let ofBinary (device : Device) (binary : RaytracingProgramBinary) =
                        let stages = binary.Groups |> ShaderGroups.ofBinary device
                        stages |> create device binary.Shader binary.Layout


            let toByteArray (program : RaytracingProgram) =
                let binary = RaytracingProgram.toBinary program
                RaytracingProgramBinary.pickle binary

            let ofByteArray (device : Device) (reference : RaytracingProgram option) (data : byte[]) =
                let binary = RaytracingProgramBinary.unpickle data

                reference |> Option.iter (fun program ->
                    if binary <> RaytracingProgram.toBinary program then
                        failwith "differs from recompiled reference"
                )

                RaytracingProgram.ofBinary device binary


        let private tryGetCacheFile (device : Device) (id : string) =
            device.ShaderCachePath |> Option.map (fun prefix ->
                let hash = ShaderProgram.FileCacheName.ofEffectId device id
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
                    binary |> File.writeAllBytesSafe file
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