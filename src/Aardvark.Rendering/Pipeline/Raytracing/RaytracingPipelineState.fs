namespace Aardvark.Rendering.Raytracing

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive

type RaytracingScene =
    {
        /// The instances in the scene.
        Instances : aset<ITraceInstance>

        /// Usage flags for the underlying acceleration structure.
        Usage   : AccelerationStructureUsage
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RaytracingScene =

    /// Empty scene with usage Dynamic | Update.
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

    let usage (usage : AccelerationStructureUsage) (scene : RaytracingScene) =
        { scene with Usage = usage }


type RaytracingPipelineState =
    {
        Effect            : FShade.RaytracingEffect
        Scenes            : Map<Symbol, RaytracingScene>
        Uniforms          : IUniformProvider
        MaxRecursionDepth : aval<int>
    }