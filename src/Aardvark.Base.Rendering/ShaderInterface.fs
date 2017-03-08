namespace Aardvark.Base.ShaderReflection

open System
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Base.Incremental

type ShaderParameterType =
    | Bool
    | Float
    | Double
    | Int
    | UnsignedInt
    | Vector of componentType : ShaderParameterType * dimension : int
    | Matrix of componentType : ShaderParameterType * rows : int * cols : int * isRowMajor : bool
    | Sampler of t : ShaderParameterType * dim : TextureDimension * ms : bool * array : bool * shadow : bool
    | Image of t : ShaderParameterType * dim : TextureDimension * ms : bool * array : bool
    | AtomicCounter of contentType : ShaderParameterType
    | DynamicArray of elementType : ShaderParameterType * stride : int
    | FixedArray of elementType : ShaderParameterType * stride : int * length : int
    | Struct of size : int * fields : list<ShaderStructField>

and ShaderStructField =
    {
        Name            : string
        Type            : ShaderParameterType
        Offset          : int
    }

[<RequireQualifiedAccess>]
type ShaderPath =
    | Value of string
    | Item of ShaderPath * int
    | Field of ShaderPath * string

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ShaderPath =
    open System.Reflection
    open System.Reflection.Emit
    
    let rec name (p : ShaderPath) =
        match p with
            | ShaderPath.Value str -> str
            | ShaderPath.Item(p,_) -> name p
            | ShaderPath.Field(p,_) -> name p

    let rec toString (p : ShaderPath) =
        match p with
            | ShaderPath.Value str -> str
            | ShaderPath.Field(p, name) -> toString p + "." + name
            | ShaderPath.Item(p, index) -> toString p + "[" + string index + "]"

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ShaderParameterType =

    let inline vector (componentType : ShaderParameterType) (dimension : int) = 
        Vector(componentType, dimension)

    let inline matrix (componentType : ShaderParameterType) (rows : int) (cols : int) = 
        Matrix(componentType, rows, cols, true)

    let inline sampler (t : ShaderParameterType) (dim : TextureDimension) (ms : bool) (array : bool) (shadow : bool) =
        Sampler(t, dim, ms, array, shadow)

    let inline image (t : ShaderParameterType) (dim : TextureDimension) (ms : bool) (array : bool) =
        Image(t, dim, ms, array)
        
    let inline atomicCounter (t : ShaderParameterType)  =
        AtomicCounter(t)

    let inline dynamicArray (t : ShaderParameterType) (stride : int) =
        DynamicArray(t, stride)
        
    let inline array (t : ShaderParameterType) (stride : int) (len : int) =
        FixedArray(t, stride, len)

    let float = Float
    let V2f = vector float 2
    let V3f = vector float 3
    let V4f = vector float 4

    let double = Double
    let V2d = vector double 2
    let V3d = vector double 3
    let V4d = vector double 4

    let int = Int
    let V2i = vector int 2
    let V3i = vector int 3
    let V4i = vector int 4
    
    let uint = UnsignedInt
    let V2ui = vector uint 2
    let V3ui = vector uint 3
    let V4ui = vector uint 4

    let bool = Bool
    let V2b = vector bool 2
    let V3b = vector bool 3
    let V4b = vector bool 4

    let M22f = matrix float 2 2
    let M23f = matrix float 2 3
    let M24f = matrix float 2 4
    let M32f = matrix float 3 2
    let M33f = matrix float 3 3
    let M34f = matrix float 3 4
    let M42f = matrix float 4 2
    let M43f = matrix float 4 3
    let M44f = matrix float 4 4

    let M22d = matrix double 2 2
    let M23d = matrix double 2 3
    let M24d = matrix double 2 4
    let M32d = matrix double 3 2
    let M33d = matrix double 3 3
    let M34d = matrix double 3 4
    let M42d = matrix double 4 2
    let M43d = matrix double 4 3
    let M44d = matrix double 4 4


    let sampler1D = sampler float TextureDimension.Texture1D false false false
    let sampler2D = sampler float TextureDimension.Texture2D false false false
    let sampler3D = sampler float TextureDimension.Texture3D false false false
    let samplerCube = sampler float TextureDimension.TextureCube false false false
    let sampler1DShadow = sampler float TextureDimension.Texture1D false false true
    let sampler2DShadow = sampler float TextureDimension.Texture2D false false true
    let sampler1DArray = sampler float TextureDimension.Texture1D false true false
    let sampler2DArray = sampler float TextureDimension.Texture2D false true false
    let sampler1DArrayShadow = sampler float TextureDimension.Texture1D false true true
    let sampler2DArrayShadow = sampler float TextureDimension.Texture2D false true true
    let sampler2DMS = sampler float TextureDimension.Texture2D true false false
    let sampler2DMSArray = sampler float TextureDimension.Texture2D true true false
    let samplerCubeShadow = sampler float TextureDimension.TextureCube false false true
    
    let isampler1D = sampler int TextureDimension.Texture1D false false false
    let isampler2D = sampler int TextureDimension.Texture2D false false false
    let isampler3D = sampler int TextureDimension.Texture3D false false false
    let isamplerCube = sampler int TextureDimension.TextureCube false false false
    let isampler1DArray = sampler int TextureDimension.Texture1D false true false
    let isampler2DArray = sampler int TextureDimension.Texture2D false true false
    let isampler2DMS = sampler int TextureDimension.Texture2D true false false
    let isampler2DMSArray = sampler int TextureDimension.Texture2D true true false
    
    let usampler1D = sampler uint TextureDimension.Texture1D false false false
    let usampler2D = sampler uint TextureDimension.Texture2D false false false
    let usampler3D = sampler uint TextureDimension.Texture3D false false false
    let usamplerCube = sampler uint TextureDimension.TextureCube false false false
    let usampler1DArray = sampler uint TextureDimension.Texture1D false true false
    let usampler2DArray = sampler uint TextureDimension.Texture2D false true false
    let usampler2DMS = sampler uint TextureDimension.Texture2D true false false
    let usampler2DMSArray = sampler uint TextureDimension.Texture2D true true false

    let rec sizeof (t : ShaderParameterType) =
        match t with
            | Bool -> 4
            | Float -> 4
            | Double -> 8
            | Int -> 4
            | UnsignedInt -> 4
            | Vector(t,d) -> sizeof t * d
            | Matrix(t, r, c, _) -> sizeof t * r * c
            | AtomicCounter t -> sizeof t
            | FixedArray(_, s, l) -> s * l
            | DynamicArray _ -> -1
            | Sampler _ -> -1
            | Image _ -> -1
            | Struct(s, _) -> s

    let private lookup (l : list<'a * 'b>) : 'a -> Option<'b> =
        let dict = Dictionary.ofList l
        fun (a : 'a) ->
            match dict.TryGetValue a with
                | (true, b) -> Some b
                | _ -> None

    let tryGetExpectedType =
        lookup [
            bool,       typeof<int>
            float,      typeof<float32>
            double,     typeof<float>
            int,        typeof<int>
            uint,       typeof<uint32>
            
            V2f,        typeof<V2f>
            V3f,        typeof<V3f>
            V4f,        typeof<V4f>
            V2d,        typeof<V2d>
            V3d,        typeof<V3d>
            V4d,        typeof<V4d>
            V2i,        typeof<V2i>
            V3i,        typeof<V3i>
            V4i,        typeof<V4i>
            V3ui,       typeof<C3ui>
            V4ui,       typeof<C4ui>

            M22f,       typeof<M22f>
            M23f,       typeof<M23f>
            M33f,       typeof<M34f>
            M34f,       typeof<M34f>
            M44f,       typeof<M44f>
            M22d,       typeof<M22d>
            M23d,       typeof<M23d>
            M33d,       typeof<M33d>
            M34d,       typeof<M34d>
            M44d,       typeof<M44d>

        ]

    let getExpectedType (t : ShaderParameterType) =
        match tryGetExpectedType t with
            | Some t -> t
            | None -> failwithf "[Shader] cannot get expected type for %A" t

    let rec makeRowMajor (t : ShaderParameterType) =
        match t with
            | Matrix(t, r, c, _) -> 
                Matrix(t, r, c, true)

            | FixedArray(t, s, l) -> 
                FixedArray(makeRowMajor t, s, l)

            | DynamicArray(t, s) -> 
                DynamicArray(makeRowMajor t, s)

            | Struct(s, fields) ->
                Struct(s, fields |> List.map (fun f -> { f with Type = makeRowMajor f.Type }))

            | _ ->
                t

    let rec flipMatrixMajority (t : ShaderParameterType) =
        match t with
            | Matrix(t, r, c, m) -> 
                Matrix(t, r, c, not m)

            | FixedArray(t, s, l) -> 
                FixedArray(flipMatrixMajority t, s, l)

            | DynamicArray(t, s) -> 
                DynamicArray(flipMatrixMajority t, s)

            | Struct(s, fields) ->
                Struct(s, fields |> List.map (fun f -> { f with Type = flipMatrixMajority f.Type }))

            | _ ->
                t

    let private prefix (t : ShaderParameterType) =
        match t with
            | Bool -> "b"
            | Float -> ""
            | Double -> "d"
            | Int -> "i"
            | UnsignedInt -> "u"
            | _ -> ""
        
    let private dimstr (d : TextureDimension) =
        match d with
            | TextureDimension.Texture1D -> "1D"
            | TextureDimension.Texture2D -> "2D"
            | TextureDimension.Texture3D -> "3D"
            | TextureDimension.TextureCube -> "Cube"
            | _ -> ""

    let rec toString (t : ShaderParameterType) =
        match t with
            | Bool -> "bool"
            | Float -> "float"
            | Double -> "double"
            | Int -> "int"
            | UnsignedInt -> "uint"
            | Vector(t,d) -> prefix t + "vec" + string d
            | Matrix(t, r, c, _) when r = c -> prefix t + "mat" + string r
            | Matrix(t, r, c, _) -> prefix t + "mat" + string r + "x" + string c
            | AtomicCounter t -> toString t
            | FixedArray(e, _, l) -> toString e + "[" + string l + "]"
            | DynamicArray(e,_) -> toString e + "[]"
            | Sampler(t, dim, ms, array, shadow) -> prefix t + "sampler" + dimstr dim + (if ms then "MS" else "") + (if array then "Array" else "") + (if shadow then "Shadow" else "")
            | Image(t, dim, ms, array) -> prefix t + "image" + dimstr dim + (if ms then "MS" else "") + (if array then "Array" else "")
            | Struct(s, fields) ->
                fields |> List.map (fun f -> f.Name + " : " + toString f.Type) |> String.concat ";" |> sprintf "{ %s }"


type ShaderBlockField =
    {
        Path            : ShaderPath
        Type            : ShaderParameterType
        Offset          : int
        Referenced      : Set<ShaderStage>
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ShaderBlockField =
    let rec flipMatrixMajority (t : ShaderBlockField) =
        { t with Type = ShaderParameterType.flipMatrixMajority t.Type }

    let toString (f : ShaderBlockField) =
        sprintf "%s : %s" (ShaderPath.toString f.Path) (ShaderParameterType.toString f.Type)

type ShaderBlock =
    {
        Index           : int
        Name            : string
        Fields          : list<ShaderBlockField>
        Referenced      : Set<ShaderStage>
        DataSize        : int
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ShaderBlock =
    let rec flipMatrixMajority (t : ShaderBlock) =
        { t with Fields = t.Fields |> List.map ShaderBlockField.flipMatrixMajority }

    let toString (b : ShaderBlock) =
        b.Fields |> List.map ShaderBlockField.toString |> String.concat "; " |> sprintf "%s { %s }" b.Name

type ShaderParameter =
    {
        Location        : int
        Path            : ShaderPath
        Type            : ShaderParameterType 
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ShaderParameter =
    let rec flipMatrixMajority (t : ShaderParameter) =
        { t with Type = ShaderParameterType.flipMatrixMajority t.Type }

    let toString (p : ShaderParameter) =
        sprintf "%s : %s" (ShaderPath.toString p.Path) (ShaderParameterType.toString p.Type)

type ShaderInterface =
    {
        Inputs          : list<ShaderParameter>
        Outputs         : list<ShaderParameter>
        Uniforms        : list<ShaderParameter>
        UniformBlocks   : list<ShaderBlock>
        StorageBlocks   : list<ShaderBlock>
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ShaderInterface =
    let flipMatrixMajority (iface : ShaderInterface) : ShaderInterface =
        {
            Inputs          = iface.Inputs |> List.map ShaderParameter.flipMatrixMajority
            Outputs         = iface.Outputs |> List.map ShaderParameter.flipMatrixMajority
            Uniforms        = iface.Uniforms |> List.map ShaderParameter.flipMatrixMajority
            UniformBlocks   = iface.UniformBlocks |> List.map ShaderBlock.flipMatrixMajority
            StorageBlocks   = iface.StorageBlocks |> List.map ShaderBlock.flipMatrixMajority
        }
    
    let toString (iface : ShaderInterface) =
        let input   = iface.Inputs |> List.map ShaderParameter.toString |> String.concat "\r\n" |> String.indent 1 |> sprintf "in {\r\n%s\r\n}"
        let output  = iface.Outputs |> List.map ShaderParameter.toString |> String.concat "\r\n" |> String.indent 1 |> sprintf "out {\r\n%s\r\n}"
        let uniform = iface.Uniforms |> List.map ShaderParameter.toString |> String.concat "\r\n" |> String.indent 1 |> sprintf "uniform {\r\n%s\r\n}"
        let ubs = iface.UniformBlocks |> List.map ShaderBlock.toString |> String.concat "\r\n" |> String.indent 1 |> sprintf "uniform {\r\n%s\r\n}"
        let sbs = iface.StorageBlocks |> List.map ShaderBlock.toString |> String.concat "\r\n" |> String.indent 1 |> sprintf "buffer {\r\n%s\r\n}"

        String.concat "\r\n" [input; output; uniform; ubs; sbs]


type IAdaptiveWriter =
    inherit IAdaptiveObject
    abstract member Write : token : AdaptiveToken * target : nativeint -> unit

[<AbstractClass; Sealed; Extension>]
type IAdptiveWriterExtensions private() =
    [<Extension>]
    static member Write(this : IAdaptiveWriter, target : nativeint) =
        this.Write(AdaptiveToken.Top, target)

module ShaderParameterWriter =
    open System.Reflection
    open System.Runtime.InteropServices
    open System.Collections.Concurrent
    open Aardvark.Base.Incremental


    [<AbstractClass>]
    type AdaptiveWriter() =
        inherit AdaptiveObject()

        abstract member PerformWrite : token : AdaptiveToken * target : nativeint -> unit

        member x.Write(token : AdaptiveToken, target : nativeint) =
            x.EvaluateAlways token (fun token ->
                x.PerformWrite(token, target)
            )

        interface IAdaptiveWriter with
            member x.Write(token, target) = x.Write(token, target)


    [<AbstractClass>]
    type Writer<'a>() =
        inherit Writer()
        abstract member Write : target : nativeint * value : 'a -> unit

        override this.Bind(m : IMod) =
            match m with
                | :? IMod<'a> as m ->
                    { new AdaptiveWriter() with
                        member x.PerformWrite (token : AdaptiveToken, target : nativeint) =
                            let value = m.GetValue token
                            this.Write(target, value)
                    } :> IAdaptiveWriter

                | _ ->
                    failwith "not possible"

    and Writer() =
        abstract member Bind : IMod -> IAdaptiveWriter
        default x.Bind m = failwith "not possible"
        

    [<AutoOpen>]
    module private Helpers = 
        let cache = ConcurrentDictionary<Type * ShaderParameterType, Writer>()

        let createWriter<'a when 'a :> Writer> (targs : Type[]) (args : array<obj>) =
            let tWriter = typeof<'a>.GetGenericTypeDefinition()
            let tWriter = tWriter.MakeGenericType targs
            let types = args |> Array.map (fun o -> o.GetType())
            let ctor = tWriter.GetConstructor(BindingFlags.Instance ||| BindingFlags.NonPublic ||| BindingFlags.Public, Type.DefaultBinder, types, null)
            ctor.Invoke(args) |> unbox<Writer>

        let rec (|ArrOf|_|) (t : Type) =
            if t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<Arr<_,_>> then
                let targs = t.GetGenericArguments()
                Some (targs.[0], targs.[1])
            else
                match t.BaseType with
                    | null -> None
                    | ArrOf(s,t) -> Some(s,t)
                    | _ -> None


        let (|ArrayOf|_|) (t : Type) =
            if t.IsArray then 
                Some (t.GetElementType())
            else
                None

        let (|SeqOf|_|) (t : Type) =
            let iface = t.GetInterface(typedefof<seq<_>>.Name)
            if isNull iface then 
                None
            else
                Some (iface.GetGenericArguments().[0])


        let rec (|All|_|) (l : list<Option<'a>>) =
            match l with
                | [] -> Some []
                | Some h :: All rest -> Some (h :: rest)
                | _ -> None

        type MemberInfo with
            member x.Type =
                match x with
                    | :? FieldInfo as f -> f.FieldType
                    | :? PropertyInfo as p -> p.PropertyType
                    | _ -> failwith "sadsadsadsad"

    [<AutoOpen>]
    module Writers =
        type NoConversionWriter<'a when 'a : unmanaged>() =
            inherit Writer<'a>()
            override x.Write(target : nativeint, value : 'a) =
                NativeInt.write target value

        type ConversionWriter<'a, 'b when 'b : unmanaged>(convert : 'a -> 'b) =
            inherit Writer<'a>()

            override x.Write(target : nativeint, value : 'a) =
                let b = convert value
                NativeInt.write target b

        type MapWriter<'a, 'b>(inner : Writer<'b>, convert : 'a -> 'b) =
            inherit Writer<'a>()

            override x.Write(target : nativeint, value : 'a) =
                let b = convert value
                inner.Write(target, b)

        type FixedArrayMemcpyWriter<'a when 'a : unmanaged>(length : int) =
            inherit Writer<'a[]>()
            static let sa = sizeof<'a>

            override x.Write(target : nativeint, value : 'a[]) =
                let mutable target = target
                let cnt = min length value.Length
                let gc = GCHandle.Alloc(value, GCHandleType.Pinned)
                try Marshal.Copy(gc.AddrOfPinnedObject(), target, cnt * sa)
                finally gc.Free()
           
        type FixedArrayWriter<'a>(inner : Writer<'a>, stride : int, length : int) =
            inherit Writer<'a[]>()

            let stride = nativeint stride

            override x.Write(target : nativeint, value : 'a[]) =
                let mutable target = target
                let cnt = min length value.Length
                for i in 0 .. cnt - 1 do
                    inner.Write(target, value.[i])
                    target <- target + stride
 
        type FixedArrMemcpyWriter<'d, 'a when 'a : unmanaged and 'd :> INatural>(length : int) =
            inherit Writer<Arr<'d, 'a>>()
            static let sa = sizeof<'a>
            let copySize = sa * min Peano.typeSize<'d> length

            override x.Write(target : nativeint, value : Arr<'d, 'a>) =
                let mutable target = target
                let gc = GCHandle.Alloc(value.Data, GCHandleType.Pinned)
                try Marshal.Copy(gc.AddrOfPinnedObject(), target, copySize)
                finally gc.Free()
           
        type FixedArrWriter<'d,'a when 'd :> INatural>(inner : Writer<'a>, stride : int, length : int) =
            inherit Writer<Arr<'d, 'a>>()

            let stride = nativeint stride
            let cnt = min Peano.typeSize<'d> length

            override x.Write(target : nativeint, value : Arr<'d, 'a>) =
                let mutable target = target
                for i in 0 .. cnt - 1 do
                    inner.Write(target, value.[i])
                    target <- target + stride
                 
        type FixedSeqWriter<'a>(inner : Writer<'a>, stride : int, length : int) =
            inherit Writer<seq<'a>>()

            let stride = nativeint stride

            override x.Write(target : nativeint, value : seq<'a>) =
                let mutable target = target
                let mutable index = 0
                use enum = value.GetEnumerator()
                while enum.MoveNext() && index < length do
                    inner.Write(target, enum.Current)
                    target <- target + stride
                    index <- index + 1

    module ReflectedWriter =
        open System.Threading
        open System.Reflection.Emit

        let dAss = AppDomain.CurrentDomain.DefineDynamicAssembly(AssemblyName "Aardvark.Rendering.GL.Writers", AssemblyBuilderAccess.RunAndSave)
        let dMod = dAss.DefineDynamicModule("MainModule")

        let mutable currentId = 0
        let name() =
            let id = Interlocked.Increment(&currentId)
            sprintf "_anonymous%d" id


        let defineMethod (t : Type) (fields : list<nativeint * MemberInfo>) =
            let writerTypes = fields |> List.map (fun (offset,f) -> typedefof<Writer<_>>.MakeGenericType [| f.Type |])

            let argumentTypes = List.concat [ [typeof<nativeint>; t]; writerTypes] |> List.toArray
            let meth =
                DynamicMethod(
                    name(), 
                    MethodAttributes.Public ||| MethodAttributes.Static, 
                    CallingConventions.Standard, 
                    typeof<Void>, 
                    argumentTypes, 
                    t, 
                    true
                )

            let il = meth.GetILGenerator()

            let mutable argIndex = 2
            for (wt, (offset, f)) in List.zip writerTypes fields do
                // load the writer
                match argIndex with
                    | 2 -> il.Emit(OpCodes.Ldarg_2)
                    | 3 -> il.Emit(OpCodes.Ldarg_3)
                    | i -> il.Emit(OpCodes.Ldarg_S, int16 i)


                // load the pointer
                il.Emit(OpCodes.Ldarg_0)
                if sizeof<nativeint> = 4 then il.Emit(OpCodes.Ldc_I4, int offset)
                else il.Emit(OpCodes.Ldc_I8, int64 offset)
                il.Emit(OpCodes.Conv_I)
                il.Emit(OpCodes.Add)


                // load the field
                il.Emit(OpCodes.Ldarg_1)
                match f with
                    | :? FieldInfo as fi -> 
                        il.Emit(OpCodes.Ldfld, fi)

                    | :? PropertyInfo as pi when pi.GetIndexParameters().Length = 0 ->
                        let get = pi.GetMethod
                        if get.IsVirtual then il.EmitCall(OpCodes.Callvirt, get, null)
                        else il.EmitCall(OpCodes.Call, get, null)
                        
                    | _ ->
                        failwith ""


                let writeMeth = wt.GetMethod("Write", BindingFlags.Instance ||| BindingFlags.NonPublic ||| BindingFlags.Public)
                il.EmitCall(OpCodes.Callvirt, writeMeth, null)

                argIndex <- argIndex + 1

            il.Emit(OpCodes.Ret)


            let dType = System.Linq.Expressions.Expression.GetDelegateType(Array.append argumentTypes [|typeof<Void>|])
            meth.CreateDelegate(dType)


        let defineWriter (t : Type) (fields : list<nativeint * MemberInfo>) : list<Writer> -> Writer =
            let realWrite = defineMethod t fields
            let realWriteType = realWrite.GetType()
            let realWriteInvoke = realWriteType.GetMethod("Invoke")

            let baseType = typedefof<Writer<_>>.MakeGenericType [| t |]
            let dType = dMod.DefineType(name(), TypeAttributes.Class, baseType)

            let delegateField = dType.DefineField("_invoke", realWriteType, FieldAttributes.Private)

            let writes =
                fields |> List.mapi (fun i (offset, fi) ->
                    let tWriter = typedefof<Writer<_>>.MakeGenericType [| fi.Type |]
                    let writerField = dType.DefineField(sprintf "writer%d" i, tWriter, FieldAttributes.Private)
                    writerField
                )   

            let write = dType.DefineMethod("Write", MethodAttributes.Virtual ||| MethodAttributes.Public, typeof<Void>, [| typeof<nativeint>; t |])
            let il = write.GetILGenerator()
            
            // load the delegate
            il.Emit(OpCodes.Ldarg_0)
            il.Emit(OpCodes.Ldfld, delegateField)

            // load the pointer
            il.Emit(OpCodes.Ldarg_1)

            // load the argument
            il.Emit(OpCodes.Ldarg_2)

            // load all writers
            for f in writes do
                il.Emit(OpCodes.Ldarg_0)
                il.Emit(OpCodes.Ldfld, f)

            il.EmitCall(OpCodes.Callvirt, realWriteInvoke, null)
            il.Emit(OpCodes.Ret)

            dType.DefineMethodOverride(write, baseType.GetMethod "Write")

            let writes = List.toArray writes
            let argTypes = Array.concat [ [|realWriteType|]; writes |> Array.map (fun f -> f.FieldType) ]
            let ctor = dType.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, argTypes)
            let il = ctor.GetILGenerator()

            il.Emit(OpCodes.Ldarg_0)
            il.Emit(OpCodes.Call, baseType.GetConstructor [||])

            
            il.Emit(OpCodes.Ldarg_0)
            il.Emit(OpCodes.Ldarg_1)
            il.Emit(OpCodes.Stfld, delegateField)

            let mutable argIndex = 2
            for f in writes do
                
                il.Emit(OpCodes.Ldarg_0)

                match argIndex with
                    | 2 -> il.Emit(OpCodes.Ldarg_2)
                    | 3 -> il.Emit(OpCodes.Ldarg_3)
                    | i -> il.Emit(OpCodes.Ldarg_S, int16 i)
                
                il.Emit(OpCodes.Stfld, f)

                argIndex <- argIndex + 1


            il.Emit(OpCodes.Ret)


            let writerType = dType.CreateType()

            let ctor = writerType.GetConstructor argTypes

            fun writers ->
                let writers = writers |> List.map (fun w -> w :> obj)
                let args = (realWrite :> obj) :: writers
                ctor.Invoke(List.toArray args) |> unbox<Writer>


    let private vecFieldNames = [ "X"; "Y"; "Z"; "W" ]
    let private matFieldNames = Array2D.init 4 4 (sprintf "M%d%d")

    let rec get (input : Type) (target : ShaderParameterType) : Writer =
        cache.GetOrAdd((input, target), fun (input, target) ->
            match ShaderParameterType.tryGetExpectedType target with
                | Some b ->
                    if input = b then 
                        createWriter<NoConversionWriter<int>> [|input|] [||]
                    else
                        let converter = PrimitiveValueConverter.getConverter input b
                        createWriter<ConversionWriter<int, int>> [|input; b|] [|converter|]

                | None ->
                    match target with
                        | Bool | Float | Double | Int | UnsignedInt ->
                            failwithf "[Writer] missing conversion from %A to %A" input target

                        // transposed matrices
                        | Matrix(t, rows, cols, false) ->
                            let inner = get input (Matrix(t, rows, cols, true))
                            let transpose = PrimitiveValueConverter.getTransposeConverter input
                            createWriter<MapWriter<int, int>> [|input; input|] [|inner; transpose|]

                        // arrays
                        | FixedArray(b, stride, _) | DynamicArray(b, stride) ->
                            let length =
                                match target with
                                    | FixedArray(_,_,l) -> l
                                    | _ -> Int32.MaxValue

                            match input with
                                | ArrayOf a ->
                                    match ShaderParameterType.tryGetExpectedType b with
                                        | Some b when b = a && stride = Marshal.SizeOf a ->
                                            createWriter<FixedArrayMemcpyWriter<int>> [| a |] [| length |]
                                        | _ -> 
                                            let inner = get a b
                                            createWriter<FixedArrayWriter<_>> [| a |] [|inner; stride; length |]

                                | ArrOf(la, a) ->
                                    match ShaderParameterType.tryGetExpectedType b with
                                        | Some b when b = a && stride = Marshal.SizeOf a ->
                                            createWriter<FixedArrMemcpyWriter<Z, int>> [| la; a |] [| length |]
                                        | _ ->
                                            let inner = get a b
                                            createWriter<FixedArrWriter<_, _>> [| la; a |] [|inner; stride; length |]
                            
                                | SeqOf a ->
                                    let inner = get a b
                                    createWriter<FixedSeqWriter<_>> [| a |] [|inner; stride; length |]
                            
                                | _ -> 
                                    // write only the first element
                                    get input b

                        // struct types (all fields need to be present)
                        | Struct(size, fields) ->
                            let inputFields = 
                                fields |> List.map (fun f ->
                                    let fi = input.GetField(f.Name, BindingFlags.NonPublic ||| BindingFlags.Public ||| BindingFlags.Instance)
                                    if isNull fi then
                                        let pi = input.GetProperty(f.Name, BindingFlags.NonPublic ||| BindingFlags.Public ||| BindingFlags.Instance)
                                        if isNull pi then None
                                        else Some (pi :> MemberInfo)
                                    else
                                        Some (fi :> MemberInfo)
                                )

                            match inputFields with
                                | All inputFields ->
                                    let zip = List.zip fields inputFields
                                    let res = zip |> List.map (fun (f,fi) -> nativeint f.Offset, fi)
                                    let creator = ReflectedWriter.defineWriter input res
                                    let writers = zip |> List.map (fun (f, fi) -> get fi.Type f.Type) 
                                    creator writers
                                    
                                | _ ->
                                    let unresolved =
                                        List.zip fields inputFields
                                            |> List.choose (fun (f, fi) ->
                                                match fi with
                                                    | None -> Some f.Name
                                                    | _ -> None
                                            )  

                                    failwithf "[Writer] cannot convert from %A to %A (missing fields %A)" input target unresolved

                        // unsupported vector types (bvec*, uvec*, etc.)
                        | Vector(et, dim) ->
                            let se = ShaderParameterType.sizeof et
                            let fields =
                                vecFieldNames 
                                    |> List.take dim 
                                    |> List.mapi (fun i n ->
                                        {
                                            Name = n
                                            Type = et
                                            Offset = i * se
                                        }
                                    )

                            get input (Struct(se * dim, fields))

                        // unsupported matrix types (mat2x4, etc.)
                        | Matrix(et, rows, cols, isRow) ->
                            let se = ShaderParameterType.sizeof et
                            let fields =
                                List.init (rows * cols) (fun i ->
                                    let r = i / rows
                                    let c = i % rows

                                    let r,c =
                                        if isRow then r,c
                                        else c,r
                                        
                                    {
                                        Name = sprintf "M%d%d" r c
                                        Type = et
                                        Offset = i * se
                                    }
                                )   

                            get input (Struct(se * rows * cols, fields))

                        // atmoc counters are nop??
                        | AtomicCounter t ->
                            get input t

                        | Sampler _ | Image _ ->
                            failwithf "[Writer] field of type %A cannot exist in buffers" target

        )

    let writer<'a> (target : ShaderParameterType) : Writer<'a> =
        get typeof<'a> target |> unbox<Writer<'a>>

    let adaptive (input : IMod) (target : ShaderParameterType) : IAdaptiveWriter =
        let contentType = input.GetType().GetInterface(typedefof<IMod<_>>.Name).GetGenericArguments().[0]
        let writer = get contentType target
        writer.Bind input

module ShaderBlockWriter =
    open Aardvark.Base.Incremental
    
    let writers (resolve : string -> Option<IMod>) (block : ShaderBlock) =
        block.Fields |> List.map (fun (f : ShaderBlockField) ->
            match f.Path with
                | ShaderPath.Value name -> 
                    match resolve name with
                        | Some m ->
                            let contentType = m.GetType().GetInterface(typedefof<IMod<_>>.Name).GetGenericArguments().[0]
                            let writer = ShaderParameterWriter.get contentType f.Type
                            writer.Bind m
                        | None ->
                            failwith ""
                | _ ->
                    failwith ""
        )
