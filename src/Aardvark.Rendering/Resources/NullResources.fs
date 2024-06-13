namespace Aardvark.Rendering

open System
open FSharp.Data.Adaptive

#nowarn "44"

type NullTexture private () =
    static let instance = NullTexture() :> ITexture
    static let instanceConst = AVal.constant instance

    static member Instance = instance
    static member InstanceConst = instanceConst

    override x.GetHashCode() = 0
    override x.Equals o =
        match o with
        | :? NullTexture -> true
        | _ -> false

    interface ITexture with
        member x.WantMipMaps = false

[<AutoOpen>]
module NullResources =

    let nullTexture = NullTexture.Instance

    let nullTextureConst = NullTexture.InstanceConst

    let isNullResource (obj : obj) =
        match obj with
        | :? NullTexture -> true
        | _ -> false

    let isValidResourceAdaptive (m : IAdaptiveValue) =
        match m with
        | :? ISingleValueBuffer -> AVal.constant false
        | _ ->
            AVal.custom (fun t ->
                not <| isNullResource (m.GetValueUntyped t)
            )