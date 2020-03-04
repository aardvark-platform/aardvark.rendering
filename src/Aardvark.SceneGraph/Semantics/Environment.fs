namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Ag
open Aardvark.SceneGraph
open Aardvark.Base.Rendering


[<AutoOpen>]
module EnvironmentSemantics =

    type Ag.Scope with
        member x.CameraLocation : aval<V3d> = x?CameraLocation

    [<Rule>]
    type DefaultValues() =

        let getViewPosition (viewTrafo : Trafo3d) = viewTrafo.GetViewPosition()

        member x.CameraLocation(e : Sg.ViewTrafoApplicator, scope : Ag.Scope) =
            e.Child?CameraLocation <- AVal.map getViewPosition e.ViewTrafo

        member x.LightLocation(e : obj, scope : Ag.Scope) : aval<V3d> =
            scope.CameraLocation

        member x.RcpViewportSize(e : ISg, scope : Ag.Scope) = 
            scope?ViewportSize |> AVal.map (fun (s : V2i) -> 1.0 / (V2d s))
