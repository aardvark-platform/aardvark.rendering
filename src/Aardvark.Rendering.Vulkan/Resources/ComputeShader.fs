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
        val mutable public GroupSize : V3i

        interface IComputeShader with
            member x.LocalSize = x.GroupSize


        new(d,s,l,p,tn,sd,gs) = { Device = d; ShaderModule = s; Layout = l; Handle = p; TextureNames = tn; Samplers = sd; GroupSize = gs }
    end

type BindingReference =
    | UniformRef of buffer : UniformBuffer * offset : int * valueType : UniformType
    | StorageBufferRef of set : int * binding : int * elementType : UniformType
    | SampledImageRef of set : int * binding : int * index : int * info : ShaderSamplerType * sampler : Sampler
    | StorageImageRef of set : int * binding : int * info : ShaderSamplerType

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

                | StorageImageRef(set, binding, info) ->
                    let view, res = 
                        match value with
                            | :? Image as img -> 
                                let view = device.CreateOutputImageView(img, 0, 1, 0, 1)
                                view, Some { new IDisposable with member x.Dispose() = device.Delete view }

                            | :? ImageView as view ->
                                view, None

                            | :? ImageSubresourceRange as r ->
                                let view = device.CreateOutputImageView(r.Image, r.BaseLevel, r.LevelCount, r.BaseSlice, r.SliceCount)
                                view, Some { new IDisposable with member x.Dispose() = device.Delete view }

                            | _ -> 
                                failf "invalid storage image argument: %A" value

                    let write = Descriptor.StorageImage(binding, view)
                    update set binding write
                    setResource set binding 0 res

                | SampledImageRef(set, binding, index, info, sampler) ->
                    let content = imageArrays.[(set, binding)]
                    let res = 
                        match value with
                            | null ->
                                content.[index] <- None
                                None

                            | :? ITexture as tex ->
                                let image = device.CreateImage tex
                                let view = device.CreateInputImageView(image, info, VkComponentMapping.Identity)
                                content.[index] <- Some (view, sampler)
                                Some { new IDisposable with member x.Dispose() = device.Delete image; device.Delete view }

                            | _ -> 
                                failf "invalid storage image argument: %A" value

                    
                    let write = Descriptor.CombinedImageSampler(binding, content)
                    update set binding write
                    setResource set binding index res

                | StorageBufferRef(set, binding, elementType) ->
                    let buffer,offset,size,res =
                        match value with
                            | :? IBuffer as b -> 
                                let buffer = device.CreateBuffer(VkBufferUsageFlags.TransferSrcBit ||| VkBufferUsageFlags.TransferDstBit ||| VkBufferUsageFlags.StorageBufferBit, b)
                                buffer, 0L, buffer.Size, Some { new IDisposable with member x.Dispose() = device.Delete buffer }

                            | :? IBufferRange as b ->
                                let buffer = b.Buffer.Handle |> unbox<Buffer>
                                buffer, int64 b.Offset, int64 b.Size, None

                            | _ -> 
                                failf "unexpected storage buffer %A" value

                    let write = Descriptor.StorageBuffer(binding, buffer, offset, size)
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

    let uploadCommand (buffers : list<UniformBuffer>) =
        match buffers with
            | [] -> Command.Nop
            | _ -> 
                { new Command() with
                    member x.Compatible = QueueFlags.All
                    member x.Enqueue cmd = 
                        cmd.AppendCommand()
                        for b in buffers do
                            VkRaw.vkCmdUpdateBuffer(cmd.Handle, b.Handle, 0UL, uint64 b.Storage.Size, b.Storage.Pointer)
                        Disposable.Empty
                }

    member private x.AsString =
        references 
            |> Map.toSeq 
            |> Seq.map (fun (name, rs) ->
                match rs with
                    | UniformRef(_,_,t) :: _ -> sprintf "%s : %s" name (prettyName t)
                    | StorageImageRef(_,_,_) :: _ -> sprintf "%s : image" name
                    | SampledImageRef(_,_,_,_,_) :: _ -> sprintf "%s : sampler" name
                    | StorageBufferRef(_,_,et) :: _ -> sprintf "%s : buffer<%s>" name (prettyName et)
                    | _ -> sprintf "%s : unknown" name
            )
            |> String.concat "; "
            |> sprintf "{ %s }"
        

    override x.ToString() = x.AsString

    member x.Dispose() = release()

   

    member x.GetWriter<'a>(name : string) =
        match Map.tryFind name references with
            | Some refs ->
                let buffers = System.Collections.Generic.List<UniformBuffer>()
                let writers = 
                    refs |> List.choose (fun r ->
                        match r with
                            | BindingReference.UniformRef(buffer, offset, valueType) ->
                                let w = UniformWriters.getWriter offset valueType typeof<'a> |> unbox<UniformWriters.IWriter<'a>>
                                buffers.Add buffer

                                let write (value : 'a) =
                                    w.WriteValue(value, buffer.Storage.Pointer)

                                Some write
                            | _ ->
                                None
                    )

                let cmd = uploadCommand (CSharpList.toList buffers)

                let write (value : 'a) =
                    writers |> List.iter (fun w -> w value)
                cmd, write
            | None ->
                Command.Nop, ignore
    member x.References = references
    member x.Device = device
    member x.DescriptorPool = pool
    member x.Set(ref : BindingReference, value : obj) = write ref value
    member x.Set(name : string, value : obj) = 
        match Map.tryFind name references with
            | Some refs -> for r in refs do write r value
            | None -> () //failf "invalid reference %A" name

    member x.Item
        with set (name : string) (value : obj) = x.Set(name, value)

    member x.Item
        with set (ref : BindingReference) (value : obj) = x.Set(ref, value)

    member x.Flush() = flush()
    member x.Bind = bind

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    interface IComputeShaderInputBinding with
        member x.Item 
            with set (name : string) (value : obj) = x.[name] <- value

        member x.Flush() =
            x.Flush()

[<AutoOpen>]
module ``Compute Commands`` =
    
    type Command with

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
        static member DispatchIndirect (b : Buffer) =
            { new Command() with
                override x.Compatible = QueueFlags.Compute
                override x.Enqueue(cmd) =
                    
                    cmd.AppendCommand()
                    VkRaw.vkCmdDispatchIndirect(cmd.Handle, b.Handle, 0UL)

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

    let ofFShade (shader : FShade.ComputeShader) (device : Device) =
        let glsl = shader |> FShade.ComputeShader.toModule |> ModuleCompiler.compileGLSLVulkan
        
        ShaderProgram.logLines glsl.code

        let localSize =
            if shader.csLocalSize.AllGreater 0 then shader.csLocalSize
            else V3i.III

        let sm = ShaderModule.ofGLSL ShaderStage.Compute glsl.code device

        match sm.TryGetShader ShaderStage.Compute with
            | (true, shaderInfo) ->
                let layout =
                    device.CreatePipelineLayout([| shaderInfo |], 1, Set.empty)

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
                

                ComputeShader(device, sm, layout, handle, shader.csTextureNames, samplers, shader.csLocalSize)
            | _ ->
                failf "could not create compute shader"

    let ofFunction (f : 'a -> 'b) (device : Device) =
        let shader = FShade.ComputeShader.ofFunction device.PhysicalDevice.Limits.Compute.MaxWorkGroupSize f
        ofFShade shader device



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
                                    let reference = SampledImageRef(si, bi, i, img.samplerType, sampler)
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

                        let reference = StorageImageRef(si, bi, i.samplerType)
                        references.[name] <- [reference]
                        //descriptors.[bi] <- Descriptor.StorageImage(bi, Unchecked.defaultof<_>)

                    | Other -> ()

            pool.Update(set, CSharpList.toArray descriptors)

            sets.[si] <- set

        new InputBinding(pool, shader, sets, Dict.toMap references, imageArrays, buffers)