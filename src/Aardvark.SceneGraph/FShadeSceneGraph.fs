namespace Aardvark.SceneGraph
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering

[<AutoOpen>]
module FShadeSceneGraph =

    type SgEffectBuilder() =
        inherit EffectBuilder()

        member x.Run(f : unit -> IMod<list<FShadeEffect>>) =
            let surface = 
                f() |> Mod.map (fun effects ->
                    effects
                        |> FShade.SequentialComposition.compose
                        |> FShadeSurface
                        :> ISurface
                )

            fun (sg : ISg) -> Sg.SurfaceApplicator(surface, sg) :> ISg

    module Sg =
        let effect (s : #seq<FShadeEffect>) (sg : ISg) =
            let e = FShade.SequentialComposition.compose s
            let s = Mod.constant (FShadeSurface(e) :> ISurface)
            Sg.SurfaceApplicator(s, sg) :> ISg

        let effect' (e : IMod<FShadeEffect>) (sg : ISg) =
            let s = e |> Mod.map (fun e -> (FShadeSurface(e) :> ISurface))
            Sg.SurfaceApplicator(s, sg) :> ISg

        let shader = SgEffectBuilder()