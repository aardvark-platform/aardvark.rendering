namespace Aardvark.Rendering.Vulkan


open SpirV
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Microsoft.FSharp.NativeInterop
open FShade.SpirV
open System
open System.Runtime.InteropServices
open System.Runtime.CompilerServices

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



type UniformBufferLayout = { align : int; size : int; fieldOffsets : Map<string, int>; fieldTypes : Map<string, ShaderType> }

module UniformBufferLayoutStd140 =
    open System.Collections.Generic
    open FShade.SpirV

    

    let rec private sizeAndAlignOf (t : ShaderType) : int * int =
        match t with
            | Int(w,_) | Float(w) -> 
                // both the size and alignment are the size of the scalar
                // in basic machine types (e.g. sizeof<int>)
                let s = w / 8
                (s,s)

            | Bool -> 
                (4,4)

            | Vector(bt,3) -> 
                // both the size and alignment are 4 times the size
                // of the underlying scalar type.
                let s = sizeOf bt * 4
                (s,s)

            | Vector(bt,d) ->  
                // both the size and alignment are <d> times the size
                // of the underlying scalar type.
                let s = sizeOf bt * d
                (s,s)

            | Array(bt, len) -> 
                let physicalSize = sizeOf bt

                // the size of each element in the array will be the size
                // of the element type rounded up to a multiple of the size
                // of a vec4. This is also the array's alignment.
                // The array's size will be this rounded-up element's size
                // times the number of elements in the array.
                let size =
                    if physicalSize % 16 = 0 then physicalSize
                    else physicalSize + 16 - (physicalSize % 16)

                (size * len, size)

            | Matrix(colType, cols) ->
                // same layout as an array of N vectors each with 
                // R components, where N is the total number of columns
                // present.
                sizeAndAlignOf (Array(colType, cols))

            | Ptr(_,t) -> 
                sizeAndAlignOf t

            | Image(sampledType, dim,depth,arr, ms, sam, fmt) -> 
                failf "cannot determine size for image type"

            | Sampler -> 
                failf "cannot determine size for sampler type"

            | Struct(name,fields) -> 
                let layout = structLayout fields
                layout.align, layout.size

            | Void -> 
                failf "cannot determine size for void type"

            | Function _ ->
                failf "cannot use function in UniformBuffer"

            | SampledImage _ ->
                failf "cannot use SampledImage in UniformBuffer"
                

    and sizeOf (t : ShaderType) =
        t |> sizeAndAlignOf |> fst

    and alignOf (t : ShaderType) =
        t |> sizeAndAlignOf |> snd

    and structLayout (fields : list<ShaderType * string * list<Decoration * uint32[]>>) : UniformBufferLayout =
        let mutable currentOffset = 0
        let mutable offsets : Map<string, int> = Map.empty
        let mutable types : Map<string, ShaderType> = Map.empty
        let mutable biggestFieldSize = 0
        let mutable biggestFieldAlign = 0

        for (t,n,dec) in fields do
            let (size,align) = sizeAndAlignOf t

            // align the field offset
            if currentOffset % align <> 0 then
                currentOffset <- currentOffset + align - (currentOffset % align)

            // keep track of the biggest member
            if size > biggestFieldSize then
                biggestFieldSize <- size
                biggestFieldAlign <- align

            // store the member's offset
            offsets <- Map.add n currentOffset offsets
            types <- Map.add n t types
            currentOffset <- currentOffset + size
            ()

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

        { align = structAlign; size = structSize; fieldOffsets = offsets; fieldTypes = types }

