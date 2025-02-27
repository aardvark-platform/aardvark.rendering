﻿module Program

open System
open System.IO
open Rendering.Examples

open System.Runtime.InteropServices
open System.Diagnostics
open System.Threading
open Microsoft.FSharp.NativeInterop
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Management
open Aardvark.SceneGraph
open FSharp.Data.Traceable
open FSharp.Data.Adaptive

#nowarn "9"

        
open System.Threading.Tasks


let colorLockTest() =
    let l = ColoredLock<int>()
    
//    let mutable counts : int[] = Array.zeroCreate 2
//    let mutable maxCounts : int[] = Array.zeroCreate 2
//    let mutable exCount = 0
//    let mutable bad = false
//
//    let size = 1 <<< 13
//    let start = new ManualResetEventSlim(false)
//    let countDown = new CountdownEvent(size)
//
//    let rand = RandomSystem()
//    for _ in 1 .. size do
//        let a =
//            async {
//                do! Async.SwitchToNewThread()
//                let nested = rand.UniformDouble() > 0.9
//                let isColored = rand.UniformDouble() > 0.2
//                let color = rand.UniformInt(counts.Length)
//                start.Wait()
//                if isColored then
//                    if l.Status <> NotEntered then
//                        bad <- true
//
//                    l.Enter(color)
//                    let cnt = Interlocked.Increment(&counts.[color])
//
//                    if l.Status <> Colored color then
//                        bad <- true
//
//                    if cnt > maxCounts.[color] then
//                        maxCounts.[color] <- cnt
//
//                    if nested then
//                        l.Enter()
//                        if l.Status <> Exclusive then
//                            bad <- true
//                        l.Exit()
//                        if l.Status <> Colored color then
//                            bad <- true
//
//                    Thread.Sleep(0)
//                    Interlocked.Decrement(&counts.[color]) |> ignore
//                    l.Exit()
//
//                    if l.Status <> NotEntered then
//                        bad <- true
//
//                    countDown.Signal() |> ignore
//                else
//                    if l.Status <> NotEntered then
//                        bad <- true
//                    l.Enter()
//                    if l.Status <> Exclusive then
//                        bad <- true
//                    let c = Interlocked.Increment(&exCount)
//                    if c > 1 then bad <- true
//                    Thread.Sleep(0)
//                    Interlocked.Decrement(&exCount) |> ignore
//                    l.Exit()
//                    if l.Status <> NotEntered then
//                        bad <- true
//                    countDown.Signal() |> ignore
//            }
//        Async.Start a
//
//
//    start.Set()
//    countDown.Wait()
//    printfn "count: %A" maxCounts
//    printfn "bad:   %A" bad
//

    let check should =
        if l.Status <> should then printfn "error: %A vs %A" l.Status should
        else printfn "good: %A" should

    check NotEntered
    l.Enter(1)
    check (Colored 1)
    l.Enter(1)
    check (Colored 1)
    l.Enter(2)
    check (Colored 2)
    l.Exit()
    check (Colored 1)


    l.Enter()
    check (Exclusive)
    l.Enter()
    check (Exclusive)
    l.Exit()
    check (Exclusive)
    l.Exit()
    check (Colored 1)
    l.Exit()
    check (Colored 1)
    l.Exit()
    check NotEntered
    Environment.Exit 0

open Aardvark.Rendering.Interactive

module MemoryManagerTest =
    
    let testMem<'a> : Memory<'a[]> = 
        {
            malloc = fun s -> 
                Array.zeroCreate (int s)

            mfree = fun a s -> 
                ()

            mcopy = fun src srcOff dst dstOff size -> 
                Array.Copy(src, int64 srcOff, dst, int64 dstOff, int64 size)

            mrealloc = fun ptr o n -> 
                let mutable ptr = ptr
                Array.Resize(&ptr, int n)
                ptr
        }

    let run() =
        let manager = new MemoryManager<int[]>(testMem<int>, 16n)
        
        let a = manager.Alloc(8n, 5n)
        printfn "a: %A" a
        
        let b = manager.Alloc(8n, 13n)
        printfn "b: %A" b

        let c = manager.Alloc(8n, 10n)
        printfn "c: %A" c

        manager.Free b
        manager.Realloc(a, 8n, 100n)
        printfn "a: %A" a


        let d = manager.Alloc(8n, 13n)
        printfn "d: %A" d
        let e = manager.Alloc(8n, 3n)
        printfn "e: %A" e

        Environment.Exit 0
        ()


