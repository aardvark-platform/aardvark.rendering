namespace Aardvark.Rendering.Raytracing

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive

type RaytracingScene =
    {
        /// The objects in the scene.
        Objects : aset<TraceObject>

        /// Usage flag for the underlying acceleration structure.
        Usage   : AccelerationStructureUsage
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RaytracingScene =

    let empty =
        { Objects = ASet.empty; Usage = AccelerationStructureUsage.Static }

    let ofASet (objects : aset<TraceObject>) =
        { Objects = objects; Usage = AccelerationStructureUsage.Static }

    let ofList (objects : List<TraceObject>) =
        objects |> ASet.ofList |> ofASet

    let ofArray (objects : TraceObject[]) =
        objects |> ASet.ofArray |> ofASet

    let ofSeq(objects : seq<TraceObject>) =
        objects |> ASet.ofSeq |> ofASet

    let usage (u : AccelerationStructureUsage) (scene : RaytracingScene) =
        { scene with Usage = u }


type RaytracingPipelineState =
    {
        Effect            : FShade.RaytracingEffect
        Scenes            : Map<Symbol, RaytracingScene>
        Uniforms          : Map<Symbol, IAdaptiveValue>
        MaxRecursionDepth : aval<int>
    }