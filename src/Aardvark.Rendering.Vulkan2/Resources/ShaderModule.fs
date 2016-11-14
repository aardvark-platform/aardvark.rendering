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

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module private SpirVReflector =
    open SpirV
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

    let glslangStage =
        LookupTable.lookupTable [
            ShaderStage.Vertex, GLSLang.ShaderStage.Vertex
            ShaderStage.TessControl, GLSLang.ShaderStage.TessControl
            ShaderStage.TessEval, GLSLang.ShaderStage.TessEvaluation
            ShaderStage.Geometry, GLSLang.ShaderStage.Geometry
            ShaderStage.Pixel, GLSLang.ShaderStage.Fragment
        ]


type ShaderModule =
    class
        inherit Resource<VkShaderModule>
        val mutable public Stage : ShaderStage
        val mutable public Interface : ShaderInterface
        new(device : Device, handle : VkShaderModule, stage, iface) = { inherit Resource<_>(device, handle); Stage = stage; Interface = iface }
    end

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
        match GLSLang.GLSLang.tryCompileSpirVBinary (SpirVReflector.glslangStage stage) code with
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
type ContextShaderModuleExtensions private() =
    [<Extension>]
    static member inline CreateShaderModule(this : Device, stage : ShaderStage, glsl : string) =
        this |> ShaderModule.ofGLSL stage glsl

    [<Extension>]
    static member inline CreateShaderModule(this : Device, stage : ShaderStage, spirv : uint32[]) =
        this |> ShaderModule.ofBinary stage spirv
        
    [<Extension>]
    static member inline Delete(this : Device, shader : ShaderModule) =
        this |> ShaderModule.delete shader
        