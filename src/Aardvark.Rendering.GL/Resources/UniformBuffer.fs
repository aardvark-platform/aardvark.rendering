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

#nowarn "9"


[<AutoOpen>]
module private BufferMemoryUsage =

    let addUniformBuffer (ctx:Context) size =
        Interlocked.Increment(&ctx.MemoryUsage.UniformBufferCount) |> ignore
        Interlocked.Add(&ctx.MemoryUsage.UniformBufferMemory,size) |> ignore

    let removeUniformBuffer (ctx:Context) size =
        Interlocked.Decrement(&ctx.MemoryUsage.UniformBufferCount) |> ignore
        Interlocked.Add(&ctx.MemoryUsage.UniformBufferMemory,-size) |> ignore

    let addUniformPool (ctx:Context) size =
        Interlocked.Increment(&ctx.MemoryUsage.UniformPoolCount) |> ignore
        Interlocked.Add(&ctx.MemoryUsage.UniformPoolMemory,size) |> ignore

    let removeUniformPool (ctx:Context) size =
        Interlocked.Decrement(&ctx.MemoryUsage.UniformPoolCount) |> ignore
        Interlocked.Add(&ctx.MemoryUsage.UniformPoolMemory,-size) |> ignore

    let updateUniformPool (ctx:Context) oldSize newSize =
        Interlocked.Add(&ctx.MemoryUsage.UniformPoolMemory,newSize-oldSize) |> ignore

    let addUniformBufferView (ctx:Context) size =
        Interlocked.Increment(&ctx.MemoryUsage.UniformBufferViewCount) |> ignore
        Interlocked.Add(&ctx.MemoryUsage.UniformBufferViewMemory,size) |> ignore

    let removeUniformBufferView (ctx:Context) size =
        Interlocked.Decrement(&ctx.MemoryUsage.UniformBufferViewCount) |> ignore
        Interlocked.Add(&ctx.MemoryUsage.UniformBufferViewMemory,-size) |> ignore

type UniformPath =
    | ValuePath of string
    | IndexPath of UniformPath * int
    | FieldPath of UniformPath * string

type UniformField = 
    { 
        semantic : string
        path : UniformPath
        offset : int
        arrayStride : int
        uniformType : ActiveUniformType
        isRowMajor : bool
        count : int 
    } with
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

module UniformConverter =

    let private uniformTypes =
        Dict.ofList [
            ActiveUniformType.Int ,                 typeof<int>
            ActiveUniformType.IntVec2 ,             typeof<V2i>
            ActiveUniformType.IntVec3 ,             typeof<V3i>
            ActiveUniformType.IntVec4 ,             typeof<V4i>

            ActiveUniformType.UnsignedInt ,         typeof<uint32>
            ActiveUniformType.UnsignedIntVec3 ,     typeof<C3ui>
            ActiveUniformType.UnsignedIntVec4 ,     typeof<C4ui>

            ActiveUniformType.Float ,               typeof<float32>
            ActiveUniformType.FloatVec2 ,           typeof<V2f>
            ActiveUniformType.FloatVec3 ,           typeof<V3f>
            ActiveUniformType.FloatVec4 ,           typeof<V4f>
            ActiveUniformType.FloatMat2 ,           typeof<M22f>
            ActiveUniformType.FloatMat3 ,           typeof<M34f>
            ActiveUniformType.FloatMat4 ,           typeof<M44f>
            ActiveUniformType.FloatMat2x3 ,         typeof<M23f>
            ActiveUniformType.FloatMat3x4 ,         typeof<M34f>

            ActiveUniformType.Double ,              typeof<float>
            ActiveUniformType.DoubleVec2 ,          typeof<V2d>
            ActiveUniformType.DoubleVec3 ,          typeof<V3d>
            ActiveUniformType.DoubleVec4 ,          typeof<V4d>
            

            ActiveUniformType.Bool,                 typeof<int>
            ActiveUniformType.BoolVec2 ,            typeof<V2i>
            ActiveUniformType.BoolVec3 ,            typeof<V3i>
            ActiveUniformType.BoolVec4 ,            typeof<V4i>

        ]

    let private locationUniformTypes =
        Dict.union [
            uniformTypes
            Dict.ofList [
                ActiveUniformType.FloatMat3 ,           typeof<M33f>
            ]
        ]

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

    let getTotalFieldSize (target : ConversionTarget) (f : UniformField) =
        let t = getExpectedType target f.uniformType
        Marshal.SizeOf(t) * f.count


