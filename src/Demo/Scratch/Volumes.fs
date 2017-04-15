namespace Scratch

open System
open System.IO
open System.IO.MemoryMappedFiles
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Rendering
open Microsoft.FSharp.NativeInterop

#nowarn "9"

type IVolumeInputFile<'a when 'a : unmanaged> =
    inherit IDisposable
    abstract member Size : V3i
    abstract member GetSlice : int -> PixImage<'a>
    abstract member GetNativeTensor : unit -> NativeTensor4<'a>
    abstract member GetTensor : unit -> Tensor4<'a>

module VolumeInputFile =
    type private RawVolume<'a when 'a : unmanaged>(handle : MemoryMappedFile, size : V3i) =
        let mutable isOpen = true
        static let sa = sizeof<'a> |> nativeint
    
        let view = handle.CreateViewAccessor()
        let ptr = view.SafeMemoryMappedViewHandle.DangerousGetHandle()

        let sliceSize = nativeint size.X * nativeint size.Y * sa
    
        member x.Size = size

        member x.GetSlice(z : int, ?level : int) =
            if not isOpen then raise <| ObjectDisposedException("RawVolume")
            let level = defaultArg level 0
            let offset = sliceSize * nativeint z
            let pi = PixImage<'a>(Col.Format.Gray, size.XY, 1L)
            let gc = GCHandle.Alloc(pi.Volume.Data, GCHandleType.Pinned)
            try 
                Marshal.Copy(ptr + offset, gc.AddrOfPinnedObject(), sliceSize)
                pi
            finally 
                gc.Free()

        member x.GetNativeTensor() =
            let info =
                Tensor4Info(
                    0L,
                    V4l(size.X, size.Y, size.Z, 1),
                    V4l(1L, int64 size.X, int64 size.X * int64 size.Y, 1L)
                )
            NativeTensor4<'a>(NativePtr.ofNativeInt ptr, info)
            
        member x.GetTensor() =
            let res = Tensor4<'a>(V4l(size.X, size.Y, size.Z, 1))
            let gc = GCHandle.Alloc(res.Data, GCHandleType.Pinned)
            try
                Marshal.Copy(ptr, gc.AddrOfPinnedObject(), sliceSize * nativeint size.Z)
                res
            finally
                gc.Free()


        member x.Dispose() =
            if isOpen then
                isOpen <- false
                view.Dispose()
                handle.Dispose()

        static member Open(file : string, size : V3i) =
            if File.Exists file then
                let info = FileInfo(file)
                let expectedSize = int64 size.X * int64 size.Y * int64 size.Z * int64 sizeof<'a>

                if info.Length = expectedSize then
                    let handle = MemoryMappedFile.CreateFromFile(file, FileMode.Open, Guid.NewGuid() |> string, info.Length, MemoryMappedFileAccess.ReadWrite)
                    new RawVolume<'a>(handle, size) :> IVolumeInputFile<'a>
                else
                    failwithf "[VolumeFile] unexpected file-size %d (expected %d)" info.Length expectedSize
                
            else
                failwithf "[VolumeFile] cannot open %A" file


        interface IDisposable with
            member x.Dispose() = x.Dispose()

        interface IVolumeInputFile<'a> with
            member x.GetNativeTensor() = x.GetNativeTensor()
            member x.GetSlice z = x.GetSlice z
            member x.Size = x.Size
            member x.GetTensor() = x.GetTensor()

    let openRaw<'a when 'a : unmanaged> (size : V3i) (file : string) =
        RawVolume<'a>.Open(file, size)

type IVolumeFile<'a when 'a : unmanaged> =
    inherit IVolumeInputFile<'a>
    abstract member SetSlice : int * PixImage<'a> -> unit

type IVolumeStore<'a when 'a : unmanaged> =
    inherit IDisposable
    abstract member Size : V3i
    abstract member MipMapLevels : int
    abstract member GetSlice : z : int * level : int -> PixImage<'a>
    abstract member SetSlice : z : int * level : int * img : PixImage<'a> -> unit
    abstract member GetLevel : level : int -> IVolumeFile<'a>
    abstract member Min : uint16 with get, set
    abstract member Max : uint16 with get, set

