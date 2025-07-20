namespace Aardvark.Rendering

open System
open System.Reflection
open System.Reflection.Emit
open System.Runtime.InteropServices
open Microsoft.FSharp.Reflection
open Microsoft.FSharp.NativeInterop
open Aardvark.Base
open FSharp.Data.Adaptive
open FShade.GLSL

#nowarn "9"

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module GLSLType =

    module Interop =

        /// Type representing 2x4 matrices
        /// Only used for padding rows of 2x2 and 2x3 matrices
        [<Struct; StructLayout(LayoutKind.Sequential)>]
        type M24f =
            val M00 : float32
            val M01 : float32
            val M02 : float32
            val M03 : float32
            val M10 : float32
            val M11 : float32
            val M12 : float32
            val M13 : float32

            static do PrimitiveValueConverter.addConverters [
                // padding for 2 row matrices
                ( fun (i : M22f) -> M24f &i ) :> obj
                ( fun (i : M22d) -> M24f &i ) :> obj
                ( fun (i : M23f) -> M24f &i ) :> obj
                ( fun (i : M23d) -> M24f &i ) :> obj
            ]

            new (m : inref<M22f>) =
                { M00 = m.M00; M01 = m.M01; M02 = 0.0f; M03 = 0.0f
                  M10 = m.M10; M11 = m.M11; M12 = 0.0f; M13 = 0.0f }

            new (m : inref<M22d>) =
                { M00 = float32 m.M00; M01 = float32 m.M01; M02 = 0.0f; M03 = 0.0f
                  M10 = float32 m.M10; M11 = float32 m.M11; M12 = 0.0f; M13 = 0.0f }

            new (m : inref<M23f>) =
                { M00 = m.M00; M01 = m.M01; M02 = m.M02; M03 = 0.0f
                  M10 = m.M10; M11 = m.M11; M12 = m.M12; M13 = 0.0f }

            new (m : inref<M23d>) =
                { M00 = float32 m.M00; M01 = float32 m.M01; M02 = float32 m.M02; M03 = 0.0f
                  M10 = float32 m.M10; M11 = float32 m.M11; M12 = float32 m.M12; M13 = 0.0f }

        module Patterns =
            open TypeMeta

            /// MatrixOf pattern also considering interop types.
            [<return: Struct>]
            let (|MatrixOf|_|) (t : Type) =
                match t with
                | MatrixOf r -> ValueSome r
                | _ ->
                    if t = typeof<M24f> then ValueSome (V2i(4, 2), typeof<float32>)
                    else ValueNone

    let toType =
        LookupTable.lookup [
            Bool, typeof<int>
            Void, typeof<unit>

            Int(true, 8), typeof<sbyte>
            Int(true, 16), typeof<int16>
            Int(true, 32), typeof<int32>
            Int(true, 64), typeof<int64>

            Int(false, 8), typeof<byte>
            Int(false, 16), typeof<uint16>
            Int(false, 32), typeof<uint32>
            Int(false, 64), typeof<uint64>

            Float(16), typeof<float16>
            Float(32), typeof<float32>
            Float(64), typeof<float32>

            Vec(2, Int(true, 32)), typeof<V2i>
            Vec(3, Int(true, 32)), typeof<V3i>
            Vec(4, Int(true, 32)), typeof<V4i>

            Vec(2, Int(false, 32)), typeof<V2ui>
            Vec(3, Int(false, 32)), typeof<V3ui>
            Vec(4, Int(false, 32)), typeof<V4ui>

            Vec(2, Float(32)), typeof<V2f>
            Vec(3, Float(32)), typeof<V3f>
            Vec(4, Float(32)), typeof<V4f>

            Vec(2, Float(64)), typeof<V2f>
            Vec(3, Float(64)), typeof<V3f>
            Vec(4, Float(64)), typeof<V4f>

            Mat(2,2,Int(true,32)), typeof<M22i>
            Mat(2,3,Int(true,32)), typeof<M23i>
            Mat(3,3,Int(true,32)), typeof<M34i>
            Mat(3,4,Int(true,32)), typeof<M34i>
            Mat(4,4,Int(true,32)), typeof<M44i>

            Mat(2,2,Float(32)), typeof<Interop.M24f> // Matrix rows need to be padded to 4 elements according to std140
            Mat(2,3,Float(32)), typeof<Interop.M24f>
            Mat(3,3,Float(32)), typeof<M34f>
            Mat(3,4,Float(32)), typeof<M34f>
            Mat(4,4,Float(32)), typeof<M44f>

            Mat(2,2,Float(64)), typeof<Interop.M24f>
            Mat(2,3,Float(64)), typeof<Interop.M24f>
            Mat(3,3,Float(64)), typeof<M34f>
            Mat(3,4,Float(64)), typeof<M34f>
            Mat(4,4,Float(64)), typeof<M44f>
        ]

    let rec sizeof (t : GLSLType) =
        match t with
        | Bool -> 4
        | Int(_,b) -> b / 8
        | Float(w) -> w / 8
        | Vec(d,e) -> d * sizeof e
        | Mat(r,c,e) -> r * c * sizeof e
        | Array(len, e, stride) -> len * stride
        | Struct(_,f,size) -> size
        | Void -> failwith "[UniformWriter] void does not have a size"
        | Image _ -> failwith "[UniformWriter] image does not have a size"
        | Sampler _ -> failwith "[UniformWriter] sampler does not have a size"
        | DynamicArray _ -> failwith "[UniformWriter] dynamic arrays do not have a size"
        | Intrinsic _ -> failwith "[UniformWriter] dynamic arrays do not have a size"
        | SamplerState -> failwith "[UniformWriter] SamplerStates do not have a size"
        | Texture _ -> failwith "[UniformWriter] Textures do not have a size"



