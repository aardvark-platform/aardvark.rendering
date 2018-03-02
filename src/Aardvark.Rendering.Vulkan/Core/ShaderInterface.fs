namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open FShade.SpirV
open Aardvark.Base.Monads.Option


type Width = int

[<StructuralComparison; StructuralEquality>]
type ShaderType =
    | Void
    | Bool
    | Sampler
    | Function of args : list<ShaderType> * retType : ShaderType
    | Int of width : int * signed : bool
    | Float of width : int
    | Vector of compType : ShaderType * dim : int
    | Matrix of colType : ShaderType * dim : int
    | Array of elementType : ShaderType * length : int
    | Struct of name : string * fields : list<ShaderType * string * list<Decoration * int[]>>
    | Image of sampledType : ShaderType * dim : Dim * depth : int * arrayed : bool * ms : int * sampled : bool * format : ImageFormat
    | SampledImage of ShaderType
    | Ptr of StorageClass * ShaderType
    | RuntimeArray of ShaderType

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ShaderType =
    open System

    let glslDecoration (decorations : list<Decoration * int[]>) =
        if List.isEmpty decorations then
            ""
        else
            let decorations =
                decorations |> List.map (fun t ->
                    match t with
                        | Decoration.Location, [|loc|] -> sprintf "location = %d" (int loc)
                        | Decoration.Binding, [|b|] -> sprintf "binding = %d" (int b)
                        | Decoration.DescriptorSet, [|s|] -> sprintf "set = %d" (int s)
                        | Decoration.Index, [|i|] -> sprintf "index = %d" (int i)
                        | Decoration.BuiltIn, [|id|] -> sprintf "builtin = %A" (unbox<BuiltIn> id)

                        | Decoration.NonReadable, _ -> "write_only"
                        | Decoration.NonWritable, _ -> "read_only"
                        | Decoration.RowMajor, _ -> "row_major"
                        | Decoration.ColMajor, _ -> "col_major"
                        | Decoration.NoPerspective, _ -> "noperspective"
                        | Decoration.Flat, _ -> "flat"

                        | d, [||] -> sprintf "%A" d
                        | d, args -> sprintf "%A = { %s }" d (args |> Array.map string |> String.concat ", ")
                )
            decorations |> String.concat ", " |> sprintf "layout(%s)"

    let rec glslName (t : ShaderType) =
        match t with
            | Ptr(_,t) -> glslName t
            | Array(t, l) -> sprintf "%s[%d]" (glslName t) l

            | Bool -> "bool"
            | Int(32, true) -> "int"
            | Int(32, false) -> "uint"
            | Float 32 -> "float"
            | Float 64 -> "double"

            | Vector(Bool, d) -> sprintf "bvec%d" d
            | Vector(Int(32, true), d) -> sprintf "ivec%d" d
            | Vector(Int(32, false), d) -> sprintf "uvec%d" d
            | Vector(Float 32, d) -> sprintf "vec%d" d
            | Vector(Float 64, d) -> sprintf "dvec%d" d

            | Matrix(Vector(Float 32, r), c) -> 
                if r = c then sprintf "mat%d" r
                else sprintf "mat%dx%d" r c

            | Matrix(Vector(Float 64, r), c) -> 
                if r = c then sprintf "dmat%d" r
                else sprintf "dmat%dx%d" r c

            | Image(st,dim,d,a,m, x, y) -> 
                let st =
                    match st with
                        | Float 32 -> ""
                        | Int(32, true) -> "i"
                        | Int(32, false) -> "u"
                        | _ -> "g"

                let dim =
                    match dim with
                        | Dim.Dim1D -> "1D"
                        | Dim.Dim2D -> "2D"
                        | Dim.Dim3D -> "3D"
                        | Dim.Buffer -> "Buffer"
                        | Dim.Cube -> "Cube"
                        | Dim.Rect -> "Rect"
                        | _ -> failwithf "unsupported sampler dimension: %A" dim
                let d = if d > 0 then "Shadow" else ""
                let a = if a then "Array" else ""
                let m = if m > 1 then "MS" else ""
                st + "sampler" + dim + m + a + d



            | Struct(name, fields) ->
                
                let strs =
                    fields |> List.map (fun (t, n, dec) ->
                        let t = glslName t
                        let layout = glslDecoration dec

                        if layout.Length = 0 then
                            sprintf "    %s %s;" t n
                        else
                            sprintf "    %s %s %s;" layout t n
                    )

                strs |> String.concat "\r\n" |> sprintf "%s {\r\n%s\r\n }" name

            | _ -> sprintf "unknown type: %A" t

    let Bool = ShaderType.Bool
    let UnsignedInt = ShaderType.Int(32, false)
    let Double = ShaderType.Float(64)
    let Half = ShaderType.Float(16)
    let Int32 = ShaderType.Int(32, true)
    let Float32 =ShaderType. Float(32)

    let IntVec2 = ShaderType.Vector(Int32, 2)
    let IntVec3 = ShaderType.Vector(Int32, 3)
    let IntVec4 = ShaderType.Vector(Int32, 4)

    let UnsignedIntVec2 = ShaderType.Vector(UnsignedInt, 2)
    let UnsignedIntVec3 = ShaderType.Vector(UnsignedInt, 3)
    let UnsignedIntVec4 = ShaderType.Vector(UnsignedInt, 4)

    let BoolVec2 = ShaderType.Vector(Bool, 2)
    let BoolVec3 = ShaderType.Vector(Bool, 3)
    let BoolVec4 = ShaderType.Vector(Bool, 4)

    let FloatVec2 = ShaderType.Vector(Float32, 2)
    let FloatVec3 = ShaderType.Vector(Float32, 3)
    let FloatVec4 = ShaderType.Vector(Float32, 4)
    let FloatMat2 = ShaderType.Matrix(FloatVec2, 2)
    let FloatMat3 = ShaderType.Matrix(FloatVec3, 3)
    let FloatMat4 = ShaderType.Matrix(FloatVec4, 4)
    let FloatMat2x2 = ShaderType.Matrix(FloatVec2, 2)
    let FloatMat3x2 = ShaderType.Matrix(FloatVec3, 2)
    let FloatMat4x2 = ShaderType.Matrix(FloatVec4, 2)
    let FloatMat2x3 = ShaderType.Matrix(FloatVec2, 3)
    let FloatMat3x3 = ShaderType.Matrix(FloatVec3, 3)
    let FloatMat4x3 = ShaderType.Matrix(FloatVec4, 3)
    let FloatMat2x4 = ShaderType.Matrix(FloatVec2, 4)
    let FloatMat3x4 = ShaderType.Matrix(FloatVec3, 4)
    let FloatMat4x4 = ShaderType.Matrix(FloatVec4, 4)


    let DoubleVec2 = ShaderType.Vector(Double, 2)
    let DoubleVec3 = ShaderType.Vector(Double, 3)
    let DoubleVec4 = ShaderType.Vector(Double, 4)
    let DoubleMat2 = ShaderType.Matrix(DoubleVec2, 2)
    let DoubleMat3 = ShaderType.Matrix(DoubleVec3, 3)
    let DoubleMat4 = ShaderType.Matrix(DoubleVec4, 4)
    let DoubleMat2x2 = ShaderType.Matrix(DoubleVec2, 2)
    let DoubleMat3x2 = ShaderType.Matrix(DoubleVec3, 2)
    let DoubleMat4x2 = ShaderType.Matrix(DoubleVec4, 2)
    let DoubleMat2x3 = ShaderType.Matrix(DoubleVec2, 3)
    let DoubleMat3x3 = ShaderType.Matrix(DoubleVec3, 3)
    let DoubleMat4x3 = ShaderType.Matrix(DoubleVec4, 3)
    let DoubleMat2x4 = ShaderType.Matrix(DoubleVec2, 4)
    let DoubleMat3x4 = ShaderType.Matrix(DoubleVec3, 4)
    let DoubleMat4x4 = ShaderType.Matrix(DoubleVec4, 4)


    let toPrimtiveType =
        LookupTable.lookupTable [
            Bool,               typeof<bool>
            
            Int32,              typeof<int32>
            UnsignedInt,        typeof<uint32>
            //Half,               typeof<float16>
            Float32,            typeof<float32>
            Double,             typeof<float>
            IntVec2,            typeof<V2i>
            IntVec3,            typeof<V3i>
            IntVec4,            typeof<V4i>
            FloatVec2,          typeof<V2f>
            FloatVec3,          typeof<V3f>
            FloatVec4,          typeof<V4f>
            DoubleVec2,         typeof<V2d>
            DoubleVec3,         typeof<V3d>
            DoubleVec4,         typeof<V4d>
            
            FloatMat2,          typeof<M22f>
            FloatMat3,          typeof<M33f>
            FloatMat3x4,        typeof<M34f>
            FloatMat4,          typeof<M44f>
            DoubleMat2,         typeof<M22d>
            DoubleMat3,         typeof<M33d>
            DoubleMat3x4,       typeof<M34d>
            DoubleMat4,         typeof<M44d>


        ]

    let rec toType (t : ShaderType) =
        match t with
            | ShaderType.Ptr(_,t) -> toType t
            | _ -> toPrimtiveType t


