namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Collections.Generic
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop
open Aardvark.Base
open Aardvark.Rendering

#nowarn "9"

module Config =
    let mutable showRecompile = true


[<AutoOpen>]
module private Utilities =

    type Converter<'a when 'a : unmanaged> private() =
        static let convert =
            let t = typeof<'a>
            if t.IsEnum then 
                let bt = t.GetEnumUnderlyingType()
                if bt = typeof<uint8> then fun (a : 'a[]) -> a |> Array.map unbox<uint8> |> unbox<Array>
                elif bt = typeof<int8> then fun (a : 'a[]) -> a |> Array.map unbox<int8> |> unbox<Array>
                elif bt = typeof<uint16> then fun (a : 'a[]) -> a |> Array.map unbox<uint16> |> unbox<Array>
                elif bt = typeof<int16> then fun (a : 'a[]) -> a |> Array.map unbox<int16> |> unbox<Array>
                elif bt = typeof<uint32> then fun (a : 'a[]) -> a |> Array.map unbox<uint32> |> unbox<Array>
                elif bt = typeof<int32> then fun (a : 'a[]) -> a |> Array.map unbox<int32> |> unbox<Array>
                elif bt = typeof<uint64> then fun (a : 'a[]) -> a |> Array.map unbox<uint64> |> unbox<Array>
                elif bt = typeof<int64> then fun (a : 'a[]) -> a |> Array.map unbox<int64> |> unbox<Array>
                else fun (a : 'a[]) -> a :> System.Array
            else 
                fun (a : 'a[]) -> a :> System.Array

        static member Convert(v : 'a[]) = convert v

    type NativeBuilder() =

        member inline x.Bind<'a,'r when 'a : unmanaged>(value : 'a, f : nativeptr<'a> -> 'r) =
            let ptr = NativePtr.alloc 1
            NativePtr.write ptr value
            try f ptr
            finally NativePtr.free ptr
            
        member inline x.Bind<'a,'r when 'a : unmanaged>(m : 'a[], f : nativeptr<'a> -> 'r) =
            if m.Length = 0 then
                f NativePtr.zero
            else
                let gc = GCHandle.Alloc(Converter<'a>.Convert m, GCHandleType.Pinned)
                try f (NativePtr.ofNativeInt (gc.AddrOfPinnedObject()))
                finally gc.Free()
            
        member inline x.Bind<'r>(value : string, f : nativeptr<byte> -> 'r) =
            let ptr = Marshal.StringToHGlobalAnsi value
            try f (NativePtr.ofNativeInt ptr)
            finally Marshal.FreeHGlobal ptr
            
        member inline x.Bind<'r>(values : string[], f : nativeptr<nativeptr<byte>> -> 'r) =
            let arr = values |> Array.map (Marshal.StringToHGlobalAnsi >> NativePtr.ofNativeInt<byte>)
            let gc = GCHandle.Alloc(arr, GCHandleType.Pinned)
            try
                f (NativePtr.ofNativeInt (gc.AddrOfPinnedObject()))
            finally
                gc.Free()
                for ptr in arr do Marshal.FreeHGlobal (NativePtr.toNativeInt ptr)


        member inline x.Bind<'a,'r when 'a : unmanaged>(m : Option<'a>, f : nativeptr<'a> -> 'r) =
            match m with
            | None -> 
                f NativePtr.zero
            | Some v ->
                let ptr = NativePtr.alloc 1
                NativePtr.write ptr v
                try f ptr
                finally NativePtr.free ptr

        member inline x.Bind<'a,'r when 'a : unmanaged>(m : list<'a>, f : nativeptr<'a> -> 'r) =
            match m with
            | [] ->
                f NativePtr.zero
            | _ -> 
                let sa = sizeof<'a>
                let mutable ptr = Marshal.AllocHGlobal (4 * sa)
                try 
                    let mutable cap = 4
                    let mutable cnt = 0
                    for e in m do
                        if cnt >= cap then 
                            ptr <- Marshal.ReAllocHGlobal(ptr, nativeint (2 * cap * sa))
                            cap <- cap * 2
                        NativePtr.set (NativePtr.ofNativeInt ptr) cnt e
                        cnt <- cnt + 1
                    f (NativePtr.ofNativeInt ptr)
                finally 
                    Marshal.FreeHGlobal ptr

        member inline x.Return(v : 'a) = v
        member inline x.Zero() = ()
        member inline x.Combine(l : unit, r : unit -> 'a) = r()
        member inline x.Delay(f : unit -> 'a) = f
        member inline x.Run(f : unit -> 'a) = f()

        member inline x.For(s : seq<'a>, f : 'a -> unit) =
            for e in s do f e

        member inline x.While(guard : unit -> bool, body : unit -> unit) =
            while guard() do
                body()

        member inline x.Using<'a, 'r when 'a :> IDisposable>(v : 'a, f : 'a -> 'r) =
            try
                f v
            finally
                v.Dispose()

    let native = NativeBuilder()


    type nativeptr<'a when 'a : unmanaged> with
        member x.Value
            with inline get() = NativePtr.read x
            and inline set (v : 'a) = NativePtr.write x v

        member x.Item
            with inline get(i : int) = NativePtr.get x i
            and inline set (i : int) (v : 'a) = NativePtr.set x i v

    let inline (!!) (v : nativeptr<'a>) = NativePtr.read v
    let inline (<!-) (ptr : nativeptr<'a>) (v : 'a) = NativePtr.write ptr v

    let temporary<'a, 'r when 'a : unmanaged> (f : nativeptr<'a> -> 'r) =
        native {
            let! ptr = Unchecked.defaultof<'a>
            return f ptr
        }
        
    let pin (f : nativeptr<'a> -> 'r) (v : 'a)  =
        native {
            let! ptr = v
            return f ptr
        }

    let check (str : string) (err : VkResult) =
        if err <> VkResult.Success then 
            Log.error "[Vulkan] %s (%A)" str err
            failwithf "[Vulkan] %s (%A)" str err

    let checkf (fmt : Printf.StringFormat<'a, VkResult -> unit>) =
        Printf.kprintf (fun (str : string) (res : VkResult) ->
            if res <> VkResult.Success then 
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
    //module NativePtr =
    //    let withA (f : nativeptr<'a> -> 'b) (a : 'a[]) =
    //        if a.Length = 0 then
    //            f NativePtr.zero
    //        else
    //            let gc = GCHandle.Alloc(a, GCHandleType.Pinned)
    //            try f (gc.AddrOfPinnedObject() |> NativePtr.ofNativeInt)
    //            finally gc.Free()

    //    let withOption (f : nativeptr<'a> -> 'b) (a : Option<'a>) =
    //        match a with
    //            | Some a -> [| a |] |> withA f
    //            | None -> f NativePtr.zero


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

    /// Converts a bit field to another given a conversion table
    let inline convertFlags< ^T, ^U when ^T : comparison and ^T :> Enum and
                                         ^U : (static member (|||) : ^U -> ^U -> ^U)> (lookup : Map< ^T, ^U>) (none : ^U) (value : ^T) =
        let mutable result = none

        lookup |> Map.iter (fun src dst ->
            if value.HasFlag src then result <- result ||| dst
        )

        result

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

[<Struct>]
type nativeptr =
    {
        Type : Type
        Handle : nativeint
    }

[<AutoOpen>]
module UntypedNativePtrExtensions =

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module NativePtr =

        /// Converts the given untyped nativeptr to a nativeptr<'T>
        /// Fails if the wrapped pointer is not of the expected type.
        let typed<'T when 'T : unmanaged> (ptr : nativeptr) : nativeptr<'T> =
            if ptr.Type = typeof<'T> then
                NativePtr.ofNativeInt ptr.Handle
            else
                failwithf "cannot cast nativeptr, expected type %A but has type %A" typeof<'T> ptr.Type

        /// Creates an untyped nativeptr from a nativeptr<'T>
        let untyped (ptr : nativeptr<'T>) =
            { Type = typeof<'T>; Handle = NativePtr.toNativeInt ptr }


/// Utility struct to build chains of Vulkan structs connected via their pNext fields.
type VkStructChain =
    val mutable Chain : list<nativeptr>

    new() = { Chain = [] }

    /// Returns the handle of the chain head.
    member x.Handle =
        match x.Chain with
        | [] -> 0n
        | ptr::_ -> ptr.Handle

    /// Returns the length of the chain.
    member x.Length =
        List.length x.Chain

    /// Clears the chain, freeing all handles.
    member x.Clear() =
        x.Chain |> List.iter (fun ptr -> Marshal.FreeHGlobal ptr.Handle )
        x.Chain <- []

    /// Adds a struct to the beginning of the chain.
    /// Returns a pointer to the passed struct, which is valid until the chain is cleared or disposed.
    member inline x.Add< ^T when ^T : unmanaged and ^T : (member set_pNext : nativeint -> unit)>(obj : ^T) =
        let ptr = NativePtr.alloc 1

        let mutable tmp = obj
        (^T : (member set_pNext : nativeint -> unit) (tmp, x.Handle))
        x.Chain <- NativePtr.untyped ptr :: x.Chain

        NativePtr.write ptr tmp
        ptr

    /// Adds an empty struct to the beginning of the chain.
    /// Returns a pointer to the new struct, which is valid until the chain is cleared or disposed.
    member inline x.Add< ^T when ^T : unmanaged and ^T : (member set_pNext : nativeint -> unit) and ^T : (static member Empty : ^T)>() =
        let empty = (^T : (static member Empty : ^T) ())
        x.Add(empty)

    interface IDisposable with
        member x.Dispose() = x.Clear()

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module VkStructChain =

    /// Creats an empty struct chain.
    let empty() =
        new VkStructChain()

    /// Casts the chain to a nativeptr. Fails if type of the head does not match.
    let toNativePtr (chain : VkStructChain) : nativeptr<'T> =
        match chain.Chain with
        | [] -> NativePtr.zero
        | ptr::_ -> NativePtr.typed ptr

    /// Adds a struct to the beginning of the chain.
    let inline add (value : ^T) (chain : VkStructChain) =
        chain.Add(value) |> ignore
        chain

    /// Adds an empty struct to the beginning of the chain.
    let inline addEmpty< ^T when ^T : unmanaged and ^T : (member set_pNext : nativeint -> unit) and ^T : (static member Empty : ^T)> (chain : VkStructChain) =
        chain.Add< ^T>() |> ignore
        chain
