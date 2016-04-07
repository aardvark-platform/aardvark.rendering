namespace Aardvark.Rendering.Vulkan

open System
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Base.Rendering
open Microsoft.FSharp.NativeInterop

#nowarn "9"
#nowarn "51"


type Shader =
    class
        val mutable public Handle : VkShaderModule
        val mutable public Interface : ShaderInterface
        val mutable public SourceCode : Option<string>
        val mutable public Stage : VkShaderStageFlags
        new(handle, iface, source, stage) = { Handle = handle; Interface = iface; SourceCode = source; Stage = stage }
    end

type DescriptorSetLayoutBinding =
    class
        val mutable public Handle : VkDescriptorSetLayoutBinding
        val mutable public Parameter : Parameter

        member x.Name = x.Parameter.paramName

        new(h, p) = { Handle = h; Parameter = p }

    end

type DescriptorSetLayout =
    class
        val mutable public Handle : VkDescriptorSetLayout
        val mutable public Descriptors : DescriptorSetLayoutBinding[]

        new(h, desc) = { Handle = h; Descriptors = desc }
    end

type PipelineLayout =
    class
        val mutable public Handle : VkPipelineLayout
        val mutable public DescriptorSetLayouts : DescriptorSetLayout[]

        new(h,d) = { Handle = h; DescriptorSetLayouts = d }
    end

type ShaderProgram =
    class
        val mutable public Surface : BackendSurface
        val mutable public Shaders : list<Shader>
        val mutable public RenderPass : RenderPass
        val mutable public DescriptorSetLayouts : DescriptorSetLayout[]
        val mutable public PipelineLayout : PipelineLayout
        val mutable public Inputs : Parameter[]
        val mutable public Outputs : Parameter[]

        member x.PrintSignature() =
            
            Log.start "program layout"

            for i in x.Inputs do
                let typeName = ShaderType.glslName i.paramType
                let dec = ShaderType.glslDecoration i.paramDecorations
                if dec.Length = 0 then Log.line "in %s %s;"  typeName i.paramName
                else Log.line "%s in %s %s;" dec typeName i.paramName

            for s in x.Shaders do
                if not <| List.isEmpty s.Interface.uniforms || not <| List.isEmpty s.Interface.images then
                    Log.start "%A" s.Stage
                    for u in s.Interface.uniforms do
                        match u.paramType with
                            | ShaderType.Ptr(_,ShaderType.Struct(name, fields)) ->
                                let str = ShaderType.glslDecoration u.paramDecorations
                                if str.Length > 0 then Log.line "%s" str

                                Log.line "uniform %s {" name
                                for (t,n,d) in fields do
                                    let d = ShaderType.glslDecoration d
                                    if d.Length > 0 then Log.line "    %s %s %s;" d (ShaderType.glslName t) n
                                    else Log.line "    %s %s;" (ShaderType.glslName t) n

                                Log.line "} %s;" u.paramName
                            | _ -> 
                                let typeName = ShaderType.glslName u.paramType
                                let str = ShaderType.glslDecoration u.paramDecorations
                                if str.Length > 0 then Log.line "%s uniform %s %s;" str typeName u.paramName
                                else Log.line "uniform %s %s;" typeName u.paramName

                    for u in s.Interface.images do
                        let typeName = ShaderType.glslName u.paramType
                        let str = ShaderType.glslDecoration u.paramDecorations
                        if str.Length > 0 then Log.line "%s uniform %s %s;" str typeName u.paramName
                        else Log.line "uniform %s %s;" typeName u.paramName

                    Log.stop()

            for i in x.Outputs do
                let typeName = ShaderType.glslName i.paramType
                let dec = ShaderType.glslDecoration i.paramDecorations
                if dec.Length = 0 then Log.line "out %s %s;"  typeName i.paramName
                else Log.line "%s out %s %s;" dec typeName i.paramName

            Log.stop()

        new(surf, shaders, pass, dsl, pl, ip, op) = { Surface = surf; Shaders = shaders; RenderPass = pass; DescriptorSetLayouts = dsl; PipelineLayout = pl; Inputs = ip; Outputs = op }

    end