type ShaderParameter = 
    { 
        paramName : string
        paramType : ShaderType
        paramDecorations : list<Decoration * int[]> 
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ShaderParameter =
    let tryGetLocation (p : ShaderParameter) =
        p.paramDecorations |> List.tryPick (fun (d,args) ->
            match d with
                | Decoration.Location -> Some (args.[0] |> int)
                | _ -> None
        ) 

    let tryGetBinding (p : ShaderParameter) =
        p.paramDecorations |> List.tryPick (fun (d,args) ->
            match d with
                | Decoration.Binding -> Some (args.[0] |> int)
                | _ -> None
        ) 

    let tryGetDescriptorSet (p : ShaderParameter) =
        p.paramDecorations |> List.tryPick (fun (d,args) ->
            match d with
                | Decoration.DescriptorSet -> Some (args.[0] |> int)
                | _ -> None
        ) 

    let tryGetBuiltInSemantic (p : ShaderParameter) =
        p.paramDecorations |> List.tryPick (fun (d,args) ->
            match d with
                | Decoration.BuiltIn -> Some (args.[0] |> int |> unbox<BuiltIn>)
                | _ -> None
        )

    let isRowMajor (p : ShaderParameter) =
        p.paramDecorations |> List.exists (fun (d,args) ->
            match d with
                | Decoration.RowMajor -> true
                | _ -> false
        )

    let getArraySize (p : ShaderParameter) =
        1

    let inline paramName (p : ShaderParameter) = p.paramName
    let inline paramType (p : ShaderParameter) = p.paramType
    let inline paramDecorations (p : ShaderParameter) = p.paramDecorations




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
    
    let rec ofShaderType (t : ShaderType) =
        match t with
            | ShaderType.Bool -> PrimitiveType.Bool
            | ShaderType.Float b -> PrimitiveType.Float b
            | ShaderType.Int(w,s) -> PrimitiveType.Int(w,s)
            | ShaderType.Vector(comp, dim) -> PrimitiveType.Vector(ofShaderType comp, dim)
            | ShaderType.Matrix(comp, dim) -> PrimitiveType.Matrix(ofShaderType comp, dim)
            | _ -> failf "cannot convert type %A to PrimitiveType" t 

    let rec sizeof (t : PrimitiveType) =
        match t with
            | PrimitiveType.Bool -> 4
            | PrimitiveType.Float w -> w / 8
            | PrimitiveType.Int(w,_) -> w / 8
            | PrimitiveType.Vector(t, d) -> sizeof t * d
            | PrimitiveType.Matrix(v, d) -> sizeof v * d

type Size =
    | Fixed of int
    | Dynamic

type UniformType =
    | Struct of UniformBufferLayout
    | Primitive of t : PrimitiveType * size : int * align : int
    | Array of elementType : UniformType * length : int * size : int * align : int
    | RuntimeArray of elementType : UniformType * elementSize : int * elementAlign : int

    member x.align =
        match x with
            | Struct l -> l.align
            | Primitive(_,s,a) -> a
            | Array(_,_,s,a) -> a
            | RuntimeArray(_,s,a) -> a

    member x.size =
        match x with
            | Struct l -> l.size
            | Primitive(_,s,a) -> Fixed s
            | Array(e,l, s, a) -> Fixed s
            | RuntimeArray _ -> Dynamic

and UniformBufferField =
    {
        name        : string
        fieldType   : UniformType
        offset      : int
    }

and UniformBufferLayout = 
    { 
        align : int
        size : Size
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

            | ShaderType.Float(w) -> 
                // both the size and alignment are the size of the scalar
                // in basic machine types (e.g. sizeof<int>)
                let size = w / 8
                UniformType.Primitive(PrimitiveType.Float(w), size, size)

            | ShaderType.Bool -> 
                UniformType.Primitive(PrimitiveType.Bool, 4, 4)

            | ShaderType.Vector(bt,3) -> 
                // both the size and alignment are 4 times the size
                // of the underlying scalar type.
                match toUniformType bt with
                    | Primitive(t,s,a) -> 
                        UniformType.Primitive(PrimitiveType.Vector(t, 3), s * 3, s * 4)
                    | o ->
                        UniformType.Struct(structLayout [bt, "X", []; bt, "Y", []; bt, "Z", []])

            | ShaderType.Vector(bt,d) ->  
                // both the size and alignment are <d> times the size
                // of the underlying scalar type.
                match toUniformType bt with
                    | Primitive(t,s,a) -> 
                        UniformType.Primitive(PrimitiveType.Vector(t, d), s * d, s * d)
                    | o ->
                        let fields = [bt, "X", []; bt, "Y", []; bt, "Z", []; bt, "Z", []] |> List.take d
                        UniformType.Struct(structLayout fields)


            | ShaderType.Array(bt, len) -> 
                // the size of each element in the array will be the size
                // of the element type rounded up to a multiple of the size
                // of a vec4. This is also the array's alignment.
                // The array's size will be this rounded-up element's size
                // times the number of elements in the array.
                let et = toUniformType bt
                let physicalSize = et.size

                match physicalSize with
                    | Fixed physicalSize ->
                        let size =
                            if physicalSize % 16 = 0 then physicalSize
                            else physicalSize + 16 - (physicalSize % 16)

                        UniformType.Array(et, len, size * len, size)
                    | Dynamic ->
                        failwith "UniformBuffer cannot contain arrays of dynamically sized objects"
                

            | ShaderType.RuntimeArray(bt) -> 
                // the size of each element in the array will be the size
                // of the element type rounded up to a multiple of the size
                // of a vec4. This is also the array's alignment.
                // The array's size will be this rounded-up element's size
                // times the number of elements in the array.
                let et = toUniformType bt
                let physicalSize = et.size
                
                match physicalSize with
                    | Fixed physicalSize ->
                        let size =
                            if physicalSize % 16 = 0 then physicalSize
                            else physicalSize + 16 - (physicalSize % 16)

                        UniformType.RuntimeArray(et, size, size)
                    | Dynamic ->
                        failwith "UniformBuffer cannot contain arrays of dynamically sized objects"
                

            | ShaderType.Matrix(colType, cols) ->
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

            | ShaderType.Struct(name,fields) -> 
                let layout = structLayout fields
                UniformType.Struct(structLayout fields)

            | ShaderType.Ptr(_,t) -> 
                toUniformType t

            | ShaderType.Image(sampledType, dim,depth,arr, ms, sam, fmt) -> 
                failf "cannot determine size for image type"

            | ShaderType.Sampler -> 
                failf "cannot determine size for sampler type"


            | ShaderType.Void -> 
                failf "cannot determine size for void type"

            | ShaderType.Function _ ->
                failf "cannot use function in UniformBuffer"

            | ShaderType.SampledImage _ ->
                failf "cannot use SampledImage in UniformBuffer"

    and structLayout (fields : list<ShaderType * string * list<Decoration * int[]>>) : UniformBufferLayout =
        let mutable currentOffset = 0
        let mutable offsets : Map<string, int> = Map.empty
        let mutable types : Map<string, ShaderType> = Map.empty
        let mutable biggestFieldSize = 0
        let mutable biggestFieldAlign = 0

        let fields = 
            fields |> List.map (fun (t,n,dec) ->
                if currentOffset < 0 then
                    failwith "UniformBuffer cannot contain fields after RuntimeArray"

                let t = toUniformType t
                let align = t.align

                // align the field offset
                if currentOffset % align <> 0 then
                    currentOffset <- currentOffset + align - (currentOffset % align)

                let declaredOffset =
                    dec |> List.tryPick (function (Decoration.Offset,[| off |]) -> Some (int off) | _ -> None)

                let offset =
                    match declaredOffset with
                        | Some o -> o
                        | None -> currentOffset

                let size = t.size
                match size with
                    | Dynamic ->
                        ()
                    | Fixed size ->
                        // keep track of the biggest member
                        if size > biggestFieldSize then
                            biggestFieldSize <- size
                            biggestFieldAlign <- align

                let result = 
                    {
                        name        = n
                        fieldType   = t
                        offset      = offset
                    }

                match size with
                    | Fixed size ->
                        // store the member's offset
                        currentOffset <- offset + size
                    | Dynamic ->
                        currentOffset <- -1

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
            if currentOffset < 0 then Dynamic
            elif currentOffset % structAlign = 0 then Fixed currentOffset
            else currentOffset + structAlign - (currentOffset % structAlign) |> Fixed

        { align = structAlign; size = structSize; fields = fields }



type ShaderIOParameter =
    {
        location    : int
        name        : string
        semantic    : Symbol
        shaderType  : PrimitiveType
        hostType    : System.Type
        count       : int
        isRowMajor  : bool
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ShaderIOParameter =
    let ofShaderParameter (p : ShaderParameter) =
        let location =
            match ShaderParameter.tryGetLocation p with
                | Some loc -> loc
                | None -> failf "could not get input location for parameter %A" p

        let isRowMajor = ShaderParameter.isRowMajor p

        let paramType, arraySize =
            match p.paramType with
                | ShaderType.Ptr((StorageClass.Input | StorageClass.Output),realType) ->
                    match realType with
                        | ShaderType.Array(elementType, count) -> PrimitiveType.ofShaderType elementType, count
                        | t -> PrimitiveType.ofShaderType t, 1
                | _ ->
                    failf "input parameter %A has invalid type %A" p.paramName p.paramType

        {
            location = location
            name = p.paramName
            semantic = Symbol.Create p.paramName
            shaderType = paramType
            hostType = PrimitiveType.toType paramType
            count = arraySize
            isRowMajor = isRowMajor
        }


type ShaderUniformBlock =
    {
        set         : int
        binding     : int
        name        : string
        layout      : UniformBufferLayout
    }

type ShaderSamplerType =
    {        
        dimension       : TextureDimension
        isArray         : bool
        isMultisampled  : bool
    }

type ShaderTextureInfo =
    {
        set             : int
        binding         : int
        count           : int
        name            : string
        description     : list<SamplerDescription>
        resultType      : PrimitiveType
        isDepth         : Option<bool>
        isSampled       : bool
        format          : Option<TextureFormat>
        samplerType     : ShaderSamplerType
    }

type ShaderUniformParameter =
    | UniformBlockParameter of ShaderUniformBlock
    | ImageParameter of ShaderTextureInfo

    member x.Name =
        match x with
            | UniformBlockParameter b -> b.name
            | ImageParameter i -> i.name
            
    member x.DescriptorSet =
        match x with
            | UniformBlockParameter b -> b.set
            | ImageParameter i -> i.set
            
    member x.Binding =
        match x with
            | UniformBlockParameter b -> b.binding
            | ImageParameter i -> i.binding

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ShaderUniformParameter =

    module Dim =
        let toTextureDimension =
            LookupTable.lookupTable [
                Dim.Dim1D, TextureDimension.Texture1D
                Dim.Dim2D, TextureDimension.Texture2D
                Dim.Dim3D, TextureDimension.Texture3D
                Dim.Cube, TextureDimension.TextureCube
                Dim.Rect, TextureDimension.Texture2D
                Dim.Buffer, TextureDimension.Texture1D
                Dim.SubpassData, TextureDimension.Texture2D
            ]

    module ImageFormat =
        let private textureFormats =
            // https://www.khronos.org/registry/spir-v/specs/1.0/SPIRV.html#Image_Format
            [|
                unbox<TextureFormat> 0
                TextureFormat.Rgba32f
                TextureFormat.Rgba16f
                TextureFormat.R32f
                TextureFormat.Rgba8
                TextureFormat.Rgba8Snorm
                TextureFormat.Rg32f
                TextureFormat.Rg16f
                TextureFormat.R11fG11fB10f
                TextureFormat.R16f
                TextureFormat.Rgba16
                TextureFormat.Rgb10A2
                TextureFormat.Rg16
                TextureFormat.Rg8
                TextureFormat.R16
                TextureFormat.R8
                TextureFormat.Rgba16Snorm
                TextureFormat.Rg16Snorm
                TextureFormat.Rg8Snorm
                TextureFormat.R16Snorm
                TextureFormat.R8Snorm
                TextureFormat.Rgba32i
                TextureFormat.Rgba16i
                TextureFormat.Rgba8i
                TextureFormat.R32i
                TextureFormat.Rg32i
                TextureFormat.Rg16i
                TextureFormat.Rg8i
                TextureFormat.R16i
                TextureFormat.R8i
                TextureFormat.Rgba32ui
                TextureFormat.Rgba16ui
                TextureFormat.Rgba8ui
                TextureFormat.R32ui
                TextureFormat.Rgb10A2ui
                TextureFormat.Rg32ui
                TextureFormat.Rg16ui
                TextureFormat.Rg8ui
                TextureFormat.R16ui
                TextureFormat.R8ui
            |]

        let toTextureFormat (fmt : ImageFormat) =
            let fmt = int fmt
            if fmt < 1 || fmt >= textureFormats.Length then None
            else Some textureFormats.[fmt]

    let ofShaderParameter (p : ShaderParameter) =
        match p.paramType with
            | ShaderType.Ptr((StorageClass.Uniform | StorageClass.UniformConstant), realType) ->
                let set =
                    match ShaderParameter.tryGetDescriptorSet p with
                        | Some set -> set
                        | None -> failf "uniform %A does not provide a DescriptorSet" p.paramName
                        
                let binding =
                    match ShaderParameter.tryGetBinding p with
                        | Some set -> set
                        | None -> failf "uniform %A does not provide a binding" p.paramName

                match realType with
                    | ShaderType.SampledImage(ShaderType.Image(resultType,dim,isDepth,isArray,isMS,isSampled,fmt))
                    | ShaderType.Image(resultType,dim,isDepth,isArray,isMS,isSampled,fmt) ->
                        ImageParameter {
                            set             = set
                            binding         = binding
                            name            = p.paramName
                            count           = 1
                            description     = []
                            resultType      = PrimitiveType.ofShaderType resultType
                            isDepth         = (match isDepth with | 0 -> Some false | 1 -> Some true | _ -> None)
                            isSampled       = isSampled
                            format          = ImageFormat.toTextureFormat fmt
                            samplerType =
                                {
                                    dimension       = Dim.toTextureDimension dim
                                    isArray         = isArray
                                    isMultisampled  = (if isMS = 0 then false else true)
                                }
                        }

                    | ShaderType.Array((ShaderType.SampledImage(ShaderType.Image(resultType,dim,isDepth,isArray,isMS,isSampled,fmt)) | ShaderType.Image(resultType,dim,isDepth,isArray,isMS,isSampled,fmt)), len) ->
                        ImageParameter {
                            set             = set
                            binding         = binding
                            name            = p.paramName
                            count           = len
                            description     = []
                            resultType      = PrimitiveType.ofShaderType resultType
                            isDepth         = (match isDepth with | 0 -> Some false | 1 -> Some true | _ -> None)
                            format          = ImageFormat.toTextureFormat fmt
                            isSampled       = isSampled
                            samplerType =
                                {
                                    dimension       = Dim.toTextureDimension dim
                                    isMultisampled  = (if isMS = 0 then false else true)
                                    isArray         = isArray
                                }
                        }

                    | ShaderType.Struct(name, fields) ->
                        let layout = UniformBufferLayoutStd140.structLayout fields
                        UniformBlockParameter {
                            set         = set
                            binding     = binding
                            name        = p.paramName
                            layout      = layout
                        }

                    | other ->
                        let uniformType = UniformBufferLayoutStd140.toUniformType other
                        let layout = 
                            { 
                                align = uniformType.align
                                size = uniformType.size
                                fields = [ { name = p.paramName; fieldType = uniformType; offset = 0 } ] 
                            }
                        
                        UniformBlockParameter {
                            set         = set
                            binding     = binding
                            name        = ""
                            layout      = layout
                        }
            | t ->
                failf "uniform %A has unexpected type %A" p.paramName t

[<System.Flags>]
type TessellationFlags =
    | None                  = 0x0000
    /// Requests the tessellation primitive generator to divide edges into a collection of equal-sized segments.
    | SpacingEqual          = 0x0001
    /// Requests the tessellation primitive generator to divide edges into an even number of equal-length segments plus two additional shorter fractional segments.
    | SpacingFractionalEven = 0x0002
    /// Requests the tessellation primitive generator to divide edges into an odd number of equal-length segments plus two additional shorter fractional segments.
    | SpacingFractionalOdd  = 0x0004
    /// Requests the tessellation primitive generator to generate triangles in clockwise order.
    | VertexOrderCw         = 0x0008
    /// Requests the tessellation primitive generator to generate triangles in counter-clockwise order.
    | VertexOrderCcw        = 0x0010
    /// Requests the tessellation primitive generator to generate a point for each distinct vertex in the subdivided primitive, rather than to generate lines or triangles
    | OutputPoints          = 0x0020
    /// Requests the tessellation primitive generator to generate triangles.
    | OutputTriangles       = 0x0040
    /// Requests the tessellation primitive generator to generate quads.
    | OutputQuads           = 0x0080
    /// Requests the tessellation primitive generator to generate isolines.
    | OutputIsolines        = 0x0100

[<System.Flags>]
type GeometryFlags =
    | None                      = 0x0000
    /// Stage input primitive is points. 
    | InputPoints               = 0x0001
    /// Stage input primitive is lines. 
    | InputLines                = 0x0002
    /// Stage input primitive is lines adjacency.
    | InputLinesAdjacency       = 0x0004
    /// Stage input primitive is triangles.
    | InputTriangles            = 0x0008
    /// Geometry stage input primitive is triangles adjacency.
    | InputTrianglesAdjacency    = 0x0008
    /// Stage output primitive is points. 
    | OutputPoints              = 0x0010
    /// Stage output primitive is line strip.
    | OutputLineStrip           = 0x0020
    /// Stage output primitive is triangle strip.
    | OutputTriangleStrip       = 0x0040

[<System.Flags>]
type FragmentFlags =
    | None                      = 0x0000
    /// Pixels appear centered on whole-number pixel offsets. E.g., the coordinate (0.5, 0.5) appears to move to (0.0, 0.0).
    | PixelCenterInteger        = 0x0001
    /// Pixel coordinates appear to originate in the upper left, and increase toward the right and downward.
    | OriginUpperLeft           = 0x0002
    /// Pixel coordinates appear to originate in the lower left, and increase toward the right and upward.
    | OriginLowerLeft           = 0x0004
    /// Fragment tests are to be performed before fragment shader execution.
    | EarlyFragmentTests        = 0x0008
    /// This mode must be declared if this module potentially changes the fragment’s depth.
    | DepthReplacing            = 0x0010
    /// External optimizations may assume depth modifications will leave the fragment’s depth as greater than or equal to the fragment’s interpolated depth value (given by the z component of the FragCoord BuiltIn decorated variable).
    | DepthGreater              = 0x0020
    /// External optimizations may assume depth modifications leave the fragment’s depth less than the fragment’s interpolated depth value, (given by the z component of the FragCoord BuiltIn decorated variable).
    | DepthLess                 = 0x0040
    /// External optimizations may assume this stage did not modify the fragment’s depth. However, DepthReplacing mode must accurately represent depth modification.
    | DepthUnchanged            = 0x0080

type TessellationInfo =
    {
        /// Tessellation flags specifying execution aspects of the  shader
        flags           : TessellationFlags
        /// The number of vertices in the output patch produced by the tessellation control shader, which also specifies the number of times the tessellation control shader is invoked.
        outputVertices  : int
    }

type GeometryInfo =
    {
        flags           : GeometryFlags
        outputVertices  : int
        invocations     : int
    }

type FragmentInfo =
    {
        flags           : FragmentFlags
        discard         : bool
        sampleShading   : bool
    }

type ShaderExecutionInfo =
    {
        invocations : int
    }

type ShaderKind =
    | Vertex
    | TessControl of TessellationInfo
    | TessEval of TessellationFlags
    | Geometry of GeometryInfo
    | Fragment of FragmentInfo
    | Compute

type ShaderInfo = 
    { 
        kind            : ShaderKind
        entryPoint      : string
        builtInInputs   : Map<BuiltIn, ShaderType>
        builtInOutputs  : Map<BuiltIn, ShaderType>

        inputs          : list<ShaderIOParameter>
        uniformBlocks   : list<ShaderUniformBlock>
        storageBlocks   : list<ShaderUniformBlock>
        textures        : list<ShaderTextureInfo>
        outputs         : list<ShaderIOParameter>
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module private ShaderInfo =
    open System.Collections.Generic
    open System.Runtime.InteropServices
    open FShade.SpirV
    open Aardvark.Base.Monads.Option

    module private List =
        let mapOption (f : 'a -> Option<'b> ) (xs : list<'a>) =
            let mapped = xs |> List.map f
            if mapped |> List.forall Option.isSome 
            then mapped |> List.map Option.get |> Some
            else None

    module private Map =
        let ofListWithDuplicates(xs : list<'k*'v>) : Map<'k,list<'v>> =
            let mutable map = Map.empty
            for (k,v) in xs do
                match map |> Map.tryFind k with
                    | Some v' -> map <- Map.add k (v :: v') map
                    | None -> map <- Map.add k [v] map
            map

    type private FunctionProperties =
        {
            mutable id              : uint32
            mutable entryModel      : ExecutionModel
            mutable entryName       : string
            mutable geometryFlags   : GeometryFlags
            mutable tessFlags       : TessellationFlags
            mutable fragFlags       : FragmentFlags
            mutable outputVertices  : int
            mutable invocations     : int
            mutable discards        : bool
            usedVariables           : HashSet<uint32>
        }
        static member Empty = 
            {
                id = 0u
                entryModel = ExecutionModel.Vertex
                entryName = ""
                geometryFlags = GeometryFlags.None
                tessFlags = TessellationFlags.None
                fragFlags = FragmentFlags.None
                outputVertices = 0
                invocations = 1
                discards = false
                usedVariables = HashSet.empty
            }

    let private structType      = ShaderType.Struct("", []).GetType()
    let private structName      = structType.GetField("_name", System.Reflection.BindingFlags.NonPublic ||| System.Reflection.BindingFlags.Instance)
    let private structFields    = structType.GetField("_fields", System.Reflection.BindingFlags.NonPublic ||| System.Reflection.BindingFlags.Instance)


    let ofInstructions (instructions : list<Instruction>) =
        let variables               = Dict.empty
        let names                   = Dict.empty
        let decorations             = Dict.empty
        let memberNames             = Dict.empty
        let memberDecorations       = Dict.empty
        let types                   = Dict.empty
        let functions               = Dict.empty
        let structs                 = HashSet.empty
        let callers                 = Dict.empty
        let constants               = Dict.empty
        let entries                 = List<uint32>()
        let mutable currentFunction = None

        let getProps e = functions.GetOrCreate(e, fun e -> { FunctionProperties.Empty with id = e })

        let rec addUsed (v : uint32) (f : FunctionProperties) =
            if f.usedVariables.Add v then
                match callers.TryGetValue f.id with
                    | (true, callers) ->
                        for c in callers do
                            let c = getProps c
                            addUsed v c
                    | _ ->
                        ()

        let rec addCaller (caller : FunctionProperties) (callee : uint32) =
            let set = callers.GetOrCreate(callee, fun _ -> HashSet())
            if set.Add(caller.id) then
                match functions.TryGetValue callee with
                    | (true, calleeProps) ->
                        let calleeUsed = calleeProps.usedVariables
                        if calleeUsed.Count = 0 then
                            let rec union (current : FunctionProperties) =
                                current.usedVariables.UnionWith calleeUsed
                                match callers.TryGetValue current.id with
                                    | (true, callers) ->
                                        for c in callers do
                                            let props = getProps c
                                            union props
                                    | _ ->
                                        ()
                            union caller
                    | _ ->
                        ()

        // process the instructions maintaining all needed information
        for i in instructions do
            match i with

                | OpConstant(t,r,v) ->
                    match types.[t] with
                        | ShaderType.Int(32,_) -> constants.[r] <- int v.[0]
                        | _ -> ()

                | OpTypeVoid r              -> types.[r] <- ShaderType.Void
                | OpTypeBool r              -> types.[r] <- ShaderType.Bool
                | OpTypeInt (r, w, s)       -> types.[r] <- ShaderType.Int(int w, s = 1)
                | OpTypeFloat (r, w)        -> types.[r] <- ShaderType.Float(int w)
                | OpTypeVector (r, c, d)    -> types.[r] <- ShaderType.Vector(types.[c], int d)
                | OpTypeMatrix (r, c, d)    -> types.[r] <- ShaderType.Matrix(types.[c], int d)
                | OpTypeArray (r, e, l)     -> types.[r] <- ShaderType.Array(types.[e], constants.[l])
                | OpTypeSampler r           -> types.[r] <- ShaderType.Sampler
                | OpTypeSampledImage (r,t)  -> types.[r] <- ShaderType.SampledImage(types.[t])
                | OpTypePointer (r, c, t)   -> types.[r] <- ShaderType.Ptr(c, types.[t])
                | OpTypeRuntimeArray(r,t)   -> types.[r] <- ShaderType.RuntimeArray(types.[t])
                | OpTypeImage(r,sampledType,dim,depth, arrayed, ms, sampled,format,access) ->
                    types.[r] <- ShaderType.Image(types.[sampledType], unbox<Dim> dim, int depth, (arrayed = 1), int ms, (sampled = 1), format)

                | OpTypeStruct (r, fts) -> 
                    let fieldTypes = fts |> Array.toList |> List.map (fun ft -> types.[ft])
                    structs.Add r |> ignore
                    types.[r] <- ShaderType.Struct("", fieldTypes |> List.map (fun t -> t, "", []))

                | OpFunction(_,id,_,_) -> currentFunction <- Some id
                | OpFunctionEnd -> currentFunction <- None

                | OpLoad(_,_,id,_)
                | OpAccessChain(_,_,id,_) 
                | OpInBoundsAccessChain(_,_,id,_) 
                | OpPtrAccessChain(_,_,id,_,_)
                | OpStore(id,_,_)
                | OpImageTexelPointer(_,_,id,_,_) ->
                    match currentFunction with
                        | Some f when variables.Contains id ->
                            let props = getProps f
                            addUsed id props
                        | _ ->
                            ()

                

                | OpVariable(typeId, id, kind, _) ->
                    match kind with
                        | StorageClass.Input 
                        | StorageClass.Uniform 
                        | StorageClass.UniformConstant
                        | StorageClass.Image 
                        | StorageClass.Output ->
                            variables.[id] <- (typeId, kind)

                        | StorageClass.PushConstant ->
                            failf "PushConstants not supported atm."

                        | _ -> 
                            ()

                | OpName(target, name) ->
                    names.[target] <- name

                | OpMemberName(target, index, name) -> 
                    memberNames.[(target, index)] <- name

                | OpMemberDecorate(target, index,dec,args) -> 
                    let decorations = memberDecorations.GetOrCreate((target,index), fun _ -> CSharpList.empty)
                    decorations.Add(dec, args)

                | OpDecorate(target, dec,args) ->
                    let decorations = decorations.GetOrCreate(target, fun _ -> CSharpList.empty)
                    decorations.Add(dec, args)

                | OpEntryPoint(model, id, name,_) ->
                    let mode = getProps id
                    mode.entryModel <- model
                    mode.entryName <- name
                    entries.Add id

                | OpKill ->
                    match currentFunction with
                        | Some f -> 
                            let s = getProps f
                            s.discards <- true
                            match callers.TryGetValue f with
                                | (true, callers) ->
                                    for c in callers do
                                        let m = getProps c
                                        m.discards <- true
                                | _ ->
                                    ()

                        | None -> ()

                | OpFunctionCall(_,_,f,args) ->
                    match currentFunction with
                        | Some current -> 
                            let cm = getProps current
                            addCaller cm f

                            for a in args do
                                if variables.Contains a then
                                    addUsed a cm

                        | _ ->
                            ()

                | OpExecutionMode(entry, mode, arg) ->
                    let m = getProps entry

                    match mode with
                        | ExecutionMode.SpacingEqual            -> m.tessFlags <- m.tessFlags ||| TessellationFlags.SpacingEqual
                        | ExecutionMode.SpacingFractionalEven   -> m.tessFlags <- m.tessFlags ||| TessellationFlags.SpacingFractionalEven
                        | ExecutionMode.SpacingFractionalOdd    -> m.tessFlags <- m.tessFlags ||| TessellationFlags.SpacingFractionalOdd
                        | ExecutionMode.VertexOrderCw           -> m.tessFlags <- m.tessFlags ||| TessellationFlags.VertexOrderCw
                        | ExecutionMode.VertexOrderCcw          -> m.tessFlags <- m.tessFlags ||| TessellationFlags.VertexOrderCcw
                        | ExecutionMode.PointMode               -> m.tessFlags <- m.tessFlags ||| TessellationFlags.OutputPoints
                        | ExecutionMode.Quads                   -> m.tessFlags <- m.tessFlags ||| TessellationFlags.OutputQuads
                        | ExecutionMode.Isolines                -> m.tessFlags <- m.tessFlags ||| TessellationFlags.OutputIsolines

                        | ExecutionMode.Triangles -> 
                            m.tessFlags <- m.tessFlags ||| TessellationFlags.OutputTriangles
                            m.geometryFlags <- m.geometryFlags ||| GeometryFlags.InputTriangles

                        | ExecutionMode.InputPoints             -> m.geometryFlags <- m.geometryFlags ||| GeometryFlags.InputPoints
                        | ExecutionMode.InputLines              -> m.geometryFlags <- m.geometryFlags ||| GeometryFlags.InputLines
                        | ExecutionMode.InputLinesAdjacency     -> m.geometryFlags <- m.geometryFlags ||| GeometryFlags.InputLinesAdjacency
                        | ExecutionMode.InputTrianglesAdjacency -> m.geometryFlags <- m.geometryFlags ||| GeometryFlags.InputTrianglesAdjacency
                        | ExecutionMode.OutputPoints            -> m.geometryFlags <- m.geometryFlags ||| GeometryFlags.OutputPoints
                        | ExecutionMode.OutputLineStrip         -> m.geometryFlags <- m.geometryFlags ||| GeometryFlags.OutputLineStrip
                        | ExecutionMode.OutputTriangleStrip     -> m.geometryFlags <- m.geometryFlags ||| GeometryFlags.OutputTriangleStrip

                        | ExecutionMode.Invocations             -> m.invocations <- arg.[0]
                        | ExecutionMode.OutputVertices          -> m.outputVertices <- arg.[0]


                        | ExecutionMode.PixelCenterInteger      -> m.fragFlags <- m.fragFlags ||| FragmentFlags.PixelCenterInteger
                        | ExecutionMode.OriginUpperLeft         -> m.fragFlags <- m.fragFlags ||| FragmentFlags.OriginUpperLeft
                        | ExecutionMode.OriginLowerLeft         -> m.fragFlags <- m.fragFlags ||| FragmentFlags.OriginLowerLeft
                        | ExecutionMode.EarlyFragmentTests      -> m.fragFlags <- m.fragFlags ||| FragmentFlags.EarlyFragmentTests
                        | ExecutionMode.DepthReplacing          -> m.fragFlags <- m.fragFlags ||| FragmentFlags.DepthReplacing
                        | ExecutionMode.DepthGreater            -> m.fragFlags <- m.fragFlags ||| FragmentFlags.DepthGreater
                        | ExecutionMode.DepthLess               -> m.fragFlags <- m.fragFlags ||| FragmentFlags.DepthLess
                        | ExecutionMode.DepthUnchanged          -> m.fragFlags <- m.fragFlags ||| FragmentFlags.DepthUnchanged

                        | _ -> ()

                | _ ->
                    ()
          
        // inject field-names/-decorations into structs via reflection
        // NOTE: avoids multiple traversals of the instruction list
        for id in structs do
            let t = types.[id]
            match t with
                | ShaderType.Struct(_, fields) ->
                    let name =
                        match names.TryGetValue id with
                            | (true, name) -> name
                            | _ -> "noname"

                    let fields =
                        fields |> List.mapi (fun fi (t,_,_) ->
                            let name = 
                                match memberNames.TryGetValue ((id, fi)) with
                                    | (true, name) -> name
                                    | _ -> sprintf "field%d" fi

                            let decorations =
                                match memberDecorations.TryGetValue((id, fi)) with
                                    | (true, dec) -> dec |> CSharpList.toList
                                    | _ -> []

                            (t, name, decorations)
                        )

                    structName.SetValue(t, name)
                    structFields.SetValue(t, fields)
                | _ ->
                    failf "not a struct"


        let parameters = Dict.empty
        for KeyValue(id, (tid, kind)) in variables do
            let vType   = types.[tid]
            let vDec    = (match decorations.TryGetValue id with | (true, d) -> CSharpList.toList d | _ -> [])
            let vName   = (match names.TryGetValue id with | (true, n) -> n | _ -> "")
            let vPar    = { paramName = vName; paramType = vType; paramDecorations = vDec }
            parameters.[id] <- vPar
        
        entries
            |> Seq.map (fun eid -> functions.[eid])
            |> Seq.map (fun m ->
                let mutable stage, kind =
                    match m.entryModel with
                        | ExecutionModel.Vertex -> ShaderStage.Vertex, Vertex
                        | ExecutionModel.TessellationControl -> ShaderStage.TessControl, TessControl { flags = m.tessFlags; outputVertices = m.outputVertices }
                        | ExecutionModel.TessellationEvaluation -> ShaderStage.TessEval, TessEval m.tessFlags
                        | ExecutionModel.Geometry -> ShaderStage.Geometry, Geometry { flags = m.geometryFlags; outputVertices = m.outputVertices; invocations = m.invocations }
                        | ExecutionModel.Fragment -> ShaderStage.Fragment, Fragment { flags = m.fragFlags; discard = m.discards; sampleShading = false }
                        | ExecutionModel.GLCompute -> ShaderStage.Compute, Compute
                        | m -> failf "unsupported ExecutionModel %A" m

                let inputs          = CSharpList.empty
                let outputs         = CSharpList.empty
                let uniformBlocks   = CSharpList.empty
                let storageBlocks   = CSharpList.empty
                let textures        = CSharpList.empty
                let builtInInputs   = Dict.empty
                let builtInOutputs  = Dict.empty

                for vid in m.usedVariables do
                    let vPar = parameters.[vid]
                    match vPar.paramType with
                        | Ptr(StorageClass.Input,t) -> 
                            match t with
                                | ShaderType.Struct(name,fields) when name.StartsWith "gl_" -> 
                                    ()
                                | _ -> 
                                    match ShaderParameter.tryGetBuiltInSemantic vPar with
                                        | Some sem -> 
                                            match sem with
                                                | BuiltIn.SampleId | BuiltIn.SamplePosition -> 
                                                    kind <- 
                                                        match kind with
                                                            | Fragment info -> Fragment { info with sampleShading = true }
                                                            | _ -> kind
                                                | _ ->
                                                    ()
                                            builtInInputs.[sem] <- t
                                        | None -> inputs.Add(ShaderIOParameter.ofShaderParameter vPar)

                        | Ptr(StorageClass.Output,t) ->
                            match t with
                                | ShaderType.Struct(name,fields) when name.StartsWith "gl_" -> ()
                                | _ -> 
                                    match ShaderParameter.tryGetBuiltInSemantic vPar with
                                        | Some sem -> builtInOutputs.[sem] <- t
                                        | None -> outputs.Add(ShaderIOParameter.ofShaderParameter vPar)

                        | Ptr((StorageClass.Uniform | StorageClass.Image | StorageClass.UniformConstant),_) ->
                            match ShaderUniformParameter.ofShaderParameter vPar with
                                | UniformBlockParameter block -> 
                                    match block.layout.size with
                                        | Fixed _ -> uniformBlocks.Add block
                                        | Dynamic -> storageBlocks.Add block
                                | ImageParameter img -> 
                                    textures.Add img

                        | _ ->
                            ()

                stage, { 
                    kind            = kind
                    entryPoint      = m.entryName
                    builtInInputs   = Dict.toMap builtInInputs
                    builtInOutputs  = Dict.toMap builtInOutputs
                    inputs          = CSharpList.toList inputs
                    uniformBlocks   = CSharpList.toList uniformBlocks
                    storageBlocks   = CSharpList.toList storageBlocks
                    textures        = CSharpList.toList textures
                    outputs         = CSharpList.toList outputs
                }
               )
            |> Map.ofSeq

    let ofModule (m : Module) =
        ofInstructions m.instructions

    let ofBinary (code : byte[]) =
        Module.ofByteArray code
            |> ofModule
//        use reader = new System.IO.BinaryReader(new System.IO.MemoryStream(code))
//        reader 
//            |> Serializer.read
//            |> ofModule

    let ofStream (stream : System.IO.Stream) =
        Module.readFrom stream
            |> ofModule
//        use reader = new System.IO.BinaryReader(stream)
//        reader 
//            |> Serializer.read
//            |> ofModule


    let resolveSamplerDescriptions (resolve : ShaderTextureInfo -> list<SamplerDescription>) (info : ShaderInfo) =
        { info with
            textures = info.textures |> List.map (fun t ->
                match resolve t with
                    | [] -> t
                    | sampler -> { t with description = sampler }
            )
        }