module UniformWriters =
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
                let value = unbox<aval<'a>> value
                x.Write(value.GetValue caller, ptr)

            member x.WriteUnsafeValue(value, ptr) =
                match value with
                    | :? 'a as value -> x.Write(value, ptr)
                    | _ -> failwithf "[UniformWriter] unexpected value %A (expecting %A)" value typeof<'a>

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
            
        type CallWriter<'a, 'b>(mi : MethodInfo, inner : IWriter<'b>) =
            inherit MapWriter<'a, 'b>((mi.CreateDelegate(typeof<Func<'a, 'b>>) |> unbox<Func<'a, 'b>>).Invoke, inner)

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

        type ReplicateWriter<'a>(targetCount : int, stride : nativeint, inner : IWriter<'a>) =
            inherit AbstractWriter<'a>()
            
            let targetSize = nativeint (targetCount - 1) * stride + inner.TargetSize

            override x.TargetSize = targetSize
                
            override x.Write(value : 'a, ptr : nativeint) =
                let mutable offset = 0n

                let mutable cnt = 0
                while cnt < targetCount do
                    inner.WriteValue(value, ptr + offset)
                    offset <- offset + stride
                    cnt <- cnt + 1

                let remaining = targetSize - offset
                if remaining > 0n then
                    Marshal.Set(ptr + offset, 0, remaining)

        type SubTypeTestWriter<'a, 'b when 'a : not struct>(inner : IWriter<'b>) =
            inherit AbstractWriter<'a>()
            
            override x.TargetSize =
                inner.TargetSize

            override x.Write(value : 'a, ptr : nativeint) =
                match value :> obj with
                    | null -> Marshal.Set(ptr, 0, inner.TargetSize)
                    | :? 'b as b -> inner.WriteValue(b, ptr)
                    | _ -> Marshal.Set(ptr, 0, inner.TargetSize)

        type ZeroWhenNullWriter<'a when 'a : not struct>(inner : IWriter<'a>) =
            inherit AbstractWriter<'a>()
            
            override x.TargetSize =
                inner.TargetSize

            override x.Write(value : 'a, ptr : nativeint) =
                match value :> obj with
                    | null -> Marshal.Set(ptr, 0, inner.TargetSize)
                    | _ -> inner.WriteValue(value, ptr)

        type PrimitiveArrayWriter<'a when 'a : unmanaged>(count : int) =
            inherit AbstractWriter<'a[]>()
            
            let targetSize = nativeint count * nativeint sizeof<'a>

            override x.TargetSize = targetSize
                
            override x.Write(value : 'a[], ptr : nativeint) =
                let inputSize = nativeint value.Length * nativeint sizeof<'a>
                let copySize = min targetSize inputSize

                value |> NativePtr.pinArr (fun pSrc ->
                    Marshal.Copy(pSrc.Address, ptr, copySize)
                    if targetSize > copySize then Marshal.Set(ptr + copySize, 0, targetSize - copySize)
                )

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
               
        type PrimitiveCallWriter<'a, 'b when 'b : unmanaged>(mi : MethodInfo) =
            inherit PrimitiveMapWriter<'a, 'b>((mi.CreateDelegate(typeof<Func<'a, 'b>>) |> unbox<Func<'a, 'b>>).Invoke)


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
                
        let private newCallWriter (mi : MethodInfo) (inner : IWriter) =
            let args = mi.GetParameters()
            if mi.ReturnType.IsBlittable && inner.IsPrimitive then
                let tWriter = typedefof<PrimitiveCallWriter<int, int>>.MakeGenericType [| args.[0].ParameterType; mi.ReturnType |]
                let ctor = tWriter.GetConstructor [| typeof<MethodInfo> |]
                ctor.Invoke [| mi :> obj |] |> unbox<IWriter>
            else
                let tWriter = typedefof<CallWriter<_,_>>.MakeGenericType [| args.[0].ParameterType; mi.ReturnType |]
                let ctor = tWriter.GetConstructor [| typeof<MethodInfo>; inner.GetType() |]
                ctor.Invoke [| mi :> obj; inner :> obj |] |> unbox<IWriter>
                
        let private newMemberWriter (mi : MemberInfo) (inner : IWriter) =
            match mi with
                | :? FieldInfo as fi -> newFieldWriter fi inner
                | :? PropertyInfo as pi -> newPropertyWriter pi inner
                | :? MethodInfo as mi -> newCallWriter mi inner
                | _ -> failwith "sadasdasdd"

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
            
        let private newZeroWhenNullWriter (inner : IWriter) =
            let t = typedefof<ZeroWhenNullWriter<_>>.MakeGenericType [| inner.ValueType |]
            let ctor = t.GetConstructor [| inner.GetType() |]
            ctor.Invoke [|inner :> obj |] |> unbox<IWriter>

        let private newPrimitiveArrayWriter (tElem : Type) (cnt : int) =
            let t = typedefof<PrimitiveArrayWriter<int>>.MakeGenericType [| tElem |]
            let ctor = t.GetConstructor [| typeof<int> |]
            ctor.Invoke [| cnt :> obj |] |> unbox<IWriter>


        let private newReplicateWriter (inner : IWriter) (stride : nativeint) (cnt : int) =
            let t = typedefof<ReplicateWriter<_>>.MakeGenericType [| inner.ValueType |]
            let ctor = t.GetConstructor [| typeof<int>; typeof<nativeint>; (typedefof<IWriter<_>>.MakeGenericType [| inner.ValueType |]) |]
            ctor.Invoke [| cnt :> obj; stride :> obj; inner :> obj |] |> unbox<IWriter>


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

        type MemberInfo with
            member x.Type =
                match x with
                    | :? FieldInfo as fi -> fi.FieldType
                    | :? PropertyInfo as pi -> pi.PropertyType
                    | :? MethodInfo as mi -> mi.ReturnType
                    | _ -> failwith "[UnformWriter] invalid member info"

        let rec tryCreateWriterInternal (target : FShade.GLSL.GLSLType) (tSource : Type) =
            match target with

                | FShade.GLSL.Struct(name, fields, size) ->
                    let fieldWriters =
                        if FSharpType.IsUnion(tSource, true) then
                            let cases = FSharpType.GetUnionCases(tSource, true)
                            let table = 
                                cases |> Seq.collect (fun ci ->
                                    ci.GetFields() |> Seq.map (fun pi ->
                                        ci.Name + "_" + pi.Name, pi :> MemberInfo
                                    )
                                )
                                |> Map.ofSeq
                                |> Map.add "tag" (FSharpValue.PreComputeUnionTagMemberInfo(tSource, true))
                                    

                            fields |> List.mapOption (fun (name, typ, offset) ->
                                match Map.tryFind name table with
                                    | Some pi ->
                                        match tryCreateWriterInternal typ pi.Type with
                                            | Some inner ->
                                                let w = newMemberWriter pi inner
                                                let w = if name <> "tag" then newSubTypeTestWriter tSource pi.DeclaringType w else w
                                                Some (nativeint offset, w)
                                            | None ->
                                                None
                                    | None ->
                                        None

                            )
                        else
                            fields |> List.mapOption (fun (name, typ, offset) ->
                                let fi = tSource.GetField(name, BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.Instance)

                                if isNull fi then
                                    let pi = tSource.GetProperty(name, BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.Instance)
                                    if isNull pi then
                                        None
                                    else
                                        match tryCreateWriterInternal typ pi.PropertyType with
                                            | Some inner -> 
                                                Some (nativeint offset, newPropertyWriter pi inner)
                                            | None ->
                                                None
                            
                                else
                                    match tryCreateWriterInternal typ fi.FieldType with
                                        | Some inner -> 
                                            Some (nativeint offset, newFieldWriter fi inner)
                                        | None ->
                                            None
                            )
                    match fieldWriters with
                        | Some fieldWriters ->
                            Some (newStructWriter tSource (nativeint size) fieldWriters)
                        | None ->
                            None

                | FShade.GLSL.Array(len, itemType, stride) ->
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

                        | t ->
                            match tryCreateWriterInternal itemType tSource with
                                | Some elemWriter ->
                                    newReplicateWriter elemWriter stride len |> Some
                                | None ->
                                    None

                | t -> 
                    let tTarget = GLSLType.toType t

                    let prim = newPrimitiveWriter tTarget

                    if tTarget = tSource then
                        Some prim
                    else
                        let converter = PrimitiveValueConverter.getConverter tSource tTarget
                        newMapWriter tSource tTarget converter prim |> Some

    let cache = System.Collections.Concurrent.ConcurrentDictionary<FShade.GLSL.GLSLType * Type, Option<IWriter>>()

    let tryGetWriter (offset : int) (tTarget : FShade.GLSL.GLSLType) (tSource : Type) =
        let key = (tTarget, tSource)
        let writer = cache.GetOrAdd(key, fun (tTarget, tSource) -> NewWriters.tryCreateWriterInternal tTarget tSource)

        match writer with
            | Some w -> 
                w.WithOffset (nativeint offset) |> Some
            | None ->
                None
    
    let getWriter (offset : int) (tTarget : FShade.GLSL.GLSLType) (tSource : Type) =
        match tryGetWriter offset tTarget tSource with
            | Some w -> w
            | None -> failwithf "[UniformWriter] could not create UniformWriter for field %A (input-type: %A)" tTarget tSource