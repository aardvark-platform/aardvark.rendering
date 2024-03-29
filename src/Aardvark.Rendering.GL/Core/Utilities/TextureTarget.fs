﻿namespace Aardvark.Rendering.GL

open Aardvark.Base
open Aardvark.Rendering
open OpenTK.Graphics.OpenGL4

module internal TextureTarget =
    let ofParameters (dim : TextureDimension) (isArray : bool) (isMS : bool) =
        match dim, isArray, isMS with
        | TextureDimension.Texture1D,      _,       true     -> failwith "Texture1D cannot be multisampled"
        | TextureDimension.Texture1D,      true,    _        -> TextureTarget.Texture1DArray
        | TextureDimension.Texture1D,      false,   _        -> TextureTarget.Texture1D

        | TextureDimension.Texture2D,      false,   false    -> TextureTarget.Texture2D
        | TextureDimension.Texture2D,      true,    false    -> TextureTarget.Texture2DArray
        | TextureDimension.Texture2D,      false,   true     -> TextureTarget.Texture2DMultisample
        | TextureDimension.Texture2D,      true,    true     -> TextureTarget.Texture2DMultisampleArray

        | TextureDimension.Texture3D,      false,   false    -> TextureTarget.Texture3D
        | TextureDimension.Texture3D,      _,       _        -> failwith "Texture3D cannot be multisampled or an array"

        | TextureDimension.TextureCube,   false,    false    -> TextureTarget.TextureCubeMap
        | TextureDimension.TextureCube,   true,     false    -> TextureTarget.TextureCubeMapArray
        | TextureDimension.TextureCube,   _,        true     -> failwith "TextureCube cannot be multisampled"

        | _ -> failwithf "unknown texture dimension: %A" dim

    let toProxy = function
        | TextureTarget.Texture1D                   -> TextureTarget.ProxyTexture1D
        | TextureTarget.Texture1DArray              -> TextureTarget.ProxyTexture1DArray

        | TextureTarget.Texture2D                   -> TextureTarget.ProxyTexture2D
        | TextureTarget.Texture2DArray              -> TextureTarget.ProxyTexture2DArray
        | TextureTarget.Texture2DMultisample        -> TextureTarget.ProxyTexture2DMultisample
        | TextureTarget.Texture2DMultisampleArray   -> TextureTarget.ProxyTexture2DMultisampleArray

        | TextureTarget.Texture3D                   -> TextureTarget.ProxyTexture3D

        | TextureTarget.TextureCubeMap              -> TextureTarget.ProxyTextureCubeMap
        | TextureTarget.TextureCubeMapArray         -> TextureTarget.ProxyTextureCubeMapArray

        | target -> failwithf "no corresponding proxy target for %A" target

    // cubeSides are sorted like in their implementation (making some things easier)
    let cubeSides =
        [|
            CubeSide.PositiveX, TextureTarget.TextureCubeMapPositiveX
            CubeSide.NegativeX, TextureTarget.TextureCubeMapNegativeX

            CubeSide.PositiveY, TextureTarget.TextureCubeMapPositiveY
            CubeSide.NegativeY, TextureTarget.TextureCubeMapNegativeY

            CubeSide.PositiveZ, TextureTarget.TextureCubeMapPositiveZ
            CubeSide.NegativeZ, TextureTarget.TextureCubeMapNegativeZ
        |]

    let toSliceTarget (slice : int) = function
        | TextureTarget.TextureCubeMap -> cubeSides.[slice] |> snd
        | target -> target