type private AStage = Aardvark.Base.ShaderStage
type private GStage = GLSLang.ShaderStage

[<AbstractClass; Sealed; Extension>]
type ShaderProgramExtensions private() =

    static let versionRx = System.Text.RegularExpressions.Regex @"\#version[ \t][0-9]+[\r\n]*"

    static let toGLSLangStage =
        lookupTable [
            AStage.Vertex, GStage.Vertex
            AStage.TessControl, GStage.TessControl
            AStage.TessEval, GStage.TessEvaluation
            AStage.Geometry, GStage.Geometry
            AStage.Pixel, GStage.Fragment
        ]

    static let toVkStage =
        lookupTable [
            AStage.Vertex, VkShaderStageFlags.VertexBit
            AStage.TessControl, VkShaderStageFlags.TessellationControlBit
            AStage.TessEval, VkShaderStageFlags.TessellationEvaluationBit
            AStage.Geometry, VkShaderStageFlags.GeometryBit
            AStage.Pixel, VkShaderStageFlags.FragmentBit
        ]
    
    [<Extension>]
    static member CreateGLSLShader(this : Device, code : string, shaderStage : AStage) =
        match GLSLang.GLSLang.tryCompileSpirVBinary (toGLSLangStage shaderStage) code with
            | Success spirvBinary -> 
                let vkStage = toVkStage shaderStage
                let iface = SpirVReflector.ofBinary spirvBinary

                let binary =
                    Array.concat [
                        spirvBinary
                        [|0uy;0uy;0uy;0uy|]
                    ]

                let length = (spirvBinary.Length + 3) &&& ~~~3
                let codePtr = GCHandle.Alloc(binary,GCHandleType.Pinned)

                let mutable info = 
                    VkShaderModuleCreateInfo(
                        VkStructureType.ShaderModuleCreateInfo,
                        0n,
                        VkShaderModuleCreateFlags.MinValue,
                        uint64 length,
                        NativePtr.ofNativeInt (codePtr.AddrOfPinnedObject())
                    )


                let mutable m = VkShaderModule.Null
                VkRaw.vkCreateShaderModule(this.Handle, &&info, NativePtr.zero, &&m) |> check "vkCreateShaderModule"

                codePtr.Free()

                Shader(m, iface, None, vkStage)

            | Error e -> failwith e

    [<Extension>]
    static member CreateSpriVShader(this : Device, binary : byte[], shaderStage : AStage) =
        let vkStage = toVkStage shaderStage

        let iface = SpirVReflector.ofBinary binary
        let codePtr = GCHandle.Alloc(binary,GCHandleType.Pinned)

        let mutable info = 
            VkShaderModuleCreateInfo(
                VkStructureType.ShaderModuleCreateInfo,
                0n,
                VkShaderModuleCreateFlags.MinValue,
                uint64 binary.Length,
                NativePtr.ofNativeInt (codePtr.AddrOfPinnedObject())
            )


        let mutable m = VkShaderModule.Null
        VkRaw.vkCreateShaderModule(this.Handle, &&info, NativePtr.zero, &&m) |> check "vkCreateShaderModule"
        codePtr.Free()

        Shader(m, iface, None, vkStage)

    [<Extension>]
    static member Delete(this : Device, shader : Shader) =
        if shader.Handle.IsValid then
            VkRaw.vkDestroyShaderModule(this.Handle, shader.Handle, NativePtr.zero)
            shader.Handle <- VkShaderModule.Null
            shader.Interface <- Unchecked.defaultof<_>
            shader.SourceCode <- None

    [<Extension>]
    static member CreateDescriptorSetLayoutBinding(this : Device, descriptorType : VkDescriptorType, shaderStages : VkShaderStageFlags, parameter : Parameter) =
        let h =
            VkDescriptorSetLayoutBinding(
                parameter |> Parameter.tryGetBinding |> Option.get |> uint32,
                descriptorType,
                parameter |> Parameter.getArraySize |> uint32,
                shaderStages,
                NativePtr.zero
            )
        DescriptorSetLayoutBinding(h, parameter)

    [<Extension>]
    static member CreateDescriptorSetLayout(this : Device, bindings : DescriptorSetLayoutBinding[]) =
        let pBindings = NativePtr.pushStackArray (bindings |> Array.map (fun b -> b.Handle))
        let mutable info =
            VkDescriptorSetLayoutCreateInfo(
                VkStructureType.DescriptorSetLayoutCreateInfo,
                0n, VkDescriptorSetLayoutCreateFlags.MinValue,
                uint32 bindings.Length,
                pBindings
            )

        let mutable layout = VkDescriptorSetLayout.Null
        VkRaw.vkCreateDescriptorSetLayout(this.Handle, &&info, NativePtr.zero, &&layout) |> check "vkCreateDescriptorSetLayout"

        DescriptorSetLayout(layout, bindings)

    [<Extension>]
    static member Delete(this : Device, layout : DescriptorSetLayout) =
        if layout.Handle.IsValid then
            VkRaw.vkDestroyDescriptorSetLayout(this.Handle, layout.Handle, NativePtr.zero)
            layout.Handle <- VkDescriptorSetLayout.Null
            layout.Descriptors <- [||]

    [<Extension>]
    static member CreatePipelineLayout(this : Device, descriptorSetLayouts : DescriptorSetLayout[]) =
        let pLayouts = NativePtr.pushStackArray (descriptorSetLayouts |> Array.map (fun l -> l.Handle))

        let mutable info =
            VkPipelineLayoutCreateInfo(
                VkStructureType.PipelineLayoutCreateInfo,
                0n, VkPipelineLayoutCreateFlags.MinValue,
                uint32 descriptorSetLayouts.Length,
                pLayouts,
                0u,
                NativePtr.zero
            )

        let mutable pipelineLayout = VkPipelineLayout.Null
        VkRaw.vkCreatePipelineLayout(this.Handle, &&info, NativePtr.zero, &&pipelineLayout) |> check "vkCreatePipelineLayout"
        PipelineLayout(pipelineLayout, descriptorSetLayouts)

    [<Extension>]
    static member Delete(this : Device, pipelineLayout : PipelineLayout) =
        if pipelineLayout.Handle.IsValid then
            VkRaw.vkDestroyPipelineLayout(this.Handle, pipelineLayout.Handle, NativePtr.zero)
            pipelineLayout.Handle <- VkPipelineLayout.Null
            pipelineLayout.DescriptorSetLayouts <- [||]

    [<Extension>]
    static member CreateShaderProgram(this : Device, shaders : list<Shader>, renderPass : RenderPass) =
            
        let vs = shaders |> List.find (fun s -> s.Stage = VkShaderStageFlags.VertexBit)
        let fs = shaders |> List.find (fun s -> s.Stage = VkShaderStageFlags.FragmentBit)

        let uniforms = 
            shaders 
                |> List.collect (fun s -> s.Interface.uniforms |> List.map (fun p -> s.Stage, p))

        let images = 
            shaders 
                |> List.collect (fun s -> s.Interface.images |> List.map (fun p -> s.Stage, p))

        let inputs = 
            vs.Interface.inputs 
                |> List.filter (Parameter.tryGetBuiltInSemantic >> Option.isNone)
                |> List.sortBy (fun p -> match Parameter.tryGetLocation p with | Some loc -> loc | None -> failwithf "no explicit input location given for: %A" p)
                |> List.toArray

        let outputs =
            fs.Interface.outputs 
                |> List.filter (Parameter.tryGetBuiltInSemantic >> Option.isNone)
                |> List.sortBy (fun p -> match Parameter.tryGetLocation p with | Some loc -> loc | None -> failwithf "no explicit output location given for: %A" p)
                |> List.toArray



        // create DescriptorSetLayouts using the pipelines annotations
        // while using index -1 for non-annotated bindings
        let descriptorSetLayoutsMap = 
            [
                uniforms |> List.map (fun (a,b) -> VkDescriptorType.UniformBuffer, a, b)
                images |> List.map (fun (a,b) -> VkDescriptorType.CombinedImageSampler, a, b)
            ]
                |> List.concat

                // find identical parameter across stages
                |> Seq.groupBy (fun (dt,s,p) -> p)
                |> Seq.map (fun (p,instances) ->
                        let stages = instances |> Seq.fold (fun s (_,p,_) -> s ||| p) VkShaderStageFlags.None
                        let (dt,_,_) = instances |> Seq.head
                        (dt, stages, p)
                    )

                // group by assigned descriptor-set index (fail if none)
                |> Seq.groupBy (fun (dt, s, p) ->
                        match Parameter.tryGetDescriptorSet p with
                            | Some descSet -> descSet
                            | None -> 0
//                                    match dt with
//                                        | VkDescriptorType.CombinedImageSampler -> 0
//                                        | _ -> failwithf "no explicit DescriptorSet given for uniform: %A" p
                    )
                |> Seq.map (fun (g,s) -> g, Seq.toArray s)

                // create DescriptorLayoutBindings
                |> Seq.map (fun (setIndex,arr) ->
                        let bindings = 
                            arr |> Array.sortBy (fun (_,_,p) -> match Parameter.tryGetBinding p with | Some b -> b | _ -> 0)
                                |> Array.map (fun (a,b,c) -> ShaderProgramExtensions.CreateDescriptorSetLayoutBinding(this,a,b,c))

                        setIndex,bindings
                    )

                // create DescriptorSetLayouts
                |> Seq.map (fun (index,bindings) ->
                        index, ShaderProgramExtensions.CreateDescriptorSetLayout(this, bindings)
                    )
                |> Map.ofSeq

        // make the DescriptorSetLayouts dense by inserting NULL
        // where no bindings given and appending the "default" set at the end
        let maxIndex = 
            if Map.isEmpty descriptorSetLayoutsMap then -1
            else descriptorSetLayoutsMap |> Map.toSeq |> Seq.map fst |> Seq.max

        let descriptorSetLayouts =
            [|
                for i in 0..maxIndex do
                    match Map.tryFind i descriptorSetLayoutsMap with
                        | Some l -> yield l
                        | None -> 
                            warnf "found empty descriptor-set (index = %d) in ShaderProgram" i
                            yield ShaderProgramExtensions.CreateDescriptorSetLayout(this, [||])

            |]

        // create a pipeline layout from the given DescriptorSetLayouts
        let pipelineLayout = ShaderProgramExtensions.CreatePipelineLayout(this, descriptorSetLayouts)

        ShaderProgram(BackendSurface("", Dictionary.empty), shaders, renderPass, descriptorSetLayouts, pipelineLayout, inputs, outputs)


    [<Extension>]
    static member Delete(this : Device, prog : ShaderProgram) =
        if not (List.isEmpty prog.Shaders) then
            for d in prog.DescriptorSetLayouts do
                ShaderProgramExtensions.Delete(this, d)

            
            ShaderProgramExtensions.Delete(this, prog.PipelineLayout)
            prog.Shaders <- []
            prog.DescriptorSetLayouts <- [||]
            prog.PipelineLayout <- Unchecked.defaultof<_>
            prog.Inputs <- [||]
            prog.Outputs <- [||]



    [<Extension>]
    static member CreateShaderProgram(this : Device, runtime : IRuntime, s : ISurface, renderPass : RenderPass) =
        match s with
            | :? BackendSurface as s ->
                    
                let shaders = 
                    s.EntryPoints 
                        |> Dictionary.toList
                        |> List.map (fun (stage, entry) ->
                                let define =
                                    match stage with
                                        | ShaderStage.Vertex -> "Vertex"
                                        | ShaderStage.Pixel -> "Pixel"
                                        | ShaderStage.Geometry -> "Geometry"
                                        | ShaderStage.TessControl -> "TessControl"
                                        | ShaderStage.TessEval -> "TessEval"
                                        | _ -> failwithf "unsupported shader stage: %A" stage

                                let code = s.Code.Replace(sprintf "%s(" entry, "main(")
                                let code = versionRx.Replace(code, "#version 140\r\n" + (sprintf "#define %s\r\n" define))

                       

                                let res = ShaderProgramExtensions.CreateGLSLShader(this, code, stage)
                                res
                            )

                let res = ShaderProgramExtensions.CreateShaderProgram(this, shaders, renderPass)
                res.Surface <- s


                res

