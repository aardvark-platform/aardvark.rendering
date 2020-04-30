namespace Aardvark.Base

type NullTexture() =
    override x.GetHashCode() = 0
    override x.Equals o =
        match o with
        | :? NullTexture -> true
        | _ -> false

    interface ITexture with
        member x.WantMipMaps = false