module UniformConverter =

    let expectedType =
        lookupTable [
            ShaderType.Int ,                 typeof<int>
            ShaderType.IntVec2 ,             typeof<V2i>
            ShaderType.IntVec3 ,             typeof<V3i>
            ShaderType.IntVec4 ,             typeof<V4i>

            ShaderType.UnsignedInt ,         typeof<uint32>
            ShaderType.UnsignedIntVec3 ,     typeof<C3ui>
            ShaderType.UnsignedIntVec4 ,     typeof<C4ui>

            ShaderType.Float ,               typeof<float32>
            ShaderType.FloatVec2 ,           typeof<V2f>
            ShaderType.FloatVec3 ,           typeof<V3f>
            ShaderType.FloatVec4 ,           typeof<V4f>
            ShaderType.FloatMat2 ,           typeof<M22f>
            ShaderType.FloatMat3 ,           typeof<M34f>
            ShaderType.FloatMat4 ,           typeof<M44f>
            ShaderType.FloatMat2x3 ,         typeof<M23f>
            ShaderType.FloatMat3x4 ,         typeof<M34f>

            ShaderType.Double ,              typeof<float>
            ShaderType.DoubleVec2 ,          typeof<V2d>
            ShaderType.DoubleVec3 ,          typeof<V3d>
            ShaderType.DoubleVec4 ,          typeof<V4d>
            

            ShaderType.Bool,                 typeof<int>
            ShaderType.BoolVec2 ,            typeof<V2i>
            ShaderType.BoolVec3 ,            typeof<V3i>
            ShaderType.BoolVec4 ,            typeof<V4i>

        ]

