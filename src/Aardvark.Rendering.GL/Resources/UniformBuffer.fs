#if INTERACTIVE
#I @"E:\Development\Aardvark-2015\build\Release\AMD64"
#r "Aardvark.Base.TypeProviders.dll"
#r "Aardvark.Base.dll"
#r "Aardvark.Base.FSharp.dll"
#r "OpenTK.dll"
#r "FSharp.PowerPack.dll"
#r "FSharp.PowerPack.Linq.dll"
#r "FSharp.PowerPack.Metadata.dll"
#r "FSharp.PowerPack.Parallel.Seq.dll"
#r "Aardvark.Rendering.GL.dll"
open Aardvark.Rendering.GL
#else
namespace Aardvark.Rendering.GL
#endif
open System
open System.Collections.Generic
open System.Threading
open System.Collections.Concurrent
open System.Runtime.InteropServices
open Aardvark.Base
open OpenTK
open OpenTK.Platform
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Microsoft.FSharp.Quotations
open FSharp.Quotations.Evaluator
open Aardvark.Base.Incremental
open System.Reflection
open Microsoft.FSharp.Reflection
open Microsoft.FSharp.Quotations.Patterns

#nowarn "9"

type UniformPath =
    | ValuePath of string
    | IndexPath of UniformPath * int
    | FieldPath of UniformPath * string

type UniformField = 
    { semantic : string; path : UniformPath; offset : int; uniformType : ActiveUniformType; count : int } with
    member x.UniformName =
        let rec name (p : UniformPath) =
            match p with
                | ValuePath n -> n
                | IndexPath(i,_) -> name i
                | FieldPath(i,_) -> name i
        name x.path



type ConversionTarget =
    | ConvertForBuffer
    | ConvertForLocation



