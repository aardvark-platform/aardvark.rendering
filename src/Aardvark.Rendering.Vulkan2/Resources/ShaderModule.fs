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

open SpirV
open FShade.SpirV
open Aardvark.Base.Monads.Option


type Width = int
type ShaderType = FShade.SpirV.Type

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ShaderType =

    let glslDecoration (decorations : list<Decoration * uint32[]>) =
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
    let Int = ShaderType.Int(32, true)
    let Float =ShaderType. Float(32)

    let IntVec2 = ShaderType.Vector(Int, 2)
    let IntVec3 = ShaderType.Vector(Int, 3)
    let IntVec4 = ShaderType.Vector(Int, 4)

    let UnsignedIntVec2 = ShaderType.Vector(UnsignedInt, 2)
    let UnsignedIntVec3 = ShaderType.Vector(UnsignedInt, 3)
    let UnsignedIntVec4 = ShaderType.Vector(UnsignedInt, 4)

    let BoolVec2 = ShaderType.Vector(Bool, 2)
    let BoolVec3 = ShaderType.Vector(Bool, 3)
    let BoolVec4 = ShaderType.Vector(Bool, 4)

    let FloatVec2 = ShaderType.Vector(Float, 2)
    let FloatVec3 = ShaderType.Vector(Float, 3)
    let FloatVec4 = ShaderType.Vector(Float, 4)
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


    let toType =
        LookupTable.lookupTable [
            Bool,               typeof<bool>
            
            Int,                typeof<int32>
            UnsignedInt,        typeof<uint32>
            //Half,               typeof<float16>
            Float,              typeof<float32>
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

type ShaderParameter = 
    { 
        paramName : string
        paramType : ShaderType
        paramDecorations : list<Decoration * uint32[]> 
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


    let getArraySize (p : ShaderParameter) =
        1

    let inline paramName (p : ShaderParameter) = p.paramName
    let inline paramType (p : ShaderParameter) = p.paramType
    let inline paramDecorations (p : ShaderParameter) = p.paramDecorations

type ShaderInterface = 
    { 
        executionModel : ExecutionModel
        entryPoint : string
        inputs : list<ShaderParameter>
        outputs : list<ShaderParameter>
        uniforms : list<ShaderParameter>
        images : list<ShaderParameter> 
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SpirVReflector =
    
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

    let ofInstructions (instructions : list<Instruction>) =

        let inputVariables = 
            instructions |> List.choose (fun i -> 
                match i with
                    | OpVariable(typeId,target, kind, _) ->
                        match kind with
                            | StorageClass.Input 
                            | StorageClass.Uniform 
                            | StorageClass.UniformConstant
                            | StorageClass.Image 
                            | StorageClass.Output ->
                                Some (typeId, target, kind)
                            | _ -> None
                    | _ -> None
            )

        let names = 
            instructions |> List.choose (fun i ->
                match i with
                    | OpName(target,name) -> Some (target,name)
                    | _ -> None
            ) |> Map.ofList

        let memberNames = 
            instructions |> List.choose (fun i ->
                match i with
                    | OpMemberName(target, index, name) -> 
                        Some ((target,index), name)
                    | _ -> None
            ) |> Map.ofList

        let memberDecorations =   
            instructions |> List.choose (fun i ->
                match i with
                    | OpMemberDecorate(target, index,dec,args) -> Some ((target, index),(dec,args))
                    | _ -> None
            ) |> Map.ofListWithDuplicates

        let map = 
            instructions |> List.choose (fun i ->
                match i.ResultId with
                    | Some id -> Some (id,i)
                    | None -> None
            ) |> Map.ofList

        let rec typeForId id = 
            option {
                let! i = Map.tryFind id map 
                match i with 
                    | OpTypeVoid _ -> return Void
                    | OpTypeBool _ -> return Bool
                    | OpTypeInt(_,width,signed) -> return Int (int width,(signed = 1u)) 
                    | OpTypeFloat(_,width) -> return Float (int width) 
                    | OpTypeVector(_, comp, dim) -> 
                        let! comp = typeForId comp
                        return Vector(comp, int dim)
                    | OpTypeMatrix(_,colType,colCount) ->
                        let! colType = typeForId colType
                        return Matrix(colType,int colCount)
                    | OpTypeImage(resId,sampledType,dim,depth, arrayed, ms, sampled,format,access) ->
                        let! sampledType = typeForId sampledType
                        return Image(sampledType, unbox<Dim> dim, int depth, (arrayed = 1u), int ms, (sampled = 1u), format)
                    | OpTypeSampledImage(resId, imageType) ->
                        return! typeForId imageType
                    | OpTypeSampler(_) -> return Sampler
                    | OpTypeArray(_,elem,len) -> 
                        let! elem = typeForId elem
                        return Array(elem,int len)
                    | OpTypeStruct(res, fields) ->
                        let! fieldTypes = fields |> Array.toList |> List.mapOption typeForId

                        let fieldDecorations =
                            List.init fields.Length (fun i ->
                                match Map.tryFind (id,uint32 i) memberDecorations with
                                    | Some d -> d
                                    | None -> []
                            )

                        let fieldNames =
                            List.init fields.Length (fun i ->
                                match Map.tryFind (id, uint32 i) memberNames with
                                    | Some name -> name
                                    | _ -> failwith "no field name"
                            )

                        let! n = Map.tryFind res names

                        return Struct (n, List.zip3 fieldTypes fieldNames fieldDecorations)

                    | OpTypePointer(_,storageClass,baseType) ->
                        let! baseType = typeForId baseType
                        return Ptr(storageClass, baseType)
                    | _ -> return! None
                        
            }

        let decorations =   
            instructions |> List.choose (fun i ->
                match i with
                    | OpDecorate(target,dec,args) -> Some (target,(dec,args))
                    | _ -> None
            ) |> Map.ofListWithDuplicates

        let parameters = 
            inputVariables |> List.map (fun (tid,id,kind) ->
                let t = typeForId tid
                let n = names |> Map.tryFind id
                let decorations = Map.tryFind id decorations
                match t,n with
                    | Some t, Some n -> 
                        let d = match decorations with Some d -> d | None -> []
                        (kind, n, t, d)

                    | Some (Ptr (_,Struct(n,_)) as t), None ->
                        let d = match decorations with Some d -> d | None -> []
                        (kind, n, t, d)

                    | _ -> 
                        failwithf "could not resolve type or name: { type = %A; name = %A }" t n
            )

        let extractParamteters (c : StorageClass) =
            parameters 
                |> List.choose (fun p ->
                    match p with
                        | ci, n, t, d when ci = c -> Some { paramName = n; paramType = t; paramDecorations = d }
                        | _ -> None
                    )

        let images =
            parameters 
                |> List.choose (fun (ci, n, t, d) ->
                    match ci, t with
                        | (StorageClass.UniformConstant | StorageClass.Uniform), Ptr(_,Image _) -> Some { paramName = n; paramType = t; paramDecorations = d }
                        | _ -> None
                    )

        let ioSort (p : ShaderParameter) =
            match ShaderParameter.tryGetLocation p with
                | Some l -> (l, p.paramName)
                | None -> (System.Int32.MaxValue, p.paramName)

        let uniformSort (p : ShaderParameter) =
            let set = 
                match ShaderParameter.tryGetDescriptorSet p with
                    | Some l -> l
                    | None -> System.Int32.MaxValue
   
            let binding = 
                match ShaderParameter.tryGetDescriptorSet p with
                    | Some l -> l
                    | None -> System.Int32.MaxValue

            (set, binding, p.paramName)
        
        let (execModel, entryPoint) = instructions |> List.pick (function OpEntryPoint(m,_,name,_) -> Some (m,name) | _ -> None)
        let inputs = extractParamteters StorageClass.Input |> List.sortBy ioSort
        let outputs = extractParamteters StorageClass.Output |> List.sortBy ioSort
        let uniforms = extractParamteters StorageClass.Uniform |> List.sortBy uniformSort
        
        { executionModel = execModel; entryPoint = entryPoint ;inputs = inputs; outputs = outputs; uniforms = uniforms; images = images }

    let ofModule (m : Module) =
        ofInstructions m.instructions

    let ofBinary (code : uint32[]) =
        let code = Array.copy(code).UnsafeCoerce<byte>()
        use reader = new System.IO.BinaryReader(new System.IO.MemoryStream(code))
        reader 
            |> Serializer.read
            |> ofModule

    let ofStream (stream : System.IO.Stream) =
        use reader = new System.IO.BinaryReader(stream)
        reader 
            |> Serializer.read
            |> ofModule

type ShaderModule =
    class
        inherit Resource<VkShaderModule>
        val mutable public Stage : ShaderStage
        val mutable public Interface : ShaderInterface
        new(device : Device, handle : VkShaderModule, stage, iface) = { inherit Resource<_>(device, handle); Stage = stage; Interface = iface }
    end

module private GLSLang =
    let stage =
        LookupTable.lookupTable [
            ShaderStage.Vertex, GLSLang.ShaderStage.Vertex
            ShaderStage.TessControl, GLSLang.ShaderStage.TessControl
            ShaderStage.TessEval, GLSLang.ShaderStage.TessEvaluation
            ShaderStage.Geometry, GLSLang.ShaderStage.Geometry
            ShaderStage.Pixel, GLSLang.ShaderStage.Fragment
        ]

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ShaderModule =


    let private createRaw (binary : uint32[]) (device : Device) =
        binary |> NativePtr.withA (fun pBinary ->
            let mutable info =
                VkShaderModuleCreateInfo(
                    VkStructureType.ShaderModuleCreateInfo, 0n, 
                    VkShaderModuleCreateFlags.MinValue,
                    uint64 binary.LongLength,
                    pBinary
                )

            let mutable handle = VkShaderModule.Null
            VkRaw.vkCreateShaderModule(device.Handle, &&info, NativePtr.zero, &&handle)
                |> check "could not create shader module"

            handle
        )

    let ofBinary (stage : ShaderStage) (binary : uint32[]) (device : Device) =
        let handle = device |> createRaw binary
        let iface = SpirVReflector.ofBinary binary
        let result = ShaderModule(device, handle, stage, iface)
        result

    let ofGLSL (stage : ShaderStage) (code : string) (device : Device) =
        match GLSLang.GLSLang.tryCompileSpirVBinary (GLSLang.stage stage) code with
            | Success binary ->
                let handle = device |> createRaw binary
                let iface = SpirVReflector.ofBinary binary
                let result = ShaderModule(device, handle, stage, iface)
                result
            | Error err ->
                Log.error "%s" err
                failf "shader compiler returned errors %A" err

    let delete (shader : ShaderModule) (device : Device) =
        if shader.Handle.IsValid then
            VkRaw.vkDestroyShaderModule(device.Handle, shader.Handle, NativePtr.zero)
            shader.Handle <- VkShaderModule.Null
    

[<AbstractClass; Sealed; Extension>]
type ContextFramebufferExtensions private() =
    [<Extension>]
    static member inline CreateShaderModule(this : Device, stage : ShaderStage, glsl : string) =
        this |> ShaderModule.ofGLSL stage glsl

    [<Extension>]
    static member inline CreateShaderModule(this : Device, stage : ShaderStage, spirv : uint32[]) =
        this |> ShaderModule.ofBinary stage spirv
        
    [<Extension>]
    static member inline Delete(this : Device, shader : ShaderModule) =
        this |> ShaderModule.delete shader
        