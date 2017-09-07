namespace Aardvark.Rendering.Vulkan

open FShade
open FShade.Imperative
open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop
open System.Collections.Generic

#nowarn "9"
#nowarn "51"


type ComputeShader =
    class
        val mutable public Device : Device
        val mutable public ShaderModule : ShaderModule
        val mutable public Layout : PipelineLayout
        val mutable public Handle : VkPipeline
        val mutable public TextureNames : Map<string * int, string>
        val mutable public Samplers : Map<string * int, Sampler>
        new(d,s,l,p,tn,sd) = { Device = d; ShaderModule = s; Layout = l; Handle = p; TextureNames = tn; Samplers = sd }
    end

type ComputeShaderInputBinding(pool : DescriptorPool, shader : ComputeShader, sets : DescriptorSet[], storageBuffers : Dictionary<string, Buffer>, images : Dictionary<string, Image>, release : unit -> unit) =
    let device = pool.Device


    let setHandles = NativePtr.alloc sets.Length
    do for i in 0 .. sets.Length - 1 do NativePtr.set setHandles i sets.[i].Handle

    member x.Bind =
        { new Command() with
            override x.Compatible = QueueFlags.All
            override x.Enqueue(cmd : CommandBuffer) =
                cmd.AppendCommand()
                VkRaw.vkCmdBindDescriptorSets(cmd.Handle, VkPipelineBindPoint.Compute, shader.Layout.Handle, 0u, uint32 sets.Length, setHandles, 0u, NativePtr.zero)
                Disposable.Empty
        }

    member x.TryGetBuffer(name : string) =
        match storageBuffers.TryGetValue name with
            | (true, b) -> Some b
            | _ -> None
        

    member x.Dispose() =
        for s in sets do pool.Free s
        release()
        for b in storageBuffers.Values do device.Delete b
        for i in images.Values do device.Delete i
        storageBuffers.Clear()
        images.Clear()

        NativePtr.free setHandles

    interface IDisposable with
        member x.Dispose() = x.Dispose()


type BindingReference =
    | UniformRef of buffer : UniformBuffer * offset : int * valueType : UniformType
    | StorageBufferRef of set : int * binding : int * elementType : UniformType
    | SampledImageRef of set : int * binding : int * index : int * sampler : Sampler
    | StorageImageRef of set : int * binding : int * format : Option<TextureFormat>

