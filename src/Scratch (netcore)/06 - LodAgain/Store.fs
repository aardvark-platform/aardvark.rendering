namespace LodAgain

open System
open System.IO
open System.IO.MemoryMappedFiles
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop
open Aardvark.Base

#nowarn "9"

[<AutoOpen>]
module private Utilities = 

    let magic = Guid.Parse "2995381e-4838-4496-af7c-e41f092bdf60"
    let currentVersion = Version(0,1,0)

    [<StructLayout(LayoutKind.Sequential, Size = 256)>]
    type Header =
        struct
            val mutable public Magic : Guid
            val mutable public MajorVersion : int
            val mutable public MinorVersion : int
            val mutable public Patch : int
            val mutable public FileSize : int64
            val mutable public Capacity : int
            val mutable public Free : int
            val mutable public Count : int

            member x.Version = Version(x.MajorVersion, x.MinorVersion, x.Patch)

            new(v : Version, s : int64, c : int) = { Magic = magic; MajorVersion = v.Major; MinorVersion = v.Minor; Patch = v.Build; FileSize = s; Capacity = c; Free = 0; Count = 0 }

        end

    type HeaderCheckResult =
        | Success 
        | InvalidFile
        | WrongFileSize
        | WrongVersion of Version
        
    [<StructLayout(LayoutKind.Sequential, Size = 32)>]
    type Entry =
        struct
            val mutable public Key : Guid
            val mutable public Offset : int64
            val mutable public Size : int64
            val mutable public Next : int64
        end

