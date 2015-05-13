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
open System.Threading
open System.Collections.Concurrent
open System.Runtime.InteropServices
open Aardvark.Base
open OpenTK
open OpenTK.Platform
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Linq.QuotationEvaluation
open Aardvark.Base.Incremental

type UniformPath =
    | ValuePath of string
    | IndexPath of UniformPath * int
    | FieldPath of UniformPath * string

type UniformField = { path : UniformPath; offset : int; uniformType : ActiveUniformType; count : int } with
    member x.UniformName =
        let rec name (p : UniformPath) =
            match p with
                | ValuePath n -> n
                | IndexPath(i,_) -> name i
                | FieldPath(i,_) -> name i
        name x.path


module private ValueConverter =
    open System.Reflection
    open Microsoft.FSharp.Reflection
    open Microsoft.FSharp.Quotations.Patterns

    type ConversionTarget =
        | ConvertForBuffer
        | ConvertForLocation



    let conversions =
        [
            <@@ fun (b : bool)      -> if b then 1 else 0 @@>

            // * -> M44f
            <@@ fun (t : Trafo3d)   -> M44f.op_Explicit (Trafo.forward t) @@>
            <@@ fun (t : M44d)      -> M44f.op_Explicit t @@>
            <@@ fun (v : M44d)      -> M34f(float32 v.M00, float32 v.M10, float32 v.M20, float32 v.M30, float32 v.M01, float32 v.M11, float32 v.M21, float32 v.M31, float32 v.M02, float32 v.M12, float32 v.M22, float32 v.M32) @@>

            <@@ fun (v : M33f)      -> M34f(v.M00, v.M10, v.M20, 0.0f, v.M01, v.M11, v.M21, 0.0f, v.M02, v.M12, v.M22, 0.0f) @@>
            <@@ fun (v : M33d)      -> M34f(float32 v.M00, float32 v.M10, float32 v.M20, 0.0f, float32 v.M01, float32 v.M11, float32 v.M21, 0.0f, float32 v.M02, float32 v.M12, float32 v.M22, 0.0f) @@>

            <@@ fun (v : V4d)       -> V4f.op_Explicit v @@>
            <@@ fun (v : V3d)       -> V3f.op_Explicit v @@>
            <@@ fun (v : V2d)       -> V2f.op_Explicit v @@>
            <@@ fun (v : float)     -> float32 v @@>


            <@@ fun (c : C4b)     -> V4f (C4f c) @@>
            <@@ fun (c : C4us)    -> V4f (C4f c) @@>
            <@@ fun (c : C4ui)    -> V4f (C4f c) @@>
            <@@ fun (c : C4f)     -> V4f c @@>
            <@@ fun (c : C4d)     -> V4f c @@>


            <@@ fun (c : C3b)     -> V4f (C4f c) @@>
            <@@ fun (c : C3us)    -> V4f (C4f c) @@>
            <@@ fun (c : C3ui)    -> V4f (C4f c) @@>
            <@@ fun (c : C3f)     -> V4f c @@>
            <@@ fun (c : C3d)     -> V4f c @@>
        ]


    let uniformTypes =
        [
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

    let locationUniformTypes =
        [
            ActiveUniformType.FloatMat3 ,           typeof<M33f>
        ]

    module private Paths =
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


        let private createLeafTransformation (input : Expr) =
            // TODO: compile proper conversions
            input

        let private getArrayMethod =
            let t = Type.GetType("Microsoft.FSharp.Core.LanguagePrimitives+IntrinsicFunctions, FSharp.Core")
            let mi = t.GetMethod("GetArray")
            mi

        let private all = BindingFlags.NonPublic ||| BindingFlags.Instance ||| BindingFlags.Public

        let rec createUniformPath (input : Expr) (path : UniformPath) =
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
                            | _ ->
                                failwith ""
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
                            failwith ""
          
        let compileUniformPath (path : UniformPath) : 'a -> 'b =
            let input = Var("input", typeof<'a>)
            let e = createUniformPath (Expr.Var input) path
            let lambda = Expr.Lambda(input, createLeafTransformation e)
            lambda.CompileUntyped() () |> unbox<'a -> 'b>

    module private Convert =
        open System.Collections.Generic
        open Microsoft.FSharp.Quotations.Patterns



        let bufferTypeMapping = Dict.ofList uniformTypes
        let locationTypeMapping = 
            Dict.union [bufferTypeMapping; Dict.ofList locationUniformTypes]

        let getExpectedType (target : ConversionTarget) (t : ActiveUniformType) =
            match target with
                | ConvertForBuffer ->
                    match bufferTypeMapping.TryGetValue t with
                        | (true, t) -> t
                        | _ -> failwithf "unsupported uniform type: %A" t

                | ConvertForLocation ->
                    match locationTypeMapping.TryGetValue t with
                        | (true, t) -> t
                        | _ -> failwithf "unsupported uniform type: %A" t
        
        type ConversionMapping() =
            let store = Dict<Type, Dict<Type, Expr>>()

            member x.Add(input : Type, output : Type, e : Expr) =
                let map = store.GetOrCreate(input, fun _ -> Dict())
                map.[output] <- e

            member x.TryGet(input : Type, output : Type, [<Out>] e : byref<Expr>) =
                match store.TryGetValue input with
                    | (true, m) ->
                        m.TryGetValue(output, &e)
                    | _ ->
                        false

        let createMap (l : list<Expr>) =
            let result = ConversionMapping()

            for e in l do
                let (i,o) = FSharpType.GetFunctionElements e.Type
                result.Add(i,o,e)

            result

        let mapping = createMap conversions

        let getConversion (target : ConversionTarget) (input : Expr) (field : UniformField) =
            let inType = input.Type
            let outType = getExpectedType target field.uniformType

            if field.count > 1 then
                failwith "arrays are currently not implemented"
            else
                match mapping.TryGet(inType, outType) with
                    | (true, (Lambda(v, e))) ->
                        Expr.Let(v, input, e)
                    | _ ->
                        if inType = outType then
                            input
                        else
                            failwithf "unknown conversion from %A to %A" inType.FullName outType.FullName


    let addintptr (a : nativeint) (b : int) =
        a + nativeint b

    open Microsoft.FSharp.NativeInterop

    let private createSetter (target : ConversionTarget) (paths : list<UniformField>, inputType : Type) : Expr =
        let input = Var("input", inputType)
        let ptr = Var("ptr", typeof<nativeint>)

        let writers =
            let ptr = Expr.Var ptr
            paths |> List.map (fun f ->
                let offset = f.offset
                let pathValue = Paths.createUniformPath (Expr.Var input) f.path
                let resultValue = Convert.getConversion target pathValue f

                let writeMeth = typeof<Marshal>.GetMethod("StructureToPtr", [| typeof<obj>; typeof<nativeint>;typeof<bool> |])
                // TODO: use generic
                
                let pos = 
                    if offset = 0 then ptr
                    else <@@ addintptr (%%ptr : nativeint) offset @@>

                // Marshal.StructureToPtr(resultValue :> obj, ptr + (nativeint offset), false)
                Expr.Call(writeMeth, [Expr.Coerce(resultValue, typeof<obj>); pos; Expr.Value true])
            )

        let rec seq (e : list<Expr>) =
            match e with
                | [e] -> e
                | e::es -> Expr.Sequential(e, seq es)
                | [] -> Expr.Value(())

        Expr.Lambda(input, Expr.Lambda(ptr, seq writers))

    let rec private extractName (p : UniformPath) =
        match p with
            | ValuePath name -> name
            | IndexPath(inner,_) -> extractName inner
            | FieldPath(inner,_) -> extractName inner

    let rec private extractModType (t : Type) =
        if t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<IMod<_>> then
            Some (t, t.GetGenericArguments().[0])
        elif t <> typeof<obj> && t <> null then
            extractModType t.BaseType
        else
            None

    let rec private getMethodInfoInternal (e : Expr) =
        match e with
            | Patterns.Call(_,mi,_) -> 
                if mi.IsGenericMethod then mi.GetGenericMethodDefinition() |> Some
                else mi |> Some

            | ExprShape.ShapeCombination(_, args) -> 
                args |> List.tryPick getMethodInfoInternal
            | ExprShape.ShapeLambda(_,b) ->
                getMethodInfoInternal b
            | _ -> None

    let getMethodInfo (e : Expr) =
        (getMethodInfoInternal e).Value

    let private unboxMeth = getMethodInfo <@ unbox<_> @>
    let dict = ConcurrentDictionary<list<UniformField> * Type, (obj -> nativeint -> unit)>()

    let compileSetter (target : ConversionTarget) (paths : list<UniformField>) (t : Type) : obj -> nativeint -> unit =
        let create (paths, t) =
            let mt = typedefof<IMod<_>>.MakeGenericType([|t|])
            let ub = unboxMeth.MakeGenericMethod [|mt|]
            let ex = createSetter target (paths, t)
            let m = Var("mod", typeof<obj>)
            let mi = mt.GetMethod("GetValue")
            let getter = Expr.Call(Expr.Call(ub, [Expr.Var m]), mi, [])
            let ptr = Var("ptr", typeof<nativeint>)

            let ex = 
                match ex with
                    | Lambda(v, Lambda(ptr, b)) -> Expr.Lambda(m, Expr.Lambda(ptr, Expr.Let(v, getter, b)))
                    | _ -> failwith "asdasd"

            ex.CompileUntyped() () |> unbox<obj -> nativeint -> unit>

        let key = (paths,t)

        dict.GetOrAdd(key, Func<list<UniformField> * Type, obj -> nativeint -> unit>(create))

    let getTotalFieldSize (target : ConversionTarget) (f : UniformField) =
        let t = Convert.getExpectedType target f.uniformType
        Marshal.SizeOf(t) * f.count


type UniformBuffer(ctx : Context, handle : int, size : int, fields : list<UniformField>) =
    let data = Marshal.AllocHGlobal(size)
    let mutable dirty = true

    let paths = fields |> Seq.groupBy(fun f -> f.UniformName) |> Seq.map (fun (n,g) -> Sym.ofString n, g |> Seq.toList) |> SymDict.ofSeq 

    member x.CompileSetter (name : Symbol) (m : IMod) : unit -> unit =
        let t = m.GetType()

        match t with
            | ModOf(t) ->
                match paths.TryGetValue name with
                    | (true, paths) ->
                        let write = ValueConverter.compileSetter ValueConverter.ConversionTarget.ConvertForBuffer paths t

                        fun () -> 
                            dirty <- true
                            write (m :> obj) data
                    

                    | _ ->
                        id
            | _ ->
                failwith "unsupported mod-type"

    member x.Free() = Marshal.FreeHGlobal data
    member x.Context = ctx
    member x.Handle = handle
    member x.Size = size
    member x.Fields = fields
    member x.Data = data
    member x.Dirty 
        with get() = dirty
        and set d = dirty <- d


[<AutoOpen>]
module UniformBufferExtensions =

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
    

    module ExecutionContext =
        let bindUniformBuffer (index : int) (b : UniformBuffer) =
            seq {
                if ExecutionContext.uniformBuffersSupported then
                    yield Instruction.BindBufferRange (int BufferTarget.UniformBuffer) index b.Handle 0n (nativeint b.Size)

                else
                    for field in b.Fields do
                        // when uniform buffers are not supported the
                        // offsets store the uniform's location 
                        // TODO: ensure that this is done correctly 
                        //       by the shader compiler.
                        let location = field.offset
                        let data = b.Data + (nativeint location)
                        let size = ValueConverter.getTotalFieldSize ValueConverter.ConversionTarget.ConvertForBuffer field / sizeof<float32>
                        
                        // NOTE: OpenGl only supports uniforms having a
                        //       multipe of sizeof<float32>.
                        yield Instruction.Uniform1fv location size data
            }