[<StructuredFormatDisplay("{AsString}")>]
type InputBinding(pool : DescriptorPool, shader : ComputeShader, sets : DescriptorSet[], references : Map<string, list<BindingReference>>, imageArrays : MapExt<int * int, Option<ImageView * Sampler>[]>, buffers : List<UniformBuffer>) =
    
    
    static let rec prettyPrimitive (t : PrimitiveType) =
        match t with
            | PrimitiveType.Bool -> "bool"
            | PrimitiveType.Float(32) -> "float"
            | PrimitiveType.Float(64) -> "double"
            | PrimitiveType.Float(w) -> sprintf "float%d" w
            | PrimitiveType.Int(32, true) -> "int"
            | PrimitiveType.Int(w, true) -> sprintf "int%d" w
            | PrimitiveType.Int(32, false) -> "uint"
            | PrimitiveType.Int(w, false) -> sprintf "uint%d" w
            | PrimitiveType.Vector(et, dim) -> sprintf "%s%d" (prettyPrimitive et) dim
            | PrimitiveType.Matrix(et, dim) -> sprintf "%sx%d" (prettyPrimitive et) dim

    static let rec prettyName (t : UniformType) =
        match t with
            | Primitive(t,_,_) -> prettyPrimitive t
            | Array(et,len,_,_) -> sprintf "%s[%d]" (prettyName et) len
            | RuntimeArray(et,_,_) -> sprintf "%s[]" (prettyName et) 
            | Struct layout -> 
                layout.fields 
                    |> List.map (fun f -> sprintf "%s : %s" f.name (prettyName f.fieldType))
                    |> String.concat "; "
                    |> sprintf "struct { %s }"

    
    let device = pool.Device
    let lockObj = obj()
    let mutable disposables : MapExt<int * int * int, IDisposable> = MapExt.empty
    let mutable dirtyBuffers = HSet.empty
    let mutable pendingWrites = MapExt.empty

    let setHandles = NativePtr.alloc sets.Length
    do for i in 0 .. sets.Length - 1 do NativePtr.set setHandles i sets.[i].Handle

    let bind =
        { new Command() with
            override x.Compatible = QueueFlags.All
            override x.Enqueue(cmd : CommandBuffer) =
                cmd.AppendCommand()
                VkRaw.vkCmdBindDescriptorSets(cmd.Handle, VkPipelineBindPoint.Compute, shader.Layout.Handle, 0u, uint32 sets.Length, setHandles, 0u, NativePtr.zero)
                Disposable.Empty
        }


    let update (set : int) (binding : int) (d : Descriptor) =
        pendingWrites <-
            pendingWrites |> MapExt.alter set (fun o ->
                match o with
                    | None -> MapExt.ofList [binding, d] |> Some
                    | Some o -> MapExt.add binding d o |> Some
            )

    let setResource (set : int) (binding : int) (index : int) (d : Option<IDisposable>) =
        disposables <- 
            disposables |> 
                MapExt.alter (set, binding, index) (fun old ->
                    match old with
                        | Some o -> o.Dispose()
                        | _ -> ()

                    d
                )

    let write (ref : BindingReference) (value : obj) =
        lock lockObj (fun () ->
            match ref with
                | UniformRef(buffer, offset, targetType) ->
                    let w = UniformWriters.getWriter offset targetType (value.GetType())
                    w.WriteUnsafeValue(value, buffer.Storage.Pointer)
                    dirtyBuffers <- HSet.add buffer dirtyBuffers

                | StorageImageRef(set, binding, format) ->

                    let view, res = 
                        match value with
                            | :? Image as img -> 
                                let view = device.CreateImageView(img, 0, 1, 0, 1, VkComponentMapping.Identity)
                                view, Some { new IDisposable with member x.Dispose() = device.Delete view }

                            | :? ImageView as view ->
                                view, None

                            | :? ImageSubresourceRange as r ->
                                let view = device.CreateImageView(r.Image, r.BaseLevel, r.LevelCount, r.BaseSlice, r.SliceCount, VkComponentMapping.Identity)
                                view, Some { new IDisposable with member x.Dispose() = device.Delete view }

                            | _ -> 
                                failf "invalid storage image argument: %A" value

                    let write = Descriptor.StorageImage(binding, view)
                    update set binding write
                    setResource set binding 0 res

                | SampledImageRef(set, binding, index, sampler) ->
                    let content = imageArrays.[(set, binding)]
                    let res = 
                        match value with
                            | null ->
                                content.[index] <- None
                                None

                            | :? ITexture as tex ->
                                let image = device.CreateImage tex
                                let view = device.CreateImageView(image, VkComponentMapping.Identity)
                                content.[index] <- Some (view, sampler)
                                Some { new IDisposable with member x.Dispose() = device.Delete image; device.Delete view }

                            | _ -> 
                                failf "invalid storage image argument: %A" value

                    
                    let write = Descriptor.CombinedImageSampler(binding, content)
                    update set binding write
                    setResource set binding index res

                | StorageBufferRef(set, binding, elementType) ->
                    let buffer,res =
                        match value with
                            | :? IBuffer as b -> 
                                let buffer = device.CreateBuffer(VkBufferUsageFlags.TransferSrcBit ||| VkBufferUsageFlags.TransferDstBit ||| VkBufferUsageFlags.StorageBufferBit, b)
                                buffer, Some { new IDisposable with member x.Dispose() = device.Delete buffer }
                            | _ -> 
                                failf "unexpected storage buffer %A" value

                    let write = Descriptor.StorageBuffer(binding, buffer)
                    update set binding write
                    setResource set binding 0 res
        )

    let flush() =
        lock lockObj (fun () ->
            let buffers = Interlocked.Exchange(&dirtyBuffers, HSet.empty)
            let writes = Interlocked.Exchange(&pendingWrites, MapExt.empty)

            if not (HSet.isEmpty buffers) then
                use token = device.Token
                for b in buffers do device.Upload b
                token.Sync()

            for (set, desc) in MapExt.toSeq writes do   
                let values = desc |> MapExt.toSeq |> Seq.map snd |> Seq.toArray
                pool.Update(sets.[set], values)
        )

    let release() =
        lock lockObj (fun () ->
            dirtyBuffers <- HSet.empty
            pendingWrites <- MapExt.empty
            for (_,d) in MapExt.toSeq disposables do d.Dispose()
            for b in buffers do device.Delete b
            buffers.Clear()
            disposables <- MapExt.empty
            for s in sets do pool.Free s
            NativePtr.free setHandles
        )   

    member private x.AsString =
        references 
            |> Map.toSeq 
            |> Seq.map (fun (name, rs) ->
                match rs with
                    | UniformRef(_,_,t) :: _ -> sprintf "%s : %s" name (prettyName t)
                    | StorageImageRef(_,_,Some fmt) :: _ -> sprintf "%s : image<%A>" name fmt
                    | StorageImageRef(_,_,None) :: _ -> sprintf "%s : image" name
                    | SampledImageRef(_,_,_,_) :: _ -> sprintf "%s : sampler" name
                    | StorageBufferRef(_,_,et) :: _ -> sprintf "%s : buffer<%s>" name (prettyName et)
                    | _ -> sprintf "%s : unknown" name
            )
            |> String.concat "; "
            |> sprintf "{ %s }"
        

    override x.ToString() = x.AsString

    member x.Dispose() = release()

    member x.References = references
    member x.Device = device
    member x.DescriptorPool = pool
    member x.Set(ref : BindingReference, value : obj) = write ref value
    member x.Set(name : string, value : obj) = 
        match Map.tryFind name references with
            | Some refs -> for r in refs do write r value
            | None -> failf "invalid reference %A" name

    member x.Item
        with set (name : string) (value : obj) = x.Set(name, value)

    member x.Item
        with set (ref : BindingReference) (value : obj) = x.Set(ref, value)

    member x.Flush() = flush()
    member x.Bind = bind

    interface IDisposable with
        member x.Dispose() = x.Dispose()


