namespace Aardvark.Rendering

open Aardvark.Base

open System
open System.IO
open System.Runtime.InteropServices

/// <summary>
/// Texture with data loaded from an image stream.
/// The image stream is not opened and read until the texture is used and prepared.
/// </summary>
type StreamTexture =
    val private openStream : Func<unit, Stream>

    /// Flags controlling texture creation and upload.
    val TextureParams   : TextureParams

    /// <summary>
    /// Image loader to use, or <c>null</c> if no specific loader is to be used.
    /// </summary>
    val PreferredLoader : IPixLoader

    /// <summary>
    /// Creates a new <see cref="StreamTexture"/> instance.
    /// </summary>
    /// <remarks>
    /// The image stream is not opened and read until the texture is used and prepared.
    /// </remarks>
    /// <param name="openStream">Returns the stream to read from; result is owned by the caller.</param>
    /// <param name="textureParams">Flags controlling texture creation and upload.</param>
    /// <param name="loader">Image loader to use, or <c>null</c> if no specific loader is to be used.</param>
    /// <exception cref="ArgumentNullException">if <paramref name="openStream"/> is <c>null</c>.</exception>
    new (openStream: Func<unit, Stream>, textureParams: TextureParams, [<Optional; DefaultParameterValue(null : IPixLoader)>] loader: IPixLoader) =
        if isNull openStream then raise <| ArgumentNullException(nameof openStream)
        { openStream = openStream; TextureParams = textureParams; PreferredLoader = loader }

    /// <summary>
    /// Creates a new <see cref="StreamTexture"/> instance.
    /// </summary>
    /// <remarks>
    /// The image stream is not opened and read until the texture is used and prepared.
    /// </remarks>
    /// <param name="openStream">Returns the stream to read from; result is owned by the caller.</param>
    /// <param name="wantMipMaps">If true, a mipmap chain is loaded or generated; if false, only the base level is uploaded.</param>
    /// <param name="loader">Image loader to use, or <c>null</c> if no specific loader is to be used.</param>
    /// <exception cref="ArgumentNullException">if <paramref name="openStream"/> is <c>null</c>.</exception>
    new (openStream: Func<unit, Stream>,
         [<Optional; DefaultParameterValue(true)>] wantMipMaps: bool,
         [<Optional; DefaultParameterValue(null : IPixLoader)>] loader: IPixLoader) =
        let flags = if wantMipMaps then TextureParams.WantMipMaps else TextureParams.None
        StreamTexture(openStream, flags, loader)

    /// <summary>
    /// Opens and returns the associated stream.
    /// If <paramref name="seekable"/> is true and the stream is not seekable, it is copied to a <see cref="MemoryStream"/> prior to being returned.
    /// </summary>
    /// <param name="seekable">Indicates whether the returned stream must be seekable.</param>
    member this.Open([<Optional; DefaultParameterValue(false)>] seekable: bool) =
        let stream = this.openStream.Invoke()
        if stream.CanSeek || not seekable then stream
        else
            try
                let temp = new MemoryStream()
                stream.CopyTo(temp)
                temp :> Stream
            finally
                stream.Dispose()

    override this.GetHashCode() =
        let loaderHash = if notNull this.PreferredLoader then this.PreferredLoader.GetHashCode() else 0
        HashCode.Combine(this.openStream.GetHashCode(), this.TextureParams.GetHashCode(), loaderHash)

    member inline private this.Equals(other: StreamTexture) =
        this.openStream = other.openStream && this.TextureParams = other.TextureParams && this.PreferredLoader = other.PreferredLoader

    override this.Equals(obj: obj) =
        match obj with
        | :? StreamTexture as other -> this.Equals(other)
        | _ -> false

    interface IEquatable<StreamTexture> with
        member this.Equals(other) = this.Equals(other)

    interface ITexture with
        member this.WantMipMaps = this.TextureParams.HasFlag TextureParams.WantMipMaps