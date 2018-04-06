namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Collections.Generic
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop
open Aardvark.Base

#nowarn "9"
#nowarn "51"

[<AutoOpen>]
module private Utilities =
    let check (str : string) (err : VkResult) =
        if err <> VkResult.VkSuccess then 
            Log.error "[Vulkan] %s (%A)" str err
            failwithf "[Vulkan] %s (%A)" str err

    let checkf (fmt : Printf.StringFormat<'a, VkResult -> unit>) =
        Printf.kprintf (fun (str : string) (res : VkResult) ->
            if res <> VkResult.VkSuccess then 
                Log.error "[Vulkan] %s (%A)" str res
                failwithf "[Vulkan] %s (%A)" str res
        ) fmt

    let inline failf fmt = 
        Printf.kprintf (fun str -> 
            Log.error "[Vulkan] %s" str
            failwith ("[Vulkan] " + str)
        ) fmt

    let VK_QUEUE_FAMILY_IGNORED = ~~~0u

    module Map =
        let ofSeqDupl (s : seq<'a * 'b>) =
            let mutable res = Map.empty
            for (k,v) in s do
                match Map.tryFind k res with
                    | Some set ->
                        res <- Map.add k (Set.add v set) res
                    | None ->
                        res <- Map.add k (Set.singleton v) res
            res

    let private nextBetterFormat =
        Map.ofList [
            VkFormat.D24UnormS8Uint, VkFormat.D32SfloatS8Uint
            VkFormat.X8D24UnormPack32, VkFormat.D32Sfloat
        ]

    type VkFormat with
        member x.NextBetter = Map.tryFind x nextBetterFormat
            

type ILogger =
    abstract member section<'a, 'x>     : Printf.StringFormat<'a, (unit -> 'x) -> 'x> -> 'a
    abstract member line<'a, 'x>        : Printf.StringFormat<'a, unit> -> 'a
    abstract member WithVerbosity       : int -> ILogger
    abstract member Verbosity           : int

type Logger private(verbosity : int) =
    static let instances = Array.init 6 (fun i -> Logger(i) :> ILogger)

    static member Default = instances.[2]
    static member Get v = instances.[v]

    interface ILogger with
        member x.Verbosity = verbosity
        member x.WithVerbosity(v) = instances.[v]
        member x.section (fmt : Printf.StringFormat<'a, (unit -> 'x) -> 'x>) =
            fmt |> Printf.kprintf (fun (str : string) ->
                fun cont -> 
                    try
                        Report.Begin(verbosity, "{0}", str)
                        cont()
                    finally 
                        Report.End(verbosity) |> ignore
            )

        member x.line fmt = Printf.kprintf (fun str -> Report.Line(verbosity, str)) fmt



