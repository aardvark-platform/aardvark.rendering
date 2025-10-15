namespace Aardvark.Rendering

open System
open System.Runtime.InteropServices
open Aardvark.Base

/// <summary>
/// Cube texture with data stored in a <see cref="PixCube"/>.
/// The image data is not uploaded until the texture is used and prepared.
/// </summary>
type PixTextureCube =

    /// Image data.
    val PixCube : PixCube

    /// Flags controlling texture creation and upload.
    val TextureParams : TextureParams

    /// <summary>
    /// Creates a new <see cref="PixTextureCube"/> instance from a <see cref="PixCube"/>.
    /// </summary>
    /// <remarks>
    /// The image data is not uploaded until the texture is used and prepared.
    /// </remarks>
    /// <param name="data">Image data.</param>
    /// <param name="textureParams">Flags controlling texture creation and upload.</param>
    /// <exception cref="ArgumentNullException">if <paramref name="data"/> is <c>null</c>.</exception>
    new (data: PixCube, textureParams: TextureParams) =
        if isNull data then raise <| ArgumentNullException(nameof data)
        { PixCube = data; TextureParams = textureParams }

    /// <summary>
    /// Creates a new <see cref="PixTextureCube"/> instance from a <see cref="PixCube"/>.
    /// </summary>
    /// <remarks>
    /// The image data is not uploaded until the texture is used and prepared.
    /// </remarks>
    /// <param name="data">Image data.</param>
    /// <param name="wantMipMaps">If true, the whole mipmap chains are uploaded and missing levels are generated; if false, only the base levels are uploaded without generating the other levels.</param>
    /// <exception cref="ArgumentNullException">if <paramref name="data"/> is <c>null</c>.</exception>
    new (data: PixCube, [<Optional; DefaultParameterValue(true)>] wantMipMaps: bool) =
        let flags = if wantMipMaps then TextureParams.WantMipMaps else TextureParams.None
        PixTextureCube(data, flags)

    override this.GetHashCode() =
        HashCode.Combine(this.PixCube.GetHashCode(), this.TextureParams.GetHashCode())

    member inline private this.Equals(other: PixTextureCube) =
        this.PixCube = other.PixCube && this.TextureParams = other.TextureParams

    override this.Equals(obj: obj) =
        match obj with
        | :? PixTextureCube as other -> this.Equals(other)
        | _ -> false

    interface IEquatable<PixTextureCube> with
        member this.Equals(other) = this.Equals(other)

    interface ITexture with
        member this.WantMipMaps = this.TextureParams.HasFlag TextureParams.WantMipMaps