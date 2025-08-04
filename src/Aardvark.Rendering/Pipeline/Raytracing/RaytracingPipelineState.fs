namespace Aardvark.Rendering.Raytracing

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive

type RaytracingSceneDescription =
    {
        /// The instances in the scene.
        Instances : aset<ITraceInstance>

        /// Usage flags for the underlying acceleration structure.
        Usage   : AccelerationStructureUsage
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RaytracingSceneDescription =

    let empty =
        { Instances = ASet.empty; Usage = AccelerationStructureUsage.Dynamic ||| AccelerationStructureUsage.Update }

    let ofASet (instances : aset<#ITraceInstance>) =
        { empty with Instances = instances |> ASet.map (fun x -> x :> ITraceInstance) }

    let ofList (instances : List<#ITraceInstance>) =
        instances |> ASet.ofList |> ofASet

    let ofArray (instances : #ITraceInstance[]) =
        instances |> ASet.ofArray |> ofASet

    let ofSeq (instances : seq<#ITraceInstance>) =
        instances |> ASet.ofSeq |> ofASet

    let usage (u : AccelerationStructureUsage) (scene : RaytracingSceneDescription) =
        { scene with Usage = u }


type RaytracingPipelineState =
    {
        Effect            : FShade.RaytracingEffect
        Scenes            : Map<Symbol, RaytracingSceneDescription>
        Uniforms          : IUniformProvider
        MaxRecursionDepth : aval<int>
    }