[<AutoOpen>]
module ``Compute Commands`` =
    
    type Command with
        static member SetInputs (binding : ComputeShaderInputBinding) =
            binding.Bind

        static member SetInputs (binding : InputBinding) =
            binding.Bind

        static member Bind (shader : ComputeShader) =
            { new Command() with
                override x.Compatible = QueueFlags.Compute
                override x.Enqueue(cmd) =
                    
                    cmd.AppendCommand()
                    VkRaw.vkCmdBindPipeline(cmd.Handle, VkPipelineBindPoint.Compute, shader.Handle)

                    Disposable.Empty
            }

        static member Dispatch (size : V3i) =
            if size.AnySmallerOrEqual 0 then
                Command.Nop
            else
                { new Command() with
                    override x.Compatible = QueueFlags.Compute
                    override x.Enqueue(cmd) =
                    
                        cmd.AppendCommand()
                        VkRaw.vkCmdDispatch(cmd.Handle, uint32 size.X, uint32 size.Y, uint32 size.Z)

                        Disposable.Empty
                }
            
        static member Dispatch (size : V2i) = Command.Dispatch(V3i(size.X, size.Y, 1))

        static member Dispatch (sizeX : int) = Command.Dispatch(V3i(sizeX, 1, 1))

        static member Dispatch (sizeX : int, sizeY : int, sizeZ : int) = Command.Dispatch(V3i(sizeX, sizeY, sizeZ))
        static member Dispatch (sizeX : int, sizeY : int) = Command.Dispatch(V3i(sizeX, sizeY, 1))
            
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ComputeShader =  
    let private main = CStr.malloc "main"

    let delete (shader : ComputeShader) =
        let device = shader.Device
        if shader.Handle.IsValid then

            VkRaw.vkDestroyPipeline(device.Handle, shader.Handle, NativePtr.zero)

            for (_,s) in Map.toSeq shader.Samplers do device.Delete s
            device.Delete shader.Layout
            device.Delete shader.ShaderModule

            shader.ShaderModule <- Unchecked.defaultof<_>
            shader.Layout <- Unchecked.defaultof<_>
            shader.Handle <- VkPipeline.Null
            shader.TextureNames <- Map.empty
            shader.Samplers <- Map.empty

    let ofFunction (f : 'a -> 'b) (device : Device) =
        let shader = FShade.ComputeShader.ofFunction device.PhysicalDevice.Limits.Compute.MaxWorkGroupSize f
        let glsl = shader |> FShade.ComputeShader.toModule |> ModuleCompiler.compileGLSLVulkan

        printfn "%s" glsl.code

        let localSize =
            if shader.csLocalSize.AllGreater 0 then shader.csLocalSize
            else V3i.III

        let sm = ShaderModule.ofGLSL ShaderStage.Compute glsl.code device

        match sm.TryGetShader ShaderStage.Compute with
            | (true, shaderInfo) ->
                let layout =
                    device.CreatePipelineLayout [| shaderInfo |]

                let shaderInfo =
                    VkPipelineShaderStageCreateInfo(
                        VkStructureType.PipelineShaderStageCreateInfo, 0n,
                        VkPipelineShaderStageCreateFlags.MinValue,
                        VkShaderStageFlags.ComputeBit,
                        sm.Handle,
                        main,
                        NativePtr.zero
                    )

                let mutable pipelineInfo =
                    VkComputePipelineCreateInfo(
                        VkStructureType.ComputePipelineCreateInfo, 0n,
                        VkPipelineCreateFlags.None,
                        shaderInfo,
                        layout.Handle,
                        VkPipeline.Null,
                        0
                    )

                let mutable handle = VkPipeline.Null
                VkRaw.vkCreateComputePipelines(device.Handle, VkPipelineCache.Null, 1u, &&pipelineInfo, NativePtr.zero, &&handle)
                    |> check "could not create compute pipeline"

               
                let samplers =
                    shader.csSamplerStates |> Map.map (fun _ s -> device.CreateSampler s.SamplerStateDescription)
                

                ComputeShader(device, sm, layout, handle, shader.csTextureNames, samplers)
            | _ ->
                failf "could not create compute shader"

    let createInputBinding (tryGetValue : string -> Option<obj>) (shader : ComputeShader) (pool : DescriptorPool) =
        let device = shader.Device
        let storageBuffers  = Dictionary<string, Buffer>()
        let images          = Dictionary<string, Image>()
        let imageViews      = List<ImageView>()
        let uniformBuffers  = List<UniformBuffer>()
        
        use token = device.Token

        let sets = 
            shader.Layout.DescriptorSetLayouts |> Array.map (fun dsl ->
                let set = pool.Alloc(dsl)

                let bindings = 
                    dsl.Bindings |> Array.map (fun b ->
                        match b.Parameter with
                            | UniformBlockParameter block ->
                                match b.DescriptorType with
                                    | VkDescriptorType.UniformBuffer ->
                                        let buffer = device.CreateUniformBuffer(block.layout)
                                        uniformBuffers.Add buffer

                                        for field in block.layout.fields do
                                            let name =
                                                if field.name.StartsWith "cs_" then field.name.Substring 3
                                                else field.name

                                            match tryGetValue name with
                                                | Some o ->
                                                    let writer = UniformWriters.getWriter field.offset field.fieldType (o.GetType())
                                                    writer.WriteUnsafeValue(o, buffer.Storage.Pointer)

                                                | None ->
                                                    failf "no value given for uniform %A" field.name

                                        device.Upload buffer
                                        Descriptor.UniformBuffer(b.Binding, buffer)

                                    | VkDescriptorType.StorageBuffer ->
                                        match block.layout.fields with
                                            | [ field ] ->
                                                match tryGetValue field.name with
                                                    | Some o ->
                                                        match o with
                                                            | :? IBuffer as o ->
                                                                match storageBuffers.TryGetValue field.name with
                                                                    | (true, buffer) -> 
                                                                        Descriptor.StorageBuffer(b.Binding, buffer)
                                                                    | _ -> 
                                                                        let o = device.CreateBuffer(VkBufferUsageFlags.TransferSrcBit ||| VkBufferUsageFlags.TransferDstBit ||| VkBufferUsageFlags.StorageBufferBit, o)
                                                                        storageBuffers.[field.name] <- o
                                                                        Descriptor.StorageBuffer(b.Binding, o)

                                                            | _ ->
                                                                failf "unupported storage buffer argument %A" o
                                                                

                                                    | None ->
                                                        failf "no value given for uniform %A" field.name
                                                
                                            | _ ->
                                                failf "storage buffers cannot contain multiple fields atm."

                                    | t ->
                                        failf "unsupported compute shader input: %A" t

                            | ImageParameter imgInfo when imgInfo.isSampled ->
                                let samplerName = b.Name

                                let viewsAndSamplers = 
                                    Array.init imgInfo.count (fun i ->
                                        let key = (samplerName, i)
                                        match Map.tryFind key shader.TextureNames with
                                            | Some textureName ->
                                                match tryGetValue textureName with
                                                    | Some value ->
                                                        let sampler = shader.Samplers.[key]

                                                        let image = 
                                                            match value with
                                                                | :? ITexture as t ->
                                                                    match images.TryGetValue textureName with
                                                                        | (true, img) -> img
                                                                        | _ -> 
                                                                            let img = device.CreateImage(t)
                                                                            images.[textureName] <- img
                                                                            img
                                                                | _ ->
                                                                    failf "unsupported image type %A" value
                                                        
                                                        let imageView =
                                                            device.CreateImageView(image, VkComponentMapping.Identity)

                                                        imageViews.Add(imageView)

                                                        Some(imageView, sampler)


                                                    | None ->
                                                        None
                                            | None ->
                                                None
                                    )

                                Descriptor.CombinedImageSampler(b.Binding, viewsAndSamplers)

                            | ImageParameter imgInfo ->
                                
                                let name =
                                    if b.Name.StartsWith "cs_" then b.Name.Substring 3
                                    else b.Name

                                match tryGetValue name with
                                    | Some value ->
                                        
                                        match value with
                                            | :? Image as img ->
                                                let view = device.CreateImageView(img, 0, 1, 0, 1, VkComponentMapping.Identity)
                                                imageViews.Add view
                                                Descriptor.StorageImage(b.Binding, view)

                                            | :? ImageView as view ->
                                                Descriptor.StorageImage(b.Binding, view)
                                                
                                            | :? ImageSubresourceRange as r ->
                                                let view = device.CreateImageView(r.Image, r.BaseLevel, r.LevelCount, r.BaseSlice, r.SliceCount, VkComponentMapping.Identity)
                                                imageViews.Add view
                                                Descriptor.StorageImage(b.Binding, view)

                                            | _ ->
                                                failf "unexpected storage image: %A" value


                                    | None -> 
                                        failf "no image found with name %A" name

                    )

                pool.Update(set, bindings)

                set

            )

        token.Sync()


        let dispose() =
            for view in imageViews do device.Delete view
            for ub in uniformBuffers do device.Delete ub

        new ComputeShaderInputBinding(pool, shader, sets, storageBuffers, images, dispose)


    let private (|UniformBufferBinding|StorageBufferBinding|SampledImageBinding|StorageImageBinding|Other|) (b : DescriptorSetLayoutBinding) =
        match b.DescriptorType with
            | VkDescriptorType.UniformBuffer ->
                match b.Parameter with
                    | UniformBlockParameter p -> UniformBufferBinding p.layout
                    | _ -> Other
            | VkDescriptorType.StorageBuffer ->
                match b.Parameter with
                    | UniformBlockParameter p ->
                        match p.layout.fields with
                            | [f] -> StorageBufferBinding(f.name, f.fieldType)
                            | _ -> Other
                    | _ ->
                        Other

            | VkDescriptorType.CombinedImageSampler ->
                match b.Parameter with
                    | ImageParameter i -> SampledImageBinding i
                    | _ -> Other

            | VkDescriptorType.StorageImage ->
                match b.Parameter with
                    | ImageParameter i -> StorageImageBinding i
                    | _ -> Other

            | _ ->
                Other

    let newInputBinding (shader : ComputeShader) (pool : DescriptorPool) =
        let device = shader.Device
        let references = Dict<string, list<BindingReference>>()
        let setLayouts = shader.Layout.DescriptorSetLayouts
        let sets = Array.zeroCreate setLayouts.Length

        let buffers = List<UniformBuffer>()
        let mutable imageArrays : MapExt<int * int, Option<ImageView * Sampler>[]> = MapExt.empty

        for si in 0 .. setLayouts.Length - 1 do
            let setLayout = setLayouts.[si]
            let set = pool.Alloc setLayout

            let descriptors = List()

            for bi in 0 .. setLayout.Bindings.Length - 1 do
                let binding = setLayout.Bindings.[bi]
                match binding with
                    | UniformBufferBinding layout ->
                        let buffer = device.CreateUniformBuffer layout
                        buffers.Add buffer

                        for field in layout.fields do
                            let name = 
                                if field.name.StartsWith "cs_" then field.name.Substring 3
                                else field.name
                            
                            let reference = UniformRef(buffer, field.offset, field.fieldType)
                            references.[name] <- [reference]
                            
                        descriptors.Add (Descriptor.UniformBuffer(bi, buffer))

                    | StorageBufferBinding (name, elementType) ->
                        let reference = StorageBufferRef(si, bi, elementType)
                        references.[name] <- [reference]
                        //descriptors.[bi] <- Descriptor.StorageBuffer(bi, Buffer(device, VkBuffer.Null, DevicePtr.Null))

                    | SampledImageBinding img ->
                        let name = img.name
                        let images : Option<ImageView * Sampler>[] = Array.zeroCreate img.count 
                        for i in 0 .. img.count - 1 do
                            match Map.tryFind (name, i) shader.Samplers, Map.tryFind (name, i) shader.TextureNames with
                                | Some sampler, Some texName ->
                                    let reference = SampledImageRef(si, bi, i, sampler)
                                    let old = references.GetOrCreate(texName, fun _ -> [])
                                    references.[texName] <- reference :: old
                                | _ ->
                                    ()
                        imageArrays <- MapExt.add (si, bi) images imageArrays
                        //descriptors.[bi] <- Descriptor.CombinedImageSampler(bi, images)

                    | StorageImageBinding i ->
                        let name = 
                            if i.name.StartsWith "cs_" then i.name.Substring 3
                            else i.name

                        let reference = StorageImageRef(si, bi, i.format)
                        references.[name] <- [reference]
                        //descriptors.[bi] <- Descriptor.StorageImage(bi, Unchecked.defaultof<_>)

                    | Other -> ()

            pool.Update(set, CSharpList.toArray descriptors)

            sets.[si] <- set

        new InputBinding(pool, shader, sets, Dict.toMap references, imageArrays, buffers)