module AdaptiveResources9000 =
    let mutable currentResourceVersion = 0L

    [<StructuralEquality; StructuralComparison>]
    type ResourceVersion =
        struct
            val mutable private Value : int64

            static member Zero = ResourceVersion(0L)
            static member New = ResourceVersion(Interlocked.Increment(&currentResourceVersion))

            private new(v) = { Value = v }
        end

    type ResourceInfo(v : ResourceVersion, h : obj, l : HashSet<ILockedResource>) =

        static let empty = ResourceInfo(ResourceVersion.Zero, null, HashSet.empty)

        static member Empty = empty

        member x.Version = v
        member x.Handle = h
        member x.Locked = l

    type ResourceInfo<'h>(v : ResourceVersion, h : 'h, l : HashSet<ILockedResource>) =
        inherit ResourceInfo(v, h :> obj, l)
        member x.Handle = h

    type IResourceDescription<'a, 'h> =
        abstract member Create : 'a -> ResourceInfo<'h>
        abstract member Update : ResourceInfo<'h> * 'a -> ResourceInfo<'h>
        abstract member Delete : ResourceInfo<'h> -> unit


    type ResourceDescription<'a, 'h> =
        {
            create : 'a -> ResourceInfo<'h>
            update : 'a -> ResourceInfo<'h> -> ResourceInfo<'h>
            delete : ResourceInfo<'h> -> unit
        }


    type IResource =
        inherit IAdaptiveObject
        abstract member Force : AdaptiveToken -> ResourceInfo
    
    type IResource<'h> =
        inherit IResource
        abstract member Force : AdaptiveToken -> ResourceInfo<'h>

    type ResourceReference(set : ResourceSet, r : IResource) =
        let mutable refCount = 0
        let mutable currentInfo = ResourceInfo.Empty


        member x.Resource = r
        member x.Set = set
        member x.CurrentInfo = currentInfo

        member x.Update(token : AdaptiveToken) =
            let info = r.Force(token)
            currentInfo <- info
            info

        member internal x.Acquire() = Interlocked.Increment(&refCount) = 1
        member internal x.Release() = Interlocked.Decrement(&refCount) = 0

    and ResourceReference<'h>(set : ResourceSet, r : IResource<'h>) =
        inherit ResourceReference(set, r :> IResource)
        member x.Resource = r
        
        member x.Update(token : AdaptiveToken) =
            let info = base.Update(token) |> unbox<ResourceInfo<'h>>
            x.WriteHandle info.Handle
            info

        abstract member WriteHandle : 'h -> unit
        default x.WriteHandle _ = ()
            
    and ResourceReference<'h, 'n when 'n : unmanaged>(set : ResourceSet, r : IResource<'h>, view : 'h -> 'n) =
        inherit ResourceReference<'h>(set, r)

        let mutable ptr : nativeptr<'n> = NativePtr.alloc 1

        member x.Pointer = ptr

        override x.WriteHandle(h : 'h) : unit =
            NativePtr.write ptr (view h)

    and private ResourceStore =
        class
            val mutable public Reference : ResourceReference
            val mutable public ResourceInfo : ResourceInfo

            member x.UpdateInfo(info : ResourceInfo) =
                if info.Version > x.ResourceInfo.Version then
                    let delta = HashSet.computeDelta x.ResourceInfo.Locked info.Locked
                    x.ResourceInfo <- info
                    Some delta
                else
                    None

            new(r) = { Reference = r; ResourceInfo = r.CurrentInfo }
        end

    and ResourceSet() =
        inherit AdaptiveObject() 

        let refStore = System.Collections.Concurrent.ConcurrentDictionary<IResource, ResourceStore>()
        let dirty = System.Collections.Generic.HashSet<ResourceStore>()

        let tryGetStore(r : IResource) =
            match refStore.TryGetValue r with
                | (true, s) -> Some s
                | _ -> None

        let mutable lockedResources : CountingHashSet<ILockedResource> = CountingHashSet.empty

        let addDirty o = lock dirty (fun () -> dirty.Add o |> ignore)
        let remDirty o = lock dirty (fun () -> dirty.Remove o |> ignore)
        let consumeDirty() =
            lock dirty (fun () ->
                let arr = dirty |> Seq.toArray
                dirty.Clear()
                arr
            )

        let mutable maxVersion = ResourceVersion.New

        override x.InputChangedObject(_,o) =
            match o with
                | :? IResource as r ->
                    match tryGetStore r with
                        | Some s -> addDirty s
                        | None -> ()
                | _ ->
                    ()


        member x.Add(r : IResource<'h>, view : 'h -> 'n) : ResourceReference<'h, 'n> =
            lock x (fun () ->
                let store = refStore.GetOrAdd(r :> IResource, fun _ -> ResourceStore(ResourceReference<'h, 'n>(x, r, view))) 
                let ref = store.Reference |> unbox<ResourceReference<'h, 'n>>

                if ref.Acquire() then
                    lock r (fun () ->
                        if r.OutOfDate then
                            addDirty store
                            // mark x outofdate????
                        else
                            r.Outputs.Add(x) |> ignore
                            x.Level <- max x.Level (r.Level + 1)
                            maxVersion <- max maxVersion ref.CurrentInfo.Version
                            match store.UpdateInfo ref.CurrentInfo with
                                | Some deltas ->
                                    let (l', _) = lockedResources.ApplyDelta(deltas)
                                    lockedResources <- l'

                                | None ->
                                    Log.warn "strange case"

                    )
                    maxVersion <- ResourceVersion.New
            
                ref
            )

        member x.Add(r : IResource<'h>) : ResourceReference<'h> =
            x.Add(r, fun _ -> 0) :> ResourceReference<_>

        member x.Remove(r : IResource) =
            lock x (fun () ->
                match refStore.TryGetValue(r) with
                    | (true, store) ->
                        let res = store.Reference.Resource
                        if store.Reference.Release() then
                            lock res (fun () ->
                                res.Outputs.Remove x |> ignore
                                let deltas = HashSet.computeDelta store.ResourceInfo.Locked HashSet.empty
                                let (l', _) = lockedResources.ApplyDelta(deltas)
                                lockedResources <- l'
                                remDirty store |> ignore
                            )
                            refStore.TryRemove r |> ignore
                            maxVersion <- ResourceVersion.New
                    | _ ->
                        ()
            )

        member x.Use(token : AdaptiveToken, action : ResourceVersion -> 'r) =
            x.EvaluateAlways token (fun token ->
                let rec run() =
                    let dirty = consumeDirty()
                    if dirty.Length > 0 then
                        for store in dirty do
                            let info = store.Reference.Update(token)
                            maxVersion <- max maxVersion info.Version
                            match store.UpdateInfo info with
                                | Some deltas ->
                                    let (l', _) = lockedResources.ApplyDelta(deltas)
                                    lockedResources <- l'

                                | None ->
                                    Log.warn "strange case"

                        run()
                run()

                let mine = lockedResources
                for l in mine do l.Lock.Enter(ResourceUsage.Render, l.OnLock)
                try 
                    action maxVersion
                finally 
                    for l in mine do l.Lock.Exit(l.OnUnlock)
            )



module AdaptiveResourcesEager =
    open System.Collections.Generic
    open System.Linq
    open FSharp.Data.Adaptive


    let mutable private currentTime = 0L
    let private newTime() = Interlocked.Increment(&currentTime)

    type IRefCounted =
        abstract member Acquire : unit -> unit
        abstract member Release : unit -> unit

    type IResource =
        inherit IRefCounted
        abstract member AddOutput : IResource -> unit
        abstract member RemoveOutput : IResource -> unit
        abstract member Outputs : seq<IResource>
        abstract member Level : int
        abstract member Update : unit -> int64

    type IResource<'h> =
        inherit IResource
        abstract member Handle : 'h

    [<AbstractClass>]
    type AbstractResource<'h>(level : int) =
        let mutable refCount = 0

        let mutable updateTime = -1L
        let mutable currentHandle : Option<'h> = None
        let mutable outputs : HashSet<IResource> = HashSet.empty

        abstract member Create : unit -> 'h
        abstract member Update : 'h -> bool * 'h
        abstract member Destroy : 'h -> unit

        member x.Acquire() = 
            if Interlocked.Increment(&refCount) = 1 then
                updateTime <- newTime()
                ()

        member x.Release() = 
            if Interlocked.Decrement(&refCount) = 0 then
                match currentHandle with
                    | Some h -> 
                        x.Destroy h
                        currentHandle <- None
                        updateTime <- newTime()
                    | None ->
                        ()

        member x.Level = level

        member x.Outputs = outputs

        member x.Handle = 
            match currentHandle with
                | Some h -> h
                | _ -> failwith "[Resource] does not have a handle"

        member x.AddOutput (r : IResource) =
            assert(r.Level > x.Level)
            outputs <- HashSet.add r outputs

        member x.RemoveOutput (r : IResource) =
            assert(r.Level > x.Level)
            outputs <- HashSet.remove r outputs

        member x.Update() =
            lock x (fun () ->
                if refCount = 0 then failwith "[Resource] cannot update without reference"

                let (changed, handle) = 
                    match currentHandle with
                        | Some h -> x.Update(h)
                        | None -> true, x.Create()

                if changed then 
                    updateTime <- newTime()

                currentHandle <- Some handle
                updateTime
            )

        interface IRefCounted with
            member x.Acquire() = x.Acquire()
            member x.Release() = x.Release()

        interface IResource with
            member x.Level = x.Level
            member x.AddOutput o = x.AddOutput o
            member x.RemoveOutput o = x.RemoveOutput o
            member x.Outputs = x.Outputs :> seq<_>
            member x.Update() = x.Update()

        interface IResource<'h> with
            member x.Handle = x.Handle

    module Resource =
        let mapN (update : Option<'b> -> 'h[] -> bool * 'b) (destroy : 'b -> unit) (inputs : IResource<'h>[]) =
            let level = inputs |> Seq.map (fun r -> r.Level) |> Seq.max
            {
                new AbstractResource<'b>(1 + level) with
                    override x.Update(old) =
                        let handles = inputs |> Array.map (fun r -> r.Handle)
                        update (Some old) handles

                    override x.Create() =
                        for i in inputs do 
                            i.Acquire()
                            i.AddOutput x

                        let handles = inputs |> Array.map (fun r -> r.Handle)
                        let _, h = update None handles
                        h

                    override x.Destroy h =
                        for i in inputs do 
                            i.Release()
                            i.RemoveOutput x

                        destroy h

            } :> IResource<_>
        
        let map (update : Option<'b> -> 'h -> bool * 'b) (destroy : 'b -> unit) (input : IResource<'h>) =
            {
                new AbstractResource<'b>(1 + input.Level) with
                    override x.Update(old) =
                        update (Some old) input.Handle

                    override x.Create() =
                        input.Acquire()
                        input.AddOutput x
                        let _, h = update None input.Handle
                        h

                    override x.Destroy h =
                        input.Release()
                        input.RemoveOutput x
                        destroy h

            } :> IResource<_>

    type ResourceSet() =
        let all = ReferenceCountingSet<IResource>()

        let mutable maxTime = -1L

        let compareLevel =
            Func<IResource, IResource, int>(fun l r ->
                compare l.Level r.Level
            )

        member x.Add(r : IResource) =
            if all.Add r then
                r.Acquire()

        member x.Remove(r : IResource) =
            if all.Remove r then
                r.Release()

        member x.Update(dirty : seq<IResource>) =
            let heap = List<IResource>()
            for r in dirty do
                if all.Contains r then
                    heap.HeapEnqueue(compareLevel, r)

            while heap.Count > 0 do
                let r = heap.HeapDequeue(compareLevel)
                let t = r.Update()
                if t > maxTime then
                    for o in r.Outputs do
                        if all.Contains o then
                            heap.HeapEnqueue(compareLevel, o)

                    maxTime <- t

            maxTime



open Aardvark.Application.WinForms

module private NativeSupport =
    open System.IO
    open System.Runtime.InteropServices

    [<DllImport("kernel32.dll", SetLastError=true)>]
    extern bool ReadFile(Microsoft.Win32.SafeHandles.SafeFileHandle handle, IntPtr buffer, uint32 numBytesToRead, int* numBytesRead, IntPtr overlapped);

    let ReadFile2 (file : string) = 
        let info = FileInfo(file)
        let f = File.OpenRead file

        let mutable ptr = Marshal.AllocHGlobal( info.Length |> nativeint)
        let result = ptr
        let mutable remaining = info.Length
        let buffer = Array.zeroCreate<byte> (1 <<< 22)
        while remaining > 0L do
            let s = f.Read(buffer, 0, min remaining buffer.LongLength |> int)
            Marshal.Copy(buffer, 0, ptr, s)
            ptr <- ptr + nativeint s
            remaining <- remaining - int64 s
        result


let lerp : float -> byte -> byte -> byte =
    fun t a b -> Fun.Lerp(t,a,b)


type NativeVolume<'a when 'a : unmanaged> with
    member x.BlitToInternalYXZE(y : NativeVolume<'a>, lerp : float -> 'a -> 'a -> 'a) = 
        let lerp = OptimizedClosures.FSharpFunc<float, 'a, 'a, 'a>.Adapt(lerp)
        let sa = nativeint (sizeof<'a>)
        let mutable py = y.Pointer |> NativePtr.toNativeInt
        py <- py + nativeint y.Info.Origin * sa
        let mutable px = x.Pointer |> NativePtr.toNativeInt
        px <- px + nativeint x.Info.Origin * sa
        let xdY = nativeint x.DY * sa
        let ysY = nativeint (y.SY * y.DY) * sa
        let yjY = nativeint (y.DY - y.SX * y.DX) * sa
        let xdX = nativeint x.DX * sa
        let ysX = nativeint (y.SX * y.DX) * sa
        let yjX = nativeint (y.DX - y.SZ * y.DZ) * sa
        let xdZ = nativeint x.DZ * sa
        let xjZ = nativeint x.DZ * sa
        let ysZ = nativeint (y.SZ * y.DZ) * sa
        let yjZ = nativeint y.DZ * sa
        let ratio = V3d(x.Size) / V3d(y.Size)
        let initialCoord = 0.5 * ratio - V3d.Half
        let initialiCoord = V3l(initialCoord.Floor())
        let initialFrac = initialCoord - V3d(initialiCoord)
        let step = V3d.One * ratio
        let mutable coord = initialCoord
        let mutable icoord = initialiCoord
        let mutable frac = initialFrac
        px <- px + xdY * nativeint icoord.Y + xdX * nativeint icoord.X + xdZ * nativeint icoord.Z
        let yeY = py + ysY
        while py <> yeY do
            let yeX = py + ysX
            coord.X <- initialCoord.X
            px <- px + xdX * nativeint (initialiCoord.X - icoord.X)
            frac.X <- initialFrac.X
            icoord.X <- initialiCoord.X
            while py <> yeX do
                let yeZ = py + ysZ
                coord.Z <- initialCoord.Z
                px <- px + xdZ * nativeint (initialiCoord.Z - icoord.Z)
                icoord.Z <- initialiCoord.Z
                while py <> yeZ do
                    let v000 : 'a = NativePtr.read (NativePtr.ofNativeInt px)
                    let v010 : 'a = NativePtr.read (NativePtr.ofNativeInt (px + xdY))
                    let v100 : 'a = NativePtr.read (NativePtr.ofNativeInt (px + xdX))
                    let v110 : 'a = NativePtr.read (NativePtr.ofNativeInt (px + xdY + xdX))
                    // lerp X
                    let vx00 = lerp.Invoke(frac.X, v000, v100)
                    let vx10 = lerp.Invoke(frac.X, v010, v110)
                    // lerp Y
                    let vxx0 = lerp.Invoke(frac.Y, vx00, vx10)
                    NativePtr.write (NativePtr.ofNativeInt<'a> py) vxx0
                    py <- py + yjZ
                    px <- px + xjZ
                    coord.Z <- coord.Z + step.Z
                    icoord.Z <- icoord.Z + 1L
                py <- py + yjX
                coord.X <- coord.X + step.X
                let ni = int64 (floor coord.X)
                px <- px + xdX * nativeint (ni - icoord.X)
                icoord.X <- ni
                frac.X <- coord.X - float(icoord.X)
            py <- py + yjY
            coord.Y <- coord.Y + step.Y
            let ni = int64 (floor coord.Y)
            px <- px + xdY * nativeint (ni - icoord.Y)
            icoord.Y <- ni
            frac.Y <- coord.Y - float(icoord.Y)




[<EntryPoint>]
[<STAThread>]
let main args =
    //Management.run()

    let useVulkan = true

    
    Aardvark.Init()


    Examples.Jpeg.run()
    System.Environment.Exit 0

    ////let inputVolume = NativeSupport.ReadFile2 @"C:\volumes\GussPK_AlSi_0.5Sn_180kV_925x925x500px.raw"

    ////let size = V3i(925,925,500)
    ////let halfSize = size / 2
    ////let volume = NativeVolume<uint16>(NativePtr.ofNativeInt inputVolume, VolumeInfo(0L,V3l size,V3l(1,size.X,size.X*size.Y)))

    ////let target : byte[] = Array.zeroCreate (halfSize.X*halfSize.Y*halfSize.Z * 2)
    ////let gc = GCHandle.Alloc(target,GCHandleType.Pinned)

    ////let targetVolume = NativeVolume<uint16>(NativePtr.ofNativeInt<| gc.AddrOfPinnedObject(), VolumeInfo(0L,V3l halfSize,V3l(1,halfSize.X,halfSize.X*halfSize.Y)))

    ////targetVolume.SetByCoord(fun (v : V3d) -> 
    ////    volume.SampleLinear(v, fun t a b -> Fun.Lerp(t,a,b))
    ////)

    ////gc.Free()

    ////File.WriteAllBytes(sprintf @"C:\volumes\gussHalf_%d_%d_%d.raw" halfSize.X halfSize.Y halfSize.Z, target)
    ////System.Environment.Exit 0
    //let file = @"C:\Users\Schorsch\Desktop\london.jpg"
    //let src = PixImage.Create(file).ToPixImage<byte>()
    ////let src = PixImage.Create(@"C:\volumes\dog2.png").ToPixImage<byte>()
    //let dst = PixImage<byte>(src.Format, src.Size * 2 / 3)
    //let dst2 = PixImage<byte>(src.Format, src.Size * 2 / 3)

    //let bla = V3d(0.5 / V2d src.Size,0.0)

    //let vSrc = src.Volume // GetChannel(0L)
    //let vDst = dst.Volume //GetChannel(0L)
    //let vDst2 = dst2.Volume //GetChannel(0L)


    //NativeVolume.using vSrc (fun pSrc -> 
    //    NativeVolume.using vDst (fun pDst -> 
    //        let lerp = lerp

    //        let test = pSrc.[0,0,0]

    //        for i in 1 .. 2 do
    //            NativeVolume.blit pSrc pDst
        
    //        let iter = 10
    //        let sw = System.Diagnostics.Stopwatch.StartNew()
    //        for i in 1 .. iter do
    //            NativeVolume.blit pSrc pDst
    //        sw.Stop()

    //        Log.line "took: %A" (sw.MicroTime / iter)
    //        dst.SaveAsImage(@"C:\Users\Schorsch\Desktop\blit.png")

    //    )
    //)
    //NativeVolume.using vSrc (fun pSrc -> 
    //    NativeVolume.using vDst2 (fun pDst -> 
    //        let lerp = lerp
    //        pDst.SetByCoord(fun (c : V3d) ->
    //            pSrc.SampleLinear(c, lerp)
    //        )
    //        dst2.SaveAsImage(@"C:\Users\Schorsch\Desktop\sample.png")
    //    )
    //)

    //let gc = GCHandle.Alloc(src.Volume.Data, GCHandleType.Pinned)
    //let img = IL.GenImage()
    //IL.BindImage(img)
    ////IL.LoadImage file |> ignore
    //IL.TexImage(src.Size.X, src.Size.Y, 1, 3uy, ChannelFormat.RGB, ChannelType.UnsignedByte, gc.AddrOfPinnedObject()) |> ignore
    //ILU.ImageParameter(ImageParameterName.Filter, int Filter.Bilinear)
    //ILU.Scale(dst.Size.X, dst.Size.Y, 1) |> ignore
    //ILU.FlipImage() |> ignore
    ////ILU.FlipImage() |> ignore
    //IL.Save(ImageType.Png, @"C:\Users\Schorsch\Desktop\devil.png") |> ignore
    //IL.BindImage(0)
    //IL.DeleteImage(img)
    //gc.Free()

    //let mutable maxDiff = 0


    //let compare (a : byte) (b : byte) =
    //    let d = int a - int b |> abs
    //    if d <> 0 then
    //        maxDiff <- max maxDiff d
    //        1
    //    else
    //        0

    //let diff = dst.Volume.InnerProduct(dst2.Volume, System.Func<_,_,_>(compare), 0, System.Func<_,_,_>((+)))
    //Log.warn "size: %A" dst.Size
    //Log.warn "diff <= %A" maxDiff
    //Log.warn "diff: %.2f%%" (100.0 * float diff / float (dst.Size.X * dst.Size.Y * 3))

    ////dst.SaveAsImage(@"C:\Users\Schorsch\Desktop\blit.png")
    //src.SaveAsImage(@"C:\Users\Schorsch\Desktop\input.png")
    //System.Environment.Exit 0


    ////Aardvark.Application.OpenVR.UnhateTest.run()
    
    //let lerp = OptimizedClosures.FSharpFunc<float, float, float>.Adapt(fun a b -> a)

    //Examples.Tessellation.run()
    //Examples.Stereo.runNew()
    //Examples.ComputeShader.run()
    //Examples.CommandTest.run()
    //Examples.LoD.run()
    //Examples.LevelOfDetail.run()
    //Examples.Wobble.run()
    //Examples.GeometryComposition.run()
    Examples.Eigi.run()
    //Examples.Sponza.run()
    //Examples.IGPrimitives.run()
    //Examples.LevelOfDetail.run()
    //Examples.Terrain.run()
    //Examples.Jpeg.run()
    System.Environment.Exit 0

    //colorLockTest()
    let app,win =
        if useVulkan then
            let app = new Aardvark.Application.WinForms.VulkanApplication()
            let win = app.CreateSimpleRenderWindow()
            app :> Aardvark.Application.IApplication,win :> Aardvark.Application.IRenderWindow
        else
            let app = new Aardvark.Application.WinForms.OpenGlApplication()
            let win = app.CreateSimpleRenderWindow()
            app:> Aardvark.Application.IApplication,win :> Aardvark.Application.IRenderWindow
    CullingTest.run app win |> ignore
    //CullingTest.runInstanced () |> ignore
    //CullingTest.runStructural app win|> ignore
    System.Environment.Exit 0


    //Examples.Tutorial.run()
    //Examples.Instancing.run()
    //Examples.Render2TexturePrimitive.run()
    //Examples.Render2TextureComposable.run()
    //Examples.Render2TexturePrimiviteChangeableSize.run()
    //Examples.Render2TexturePrimitiveFloat.run()
    //Examples.ComputeTest.run()
    //colorLockTest()



    
    Aardvark.Init()
    
    Interactive.Renderer <- Vulkan
    
    Aardvark.Rendering.GL.RuntimeConfig.SuppressSparseBuffers <- false
    //Examples.PostProcessing.run()

    //Examples.CommandTest.run()
    //Examples.LoD.run()
    //Examples.Shadows.run()
    //Examples.AssimpInterop.run() 
    //Examples.ShaderSignatureTest.run()
    //Examples.Polygons.run()           attention: this one is currently broken due to package refactoring
    //Examples.TicTacToe.run()          attention: this one is currently broken due to package refactoring
    0
