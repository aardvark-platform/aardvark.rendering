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

module VkShaderStageFlags =
    let ofShaderStage =
        LookupTable.lookupTable [
            ShaderStage.Vertex, VkShaderStageFlags.VertexBit
            ShaderStage.TessControl, VkShaderStageFlags.TessellationControlBit
            ShaderStage.TessEval, VkShaderStageFlags.TessellationEvaluationBit
            ShaderStage.Geometry, VkShaderStageFlags.GeometryBit
            ShaderStage.Pixel, VkShaderStageFlags.FragmentBit
        ]

type PipelineLayout =
    class
        inherit Resource<VkPipelineLayout>

        val mutable public DescriptorSetLayouts : list<DescriptorSetLayout>

        new(device, handle, descriptorSetLayouts) = { inherit Resource<_>(device, handle); DescriptorSetLayouts = descriptorSetLayouts }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module PipelineLayout =
    let create (descriptors : list<DescriptorSetLayout>) (device : Device) =
        let arr = descriptors |> List.map (fun d -> d.Handle) |> List.toArray
        arr |> NativePtr.withA (fun pArr ->
            let mutable info =
                VkPipelineLayoutCreateInfo(
                    VkStructureType.PipelineLayoutCreateInfo, 0n,
                    VkPipelineLayoutCreateFlags.MinValue,
                    uint32 arr.Length, pArr,
                    0u, NativePtr.zero
                )
            let mutable handle = VkPipelineLayout.Null
            VkRaw.vkCreatePipelineLayout(device.Handle, &&info, NativePtr.zero, &&handle)
                |> check "could not create PipelineLayout"
            
            PipelineLayout(device, handle, descriptors)
        )

    let ofShaderModules (shaders : list<ShaderModule>) (device : Device) =
        let vs = shaders |> List.find (fun s -> s.Stage = ShaderStage.Vertex)
        let fs = shaders |> List.find (fun s -> s.Stage = ShaderStage.Pixel)

        let uniforms = 
            shaders 
                |> List.collect (fun s -> s.Interface.uniforms |> List.map (fun p -> VkShaderStageFlags.ofShaderStage s.Stage, p))

        let images = 
            shaders 
                |> List.collect (fun s -> s.Interface.images |> List.map (fun p -> VkShaderStageFlags.ofShaderStage s.Stage, p))

        let inputs = 
            vs.Interface.inputs 
                |> List.filter (ShaderParameter.tryGetBuiltInSemantic >> Option.isNone)
                |> List.sortBy (fun p -> match ShaderParameter.tryGetLocation p with | Some loc -> loc | None -> failwithf "no explicit input location given for: %A" p)
                |> List.toArray

        let outputs =
            fs.Interface.outputs 
                |> List.filter (ShaderParameter.tryGetBuiltInSemantic >> Option.isNone)
                |> List.sortBy (fun p -> match ShaderParameter.tryGetLocation p with | Some loc -> loc | None -> failwithf "no explicit output location given for: %A" p)
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
                        match ShaderParameter.tryGetDescriptorSet p with
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
                            arr |> Array.sortBy (fun (_,_,p) -> match ShaderParameter.tryGetBinding p with | Some b -> b | _ -> 0)
                                |> Array.map (fun (a,b,c) -> device |> DescriptorSetLayoutBinding.create a b c)
                                |> Array.toList

                        setIndex,bindings
                    )

                // create DescriptorSetLayouts
                |> Seq.map (fun (index,bindings) ->
                        index, device |> DescriptorSetLayout.create bindings
                    )
                |> Map.ofSeq

        // make the DescriptorSetLayouts dense by inserting NULL
        // where no bindings given and appending the "default" set at the end
        let maxIndex = 
            if Map.isEmpty descriptorSetLayoutsMap then -1
            else descriptorSetLayoutsMap |> Map.toSeq |> Seq.map fst |> Seq.max

        let descriptorSetLayouts =
            [
                for i in 0..maxIndex do
                    match Map.tryFind i descriptorSetLayoutsMap with
                        | Some l -> yield l
                        | None -> 
                            VkRaw.warn "found empty descriptor-set (index = %d) in ShaderProgram" i
                            yield device |> DescriptorSetLayout.create []

            ]

        // create a pipeline layout from the given DescriptorSetLayouts
        let pipelineLayout = device |> create descriptorSetLayouts

        
        pipelineLayout

    let delete (layout : PipelineLayout) (device : Device) =
        if layout.Handle.IsValid then
            for b in layout.DescriptorSetLayouts do
                device |> DescriptorSetLayout.delete b

            VkRaw.vkDestroyPipelineLayout(device.Handle, layout.Handle, NativePtr.zero)
            layout.Handle <- VkPipelineLayout.Null
            layout.DescriptorSetLayouts <- []

[<AbstractClass; Sealed; Extension>]
type ContextPipelineLayoutExtensions private() =
    [<Extension>]
    static member inline CreatePipelineLayout(this : Device, bindings : list<ShaderModule>) =
        this |> PipelineLayout.ofShaderModules bindings

    [<Extension>]
    static member inline Delete(this : Device, layout : PipelineLayout) =
        this |> PipelineLayout.delete layout       