type FileDict(info : FileInfo) =
    static let minCap = 1024

    static let checkHeader (info : FileInfo) =
        if info.Exists && info.Length >= int64 sizeof<Header> then
            let arr : byte[] = Array.zeroCreate sizeof<Header>
            use s = info.OpenRead()
            s.Read(arr, 0, sizeof<Header>) |> ignore
            let gc = GCHandle.Alloc(arr, GCHandleType.Pinned)
            let header =
                try NativePtr.read (NativePtr.ofNativeInt<Header> (gc.AddrOfPinnedObject()))
                finally gc.Free()
            if header.Magic <> magic then   
                InvalidFile
            elif currentVersion.Major <> header.MajorVersion || currentVersion.Minor <> header.MinorVersion then
                WrongVersion header.Version
            elif header.FileSize <> info.Length then
                WrongFileSize
            else
                Success
        else
            InvalidFile

    static let fileSize (cap : int) =
        int64 sizeof<Header> +
        int64 cap * (int64 sizeof<Entry>) +
        int64 cap * (int64 sizeof<int64>)

    do
        if info.Exists then
            let res = checkHeader info
            if res <> Success then failwithf "error opening %A: %A" info.FullName res

    let access = if info.Exists && info.IsReadOnly then MemoryMappedFileAccess.Read else MemoryMappedFileAccess.ReadWrite

    let id = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes info.FullName)
    let mutable handle, isNew =
        if info.Exists then 
            MemoryMappedFile.CreateFromFile(info.FullName, FileMode.Open, id, info.Length, access), false
        else 
            let f = MemoryMappedFile.CreateFromFile(info.FullName, FileMode.OpenOrCreate, id, fileSize minCap, access)
            info.Refresh()
            f, true

    let mutable view = handle.CreateViewAccessor(0L, info.Length, access)
    let mutable ptr = view.SafeMemoryMappedViewHandle.DangerousGetHandle()

    let withHeader (f : nativeptr<Header> -> 'r) : 'r =
        try f (NativePtr.ofNativeInt ptr)
        finally view.Flush()
        
    let getHeader (f : Header -> 'r) : 'r =
        try f (NativePtr.read (NativePtr.ofNativeInt ptr))
        finally view.Flush()

    let updateHeader (f : Header -> Header * 'r) : 'r =
        try 
            let h, r = f (NativePtr.read (NativePtr.ofNativeInt ptr))
            NativePtr.write (NativePtr.ofNativeInt ptr) h
            r
        finally 
            view.Flush()

    let mutable capacity =
        if isNew then 
            Marshal.Set(ptr, 0, info.Length)
            withHeader (fun pHeader -> NativePtr.write pHeader (Header(currentVersion, info.Length, minCap)))
            minCap
        else
            withHeader (fun pHeader ->
                let h = NativePtr.read pHeader
                h.Capacity
            )

    let withKeys (f : nativeptr<Entry> -> 'r) : 'r =
        try f (NativePtr.ofNativeInt (ptr + nativeint sizeof<Header>))
        finally view.Flush()
        
    let withTable (f : nativeptr<int> -> 'r) : 'r =
        try f (NativePtr.ofNativeInt (ptr + nativeint sizeof<Header> + nativeint capacity * nativeint sizeof<Entry>))
        finally view.Flush()


    let resize(newCapacity : int) =
        if newCapacity <> capacity then
            let s = fileSize newCapacity
            view.Dispose()
            handle.Dispose()
            handle <- MemoryMappedFile.CreateFromFile(info.FullName, FileMode.Open, id, s, access)
            view <- handle.CreateViewAccessor(0L, info.Length, access)
            ptr <- view.SafeMemoryMappedViewHandle.DangerousGetHandle()
            withHeader (fun pHeader -> 
                let header = NativePtr.read pHeader
                NativePtr.write pHeader (Header(header.Version, s, newCapacity))
            )
            info.Refresh()





    let allocEntry () =
        updateHeader (fun h ->
            let mutable h = h
            let f = h.Free - 1
            let entryId = 
                if f >= 0 then
                    withKeys (fun pEntry ->
                        let r = NativePtr.get pEntry f
                        h.Free <- int r.Next
                        f
                    )
                elif h.Count < h.Capacity then
                    withKeys (fun pEntry ->
                        let eid = h.Count
                        h.Count <- eid + 1
                        eid
                    )
                else
                    failwith "resize"

                
            h.Count <- h.Count + 1
            h, entryId
        )

    let writeEntry (id : int) (e : Entry) =
        withKeys (fun pKeys ->
            NativePtr.set pKeys id e
        )


    member x.TryGetValue(id : Guid, [<Out>] offset : byref<int64>, [<Out>] size : byref<int64>) =
        let hash = uint32 (id.GetHashCode())
        let slot = int (hash % uint32 capacity)
        let o = withTable (fun pTable -> NativePtr.get pTable slot - 1)
        Log.warn "entry: %A" o
        if o < 0 then
            false
        else
            let mutable i = o
            let mutable e = Entry()
            let mutable found = false
            withKeys (fun pEntry ->
                e <- NativePtr.get pEntry i
                while e.Next <> 0L && not found do
                    if e.Key = id then
                        found <- true
                    else
                        i <- int e.Next - 1
                        e <- NativePtr.get pEntry i
            )

            if found || e.Key = id then
                offset <- e.Offset
                size <- e.Size
                true
            else
                false

    member x.Add(id : Guid, offset : int64, size : int64) =
        let hash = uint32 (id.GetHashCode())
        let slot = int (hash % uint32 capacity)

        let o = withTable (fun pTable -> NativePtr.get pTable slot - 1)
        if o < 0 then
            let entryId = allocEntry()
            Log.warn "entry: %A" entryId
            writeEntry entryId (Entry(Key = id, Offset = offset, Size = size, Next = 0L))
            withTable (fun pTable -> NativePtr.set pTable slot (1 + entryId))
            true
        else
            let mutable i = o
            let mutable e = Entry()
            let mutable found = false
            withKeys (fun pEntry ->
                e <- NativePtr.get pEntry i
                while e.Next <> 0L && not found do
                    if e.Key = id then
                        found <- true
                    else
                        i <- int e.Next - 1
                        e <- NativePtr.get pEntry i
            )

            if found || e.Key = id then
                let entryId = i
                writeEntry entryId (Entry(Key = id, Offset = offset, Size = size, Next = e.Next))
                false
            else
                let newId = allocEntry()
                Log.warn "entry: %A" newId
                withKeys (fun pEntry ->
                    e.Next <- 1L + int64 newId
                    NativePtr.set pEntry i e
                    NativePtr.set pEntry newId (Entry(Key = id, Offset = offset, Size = size, Next = e.Next))
                )
                true

    member x.Remove(id : Guid) =
        let hash = uint32 (id.GetHashCode())
        let slot = int (hash % uint32 capacity)

        let o = withTable (fun pTable -> NativePtr.get pTable slot - 1)
        if o >= 0 then
            let mutable i = o
            let mutable e = Entry()
            let mutable found = false
            withKeys (fun pEntry ->
                e <- NativePtr.get pEntry i
                while e.Next <> 0L && not found do
                    if e.Key = id then
                        found <- true
                    else
                        i <- int e.Next - 1
                        e <- NativePtr.get pEntry i
            )

            if found || e.Key = id then
                let cnt = 
                    updateHeader (fun h ->
                        let mutable h = h

                        //if e.Next <> 0L then
                        //withKeys (fun pEntry -> 
                            
                        //)

                        h.Count <- h.Count - 1
                        if i <> h.Count - 1 then
                            if h.Free <> 0 then
                                let f = h.Free - 1
                                withKeys (fun pEntry -> 
                                    let mutable oe = NativePtr.get pEntry f
                                    oe.Next <- 1L + int64 i
                                    NativePtr.set pEntry f oe
                                )
                            else
                                h.Free <- 1 + i
                            
                        h, h.Count
                    )




                true
            else
                false

        else
            false
    member private x.Dispose(disposing : bool) =
        if disposing then GC.SuppressFinalize x
        view.Dispose()
        handle.Dispose()

    member x.Dispose() = x.Dispose(true)
    override x.Finalize() = x.Dispose(false)

    interface IDisposable with
        member x.Dispose() = x.Dispose()


module FileDict =
    let test() =
        let id = Guid.Parse "e668ff07-b6a6-4cbb-95f2-ccbb8353edbe"
        use d = new FileDict(FileInfo @"C:\Users\Schorsch\Desktop\test.bin")
        
        match d.TryGetValue id with
            | (true, o, s) -> Log.line "got: %A %A" o s
            | _ ->
                let o = 1024L
                let s = 256L
                d.Add(id, o, s) |> ignore
                Log.line "add: %A %A" o s
