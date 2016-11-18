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

[<StructuralComparison; StructuralEquality>]
type PrimitiveType =
    | Bool
    | Int of width : int * signed : bool
    | Float of width : int
    | Vector of compType : PrimitiveType * dim : int
    | Matrix of colType : PrimitiveType * dim : int

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module PrimitiveType =
    let bool = PrimitiveType.Bool
    let int8 = PrimitiveType.Int(8, true)
    let int16 = PrimitiveType.Int(16, true)
    let int32 = PrimitiveType.Int(32, true)
    let int64 = PrimitiveType.Int(64, true)
    let uint8 = PrimitiveType.Int(8, false)
    let uint16 = PrimitiveType.Int(16, false)
    let uint32 = PrimitiveType.Int(32, false)
    let uint64 = PrimitiveType.Int(64, false)

    let float16 = PrimitiveType.Float(16)
    let float32 = PrimitiveType.Float(32)
    let float64 = PrimitiveType.Float(64)

    let c3b = PrimitiveType.Vector(uint8, 3)
    let c4b = PrimitiveType.Vector(uint8, 4)
    let c3us = PrimitiveType.Vector(uint16, 3)
    let c4us = PrimitiveType.Vector(uint16, 4)
    let c3ui = PrimitiveType.Vector(uint32, 3)
    let c4ui = PrimitiveType.Vector(uint32, 4)

    let v2i = PrimitiveType.Vector(int32, 2)
    let v3i = PrimitiveType.Vector(int32, 3)
    let v4i = PrimitiveType.Vector(int32, 4)
    let v2f = PrimitiveType.Vector(float32, 2)
    let v3f = PrimitiveType.Vector(float32, 3)
    let v4f = PrimitiveType.Vector(float32, 4)
    let v2d = PrimitiveType.Vector(float64, 2)
    let v3d = PrimitiveType.Vector(float64, 3)
    let v4d = PrimitiveType.Vector(float64, 4)

    let m22f = PrimitiveType.Matrix(v2f, 2)
    let m33f = PrimitiveType.Matrix(v3f, 3)
    let m44f = PrimitiveType.Matrix(v4f, 4)
    let m22d = PrimitiveType.Matrix(v2d, 2)
    let m33d = PrimitiveType.Matrix(v3d, 3)
    let m44d = PrimitiveType.Matrix(v4d, 4)

    let toType =
        LookupTable.lookupTable [
            bool,       typeof<bool>
            int8,       typeof<int8>
            int16,      typeof<int16>
            int32,      typeof<int32>
            int64,      typeof<int64>
            uint8,      typeof<uint8>
            uint16,     typeof<uint16>
            uint32,     typeof<uint32>
            uint64,     typeof<uint64>

            float16,    typeof<float16>
            float32,    typeof<float32>
            float64,    typeof<float>

            c3b,        typeof<C3b>
            c4b,        typeof<C4b>
            c3us,       typeof<C3us>
            c4us,       typeof<C4us>
            c3ui,       typeof<C3ui>
            c4ui,       typeof<C4ui>

            v2i,        typeof<V2i>
            v3i,        typeof<V3i>
            v4i,        typeof<V4i>
            v2f,        typeof<V2f>
            v3f,        typeof<V3f>
            v4f,        typeof<V4f>
            v2d,        typeof<V2d>
            v3d,        typeof<V3d>
            v4d,        typeof<V4d>

            m22f,       typeof<M22f>
            m33f,       typeof<M34f>
            m44f,       typeof<M44f>
            m22d,       typeof<M22d>
            m33d,       typeof<M34d>
            m44d,       typeof<M44d>
        ]
    

type UniformType =
    | Struct of UniformBufferLayout
    | Primitive of t : PrimitiveType * size : int * align : int
    | Array of elementType : UniformType * length : int * size : int * align : int

    member x.align =
        match x with
            | Struct l -> l.align
            | Primitive(_,s,a) -> a
            | Array(_,_,s,a) -> a

    member x.size =
        match x with
            | Struct l -> l.size
            | Primitive(_,s,a) -> s
            | Array(e,l, s, a) -> s

and UniformBufferField =
    {
        name        : string
        fieldType   : UniformType
        offset      : int
        size        : int
    }

and UniformBufferLayout = 
    { 
        align : int
        size : int
        fields : list<UniformBufferField>
    }

