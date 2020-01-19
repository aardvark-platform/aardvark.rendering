open Aardvark.Base
open Aardvark.Base.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open System.Runtime.InteropServices

#nowarn "9"

module ImageSharp =
    open SixLabors.ImageSharp
    open SixLabors.ImageSharp.Formats
    open SixLabors.ImageSharp.PixelFormats
    open SixLabors.ImageSharp.Advanced
    open Microsoft.FSharp.NativeInterop

    let load (path : string) =
        match Image.Load path with
        | :? Image<Rgba32> as img ->
            let res = PixImage<byte>(Col.Format.RGBA, V2i(img.Width, img.Height))
            let gc = GCHandle.Alloc(res.Volume.Data, GCHandleType.Pinned)
            try
                let ptr : nativeptr<Rgba32> = NativePtr.ofNativeInt (gc.AddrOfPinnedObject())
                img.GetPixelSpan().CopyTo(System.Span<Rgba32>(NativePtr.toVoidPtr ptr, res.Volume.Data.Length / 4))
                res :> PixImage
            finally
                gc.Free()
        | :? Image<Rgb24> as img ->
            let res = PixImage<byte>(Col.Format.RGB, V2i(img.Width, img.Height))
            let gc = GCHandle.Alloc(res.Volume.Data, GCHandleType.Pinned)
            try
                let ptr : nativeptr<Rgba32> = NativePtr.ofNativeInt (gc.AddrOfPinnedObject())
                img.GetPixelSpan().CopyTo(System.Span<Rgb24>(NativePtr.toVoidPtr ptr, res.Volume.Data.Length / 4))
                res :> PixImage
            finally
                gc.Free()
        | :? Image<PixelFormats.Alpha8> as img ->
            let res = PixImage<byte>(Col.Format.Gray, V2i(img.Width, img.Height))
            let gc = GCHandle.Alloc(res.Volume.Data, GCHandleType.Pinned)
            try
                let ptr : nativeptr<Rgba32> = NativePtr.ofNativeInt (gc.AddrOfPinnedObject())
                img.GetPixelSpan().CopyTo(System.Span<Alpha8>(NativePtr.toVoidPtr ptr, res.Volume.Data.Length / 3))
                res :> PixImage
            finally
                gc.Free()
        | :? Image<PixelFormats.Argb32> as img ->
            let res = PixImage<byte>(Col.Format.Gray, V2i(img.Width, img.Height))
            let gc = GCHandle.Alloc(res.Volume.Data, GCHandleType.Pinned)
            try
                let ptr : nativeptr<Rgba32> = NativePtr.ofNativeInt (gc.AddrOfPinnedObject())
                img.GetPixelSpan().CopyTo(System.Span<Argb32>(NativePtr.toVoidPtr ptr, res.Volume.Data.Length / 4))
                res :> PixImage
            finally
                gc.Free()
        | :? Image<PixelFormats.Rgba64> as img ->
            let res = PixImage<uint16>(Col.Format.RGBA, V2i(img.Width, img.Height))
            let gc = GCHandle.Alloc(res.Volume.Data, GCHandleType.Pinned)
            try
                let ptr : nativeptr<Rgba32> = NativePtr.ofNativeInt (gc.AddrOfPinnedObject())
                img.GetPixelSpan().CopyTo(System.Span<Rgba64>(NativePtr.toVoidPtr ptr, res.Volume.Data.Length / 4))
                res :> PixImage
            finally
                gc.Free()
        | _ ->
            failwith "not implemented"




[<EntryPoint>]
let main argv = 
    // first we need to initialize Aardvark's core components
    Ag.initialize()
    Aardvark.Init()

    let img = ImageSharp.load @"C:\Users\Schorsch\Desktop\images\e.jpg"

    //img.SaveAsImage @"C:\Users\Schorsch\Desktop\test.png"

    //System.Environment.Exit 0


    // lets define the bounds/color for our box
    // NOTE that the color is going to be ignored since we're using a texture
    let box = Box3d(-V3d.III, V3d.III)
    let color = C4b.Red

    let sg = 
        // thankfully aardvark defines a primitive box
        Sg.box (AVal.constant color) (AVal.constant box)

            // apply the texture as "DiffuseTexture"
            |> Sg.diffuseTexture' (PixTexture2d(PixImageMipMap [|img|], TextureParams.mipmapped))

            // apply a shader ...
            // * transforming all vertices
            // * looking up the DiffuseTexture 
            // * applying a simple lighting to the geometry (headlight)
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.diffuseTexture
                do! DefaultSurfaces.simpleLighting
            }
    

    // show the scene in a simple window
    show {
        backend Backend.GL
        display Display.Mono
        debug false
        samples 8
        scene sg
    }

    0
