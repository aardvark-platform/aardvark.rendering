namespace Aardvark.Rendering

open System
open System.IO
open System.Runtime.InteropServices
open Aardvark.Base

/// <summary>
/// Texture with data loaded from an image file.
/// The image file is not opened and read until the texture is used and prepared.
/// </summary>
type FileTexture =
    inherit StreamTexture

    /// Path of the file to be loaded.
    val Path : string

    /// <summary>
    /// Creates a new <see cref="FileTexture"/> instance.
    /// </summary>
    /// <remarks>
    /// The image file is not opened and read until the texture is used and prepared.
    /// </remarks>
    /// <param name="path">Path of the file to be loaded.</param>
    /// <param name="textureParams">Flags controlling texture creation and upload.</param>
    /// <param name="loader">Image loader to use, or <c>null</c> if no specific loader is to be used.</param>
    /// <exception cref="ArgumentNullException">if <paramref name="path"/> is <c>null</c>.</exception>
    new (path: string, textureParams: TextureParams, [<Optional; DefaultParameterValue(null: IPixLoader)>] loader: IPixLoader) =
        if isNull path then raise <| ArgumentNullException(nameof path)
        let openStream() = File.OpenRead(path) :> Stream
        { inherit StreamTexture(openStream, textureParams, loader); Path = path }

    /// <summary>
    /// Creates a new <see cref="FileTexture"/> instance.
    /// </summary>
    /// <remarks>
    /// The image file is not opened and read until the texture is used and prepared.
    /// </remarks>
    /// <param name="path">Path of the file to be loaded.</param>
    /// <param name="wantMipMaps">If true, a mipmap chain is loaded or generated; if false, only the base level is uploaded.</param>
    /// <param name="loader">Image loader to use, or <c>null</c> if no specific loader is to be used.</param>
    /// <exception cref="ArgumentNullException">if <paramref name="path"/> is <c>null</c>.</exception>
    new (path: string,
         [<Optional; DefaultParameterValue(true)>] wantMipMaps: bool,
         [<Optional; DefaultParameterValue(null : IPixLoader)>] loader: IPixLoader) =
        let flags = if wantMipMaps then TextureParams.WantMipMaps else TextureParams.None
        FileTexture(path, flags, loader)

    override this.GetHashCode() =
        let loaderHash = if notNull this.PreferredLoader then this.PreferredLoader.GetHashCode() else 0
        HashCode.Combine(this.Path.GetHashCode(), this.TextureParams.GetHashCode(), loaderHash)

    member inline private this.Equals(other: FileTexture) =
        this.Path = other.Path && this.TextureParams = other.TextureParams && this.PreferredLoader = other.PreferredLoader

    override this.Equals(obj: obj) =
        match obj with
        | :? FileTexture as other -> this.Equals(other)
        | _ -> false

    interface IEquatable<FileTexture> with
        member this.Equals(other) = this.Equals(other)