namespace Aardvark.Rendering

open Aardvark.Base

type PixTextureCube(data : PixImageCube,  textureParams : TextureParams) =

    member x.PixImageCube = data
    member x.TextureParams = textureParams

    new(data : PixImageCube, wantMipMaps : bool) =
        PixTextureCube(data, { TextureParams.empty with wantMipMaps = wantMipMaps })

    override x.GetHashCode() =
        HashCode.Combine(data.GetHashCode(), textureParams.GetHashCode())

    override x.Equals o =
        match o with
        | :? PixTextureCube as o ->
            data = o.PixImageCube && textureParams = o.TextureParams
        | _ ->
            false

    interface ITexture with
        member x.WantMipMaps = textureParams.wantMipMaps


[<AutoOpen>]
module PixImageCubeTextureExtensions =

    open System.Runtime.CompilerServices

    [<AbstractClass; Sealed; Extension>]
    type PixImageCubeExtensions private() =

        [<Extension>]
        static member ToTexture(this : PixImageCube, mipMaps : bool) =
            PixTextureCube(this, mipMaps) :> ITexture

    module PixImageCube =
        let toTexture (mipMaps : bool) (c : PixImageCube) =
            c.ToTexture mipMaps