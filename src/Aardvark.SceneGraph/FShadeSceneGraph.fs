namespace Aardvark.SceneGraph

open Aardvark.Rendering

[<AutoOpen>]
module FShadeSceneGraph =

    type SgEffectBuilder() =
        inherit EffectBuilder()

        member x.Run(f : unit -> list<FShadeEffect>) =
            let surface = f() |> FShade.Effect.compose |> Surface.FShadeSimple
            fun (sg : ISg) -> Sg.SurfaceApplicator(surface, sg) :> ISg

    module Sg =
        let effect (s : #seq<FShadeEffect>) (sg : ISg) =
            let s = FShade.Effect.compose s |> Surface.FShadeSimple
            Sg.SurfaceApplicator(s, sg) :> ISg

        let shader = SgEffectBuilder()