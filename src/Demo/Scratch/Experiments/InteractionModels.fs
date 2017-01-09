namespace Scratch.DomainTypes

open System
open Aardvark.Base
open Aardvark.Base.Incremental

module TranslateController =

    type Axis = X | Y | Z

    [<DomainType>]
    type Model = {
        hovered           : Option<Axis>
        activeTranslation : Option<Plane3d * V3d>
        trafo             : Trafo3d
    }


module SimpleDrawingApp =

    type Polygon = list<V3d>

    type OpenPolygon = {
        cursor         : Option<V3d>
        finishedPoints : list<V3d>
    }
    
    [<DomainType>]
    type Model = {
        finished : list<Polygon>
        working  : Option<OpenPolygon>
    }

module PlaceTransformObjects =

    [<DomainType>]
    type Model = {
        objects : list<Trafo3d>
        hoveredObj : Option<int>
        selectedObj : Option<int * TranslateController.Model>
    }