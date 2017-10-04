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
            { inherit Buffer(device, handle, mem, mem.Size); Storage = storage; Layout = layout }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module UniformBuffer =
    let create (layout : UniformBufferLayout) (device : Device) =
        match layout.size with
            | Fixed size ->
                let storage = UnmanagedStruct.alloc size

                let align = device.MinUniformBufferOffsetAlignment
                let alignedSize = Alignment.next align (int64 size)

                let buffer = device.CreateBuffer(VkBufferUsageFlags.UniformBufferBit ||| VkBufferUsageFlags.TransferDstBit, alignedSize)

                UniformBuffer(device, buffer.Handle, buffer.Memory, storage, layout)
            | Dynamic ->
                failf "cannot create UniformBuffer with dynamic size"

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
    open System.Reflection.Emit

    type IWriter = 
        abstract member Write : AdaptiveToken * IAdaptiveObject * nativeint -> unit
        abstract member WriteUnsafeValue : obj * nativeint -> unit
        abstract member TargetSize : nativeint
        abstract member WithOffset : nativeint -> IWriter

    type IWriter<'a> =
        inherit IWriter
        abstract member WriteValue : 'a * nativeint -> unit


    type private ReflectionCompiler<'a, 'b> private() =
        static let propertyCache = System.Collections.Concurrent.ConcurrentDictionary<PropertyInfo, 'a -> 'b>()
        static let fieldCache = System.Collections.Concurrent.ConcurrentDictionary<FieldInfo, 'a -> 'b>()

        static let compileProperty (prop : PropertyInfo) =
            let name = sprintf "%s.getProperty%s" prop.DeclaringType.FullName prop.Name
            let meth = 
                new DynamicMethod(
                    name,
                    MethodAttributes.Static ||| MethodAttributes.Public,
                    CallingConventions.Standard,
                    typeof<'b>,
                    [| typeof<'a> |],
                    typeof<'a>,
                    true
                )
            let il = meth.GetILGenerator()

            il.Emit(OpCodes.Ldarg_0)
            il.EmitCall(OpCodes.Callvirt, prop.GetMethod, null)
            il.Emit(OpCodes.Ret)
            let t = System.Linq.Expressions.Expression.GetDelegateType [| typeof<'a>; typeof<'b> |]
            let func = meth.CreateDelegate(t) |> unbox<Func<'a, 'b>>
            func.Invoke

        static let compileField (field : FieldInfo) =
            let name = sprintf "%s.getField%s" field.DeclaringType.FullName field.Name
            let meth = 
                new DynamicMethod(
                    name,
                    MethodAttributes.Static ||| MethodAttributes.Public,
                    CallingConventions.Standard,
                    typeof<'b>,
                    [| typeof<'a> |],
                    typeof<'a>,
                    true
                )
            let il = meth.GetILGenerator()

            il.Emit(OpCodes.Ldarg_0)
            il.Emit(OpCodes.Ldfld, field)
            il.Emit(OpCodes.Ret)
            let t = System.Linq.Expressions.Expression.GetDelegateType [| typeof<'a>; typeof<'b> |]
            let func = meth.CreateDelegate(t) |> unbox<Func<'a, 'b>>
            func.Invoke

        static member Property(p : PropertyInfo) = propertyCache.GetOrAdd(p, Func<_,_>(compileProperty))
        static member Field(f : FieldInfo) = fieldCache.GetOrAdd(f, Func<_,_>(compileField))




    [<AbstractClass>]
    type AbstractWriter<'a>() =
        abstract member Write : 'a * nativeint -> unit
        abstract member TargetSize : nativeint
        default x.TargetSize = -1n

        interface IWriter with
            member x.Write(caller, value, ptr) =
                let value = unbox<IMod<'a>> value
                x.Write(value.GetValue caller, ptr)

            member x.WriteUnsafeValue(value, ptr) =
                match value with
                    | :? 'a as value -> x.Write(value, ptr)
                    | _ -> failf "unexpected value %A (expecting %A)" value typeof<'a>

            member x.TargetSize = x.TargetSize

            member x.WithOffset (offset : nativeint) =
                OffsetWriter<'a>(offset, x) :> IWriter

        interface IWriter<'a> with
            member x.WriteValue(value, ptr) = x.Write(value, ptr)

    and OffsetWriter<'a>(offset : nativeint, writer : IWriter<'a>) =
        inherit AbstractWriter<'a>()

        override x.Write(value, ptr) =
            writer.WriteValue(value, ptr + offset)

        override x.TargetSize =
            writer.TargetSize

    module NewWriters =
        type PrimitiveWriter<'a when 'a : unmanaged> private() =
            inherit AbstractWriter<'a>()
            static let instance = PrimitiveWriter<'a>() :> IWriter<'a>
            static let sa = nativeint sizeof<'a>

            static member Instance = instance

            override x.TargetSize =
                sa

            override x.Write(value : 'a, ptr : nativeint) =
                NativePtr.write (NativePtr.ofNativeInt ptr) value
            
        type MapWriter<'a, 'b>(mapping : 'a -> 'b, inner : IWriter<'b>) =
            inherit AbstractWriter<'a>()
            
            override x.TargetSize =
                inner.TargetSize

            override x.Write(value : 'a, ptr : nativeint) =
                inner.WriteValue(mapping value, ptr)
            
        type FieldWriter<'a, 'b>(field : FieldInfo, inner : IWriter<'b>) =
            inherit MapWriter<'a, 'b>(ReflectionCompiler<'a, 'b>.Field field, inner)
            
        type PropertyWriter<'a, 'b>(prop : PropertyInfo, inner : IWriter<'b>) =
            inherit MapWriter<'a, 'b>(ReflectionCompiler<'a, 'b>.Property prop, inner)

        type StructWriter<'a>(targetSize : nativeint, fieldWriters : array<nativeint * IWriter>) =
            inherit AbstractWriter<'a>()
            let fieldWriters = fieldWriters |> Array.map (fun (o,w) -> o, (unbox<IWriter<'a>> w))

            override x.TargetSize = targetSize

            override x.Write(value : 'a, ptr : nativeint) =
                for (offset, writer) in fieldWriters do
                    writer.WriteValue(value, ptr + offset)
            
        type ArrayWriter<'a>(count : int, stride : nativeint, inner : IWriter<'a>) =
            inherit AbstractWriter<'a[]>()
      
            let targetSize = nativeint (count - 1) * stride + inner.TargetSize

            override x.TargetSize = targetSize
                
            override x.Write(value : 'a[], ptr : nativeint) =
                let mutable offset = 0n

                let cnt = min value.Length count
                for i in 0 .. cnt - 1 do
                    inner.WriteValue(value.[i], ptr + offset)
                    offset <- offset + stride

                let remaining = targetSize - offset
                if remaining > 0n then
                    Marshal.Set(ptr + offset, 0, remaining)

        type ArrWriter<'d, 'a when 'd :> INatural>(targetCount : int, stride : nativeint, inner : IWriter<'a>) =
            inherit AbstractWriter<Arr<'d, 'a>>()
            
            let targetSize = nativeint (targetCount - 1) * stride + inner.TargetSize
            let inputCount = Peano.getSize typeof<'d>

            
                
            let firstEmptyByte = (stride * nativeint (inputCount - 1) + inner.TargetSize)
            let missingBytes = targetSize - firstEmptyByte
                

            override x.TargetSize = targetSize

            override x.Write(values : Arr<'d, 'a>, ptr : nativeint) =
                let mutable offset = 0n
                for i in 0 .. inputCount - 1 do
                    inner.WriteValue(values.[i], ptr + offset)
                    offset <- offset + stride
                
                if missingBytes > 0n then
                    Marshal.Set(ptr + firstEmptyByte, 0, missingBytes)

                

        let private newPrimitiveWriter (t : Type) =
            let tWriter = typedefof<PrimitiveWriter<int>>.MakeGenericType [| t |]
            let prop = tWriter.GetProperty("Instance", BindingFlags.Static ||| BindingFlags.Public)
            prop.GetValue(null) |> unbox<IWriter>
            
        let private newMapWriter (tSource : Type) (tTarget : Type) (f : obj) (inner : IWriter) =
            let tWriter = typedefof<MapWriter<_,_>>.MakeGenericType [| tSource; tTarget |]
            let ctor = tWriter.GetConstructor [| f.GetType(); inner.GetType() |]
            ctor.Invoke [| f; inner :> obj |] |> unbox<IWriter>
            
        let private newFieldWriter (fi : FieldInfo) (inner : IWriter) =
            let tWriter = typedefof<FieldWriter<_,_>>.MakeGenericType [| fi.DeclaringType; fi.FieldType |]
            let ctor = tWriter.GetConstructor [| typeof<FieldInfo>; inner.GetType() |]
            ctor.Invoke [| fi :> obj; inner :> obj |] |> unbox<IWriter>
            
        let private newPropertyWriter (pi : PropertyInfo) (inner : IWriter) =
            let tWriter = typedefof<PropertyWriter<_,_>>.MakeGenericType [| pi.DeclaringType; pi.PropertyType |]
            let ctor = tWriter.GetConstructor [| typeof<PropertyInfo>; inner.GetType() |]
            ctor.Invoke [| pi :> obj; inner :> obj |] |> unbox<IWriter>

        let private newStructWriter (structType : Type) (targetSize : nativeint) (fieldWriters : list<nativeint * IWriter>) =
            let t = typedefof<StructWriter<_>>.MakeGenericType [| structType |]
            let ctor = t.GetConstructor [| typeof<nativeint>; typeof<array<nativeint * IWriter>> |]
            ctor.Invoke [| targetSize :> obj; fieldWriters |> List.toArray :> obj |] |> unbox<IWriter>

        let private newArrayWriter (elemType : Type) (count : int) (stride : nativeint) (inner : IWriter) =
            let t = typedefof<ArrayWriter<_>>.MakeGenericType [| elemType |]
            let ctor = t.GetConstructor [| typeof<int>; typeof<nativeint>; inner.GetType() |]
            ctor.Invoke [| count :> obj; stride :> obj; inner :> obj |] |> unbox<IWriter>
        
        let private newArrWriter (lenType : Type) (elemType : Type) (count : int) (stride : nativeint) (inner : IWriter) =
            let t = typedefof<ArrWriter<_,_>>.MakeGenericType [| lenType; elemType |]
            let ctor = t.GetConstructor [| typeof<int>; typeof<nativeint>; inner.GetType() |]
            ctor.Invoke [| count :> obj; stride :> obj; inner :> obj |] |> unbox<IWriter>


        let (|ArrayOf|_|) (t : Type) =
            if t.IsArray then
                Some (t.GetElementType())
            else
                None

        let (|ArrOf|_|) (t : Type) =
            if t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<Arr<_,_>> then
                let targs = t.GetGenericArguments()
                Some (targs.[0], targs.[1])
            else
                None

        let (|SeqOf|_|) (t : Type) =
            let iface = t.GetInterface(typedefof<seq<_>>.FullName)
            if isNull iface then
                None
            else
                Some (iface.GetGenericArguments().[0])

        let rec createWriterInternal (target : UniformType) (tSource : Type) =
            match target with
                | UniformType.Primitive(t, s, a) ->
                    let tTarget = PrimitiveType.toType t

                    let prim = newPrimitiveWriter tTarget

                    if tTarget = tSource then
                        prim
                    else
                        let converter = PrimitiveValueConverter.getConverter tSource tTarget
                        newMapWriter tSource tTarget converter prim

                
                | UniformType.Struct(layout) ->
                    match layout.size with
                        | Size.Fixed s ->
                            let fieldWriters =
                                layout.fields |> List.map (fun f ->
                                    let fi = tSource.GetField(f.name, BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.Instance)

                                    if isNull fi then
                                        let pi = tSource.GetProperty(f.name, BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.Instance)
                                        if isNull pi then
                                            failwith "asdasdasd"
                                    
                                        let inner = createWriterInternal f.fieldType pi.PropertyType
                                        nativeint f.offset, newPropertyWriter pi inner
                            
                                    else
                                        let inner = createWriterInternal f.fieldType fi.FieldType
                                        nativeint f.offset, newFieldWriter fi inner
                                )

                            newStructWriter tSource (nativeint s) fieldWriters
                        | _ ->
                            failf "uniform-structs need to have a fixed size"

                | UniformType.Array(itemType, len, size, stride) ->
                    match tSource with
                        | ArrayOf tSourceElem ->
                            let elemWriter = createWriterInternal itemType tSourceElem
                            newArrayWriter tSourceElem len (nativeint stride) elemWriter

                        | ArrOf(tLength, tSourceElem) ->
                            let elemWriter = createWriterInternal itemType tSourceElem
                            newArrWriter tLength tSourceElem len (nativeint stride) elemWriter

                        | SeqOf tSourceElem ->
                            failf ""

                        | _ ->
                            failf "not a sequence type"

                | UniformType.RuntimeArray _ ->
                    failf "cannot create writer for RuntimeArray"

            

    type SingleValueWriter<'a when 'a : unmanaged>(offset : int) =
        inherit AbstractWriter<'a>()

        override x.Write(value : 'a, ptr : nativeint) =
            let ptr = NativePtr.ofNativeInt (ptr + nativeint offset)
            NativePtr.write ptr value

    type ConversionWriter<'a, 'b when 'b : unmanaged>(offset : int, convert : 'a -> 'b) =
        inherit AbstractWriter<'a>()

        override x.Write(value : 'a, ptr : nativeint) =
            let mutable ptr = NativePtr.ofNativeInt (ptr + nativeint offset)
            let res = convert value
            NativePtr.write ptr res

    type NoConversionWriter<'a when 'a : unmanaged>(offset : int) =
        inherit AbstractWriter<'a>()

        override x.Write(value : 'a, ptr : nativeint) =
            let mutable ptr = NativePtr.ofNativeInt (ptr + nativeint offset)
            NativePtr.write ptr value

    type MultiWriter<'a>(writers : list<IWriter<'a>>) =
        inherit AbstractWriter<'a>()

        override x.Write(value : 'a, ptr : nativeint) =
            for w in writers do w.WriteValue(value, ptr)

        new (writers : list<IWriter>) = MultiWriter<'a>(writers |> List.map unbox<IWriter<'a>>)

    type PropertyWriter<'a, 'b>(prop : PropertyInfo, inner : IWriter<'b>) =
        inherit AbstractWriter<'a>()

        let getProp = ReflectionCompiler<'a, 'b>.Property prop

        override x.Write(value : 'a, target : nativeint) =
            let v = getProp value
            inner.WriteValue(v, target)

    type FieldWriter<'a, 'b>(prop : FieldInfo, inner : IWriter<'b>) =
        inherit AbstractWriter<'a>()

        let getField = ReflectionCompiler<'a, 'b>.Field prop

        override x.Write(value : 'a, target : nativeint) =
            let v = getField value
            inner.WriteValue(v, target)

    type SequenceWriter<'s, 'a when 's :> seq<'a>>(inner : IWriter<'a>[]) =
        inherit AbstractWriter<'s>()

        let rec run (target : nativeint) (index : int) (e : System.Collections.Generic.IEnumerator<'a>) =
            if index >= inner.Length then 
                ()
            else
                if e.MoveNext() then
                    let v = e.Current
                    inner.[index].WriteValue(v, target)
                    run target (index + 1) e
                else
                    ()

        override x.Write(value : 's, target : nativeint) =
            use e = (value :> seq<'a>).GetEnumerator()
            run target 0 e

        new(writers : list<IWriter>) =
            SequenceWriter<'s, 'a>(writers |> List.map unbox |> List.toArray)
    
    type NoConversionArrayWriter<'a when 'a : unmanaged>(offset : int, length : int) =
        inherit AbstractWriter<'a[]>()

        static let sa = nativeint sizeof<'a>
        
        let totalSize = sa * nativeint length

        override x.Write(value : 'a[], ptr : nativeint) =
            let ptr = ptr + nativeint offset
            let copySize = nativeint (min value.Length length) * sa

            let gc = GCHandle.Alloc(value, GCHandleType.Pinned)
            try
                if copySize > 0n then
                    Marshal.Copy(gc.AddrOfPinnedObject(), ptr, copySize)
                if totalSize > copySize then
                    Marshal.Set(ptr + copySize, 0, totalSize - copySize)
            finally
                gc.Free()

    type StridedArrayWriter<'a when 'a : unmanaged>(offset : int, length : int, stride : int) =
        inherit AbstractWriter<'a[]>()

        static let def = Unchecked.defaultof<'a>
        let sb = nativeint stride

        override x.Write(value : 'a[], ptr : nativeint) =
            let mutable ptr = ptr + nativeint offset

            let c = min value.Length length
            for i in 0 .. c - 1 do
                NativePtr.write (NativePtr.ofNativeInt ptr) value.[i]
                ptr <- ptr + sb

            for i in c .. length - 1 do
                NativePtr.write (NativePtr.ofNativeInt ptr) def
                ptr <- ptr + sb
                
    type ConversionArrayWriter<'a, 'b when 'b : unmanaged>(offset : int, length : int, stride : int, converter : 'a -> 'b) =
        inherit AbstractWriter<'a[]>()

        static let def = Unchecked.defaultof<'b>
        let sb = nativeint stride

        override x.Write(value : 'a[], ptr : nativeint) =
            let mutable ptr = ptr + nativeint offset

            let c = min value.Length length
            for i in 0 .. c - 1 do
                let b = converter value.[i]
                NativePtr.write (NativePtr.ofNativeInt ptr) b
                ptr <- ptr + sb

            for i in c .. length - 1 do
                NativePtr.write (NativePtr.ofNativeInt ptr) def
                ptr <- ptr + sb


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
            | UniformType.RuntimeArray _ ->
                failf "cannot create writer for RuntimeArray"

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

            | UniformType.Array(itemType, len, size, stride) ->
                match itemType with
                    | UniformType.Primitive(tt,tes,_) when tSource.IsArray ->
                
                        let tSourceElement = tSource.GetElementType()

                        let tTargetElement = PrimitiveType.toType tt
                        if tTargetElement = tSourceElement then
                            if tes = stride then
                                let tWriter = typedefof<NoConversionArrayWriter<int>>.MakeGenericType [| tSourceElement |]
                                let ctor = tWriter.GetConstructor [|typeof<int>; typeof<int>|]
                                let writer = ctor.Invoke [|offset; len|] |> unbox<IWriter>
                                Some writer
                            else
                                let tWriter = typedefof<StridedArrayWriter<int>>.MakeGenericType [| tSourceElement |]
                                let ctor = tWriter.GetConstructor [| typeof<int>; typeof<int>; typeof<int> |]
                                let writer = ctor.Invoke [| offset; len; stride |] |> unbox<IWriter>
                                    
                                Some writer

                        else
                            let converter = PrimitiveValueConverter.getConverter tSourceElement tTargetElement

                            let tWriter = typedefof<ConversionArrayWriter<int, int>>.MakeGenericType [| tSourceElement; tTargetElement |]
                            let ctor = tWriter.GetConstructor [| typeof<int>; typeof<int>; typeof<int>; converter.GetType() |]

                            let writer = ctor.Invoke [| offset; len; stride; converter |] |> unbox<IWriter>
                            Some writer




                    | _ ->
                        let iface = tSource.GetInterface(typedefof<System.Collections.Generic.IEnumerable<_>>.FullName)
                        if isNull iface then
                            None
                        else
                            let tItem = iface.GetGenericArguments().[0]
                            let tSequence = tSource
                            let writers = 
                                List.init len id
                                |> List.mapOption (fun i ->
                                    let off = offset + i * stride
                                    match tryCreateWriterInternal off itemType tItem  with
                                        | Some writer -> Some writer
                                        | _ -> None
                                )

                            match writers with
                                | Some writers ->
                                    let tWriter = typedefof<SequenceWriter<list<int>, int>>.MakeGenericType [|tSequence; tItem|]
                                    let ctor = tWriter.GetConstructor [| typeof<list<IWriter>> |]
                                    let writer = ctor.Invoke [|writers|] |> unbox<IWriter>
                                    Some writer
                                | _ ->
                                    None
    
    let cache = System.Collections.Concurrent.ConcurrentDictionary<int * UniformType * Type, Option<IWriter>>()

    let tryGetWriter (offset : int) (tTarget : UniformType) (tSource : Type) =
        let key = (offset, tTarget, tSource)
        let writer = 
            cache.GetOrAdd(key, fun (offset, tTarget, tSource) ->
               // NewWriters.createWriterInternal tTarget tSource |> Some
                tryCreateWriterInternal offset tTarget tSource
            )
        writer
//        match writer with
//            | Some w -> 
//                if offset = 0 then w |> Some
//                else w.WithOffset (nativeint offset) |> Some
//            | None ->
//                None
    
    let getWriter (offset : int) (tTarget : UniformType) (tSource : Type) =
        match tryGetWriter offset tTarget tSource with
            | Some w -> w
            | None -> failf "could not create UniformWriter for field %A (input-type: %A)" tTarget tSource