module VolumeStore =  
    
    let private magic = Guid("8fb6dbd5-1a2f-4de3-a3dd-22ed0cae331f")


    [<StructLayout(LayoutKind.Sequential, Size = 128)>]
    type RawStoreHeader =
        struct
            val mutable public Magic : Guid
            val mutable public Size : V3i
            val mutable public Format : TextureFormat
            val mutable public Min : uint16
            val mutable public Max : uint16
            new(size, format) = { Magic = magic; Size = size; Format = format; Min = 0us; Max = 65535us }
        end

    type RawStore<'a when 'a : unmanaged>(handle : MemoryMappedFile, view : MemoryMappedViewAccessor, ptr : nativeint, size : V3i) =
        static let sa = sizeof<'a> |> nativeint
        
        static let half (s : V3i) =
            V3i(max 1 (s.X / 2), max 1 (s.Y / 2), max 1 (s.Z / 2))

        static let rec fileSize (size : V3i) (mipMaps : bool) =
            let baseSize = int64 size.X * int64 size.Y * int64 size.Z * int64 sizeof<'a>
            if mipMaps && size.AnyGreater 1 then baseSize + fileSize (half size) true
            else int64 sizeof<RawStoreHeader> + baseSize

        let mutable isOpen = true

        let levels = 1 + int(floor(Fun.Log2 (min size.X (min size.Y size.Z))))
        


        let getLevelPtrAndSize (level : int) =
            let rec offsetPtr (ptr : nativeint) (size : V3i) (l : int) =
                if l = 0 then 
                    ptr,size
                else
                    let total = nativeint size.X * nativeint size.Y * nativeint size.Z * sa
                    offsetPtr (ptr + total) (half size) (l - 1)

            offsetPtr ptr size level

        member x.Size = size
        member x.MipMapLevels = levels

        member x.GetSlice(z : int, level : int) =
            if not isOpen then raise <| ObjectDisposedException("RawStore")

            if level < 0 || level >= levels then 
                raise <| IndexOutOfRangeException()

            let ptr, size = getLevelPtrAndSize level

            if z < 0 || z >= size.Z then
                raise <| IndexOutOfRangeException()

            let sliceSize = nativeint size.X * nativeint size.Y * sa
            let offset = sliceSize * nativeint z

            let pi = PixImage<'a>(Col.Format.Gray, size.XY, 1L)
            let gc = GCHandle.Alloc(pi.Volume.Data, GCHandleType.Pinned)
            try 
                Marshal.Copy(ptr + offset, gc.AddrOfPinnedObject(), sliceSize)
                pi
            finally 
                gc.Free()

        member x.SetSlice(z : int, level : int, image : PixImage<'a>) =
            if not isOpen then raise <| ObjectDisposedException("RawStore")
            if level < 0 || level >= levels then 
                raise <| IndexOutOfRangeException()

            let ptr, size = getLevelPtrAndSize level
            if z < 0 || z >= size.Z then
                raise <| IndexOutOfRangeException()

            let image = image.ToCanonicalDenseLayout() |> unbox<PixImage<'a>>
            if image.Size <> size.XY then
                failwithf "[VolumeStore] invalid slice size: %A (expected %A)" image.Size size.XY
                
            let sliceSize = nativeint size.X * nativeint size.Y * sa
            let offset = sliceSize * nativeint z

            let gc = GCHandle.Alloc(image.Volume.Data, GCHandleType.Pinned)
            try  Marshal.Copy(gc.AddrOfPinnedObject(), ptr + offset, sliceSize)
            finally gc.Free()

        member x.Min 
            with get() =
                let pHeader = NativePtr.ofNativeInt (ptr - nativeint sizeof<RawStoreHeader>)
                let mutable header : RawStoreHeader = NativePtr.read pHeader
                header.Min
            and set v =
                let pHeader = NativePtr.ofNativeInt (ptr - nativeint sizeof<RawStoreHeader>)
                let mutable header : RawStoreHeader = NativePtr.read pHeader
                header.Min <- v
                NativePtr.write pHeader header
                

        member x.Max 
            with get() =
                let pHeader = NativePtr.ofNativeInt (ptr - nativeint sizeof<RawStoreHeader>)
                let mutable header : RawStoreHeader = NativePtr.read pHeader
                header.Max
            and set v =
                let pHeader = NativePtr.ofNativeInt (ptr - nativeint sizeof<RawStoreHeader>)
                let mutable header : RawStoreHeader = NativePtr.read pHeader
                header.Max <- v
                NativePtr.write pHeader header
    
        member x.GetNativeTensor(level : int) =
            if not isOpen then raise <| ObjectDisposedException("RawStore")
            if level < 0 || level >= levels then 
                raise <| IndexOutOfRangeException()

            let ptr, size = getLevelPtrAndSize level
            let info =
                Tensor4Info(
                    0L,
                    V4l(size.X, size.Y, size.Z, 1),
                    V4l(1L, int64 size.X, int64 size.X * int64 size.Y, 1L)
                )

            NativeTensor4<'a>(NativePtr.ofNativeInt ptr, info)
            
        member x.GetTensor(level : int) =
            if not isOpen then raise <| ObjectDisposedException("RawStore")
            if level < 0 || level >= levels then 
                raise <| IndexOutOfRangeException()

            let ptr, size = getLevelPtrAndSize level
            let res = Tensor4<'a>(V4l(size.X, size.Y, size.Z, 1))
            let gc = GCHandle.Alloc(res.Data, GCHandleType.Pinned)
            try
                Marshal.Copy(ptr, gc.AddrOfPinnedObject(), sa * nativeint size.X * nativeint size.Y * nativeint size.Z)
                res
            finally
                gc.Free()

        member x.GetLevel(level : int) =
            if not isOpen then raise <| ObjectDisposedException("RawStore")

            if level < 0 || level >= levels then 
                raise <| IndexOutOfRangeException()

            let _, size = getLevelPtrAndSize level

            { new IVolumeFile<'a> with
                member __.Dispose() = ()
                member __.Size = size
                member __.GetSlice(z) = x.GetSlice(z, level)
                member __.SetSlice(z,i) = x.SetSlice(z, level, i)
                member __.GetNativeTensor() = x.GetNativeTensor(level)
                member __.GetTensor() = x.GetTensor level
            }

        member x.Dispose() =
            if isOpen then
                isOpen <- false
                view.Dispose()
                handle.Dispose()

        static member CreateNew(file : string, size : V3i) =
            if File.Exists file then
                failwithf "[VolumeStore] cannot overwrite file %A" file

            let capacity = fileSize size true
            let handle = MemoryMappedFile.CreateFromFile(file, FileMode.CreateNew, Guid.NewGuid() |> string, capacity, MemoryMappedFileAccess.ReadWrite)
            let view = handle.CreateViewAccessor()


            let ptr = view.SafeMemoryMappedViewHandle.DangerousGetHandle()
            let format = TextureFormat.ofPixFormat (PixFormat(typeof<'a>, Col.Format.Gray)) TextureParams.empty
            let header = RawStoreHeader(size, format)
            NativePtr.write (NativePtr.ofNativeInt ptr) header

            new RawStore<'a>(handle, view, ptr + nativeint sizeof<RawStoreHeader>, size) :> IVolumeStore<_>

        static member Open(file : string) =
            if not (File.Exists file) then
                failwithf "[VolumeStore] cannot open file %A" file

            let info = FileInfo(file)
            if info.Length >= 128L then
                let handle = MemoryMappedFile.CreateFromFile(file, FileMode.Open)
                let view = handle.CreateViewAccessor()
                let ptr = view.SafeMemoryMappedViewHandle.DangerousGetHandle()
                let header : RawStoreHeader = NativePtr.read (NativePtr.ofNativeInt ptr)
                if header.Magic <> magic then
                    view.Dispose()
                    handle.Dispose()
                    failwithf "[VolumeStore] %A is not a store file" file
                

                new RawStore<'a>(handle, view, ptr + nativeint sizeof<RawStoreHeader>, header.Size) :> IVolumeStore<_>
            else
                failwithf "[VolumeStore] unexpected size %A (expected >= 128)" info.Length

        interface IDisposable with
            member x.Dispose() = x.Dispose()

        interface IVolumeStore<'a> with
            member x.GetSlice(z,l) = x.GetSlice(z,l)
            member x.SetSlice(z,l,i) = x.SetSlice(z,l,i)
            member x.Size = x.Size
            member x.MipMapLevels = x.MipMapLevels
            member x.GetLevel l = x.GetLevel l
            member x.Min 
                with get() = x.Min
                and set v = x.Min <- v

            member x.Max 
                with get() = x.Max
                and set v = x.Max <- v

    let createFile<'a when 'a : unmanaged> (size : V3i) (file : string) =
        RawStore<'a>.CreateNew(file, size)

    let openFile<'a when 'a : unmanaged> (file : string) =
        RawStore<'a>.Open(file)

       
    let generateMipMaps (dstFile : string) (src : IVolumeInputFile<uint16>) =
        let alignedSize = V3i(Fun.NextPowerOfTwo src.Size.X, Fun.NextPowerOfTwo src.Size.Y, Fun.NextPowerOfTwo src.Size.Z)
        if File.Exists dstFile then File.Delete dstFile
        let dst = RawStore.CreateNew(dstFile, alignedSize)



        if src.Size.AnyGreater dst.Size then
            failwithf "[VolumeStore] cannot generate MipMaps (size mismatch: %A > %A)" src.Size dst.Size

        let aggregate (l : PixImage<uint16>) (r : PixImage<uint16>) : PixImage<uint16> =
            if l.Size <> r.Size then failwith "[VolumeStore] mismatching sizes"

            let s = V2i(max 1 (l.Size.X / 2), max 1 (l.Size.Y / 2))
            let res = PixImage<uint16>(Col.Format.Gray, s, 1L)


            let interpolate (t : double) (l : uint16) (r : uint16) : uint16 =
                uint16 ((1.0 - t) * float l + t * float r)

            let ll = Matrix<uint16>(s)
            ll.SetScaledLinear(l.GetChannel(0L), interpolate, interpolate)
            let rr = Matrix<uint16>(s)
            rr.SetScaledLinear(r.GetChannel(0L), interpolate, interpolate)
            res.GetChannel(0L).SetMap2(ll, rr, fun (vl : uint16) (vr : uint16) -> uint16 ((uint32 vl + uint32 vr) / 2u)) |> ignore


            res

        let withBorder (newSize : V2i) (img : PixImage<uint16>) =
            if img.Size = newSize then 
                img
            else
                let res = PixImage<uint16>(img.Format, newSize)
                let d = (newSize - img.Size) / 2
                res.SubImage(d, img.Size).Set(img) |> ignore
                res

        let targetZ = (dst.Size.Z - src.Size.Z) / 2

        let zero = PixImage<uint16>(Col.Format.Gray, dst.Size.XY)

        Log.startTimed "copy level 0"
        // set leading empty slices to zero
        for z in 0 .. targetZ - 1 do
            dst.SetSlice(z, 0, zero)
            
        let mutable vMin = 65355us
        let mutable vMax = 0us

        // copy the level 0 images
        let mutable dz = targetZ
        for z in 0 .. 1 .. src.Size.Z - 1 do
            let a = src.GetSlice(z)

            let m = a.GetChannel 0L
            m.ForeachIndex(fun i ->
                let v = m.[i]
                vMin <- min v vMin
                vMax <- max v vMax
            ) |> ignore

            let a = a |> withBorder dst.Size.XY
            dst.SetSlice(dz, 0, a)
            dz <- dz + 1

        // set trailing empty slices to zero
        for z in targetZ + src.Size.Z .. dst.Size.Z - 1 do
            dst.SetSlice(z, 0, zero)

        Log.stop()

        let src = ()
        let zero = ()
        

        for dstLevel in 1 .. dst.MipMapLevels-1 do
            Log.startTimed "generate level %d" dstLevel
            let srcLevel = dstLevel - 1
            let src = dst.GetLevel srcLevel
            let dst = dst.GetLevel dstLevel
            
            for z in 0 .. 2 .. src.Size.Z - 2 do
                let a = src.GetSlice(z)
                let b = src.GetSlice(z + 1)
                let next = aggregate a b
                dst.SetSlice(z/2, next)
            Log.stop()

        dst.Min <- vMin
        dst.Max <- vMax
        dst.Dispose()
        RawStore.Open(dstFile)




module VolumeTest =
    open Aardvark.Base.Incremental
    open Aardvark.SceneGraph
    open Aardvark.Application
    open Aardvark.Application.WinForms
    open Aardvark.Rendering.GL


    [<ReflectedDefinition>]
    module Shader =
        open FShade

        let volumeTexture =
            sampler3d {
                texture uniform?VolumeTexture
                filter Filter.MinMagLinearMipPoint
                addressU WrapMode.Clamp
                addressV WrapMode.Clamp
                addressW WrapMode.Clamp
            }

        let pickRay (p : V2d) =
            let pn = uniform.ViewProjTrafoInv * V4d(p.X, p.Y, 0.0, 1.0)
            let nearPlanePoint = pn.XYZ / pn.W
            Vec.normalize nearPlanePoint

        type Vertex =
            {
                [<Position>]
                pos : V4d

                [<Semantic("RayDirection")>]
                dir : V3d

                [<Semantic("CubeCoord")>]
                cubeCoord : V3d

            }

        let hsv2rgb (h : float) (s : float) (v : float) =
            let s = clamp 0.0 1.0 s
            let v = clamp 0.0 1.0 v

            let h = h % 360.0
            let h = if h < 0.0 then h + 360.0 else h
            let hi = floor ( h / 60.0 ) |> int
            let f = h / 60.0 - float hi
            let p = v * (1.0 - s)
            let q = v * (1.0 - s * f)
            let t = v * (1.0 - s * ( 1.0 - f ))
            match hi with
                | 1 -> V3d(q,v,p)
                | 2 -> V3d(p,v,t)
                | 3 -> V3d(p,q,v)
                | 4 -> V3d(t,p,v)
                | 5 -> V3d(v,p,q)
                | _ -> V3d(v,t,p)

        let vertex (v : Vertex) =
            vertex {
                let cameraInModel = uniform.ModelTrafoInv.TransformPos uniform.CameraLocation
                let wp = uniform.ModelTrafo * v.pos
                return {
                    pos = uniform.ViewProjTrafo * wp
                    dir = v.pos.XYZ - cameraInModel
                    cubeCoord = v.pos.XYZ
                }
            }

        let fragment (v : Vertex) =
            fragment {
                let size = volumeTexture.Size / 2
                
                let dir = -Vec.normalize (v.dir * V3d size)
                let dt = V3d.III / V3d size
                let absDir = V3d(abs dir.X , abs dir.Y, abs dir.Z)


                let step =
                    if absDir.X > absDir.Y then
                        if absDir.X > absDir.Z then dt * dir / absDir.X
                        else dt * dir / abs dir.Z
                    else
                        if absDir.Y > absDir.Z then dt.Y * dir / absDir.Y
                        else dt * dir / absDir.Z
                        
                let mutable sampleLocation = v.cubeCoord
                let mutable value = 0.0
                
                let mutable steps = 0
                do
                    while sampleLocation.X >= 0.0 && sampleLocation.X <= 1.0 && 
                          sampleLocation.Y >= 0.0 && sampleLocation.Y <= 1.0 && 
                          sampleLocation.Z >= 0.0 && sampleLocation.Z <= 1.0 do

                        value <- value + 8.0 * volumeTexture.SampleLevel(sampleLocation, 1.0).X
                        sampleLocation <- sampleLocation + step
                        steps <- steps + 1
                let value = value / 1500.0 //(sampleLocation - v.cubeCoord).Length /// float steps
                //let l : float = uniform?MinValue
                //let h : float = uniform?MaxValue

                //let value = (value - 0.1) / (0.9) |> clamp 0.0 1.0

                return V4d(value, value, value, 1.0)
            }


    [<AutoOpen>]
    module ``GL Extensions`` =
        open OpenTK.Graphics.OpenGL4

        type SparseVolumeTexture<'a when 'a : unmanaged>(t : SparseTexture, data : IVolumeStore<'a>) =
            inherit SparseTexture(t.Context, t.Handle, t.Dimension, t.MipMapLevels, t.Multisamples, t.Size, t.Count, t.Format, t.PageSize, t.SparseLevels)

            member x.MakeResident(level : int) =
                use __ = x.Context.ResourceLock
                match ContextHandle.Current with
                    | Some v -> v.AttachDebugOutputIfNeeded()
                    | None -> Report.Warn("No active context handle in RenderTask.Run")
                GL.Enable EnableCap.DebugOutput

                x.Commit(level, Box3i(V3i.Zero, x.GetSize level - V3i.III))
                x.Upload(level, V3i.Zero, data.GetLevel(level).GetNativeTensor())


        type Context with
            member x.CreateSparseVolume(data : IVolumeStore<uint16>) =
                use __ = x.ResourceLock
                let t = x.CreateSparseTexture(data.Size, TextureFormat.R16, data.MipMapLevels)

                for l in t.SparseLevels .. data.MipMapLevels-1 do
                    t.Upload(l, V3i.Zero, data.GetLevel(l).GetNativeTensor())


                SparseVolumeTexture<uint16>(t, data)

    let run() =
        Ag.initialize()
        Aardvark.Init()

        use app = new OpenGlApplication()
        use win = app.CreateSimpleRenderWindow()

        let sliceFolder = @"C:\Users\Schorsch\Desktop\slices\"
        let inFile = @"C:\Users\Schorsch\Desktop\GussPK_AlSi_0.5Sn_180kV_1850x1850x1000px.raw" //@"C:\Users\Schorsch\Desktop\Testdatensatz_600x600x1000px.raw"
        let outFile = @"C:\Users\Schorsch\Desktop\GussPK_AlSi_0.5Sn_180kV.store"

        use store = 
            if File.Exists outFile then
                VolumeStore.openFile<uint16> outFile
            else
                let size = V3i(1850,1850,1000)
                use src = VolumeInputFile.openRaw<uint16> size inFile
                src |> VolumeStore.generateMipMaps outFile


//        let mutable level = 0
//        let mutable slice = 192
//        while level < store.MipMapLevels do
//            store.GetSlice(slice, level).SaveAsImage (Path.Combine(sliceFolder, sprintf "%d_%d.jpg" level slice))
//            level <- level + 1
//            slice <- slice / 2



        let size = V3d store.Size / float store.Size.NormMax


        let view = CameraView.lookAt (V3d(6,6,6)) V3d.Zero V3d.OOI
        let proj = win.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y))
        let view = view |> DefaultCameraController.control win.Mouse win.Keyboard win.Time


        let texture = app.Runtime.Context.CreateSparseVolume(store)
        //texture.MakeResident(0)
        for l in 1 .. 5 do
            texture.MakeResident(l)

//        let data = store.GetLevel(0)
//        let v = PixVolume<uint16>(Col.Format.Gray, data.GetTensor())

        //app.Runtime.Context.CreateTexture(NativeTe

        let sg = 
            Sg.box' C4b.Red (Box3d(-size, size))
                |> Sg.uniform "MinValue" (Mod.constant (float store.Min / 65535.0))
                |> Sg.uniform "MaxValue" (Mod.constant (float store.Max / 65535.0))
                |> Sg.uniform "VolumeTexture" (Mod.constant (texture :> ITexture))
                |> Sg.shader {
                    do! Shader.vertex
                    do! Shader.fragment
                   }
                |> Sg.cullMode (Mod.constant CullMode.CounterClockwise)
                |> Sg.viewTrafo (view |> Mod.map CameraView.viewTrafo)
                |> Sg.projTrafo (proj |> Mod.map Frustum.projTrafo)

        let task = app.Runtime.CompileRender(win.FramebufferSignature, sg)
        win.RenderTask <- task

        win.Run()
        ()

