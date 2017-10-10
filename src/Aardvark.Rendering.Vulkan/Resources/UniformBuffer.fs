namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop
open Microsoft.FSharp.Reflection

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
        abstract member ValueType : Type
        abstract member IsPrimitive : bool

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

        abstract member IsPrimitive : bool
        default x.IsPrimitive = false


        interface IWriter with
            member x.IsPrimitive = x.IsPrimitive
            member x.ValueType = typeof<'a>
            member x.Write(caller, value, ptr) =
                let value = unbox<IMod<'a>> value
                x.Write(value.GetValue caller, ptr)

            member x.WriteUnsafeValue(value, ptr) =
                match value with
                    | :? 'a as value -> x.Write(value, ptr)
                    | _ -> failf "unexpected value %A (expecting %A)" value typeof<'a>

            member x.TargetSize = x.TargetSize

            member x.WithOffset (offset : nativeint) =
                if offset = 0n then x :> IWriter
                else OffsetWriter<'a>(offset, x) :> IWriter

        interface IWriter<'a> with
            member x.WriteValue(value, ptr) = x.Write(value, ptr)

    and OffsetWriter<'a>(offset : nativeint, writer : IWriter<'a>) =
        inherit AbstractWriter<'a>()

        override x.Write(value, ptr) =
            writer.WriteValue(value, ptr + offset)

        override x.TargetSize =
            writer.TargetSize


    module private List =
        let rec mapOption (f : 'a -> Option<'b>) (l : list<'a>) =
            match l with
                | [] -> Some []
                | h :: rest ->
                    match f h, mapOption f rest with
                        | Some h, Some t -> Some (h :: t)
                        | _ ->  None

    module NewWriters =
        open System.Runtime.InteropServices

        type TypeInfo<'a> private() =
            static let isBlittable =
                let arr : 'a[] = Array.zeroCreate 1
                try
                    let gc = GCHandle.Alloc(arr, GCHandleType.Pinned)
                    gc.Free()
                    true
                with _ ->
                    false

            static member IsBlittable = isBlittable

        let private blitCache = System.Collections.Concurrent.ConcurrentDictionary<Type, bool>()

        type Type with
            member x.IsBlittable =
                blitCache.GetOrAdd(x, fun x -> 
                    let t = typedefof<TypeInfo<_>>.MakeGenericType [| x |]
                    t.GetProperty("IsBlittable").GetValue null |> unbox<bool>
                )


        type PrimitiveWriter<'a when 'a : unmanaged> private() =
            inherit AbstractWriter<'a>()
            static let instance = PrimitiveWriter<'a>() :> IWriter<'a>
            static let sa = nativeint sizeof<'a>

            static member Instance = instance

            override x.IsPrimitive = true

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

        type SeqWriter<'s, 'a when 's :> seq<'a>>(targetCount : int, stride : nativeint, inner : IWriter<'a>) =
            inherit AbstractWriter<'s>()
            
            let targetSize = nativeint (targetCount - 1) * stride + inner.TargetSize

            override x.TargetSize = targetSize
                
            override x.Write(value : 's, ptr : nativeint) =
                let mutable offset = 0n

                use e = (value :> seq<'a>).GetEnumerator()
                let mutable cnt = 0
                while cnt < targetCount && e.MoveNext() do
                    inner.WriteValue(e.Current, ptr + offset)
                    offset <- offset + stride
                    cnt <- cnt + 1

                let remaining = targetSize - offset
                if remaining > 0n then
                    Marshal.Set(ptr + offset, 0, remaining)

        type SubTypeTestWriter<'a, 'b>(inner : IWriter<'b>) =
            inherit AbstractWriter<'a>()
            
            override x.TargetSize =
                inner.TargetSize

            override x.Write(value : 'a, ptr : nativeint) =
                match value :> obj with
                    | :? 'b as b -> inner.WriteValue(b, ptr)
                    | _ -> ()

        type PrimitiveArrayWriter<'a when 'a : unmanaged>(count : int) =
            inherit AbstractWriter<'a[]>()
            
            let targetSize = nativeint count * nativeint sizeof<'a>

            override x.TargetSize = targetSize
                
            override x.Write(value : 'a[], ptr : nativeint) =
                let gc = GCHandle.Alloc(value, GCHandleType.Pinned)
                let inputSize = nativeint value.Length * nativeint sizeof<'a>
                let copySize = min targetSize inputSize

                try 
                    Marshal.Copy(gc.AddrOfPinnedObject(), ptr, copySize)
                    if targetSize > copySize then Marshal.Set(ptr + copySize, 0, targetSize - copySize)
                finally 
                    gc.Free()

        type PrimitiveMapWriter<'a, 'b when 'b : unmanaged>(mapping : 'a -> 'b) =
            inherit AbstractWriter<'a>()
            
            static let sb = nativeint sizeof<'b>

            override x.TargetSize = sb

            override x.Write(value : 'a, ptr : nativeint) =
                NativePtr.write (NativePtr.ofNativeInt ptr) (mapping value)

        type PrimitiveFieldWriter<'a, 'b when 'b : unmanaged>(field : FieldInfo) =
            inherit PrimitiveMapWriter<'a, 'b>(ReflectionCompiler<'a, 'b>.Field field)

        type PrimitivePropertyWriter<'a, 'b when 'b : unmanaged>(prop : PropertyInfo) =
            inherit PrimitiveMapWriter<'a, 'b>(ReflectionCompiler<'a, 'b>.Property prop)
               

        let private newPrimitiveWriter (t : Type) =
            let tWriter = typedefof<PrimitiveWriter<int>>.MakeGenericType [| t |]
            let prop = tWriter.GetProperty("Instance", BindingFlags.Static ||| BindingFlags.Public)
            prop.GetValue(null) |> unbox<IWriter>
            
        let private newMapWriter (tSource : Type) (tTarget : Type) (f : obj) (inner : IWriter) =
            if tTarget.IsBlittable && inner.IsPrimitive then
                let tWriter = typedefof<PrimitiveMapWriter<int,int>>.MakeGenericType [| tSource; tTarget |]
                let ctor = tWriter.GetConstructor [| f.GetType() |]
                ctor.Invoke [| f |] |> unbox<IWriter>
            else
                let tWriter = typedefof<MapWriter<_,_>>.MakeGenericType [| tSource; tTarget |]
                let ctor = tWriter.GetConstructor [| f.GetType(); inner.GetType() |]
                ctor.Invoke [| f; inner :> obj |] |> unbox<IWriter>
            
        let private newFieldWriter (fi : FieldInfo) (inner : IWriter) =
            if fi.FieldType.IsBlittable && inner.IsPrimitive then
                let tWriter = typedefof<PrimitiveFieldWriter<int, int>>.MakeGenericType [| fi.DeclaringType; fi.FieldType |]
                let ctor = tWriter.GetConstructor [| typeof<FieldInfo> |]
                ctor.Invoke [| fi :> obj |] |> unbox<IWriter>
            else
                let tWriter = typedefof<FieldWriter<_,_>>.MakeGenericType [| fi.DeclaringType; fi.FieldType |]
                let ctor = tWriter.GetConstructor [| typeof<FieldInfo>; inner.GetType() |]
                ctor.Invoke [| fi :> obj; inner :> obj |] |> unbox<IWriter>
            
        let private newPropertyWriter (pi : PropertyInfo) (inner : IWriter) =
            if pi.PropertyType.IsBlittable && inner.IsPrimitive then
                let tWriter = typedefof<PrimitivePropertyWriter<int, int>>.MakeGenericType [| pi.DeclaringType; pi.PropertyType |]
                let ctor = tWriter.GetConstructor [| typeof<PropertyInfo> |]
                ctor.Invoke [| pi :> obj |] |> unbox<IWriter>
            else
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

        let private newSeqWriter (tSeq : Type) (count : int) (stride : nativeint) (inner : IWriter) =
            let t = typedefof<SeqWriter<_,_>>.MakeGenericType [| tSeq; inner.ValueType |]
            let ctor = t.GetConstructor [| typeof<int>; typeof<nativeint>; inner.GetType() |]
            ctor.Invoke [| count :> obj; stride :> obj; inner :> obj |] |> unbox<IWriter>

        let private newSubTypeTestWriter (tDeclared : Type) (tReal : Type) (inner : IWriter) =
            let t = typedefof<SubTypeTestWriter<_,_>>.MakeGenericType [| tDeclared; tReal |]
            let ctor = t.GetConstructor [| inner.GetType() |]
            ctor.Invoke [| inner :> obj |] |> unbox<IWriter>

        let private newPrimitiveArrayWriter (tElem : Type) (cnt : int) =
            let t = typedefof<PrimitiveArrayWriter<int>>.MakeGenericType [| tElem |]
            let ctor = t.GetConstructor [| typeof<int> |]
            ctor.Invoke [| cnt :> obj |] |> unbox<IWriter>


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

        let rec tryCreateWriterInternal (target : UniformType) (tSource : Type) =
            match target with
                | UniformType.Primitive(t, s, a) ->
                    let tTarget = PrimitiveType.toType t

                    let prim = newPrimitiveWriter tTarget

                    if tTarget = tSource then
                        Some prim
                    else
                        let converter = PrimitiveValueConverter.getConverter tSource tTarget
                        newMapWriter tSource tTarget converter prim |> Some

                
                | UniformType.Struct(layout) ->
                    match layout.size with
                        | Size.Fixed s ->
                            let fieldWriters =
                                if FSharpType.IsUnion(tSource, true) then
                                    let cases = FSharpType.GetUnionCases(tSource, true)
                                    let table = 
                                        cases |> Seq.collect (fun ci ->
                                            ci.GetFields() |> Seq.map (fun pi ->
                                                ci.Name + "_" + pi.Name, pi
                                            )
                                        )
                                        |> Map.ofSeq
                                        |> Map.add "tag" (tSource.GetProperty("Tag", BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.Instance))
                                    
                                    layout.fields |> List.mapOption (fun f ->
                                        match Map.tryFind f.name table with
                                            | Some pi ->
                                                match tryCreateWriterInternal f.fieldType pi.PropertyType with
                                                    | Some inner ->
                                                        let w = newPropertyWriter pi inner
                                                        let w = 
                                                            if pi.DeclaringType <> tSource then 
                                                                newSubTypeTestWriter tSource pi.DeclaringType w
                                                            else
                                                                w

                                                        Some (nativeint f.offset, w)
                                                    | None ->
                                                        None
                                            | None ->
                                                None

                                    )
                                else
                                    layout.fields |> List.mapOption (fun f ->
                                        let fi = tSource.GetField(f.name, BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.Instance)

                                        if isNull fi then
                                            let pi = tSource.GetProperty(f.name, BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.Instance)
                                            if isNull pi then
                                                None
                                            else
                                                match tryCreateWriterInternal f.fieldType pi.PropertyType with
                                                    | Some inner -> 
                                                        Some (nativeint f.offset, newPropertyWriter pi inner)
                                                    | None ->
                                                        None
                            
                                        else
                                            match tryCreateWriterInternal f.fieldType fi.FieldType with
                                                | Some inner -> 
                                                    Some (nativeint f.offset, newFieldWriter fi inner)
                                                | None ->
                                                    None
                                    )
                            match fieldWriters with
                                | Some fieldWriters ->
                                    Some (newStructWriter tSource (nativeint s) fieldWriters)
                                | None ->
                                    None
                        | _ ->
                            None

                | UniformType.Array(itemType, len, size, stride) ->
                    let stride = nativeint stride
                    match tSource with
                        | ArrayOf tSourceElem ->
                            match tryCreateWriterInternal itemType tSourceElem with
                                | Some elemWriter ->
                                    if elemWriter.IsPrimitive then
                                        if elemWriter.TargetSize = stride then
                                            newPrimitiveArrayWriter tSourceElem len |> Some
                                        else
                                            // TODO: can be improved?
                                            newArrayWriter tSourceElem len stride elemWriter |> Some
                                    else
                                        newArrayWriter tSourceElem len stride elemWriter |> Some
                                | None ->
                                    None

                        | ArrOf(tLength, tSourceElem) ->
                            match tryCreateWriterInternal itemType tSourceElem with
                                | Some elemWriter ->
                                    newArrWriter tLength tSourceElem len stride elemWriter |> Some
                                | None ->
                                    None

                        | SeqOf tSourceElem ->
                            match tryCreateWriterInternal itemType tSourceElem with
                                | Some elemWriter -> 
                                    newSeqWriter tSource len stride elemWriter |> Some
                                | None ->
                                    None

                        | _ ->
                            None

                | UniformType.RuntimeArray _ ->
                    None

    let cache = System.Collections.Concurrent.ConcurrentDictionary<UniformType * Type, Option<IWriter>>()

    let tryGetWriter (offset : int) (tTarget : UniformType) (tSource : Type) =
        let key = (tTarget, tSource)
        let writer = cache.GetOrAdd(key, fun (tTarget, tSource) -> NewWriters.tryCreateWriterInternal tTarget tSource)

        match writer with
            | Some w -> 
                w.WithOffset (nativeint offset) |> Some
            | None ->
                None
    
    let getWriter (offset : int) (tTarget : UniformType) (tSource : Type) =
        match tryGetWriter offset tTarget tSource with
            | Some w -> w
            | None -> failf "could not create UniformWriter for field %A (input-type: %A)" tTarget tSource