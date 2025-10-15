namespace Aardvark.Rendering

open System
open System.Runtime.InteropServices
open Aardvark.Base

/// <summary>
/// 2D texture with data stored in a <see cref="PixImageMipMap"/>.
/// The image data is not uploaded until the texture is used and prepared.
/// </summary>
type PixTexture2d =

    /// Image data.
    val PixImageMipMap : PixImageMipMap

    /// Flags controlling texture creation and upload.
    val TextureParams : TextureParams

    /// <summary>
    /// Creates a new <see cref="PixTexture2d"/> instance from a <see cref="PixImageMipMap"/>.
    /// </summary>
    /// <remarks>
    /// The image data is not uploaded until the texture is used and prepared.
    /// </remarks>
    /// <param name="data">Image mipmap chain.</param>
    /// <param name="textureParams">Flags controlling texture creation and upload.</param>
    /// <exception cref="ArgumentNullException">if <paramref name="data"/> is <c>null</c>.</exception>
    new (data: PixImageMipMap, textureParams: TextureParams) =
        if isNull data then raise <| ArgumentNullException(nameof data)
        { PixImageMipMap = data; TextureParams = textureParams }

    /// <summary>
    /// Creates a new <see cref="PixTexture2d"/> instance from a <see cref="PixImageMipMap"/>.
    /// </summary>
    /// <remarks>
    /// The image data is not uploaded until the texture is used and prepared.
    /// </remarks>
    /// <param name="data">Image mipmap chain.</param>
    /// <param name="wantMipMaps">If true, the whole mipmap chain is uploaded and missing levels are generated; if false, only the base level is uploaded without generating the other levels.</param>
    /// <exception cref="ArgumentNullException">if <paramref name="data"/> is <c>null</c>.</exception>
    new (data: PixImageMipMap, [<Optional; DefaultParameterValue(true)>] wantMipMaps : bool) =
        let flags = if wantMipMaps then TextureParams.WantMipMaps else TextureParams.None
        PixTexture2d(data, flags)

    /// <summary>
    /// Creates a new <see cref="PixTexture2d"/> instance from a <see cref="PixImage"/>.
    /// </summary>
    /// <remarks>
    /// The image data is not uploaded until the texture is used and prepared.
    /// </remarks>
    /// <param name="data">Image data.</param>
    /// <param name="textureParams">Flags controlling texture creation and upload.</param>
    /// <exception cref="ArgumentNullException">if <paramref name="data"/> is <c>null</c>.</exception>
    new (data: PixImage, textureParams: TextureParams) =
        if isNull data then raise <| ArgumentNullException(nameof data)
        PixTexture2d(PixImageMipMap(data), textureParams)

    /// <summary>
    /// Creates a new <see cref="PixTexture2d"/> instance from a <see cref="PixImage"/>.
    /// </summary>
    /// <remarks>
    /// The image data is not uploaded until the texture is used and prepared.
    /// </remarks>
    /// <param name="data">Image data.</param>
    /// <param name="wantMipMaps">If true, a mipmap chain is generated after upload; if false, no mipmap chain is generated.</param>
    /// <exception cref="ArgumentNullException">if <paramref name="data"/> is <c>null</c>.</exception>
    new (data: PixImage, [<Optional; DefaultParameterValue(true)>] wantMipMaps: bool) =
        let flags = if wantMipMaps then TextureParams.WantMipMaps else TextureParams.None
        PixTexture2d(data, flags)

    override this.GetHashCode() =
        HashCode.Combine(this.PixImageMipMap.GetHashCode(), this.TextureParams.GetHashCode())

    member inline private this.Equals(other: PixTexture2d) =
        this.PixImageMipMap = other.PixImageMipMap && this.TextureParams = other.TextureParams

    override this.Equals(obj: obj) =
        match obj with
        | :? PixTexture2d as other -> this.Equals(other)
        | _ -> false

    interface IEquatable<PixTexture2d> with
        member this.Equals(other) = this.Equals(other)

    interface ITexture with
        member this.WantMipMaps = this.TextureParams.HasFlag TextureParams.WantMipMaps