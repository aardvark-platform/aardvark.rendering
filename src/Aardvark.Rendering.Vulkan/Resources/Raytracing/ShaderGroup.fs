namespace Aardvark.Rendering.Vulkan.Raytracing

open Aardvark.Base
open Aardvark.Rendering

type GeneralShader<'T> =
    {
        Name  : Option<Symbol>
        Stage : ShaderStage
        Value : 'T
    }

type HitGroup<'T> =
    {
        Name         : Symbol
        RayType      : Symbol
        AnyHit       : Option<'T>
        ClosestHit   : Option<'T>
        Intersection : Option<'T>
    }

// A shader group is either a single general shader (raygen, miss, or callable),
// or a hit group containing an optional any-hit, closest-hit, and intersection shader
[<RequireQualifiedAccess>]
type ShaderGroup<'T> =
    | General  of GeneralShader<'T>
    | HitGroup of HitGroup<'T>

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ShaderGroup =

    let private mapHitGroup (mapping : Symbol -> Symbol -> ShaderStage -> 'T1 -> 'T2) (group : HitGroup<'T1>) =
        { Name         = group.Name
          RayType      = group.RayType
          AnyHit       = group.AnyHit       |> Option.map (mapping group.Name group.RayType ShaderStage.AnyHit)
          ClosestHit   = group.ClosestHit   |> Option.map (mapping group.Name group.RayType ShaderStage.ClosestHit)
          Intersection = group.Intersection |> Option.map (mapping group.Name group.RayType ShaderStage.Intersection) }

    let map (mapping : Option<Symbol> -> Option<Symbol> -> ShaderStage -> 'T1 -> 'T2) (group : ShaderGroup<'T1>) =
        match group with
        | ShaderGroup.General g ->
            let value = g.Value |> mapping g.Name None g.Stage
            ShaderGroup.General { Name = g.Name; Stage = g.Stage; Value = value }

        | ShaderGroup.HitGroup g ->
            ShaderGroup.HitGroup (g |> mapHitGroup (fun n rt st v -> mapping (Some n) (Some rt) st v))

    let iter (action : Option<Symbol> -> Option<Symbol> -> ShaderStage -> 'T -> unit) (group : ShaderGroup<'T>) =
        group |> map action |> ignore

    let set (value : 'T2) (group : ShaderGroup<'T1>) =
        group |> map (fun _ _ _ _ -> value)

    let isRaygen = function
        | ShaderGroup.General g -> g.Stage = ShaderStage.RayGeneration
        | _ -> false

    let isMiss = function
        | ShaderGroup.General g -> g.Stage = ShaderStage.Miss
        | _ -> false

    let isCallable = function
        | ShaderGroup.General g -> g.Stage = ShaderStage.Callable
        | _ -> false

    let isHitGroup = function
        | ShaderGroup.HitGroup _ -> true
        | _ -> false

    let name = function
        | ShaderGroup.General g -> g.Name
        | ShaderGroup.HitGroup g -> Some g.Name

    let rayType = function
        | ShaderGroup.HitGroup g -> Some g.RayType
        | _ -> None

    let toList (group : ShaderGroup<'T>) =
        match group with
        | ShaderGroup.General g -> [ g.Value ]
        | ShaderGroup.HitGroup g ->
            [ yield! g.AnyHit |> Option.toList
              yield! g.ClosestHit |> Option.toList
              yield! g.Intersection |> Option.toList ]