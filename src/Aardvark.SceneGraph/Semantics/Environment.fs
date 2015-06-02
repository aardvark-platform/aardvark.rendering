namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Ag
open Aardvark.Base.AgHelpers
open Aardvark.SceneGraph
open Aardvark.Base.Rendering


[<AutoOpen>]
module EnvironmentSemantics =

    [<Semantic>]
    type DefaultValues() =

        let getViewPosition (viewTrafo : Trafo3d) = viewTrafo.GetViewPosition()

        member x.LightLocations(e : Sg.Environment) =
            e.Child?LightLocations <- [| Mod.map getViewPosition e.ViewTrafo |]

        member x.LightLocation(e : Sg.Environment) =
            e.Child?LightLocation <- Mod.map getViewPosition e.ViewTrafo 

        member x.CameraLocation(e : Sg.Environment) =
            e.Child?CameraLocation <- Mod.map getViewPosition e.ViewTrafo

        member x.CameraLocation(e : Sg.ViewTrafoApplicator) =
            e.Child?CameraLocation <- Mod.map getViewPosition e.ViewTrafo


        member x.NormalMatrix(s : ISg) : IMod<M33d> = 
            Mod.map (fun (t : Trafo3d) -> t.Backward.Transposed.UpperLeftM33()) s?ModelTrafo


        member x.ViewportSize(e : Sg.Environment) = e.Child?ViewportSize <- e.ViewSize
          
        member x.RcpViewportSize(e : ISg) = e?ViewportSize |> Mod.map (fun (s : V2i) -> 1.0 / (V2d s))