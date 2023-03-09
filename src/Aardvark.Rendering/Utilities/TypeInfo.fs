namespace Aardvark.Rendering

open System
open Aardvark.Base
open Aardvark.Base.TypeInfo

// TODO: Delete once moved to Aarvark.Base (>= 5.2.21?)
[<AutoOpen>]
module TypeInfoExtensions =

    module TypeInfo =

        let TC3b  = { vectorType = typeof<C3b>;  baseType = TByte;    dimension = 3 }
        let TC3us = { vectorType = typeof<C3us>; baseType = TUInt16;  dimension = 3 }
        let TC3ui = { vectorType = typeof<C3ui>; baseType = TUInt32;  dimension = 3 }
        let TC3f  = { vectorType = typeof<C3f>;  baseType = TFloat32; dimension = 3 }
        let TC3d  = { vectorType = typeof<C3d>;  baseType = TFloat64; dimension = 3 }

        let TC4b  = { vectorType = typeof<C4b>;  baseType = TByte;    dimension = 4 }
        let TC4us = { vectorType = typeof<C4us>; baseType = TUInt16;  dimension = 4 }
        let TC4ui = { vectorType = typeof<C4ui>; baseType = TUInt32;  dimension = 4 }
        let TC4f  = { vectorType = typeof<C4f>;  baseType = TFloat32; dimension = 4 }
        let TC4d  = { vectorType = typeof<C4d>;  baseType = TFloat64; dimension = 4 }

        let ColorTypes : Set<ITypeInfo> =
            Set.ofList [
                TC3b; TC3us; TC3ui; TC3f; TC3d
                TC4b; TC4us; TC4ui; TC4f; TC4d
            ]

        let private typeInfo t = { simpleType = t } :> ITypeInfo

        [<AutoOpen>]
        module Patterns =

            let (|Color|_|) (t : Type) =
                if Set.contains (typeInfo t) ColorTypes then
                    Some ()
                else
                    None

            let (|ColorOf|_|) (t : Type) =
                ColorTypes
                |> Seq.tryFind (fun ti -> ti.Type.Name = t.Name)
                |> Option.map (fun t ->
                    let vt = unbox<VectorType> t
                    vt.dimension, vt.baseType.Type
                )