namespace Aardvark.Base

open FSharp.Data.Adaptive

type NullTexture() =
    override x.GetHashCode() = 0
    override x.Equals o =
        match o with
        | :? NullTexture -> true
        | _ -> false

    interface ITexture with
        member x.WantMipMaps = false

[<AutoOpen>]
module NullResources =

    let isNullResource (obj : obj) =
        match obj with
        | :? NullTexture -> true
        | _ -> false

    let isValidResourceAdaptive (m : IAdaptiveValue) =
        match m with
        | :? SingleValueBuffer -> AVal.constant false
        | _ ->
            AVal.custom (fun t ->
                not <| isNullResource (m.GetValueUntyped t)
            )