module UniformBufferLayoutStd140 =
    open System.Collections.Generic
    open FShade.SpirV

                
    let rec toUniformType (t : ShaderType) : UniformType =
        match t with
            | ShaderType.Int(w,signed) ->
                let size = w / 8
                // both the size and alignment are the size of the scalar
                // in basic machine types (e.g. sizeof<int>)
                
                UniformType.Primitive(PrimitiveType.Int(w, signed), size, size)

            | Float(w) -> 
                // both the size and alignment are the size of the scalar
                // in basic machine types (e.g. sizeof<int>)
                let size = w / 8
                UniformType.Primitive(PrimitiveType.Float(w), size, size)

            | Bool -> 
                UniformType.Primitive(PrimitiveType.Bool, 4, 4)

            | Vector(bt,3) -> 
                // both the size and alignment are 4 times the size
                // of the underlying scalar type.
                match toUniformType bt with
                    | Primitive(t,s,a) -> 
                        UniformType.Primitive(PrimitiveType.Vector(t, 3), s * 4, s * 4)
                    | o ->
                        UniformType.Struct(structLayout [bt, "X", []; bt, "Y", []; bt, "Z", []])

            | Vector(bt,d) ->  
                // both the size and alignment are <d> times the size
                // of the underlying scalar type.
                match toUniformType bt with
                    | Primitive(t,s,a) -> 
                        UniformType.Primitive(PrimitiveType.Vector(t, d), s * d, s * d)
                    | o ->
                        let fields = [bt, "X", []; bt, "Y", []; bt, "Z", []; bt, "Z", []] |> List.take d
                        UniformType.Struct(structLayout fields)


            | Array(bt, len) -> 
                // the size of each element in the array will be the size
                // of the element type rounded up to a multiple of the size
                // of a vec4. This is also the array's alignment.
                // The array's size will be this rounded-up element's size
                // times the number of elements in the array.
                let et = toUniformType bt
                let physicalSize = et.size

                let size =
                    if physicalSize % 16 = 0 then physicalSize
                    else physicalSize + 16 - (physicalSize % 16)

                UniformType.Array(et, len, size * len, size)


            | Matrix(colType, cols) ->
                // same layout as an array of N vectors each with 
                // R components, where N is the total number of columns
                // present.
                match toUniformType colType with
                    | Primitive(t,physicalSize,a)  -> 
                        let size =
                            if physicalSize % 16 = 0 then physicalSize
                            else physicalSize + 16 - (physicalSize % 16)

                        UniformType.Primitive(PrimitiveType.Matrix(t, cols), size * cols, size)
                    | o ->
                        let fields = [colType, "C0", []; colType, "C1", []; colType, "C2", []; colType, "C3", []] |> List.take cols
                        UniformType.Struct(structLayout fields)

            | Ptr(_,t) -> 
                toUniformType t

            | Image(sampledType, dim,depth,arr, ms, sam, fmt) -> 
                failf "cannot determine size for image type"

            | Sampler -> 
                failf "cannot determine size for sampler type"

            | Struct(name,fields) -> 
                failf "cannot determine size for void type"

            | Void -> 
                failf "cannot determine size for void type"

            | Function _ ->
                failf "cannot use function in UniformBuffer"

            | SampledImage _ ->
                failf "cannot use SampledImage in UniformBuffer"

    and structLayout (fields : list<ShaderType * string * list<SpirV.Decoration * uint32[]>>) : UniformBufferLayout =
        let mutable currentOffset = 0
        let mutable offsets : Map<string, int> = Map.empty
        let mutable types : Map<string, ShaderType> = Map.empty
        let mutable biggestFieldSize = 0
        let mutable biggestFieldAlign = 0

        let fields = 
            fields |> List.map (fun (t,n,dec) ->
                let t = toUniformType t
                let align = t.align
                let size = t.size

                // align the field offset
                if currentOffset % align <> 0 then
                    currentOffset <- currentOffset + align - (currentOffset % align)

                // keep track of the biggest member
                if size > biggestFieldSize then
                    biggestFieldSize <- size
                    biggestFieldAlign <- align

                let result = 
                    {
                        name        = n
                        fieldType   = t
                        offset      = currentOffset
                        size        = size
                    }

                // store the member's offset
                currentOffset <- currentOffset + size
                result
            )

        // structure alignment will be the alignment for
        // the biggest structure member, according to the previous
        // rules, rounded up to a multiple of the size of a vec4.
        // each structure will start on this alignment, and its size will
        // be the space needed by its members, according to the previous
        // rules, rounded up to a multiple of the structure alignment.
        let structAlign =
            if biggestFieldAlign % 16 = 0 then biggestFieldAlign
            else biggestFieldAlign + 16 - (biggestFieldAlign % 16)

        let structSize =
            if currentOffset % structAlign = 0 then currentOffset
            else currentOffset + structAlign - (currentOffset % structAlign)

        { align = structAlign; size = structSize; fields = fields }

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

        let mem = device.DeviceMemory.Alloc(align, alignedSize)
        let buffer = device.CreateBuffer(VkBufferUsageFlags.UniformBufferBit ||| VkBufferUsageFlags.TransferDstBit, mem)

        UniformBuffer(device, buffer.Handle, buffer.Memory, storage, layout)

    let upload (b : UniformBuffer) (device : Device) =
        use t = device.ResourceToken
        t.Enqueue
            { new Command() with
                member x.Enqueue cmd = 
                    cmd.AppendCommand()
                    VkRaw.vkCmdUpdateBuffer(cmd.Handle, b.Handle, 0UL, uint64 b.Storage.Size, b.Storage.Pointer)
                    Disposable.Empty
            }

    let delete (b : UniformBuffer) (device : Device) =
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
        abstract member Write : IAdaptiveObject * IAdaptiveObject * nativeint -> unit

    type IWriter<'a> =
        inherit IWriter
        abstract member Write : IAdaptiveObject * 'a * nativeint -> unit



    [<AbstractClass>]
    type AbstractWriter<'a>() =
        abstract member Write : IAdaptiveObject * 'a * nativeint -> unit


        interface IWriter with
            member x.Write(caller, value, ptr) =
                let value = unbox<IMod<'a>> value
                x.Write(caller, value.GetValue caller, ptr)

        interface IWriter<'a> with
            member x.Write(caller, value, ptr) = x.Write(caller, value, ptr)


    type SingleValueWriter<'a when 'a : unmanaged>(offset : int) =
        inherit AbstractWriter<'a>()

        override x.Write(caller : IAdaptiveObject, value : 'a, ptr : nativeint) =
            let ptr = NativePtr.ofNativeInt (ptr + nativeint offset)
            NativePtr.write ptr value

    type ConversionWriter<'a, 'b when 'b : unmanaged>(offset : int, convert : 'a -> 'b) =
        inherit AbstractWriter<'a>()

        override x.Write(caller : IAdaptiveObject, value : 'a, ptr : nativeint) =
            let mutable ptr = NativePtr.ofNativeInt (ptr + nativeint offset)
            let res = convert value
            NativePtr.write ptr res

    type NoConversionWriter<'a when 'a : unmanaged>(offset : int) =
        inherit AbstractWriter<'a>()

        override x.Write(caller : IAdaptiveObject, value : 'a, ptr : nativeint) =
            let mutable ptr = NativePtr.ofNativeInt (ptr + nativeint offset)
            NativePtr.write ptr value

    type MultiWriter<'a>(writers : list<IWriter<'a>>) =
        inherit AbstractWriter<'a>()

        override x.Write(caller : IAdaptiveObject, value : 'a, ptr : nativeint) =
            for w in writers do w.Write(caller, value, ptr)

        new (writers : list<IWriter>) = MultiWriter<'a>(writers |> List.map unbox<IWriter<'a>>)

    type PropertyWriter<'a, 'b>(prop : PropertyInfo, inner : IWriter<'b>) =
        inherit AbstractWriter<'a>()

        override x.Write(caller : IAdaptiveObject, value : 'a, target : nativeint) =
            let v = prop.GetValue(value) |> unbox<'b>
            inner.Write(caller, v, target)

    type FieldWriter<'a, 'b>(prop : FieldInfo, inner : IWriter<'b>) =
        inherit AbstractWriter<'a>()

        override x.Write(caller : IAdaptiveObject, value : 'a, target : nativeint) =
            let v = prop.GetValue(value) |> unbox<'b>
            inner.Write(caller, v, target)

    type SequenceWriter<'s, 'a when 's :> seq<'a>>(inner : IWriter<'a>[]) =
        inherit AbstractWriter<'s>()

        let rec run (caller : IAdaptiveObject) (target : nativeint) (index : int) (e : System.Collections.Generic.IEnumerator<'a>) =
            if index >= inner.Length then 
                ()
            else
                if e.MoveNext() then
                    let v = e.Current
                    inner.[index].Write(caller, v, target)
                    run caller target (index + 1) e
                else
                    ()

        override x.Write(caller : IAdaptiveObject, value : 's, target : nativeint) =
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