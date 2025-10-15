namespace Aardvark.Rendering

open System
open System.Runtime.InteropServices
open Aardvark.Base

/// <summary>
/// 3D texture with data stored in a <see cref="PixVolume"/>.
/// The image data is not uploaded until the texture is used and prepared.
/// </summary>
type PixTexture3d =

    /// Image data.
    val PixVolume : PixVolume

    /// Flags controlling texture creation and upload.
    val TextureParams : TextureParams

    /// <summary>
    /// Creates a new <see cref="PixTexture3d"/> instance from a <see cref="PixVolume"/>.
    /// </summary>
    /// <remarks>
    /// The image data is not uploaded until the texture is used and prepared.
    /// </remarks>
    /// <param name="data">Image data.</param>
    /// <param name="textureParams">Flags controlling texture creation and upload.</param>
    /// <exception cref="ArgumentNullException">if <paramref name="data"/> is <c>null</c>.</exception>
    new (data: PixVolume, textureParams: TextureParams) =
        if isNull data then raise <| ArgumentNullException(nameof data)
        { PixVolume = data; TextureParams = textureParams }

    /// <summary>
    /// Creates a new <see cref="PixTexture3d"/> instance from a <see cref="PixVolume"/>.
    /// </summary>
    /// <remarks>
    /// The image data is not uploaded until the texture is used and prepared.
    /// </remarks>
    /// <param name="data">Image data.</param>
    /// <param name="wantMipMaps">If true, a mipmap chain is generated after upload; if false, no mipmap chain is generated.</param>
    /// <exception cref="ArgumentNullException">if <paramref name="data"/> is <c>null</c>.</exception>
    new (data: PixVolume, [<Optional; DefaultParameterValue(true)>] wantMipMaps: bool) =
        let flags = if wantMipMaps then TextureParams.WantMipMaps else TextureParams.None
        PixTexture3d(data, flags)

    override this.GetHashCode() =
        HashCode.Combine(this.PixVolume.GetHashCode(), this.TextureParams.GetHashCode())

    member inline private this.Equals(other: PixTexture3d) =
        this.PixVolume = other.PixVolume && this.TextureParams = other.TextureParams

    override this.Equals(obj: obj) =
        match obj with
        | :? PixTexture3d as other -> this.Equals(other)
        | _ -> false

    interface IEquatable<PixTexture3d> with
        member this.Equals(other) = this.Equals(other)

    interface ITexture with
        member this.WantMipMaps = this.TextureParams.HasFlag TextureParams.WantMipMaps