module AttributeType =
    let private attribTypes =
        Dict.ofList [
            ActiveAttribType.Double, typeof<float>
            ActiveAttribType.DoubleMat2, typeof<M22d>
            ActiveAttribType.DoubleMat2x3, typeof<M23d>
            ActiveAttribType.DoubleMat3, typeof<M33d>
            ActiveAttribType.DoubleMat3x4, typeof<M34d>
            ActiveAttribType.DoubleMat4, typeof<M44d>
            ActiveAttribType.DoubleVec2, typeof<V2d>
            ActiveAttribType.DoubleVec3, typeof<V3d>
            ActiveAttribType.DoubleVec4, typeof<V4d>

            ActiveAttribType.Float, typeof<float32>
            ActiveAttribType.FloatMat2, typeof<M22f>
            ActiveAttribType.FloatMat2x3, typeof<M23f>
            ActiveAttribType.FloatMat3, typeof<M33f>
            ActiveAttribType.FloatMat3x4, typeof<M34f>
            ActiveAttribType.FloatMat4, typeof<M44f>
            ActiveAttribType.FloatVec2, typeof<V2f>
            ActiveAttribType.FloatVec3, typeof<V3f>
            ActiveAttribType.FloatVec4, typeof<V4f>

            ActiveAttribType.Int, typeof<int>
            ActiveAttribType.IntVec2, typeof<V2i>
            ActiveAttribType.IntVec3, typeof<V3i>
            ActiveAttribType.IntVec4, typeof<V4i>

            ActiveAttribType.UnsignedInt, typeof<uint32>
            ActiveAttribType.UnsignedIntVec3, typeof<C3ui>
            ActiveAttribType.UnsignedIntVec4, typeof<C4ui>
        ]

    let getExpectedType (t : ActiveAttribType) =
        match attribTypes.TryGetValue t with
            | (true, t) -> t
            | _ ->   failwithf "[GL] could not get expected type for attrib-type: %A" t

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


    let private createLeafTransformation (transpose : bool) (outputType : Type) (input : Expr) =
        if input.Type <> outputType then
            let converter = PrimitiveValueConverter.getUniformConverter transpose input.Type outputType
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
                        let element = Expr.PropertyGet(input, p, [Expr.Value index])
                        element
                    else
                        failwithf "input-type does not support indexing"
          

    let private cache = Dictionary<UniformPath * Type * Type, obj>()

    let compileUniformPathUntyped (transpose : bool) (path : UniformPath) (inputType : Type) (outputType : Type) =
        lock cache (fun () ->
            let key = (path, inputType, outputType)
            match cache.TryGetValue key with
                | (true, f) -> f
                | _ ->
                    let result = 
                        match path with
                            | ValuePath _ -> 
                                PrimitiveValueConverter.getUniformConverter transpose inputType outputType
                            | _ -> 
                                let input = Var("input", inputType)
                                let e = createUniformPath (Expr.Var input) path
                                let lambda = Expr.Lambda(input, createLeafTransformation transpose outputType e)
                                lambda.CompileUntyped()
                    cache.[key] <- result
                    result
        )

    let compileUniformPath (rowMajor : bool) (path : UniformPath) : 'a -> 'b =
        compileUniformPathUntyped rowMajor path typeof<'a> typeof<'b> |> unbox<_>

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

type UniformBufferView =
    class
        val mutable public Buffer : Aardvark.Rendering.GL.Buffer
        val mutable public Offset : nativeint
        val mutable public Size : nativeint

        new(b,o,s) = { Buffer = b; Offset = o; Size = s }

    end



[<AutoOpen>]
module UniformBufferExtensions =
    open System.Linq
    open System.Collections.Generic
    open System.Runtime.CompilerServices
    open System.Diagnostics

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

                addUniformBuffer x (int64 size)
                UniformBuffer(x, handle, size, fields)
            )

        member x.Delete(b : UniformBuffer) =
            using x.ResourceLock (fun _ ->
                GL.DeleteBuffer(b.Handle)
                GL.Check "could not delete uniform buffer"

                removeUniformBuffer x (int64 b.Size)
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
    
