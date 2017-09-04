namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop

#nowarn "9"
#nowarn "51"

type UnmanagedStruct(ptr : nativeint, size : int) =
    let mutable ptr = ptr
    let mutable size = size

    member x.Pointer = ptr
    member x.Size = size

    member x.WriteUnsafe(offset : int, value : 'a) =
        let ptr = (ptr + nativeint offset) |> NativePtr.ofNativeInt<'a>
        NativePtr.write ptr value

    member x.Write(offset : int, value : 'a) =
        if ptr = 0n then
            raise <| ObjectDisposedException("UnmanagedStruct")

        if offset < 0 || offset + sizeof<'a> > size then
            raise <| IndexOutOfRangeException(sprintf "%d < 0 || %d > %d" offset (offset + sizeof<'a>) size)

        let ptr = (ptr + nativeint offset) |> NativePtr.ofNativeInt<'a>
        NativePtr.write ptr value

    member x.Read(offset : int) : 'a =
        if ptr = 0n then
            raise <| ObjectDisposedException("UnmanagedStruct")

        if offset < 0 || offset + sizeof<'a> > size then
            raise <| IndexOutOfRangeException()

        let ptr = (ptr + nativeint offset) |> NativePtr.ofNativeInt<'a>
        NativePtr.read ptr

    member x.Dispose() =
        if ptr <> 0n then
            Marshal.FreeHGlobal ptr
            ptr <- 0n
            size <- 0

    interface IDisposable with
        member x.Dispose() = x.Dispose()

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module UnmanagedStruct =
    
    let Null = new UnmanagedStruct(0n, 0)

    let alloc (size : int) =
        let ptr = Marshal.AllocHGlobal size
        new UnmanagedStruct(ptr, size)

    let inline free (s : UnmanagedStruct) =
        s.Dispose()
    
    let inline write (s : UnmanagedStruct) (offset : int) (value : 'a) =
        s.Write(offset, value)

    let inline writeUnsafe (s : UnmanagedStruct) (offset : int) (value : 'a) =
        s.WriteUnsafe(offset, value)


    let inline read (s : UnmanagedStruct) (offset : int) =
        s.Read(offset)




type UniformBuffer =
    class
        inherit Buffer
        val mutable public Storage : UnmanagedStruct
        val mutable public Layout : UniformBufferLayout

        new(device : Device, handle : VkBuffer, mem : DevicePtr, storage : UnmanagedStruct, layout : UniformBufferLayout) = 
            { inherit Buffer(device, handle, mem); Storage = storage; Layout = layout }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module UniformBuffer =
    let create (layout : UniformBufferLayout) (device : Device) =
        let storage = UnmanagedStruct.alloc layout.size

        let align = device.MinUniformBufferOffsetAlignment
        let alignedSize = Alignment.next align (int64 layout.size)

        let buffer = device.CreateBuffer(VkBufferUsageFlags.UniformBufferBit ||| VkBufferUsageFlags.TransferDstBit, alignedSize)

        UniformBuffer(device, buffer.Handle, buffer.Memory, storage, layout)

    let upload (b : UniformBuffer) (device : Device) =
        use t = device.Token
        t.Enqueue
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue cmd = 
                    cmd.AppendCommand()
                    VkRaw.vkCmdUpdateBuffer(cmd.Handle, b.Handle, 0UL, uint64 b.Storage.Size, b.Storage.Pointer)
                    Disposable.Empty
            }

    let delete (b : UniformBuffer) (device : Device) =
        if b <> Unchecked.defaultof<_> then
            device.Delete(b :> Buffer)
            UnmanagedStruct.free b.Storage
            b.Storage <- UnmanagedStruct.Null


[<AbstractClass; Sealed; Extension>]
type ContextUniformBufferExtensions private() =
    [<Extension>]
    static member inline CreateUniformBuffer(this : Device, layout : UniformBufferLayout) =
        this |> UniformBuffer.create layout

    [<Extension>]
    static member inline Upload(this : Device, b : UniformBuffer) =
        this |> UniformBuffer.upload b

    [<Extension>]
    static member inline Delete(this : Device, b : UniformBuffer) =
        this |> UniformBuffer.delete b       


module UniformWriters =
    open Microsoft.FSharp.NativeInterop
    open Aardvark.Base.Incremental
    open System.Reflection

    type IWriter = 
        abstract member Write : AdaptiveToken * IAdaptiveObject * nativeint -> unit

    type IWriter<'a> =
        inherit IWriter
        abstract member Write : AdaptiveToken * 'a * nativeint -> unit



    [<AbstractClass>]
    type AbstractWriter<'a>() =
        abstract member Write : AdaptiveToken * 'a * nativeint -> unit


        interface IWriter with
            member x.Write(caller, value, ptr) =
                let value = unbox<IMod<'a>> value
                x.Write(caller, value.GetValue caller, ptr)

        interface IWriter<'a> with
            member x.Write(caller, value, ptr) = x.Write(caller, value, ptr)


    type SingleValueWriter<'a when 'a : unmanaged>(offset : int) =
        inherit AbstractWriter<'a>()

        override x.Write(caller : AdaptiveToken, value : 'a, ptr : nativeint) =
            let ptr = NativePtr.ofNativeInt (ptr + nativeint offset)
            NativePtr.write ptr value

    type ConversionWriter<'a, 'b when 'b : unmanaged>(offset : int, convert : 'a -> 'b) =
        inherit AbstractWriter<'a>()

        override x.Write(caller : AdaptiveToken, value : 'a, ptr : nativeint) =
            let mutable ptr = NativePtr.ofNativeInt (ptr + nativeint offset)
            let res = convert value
            NativePtr.write ptr res

    type NoConversionWriter<'a when 'a : unmanaged>(offset : int) =
        inherit AbstractWriter<'a>()

        override x.Write(caller : AdaptiveToken, value : 'a, ptr : nativeint) =
            let mutable ptr = NativePtr.ofNativeInt (ptr + nativeint offset)
            NativePtr.write ptr value

    type MultiWriter<'a>(writers : list<IWriter<'a>>) =
        inherit AbstractWriter<'a>()

        override x.Write(caller : AdaptiveToken, value : 'a, ptr : nativeint) =
            for w in writers do w.Write(caller, value, ptr)

        new (writers : list<IWriter>) = MultiWriter<'a>(writers |> List.map unbox<IWriter<'a>>)

    type PropertyWriter<'a, 'b>(prop : PropertyInfo, inner : IWriter<'b>) =
        inherit AbstractWriter<'a>()

        override x.Write(caller : AdaptiveToken, value : 'a, target : nativeint) =
            let v = prop.GetValue(value) |> unbox<'b>
            inner.Write(caller, v, target)

    type FieldWriter<'a, 'b>(prop : FieldInfo, inner : IWriter<'b>) =
        inherit AbstractWriter<'a>()

        override x.Write(caller : AdaptiveToken, value : 'a, target : nativeint) =
            let v = prop.GetValue(value) |> unbox<'b>
            inner.Write(caller, v, target)

    type SequenceWriter<'s, 'a when 's :> seq<'a>>(inner : IWriter<'a>[]) =
        inherit AbstractWriter<'s>()

        let rec run (caller : AdaptiveToken) (target : nativeint) (index : int) (e : System.Collections.Generic.IEnumerator<'a>) =
            if index >= inner.Length then 
                ()
            else
                if e.MoveNext() then
                    let v = e.Current
                    inner.[index].Write(caller, v, target)
                    run caller target (index + 1) e
                else
                    ()

        override x.Write(caller : AdaptiveToken, value : 's, target : nativeint) =
            use e = (value :> seq<'a>).GetEnumerator()
            run caller target 0 e

        new(writers : list<IWriter>) =
            SequenceWriter<'s, 'a>(writers |> List.map unbox |> List.toArray)

    module private List =
        let rec mapOption (f : 'a -> Option<'b>) (l : list<'a>) =
            match l with
                | [] -> Some []
                | h :: rest ->
                    match f h, mapOption f rest with
                        | Some h, Some t -> Some (h :: t)
                        | _ ->  None
                            

    let rec private tryCreateWriterInternal (offset : int) (target : UniformType) (tSource : Type) =
        match target with
            | UniformType.Primitive(t, s, a) ->
                let tTarget = PrimitiveType.toType t
                if tSource <> tTarget then
                    let converter = PrimitiveValueConverter.getConverter tSource tTarget

                    let tWriter = typedefof<ConversionWriter<int,int>>.MakeGenericType [|tSource; tTarget|]
                    let ctor = tWriter.GetConstructor [|typeof<int>; converter.GetType()|]

                    let writer = ctor.Invoke [|offset; converter|] |> unbox<IWriter>
                    Some writer
                else
                    let tWriter = typedefof<NoConversionWriter<int>>.MakeGenericType [|tSource |]
                    let ctor = tWriter.GetConstructor [|typeof<int>|]

                    let writer = ctor.Invoke [|offset|] |> unbox<IWriter>

                    Some writer

            | UniformType.Struct(layout) ->
                let allMembers = tSource.GetMembers(BindingFlags.NonPublic ||| BindingFlags.Public ||| BindingFlags.Instance)
                let table = allMembers |> Seq.map (fun m -> m.Name, m) |> Dictionary.ofSeq

                let memberInfos =
                    layout.fields |> List.mapOption (fun field ->
                        match table.TryGetValue field.name with
                            | (true, mem) -> Some (field, mem)
                            | _ -> None
                    )

                match memberInfos with
                    | Some list ->
                        let writers =
                            list |> List.mapOption (fun (field, mem) ->
                                match mem with
                                    | :? PropertyInfo as pi ->
                                        let inner = tryCreateWriterInternal (offset + field.offset) field.fieldType pi.PropertyType
                                        let tWriter = typedefof<PropertyWriter<int, int>>.MakeGenericType [|tSource; pi.PropertyType|]
                                        let ctor = tWriter.GetConstructor [| typeof<PropertyInfo>; typedefof<IWriter<int>>.MakeGenericType pi.PropertyType |]
                                        let writer = ctor.Invoke [|pi; inner |] |> unbox<IWriter>
                                        Some writer
                                    | :? FieldInfo as fi ->
                                        let inner = tryCreateWriterInternal (offset + field.offset) field.fieldType fi.FieldType
                                        let tWriter = typedefof<FieldWriter<int, int>>.MakeGenericType [|tSource; fi.FieldType|]
                                        let ctor = tWriter.GetConstructor [| typeof<FieldInfo>; typedefof<IWriter<int>>.MakeGenericType fi.FieldType |]
                                        let writer = ctor.Invoke [|fi; inner |] |> unbox<IWriter>
                                        Some writer
                                    | _ ->
                                        None
                            )

                        match writers with
                            | Some list ->
                                match list with
                                    | [x] -> Some x
                                    | _ ->
                                        let tWriter = typedefof<MultiWriter<_>>.MakeGenericType [|tSource|]
                                        let ctor = tWriter.GetConstructor [| typeof<list<IWriter>> |]
                                        let writer = ctor.Invoke [|list|] |> unbox<IWriter>
                                        Some writer
                            | None ->
                                None

                    | _ ->
                        None

            | UniformType.Array(itemType, len, s, a) ->
                let iface = tSource.GetInterface(typedefof<System.Collections.Generic.IEnumerable<_>>.FullName)
                if isNull iface then
                    None
                else
                    let tItem = iface.GetGenericArguments().[0]
                    let tSequence = tSource
                    let writers = 
                        List.init len id
                        |> List.mapOption (fun i ->
                            let off = offset + i * a
                            match tryCreateWriterInternal off itemType tItem  with
                                | Some writer -> Some writer
                                | _ -> None
                        )

                    let tWriter = typedefof<SequenceWriter<list<int>, int>>.MakeGenericType [|tSequence; tItem|]
                    let ctor = tWriter.GetConstructor [| typeof<list<IWriter>> |]
                    let writer = ctor.Invoke [|writers|] |> unbox<IWriter>
                    Some writer
    
    let cache = System.Collections.Concurrent.ConcurrentDictionary<int * UniformType * Type, Option<IWriter>>()

    let tryGetWriter (offset : int) (tTarget : UniformType) (tSource : Type) =
        let key = (offset, tTarget, tSource)
        cache.GetOrAdd(key, fun (offset, tTarget, tSource) ->
            tryCreateWriterInternal offset tTarget tSource
        )
    
    let getWriter (offset : int) (tTarget : UniformType) (tSource : Type) =
        match tryGetWriter offset tTarget tSource with
            | Some w -> w
            | None -> failf "could not create UniformWriter for field %A (input-type: %A)" tTarget tSource