//            | :? Aardvark.SceneGraph.FShadeSceneGraph.FShadeSurface as f ->
//                let needed = renderPass.ColorAttachments |> Array.toList |> List.map (fun (s,a) -> s.ToString(), typeof<V4d>) |> Map.ofList
//                let result = f.Effect |> Aardvark.Rendering.SpirV.SpirVCompiler.compileEffect needed
//
//                match result with
//                    | Success result ->
//                        let shaders = 
//                            result.modules
//                                |> Map.toList
//                                |> List.map (fun (s,cs) ->
//                                    let m = cs.spirvModule
//                                    let stage =
//                                        match s with
//                                            | FShade.Types.ShaderType.Vertex -> AStage.Vertex
//                                            | FShade.Types.ShaderType.Fragment -> AStage.Pixel
//                                            | FShade.Types.ShaderType.Geometry _ -> AStage.Geometry
//                                            | FShade.Types.ShaderType.TessControl -> AStage.TessControl
//                                            | FShade.Types.ShaderType.TessEval -> AStage.TessEval
//
//
//
//
//                                    let code = Aardvark.Rendering.SpirV.InstructionPrinter.toString m.instructions
//                                    printfn "%s" code
//
//                                    x.CreateSpirVShader(cs, stage)
//
//                                )
//
//                        let res = x.CreateShaderProgram(shaders, renderPass)
//
//                        let semanticMap = SymDict.empty
//                        let samplerStates = SymDict.empty
//                        let uniforms = SymDict.empty
//                        let entries = Dictionary.empty
//                        for (s,cs) in Map.toList result.modules do
//                            let stage =
//                                match s with
//                                    | FShade.Types.ShaderType.Vertex -> AStage.Vertex
//                                    | FShade.Types.ShaderType.Fragment -> AStage.Pixel
//                                    | FShade.Types.ShaderType.Geometry _ -> AStage.Geometry
//                                    | FShade.Types.ShaderType.TessControl -> AStage.TessControl
//                                    | FShade.Types.ShaderType.TessEval -> AStage.TessEval
//
//
//                            entries.[stage] <- "main"
//                            for (u,v) in cs.uniforms do
//                                match u with
//                                    | FShade.Parameters.Uniforms.SamplerUniform(t,sem,name,state) ->
//                                        let sem = Symbol.Create sem
//                                        let name = Symbol.Create name
//
//                                        semanticMap.[name] <- sem
//                                        samplerStates.[sem] <- Aardvark.SceneGraph.FShadeSceneGraph.toSamplerStateDescription state
//
//                                        ()
//                                    | FShade.Parameters.Uniforms.UserUniform(t,(:? IMod as m)) ->
//                                        uniforms.[Symbol.Create v.Name] <- m
//                                    | _ ->
//                                        ()
//                                ()
//
//                        res.Surface <- BackendSurface("SPIRV", entries, uniforms, samplerStates, semanticMap)
//
//                        res
//                    | Error e ->
//                        failwith e




            | :? IGeneratedSurface as g ->
                let bs = g.Generate(runtime, renderPass)
                let res = ShaderProgramExtensions.CreateShaderProgram(this, runtime, bs, renderPass)
                res.Surface <- bs
                res

            | _ ->
                failwithf "unsupported surface type: %A" s


