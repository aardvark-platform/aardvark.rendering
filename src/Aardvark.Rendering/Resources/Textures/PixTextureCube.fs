namespace Aardvark.Rendering

open Aardvark.Base
open System.Runtime.InteropServices

type PixTextureCube(data : PixCube, textureParams : TextureParams) =

    member x.PixCube = data
    member x.TextureParams = textureParams

    new(data : PixCube, [<Optional; DefaultParameterValue(true)>] wantMipMaps : bool) =
        PixTextureCube(data, { TextureParams.empty with wantMipMaps = wantMipMaps })

    override x.GetHashCode() =
        HashCode.Combine(data.GetHashCode(), textureParams.GetHashCode())

    override x.Equals o =
        match o with
        | :? PixTextureCube as o ->
            data = o.PixCube && textureParams = o.TextureParams
        | _ ->
            false

    interface ITexture with
        member x.WantMipMaps = textureParams.wantMipMaps


[<AutoOpen>]
module PixCubeTextureExtensions =

    open System.Runtime.CompilerServices

    [<AbstractClass; Sealed; Extension>]
    type PixCubeExtensions private() =

        [<Extension>]
        static member ToTexture(this : PixCube, mipMaps : bool) =
            PixTextureCube(this, mipMaps) :> ITexture

    module PixCube =
        let toTexture (mipMaps : bool) (c : PixCube) =
            c.ToTexture mipMaps