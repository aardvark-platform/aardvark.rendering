namespace Aardvark.Rendering.Raytracing

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive

type RaytracingSceneDescription =
    {
        /// The instances in the scene.
        Instances : aset<TraceInstance>

        /// Usage flag for the underlying acceleration structure.
        Usage   : AccelerationStructureUsage
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RaytracingSceneDescription =

    let empty =
        { Instances = ASet.empty; Usage = AccelerationStructureUsage.Static }

    let ofASet (instances : aset<TraceInstance>) =
        { Instances = instances; Usage = AccelerationStructureUsage.Static }

    let ofList (instances : List<TraceInstance>) =
        instances |> ASet.ofList |> ofASet

    let ofArray (instances : TraceInstance[]) =
        instances |> ASet.ofArray |> ofASet

    let ofSeq (instances : seq<TraceInstance>) =
        instances |> ASet.ofSeq |> ofASet

    let usage (u : AccelerationStructureUsage) (scene : RaytracingSceneDescription) =
        { scene with Usage = u }


type RaytracingPipelineState =
    {
        Effect            : FShade.RaytracingEffect
        Scenes            : Map<Symbol, RaytracingSceneDescription>
        Uniforms          : Map<Symbol, IAdaptiveValue>
        MaxRecursionDepth : aval<int>
    }