module UniformConversion =

    let private trafoToM44f(t : Trafo3d) =
        M44f.op_Explicit (t.Forward)

    let private compiledConversions =
        [
            ( fun (b : bool)      -> if b then 1 else 0 ) :> obj
     
            ( fun (t : Trafo3d)   -> trafoToM44f t ) :> obj
            ( fun (t : M44d)      -> M44f.op_Explicit t ) :> obj
            ( fun (v : M44d)      -> M34f(float32 v.M00, float32 v.M01, float32 v.M02, float32 v.M03, float32 v.M10, float32 v.M11, float32 v.M12, float32 v.M13, float32 v.M20, float32 v.M21, float32 v.M22, float32 v.M23) ) :> obj

            ( fun (v : M33f)      -> M34f(v.M00, v.M01, v.M02, 0.0f, v.M10, v.M11, v.M12, 0.0f, v.M20, v.M21, v.M22, 0.0f) ) :> obj
            ( fun (v : M33d)      -> M34f(float32 v.M00, float32 v.M01, float32 v.M02, 0.0f, float32 v.M10, float32 v.M11, float32 v.M12, 0.0f, float32 v.M20, float32 v.M21, float32 v.M22, 0.0f) ) :> obj
 
            ( fun (v : V4d)       -> V4f.op_Explicit v ) :> obj
            ( fun (v : V3d)       -> V3f.op_Explicit v ) :> obj
            ( fun (v : V2d)       -> V2f.op_Explicit v ) :> obj
            ( fun (v : float)     -> float32 v ) :> obj

            ( fun (c : C4b)     -> V4f (C4f c) ) :> obj
            ( fun (c : C4us)    -> V4f (C4f c) ) :> obj
            ( fun (c : C4ui)    -> V4f (C4f c) ) :> obj
            ( fun (c : C4f)     -> V4f c ) :> obj
            ( fun (c : C4d)     -> V4f c ) :> obj
  
            ( fun (c : C3b)     -> V4f (C4f c) ) :> obj
            ( fun (c : C3us)    -> V4f (C4f c) ) :> obj
            ( fun (c : C3ui)    -> V4f (C4f c) ) :> obj
            ( fun (c : C3f)     -> V4f c ) :> obj
            ( fun (c : C3d)     -> V4f c ) :> obj
        ]

    let private uniformTypes =
        Dict.ofList [
            ActiveUniformType.Bool,                 typeof<int>
            ActiveUniformType.BoolVec2 ,            typeof<V2i>
            ActiveUniformType.BoolVec3 ,            typeof<V3i>
            ActiveUniformType.BoolVec4 ,            typeof<V4i>
            ActiveUniformType.Double ,              typeof<float>
            ActiveUniformType.DoubleVec2 ,          typeof<V2d>
            ActiveUniformType.DoubleVec3 ,          typeof<V3d>
            ActiveUniformType.DoubleVec4 ,          typeof<V4d>
            ActiveUniformType.Float ,               typeof<float32>
            ActiveUniformType.FloatMat2 ,           typeof<M22f>
            ActiveUniformType.FloatMat3 ,           typeof<M34f>
            ActiveUniformType.FloatMat3x4 ,         typeof<M34f>
            ActiveUniformType.FloatMat4 ,           typeof<M44f>
            ActiveUniformType.FloatVec2 ,           typeof<V2f>
            ActiveUniformType.FloatVec3 ,           typeof<V3f>
            ActiveUniformType.FloatVec4 ,           typeof<V4f>
            ActiveUniformType.Int ,                 typeof<int>
            ActiveUniformType.IntVec2 ,             typeof<V2i>
            ActiveUniformType.IntVec3 ,             typeof<V3i>
            ActiveUniformType.IntVec4 ,             typeof<V4i>
            ActiveUniformType.UnsignedInt ,         typeof<uint32>
        ]

    let private locationUniformTypes =
        Dict.union [
            uniformTypes
            Dict.ofList [
                ActiveUniformType.FloatMat3 ,           typeof<M33f>
            ]
        ]

    type private ConversionMapping<'a>() =
        let store = Dict<Type, Dict<Type, 'a>>()

        member x.Add(input : Type, output : Type, e : 'a) =
            let map = store.GetOrCreate(input, fun _ -> Dict())
            map.[output] <- e

        member x.TryGet(input : Type, output : Type, [<Out>] e : byref<'a>) =
            match store.TryGetValue input with
                | (true, m) ->
                    m.TryGetValue(output, &e)
                | _ ->
                    false

    let private createCompiledMap (l : list<obj>) =
        let result = ConversionMapping()

        for e in l do
            let (i,o) = FSharpType.GetFunctionElements (e.GetType())
            result.Add(i,o,e)

        result

    let private compiledMapping = createCompiledMap compiledConversions


    let getExpectedType (target : ConversionTarget) (t : ActiveUniformType) =
        match target with
            | ConvertForBuffer ->
                match uniformTypes.TryGetValue t with
                    | (true, t) -> t
                    | _ -> failwithf "unsupported uniform type: %A" t

            | ConvertForLocation ->
                match locationUniformTypes.TryGetValue t with
                    | (true, t) -> t
                    | _ -> failwithf "unsupported uniform type: %A" t
        
    let getConverter (inType : Type) (outType : Type) =
        if outType.IsArray then
            failwith "arrays are currently not implemented"
        else
            match compiledMapping.TryGet(inType, outType) with
                | (true, conv) ->
                    conv
                | _ ->
                    failwithf "unknown conversion from %A to %A" inType.FullName outType.FullName

    let getTotalFieldSize (target : ConversionTarget) (f : UniformField) =
        let t = getExpectedType target f.uniformType
        Marshal.SizeOf(t) * f.count

module UniformPaths =
    // struct A { int a; float b; }
    // struct B { int c; A inner[2]; }
    // uniform SomeBuffer {
    //      B Values[2];
    //      vec4 Test;
    // }

    // Values[0].c
    // Values[0].A[0].a
    // Values[0].A[0].b
    // Values[0].A[1].a
    // Values[0].A[1].b
    // Values[1].c
    // Values[1].A[0].a
    // Values[1].A[0].b
    // Values[1].A[1].a
    // Values[1].A[1].b
    // Test


    let private createLeafTransformation (outputType : Type) (input : Expr) =
        if input.Type <> outputType then
            let converter = UniformConversion.getConverter input.Type outputType
            let f = Expr.Value(converter, converter.GetType())
            Expr.Application(f, input)
        else
            input

    let private getArrayMethod =
        let t = Type.GetType("Microsoft.FSharp.Core.LanguagePrimitives+IntrinsicFunctions, FSharp.Core")
        let mi = t.GetMethod("GetArray")
        mi

    let private all = BindingFlags.NonPublic ||| BindingFlags.Instance ||| BindingFlags.Public

    let rec private createUniformPath (input : Expr) (path : UniformPath) =
        match path with
            | ValuePath _ -> input
            | FieldPath(inner, name) ->
                let input = createUniformPath input inner
                let f = input.Type.GetMember(name, MemberTypes.Field ||| MemberTypes.Property, all)

                if f.Length = 1 then
                    match f.[0] with
                        | :? PropertyInfo as f ->
                            let fieldValue = Expr.PropertyGet(input, f)
                            fieldValue
                        | :? FieldInfo as f ->
                            let fieldValue = Expr.FieldGet(input, f)
                            fieldValue
                        | mem ->
                            failwithf "unexpected member-info: %A" mem
                else
                    failwithf "could not get member: %A (%A)" name f

            | IndexPath(inner, index) ->
                if input.Type.IsArray then
                    let input = createUniformPath input inner

                    let elementType = input.Type.GetElementType()

                    let getArrayMethod = getArrayMethod.MakeGenericMethod [|elementType|]
                    let element = Expr.Call(getArrayMethod, [input; Expr.Value(index)])
                    printfn "element type: %A" element.Type.Name

                    element
                else
                    let input = createUniformPath input inner

                    let p = input.Type.GetProperty("Item", all)
                    if p <> null then
                        let element = Expr.PropertyGet(input, p)
                        element
                    else
                        failwithf "input-type does not support indexing"
          

    let private cache = Dictionary<UniformPath * Type * Type, obj>()

    let compileUniformPathUntyped (path : UniformPath) (inputType : Type) (outputType : Type) =
        lock cache (fun () ->
            let key = (path, inputType, outputType)
            match cache.TryGetValue key with
                | (true, f) -> f
                | _ ->
                    let result = 
                        match path with
                            | ValuePath _ -> 
                                UniformConversion.getConverter inputType outputType
                            | _ -> 
                                let input = Var("input", inputType)
                                let e = createUniformPath (Expr.Var input) path
                                let lambda = Expr.Lambda(input, createLeafTransformation outputType e)
                                lambda.CompileUntyped()
                    cache.[key] <- result
                    result
        )

    let compileUniformPath (path : UniformPath) : 'a -> 'b =
        compileUniformPathUntyped path typeof<'a> typeof<'b> |> unbox<_>


module UnmanagedWriters =
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

    
    

    let private createTemplate (target : ConversionTarget) (fields : list<UniformField>) (inputTypes : Map<Symbol, Type>) =
        fields 
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
                                let tTarget = UniformConversion.getExpectedType target f.uniformType
                                if f.count = 1 then
                                        
                                    if tSource <> tTarget then
                                        let converter = UniformPaths.compileUniformPathUntyped f.path tSource tTarget

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
                                        let converter = UniformConversion.getConverter tSource tTarget

                                        let ctor = 
                                            if tSource.IsArray then
                                                let tWriter = typedefof<ConversionArrayWriter<int,int>>.MakeGenericType [|tSourceElement; tTarget|]
                                                tWriter.GetConstructor [|tMod; typeof<int>; typeof<int>; typeof<int>; converter.GetType()|]
                                            else
                                                let tWriter = typedefof<ConversionSeqWriter<list<int>,int,int>>.MakeGenericType [|tSource; tSourceElement; tTarget|]
                                                tWriter.GetConstructor [|tMod; typeof<int>; typeof<int>; typeof<int>; converter.GetType()|]

                                        fun (m : IAdaptiveObject) ->
                                            ctor.Invoke [|m; f.count; f.offset; 0; converter|] |> unbox<IWriter>

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

type UniformBuffer(ctx : Context, handle : int, size : int, fields : list<UniformField>) =
    let data = Marshal.AllocHGlobal(size)
    let mutable dirty = true

    member x.Free() = Marshal.FreeHGlobal data
    member x.Context = ctx
    member x.Handle = handle
    member x.Size = size
    member x.Fields = fields
    member x.Data = data
    member x.Dirty 
        with get() = dirty
        and set d = dirty <- d


type UniformBufferPool =
    class
        val mutable public Context : Context
        val mutable public Handle : int
        val mutable public Size : int
        val mutable public Storage : MemoryManager
        val mutable public Fields : list<UniformField>
        val mutable public ElementSize : int
        val mutable public ViewCount : int
        val mutable public DirtyCount : int

        member x.AllocView() =
            Interlocked.Increment &x.ViewCount |> ignore
            new UniformBufferView(x, x.Storage.Alloc(x.ElementSize))

        member x.Free(view : UniformBufferView) =
            Interlocked.Decrement &x.ViewCount |> ignore
            ManagedPtr.free view.Pointer

        member x.ConsumeDirtyCount() =
            Interlocked.Exchange(&x.DirtyCount, 0)

        member internal x.Updated(view : UniformBufferView) =
            Interlocked.Increment &x.DirtyCount |> ignore

        new(ctx, handle, elementSize, elementFields) = { Context = ctx; Handle = handle; Size = 0; Storage = MemoryManager.createHGlobal(); Fields = elementFields; ElementSize = elementSize; ViewCount = 0; DirtyCount = 0 }
    end

and UniformBufferView(pool : UniformBufferPool, ptr : managedptr) =
    member internal x.Pointer = ptr

    member x.Pool = pool
    member x.Handle = pool.Handle
    member x.Offset = ptr.Offset
    member x.Size = ptr.Size
    member x.Fields = pool.Fields
    member x.Data = pool.Storage.Pointer + ptr.Offset

    member x.Dispose() = 
        pool.Free x

    member x.WriteOperation(f : unit -> 'a) =
        let res = 
            ReaderWriterLock.read pool.Storage.PointerLock (fun () ->
                f()
            )
        pool.Updated x
        res

    interface IDisposable with
        member x.Dispose() = x.Dispose()




[<AutoOpen>]
module UniformBufferExtensions =

    type Context with
        member x.CreateUniformBufferPool(size : int, fields : list<UniformField>) =
            using x.ResourceLock (fun _ ->
                let alignMask = GL.GetInteger(GetPName.UniformBufferOffsetAlignment) - 1 
                printfn "align: %A" (alignMask + 1)
                let size = size + alignMask &&& ~~~alignMask
                let handle = GL.GenBuffer()
                GL.Check "could not create uniform buffer"

                new UniformBufferPool(x, handle, size, fields)
            )

        member x.Upload(pool : UniformBufferPool) =
            using x.ResourceLock (fun _ ->
                GL.BindBuffer(BufferTarget.CopyWriteBuffer, pool.Handle)
                GL.Check "could not bind uniform buffer pool"

                if pool.Size = pool.Storage.Capacity then
                    GL.BufferSubData(BufferTarget.CopyWriteBuffer, 0n, nativeint pool.Size, pool.Storage.Pointer)
                else
                    pool.Size <- pool.Storage.Capacity
                    GL.BufferData(BufferTarget.CopyWriteBuffer, nativeint pool.Storage.Capacity, pool.Storage.Pointer, BufferUsageHint.DynamicDraw)              
                GL.Check "could not upload uniform buffer pool"      

                GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0)
                GL.Check "could not unbind uniform buffer pool"
            )

        member x.Delete(pool : UniformBufferPool) =
            using x.ResourceLock (fun _ ->
                pool.Storage.Dispose()
                GL.DeleteBuffer(pool.Handle)
                GL.Check "could not delete uniform buffer pool"
                pool.Handle <- -1
            )

    type Context with
        member x.CreateUniformBuffer(size : int, fields : list<UniformField>) =
            using x.ResourceLock (fun _ ->
                
                let handle = GL.GenBuffer()
                GL.Check "could not create uniform buffer"

                GL.BindBuffer(BufferTarget.CopyWriteBuffer, handle)
                GL.Check "could not bind uniform buffer"

                GL.BufferData(BufferTarget.CopyWriteBuffer, nativeint size, 0n, BufferUsageHint.DynamicRead)
                GL.Check "could not allocate uniform buffer"

                GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0)
                GL.Check "could not unbind uniform buffer"

                UniformBuffer(x, handle, size, fields)
            )

        member x.Delete(b : UniformBuffer) =
            using x.ResourceLock (fun _ ->
                GL.DeleteBuffer(b.Handle)
                GL.Check "could not delete uniform buffer"

                b.Free()
            )

        member x.Upload(b : UniformBuffer) =
            if b.Dirty then
                b.Dirty <- false
                using x.ResourceLock (fun _ ->
                    GL.BindBuffer(BufferTarget.CopyWriteBuffer, b.Handle)
                    GL.Check "could not bind uniform buffer"

                    GL.BufferSubData(BufferTarget.CopyWriteBuffer, 0n, nativeint b.Size, b.Data)
                    GL.Check "could not upload uniform buffer"                    

                    GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0)
                    GL.Check "could not unbind uniform buffer"
                )
    