module UniformWriters =
    
    type IWriter =
        abstract member Write : IAdaptiveObject * nativeint -> unit

    [<AbstractClass>]
    type AbstractWriter() =
        abstract member Write : IAdaptiveObject * nativeint -> unit

        interface IWriter with
            member x.Write(caller, ptr) = x.Write(caller, ptr)

    type ViewWriter<'a, 'b when 'b : unmanaged>(source : IMod<'a>, fields : list<int * ('a -> 'b)>) =
        inherit AbstractWriter()
     
        let fieldValues = source |> Mod.map (fun v -> fields |> List.map (fun (o,a) -> o, a v))

        override x.Write(caller : IAdaptiveObject, ptr : nativeint) =
            let fields = fieldValues.GetValue caller
            for (offset, value) in fields do
                let ptr = NativePtr.ofNativeInt (ptr + nativeint offset)
                NativePtr.write ptr value

    type SingleValueWriter<'a when 'a : unmanaged>(source : IMod<'a>, offset : int) =
        inherit AbstractWriter()

        override x.Write(caller : IAdaptiveObject, ptr : nativeint) =
            let ptr = NativePtr.ofNativeInt (ptr + nativeint offset)
            let v = source.GetValue caller
            NativePtr.write ptr v

    type ConversionArrayWriter<'a, 'b when 'b : unmanaged>(source : IMod<'a[]>, count : int, offset : int, stride : int, convert : 'a -> 'b) =
        inherit AbstractWriter()

        let stride = 
            if stride = 0 then sizeof<'a>
            else stride

        let converted =
            source |> Mod.map (Array.map convert)

        override x.Write(caller : IAdaptiveObject, ptr : nativeint) =
            let mutable ptr = NativePtr.ofNativeInt (ptr + nativeint offset)
            let v = converted.GetValue caller

            let c = min count v.Length
            for i in 0..c-1 do
                NativePtr.write ptr v.[i]
                ptr <- NativePtr.ofNativeInt (NativePtr.toNativeInt ptr + nativeint stride)

            for i in c..count-1 do
                NativePtr.write ptr Unchecked.defaultof<'b>

    type ConversionWriter<'a, 'b when 'b : unmanaged>(source : IMod<'a>, offset : int, convert : 'a -> 'b) =
        inherit AbstractWriter()

        override x.Write(caller : IAdaptiveObject, ptr : nativeint) =
            let mutable ptr = NativePtr.ofNativeInt (ptr + nativeint offset)
            let v = source.GetValue caller
            let res = convert v
            NativePtr.write ptr res


    type NoConversionWriter<'a when 'a : unmanaged>(source : IMod<'a>, offset : int) =
        inherit AbstractWriter()

        override x.Write(caller : IAdaptiveObject, ptr : nativeint) =
            let mutable ptr = NativePtr.ofNativeInt (ptr + nativeint offset)
            let v = source.GetValue caller
            NativePtr.write ptr v

    type ConversionSeqWriter<'s, 'a, 'b when 'b : unmanaged and 's :> seq<'a>>(source : IMod<'s>, count : int, offset : int, stride : int, convert : 'a -> 'b) =
        inherit AbstractWriter()

        let stride = 
            if stride = 0 then sizeof<'a>
            else stride

        let converted =
            source |> Mod.map (Seq.toArray >> Array.map convert)

        override x.Write(caller : IAdaptiveObject, ptr : nativeint) =
            let mutable ptr = NativePtr.ofNativeInt (ptr + nativeint offset)
            let v = converted.GetValue caller

            let c = min count v.Length
            for i in 0..c-1 do
                NativePtr.write ptr v.[i]
                ptr <- NativePtr.ofNativeInt (NativePtr.toNativeInt ptr + nativeint stride)

            for i in c..count-1 do
                NativePtr.write ptr Unchecked.defaultof<'b>

    type MultiWriter(writers : list<IWriter>) =
        inherit AbstractWriter()

        override x.Write(caller : IAdaptiveObject, ptr : nativeint) =
            for w in writers do w.Write(caller, ptr)


    let private createTemplate (layout : UniformBufferLayout) (inputTypes : Map<Symbol, Type>) =
        let fields = 
            layout.fieldTypes 
            |> Map.toList
            |> List.map (fun (k,t) -> 
                let off = Map.find k layout.fieldOffsets
                t, k, off
            )

        fields 
            |> Seq.groupBy(fun (t,n,_) -> n) 
            |> Seq.map (fun (n,g) -> Sym.ofString n, g |> Seq.toList) 
            |> Seq.map (fun (name, fields) ->
                let tMod = 
                    match Map.tryFind name inputTypes with
                        | Some tMod -> tMod
                        | _ -> failwithf "could not determine input type for semantic: %A" name

                match tMod with
                    | ModOf tSource ->

                        let creators = 
                            fields |> List.map (fun (t,n,offset) ->
                                match t with
                                    | Array(et,l) ->
                                        let tTarget = UniformConverter.expectedType et
                                        let tSeq = tSource.GetInterface("System.Collections.Generic.IEnumerable`1") 

                                        if tSeq <> null then
                                            let tSourceElement = tSeq.GetGenericArguments().[0]
                                            let converter = PrimitiveValueConverter.getConverter tSourceElement tTarget

                                            let ctor = 
                                                if tSource.IsArray then
                                                    let tWriter = typedefof<ConversionArrayWriter<int,int>>.MakeGenericType [|tSourceElement; tTarget|]
                                                    tWriter.GetConstructor [|tMod; typeof<int>; typeof<int>; typeof<int>; converter.GetType()|]
                                                else
                                                    let tWriter = typedefof<ConversionSeqWriter<list<int>,int,int>>.MakeGenericType [|tSource; tSourceElement; tTarget|]
                                                    tWriter.GetConstructor [|tMod; typeof<int>; typeof<int>; typeof<int>; converter.GetType()|]

                                            fun (m : IAdaptiveObject) ->
                                                ctor.Invoke [|m; l; offset; 0; converter|] |> unbox<IWriter>

                                        else
                                            failwithf "cannot write non-enumerable value to uniform-array: %A" (t,n,offset)

                                    | _ ->
                                        let tTarget = UniformConverter.expectedType t

                                        if tSource <> tTarget then
                                            let converter = PrimitiveValueConverter.getConverter tSource tTarget

                                            let tWriter = typedefof<ConversionWriter<int,int>>.MakeGenericType [|tSource; tTarget|]
                                            let ctor = tWriter.GetConstructor [|tMod; typeof<int>; converter.GetType()|]

                                            fun (m : IAdaptiveObject) ->
                                                ctor.Invoke [|m; offset; converter|] |> unbox<IWriter>
                                        else
                                            let tWriter = typedefof<NoConversionWriter<int>>.MakeGenericType [|tSource |]
                                            let ctor = tWriter.GetConstructor [|tMod; typeof<int>|]

                                            fun (m : IAdaptiveObject) ->
                                                ctor.Invoke [|m; offset|] |> unbox<IWriter>
      
                            )

                        let creator = 
                            match creators with
                                | [s] -> s
                                | _ -> 
                                    fun (m : IAdaptiveObject) ->
                                        MultiWriter (creators |> List.map (fun c -> c m)) :> IWriter


                        name, creator
                    
                    | _ ->
                        failwithf "uniform input of unexpected type: %A" tMod
                )
            |> Seq.toList

    let private templateCache = System.Collections.Generic.Dictionary<UniformBufferLayout * Map<_,_>, list<Symbol * (IAdaptiveObject -> IWriter)>>()

    let getTemplate (layout : UniformBufferLayout) (inputTypes : Map<Symbol, Type>) =
        let key = (layout, inputTypes)
        lock templateCache (fun () ->
            match templateCache.TryGetValue key with
                | (true, template) -> template
                | _ ->
                    let template = createTemplate layout inputTypes
                    templateCache.[key] <- template
                    template
        )

    let writers (layout : UniformBufferLayout) (inputs : Map<Symbol, IAdaptiveObject>) =
        let inputTypes = inputs |> Map.map (fun _ m -> m.GetType())
        let creators = getTemplate layout inputTypes

        creators |> List.choose (fun (n,create) ->
            match Map.tryFind n inputs with
                | Some m -> Some (m, create m)
                | None -> None
        )

type UniformBuffer =
    class
        val mutable public Context : Context
        val mutable public Handle : VkBuffer
        val mutable public Storage : UnmanagedStruct
        val mutable public Layout : UniformBufferLayout
        val mutable public IsDirty : bool
        val mutable public Pointer : deviceptr

        new(ctx,h,s,l,ptr) = { Context = ctx; Handle = h; Storage = s; Layout = l; IsDirty = false; Pointer = ptr }
    end


[<AbstractClass; Sealed; Extension>]
type UniformBufferExtensions private() =

    static let createBuffer(ctx : Context, ptr : deviceptr, flags : VkBufferUsageFlags) =
        let device = ctx.Device

        let mem, off, size =
            match ptr.Pointer with
                | Real(m,s) -> m, 0L, s
                | View(m,o,s) -> m, o, s
                | Managed(_,b,s) -> b.Memory, b.Offset, s
                | _ -> failf "cannot bind null memory to uniform buffer"

        // create a buffer
        let mutable info =
            VkBufferCreateInfo(
                VkStructureType.BufferCreateInfo,
                0n, VkBufferCreateFlags.None,
                uint64 size,
                flags,
                VkSharingMode.Exclusive,
                0u,
                NativePtr.zero
            )
        let mutable buffer = VkBuffer.Null

        VkRaw.vkCreateBuffer(device.Handle, &&info, NativePtr.zero, &&buffer) |> check "vkCreateBuffer"

        let mutable reqs = VkMemoryRequirements()
        VkRaw.vkGetBufferMemoryRequirements(device.Handle, buffer, &&reqs)


        if off % int64 device.Physical.Properties.limits.minUniformBufferOffsetAlignment <> 0L then
            failwithf "invalid uniform-buffer alignment: %A" off


        if size = 0L then warnf "size 0 binding"

        // bind the buffer to the device memory
        VkRaw.vkBindBufferMemory(device.Handle, buffer, mem.Handle, uint64 off) |> check "vkBindBufferMemory"

        buffer


    [<Extension>]
    static member CreateUniformBuffer(x : Context, layout : UniformBufferLayout) =
        let storage = UnmanagedStruct.alloc layout.size
        let mem = x.DeviceLocalMemory.Alloc(int64 layout.size)
        let buffer = createBuffer(x, mem, VkBufferUsageFlags.UniformBufferBit ||| VkBufferUsageFlags.TransferDstBit)
        UniformBuffer(x, buffer, storage, layout, mem)

    [<Extension>]
    static member Delete(x : Context, ub : UniformBuffer) =
        if ub.Storage.Pointer <> 0n then
            VkRaw.vkDestroyBuffer(x.Device.Handle, ub.Handle, NativePtr.zero)
            ub.Pointer.Dispose()
            UnmanagedStruct.free ub.Storage
            ub.Storage <- UnmanagedStruct.Null

    [<Extension>]
    static member Upload(x : Context, ub : UniformBuffer) =
        Command.custom( fun s ->
            if ub.IsDirty then
                VkRaw.vkCmdUpdateBuffer(s.buffer.Handle, ub.Handle, 0UL, uint64 ub.Storage.Size, ub.Storage.Pointer)
                let updated() = ub.IsDirty <- false
                { s with cleanupActions = updated::s.cleanupActions; isEmpty = false }
            else
                s
        )




module UnmanagedUniformWriters =
    open Microsoft.FSharp.NativeInterop

    type IWriter =
        abstract member Write : IAdaptiveObject * nativeint -> unit

    [<AbstractClass>]
    type AbstractWriter() =
        abstract member Write : IAdaptiveObject * nativeint -> unit

        interface IWriter with
            member x.Write(caller, ptr) = x.Write(caller, ptr)


    type ViewWriter<'a, 'b when 'b : unmanaged>(source : IMod<'a>, fields : list<int * ('a -> 'b)>) =
        inherit AbstractWriter()
     
        let fieldValues = source |> Mod.map (fun v -> fields |> List.map (fun (o,a) -> o, a v))

        override x.Write(caller : IAdaptiveObject, ptr : nativeint) =
            let fields = fieldValues.GetValue caller
            for (offset, value) in fields do
                let ptr = NativePtr.ofNativeInt (ptr + nativeint offset)
                NativePtr.write ptr value

    type SingleValueWriter<'a when 'a : unmanaged>(source : IMod<'a>, offset : int) =
        inherit AbstractWriter()

        override x.Write(caller : IAdaptiveObject, ptr : nativeint) =
            let ptr = NativePtr.ofNativeInt (ptr + nativeint offset)
            let v = source.GetValue caller
            NativePtr.write ptr v

    type ConversionArrayWriter<'a, 'b when 'b : unmanaged>(source : IMod<'a[]>, count : int, offset : int, stride : int, convert : 'a -> 'b) =
        inherit AbstractWriter()

        let stride = 
            if stride = 0 then sizeof<'a>
            else stride

        let converted =
            source |> Mod.map (Array.map convert)

        override x.Write(caller : IAdaptiveObject, ptr : nativeint) =
            let mutable ptr = NativePtr.ofNativeInt (ptr + nativeint offset)
            let v = converted.GetValue caller

            let c = min count v.Length
            for i in 0..c-1 do
                NativePtr.write ptr v.[i]
                ptr <- NativePtr.ofNativeInt (NativePtr.toNativeInt ptr + nativeint stride)

            for i in c..count-1 do
                NativePtr.write ptr Unchecked.defaultof<'b>

    type ConversionWriter<'a, 'b when 'b : unmanaged>(source : IMod<'a>, offset : int, convert : 'a -> 'b) =
        inherit AbstractWriter()

        override x.Write(caller : IAdaptiveObject, ptr : nativeint) =
            let mutable ptr = NativePtr.ofNativeInt (ptr + nativeint offset)
            let v = source.GetValue caller
            let res = convert v
            NativePtr.write ptr res


    type NoConversionWriter<'a when 'a : unmanaged>(source : IMod<'a>, offset : int) =
        inherit AbstractWriter()

        override x.Write(caller : IAdaptiveObject, ptr : nativeint) =
            let mutable ptr = NativePtr.ofNativeInt (ptr + nativeint offset)
            let v = source.GetValue caller
            NativePtr.write ptr v

    type ConversionSeqWriter<'s, 'a, 'b when 'b : unmanaged and 's :> seq<'a>>(source : IMod<'s>, count : int, offset : int, stride : int, convert : 'a -> 'b) =
        inherit AbstractWriter()

        let stride = 
            if stride = 0 then sizeof<'a>
            else stride

        let converted =
            source |> Mod.map (Seq.toArray >> Array.map convert)

        override x.Write(caller : IAdaptiveObject, ptr : nativeint) =
            let mutable ptr = NativePtr.ofNativeInt (ptr + nativeint offset)
            let v = converted.GetValue caller

            let c = min count v.Length
            for i in 0..c-1 do
                NativePtr.write ptr v.[i]
                ptr <- NativePtr.ofNativeInt (NativePtr.toNativeInt ptr + nativeint stride)

            for i in c..count-1 do
                NativePtr.write ptr Unchecked.defaultof<'b>

    type MultiWriter(writers : list<IWriter>) =
        inherit AbstractWriter()

        override x.Write(caller : IAdaptiveObject, ptr : nativeint) =
            for w in writers do w.Write(caller, ptr)

    
    

    let private createTemplate (layout : UniformBufferLayout) (inputTypes : Map<Symbol, Type>) =
        layout.fieldTypes
            |> Seq.groupBy(fun f -> f.UniformName) 
            |> Seq.map (fun (n,g) -> Sym.ofString n, g |> Seq.toList) 
            |> Seq.map (fun (name, fields) ->
                let tMod = 
                    match Map.tryFind name inputTypes with
                        | Some tMod -> tMod
                        | _ -> failwithf "could not determine input type for semantic: %A" name

                match tMod with
                    | ModOf tSource ->

                        let creators = 
                            fields |> List.map (fun f ->
                                let transpose = not f.isRowMajor

                                let tTarget = UniformConverter.getExpectedType target f.uniformType
                                if f.count = 1 then
                                        
                                    if tSource <> tTarget then
                                        let converter = UniformPaths.compileUniformPathUntyped transpose f.path tSource tTarget

                                        let tWriter = typedefof<ConversionWriter<int,int>>.MakeGenericType [|tSource; tTarget|]
                                        let ctor = tWriter.GetConstructor [|tMod; typeof<int>; converter.GetType()|]

                                        fun (m : IAdaptiveObject) ->
                                            ctor.Invoke [|m; f.offset; converter|] |> unbox<IWriter>
                                    else
                                        let tWriter = typedefof<NoConversionWriter<int>>.MakeGenericType [|tSource |]
                                        let ctor = tWriter.GetConstructor [|tMod; typeof<int>|]

                                        fun (m : IAdaptiveObject) ->
                                            ctor.Invoke [|m; f.offset|] |> unbox<IWriter>

                                else
                                    let tSeq = tSource.GetInterface("System.Collections.Generic.IEnumerable`1") 

                                    if tSeq <> null then
                                        let tSourceElement = tSeq.GetGenericArguments().[0]
                                        let converter = PrimitiveValueConverter.getUniformConverter f.isRowMajor tSourceElement tTarget

                                        let ctor = 
                                            if tSource.IsArray then
                                                let tWriter = typedefof<ConversionArrayWriter<int,int>>.MakeGenericType [|tSourceElement; tTarget|]
                                                tWriter.GetConstructor [|tMod; typeof<int>; typeof<int>; typeof<int>; converter.GetType()|]
                                            else
                                                let tWriter = typedefof<ConversionSeqWriter<list<int>,int,int>>.MakeGenericType [|tSource; tSourceElement; tTarget|]
                                                tWriter.GetConstructor [|tMod; typeof<int>; typeof<int>; typeof<int>; converter.GetType()|]

                                        fun (m : IAdaptiveObject) ->
                                            ctor.Invoke [|m; f.count; f.offset; f.arrayStride; converter|] |> unbox<IWriter>

                                    else
                                        failwithf "cannot write non-enumerable value to uniform-array: %A" f
                            )

                        let creator = 
                            match creators with
                                | [s] -> s
                                | _ -> 
                                    fun (m : IAdaptiveObject) ->
                                        MultiWriter (creators |> List.map (fun c -> c m)) :> IWriter


                        name, creator
                    
                    | _ ->
                        failwithf "uniform input of unexpected type: %A" tMod
               )
            |> Seq.toList

    let private templateCache = System.Collections.Generic.Dictionary<ConversionTarget * list<_> * Map<_,_>, list<Symbol * (IAdaptiveObject -> IWriter)>>()

    let internal getTemplate (target : ConversionTarget) (fields : list<UniformField>) (inputTypes : Map<Symbol, Type>) =
        let key = (target, fields, inputTypes)
        lock templateCache (fun () ->
            match templateCache.TryGetValue key with
                | (true, template) -> template
                | _ ->
                    let template = createTemplate target fields inputTypes
                    templateCache.[key] <- template
                    template
        )


    let writers (buffer : bool) (fields : list<UniformField>) (inputs : Map<Symbol, IAdaptiveObject>) =
        let inputTypes = inputs |> Map.map (fun _ m -> m.GetType())
        let target = if buffer then ConversionTarget.ConvertForBuffer else ConversionTarget.ConvertForLocation
        let creators = getTemplate target fields inputTypes

        creators |> List.choose (fun (n,create) ->
            match Map.tryFind n inputs with
                | Some m -> Some (m, create m)
                | None -> None
        )