[<AutoOpen>]
module BaseLibExtensions = 
    module NativePtr =
        let withA (f : nativeptr<'a> -> 'b) (a : 'a[]) =
            if a.Length = 0 then
                f NativePtr.zero
            else
                let gc = GCHandle.Alloc(a, GCHandleType.Pinned)
                try f (gc.AddrOfPinnedObject() |> NativePtr.ofNativeInt)
                finally gc.Free()

        let withOption (f : nativeptr<'a> -> 'b) (a : Option<'a>) =
            match a with
                | Some a -> [| a |] |> withA f
                | None -> f NativePtr.zero

    type NativeBuilder() =
        member x.Bind(m : 'a[], f : nativeptr<'a> -> 'r) =
            if m.Length = 0 then
                f NativePtr.zero
            else
                let gc = GCHandle.Alloc(m, GCHandleType.Pinned)
                try f (NativePtr.ofNativeInt (gc.AddrOfPinnedObject()))
                finally gc.Free()
                
        member x.Bind(m : list<'a>, f : nativeptr<'a> -> 'r) =
            x.Bind(List.toArray m, f)

        member x.Bind(m : Option<'a>, f : nativeptr<'a> -> 'r) =
            x.Bind(Option.toArray m, f)

        member x.Return(v : 'a) = v
        member x.Zero() = ()
        member x.Combine(l : unit, r : unit -> 'a) = r()
        member x.Delay(f : unit -> 'a) = f
        member x.Run(f : unit -> 'a) = f()

        member x.For(s : seq<'a>, f : 'a -> unit) =
            for e in s do f e

        member x.While(guard : unit -> bool, body : unit -> unit) =
            while guard() do
                body()

    let native = NativeBuilder()

    type Version with
        member v.ToVulkan() =
            ((uint32 v.Major) <<< 22) ||| ((uint32 v.Minor) <<< 12) ||| (uint32 v.Build)

        static member FromVulkan (v : uint32) =
            Version(int (v >>> 22), int ((v >>> 12) &&& 0x3FFu), int (v &&& 0xFFFu))

    type V2i with
        static member OfExtent (e : VkExtent2D) =
            V2i(int e.width, int e.height)
        
        member x.ToExtent() =
            VkExtent2D(uint32 x.X, uint32 x.Y)

    type V3i with
        static member OfExtent (e : VkExtent3D) =
            V3i(int e.width, int e.height, int e.depth)
        
        member x.ToExtent() =
            VkExtent3D(uint32 x.X, uint32 x.Y, uint32 x.Z)

    module VkRaw =
        let warn fmt = Printf.kprintf (fun str -> Report.Warn("[Vulkan] {0}", str)) fmt

        let debug fmt = Printf.kprintf (fun str -> Report.Line(2, "[Vulkan] {0}", str)) fmt


    module Array =
        let choosei (f : int -> 'a -> Option<'b>) (a : 'a[]) =
            let res = System.Collections.Generic.List<'b>()
            for i in 0 .. a.Length - 1 do
                match f i a.[i] with
                    | Some v -> res.Add v
                    | None -> ()

            res.ToArray()

        let collecti (f : int -> 'a -> list<'b>) (a : 'a[]) =
            let mutable i = 0
            let res = System.Collections.Generic.List<'b>()
            for v in a do
                res.AddRange(f i v)
                i <- i + 1

            res.ToArray()

    module List =
        let choosei (f : int -> 'a -> Option<'b>) (a : list<'a>) =
            let res = System.Collections.Generic.List<'b>()
            let mutable i = 0
            for v in a do
                match f i v with
                    | Some v -> res.Add v
                    | None -> ()
                i <- i + 1

            res |> CSharpList.toList


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module private Alignment = 
    let prev (align : int64) (v : int64) =
        let r = v % align
        if r = 0L then v
        else v - r

    let next (align : int64) (v : int64) =
        let r = v % align
        if r = 0L then v
        else align + v - r


[<AbstractClass>]
type VulkanObject() =
    let mutable isDisposed = 0

    let beforeDispose = Event<unit>()

    abstract member Release : unit -> unit

    [<CLIEvent>]
    member x.BeforeDispose = beforeDispose.Publish

    member x.IsDisposed = isDisposed <> 0

    member inline private x.Dispose(disposing : bool) =
        let o = Interlocked.Exchange(&isDisposed, 1)
        if o = 0 then
            beforeDispose.Trigger()
            x.Release()
            if disposing then GC.SuppressFinalize x

    member x.Dispose() = x.Dispose true
    override x.Finalize() = x.Dispose false

    interface IDisposable with
        member x.Dispose() = x.Dispose()


module Enum =
    [<AutoOpen>]
    module BitUtils =
        let bits (f : int) =
            let rec testBits (i : int) (mask : int) =
                if i > 32 then []
                else
                    if f &&& mask <> 0 then 
                        i :: testBits (i + 1) (mask <<< 1)
                    else
                        testBits (i + 1) (mask <<< 1)
            testBits 0 0x1

        let allSubsets (s : list<'a>) =
            let rec allSubsetsInternal (s : list<'a>) =
                match s with
                    | [] -> [Set.empty]
                    | h :: rest ->
                        let r = allSubsetsInternal rest
                        (r |> List.map (Set.add h)) @ r

            allSubsetsInternal s |> List.filter (Set.isEmpty >> not)

        let allSubMasks (m : int) =
            let bits = bits m
            allSubsets bits |> List.map (fun s ->
                s |> Set.fold (fun m b -> m ||| (1 <<< b)) 0
            )

    let inline allSubFlags (f : ^a) =
        allSubMasks (int f) |> List.map unbox< ^a >

type private MultiTable<'k, 'v when 'k : equality>(initial : seq<'k * 'v>) =
    let store = Dictionary<'k, HashSet<'v>>()

    member private x.TryRemoveEnum (f : 'k -> bool, e : byref<Dictionary.Enumerator<'k, HashSet<'v>>>, res : byref<'v>) =
        if e.MoveNext() then
            let current = e.Current
            let key = current.Key
            if f key then
                let value = current.Value
                let thing = value |> Seq.head
                if value.Count = 1 then
                    store.Remove key |> ignore
                    res <- thing
                    true
                else
                    value.Remove thing |> ignore
                    res <- thing
                    true
            else
                x.TryRemoveEnum(f, &e, &res)
        else
            false

    member private x.TryPeekEnum (f : 'k -> bool, e : byref<Dictionary.Enumerator<'k, HashSet<'v>>>, res : byref<'v>) =
        if e.MoveNext() then
            let current = e.Current
            let key = current.Key
            if f key then
                let value = current.Value
                let thing = value |> Seq.head
                res <- thing
                true
            else
                x.TryPeekEnum(f, &e, &res)
        else
            false

    member x.Add (k : 'k, v : 'v) =
        match store.TryGetValue k with
            | (true, set) -> set.Add v
            | _ ->
                let set = HashSet [v]
                store.[k] <- set
                true

    member x.TryRemove (f : 'k -> bool, [<Out>] res : byref<'v>) =
        let mutable e = store.GetEnumerator()
        try x.TryRemoveEnum(f, &e, &res)
        finally e.Dispose()

    member x.TryPeek (f : 'k -> bool, [<Out>] res : byref<'v>) =
        let mutable e = store.GetEnumerator()
        try x.TryPeekEnum(f, &e, &res)
        finally e.Dispose()

type FlagPool<'k, 'v when 'k : enum<int> >(initial : seq<'v>, flags : 'v -> 'k) =
    static let toInt (k : 'k) = k |> unbox<int>
    let table = MultiTable<int, 'v>(initial |> Seq.map (fun v -> (toInt (flags v), v)))
    let available = initial |> Seq.collect (flags >> toInt >> Enum.allSubFlags) |> HashSet.ofSeq

    let changed = new System.Threading.AutoResetEvent(false)

    static let check (flags : int) (f : int) =
        f &&& flags = flags

    member private x.TryAcquireInt (flags : int, [<Out>] value : byref<'v>) =
        Monitor.Enter table
        try table.TryRemove(check flags, &value)
        finally Monitor.Exit table

    member x.TryAcquire(flags : 'k, [<Out>] value : byref<'v>) =
        let flags = toInt flags
        x.TryAcquireInt(flags, &value)

    member x.Acquire (flags : 'k) =
        let flags = toInt flags
        let mutable result = Unchecked.defaultof<'v>
        while not (x.TryAcquireInt(flags, &result)) do
            changed.WaitOne() |> ignore

        result

    member x.AcquireAsync (flags : 'k) =
        async {
            let flags = toInt flags
            let mutable result = Unchecked.defaultof<'v>
            while not (x.TryAcquireInt(flags, &result)) do
                let! _ = Async.AwaitWaitHandle changed
                ()

            return result
        }

    member x.Release (value : 'v) =
        let flags = flags value
        lock table (fun () ->
            if table.Add(toInt flags, value) then
                changed.Set() |> ignore
        )


[<AllowNullLiteral; AbstractClass>]
type Disposable() =
    abstract member Dispose : unit -> unit
    interface IDisposable with
        member x.Dispose() = x.Dispose()

    static member inline Empty : Disposable = null

    static member Compose(l : Disposable, r : Disposable) =
        if isNull l then r
        elif isNull r then l
        else new Composite([l; r]) :> Disposable

    static member Compose(l : list<Disposable>) =
        match List.filter (isNull >> not) l with
            | [] -> null
            | l -> new Composite(l) :> Disposable

    static member inline Custom (f : unit -> unit) =
        { new Disposable() with member x.Dispose() = f() }

    static member inline Dispose (d : Disposable) = d.Dispose()

and private Composite(l : list<Disposable>) =
    inherit Disposable()
    override x.Dispose() = l |> List.iter Disposable.Dispose



[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module VkFormat =
    let ofTextureFormat =
        LookupTable.lookupTable [
            TextureFormat.Bgr8, VkFormat.B8g8r8Unorm
            TextureFormat.Bgra8, VkFormat.B8g8r8a8Unorm


            TextureFormat.DepthComponent, VkFormat.D24UnormS8Uint
            TextureFormat.Alpha, VkFormat.R8Unorm
            TextureFormat.Rgb, VkFormat.R8g8b8Unorm
            TextureFormat.Rgba, VkFormat.R8g8b8a8Unorm
            TextureFormat.Luminance, VkFormat.R8Unorm
            TextureFormat.LuminanceAlpha, VkFormat.R8g8Unorm
            TextureFormat.Rgb4, VkFormat.R4g4b4a4UnormPack16
            TextureFormat.Rgb5, VkFormat.R5g5b5a1UnormPack16
            TextureFormat.Rgb8, VkFormat.R8g8b8Unorm
            TextureFormat.Rgb10, VkFormat.A2b10g10r10UnormPack32
            TextureFormat.Rgb16, VkFormat.R16g16b16Uint
            TextureFormat.Rgba4, VkFormat.R4g4b4a4UnormPack16
            TextureFormat.Rgb5A1, VkFormat.R5g5b5a1UnormPack16
            TextureFormat.Rgba8, VkFormat.R8g8b8a8Unorm
            TextureFormat.Rgb10A2, VkFormat.A2r10g10b10UnormPack32
            TextureFormat.Rgba16, VkFormat.R16g16b16a16Unorm
            TextureFormat.DualAlpha4Sgis, VkFormat.R4g4UnormPack8
            TextureFormat.DualAlpha8Sgis, VkFormat.R8g8Unorm
            TextureFormat.DualAlpha16Sgis, VkFormat.R16g16Unorm
            TextureFormat.DualLuminance4Sgis, VkFormat.R4g4UnormPack8
            TextureFormat.DualLuminance8Sgis, VkFormat.R8g8Unorm
            TextureFormat.DualLuminance16Sgis, VkFormat.R16g16Unorm
            TextureFormat.DualIntensity4Sgis, VkFormat.R4g4UnormPack8
            TextureFormat.DualIntensity8Sgis, VkFormat.R8g8Unorm
            TextureFormat.DualIntensity16Sgis, VkFormat.R16g16Unorm
            TextureFormat.DualLuminanceAlpha4Sgis, VkFormat.R4g4UnormPack8
            TextureFormat.DualLuminanceAlpha8Sgis, VkFormat.R8g8Unorm
            TextureFormat.QuadAlpha4Sgis, VkFormat.R4g4b4a4UnormPack16
            TextureFormat.QuadAlpha8Sgis, VkFormat.R8g8b8a8Unorm
            TextureFormat.QuadLuminance4Sgis, VkFormat.R4g4b4a4UnormPack16
            TextureFormat.QuadLuminance8Sgis, VkFormat.R8g8b8a8Unorm
            TextureFormat.QuadIntensity4Sgis, VkFormat.R4g4b4a4UnormPack16
            TextureFormat.QuadIntensity8Sgis, VkFormat.R8g8b8a8Unorm
            TextureFormat.DepthComponent16, VkFormat.D16Unorm
            TextureFormat.DepthComponent24, VkFormat.D24UnormS8Uint
            TextureFormat.DepthComponent32, VkFormat.D32SfloatS8Uint
//                TextureFormat.CompressedRed, VkFormat.
//                TextureFormat.CompressedRg, VkFormat.
            TextureFormat.R8, VkFormat.R8Unorm
            TextureFormat.R16, VkFormat.R16Unorm
            TextureFormat.Rg8, VkFormat.R8g8Unorm
            TextureFormat.Rg16, VkFormat.R16g16Unorm
            TextureFormat.R16f, VkFormat.R16Sfloat
            TextureFormat.R32f, VkFormat.R32Sfloat
            TextureFormat.Rg16f, VkFormat.R16g16Sfloat
            TextureFormat.Rg32f, VkFormat.R32g32Sfloat
            TextureFormat.R8i, VkFormat.R8Sint
            TextureFormat.R8ui, VkFormat.R8Uint
            TextureFormat.R16i, VkFormat.R16Sint
            TextureFormat.R16ui, VkFormat.R16Uint
            TextureFormat.R32i, VkFormat.R32Sint
            TextureFormat.R32ui, VkFormat.R32Uint
            TextureFormat.Rg8i, VkFormat.R8g8Sint
            TextureFormat.Rg8ui, VkFormat.R8g8Uint
            TextureFormat.Rg16i, VkFormat.R16g16Sint
            TextureFormat.Rg16ui, VkFormat.R16g16Uint
            TextureFormat.Rg32i, VkFormat.R32g32Sint
            TextureFormat.Rg32ui, VkFormat.R32g32Uint


            // TODO: check
            TextureFormat.CompressedRgbS3tcDxt1Ext, VkFormat.Bc1RgbUnormBlock
            TextureFormat.CompressedRgbaS3tcDxt1Ext, VkFormat.Bc1RgbaUnormBlock
            TextureFormat.CompressedRgbaS3tcDxt3Ext, VkFormat.Bc2UnormBlock
            TextureFormat.CompressedRgbaS3tcDxt5Ext, VkFormat.Bc3UnormBlock

//                TextureFormat.CompressedRgbS3tcDxt1Ext, VkFormat.
//                TextureFormat.CompressedRgbaS3tcDxt1Ext, VkFormat.
//                TextureFormat.CompressedRgbaS3tcDxt3Ext, VkFormat.
//                TextureFormat.CompressedRgbaS3tcDxt5Ext, VkFormat.
//                TextureFormat.RgbIccSgix, VkFormat.
//                TextureFormat.RgbaIccSgix, VkFormat.
//                TextureFormat.AlphaIccSgix, VkFormat.
//                TextureFormat.LuminanceIccSgix, VkFormat.
//                TextureFormat.IntensityIccSgix, VkFormat.
//                TextureFormat.LuminanceAlphaIccSgix, VkFormat.
//                TextureFormat.R5G6B5IccSgix, VkFormat.
//                TextureFormat.R5G6B5A8IccSgix, VkFormat.
//                TextureFormat.Alpha16IccSgix, VkFormat.
//                TextureFormat.Luminance16IccSgix, VkFormat.
//                TextureFormat.Intensity16IccSgix, VkFormat.
//                TextureFormat.Luminance16Alpha8IccSgix, VkFormat.
//                TextureFormat.CompressedAlpha, VkFormat.
//                TextureFormat.CompressedLuminance, VkFormat.
//                TextureFormat.CompressedLuminanceAlpha, VkFormat.
//                TextureFormat.CompressedIntensity, VkFormat.
//                TextureFormat.CompressedRgb, VkFormat.
//                TextureFormat.CompressedRgba, VkFormat.
            TextureFormat.DepthStencil, VkFormat.D24UnormS8Uint
            TextureFormat.Rgba32f, VkFormat.R32g32b32a32Sfloat
            TextureFormat.Rgb32f, VkFormat.R32g32b32Sfloat
            TextureFormat.Rgba16f, VkFormat.R16g16b16a16Sfloat
            TextureFormat.Rgb16f, VkFormat.R16g16b16Sfloat
            TextureFormat.Depth24Stencil8, VkFormat.D24UnormS8Uint
//                TextureFormat.R11fG11fB10f, VkFormat.R11
//                TextureFormat.Rgb9E5, VkFormat.
            TextureFormat.Srgb, VkFormat.R8g8b8Srgb
            TextureFormat.Srgb8, VkFormat.R8g8b8Srgb
            TextureFormat.SrgbAlpha, VkFormat.R8g8b8a8Srgb
            TextureFormat.Srgb8Alpha8, VkFormat.R8g8b8a8Srgb
//                TextureFormat.SluminanceAlpha, VkFormat.
//                TextureFormat.Sluminance8Alpha8, VkFormat.
//                TextureFormat.Sluminance, VkFormat.
//                TextureFormat.Sluminance8, VkFormat.
//                TextureFormat.CompressedSrgb, VkFormat.
//                TextureFormat.CompressedSrgbAlpha, VkFormat.
//                TextureFormat.CompressedSluminance, VkFormat.
//                TextureFormat.CompressedSluminanceAlpha, VkFormat.
//                TextureFormat.CompressedSrgbS3tcDxt1Ext, VkFormat.
//                TextureFormat.CompressedSrgbAlphaS3tcDxt1Ext, VkFormat.
//                TextureFormat.CompressedSrgbAlphaS3tcDxt3Ext, VkFormat.
//                TextureFormat.CompressedSrgbAlphaS3tcDxt5Ext, VkFormat.
            TextureFormat.DepthComponent32f, VkFormat.D32Sfloat
            TextureFormat.Depth32fStencil8, VkFormat.D32SfloatS8Uint
            TextureFormat.Rgba32ui, VkFormat.R32g32b32a32Uint
            TextureFormat.Rgb32ui, VkFormat.R32g32b32Uint
            TextureFormat.Rgba16ui, VkFormat.R16g16b16a16Uint
            TextureFormat.Rgb16ui, VkFormat.R16g16b16Uint
            TextureFormat.Rgba8ui, VkFormat.R8g8b8a8Uint
            TextureFormat.Rgb8ui, VkFormat.R8g8b8Uint
            TextureFormat.Rgba32i, VkFormat.R32g32b32a32Sint
            TextureFormat.Rgb32i, VkFormat.R32g32b32Sint
            TextureFormat.Rgba16i, VkFormat.R16g16b16a16Sint
            TextureFormat.Rgb16i, VkFormat.R16g16b16Sint
            TextureFormat.Rgba8i, VkFormat.R8g8b8a8Sint
            TextureFormat.Rgb8i, VkFormat.R8g8b8Sint
            TextureFormat.Float32UnsignedInt248Rev, VkFormat.D24UnormS8Uint
//                TextureFormat.CompressedRedRgtc1, VkFormat.
//                TextureFormat.CompressedSignedRedRgtc1, VkFormat.
//                TextureFormat.CompressedRgRgtc2, VkFormat.
//                TextureFormat.CompressedSignedRgRgtc2, VkFormat.
//                TextureFormat.CompressedRgbaBptcUnorm, VkFormat.
//                TextureFormat.CompressedRgbBptcSignedFloat, VkFormat.
//                TextureFormat.CompressedRgbBptcUnsignedFloat, VkFormat.
            TextureFormat.R8Snorm, VkFormat.R8Snorm
            TextureFormat.Rg8Snorm, VkFormat.R8g8Snorm
            TextureFormat.Rgb8Snorm, VkFormat.R8g8b8Snorm
            TextureFormat.Rgba8Snorm, VkFormat.R8g8b8a8Snorm
            TextureFormat.R16Snorm, VkFormat.R16Snorm
            TextureFormat.Rg16Snorm, VkFormat.R16g16Snorm
            TextureFormat.Rgb16Snorm, VkFormat.R16g16b16Snorm
            TextureFormat.Rgba16Snorm, VkFormat.R16g16b16a16Snorm
            TextureFormat.Rgb10A2ui, VkFormat.A2b10g10r10UintPack32
//                TextureFormat.One, VkFormat.
//                TextureFormat.Two, VkFormat.
//                TextureFormat.Three, VkFormat.
//                TextureFormat.Four, VkFormat.

        ]

    let ofRenderbufferFormat (fmt : RenderbufferFormat) =
        fmt |> int |> unbox<TextureFormat> |> ofTextureFormat

    let toTextureFormat =
        let unknown = unbox<TextureFormat> 0
        LookupTable.lookupTable [
            VkFormat.Undefined, unknown
            VkFormat.R4g4UnormPack8, unknown
            VkFormat.R4g4b4a4UnormPack16, TextureFormat.Rgba4
            VkFormat.B4g4r4a4UnormPack16, unknown
            VkFormat.R5g6b5UnormPack16, TextureFormat.R5G6B5IccSgix
            VkFormat.B5g6r5UnormPack16, unknown
            VkFormat.R5g5b5a1UnormPack16, TextureFormat.R5G6B5A8IccSgix
            VkFormat.B5g5r5a1UnormPack16, unknown
            VkFormat.A1r5g5b5UnormPack16, unknown
            VkFormat.R8Unorm, TextureFormat.R8
            VkFormat.R8Snorm, TextureFormat.R8Snorm
            VkFormat.R8Uscaled, TextureFormat.R8
            VkFormat.R8Sscaled, TextureFormat.R8
            VkFormat.R8Uint, TextureFormat.R8ui
            VkFormat.R8Sint, TextureFormat.R8i
            VkFormat.R8Srgb, TextureFormat.R8
            VkFormat.R8g8Unorm, TextureFormat.Rg8
            VkFormat.R8g8Snorm, TextureFormat.Rg8Snorm
            VkFormat.R8g8Uscaled, TextureFormat.Rg8
            VkFormat.R8g8Sscaled, TextureFormat.Rg8
            VkFormat.R8g8Uint, TextureFormat.Rg8ui
            VkFormat.R8g8Sint, TextureFormat.Rg8i
            VkFormat.R8g8Srgb, TextureFormat.Rg8
            VkFormat.R8g8b8Unorm, TextureFormat.Rgb8
            VkFormat.R8g8b8Snorm, TextureFormat.Rgb8Snorm
            VkFormat.R8g8b8Uscaled, TextureFormat.Rgb8
            VkFormat.R8g8b8Sscaled, TextureFormat.Rgb8
            VkFormat.R8g8b8Uint, TextureFormat.Rgb8ui
            VkFormat.R8g8b8Sint, TextureFormat.Rgb8i
            VkFormat.R8g8b8Srgb, TextureFormat.Srgb8
            VkFormat.B8g8r8Unorm, TextureFormat.Bgr8
            VkFormat.B8g8r8Snorm, TextureFormat.Bgr8
            VkFormat.B8g8r8Uscaled, TextureFormat.Bgr8
            VkFormat.B8g8r8Sscaled, TextureFormat.Bgr8
            VkFormat.B8g8r8Uint, TextureFormat.Bgr8
            VkFormat.B8g8r8Sint, TextureFormat.Bgr8
            VkFormat.B8g8r8Srgb, TextureFormat.Bgr8
            VkFormat.R8g8b8a8Unorm, TextureFormat.Rgba8
            VkFormat.R8g8b8a8Snorm, TextureFormat.Rgba8Snorm
            VkFormat.R8g8b8a8Uscaled, TextureFormat.Rgba8
            VkFormat.R8g8b8a8Sscaled, TextureFormat.Rgba8
            VkFormat.R8g8b8a8Uint, TextureFormat.Rgba8ui
            VkFormat.R8g8b8a8Sint, TextureFormat.Rgba8i
            VkFormat.R8g8b8a8Srgb, TextureFormat.Srgb8Alpha8
            VkFormat.B8g8r8a8Unorm, TextureFormat.Bgra8
            VkFormat.B8g8r8a8Snorm, TextureFormat.Bgra8
            VkFormat.B8g8r8a8Uscaled, TextureFormat.Bgra8
            VkFormat.B8g8r8a8Sscaled, TextureFormat.Bgra8
            VkFormat.B8g8r8a8Uint, TextureFormat.Bgra8
            VkFormat.B8g8r8a8Sint, TextureFormat.Bgra8
            VkFormat.B8g8r8a8Srgb, TextureFormat.Bgra8
            VkFormat.A8b8g8r8UnormPack32, unknown
            VkFormat.A8b8g8r8SnormPack32, unknown
            VkFormat.A8b8g8r8UscaledPack32, unknown
            VkFormat.A8b8g8r8SscaledPack32, unknown
            VkFormat.A8b8g8r8UintPack32, unknown
            VkFormat.A8b8g8r8SintPack32, unknown
            VkFormat.A8b8g8r8SrgbPack32, unknown
            VkFormat.A2r10g10b10UnormPack32, unknown
            VkFormat.A2r10g10b10SnormPack32, unknown
            VkFormat.A2r10g10b10UscaledPack32, unknown
            VkFormat.A2r10g10b10SscaledPack32, unknown
            VkFormat.A2r10g10b10UintPack32, unknown
            VkFormat.A2r10g10b10SintPack32, unknown
            VkFormat.A2b10g10r10UnormPack32, unknown
            VkFormat.A2b10g10r10SnormPack32, unknown
            VkFormat.A2b10g10r10UscaledPack32, unknown
            VkFormat.A2b10g10r10SscaledPack32, unknown
            VkFormat.A2b10g10r10UintPack32, unknown
            VkFormat.A2b10g10r10SintPack32, unknown
            VkFormat.R16Unorm, TextureFormat.R16
            VkFormat.R16Snorm, TextureFormat.R16Snorm
            VkFormat.R16Uscaled, TextureFormat.R16
            VkFormat.R16Sscaled, TextureFormat.R16
            VkFormat.R16Uint, TextureFormat.R16ui
            VkFormat.R16Sint, TextureFormat.R16i
            VkFormat.R16Sfloat, TextureFormat.R16f
            VkFormat.R16g16Unorm, TextureFormat.Rg16
            VkFormat.R16g16Snorm, TextureFormat.Rg16Snorm
            VkFormat.R16g16Uscaled, TextureFormat.Rg16
            VkFormat.R16g16Sscaled, TextureFormat.Rg16
            VkFormat.R16g16Uint, TextureFormat.Rg16ui
            VkFormat.R16g16Sint, TextureFormat.Rg16i
            VkFormat.R16g16Sfloat, TextureFormat.Rg16f
            VkFormat.R16g16b16Unorm, TextureFormat.Rgb16
            VkFormat.R16g16b16Snorm, TextureFormat.Rgb16Snorm
            VkFormat.R16g16b16Uscaled, TextureFormat.Rgb16
            VkFormat.R16g16b16Sscaled, TextureFormat.Rgb16
            VkFormat.R16g16b16Uint, TextureFormat.Rgb16ui
            VkFormat.R16g16b16Sint, TextureFormat.Rgb16i
            VkFormat.R16g16b16Sfloat, TextureFormat.Rgb16f
            VkFormat.R16g16b16a16Unorm, TextureFormat.Rgba16
            VkFormat.R16g16b16a16Snorm, TextureFormat.Rgba16Snorm
            VkFormat.R16g16b16a16Uscaled, TextureFormat.Rgba16
            VkFormat.R16g16b16a16Sscaled, TextureFormat.Rgba16
            VkFormat.R16g16b16a16Uint, TextureFormat.Rgba16ui
            VkFormat.R16g16b16a16Sint, TextureFormat.Rgba16i
            VkFormat.R16g16b16a16Sfloat, TextureFormat.Rgba16f
            VkFormat.R32Uint, TextureFormat.R32ui
            VkFormat.R32Sint, TextureFormat.R32i
            VkFormat.R32Sfloat, TextureFormat.R32f
            VkFormat.R32g32Uint, TextureFormat.Rg32ui
            VkFormat.R32g32Sint, TextureFormat.Rg32i
            VkFormat.R32g32Sfloat, TextureFormat.Rg32f
            VkFormat.R32g32b32Uint, TextureFormat.Rgb32ui
            VkFormat.R32g32b32Sint, TextureFormat.Rgb32i
            VkFormat.R32g32b32Sfloat, TextureFormat.Rgb32f
            VkFormat.R32g32b32a32Uint, TextureFormat.Rgba32ui
            VkFormat.R32g32b32a32Sint, TextureFormat.Rgba32i
            VkFormat.R32g32b32a32Sfloat, TextureFormat.Rgba32f
            VkFormat.R64Uint, unknown
            VkFormat.R64Sint, unknown
            VkFormat.R64Sfloat, unknown
            VkFormat.R64g64Uint, unknown
            VkFormat.R64g64Sint, unknown
            VkFormat.R64g64Sfloat, unknown
            VkFormat.R64g64b64Uint, unknown
            VkFormat.R64g64b64Sint, unknown
            VkFormat.R64g64b64Sfloat, unknown
            VkFormat.R64g64b64a64Uint, unknown
            VkFormat.R64g64b64a64Sint, unknown
            VkFormat.R64g64b64a64Sfloat, unknown
            VkFormat.B10g11r11UfloatPack32, TextureFormat.R11fG11fB10f
            VkFormat.E5b9g9r9UfloatPack32, unknown
            VkFormat.D16Unorm, TextureFormat.DepthComponent16
            VkFormat.X8D24UnormPack32, TextureFormat.DepthComponent24
            VkFormat.D32Sfloat, TextureFormat.DepthComponent32f
            VkFormat.S8Uint, unknown
            VkFormat.D16UnormS8Uint, unknown
            VkFormat.D24UnormS8Uint, TextureFormat.Depth24Stencil8
            VkFormat.D32SfloatS8Uint, TextureFormat.Depth32fStencil8
            VkFormat.Bc1RgbUnormBlock, unknown
            VkFormat.Bc1RgbSrgbBlock, unknown
            VkFormat.Bc1RgbaUnormBlock, unknown
            VkFormat.Bc1RgbaSrgbBlock, unknown
            VkFormat.Bc2UnormBlock, unknown
            VkFormat.Bc2SrgbBlock, unknown
            VkFormat.Bc3UnormBlock, unknown
            VkFormat.Bc3SrgbBlock, unknown
            VkFormat.Bc4UnormBlock, unknown
            VkFormat.Bc4SnormBlock, unknown
            VkFormat.Bc5UnormBlock, unknown
            VkFormat.Bc5SnormBlock, unknown
            VkFormat.Bc6hUfloatBlock, unknown
            VkFormat.Bc6hSfloatBlock, unknown
            VkFormat.Bc7UnormBlock, unknown
            VkFormat.Bc7SrgbBlock, unknown
            VkFormat.Etc2R8g8b8UnormBlock, unknown
            VkFormat.Etc2R8g8b8SrgbBlock, unknown
            VkFormat.Etc2R8g8b8a1UnormBlock, unknown
            VkFormat.Etc2R8g8b8a1SrgbBlock, unknown
            VkFormat.Etc2R8g8b8a8UnormBlock, unknown
            VkFormat.Etc2R8g8b8a8SrgbBlock, unknown
            VkFormat.EacR11UnormBlock, unknown
            VkFormat.EacR11SnormBlock, unknown
            VkFormat.EacR11g11UnormBlock, unknown
            VkFormat.EacR11g11SnormBlock, unknown
            VkFormat.Astc44UnormBlock, unknown
            VkFormat.Astc44SrgbBlock, unknown
            VkFormat.Astc54UnormBlock, unknown
            VkFormat.Astc54SrgbBlock, unknown
            VkFormat.Astc55UnormBlock, unknown
            VkFormat.Astc55SrgbBlock, unknown
            VkFormat.Astc65UnormBlock, unknown
            VkFormat.Astc65SrgbBlock, unknown
            VkFormat.Astc66UnormBlock, unknown
            VkFormat.Astc66SrgbBlock, unknown
            VkFormat.Astc85UnormBlock, unknown
            VkFormat.Astc85SrgbBlock, unknown
            VkFormat.Astc86UnormBlock, unknown
            VkFormat.Astc86SrgbBlock, unknown
            VkFormat.Astc88UnormBlock, unknown
            VkFormat.Astc88SrgbBlock, unknown
            VkFormat.Astc105UnormBlock, unknown
            VkFormat.Astc105SrgbBlock, unknown
            VkFormat.Astc106UnormBlock, unknown
            VkFormat.Astc106SrgbBlock, unknown
            VkFormat.Astc108UnormBlock, unknown
            VkFormat.Astc108SrgbBlock, unknown
            VkFormat.Astc1010UnormBlock, unknown
            VkFormat.Astc1010SrgbBlock, unknown
            VkFormat.Astc1210UnormBlock, unknown
            VkFormat.Astc1210SrgbBlock, unknown
            VkFormat.Astc1212UnormBlock, unknown
            VkFormat.Astc1212SrgbBlock, unknown       
        ]
            
    let pixelSizeInBytes =
        LookupTable.lookupTable [
            VkFormat.Undefined, 0
            VkFormat.R4g4UnormPack8, 1
            VkFormat.R4g4b4a4UnormPack16, 2
            VkFormat.B4g4r4a4UnormPack16, 2
            VkFormat.R5g6b5UnormPack16, 2
            VkFormat.B5g6r5UnormPack16, 2
            VkFormat.R5g5b5a1UnormPack16, 2
            VkFormat.B5g5r5a1UnormPack16, 2
            VkFormat.A1r5g5b5UnormPack16, 2
            VkFormat.R8Unorm, 1
            VkFormat.R8Snorm, 1
            VkFormat.R8Uscaled, 1
            VkFormat.R8Sscaled, 1
            VkFormat.R8Uint, 1
            VkFormat.R8Sint, 1
            VkFormat.R8Srgb, 1
            VkFormat.R8g8Unorm, 2
            VkFormat.R8g8Snorm, 2
            VkFormat.R8g8Uscaled, 2
            VkFormat.R8g8Sscaled, 2
            VkFormat.R8g8Uint, 2
            VkFormat.R8g8Sint, 2
            VkFormat.R8g8Srgb, 2
            VkFormat.R8g8b8Unorm, 3
            VkFormat.R8g8b8Snorm, 3
            VkFormat.R8g8b8Uscaled, 3
            VkFormat.R8g8b8Sscaled, 3
            VkFormat.R8g8b8Uint, 3
            VkFormat.R8g8b8Sint, 3
            VkFormat.R8g8b8Srgb, 3
            VkFormat.B8g8r8Unorm, 3
            VkFormat.B8g8r8Snorm, 3
            VkFormat.B8g8r8Uscaled, 3
            VkFormat.B8g8r8Sscaled, 3
            VkFormat.B8g8r8Uint, 3
            VkFormat.B8g8r8Sint, 3
            VkFormat.B8g8r8Srgb, 3
            VkFormat.R8g8b8a8Unorm, 4
            VkFormat.R8g8b8a8Snorm, 4
            VkFormat.R8g8b8a8Uscaled, 4
            VkFormat.R8g8b8a8Sscaled, 4
            VkFormat.R8g8b8a8Uint, 4
            VkFormat.R8g8b8a8Sint, 4
            VkFormat.R8g8b8a8Srgb, 4
            VkFormat.B8g8r8a8Unorm, 4
            VkFormat.B8g8r8a8Snorm, 4
            VkFormat.B8g8r8a8Uscaled, 4
            VkFormat.B8g8r8a8Sscaled, 4
            VkFormat.B8g8r8a8Uint, 4
            VkFormat.B8g8r8a8Sint, 4
            VkFormat.B8g8r8a8Srgb, 4
            VkFormat.A8b8g8r8UnormPack32, 4
            VkFormat.A8b8g8r8SnormPack32, 4
            VkFormat.A8b8g8r8UscaledPack32, 4
            VkFormat.A8b8g8r8SscaledPack32, 4
            VkFormat.A8b8g8r8UintPack32, 4
            VkFormat.A8b8g8r8SintPack32, 4
            VkFormat.A8b8g8r8SrgbPack32, 4
            VkFormat.A2r10g10b10UnormPack32, 4
            VkFormat.A2r10g10b10SnormPack32, 4
            VkFormat.A2r10g10b10UscaledPack32, 4
            VkFormat.A2r10g10b10SscaledPack32, 4
            VkFormat.A2r10g10b10UintPack32, 4
            VkFormat.A2r10g10b10SintPack32, 4
            VkFormat.A2b10g10r10UnormPack32, 4
            VkFormat.A2b10g10r10SnormPack32, 4
            VkFormat.A2b10g10r10UscaledPack32, 4
            VkFormat.A2b10g10r10SscaledPack32, 4
            VkFormat.A2b10g10r10UintPack32, 4
            VkFormat.A2b10g10r10SintPack32, 4
            VkFormat.R16Unorm, 2
            VkFormat.R16Snorm, 2
            VkFormat.R16Uscaled, 2
            VkFormat.R16Sscaled, 2
            VkFormat.R16Uint, 2
            VkFormat.R16Sint, 2
            VkFormat.R16Sfloat, 2
            VkFormat.R16g16Unorm, 4
            VkFormat.R16g16Snorm, 4
            VkFormat.R16g16Uscaled, 4
            VkFormat.R16g16Sscaled, 4
            VkFormat.R16g16Uint, 4
            VkFormat.R16g16Sint, 4
            VkFormat.R16g16Sfloat, 4
            VkFormat.R16g16b16Unorm, 6
            VkFormat.R16g16b16Snorm, 6
            VkFormat.R16g16b16Uscaled, 6
            VkFormat.R16g16b16Sscaled, 6
            VkFormat.R16g16b16Uint, 6
            VkFormat.R16g16b16Sint, 6
            VkFormat.R16g16b16Sfloat, 6
            VkFormat.R16g16b16a16Unorm, 8
            VkFormat.R16g16b16a16Snorm, 8
            VkFormat.R16g16b16a16Uscaled, 8
            VkFormat.R16g16b16a16Sscaled, 8
            VkFormat.R16g16b16a16Uint, 8
            VkFormat.R16g16b16a16Sint, 8
            VkFormat.R16g16b16a16Sfloat, 8
            VkFormat.R32Uint, 4
            VkFormat.R32Sint, 4
            VkFormat.R32Sfloat, 4
            VkFormat.R32g32Uint, 8
            VkFormat.R32g32Sint, 8
            VkFormat.R32g32Sfloat, 8
            VkFormat.R32g32b32Uint, 12
            VkFormat.R32g32b32Sint, 12
            VkFormat.R32g32b32Sfloat, 12
            VkFormat.R32g32b32a32Uint, 16
            VkFormat.R32g32b32a32Sint, 16
            VkFormat.R32g32b32a32Sfloat, 16
            VkFormat.R64Uint, 8
            VkFormat.R64Sint, 8
            VkFormat.R64Sfloat, 8
            VkFormat.R64g64Uint, 16
            VkFormat.R64g64Sint, 16
            VkFormat.R64g64Sfloat, 16
            VkFormat.R64g64b64Uint, 24
            VkFormat.R64g64b64Sint, 24
            VkFormat.R64g64b64Sfloat, 24
            VkFormat.R64g64b64a64Uint, 32
            VkFormat.R64g64b64a64Sint, 32
            VkFormat.R64g64b64a64Sfloat, 32
            VkFormat.B10g11r11UfloatPack32, 4
            VkFormat.E5b9g9r9UfloatPack32, 4
            VkFormat.D16Unorm, 2
            VkFormat.X8D24UnormPack32, 4
            VkFormat.D32Sfloat, 4
            VkFormat.S8Uint, 1
            VkFormat.D16UnormS8Uint, 3
            VkFormat.D24UnormS8Uint, 4
            VkFormat.D32SfloatS8Uint, 5
            VkFormat.Bc1RgbUnormBlock, 0
            VkFormat.Bc1RgbSrgbBlock, 0
            VkFormat.Bc1RgbaUnormBlock, 0
            VkFormat.Bc1RgbaSrgbBlock, 0
            VkFormat.Bc2UnormBlock, 0
            VkFormat.Bc2SrgbBlock, 0
            VkFormat.Bc3UnormBlock, 0
            VkFormat.Bc3SrgbBlock, 0
            VkFormat.Bc4UnormBlock, 0
            VkFormat.Bc4SnormBlock, 0
            VkFormat.Bc5UnormBlock, 0
            VkFormat.Bc5SnormBlock, 0
            VkFormat.Bc6hUfloatBlock, 0
            VkFormat.Bc6hSfloatBlock, 0
            VkFormat.Bc7UnormBlock, 0
            VkFormat.Bc7SrgbBlock, 0
            VkFormat.Etc2R8g8b8UnormBlock, 0
            VkFormat.Etc2R8g8b8SrgbBlock, 0
            VkFormat.Etc2R8g8b8a1UnormBlock, 0
            VkFormat.Etc2R8g8b8a1SrgbBlock, 0
            VkFormat.Etc2R8g8b8a8UnormBlock, 0
            VkFormat.Etc2R8g8b8a8SrgbBlock, 0
            VkFormat.EacR11UnormBlock, 0
            VkFormat.EacR11SnormBlock, 0
            VkFormat.EacR11g11UnormBlock, 0
            VkFormat.EacR11g11SnormBlock, 0
            VkFormat.Astc44UnormBlock, 0
            VkFormat.Astc44SrgbBlock, 0
            VkFormat.Astc54UnormBlock, 0
            VkFormat.Astc54SrgbBlock, 0
            VkFormat.Astc55UnormBlock, 0
            VkFormat.Astc55SrgbBlock, 0
            VkFormat.Astc65UnormBlock, 0
            VkFormat.Astc65SrgbBlock, 0
            VkFormat.Astc66UnormBlock, 0
            VkFormat.Astc66SrgbBlock, 0
            VkFormat.Astc85UnormBlock, 0
            VkFormat.Astc85SrgbBlock, 0
            VkFormat.Astc86UnormBlock, 0
            VkFormat.Astc86SrgbBlock, 0
            VkFormat.Astc88UnormBlock, 0
            VkFormat.Astc88SrgbBlock, 0
            VkFormat.Astc105UnormBlock, 0
            VkFormat.Astc105SrgbBlock, 0
            VkFormat.Astc106UnormBlock, 0
            VkFormat.Astc106SrgbBlock, 0
            VkFormat.Astc108UnormBlock, 0
            VkFormat.Astc108SrgbBlock, 0
            VkFormat.Astc1010UnormBlock, 0
            VkFormat.Astc1010SrgbBlock, 0
            VkFormat.Astc1210UnormBlock, 0
            VkFormat.Astc1210SrgbBlock, 0
            VkFormat.Astc1212UnormBlock, 0
            VkFormat.Astc1212SrgbBlock, 0       
        ]

    let toRenderbufferFormat (fmt : VkFormat) =
        fmt |> toTextureFormat |> int |> unbox<RenderbufferFormat>


    let private depthFormats = HashSet.ofList [ VkFormat.D16Unorm; VkFormat.D32Sfloat; VkFormat.X8D24UnormPack32 ]
    let private depthStencilFormats = HashSet.ofList [VkFormat.D16UnormS8Uint; VkFormat.D24UnormS8Uint; VkFormat.D32SfloatS8Uint ]

    let hasDepth (fmt : VkFormat) =
        depthFormats.Contains fmt || depthStencilFormats.Contains fmt

    let toAspect (fmt : VkFormat) =
        if depthStencilFormats.Contains fmt then VkImageAspectFlags.DepthBit ||| VkImageAspectFlags.StencilBit
        elif depthFormats.Contains fmt then VkImageAspectFlags.DepthBit
        else VkImageAspectFlags.ColorBit

    let toShaderAspect (fmt : VkFormat) =
        if depthStencilFormats.Contains fmt then VkImageAspectFlags.DepthBit 
        elif depthFormats.Contains fmt then VkImageAspectFlags.DepthBit
        else VkImageAspectFlags.ColorBit

    let toColFormat =
        let r = Col.Format.Gray
        let rg = Col.Format.NormalUV
        let rgb = Col.Format.RGB
        let rgba = Col.Format.RGBA
        let bgr = Col.Format.BGR
        let bgra = Col.Format.BGRA
        let argb = Col.Format.None
        let abgr = Col.Format.None
        let none = Col.Format.None
        let d = Col.Format.Gray
        let ds = Col.Format.GrayAlpha
        let s = Col.Format.Alpha
        let unknown = Col.Format.None
        LookupTable.lookupTable [
            VkFormat.Undefined, none
            VkFormat.R4g4UnormPack8, rg
            VkFormat.R4g4b4a4UnormPack16, rgba
            VkFormat.B4g4r4a4UnormPack16, bgra
            VkFormat.R5g6b5UnormPack16, rgb
            VkFormat.B5g6r5UnormPack16, bgr
            VkFormat.R5g5b5a1UnormPack16, rgba
            VkFormat.B5g5r5a1UnormPack16, bgra
            VkFormat.A1r5g5b5UnormPack16, argb
            VkFormat.R8Unorm, r
            VkFormat.R8Snorm, r
            VkFormat.R8Uscaled, r
            VkFormat.R8Sscaled, r
            VkFormat.R8Uint, r
            VkFormat.R8Sint, r
            VkFormat.R8Srgb, r
            VkFormat.R8g8Unorm, rg
            VkFormat.R8g8Snorm, rg
            VkFormat.R8g8Uscaled, rg
            VkFormat.R8g8Sscaled, rg
            VkFormat.R8g8Uint, rg
            VkFormat.R8g8Sint, rg
            VkFormat.R8g8Srgb, rg
            VkFormat.R8g8b8Unorm, rgb
            VkFormat.R8g8b8Snorm, rgb
            VkFormat.R8g8b8Uscaled, rgb
            VkFormat.R8g8b8Sscaled, rgb
            VkFormat.R8g8b8Uint, rgb
            VkFormat.R8g8b8Sint, rgb
            VkFormat.R8g8b8Srgb, rgb
            VkFormat.B8g8r8Unorm, bgr
            VkFormat.B8g8r8Snorm, bgr
            VkFormat.B8g8r8Uscaled, bgr
            VkFormat.B8g8r8Sscaled, bgr
            VkFormat.B8g8r8Uint, bgr
            VkFormat.B8g8r8Sint, bgr
            VkFormat.B8g8r8Srgb, bgr
            VkFormat.R8g8b8a8Unorm, rgba
            VkFormat.R8g8b8a8Snorm, rgba
            VkFormat.R8g8b8a8Uscaled, rgba
            VkFormat.R8g8b8a8Sscaled, rgba
            VkFormat.R8g8b8a8Uint, rgba
            VkFormat.R8g8b8a8Sint, rgba
            VkFormat.R8g8b8a8Srgb, rgba
            VkFormat.B8g8r8a8Unorm, bgra
            VkFormat.B8g8r8a8Snorm, bgra
            VkFormat.B8g8r8a8Uscaled, bgra
            VkFormat.B8g8r8a8Sscaled, bgra
            VkFormat.B8g8r8a8Uint, bgra
            VkFormat.B8g8r8a8Sint, bgra
            VkFormat.B8g8r8a8Srgb, bgra
            VkFormat.A8b8g8r8UnormPack32, abgr
            VkFormat.A8b8g8r8SnormPack32, abgr
            VkFormat.A8b8g8r8UscaledPack32, abgr
            VkFormat.A8b8g8r8SscaledPack32, abgr
            VkFormat.A8b8g8r8UintPack32, abgr
            VkFormat.A8b8g8r8SintPack32, abgr
            VkFormat.A8b8g8r8SrgbPack32, abgr
            VkFormat.A2r10g10b10UnormPack32, argb
            VkFormat.A2r10g10b10SnormPack32, argb
            VkFormat.A2r10g10b10UscaledPack32, argb
            VkFormat.A2r10g10b10SscaledPack32, argb
            VkFormat.A2r10g10b10UintPack32, argb
            VkFormat.A2r10g10b10SintPack32, argb
            VkFormat.A2b10g10r10UnormPack32, abgr
            VkFormat.A2b10g10r10SnormPack32, abgr
            VkFormat.A2b10g10r10UscaledPack32, abgr
            VkFormat.A2b10g10r10SscaledPack32, abgr
            VkFormat.A2b10g10r10UintPack32, abgr
            VkFormat.A2b10g10r10SintPack32, abgr
            VkFormat.R16Unorm, r
            VkFormat.R16Snorm, r
            VkFormat.R16Uscaled, r
            VkFormat.R16Sscaled, r
            VkFormat.R16Uint, r
            VkFormat.R16Sint, r
            VkFormat.R16Sfloat, r
            VkFormat.R16g16Unorm, rg
            VkFormat.R16g16Snorm, rg
            VkFormat.R16g16Uscaled, rg
            VkFormat.R16g16Sscaled, rg
            VkFormat.R16g16Uint, rg
            VkFormat.R16g16Sint, rg
            VkFormat.R16g16Sfloat, rg
            VkFormat.R16g16b16Unorm, rgb
            VkFormat.R16g16b16Snorm, rgb
            VkFormat.R16g16b16Uscaled, rgb
            VkFormat.R16g16b16Sscaled, rgb
            VkFormat.R16g16b16Uint, rgb
            VkFormat.R16g16b16Sint, rgb
            VkFormat.R16g16b16Sfloat, rgb
            VkFormat.R16g16b16a16Unorm, rgba
            VkFormat.R16g16b16a16Snorm, rgba
            VkFormat.R16g16b16a16Uscaled, rgba
            VkFormat.R16g16b16a16Sscaled, rgba
            VkFormat.R16g16b16a16Uint, rgba
            VkFormat.R16g16b16a16Sint, rgba
            VkFormat.R16g16b16a16Sfloat, rgba
            VkFormat.R32Uint, r
            VkFormat.R32Sint, r
            VkFormat.R32Sfloat, r
            VkFormat.R32g32Uint, rg
            VkFormat.R32g32Sint, rg
            VkFormat.R32g32Sfloat, rg
            VkFormat.R32g32b32Uint, rgb
            VkFormat.R32g32b32Sint, rgb
            VkFormat.R32g32b32Sfloat, rgb
            VkFormat.R32g32b32a32Uint, rgba
            VkFormat.R32g32b32a32Sint, rgba
            VkFormat.R32g32b32a32Sfloat, rgba
            VkFormat.R64Uint, r
            VkFormat.R64Sint, r
            VkFormat.R64Sfloat, r
            VkFormat.R64g64Uint, rg
            VkFormat.R64g64Sint, rg
            VkFormat.R64g64Sfloat, rg
            VkFormat.R64g64b64Uint, rgb
            VkFormat.R64g64b64Sint, rgb
            VkFormat.R64g64b64Sfloat, rgb
            VkFormat.R64g64b64a64Uint, rgba
            VkFormat.R64g64b64a64Sint, rgba
            VkFormat.R64g64b64a64Sfloat, rgba
            VkFormat.B10g11r11UfloatPack32, bgr
            VkFormat.E5b9g9r9UfloatPack32, bgr
            VkFormat.D16Unorm, d
            VkFormat.X8D24UnormPack32, d
            VkFormat.D32Sfloat, ds
            VkFormat.S8Uint, s
            VkFormat.D16UnormS8Uint, ds
            VkFormat.D24UnormS8Uint, ds
            VkFormat.D32SfloatS8Uint, ds
            VkFormat.Bc1RgbUnormBlock, rgb
            VkFormat.Bc1RgbSrgbBlock, rgb
            VkFormat.Bc1RgbaUnormBlock, rgba
            VkFormat.Bc1RgbaSrgbBlock, rgba
            VkFormat.Bc2UnormBlock, unknown
            VkFormat.Bc2SrgbBlock, rgb
            VkFormat.Bc3UnormBlock, unknown
            VkFormat.Bc3SrgbBlock, rgb
            VkFormat.Bc4UnormBlock, unknown
            VkFormat.Bc4SnormBlock, unknown
            VkFormat.Bc5UnormBlock, unknown
            VkFormat.Bc5SnormBlock, unknown
            VkFormat.Bc6hUfloatBlock, unknown
            VkFormat.Bc6hSfloatBlock, unknown
            VkFormat.Bc7UnormBlock, unknown
            VkFormat.Bc7SrgbBlock, rgb
            VkFormat.Etc2R8g8b8UnormBlock, rgb
            VkFormat.Etc2R8g8b8SrgbBlock, rgb
            VkFormat.Etc2R8g8b8a1UnormBlock, rgba
            VkFormat.Etc2R8g8b8a1SrgbBlock, rgba
            VkFormat.Etc2R8g8b8a8UnormBlock, rgba
            VkFormat.Etc2R8g8b8a8SrgbBlock, rgba
            VkFormat.EacR11UnormBlock, r
            VkFormat.EacR11SnormBlock, r
            VkFormat.EacR11g11UnormBlock, rg
            VkFormat.EacR11g11SnormBlock, rg
            VkFormat.Astc44UnormBlock, unknown
            VkFormat.Astc44SrgbBlock, rgb
            VkFormat.Astc54UnormBlock, unknown
            VkFormat.Astc54SrgbBlock, rgb
            VkFormat.Astc55UnormBlock, unknown
            VkFormat.Astc55SrgbBlock, rgb
            VkFormat.Astc65UnormBlock, unknown
            VkFormat.Astc65SrgbBlock, rgb
            VkFormat.Astc66UnormBlock, unknown
            VkFormat.Astc66SrgbBlock, rgb
            VkFormat.Astc85UnormBlock, unknown
            VkFormat.Astc85SrgbBlock, rgb
            VkFormat.Astc86UnormBlock, unknown
            VkFormat.Astc86SrgbBlock, rgb
            VkFormat.Astc88UnormBlock, unknown
            VkFormat.Astc88SrgbBlock, rgb
            VkFormat.Astc105UnormBlock, unknown
            VkFormat.Astc105SrgbBlock, rgb
            VkFormat.Astc106UnormBlock, unknown
            VkFormat.Astc106SrgbBlock, rgb
            VkFormat.Astc108UnormBlock, unknown
            VkFormat.Astc108SrgbBlock, rgb
            VkFormat.Astc1010UnormBlock, unknown
            VkFormat.Astc1010SrgbBlock, rgb
            VkFormat.Astc1210UnormBlock, unknown
            VkFormat.Astc1210SrgbBlock, rgb
            VkFormat.Astc1212UnormBlock, unknown
            VkFormat.Astc1212SrgbBlock, rgb   
        ]

    let channels =
        LookupTable.lookupTable [
            VkFormat.Undefined, -1
            VkFormat.R4g4UnormPack8, 2
            VkFormat.R4g4b4a4UnormPack16, 4
            VkFormat.B4g4r4a4UnormPack16, 4
            VkFormat.R5g6b5UnormPack16, 3
            VkFormat.B5g6r5UnormPack16, 3
            VkFormat.R5g5b5a1UnormPack16, 4
            VkFormat.B5g5r5a1UnormPack16, 4
            VkFormat.A1r5g5b5UnormPack16, 4
            VkFormat.R8Unorm, 1
            VkFormat.R8Snorm, 1
            VkFormat.R8Uscaled, 1
            VkFormat.R8Sscaled, 1
            VkFormat.R8Uint, 1
            VkFormat.R8Sint, 1
            VkFormat.R8Srgb, 1
            VkFormat.R8g8Unorm, 2
            VkFormat.R8g8Snorm, 2
            VkFormat.R8g8Uscaled, 2
            VkFormat.R8g8Sscaled, 2
            VkFormat.R8g8Uint, 2
            VkFormat.R8g8Sint, 2
            VkFormat.R8g8Srgb, 2
            VkFormat.R8g8b8Unorm, 3
            VkFormat.R8g8b8Snorm, 3
            VkFormat.R8g8b8Uscaled, 3
            VkFormat.R8g8b8Sscaled, 3
            VkFormat.R8g8b8Uint, 3
            VkFormat.R8g8b8Sint, 3
            VkFormat.R8g8b8Srgb, 3
            VkFormat.B8g8r8Unorm, 3
            VkFormat.B8g8r8Snorm, 3
            VkFormat.B8g8r8Uscaled, 3
            VkFormat.B8g8r8Sscaled, 3
            VkFormat.B8g8r8Uint, 3
            VkFormat.B8g8r8Sint, 3
            VkFormat.B8g8r8Srgb, 3
            VkFormat.R8g8b8a8Unorm, 4
            VkFormat.R8g8b8a8Snorm, 4
            VkFormat.R8g8b8a8Uscaled, 4
            VkFormat.R8g8b8a8Sscaled, 4
            VkFormat.R8g8b8a8Uint, 4
            VkFormat.R8g8b8a8Sint, 4
            VkFormat.R8g8b8a8Srgb, 4
            VkFormat.B8g8r8a8Unorm, 4
            VkFormat.B8g8r8a8Snorm, 4
            VkFormat.B8g8r8a8Uscaled, 4
            VkFormat.B8g8r8a8Sscaled, 4
            VkFormat.B8g8r8a8Uint, 4
            VkFormat.B8g8r8a8Sint, 4
            VkFormat.B8g8r8a8Srgb, 4
            VkFormat.A8b8g8r8UnormPack32, 4
            VkFormat.A8b8g8r8SnormPack32, 4
            VkFormat.A8b8g8r8UscaledPack32, 4
            VkFormat.A8b8g8r8SscaledPack32, 4
            VkFormat.A8b8g8r8UintPack32, 4
            VkFormat.A8b8g8r8SintPack32, 4
            VkFormat.A8b8g8r8SrgbPack32, 4
            VkFormat.A2r10g10b10UnormPack32, 4
            VkFormat.A2r10g10b10SnormPack32, 4
            VkFormat.A2r10g10b10UscaledPack32, 4
            VkFormat.A2r10g10b10SscaledPack32, 4
            VkFormat.A2r10g10b10UintPack32, 4
            VkFormat.A2r10g10b10SintPack32, 4
            VkFormat.A2b10g10r10UnormPack32, 4
            VkFormat.A2b10g10r10SnormPack32, 4
            VkFormat.A2b10g10r10UscaledPack32, 4
            VkFormat.A2b10g10r10SscaledPack32, 4
            VkFormat.A2b10g10r10UintPack32, 4
            VkFormat.A2b10g10r10SintPack32, 4
            VkFormat.R16Unorm, 1
            VkFormat.R16Snorm, 1
            VkFormat.R16Uscaled, 1
            VkFormat.R16Sscaled, 1
            VkFormat.R16Uint, 1
            VkFormat.R16Sint, 1
            VkFormat.R16Sfloat, 1
            VkFormat.R16g16Unorm, 2
            VkFormat.R16g16Snorm, 2
            VkFormat.R16g16Uscaled, 2
            VkFormat.R16g16Sscaled, 2
            VkFormat.R16g16Uint, 2
            VkFormat.R16g16Sint, 2
            VkFormat.R16g16Sfloat, 2
            VkFormat.R16g16b16Unorm, 3
            VkFormat.R16g16b16Snorm, 3
            VkFormat.R16g16b16Uscaled, 3
            VkFormat.R16g16b16Sscaled, 3
            VkFormat.R16g16b16Uint, 3
            VkFormat.R16g16b16Sint, 3
            VkFormat.R16g16b16Sfloat, 3
            VkFormat.R16g16b16a16Unorm, 4
            VkFormat.R16g16b16a16Snorm, 4
            VkFormat.R16g16b16a16Uscaled, 4
            VkFormat.R16g16b16a16Sscaled, 4
            VkFormat.R16g16b16a16Uint, 4
            VkFormat.R16g16b16a16Sint, 4
            VkFormat.R16g16b16a16Sfloat, 4
            VkFormat.R32Uint, 1
            VkFormat.R32Sint, 1
            VkFormat.R32Sfloat, 1
            VkFormat.R32g32Uint, 2
            VkFormat.R32g32Sint, 2
            VkFormat.R32g32Sfloat, 2
            VkFormat.R32g32b32Uint, 3
            VkFormat.R32g32b32Sint, 3
            VkFormat.R32g32b32Sfloat, 3
            VkFormat.R32g32b32a32Uint, 4
            VkFormat.R32g32b32a32Sint, 4
            VkFormat.R32g32b32a32Sfloat, 4
            VkFormat.R64Uint, 1
            VkFormat.R64Sint, 1
            VkFormat.R64Sfloat, 1
            VkFormat.R64g64Uint, 2
            VkFormat.R64g64Sint, 2
            VkFormat.R64g64Sfloat, 2
            VkFormat.R64g64b64Uint, 3
            VkFormat.R64g64b64Sint, 3
            VkFormat.R64g64b64Sfloat, 3
            VkFormat.R64g64b64a64Uint, 4
            VkFormat.R64g64b64a64Sint, 4
            VkFormat.R64g64b64a64Sfloat, 4
            VkFormat.B10g11r11UfloatPack32, 3
            VkFormat.E5b9g9r9UfloatPack32, 3
            VkFormat.D16Unorm, 1
            VkFormat.X8D24UnormPack32, 1
            VkFormat.D32Sfloat, 1
            VkFormat.S8Uint, 1
            VkFormat.D16UnormS8Uint, 2
            VkFormat.D24UnormS8Uint, 2
            VkFormat.D32SfloatS8Uint, 2
            VkFormat.Bc1RgbUnormBlock, -1
            VkFormat.Bc1RgbSrgbBlock, -1
            VkFormat.Bc1RgbaUnormBlock, -1
            VkFormat.Bc1RgbaSrgbBlock, -1
            VkFormat.Bc2UnormBlock, -1
            VkFormat.Bc2SrgbBlock, -1
            VkFormat.Bc3UnormBlock, -1
            VkFormat.Bc3SrgbBlock, -1
            VkFormat.Bc4UnormBlock, -1
            VkFormat.Bc4SnormBlock, -1
            VkFormat.Bc5UnormBlock, -1
            VkFormat.Bc5SnormBlock, -1
            VkFormat.Bc6hUfloatBlock, -1
            VkFormat.Bc6hSfloatBlock, -1
            VkFormat.Bc7UnormBlock, -1
            VkFormat.Bc7SrgbBlock, -1
            VkFormat.Etc2R8g8b8UnormBlock, -1
            VkFormat.Etc2R8g8b8SrgbBlock, -1
            VkFormat.Etc2R8g8b8a1UnormBlock, -1
            VkFormat.Etc2R8g8b8a1SrgbBlock, -1
            VkFormat.Etc2R8g8b8a8UnormBlock, -1
            VkFormat.Etc2R8g8b8a8SrgbBlock, -1
            VkFormat.EacR11UnormBlock, -1
            VkFormat.EacR11SnormBlock, -1
            VkFormat.EacR11g11UnormBlock, -1
            VkFormat.EacR11g11SnormBlock, -1
            VkFormat.Astc44UnormBlock, -1
            VkFormat.Astc44SrgbBlock, -1
            VkFormat.Astc54UnormBlock, -1
            VkFormat.Astc54SrgbBlock, -1
            VkFormat.Astc55UnormBlock, -1
            VkFormat.Astc55SrgbBlock, -1
            VkFormat.Astc65UnormBlock, -1
            VkFormat.Astc65SrgbBlock, -1
            VkFormat.Astc66UnormBlock, -1
            VkFormat.Astc66SrgbBlock, -1
            VkFormat.Astc85UnormBlock, -1
            VkFormat.Astc85SrgbBlock, -1
            VkFormat.Astc86UnormBlock, -1
            VkFormat.Astc86SrgbBlock, -1
            VkFormat.Astc88UnormBlock, -1
            VkFormat.Astc88SrgbBlock, -1
            VkFormat.Astc105UnormBlock, -1
            VkFormat.Astc105SrgbBlock, -1
            VkFormat.Astc106UnormBlock, -1
            VkFormat.Astc106SrgbBlock, -1
            VkFormat.Astc108UnormBlock, -1
            VkFormat.Astc108SrgbBlock, -1
            VkFormat.Astc1010UnormBlock, -1
            VkFormat.Astc1010SrgbBlock, -1
            VkFormat.Astc1210UnormBlock, -1
            VkFormat.Astc1210SrgbBlock, -1
            VkFormat.Astc1212UnormBlock, -1
            VkFormat.Astc1212SrgbBlock, -1      
        ]

    let sizeInBytes =

        LookupTable.lookupTable [
            VkFormat.Undefined, -1
            VkFormat.R4g4UnormPack8, 1
            VkFormat.R4g4b4a4UnormPack16, 2
            VkFormat.B4g4r4a4UnormPack16, 2
            VkFormat.R5g6b5UnormPack16, 2
            VkFormat.B5g6r5UnormPack16, 2
            VkFormat.R5g5b5a1UnormPack16, 2
            VkFormat.B5g5r5a1UnormPack16, 2
            VkFormat.A1r5g5b5UnormPack16, 2
            VkFormat.R8Unorm, 1
            VkFormat.R8Snorm, 1
            VkFormat.R8Uscaled, 1
            VkFormat.R8Sscaled, 1
            VkFormat.R8Uint, 1
            VkFormat.R8Sint, 1
            VkFormat.R8Srgb, 1
            VkFormat.R8g8Unorm, 2
            VkFormat.R8g8Snorm, 2
            VkFormat.R8g8Uscaled, 2
            VkFormat.R8g8Sscaled, 2
            VkFormat.R8g8Uint, 2
            VkFormat.R8g8Sint, 2
            VkFormat.R8g8Srgb, 2
            VkFormat.R8g8b8Unorm, 3
            VkFormat.R8g8b8Snorm, 3
            VkFormat.R8g8b8Uscaled, 3
            VkFormat.R8g8b8Sscaled, 3
            VkFormat.R8g8b8Uint, 3
            VkFormat.R8g8b8Sint, 3
            VkFormat.R8g8b8Srgb, 3
            VkFormat.B8g8r8Unorm, 3
            VkFormat.B8g8r8Snorm, 3
            VkFormat.B8g8r8Uscaled, 3
            VkFormat.B8g8r8Sscaled, 3
            VkFormat.B8g8r8Uint, 3
            VkFormat.B8g8r8Sint, 3
            VkFormat.B8g8r8Srgb, 3
            VkFormat.R8g8b8a8Unorm, 4
            VkFormat.R8g8b8a8Snorm, 4
            VkFormat.R8g8b8a8Uscaled, 4
            VkFormat.R8g8b8a8Sscaled, 4
            VkFormat.R8g8b8a8Uint, 4
            VkFormat.R8g8b8a8Sint, 4
            VkFormat.R8g8b8a8Srgb, 4
            VkFormat.B8g8r8a8Unorm, 4
            VkFormat.B8g8r8a8Snorm, 4
            VkFormat.B8g8r8a8Uscaled, 4
            VkFormat.B8g8r8a8Sscaled, 4
            VkFormat.B8g8r8a8Uint, 4
            VkFormat.B8g8r8a8Sint, 4
            VkFormat.B8g8r8a8Srgb, 4
            VkFormat.A8b8g8r8UnormPack32, 4
            VkFormat.A8b8g8r8SnormPack32, 4
            VkFormat.A8b8g8r8UscaledPack32, 4
            VkFormat.A8b8g8r8SscaledPack32, 4
            VkFormat.A8b8g8r8UintPack32, 4
            VkFormat.A8b8g8r8SintPack32, 4
            VkFormat.A8b8g8r8SrgbPack32, 4
            VkFormat.A2r10g10b10UnormPack32, 4
            VkFormat.A2r10g10b10SnormPack32, 4
            VkFormat.A2r10g10b10UscaledPack32, 4
            VkFormat.A2r10g10b10SscaledPack32, 4
            VkFormat.A2r10g10b10UintPack32, 4
            VkFormat.A2r10g10b10SintPack32, 4
            VkFormat.A2b10g10r10UnormPack32, 4
            VkFormat.A2b10g10r10SnormPack32, 4
            VkFormat.A2b10g10r10UscaledPack32, 4
            VkFormat.A2b10g10r10SscaledPack32, 4
            VkFormat.A2b10g10r10UintPack32, 4
            VkFormat.A2b10g10r10SintPack32, 4
            VkFormat.R16Unorm, 2
            VkFormat.R16Snorm, 2
            VkFormat.R16Uscaled, 2
            VkFormat.R16Sscaled, 2
            VkFormat.R16Uint, 2
            VkFormat.R16Sint, 2
            VkFormat.R16Sfloat, 2
            VkFormat.R16g16Unorm, 4
            VkFormat.R16g16Snorm, 4
            VkFormat.R16g16Uscaled, 4
            VkFormat.R16g16Sscaled, 4
            VkFormat.R16g16Uint, 4
            VkFormat.R16g16Sint, 4
            VkFormat.R16g16Sfloat, 4
            VkFormat.R16g16b16Unorm, 6
            VkFormat.R16g16b16Snorm, 6
            VkFormat.R16g16b16Uscaled, 6
            VkFormat.R16g16b16Sscaled, 6
            VkFormat.R16g16b16Uint, 6
            VkFormat.R16g16b16Sint, 6
            VkFormat.R16g16b16Sfloat, 6
            VkFormat.R16g16b16a16Unorm, 8
            VkFormat.R16g16b16a16Snorm, 8
            VkFormat.R16g16b16a16Uscaled, 8
            VkFormat.R16g16b16a16Sscaled, 8
            VkFormat.R16g16b16a16Uint, 8
            VkFormat.R16g16b16a16Sint, 8
            VkFormat.R16g16b16a16Sfloat, 8
            VkFormat.R32Uint, 4
            VkFormat.R32Sint, 4
            VkFormat.R32Sfloat, 4
            VkFormat.R32g32Uint, 8
            VkFormat.R32g32Sint, 8
            VkFormat.R32g32Sfloat, 8
            VkFormat.R32g32b32Uint, 12
            VkFormat.R32g32b32Sint, 12
            VkFormat.R32g32b32Sfloat, 12
            VkFormat.R32g32b32a32Uint, 16
            VkFormat.R32g32b32a32Sint, 16
            VkFormat.R32g32b32a32Sfloat, 16
            VkFormat.R64Uint, 8
            VkFormat.R64Sint, 8
            VkFormat.R64Sfloat, 8
            VkFormat.R64g64Uint, 16
            VkFormat.R64g64Sint, 16
            VkFormat.R64g64Sfloat, 16
            VkFormat.R64g64b64Uint, 24
            VkFormat.R64g64b64Sint, 24
            VkFormat.R64g64b64Sfloat, 24
            VkFormat.R64g64b64a64Uint, 32
            VkFormat.R64g64b64a64Sint, 32
            VkFormat.R64g64b64a64Sfloat, 32
            VkFormat.B10g11r11UfloatPack32, 4
            VkFormat.E5b9g9r9UfloatPack32, 4
            VkFormat.D16Unorm, 2
            VkFormat.X8D24UnormPack32, 4
            VkFormat.D32Sfloat, 4
            VkFormat.S8Uint, 1
            VkFormat.D16UnormS8Uint, 3
            VkFormat.D24UnormS8Uint, 4
            VkFormat.D32SfloatS8Uint, 5
            VkFormat.Bc1RgbUnormBlock, -1
            VkFormat.Bc1RgbSrgbBlock, -1
            VkFormat.Bc1RgbaUnormBlock, -1
            VkFormat.Bc1RgbaSrgbBlock, -1
            VkFormat.Bc2UnormBlock, -1
            VkFormat.Bc2SrgbBlock, -1
            VkFormat.Bc3UnormBlock, -1
            VkFormat.Bc3SrgbBlock, -1
            VkFormat.Bc4UnormBlock, -1
            VkFormat.Bc4SnormBlock, -1
            VkFormat.Bc5UnormBlock, -1
            VkFormat.Bc5SnormBlock, -1
            VkFormat.Bc6hUfloatBlock, -1
            VkFormat.Bc6hSfloatBlock, -1
            VkFormat.Bc7UnormBlock, -1
            VkFormat.Bc7SrgbBlock, -1
            VkFormat.Etc2R8g8b8UnormBlock, -1
            VkFormat.Etc2R8g8b8SrgbBlock, -1
            VkFormat.Etc2R8g8b8a1UnormBlock, -1
            VkFormat.Etc2R8g8b8a1SrgbBlock, -1
            VkFormat.Etc2R8g8b8a8UnormBlock, -1
            VkFormat.Etc2R8g8b8a8SrgbBlock, -1
            VkFormat.EacR11UnormBlock, -1
            VkFormat.EacR11SnormBlock, -1
            VkFormat.EacR11g11UnormBlock, -1
            VkFormat.EacR11g11SnormBlock, -1
            VkFormat.Astc44UnormBlock, -1
            VkFormat.Astc44SrgbBlock, -1
            VkFormat.Astc54UnormBlock, -1
            VkFormat.Astc54SrgbBlock, -1
            VkFormat.Astc55UnormBlock, -1
            VkFormat.Astc55SrgbBlock, -1
            VkFormat.Astc65UnormBlock, -1
            VkFormat.Astc65SrgbBlock, -1
            VkFormat.Astc66UnormBlock, -1
            VkFormat.Astc66SrgbBlock, -1
            VkFormat.Astc85UnormBlock, -1
            VkFormat.Astc85SrgbBlock, -1
            VkFormat.Astc86UnormBlock, -1
            VkFormat.Astc86SrgbBlock, -1
            VkFormat.Astc88UnormBlock, -1
            VkFormat.Astc88SrgbBlock, -1
            VkFormat.Astc105UnormBlock, -1
            VkFormat.Astc105SrgbBlock, -1
            VkFormat.Astc106UnormBlock, -1
            VkFormat.Astc106SrgbBlock, -1
            VkFormat.Astc108UnormBlock, -1
            VkFormat.Astc108SrgbBlock, -1
            VkFormat.Astc1010UnormBlock, -1
            VkFormat.Astc1010SrgbBlock, -1
            VkFormat.Astc1210UnormBlock, -1
            VkFormat.Astc1210SrgbBlock, -1
            VkFormat.Astc1212UnormBlock, -1
            VkFormat.Astc1212SrgbBlock, -1               
        ]

    let expectedType =
        LookupTable.lookupTable [
            VkFormat.Undefined, null
            VkFormat.R4g4UnormPack8, typeof<uint8>
            VkFormat.R4g4b4a4UnormPack16, typeof<uint16>
            VkFormat.B4g4r4a4UnormPack16, typeof<uint16>
            VkFormat.R5g6b5UnormPack16, typeof<uint16>
            VkFormat.B5g6r5UnormPack16, typeof<uint16>
            VkFormat.R5g5b5a1UnormPack16, typeof<uint16>
            VkFormat.B5g5r5a1UnormPack16, typeof<uint16>
            VkFormat.A1r5g5b5UnormPack16, typeof<uint16>
            VkFormat.R8Unorm, typeof<uint8>
            VkFormat.R8Snorm, typeof<int8>
            VkFormat.R8Uscaled, typeof<uint8>
            VkFormat.R8Sscaled, typeof<int8>
            VkFormat.R8Uint, typeof<uint8>
            VkFormat.R8Sint, typeof<int8>
            VkFormat.R8Srgb, typeof<uint8>
            VkFormat.R8g8Unorm, typeof<uint8>
            VkFormat.R8g8Snorm, typeof<int8>
            VkFormat.R8g8Uscaled, typeof<uint8>
            VkFormat.R8g8Sscaled, typeof<int8>
            VkFormat.R8g8Uint, typeof<uint8>
            VkFormat.R8g8Sint, typeof<int8>
            VkFormat.R8g8Srgb, typeof<uint8>
            VkFormat.R8g8b8Unorm, typeof<uint8>
            VkFormat.R8g8b8Snorm, typeof<int8>
            VkFormat.R8g8b8Uscaled, typeof<uint8>
            VkFormat.R8g8b8Sscaled, typeof<int8>
            VkFormat.R8g8b8Uint, typeof<uint8>
            VkFormat.R8g8b8Sint, typeof<int8>
            VkFormat.R8g8b8Srgb, typeof<uint8>
            VkFormat.B8g8r8Unorm, typeof<uint8>
            VkFormat.B8g8r8Snorm, typeof<int8>
            VkFormat.B8g8r8Uscaled, typeof<uint8>
            VkFormat.B8g8r8Sscaled, typeof<int8>
            VkFormat.B8g8r8Uint, typeof<uint8>
            VkFormat.B8g8r8Sint, typeof<int8>
            VkFormat.B8g8r8Srgb, typeof<uint8>
            VkFormat.R8g8b8a8Unorm, typeof<uint8>
            VkFormat.R8g8b8a8Snorm, typeof<int8>
            VkFormat.R8g8b8a8Uscaled, typeof<uint8>
            VkFormat.R8g8b8a8Sscaled, typeof<int8>
            VkFormat.R8g8b8a8Uint, typeof<uint8>
            VkFormat.R8g8b8a8Sint, typeof<int8>
            VkFormat.R8g8b8a8Srgb, typeof<uint8>
            VkFormat.B8g8r8a8Unorm, typeof<uint8>
            VkFormat.B8g8r8a8Snorm, typeof<int8>
            VkFormat.B8g8r8a8Uscaled, typeof<uint8>
            VkFormat.B8g8r8a8Sscaled, typeof<int8>
            VkFormat.B8g8r8a8Uint, typeof<uint8>
            VkFormat.B8g8r8a8Sint, typeof<int8>
            VkFormat.B8g8r8a8Srgb, typeof<uint8>
            VkFormat.A8b8g8r8UnormPack32, typeof<uint32>
            VkFormat.A8b8g8r8SnormPack32, typeof<uint32>
            VkFormat.A8b8g8r8UscaledPack32, typeof<uint32>
            VkFormat.A8b8g8r8SscaledPack32, typeof<uint32>
            VkFormat.A8b8g8r8UintPack32, typeof<uint32>
            VkFormat.A8b8g8r8SintPack32, typeof<uint32>
            VkFormat.A8b8g8r8SrgbPack32, typeof<uint32>
            VkFormat.A2r10g10b10UnormPack32, typeof<uint32>
            VkFormat.A2r10g10b10SnormPack32, typeof<uint32>
            VkFormat.A2r10g10b10UscaledPack32, typeof<uint32>
            VkFormat.A2r10g10b10SscaledPack32, typeof<uint32>
            VkFormat.A2r10g10b10UintPack32, typeof<uint32>
            VkFormat.A2r10g10b10SintPack32, typeof<uint32>
            VkFormat.A2b10g10r10UnormPack32, typeof<uint32>
            VkFormat.A2b10g10r10SnormPack32, typeof<uint32>
            VkFormat.A2b10g10r10UscaledPack32, typeof<uint32>
            VkFormat.A2b10g10r10SscaledPack32, typeof<uint32>
            VkFormat.A2b10g10r10UintPack32, typeof<uint32>
            VkFormat.A2b10g10r10SintPack32, typeof<uint32>
            VkFormat.R16Unorm, typeof<uint16>
            VkFormat.R16Snorm, typeof<int16>
            VkFormat.R16Uscaled, typeof<uint16>
            VkFormat.R16Sscaled, typeof<int16>
            VkFormat.R16Uint, typeof<uint16>
            VkFormat.R16Sint, typeof<int16>
            VkFormat.R16Sfloat, typeof<float16>
            VkFormat.R16g16Unorm, typeof<uint16>
            VkFormat.R16g16Snorm, typeof<int16>
            VkFormat.R16g16Uscaled, typeof<uint16>
            VkFormat.R16g16Sscaled, typeof<int16>
            VkFormat.R16g16Uint, typeof<uint16>
            VkFormat.R16g16Sint, typeof<int16>
            VkFormat.R16g16Sfloat, typeof<float16>
            VkFormat.R16g16b16Unorm, typeof<uint16>
            VkFormat.R16g16b16Snorm, typeof<int16>
            VkFormat.R16g16b16Uscaled, typeof<uint16>
            VkFormat.R16g16b16Sscaled, typeof<int16>
            VkFormat.R16g16b16Uint, typeof<uint16>
            VkFormat.R16g16b16Sint, typeof<int16>
            VkFormat.R16g16b16Sfloat, typeof<float16>
            VkFormat.R16g16b16a16Unorm, typeof<uint16>
            VkFormat.R16g16b16a16Snorm, typeof<int16>
            VkFormat.R16g16b16a16Uscaled, typeof<uint16>
            VkFormat.R16g16b16a16Sscaled, typeof<int16>
            VkFormat.R16g16b16a16Uint, typeof<uint16>
            VkFormat.R16g16b16a16Sint, typeof<int16>
            VkFormat.R16g16b16a16Sfloat, typeof<float16>
            VkFormat.R32Uint, typeof<uint32>
            VkFormat.R32Sint, typeof<int32>
            VkFormat.R32Sfloat, typeof<float32>
            VkFormat.R32g32Uint, typeof<uint32>
            VkFormat.R32g32Sint, typeof<int32>
            VkFormat.R32g32Sfloat, typeof<float32>
            VkFormat.R32g32b32Uint, typeof<uint32>
            VkFormat.R32g32b32Sint, typeof<int32>
            VkFormat.R32g32b32Sfloat, typeof<float32>
            VkFormat.R32g32b32a32Uint, typeof<uint32>
            VkFormat.R32g32b32a32Sint, typeof<int32>
            VkFormat.R32g32b32a32Sfloat, typeof<float32>
            VkFormat.R64Uint, typeof<uint64>
            VkFormat.R64Sint, typeof<int64>
            VkFormat.R64Sfloat, typeof<float>
            VkFormat.R64g64Uint, typeof<uint64>
            VkFormat.R64g64Sint, typeof<int64>
            VkFormat.R64g64Sfloat, typeof<float>
            VkFormat.R64g64b64Uint, typeof<uint64>
            VkFormat.R64g64b64Sint, typeof<int64>
            VkFormat.R64g64b64Sfloat, typeof<float>
            VkFormat.R64g64b64a64Uint, typeof<uint64>
            VkFormat.R64g64b64a64Sint, typeof<int64>
            VkFormat.R64g64b64a64Sfloat, typeof<float>
            VkFormat.B10g11r11UfloatPack32, typeof<uint32>
            VkFormat.E5b9g9r9UfloatPack32, typeof<uint32>
            VkFormat.D16Unorm, typeof<uint16>
            VkFormat.X8D24UnormPack32, typeof<uint32>
            VkFormat.D32Sfloat, null
            VkFormat.S8Uint, typeof<uint8>
            VkFormat.D16UnormS8Uint, null
            VkFormat.D24UnormS8Uint, typeof<uint32>
            VkFormat.D32SfloatS8Uint, null
            VkFormat.Bc1RgbUnormBlock, null
            VkFormat.Bc1RgbSrgbBlock, null
            VkFormat.Bc1RgbaUnormBlock, null
            VkFormat.Bc1RgbaSrgbBlock, null
            VkFormat.Bc2UnormBlock, null
            VkFormat.Bc2SrgbBlock, null
            VkFormat.Bc3UnormBlock, null
            VkFormat.Bc3SrgbBlock, null
            VkFormat.Bc4UnormBlock, null
            VkFormat.Bc4SnormBlock, null
            VkFormat.Bc5UnormBlock, null
            VkFormat.Bc5SnormBlock, null
            VkFormat.Bc6hUfloatBlock, null
            VkFormat.Bc6hSfloatBlock, null
            VkFormat.Bc7UnormBlock, null
            VkFormat.Bc7SrgbBlock, null
            VkFormat.Etc2R8g8b8UnormBlock, null
            VkFormat.Etc2R8g8b8SrgbBlock, null
            VkFormat.Etc2R8g8b8a1UnormBlock, null
            VkFormat.Etc2R8g8b8a1SrgbBlock, null
            VkFormat.Etc2R8g8b8a8UnormBlock, null
            VkFormat.Etc2R8g8b8a8SrgbBlock, null
            VkFormat.EacR11UnormBlock, null
            VkFormat.EacR11SnormBlock, null
            VkFormat.EacR11g11UnormBlock, null
            VkFormat.EacR11g11SnormBlock, null
            VkFormat.Astc44UnormBlock, null
            VkFormat.Astc44SrgbBlock, null
            VkFormat.Astc54UnormBlock, null
            VkFormat.Astc54SrgbBlock, null
            VkFormat.Astc55UnormBlock, null
            VkFormat.Astc55SrgbBlock, null
            VkFormat.Astc65UnormBlock, null
            VkFormat.Astc65SrgbBlock, null
            VkFormat.Astc66UnormBlock, null
            VkFormat.Astc66SrgbBlock, null
            VkFormat.Astc85UnormBlock, null
            VkFormat.Astc85SrgbBlock, null
            VkFormat.Astc86UnormBlock, null
            VkFormat.Astc86SrgbBlock, null
            VkFormat.Astc88UnormBlock, null
            VkFormat.Astc88SrgbBlock, null
            VkFormat.Astc105UnormBlock, null
            VkFormat.Astc105SrgbBlock, null
            VkFormat.Astc106UnormBlock, null
            VkFormat.Astc106SrgbBlock, null
            VkFormat.Astc108UnormBlock, null
            VkFormat.Astc108SrgbBlock, null
            VkFormat.Astc1010UnormBlock, null
            VkFormat.Astc1010SrgbBlock, null
            VkFormat.Astc1210UnormBlock, null
            VkFormat.Astc1210SrgbBlock, null
            VkFormat.Astc1212UnormBlock, null
            VkFormat.Astc1212SrgbBlock, null               
        ]

    let ofPixFormat (fmt : PixFormat) (t : TextureParams) =
        TextureFormat.ofPixFormat fmt t |> ofTextureFormat

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module VkImageLayout =
    open KHRSwapchain

    let toAccessFlags =
        LookupTable.lookupTable [
            VkImageLayout.Undefined,                        VkAccessFlags.None
            VkImageLayout.General,                          VkAccessFlags.None
            VkImageLayout.ColorAttachmentOptimal,           VkAccessFlags.ColorAttachmentWriteBit
            VkImageLayout.DepthStencilAttachmentOptimal,    VkAccessFlags.DepthStencilAttachmentWriteBit
            VkImageLayout.DepthStencilReadOnlyOptimal,      VkAccessFlags.DepthStencilAttachmentReadBit
            VkImageLayout.ShaderReadOnlyOptimal,            VkAccessFlags.ShaderReadBit
            VkImageLayout.TransferSrcOptimal,               VkAccessFlags.TransferReadBit
            VkImageLayout.TransferDstOptimal,               VkAccessFlags.TransferWriteBit
            VkImageLayout.Preinitialized,                   VkAccessFlags.HostWriteBit
            VkImageLayout.PresentSrcKhr,                    VkAccessFlags.MemoryReadBit
        ]
        
    let toSrcStageFlags =
        LookupTable.lookupTable [
            VkImageLayout.Undefined,                        VkPipelineStageFlags.HostBit
            VkImageLayout.General,                          VkPipelineStageFlags.HostBit
            VkImageLayout.ColorAttachmentOptimal,           VkPipelineStageFlags.ColorAttachmentOutputBit
            VkImageLayout.DepthStencilAttachmentOptimal,    VkPipelineStageFlags.LateFragmentTestsBit
            VkImageLayout.DepthStencilReadOnlyOptimal,      VkPipelineStageFlags.LateFragmentTestsBit
            VkImageLayout.ShaderReadOnlyOptimal,            VkPipelineStageFlags.FragmentShaderBit
            VkImageLayout.TransferSrcOptimal,               VkPipelineStageFlags.TransferBit
            VkImageLayout.TransferDstOptimal,               VkPipelineStageFlags.TransferBit
            VkImageLayout.Preinitialized,                   VkPipelineStageFlags.HostBit
            VkImageLayout.PresentSrcKhr,                    VkPipelineStageFlags.TransferBit
        ]
        
    let toDstStageFlags =
        LookupTable.lookupTable [
            VkImageLayout.Undefined,                        VkPipelineStageFlags.HostBit
            VkImageLayout.General,                          VkPipelineStageFlags.HostBit
            VkImageLayout.ColorAttachmentOptimal,           VkPipelineStageFlags.ColorAttachmentOutputBit
            VkImageLayout.DepthStencilAttachmentOptimal,    VkPipelineStageFlags.EarlyFragmentTestsBit
            VkImageLayout.DepthStencilReadOnlyOptimal,      VkPipelineStageFlags.EarlyFragmentTestsBit
            VkImageLayout.ShaderReadOnlyOptimal,            VkPipelineStageFlags.VertexShaderBit
            VkImageLayout.TransferSrcOptimal,               VkPipelineStageFlags.TransferBit
            VkImageLayout.TransferDstOptimal,               VkPipelineStageFlags.TransferBit
            VkImageLayout.Preinitialized,                   VkPipelineStageFlags.HostBit
            VkImageLayout.PresentSrcKhr,                    VkPipelineStageFlags.TransferBit
        ]