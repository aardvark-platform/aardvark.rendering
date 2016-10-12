namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Ag
open Aardvark.SceneGraph
open Aardvark.Base.Rendering


[<AutoOpen>]
module EnvironmentSemantics =

    [<Semantic>]
    type DefaultValues() =

        let getViewPosition (viewTrafo : Trafo3d) = viewTrafo.GetViewPosition()

        member x.CameraLocation(e : Sg.ViewTrafoApplicator) =
            e.Child?CameraLocation <- Mod.map getViewPosition e.ViewTrafo

        member x.LightLocation(e : obj) : IMod<V3d> =
            e?CameraLocation


        member x.RcpViewportSize(e : ISg) = e?ViewportSize |> Mod.map (fun (s : V2i) -> 1.0 / (V2d s))