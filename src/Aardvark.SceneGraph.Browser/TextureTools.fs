namespace Aardvark.SceneGraph

open Offler
open System
open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Rendering
open Microsoft.FSharp.NativeInterop
open System.Threading

#nowarn "9"

module internal BrowserTexture =


    module private Align =
        
        let prev (align : V2i) (v : V2i) =
            let x = 
                let mx = v.X % align.X
                if mx = 0 then v.X
                else v.X - mx
            let y =
                let my = v.Y % align.Y
                if my = 0 then v.Y
                else v.Y - my
            V2i(x,y)

                                            
        let next (align : V2i) (v : V2i) =
            let x = 
                let mx = v.X % align.X
                if mx = 0 then v.X
                else v.X - mx + align.X
            let y =
                let my = v.Y % align.Y
                if my = 0 then v.Y
                else v.Y - my + align.Y
            V2i(x,y)

    module Vulkan =
        open Aardvark.Rendering.Vulkan
        open Microsoft.FSharp.NativeInterop

        let create (runtime : Runtime) (client : Offler) (mipMaps : bool) =
            let device = runtime.Device
            
            let levels (size : V2i) =
                if mipMaps then 
                    Fun.MipmapLevels(size)
                else    
                    1

            let emptySub = { new IDisposable with member x.Dispose() = () }
            let mutable tex = Unchecked.defaultof<Image>
            let mutable running = false
            let mutable dirty = false
            let mutable sub = emptySub

            
            let allIndices = device.PhysicalDevice.QueueFamilies |> Array.map (fun f -> uint32 f.index)


            let createImage (textureDim : V3i) =
                use pAll = fixed allIndices
                let fmt = VkFormat.R8g8b8a8Unorm
                let levels = levels textureDim.XY
                use pInfo =
                    fixed [|
                        VkImageCreateInfo(
                            VkImageCreateFlags.None,
                            VkImageType.D2d, fmt,
                            VkExtent3D(textureDim.X, textureDim.Y, textureDim.Z),
                            uint32 levels, 1u, VkSampleCountFlags.D1Bit, VkImageTiling.Optimal, 
                            VkImageUsageFlags.TransferDstBit ||| VkImageUsageFlags.SampledBit,
                            VkSharingMode.Concurrent,
                            uint32 allIndices.Length,
                            pAll,
                            VkImageLayout.Preinitialized
                        )
                    |]

                let img = [| VkImage.Null |]
                use pImg = fixed img
                VkRaw.vkCreateImage(device.Handle, pInfo, NativePtr.zero, pImg)
                |> ignore
                let handle = img.[0]

                let reqs = [| VkMemoryRequirements() |]
                use pReqs = fixed reqs
                VkRaw.vkGetImageMemoryRequirements(device.Handle, handle, pReqs)
                let reqs = reqs.[0]

                let mem = device.DeviceMemory.AllocRaw(reqs.size)
                VkRaw.vkBindImageMemory(device.Handle, handle, mem.Handle, 0UL)
                |> ignore


                let img = new Image(device, handle, textureDim, 1, 1, 1, TextureDimension.Texture2D, fmt, mem, VkImageLayout.Preinitialized, VkImageLayout.General)
                device.perform{
                    do! Command.TransformLayout(img, VkImageLayout.General)
                }
                img



            let toDispose = System.Collections.Generic.List<Image>()
            let resource = 
                { new AdaptiveResource<ITexture>() with
                    member x.Create() =
                        running <- true

                        let thread =
                            startThread <| fun () ->
                                while running do
                                    lock emptySub (fun () ->
                                        while not dirty do
                                            Monitor.Wait emptySub |> ignore
                                        dirty <- false
                                    )
                                    if running then transact (fun () -> x.MarkOutdated())



                        tex <- createImage (V3i(client.Width, client.Height, 1))
                   
                        sub <- 
                            client.Subscribe (fun img ->
                                try
                                    if img.totalWidth <> tex.Size.X || img.totalHeight <> tex.Size.Y then
                                        // recreate
                                        let size = V3i(img.totalWidth, img.totalHeight, 1)
                                        let newImg = createImage size
                                        
                                        let temp = device.CreateTensorImage<byte>(size, Col.Format.RGBA, false)
                                        temp.Write(img.data, nativeint (size.X * 4), Col.Format.BGRA, ImageTrafo.Identity)
                                        device.CopyEngine.Enqueue [
                                            CopyCommand.Copy(temp, newImg.[TextureAspect.Color, 0, 0])
                                            CopyCommand.Callback temp.Dispose
                                        ]
                                        let o = tex
                                        tex <- newImg
                                        toDispose.Add o
                                    elif img.width = img.totalWidth && img.height = img.totalHeight then    
                                        // full update
                                        let size = V3i(img.totalWidth, img.totalHeight, 1)

                                        let temp = device.CreateTensorImage<byte>(size, Col.Format.RGBA, false)
                                        temp.Write(img.data, nativeint (size.X * 4), Col.Format.BGRA, ImageTrafo.Identity)
                                        device.CopyEngine.Enqueue [
                                            CopyCommand.Copy(temp, tex.[TextureAspect.Color, 0, 0])
                                            CopyCommand.Callback temp.Dispose
                                        ]
                                    else
                                        // partial update
                                        let size = V3i(img.width, img.height, 1)
                                        let gran = device.TransferFamily.Info.minImgTransferGranularity.XY

                                        let cMin = V2i(img.x, img.y)
                                        let copyOffset = cMin |> Align.prev gran
                                        let copySize = cMin + size.XY - copyOffset |> Align.next gran

                                        let temp = device.CreateTensorImage<byte>(V3i(copySize, 1), Col.Format.RGBA, false)

                                        NativeVolume.using client.LastImage.Volume (fun src ->
                                            let srcPart = src.SubVolume(V3i(copyOffset, 0), V3i(copySize, 4))
                                            temp.Write(Col.Format.BGRA, srcPart)
                                        )

                                        device.CopyEngine.Enqueue [
                                            CopyCommand.Copy(temp, tex.[TextureAspect.Color, 0, 0], V3i(copyOffset, 0), V3i(copySize, 1))
                                            CopyCommand.Callback temp.Dispose
                                        ]
                                with e ->
                                    Log.error "%A" e


                                if not x.OutOfDate then
                                    lock emptySub (fun () ->
                                        dirty <- true
                                        Monitor.PulseAll emptySub
                                    )
                                    //System.Threading.Tasks.Task.Factory.StartNew(fun () -> transact (fun () -> x.MarkOutdated())) |> ignore
                            )

                    member x.Destroy() =
                        if running then
                            running <- false
                            lock emptySub (fun () ->
                                dirty <- true
                                Monitor.PulseAll emptySub
                            )
                            sub.Dispose()
                            sub <- emptySub
                            tex.Dispose()
                            tex <- Unchecked.defaultof<_>

                    member x.Compute(at, rt) =
                        for img in toDispose do img.Dispose()
                        toDispose.Clear()
                        if mipMaps then
                            device.perform {
                                do! Command.GenerateMipMaps tex.[TextureAspect.Color]
                            }

                        tex :> ITexture
                } :> IAdaptiveResource<_>

            resource
      
    module GL =
        open Microsoft.FSharp.NativeInterop
        open System.Runtime.InteropServices
        open OpenTK.Graphics
        open OpenTK.Graphics.OpenGL4
        open Aardvark.Rendering.GL

        let create (runtime : Runtime) (client : Offler) (mipMaps : bool) =
            let ctx = runtime.Context
            let emptySub = { new IDisposable with member x.Dispose() = () }
            let mutable tex = Texture(ctx, 0, TextureDimension.Texture2D, 1, 1, V3i.III, None, TextureFormat.Rgba8, 0L)
            let mutable running = false
            let mutable dirty = false
            let mutable sub = emptySub
            let mutable pbo = 0

            let resource = 
                { new AdaptiveResource<ITexture>() with
                    member x.Create() =
                        running <- true

                        let check name =    
                            let err = GL.GetError()
                            if err <> ErrorCode.NoError then Log.warn "%s: %A" name err

                        let levels (size : V2i) =
                            if mipMaps then 
                                Fun.MipmapLevels(size)
                            else    
                                1

                        let createPBO(size : int) =
                            let p = GL.GenBuffer()
                            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, p)
                            check "BindBuffer"
                            GL.Dispatch.BufferStorage(BufferTarget.PixelUnpackBuffer, nativeint size, 0n, BufferStorageFlags.MapWriteBit)
                            check "BufferStorage"
                            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0)
                            check "BindBuffer"
                            p

                        let mapPBO (buffer : int) (size : int) (action : nativeint -> unit) =
                            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, buffer)
                            check "BindBuffer"
                            //let ptr = GL.Dispatch.MapNamedBufferRange(buffer, 0n, nativeint size, BufferAccessMask.MapInvalidateRangeBitExt ||| BufferAccessMask.MapWriteBit)
                            let ptr = GL.MapBufferRange(BufferTarget.PixelUnpackBuffer, 0n, nativeint size, BufferAccessMask.MapInvalidateBufferBit ||| BufferAccessMask.MapWriteBit)
                            check "MapBufferRange"
                            try action ptr
                            finally 
                            
                                GL.UnmapBuffer(BufferTarget.PixelUnpackBuffer) |> ignore
                                GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0)


                        let thread =
                            startThread <| fun () ->
                                while running do
                                    lock emptySub (fun () ->
                                        while not dirty do
                                            Monitor.Wait emptySub |> ignore
                                        dirty <- false
                                    )
                                    if running then transact (fun () -> x.MarkOutdated())

                        //using ctx.ResourceLock (fun _ ->
                        //    tex <- ctx.CreateTexture2D(V2i(client.Width, client.Height), 1, TextureFormat.Rgba8, 1)
                        //    pbo <- createPBO (4 * client.Width * client.Height)
                        //)

                        sub <- 
                            client.Subscribe (fun img ->
                                use __ = ctx.ResourceLock
                    
                                let size = V2i(img.width, img.height)
                                let bytes = 4 * size.X * size.Y
                            
                                if pbo = 0 || img.totalWidth <> tex.Size.X || img.totalHeight <> tex.Size.Y then
                                    // recreate
                                    let newImg = ctx.CreateTexture2D(size, levels (V2i(img.totalWidth, img.totalHeight)), TextureFormat.Rgba8, 1)
                                    let newPBO = createPBO bytes
                                
                                    mapPBO newPBO bytes (fun ptr -> Marshal.Copy(img.data, ptr, bytes))

                                    GL.BindBuffer(BufferTarget.PixelUnpackBuffer, newPBO)
                                    GL.BindTexture(TextureTarget.Texture2D, newImg.Handle)
                                    GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, size.X, size.Y, PixelFormat.Bgra, PixelType.UnsignedByte, 0n)
                                    GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0)
                                    GL.BindTexture(TextureTarget.Texture2D, 0)
                              
                                    if pbo <> 0 then
                                        ctx.Delete tex
                                        GL.Dispatch.UnmapNamedBuffer pbo |> ignore
                                        GL.DeleteBuffer pbo
                                    tex <- newImg
                                    pbo <- newPBO

                                elif img.width = img.totalWidth && img.height = img.totalHeight then  
                                    // full update
                                    mapPBO pbo bytes (fun ptr -> Marshal.Copy(img.data, ptr, bytes))

                                    GL.BindBuffer(BufferTarget.PixelUnpackBuffer, pbo)
                                    GL.BindTexture(TextureTarget.Texture2D, tex.Handle)
                                    GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, size.X, size.Y, PixelFormat.Bgra, PixelType.UnsignedByte, 0n)
                                    GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0)
                                    GL.BindTexture(TextureTarget.Texture2D, 0)

                                else
                                    // partial update
                                    mapPBO pbo bytes (fun ptr -> 
                                        let pDst = NativeVolume<byte>(NativePtr.ofNativeInt ptr, VolumeInfo(0L, V3l(size, 4), V3l(4, 4*size.X, 1)))
                                        NativeVolume.using client.LastImage.Volume (fun pFullSrc ->
                                            let pSrc = pFullSrc.SubVolume(V3i(img.x, img.y, 0), V3i(size.X, size.Y, 4))
                                            NativeVolume.copy pSrc pDst
                                        )
                                    )

                                    GL.BindBuffer(BufferTarget.PixelUnpackBuffer, pbo)
                                    GL.BindTexture(TextureTarget.Texture2D, tex.Handle)
                                    GL.TexSubImage2D(TextureTarget.Texture2D, 0, img.x, img.y, size.X, size.Y, PixelFormat.Bgra, PixelType.UnsignedByte, 0n)
                                    GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0)
                                    GL.BindTexture(TextureTarget.Texture2D, 0)

                                GL.BindTexture(TextureTarget.Texture2D, tex.Handle)
                                GL.GenerateMipmap(GenerateMipmapTarget.Texture2D)
                                GL.BindTexture(TextureTarget.Texture2D, 0)

                                if not x.OutOfDate then
                                    lock emptySub (fun () ->
                                        dirty <- true
                                        Monitor.PulseAll emptySub
                                    )
                                    //System.Threading.Tasks.Task.Factory.StartNew(fun () -> transact (fun () -> x.MarkOutdated())) |> ignore
                            )
                    member x.Destroy() =
                        if running then
                            running <- false
                            lock emptySub (fun () ->
                                dirty <- true
                                Monitor.PulseAll emptySub
                            )
                            sub.Dispose()
                            sub <- emptySub
                            if pbo <> 0 then
                                use __ = ctx.ResourceLock
                                ctx.Delete tex
                                GL.DeleteBuffer pbo
                                tex <- Texture(ctx, 0, TextureDimension.Texture2D, 1, 1, V3i.III, None, TextureFormat.Rgba8, 0L)
                    member x.Compute(at, rt) =
                        tex :> ITexture
                } :> IAdaptiveResource<_>

            resource

    let create (runtime : IRuntime) (browser : Offler) (mipMaps : bool) =
        match runtime with
        | :? Aardvark.Rendering.Vulkan.Runtime as r -> Vulkan.create r browser mipMaps
        | :? Aardvark.Rendering.GL.Runtime as r -> GL.create r browser mipMaps
        | _ -> failwithf "unexpected runtime: %A" runtime

