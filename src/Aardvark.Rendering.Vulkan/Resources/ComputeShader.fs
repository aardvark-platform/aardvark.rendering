namespace Aardvark.Rendering.Vulkan

open FShade
open FShade.Imperative
open System
open System.Threading
open System.Runtime.InteropServices
open Aardvark.Base

open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop
open System.Collections.Generic
open Microsoft.FSharp.Core.LanguagePrimitives.IntrinsicOperators
open FSharp.Data.Adaptive

#nowarn "9"
// #nowarn "51"


type ComputeShader =
    class
        inherit RefCountedResource

        val mutable public Device : Device
        val mutable public ShaderModule : ShaderModule
        val mutable public Layout : PipelineLayout
        val mutable public Handle : VkPipeline
        val mutable public TextureNames : Map<string * int, string>
        val mutable public Samplers : Map<string * int, Sampler>
        val mutable public GroupSize : V3i
        val mutable public CacheName : Symbol
        val mutable public Interface : FShade.GLSL.GLSLShaderInterface
        val mutable public GLSL : Option<string>

        interface IComputeShader with
            member x.Runtime = x.Device.Runtime :> IComputeRuntime
            member x.LocalSize = x.GroupSize

        override x.Destroy() =
            let device = x.Device
            VkRaw.vkDestroyPipeline(device.Handle, x.Handle, NativePtr.zero)

            for (_,s) in Map.toSeq x.Samplers do device.Delete s
            device.Delete x.Layout
            device.Delete x.ShaderModule

            x.ShaderModule <- Unchecked.defaultof<_>
            x.Layout <- Unchecked.defaultof<_>
            x.Handle <- VkPipeline.Null
            x.TextureNames <- Map.empty
            x.Samplers <- Map.empty


        new(d,s : ShaderModule,l,p,tn,sd,gs,glsl) = { inherit RefCountedResource(); Device = d; ShaderModule = s; Layout = l; Handle = p; TextureNames = tn; Samplers = sd; GroupSize = gs; CacheName = Symbol.Empty; Interface = s.Interface.[ShaderStage.Compute]; GLSL = glsl }
    end

type BindingReference =
    | UniformRef of buffer : UniformBuffer * offset : int * valueType : FShade.GLSL.GLSLType
    | StorageBufferRef of set : int * binding : int * elementType : FShade.GLSL.GLSLType
    | SampledImageRef of set : int * binding : int * index : int * info : FShade.GLSL.GLSLSamplerType * sampler : Sampler
    | StorageImageRef of set : int * binding : int * info : FShade.GLSL.GLSLImageType

[<StructuredFormatDisplay("{AsString}")>]
type InputBinding(shader : ComputeShader, sets : DescriptorSet[], references : Map<string, list<BindingReference>>, imageArrays : MapExt<int * int, Option<VkImageLayout * ImageView * Sampler>[]>, buffers : List<UniformBuffer>) =
    
    
    static let rec prettyName (t : FShade.GLSL.GLSLType) =
        match t with
            | FShade.GLSL.GLSLType.Bool -> "bool"
            | FShade.GLSL.GLSLType.Void -> "void"
            | FShade.GLSL.GLSLType.Float(32) -> "float"
            | FShade.GLSL.GLSLType.Float(64) -> "double"
            | FShade.GLSL.GLSLType.Float(w) -> sprintf "float%d" w
            | FShade.GLSL.GLSLType.Int(true, 32) -> "int"
            | FShade.GLSL.GLSLType.Int(true, w) -> sprintf "int%d" w
            | FShade.GLSL.GLSLType.Int(false, 32) -> "uint"
            | FShade.GLSL.GLSLType.Int(false, w) -> sprintf "uint%d" w
            | FShade.GLSL.GLSLType.Vec(dim, et) -> sprintf "%s%d" (prettyName et) dim
            | FShade.GLSL.GLSLType.Mat(r, c, et) -> sprintf "%sx%dx%d" (prettyName et) r c
            | FShade.GLSL.GLSLType.Struct(name, fields, size) ->
                fields 
                    |> List.map (fun (name, typ,_) -> sprintf "%s : %s" name (prettyName typ))
                    |> String.concat "; "
                    |> sprintf "struct { %s }"
            | FShade.GLSL.GLSLType.Array(len, et, _) -> sprintf "%s[%d]" (prettyName et) len 
            | FShade.GLSL.GLSLType.Image _ -> "image"
            | FShade.GLSL.GLSLType.Sampler _ -> "sampler"
            | FShade.GLSL.GLSLType.DynamicArray(t,_) -> sprintf "%s[]" (prettyName t) 
            | FShade.GLSL.GLSLType.Intrinsic str -> str

    let device = shader.Device
    let lockObj = obj()
    let mutable disposables : MapExt<int * int * int, IDisposable> = MapExt.empty
    let mutable dirtyBuffers = ref HashSet.empty
    let mutable pendingWrites = MapExt.empty

    let changed = Event<unit>()

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
        changed.Trigger()
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

    let write (r : BindingReference) (value : obj) =
        lock lockObj (fun () ->
            match r with
                | UniformRef(buffer, offset, targetType) ->
                    let w = UniformWriters.getWriter offset targetType (value.GetType())
                    w.WriteUnsafeValue(value, buffer.Storage.Pointer)
                    dirtyBuffers <- ref <| HashSet.add buffer !dirtyBuffers

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

                            | :? ITextureRange as r ->
                                let image = r.Texture |> unbox<Image>
                                let view = device.CreateOutputImageView(image, r.Levels.Min, 1 + r.Levels.Max - r.Levels.Min, r.Slices.Min, 1 + r.Slices.Max - r.Slices.Min)
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
                                content.[index] <- Some (VkImageLayout.General, view, sampler)
                                Some { new IDisposable with member x.Dispose() = device.Delete image; device.Delete view }

                            | :? ITextureRange as r ->
                                let image = unbox<Image> r.Texture
                                let view = device.CreateInputImageView(image, info, r.Levels, r.Slices, VkComponentMapping.Identity)
                                content.[index] <- Some (VkImageLayout.General, view, sampler)
                                Some { new IDisposable with member x.Dispose() = device.Delete view }

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
                                let buffer = b.Buffer |> unbox<Buffer>
                                buffer, int64 b.Offset, int64 b.Size, None

                            | _ -> 
                                failf "unexpected storage buffer %A" value

                    let write = Descriptor.StorageBuffer(binding, buffer, offset, size)
                    update set binding write
                    setResource set binding 0 res
        )

    let flush() =
        lock lockObj (fun () ->
            let buffers = !Interlocked.Exchange(&dirtyBuffers, ref HashSet.empty)
            let writes = Interlocked.Exchange(&pendingWrites, MapExt.empty)

            if not (HashSet.isEmpty buffers) then
                use token = device.Token
                for b in buffers do device.Upload b
                token.Sync()

            for (set, desc) in MapExt.toSeq writes do   
                let values = desc |> MapExt.toSeq |> Seq.map snd |> Seq.toArray
                sets.[set].Update(values)
        )

    let release() =
        lock lockObj (fun () ->
            dirtyBuffers <- ref HashSet.empty
            pendingWrites <- MapExt.empty
            for (_,d) in MapExt.toSeq disposables do d.Dispose()
            for b in buffers do device.Delete b
            buffers.Clear()
            disposables <- MapExt.empty
            for s in sets do device.Delete s
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

    let missingNames = System.Collections.Generic.HashSet (Map.toSeq references |> Seq.map fst)

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

    member x.Changed = changed.Publish
    member x.Dispose() = release()

    member x.Layout = shader.Layout
    member x.Sets = sets
    member x.SetHandles = setHandles
                    

   

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
    member x.Set(ref : BindingReference, value : obj) = write ref value
    member x.Set(name : string, value : obj) = 
        match Map.tryFind name references with
            | Some refs -> 
                missingNames.Remove name |> ignore
                for r in refs do write r value
            | None -> () //failf "invalid reference %A" name

    member x.Item
        with set (name : string) (value : obj) = x.Set(name, value)

    member x.Item
        with set (ref : BindingReference) (value : obj) = x.Set(ref, value)

    member x.Flush() =
        if missingNames.Count > 0 then
            Log.warn "[Vulkan] missing inputs for compute shader: %A" (Seq.toList missingNames) 
        flush()
    member x.Bind = 
        bind

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    interface IComputeShaderInputBinding with
        member x.Shader = shader :> IComputeShader
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
         
        static member ImageBarrier(img : ImageSubresourceRange, src : VkAccessFlags, dst : VkAccessFlags) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue cmd =
                    native {
                        let! pImageMemoryBarrier =
                            VkImageMemoryBarrier(
                                src, dst,
                                VkImageLayout.General, VkImageLayout.General,
                                VK_QUEUE_FAMILY_IGNORED,
                                VK_QUEUE_FAMILY_IGNORED,
                                img.Image.Handle,
                                img.VkImageSubresourceRange
                            )

                        let srcStage =
                            match src with
                                | VkAccessFlags.TransferReadBit | VkAccessFlags.TransferWriteBit -> VkPipelineStageFlags.TransferBit
                                | _ -> VkPipelineStageFlags.ComputeShaderBit
                            
                        let dstStage =
                            match dst with
                                | VkAccessFlags.TransferReadBit | VkAccessFlags.TransferWriteBit -> VkPipelineStageFlags.TransferBit
                                | _ -> VkPipelineStageFlags.ComputeShaderBit

                        cmd.AppendCommand()
                        VkRaw.vkCmdPipelineBarrier(
                            cmd.Handle, 
                            srcStage,
                            dstStage,
                            VkDependencyFlags.None,
                            0u, NativePtr.zero,
                            0u, NativePtr.zero,
                            1u, pImageMemoryBarrier
                        )

                        return Disposable.Empty
                    }
            }

    module TextureLayout = 
        let toImageLayout =
            LookupTable.lookupTable [
                TextureLayout.Sample, VkImageLayout.ShaderReadOnlyOptimal
                TextureLayout.ShaderRead, VkImageLayout.ShaderReadOnlyOptimal
                TextureLayout.ShaderReadWrite, VkImageLayout.General
                TextureLayout.ShaderWrite, VkImageLayout.General
                TextureLayout.TransferRead, VkImageLayout.TransferSrcOptimal
                TextureLayout.TransferWrite, VkImageLayout.TransferDstOptimal
            ]

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module ComputeCommand =
        open Aardvark.Base.Monads.State

        let private accessFlags =
            LookupTable.lookupTable [
                ResourceAccess.ShaderRead, VkAccessFlags.ShaderReadBit
                ResourceAccess.ShaderWrite, VkAccessFlags.ShaderWriteBit
                ResourceAccess.TransferRead, VkAccessFlags.TransferReadBit
                ResourceAccess.TransferWrite, VkAccessFlags.TransferWriteBit
            ]

        [<AutoOpen>]
        module private Compiler =
            open KHRSwapchain

            type CompilerState =
                {
                    device          : Device
                    downloads       : list<Buffer * HostMemory>
                    uploads         : list<HostMemory * Buffer>
                    inputs          : HashSet<InputBinding>
                    initialLayouts  : HashMap<Image, VkImageLayout>
                    imageLayouts    : HashMap<Image, VkImageLayout>
                }

            [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
            module CompilerState =
                let device = State.get |> State.map (fun s -> s.device)

                let addInput (i : InputBinding) : State<CompilerState, unit> =
                    State.modify (fun s -> { s with inputs = HashSet.add i s.inputs })

                let download (stream : VKVM.CommandStream) (src : Buffer) (srcOffset : nativeint) (dst : HostMemory) (size : nativeint) =
                    state {
                        let! device = device
                        let temp = device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferDstBit (int64 size)
                        stream.CopyBuffer(src.Handle, temp.Handle, [| VkBufferCopy(uint64 srcOffset, 0UL, uint64 size) |]) |> ignore
                        do! State.modify (fun s -> { s with downloads = (temp, dst) :: s.downloads })
                    } 

                let upload (stream : VKVM.CommandStream) (src : HostMemory) (dst : Buffer) (dstOffset : nativeint) (size : nativeint) =
                    state {
                        let! device = device
                        let temp = device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferDstBit (int64 size)
                        stream.CopyBuffer(temp.Handle, dst.Handle, [| VkBufferCopy(0UL, uint64 dstOffset, uint64 size) |]) |> ignore
                        do! State.modify (fun s -> { s with uploads = (src, temp) :: s.uploads })
                    }

                let transformLayout (image : Image) (newLayout : VkImageLayout) =
                    State.custom (fun s ->
                        match HashMap.tryFind image s.imageLayouts with
                            | Some o ->
                                let s = { s with imageLayouts = HashMap.add image newLayout s.imageLayouts }
                                s, o
                            | None ->
                                let o = image.Layout
                                let s = { s with imageLayouts = HashMap.add image newLayout s.imageLayouts; initialLayouts = HashMap.add image o s.initialLayouts }
                                s, o
                    )

            type VKVM.CommandStream with
                member x.TransformLayout(img : Image, source : VkImageLayout, target : VkImageLayout) =

                    let src =
                        if source = VkImageLayout.ColorAttachmentOptimal then VkAccessFlags.ColorAttachmentWriteBit
                        elif source = VkImageLayout.DepthStencilAttachmentOptimal then VkAccessFlags.DepthStencilAttachmentWriteBit
                        elif source = VkImageLayout.TransferDstOptimal then VkAccessFlags.TransferWriteBit
                        elif source = VkImageLayout.PresentSrcKhr then VkAccessFlags.MemoryReadBit
                        elif source = VkImageLayout.Preinitialized then VkAccessFlags.HostWriteBit
                        elif source = VkImageLayout.TransferSrcOptimal then VkAccessFlags.TransferReadBit
                        elif source = VkImageLayout.ShaderReadOnlyOptimal then VkAccessFlags.ShaderReadBit // ||| VkAccessFlags.InputAttachmentReadBit
                        else VkAccessFlags.None

                    let dst =
                        if target = VkImageLayout.TransferSrcOptimal then VkAccessFlags.TransferReadBit
                        elif target = VkImageLayout.TransferDstOptimal then VkAccessFlags.TransferWriteBit
                        elif target = VkImageLayout.ColorAttachmentOptimal then VkAccessFlags.ColorAttachmentWriteBit
                        elif target = VkImageLayout.DepthStencilAttachmentOptimal then VkAccessFlags.DepthStencilAttachmentWriteBit
                        elif target = VkImageLayout.ShaderReadOnlyOptimal then VkAccessFlags.ShaderReadBit // ||| VkAccessFlags.InputAttachmentReadBit
                        elif target = VkImageLayout.PresentSrcKhr then VkAccessFlags.MemoryReadBit
                        elif target = VkImageLayout.General then VkAccessFlags.None
                        else VkAccessFlags.None

                    let srcMask =
                        if source = VkImageLayout.ColorAttachmentOptimal then VkPipelineStageFlags.ColorAttachmentOutputBit
                        elif source = VkImageLayout.DepthStencilAttachmentOptimal then VkPipelineStageFlags.LateFragmentTestsBit
                        elif source = VkImageLayout.TransferDstOptimal then VkPipelineStageFlags.TransferBit
                        elif source = VkImageLayout.PresentSrcKhr then VkPipelineStageFlags.TransferBit
                        elif source = VkImageLayout.Preinitialized then VkPipelineStageFlags.HostBit
                        elif source = VkImageLayout.TransferSrcOptimal then VkPipelineStageFlags.TransferBit
                        elif source = VkImageLayout.ShaderReadOnlyOptimal then VkPipelineStageFlags.ComputeShaderBit
                        elif source = VkImageLayout.Undefined then VkPipelineStageFlags.HostBit // VK_PIPELINE_STAGE_FLAGS_HOST_BIT
                        elif source = VkImageLayout.General then VkPipelineStageFlags.HostBit
                        else VkPipelineStageFlags.None

                    let dstMask =
                        if target = VkImageLayout.TransferSrcOptimal then VkPipelineStageFlags.TransferBit
                        elif target = VkImageLayout.TransferDstOptimal then VkPipelineStageFlags.TransferBit
                        elif target = VkImageLayout.ColorAttachmentOptimal then VkPipelineStageFlags.ColorAttachmentOutputBit
                        elif target = VkImageLayout.DepthStencilAttachmentOptimal then VkPipelineStageFlags.EarlyFragmentTestsBit
                        elif target = VkImageLayout.ShaderReadOnlyOptimal then VkPipelineStageFlags.ComputeShaderBit
                        elif target = VkImageLayout.PresentSrcKhr then VkPipelineStageFlags.TransferBit
                        elif target = VkImageLayout.General then VkPipelineStageFlags.HostBit


                        else VkPipelineStageFlags.None


                    let range =
                        if VkFormat.hasDepth img.Format then img.[ImageAspect.Depth]
                        else img.[ImageAspect.Color]

                    let barrier =
                        VkImageMemoryBarrier(
                            src,
                            dst,
                            source,
                            target,
                            VK_QUEUE_FAMILY_IGNORED,
                            VK_QUEUE_FAMILY_IGNORED,
                            img.Handle,
                            range.VkImageSubresourceRange
                        )
                    x.PipelineBarrier(
                        srcMask,
                        dstMask,
                        [||],
                        [||],
                        [| barrier |]    
                    )
                    
                member x.TransformLayout(range : ImageSubresourceRange, source : VkImageLayout, target : VkImageLayout) =

                    let src =
                        if source = VkImageLayout.ColorAttachmentOptimal then VkAccessFlags.ColorAttachmentWriteBit
                        elif source = VkImageLayout.DepthStencilAttachmentOptimal then VkAccessFlags.DepthStencilAttachmentWriteBit
                        elif source = VkImageLayout.TransferDstOptimal then VkAccessFlags.TransferWriteBit
                        elif source = VkImageLayout.PresentSrcKhr then VkAccessFlags.MemoryReadBit
                        elif source = VkImageLayout.Preinitialized then VkAccessFlags.HostWriteBit
                        elif source = VkImageLayout.TransferSrcOptimal then VkAccessFlags.TransferReadBit
                        elif source = VkImageLayout.ShaderReadOnlyOptimal then VkAccessFlags.ShaderReadBit // ||| VkAccessFlags.InputAttachmentReadBit
                        else VkAccessFlags.None

                    let dst =
                        if target = VkImageLayout.TransferSrcOptimal then VkAccessFlags.TransferReadBit
                        elif target = VkImageLayout.TransferDstOptimal then VkAccessFlags.TransferWriteBit
                        elif target = VkImageLayout.ColorAttachmentOptimal then VkAccessFlags.ColorAttachmentWriteBit
                        elif target = VkImageLayout.DepthStencilAttachmentOptimal then VkAccessFlags.DepthStencilAttachmentWriteBit
                        elif target = VkImageLayout.ShaderReadOnlyOptimal then VkAccessFlags.ShaderReadBit // ||| VkAccessFlags.InputAttachmentReadBit
                        elif target = VkImageLayout.PresentSrcKhr then VkAccessFlags.MemoryReadBit
                        elif target = VkImageLayout.General then VkAccessFlags.None
                        else VkAccessFlags.None

                    let srcMask =
                        if source = VkImageLayout.ColorAttachmentOptimal then VkPipelineStageFlags.ColorAttachmentOutputBit
                        elif source = VkImageLayout.DepthStencilAttachmentOptimal then VkPipelineStageFlags.LateFragmentTestsBit
                        elif source = VkImageLayout.TransferDstOptimal then VkPipelineStageFlags.TransferBit
                        elif source = VkImageLayout.PresentSrcKhr then VkPipelineStageFlags.TransferBit
                        elif source = VkImageLayout.Preinitialized then VkPipelineStageFlags.HostBit
                        elif source = VkImageLayout.TransferSrcOptimal then VkPipelineStageFlags.TransferBit
                        elif source = VkImageLayout.ShaderReadOnlyOptimal then VkPipelineStageFlags.ComputeShaderBit
                        elif source = VkImageLayout.Undefined then VkPipelineStageFlags.HostBit // VK_PIPELINE_STAGE_FLAGS_HOST_BIT
                        elif source = VkImageLayout.General then VkPipelineStageFlags.HostBit
                        else VkPipelineStageFlags.None

                    let dstMask =
                        if target = VkImageLayout.TransferSrcOptimal then VkPipelineStageFlags.TransferBit
                        elif target = VkImageLayout.TransferDstOptimal then VkPipelineStageFlags.TransferBit
                        elif target = VkImageLayout.ColorAttachmentOptimal then VkPipelineStageFlags.ColorAttachmentOutputBit
                        elif target = VkImageLayout.DepthStencilAttachmentOptimal then VkPipelineStageFlags.EarlyFragmentTestsBit
                        elif target = VkImageLayout.ShaderReadOnlyOptimal then VkPipelineStageFlags.ComputeShaderBit
                        elif target = VkImageLayout.PresentSrcKhr then VkPipelineStageFlags.TransferBit
                        elif target = VkImageLayout.General then VkPipelineStageFlags.HostBit
                        else VkPipelineStageFlags.None

                        
                    let barrier =
                        VkImageMemoryBarrier(
                            src,
                            dst,
                            source,
                            target,
                            VK_QUEUE_FAMILY_IGNORED,
                            VK_QUEUE_FAMILY_IGNORED,
                            range.Image.Handle,
                            range.VkImageSubresourceRange
                        )
                    x.PipelineBarrier(
                        srcMask,
                        dstMask,
                        [||],
                        [||],
                        [| barrier |]    
                    )

                member x.Sync(b : Buffer, src : VkAccessFlags, dst : VkAccessFlags) =

                    let barrier =
                        VkBufferMemoryBarrier(
                            src,
                            dst,
                            VK_QUEUE_FAMILY_IGNORED, VK_QUEUE_FAMILY_IGNORED,
                            b.Handle,
                            0UL,
                            uint64 b.Size
                        )

                    let srcStage =
                        match src with
                            | VkAccessFlags.HostReadBit -> VkPipelineStageFlags.HostBit
                            | VkAccessFlags.HostWriteBit -> VkPipelineStageFlags.HostBit
                            | VkAccessFlags.IndexReadBit -> VkPipelineStageFlags.VertexInputBit
                            | VkAccessFlags.IndirectCommandReadBit -> VkPipelineStageFlags.DrawIndirectBit
                            | VkAccessFlags.ShaderReadBit -> VkPipelineStageFlags.ComputeShaderBit
                            | VkAccessFlags.ShaderWriteBit -> VkPipelineStageFlags.ComputeShaderBit
                            | VkAccessFlags.TransferReadBit -> VkPipelineStageFlags.TransferBit
                            | VkAccessFlags.TransferWriteBit -> VkPipelineStageFlags.TransferBit
                            | VkAccessFlags.UniformReadBit -> VkPipelineStageFlags.ComputeShaderBit
                            | VkAccessFlags.VertexAttributeReadBit -> VkPipelineStageFlags.VertexInputBit
                            | _ -> VkPipelineStageFlags.None
                            
                    let dstStage =
                        match dst with
                            | VkAccessFlags.HostReadBit -> VkPipelineStageFlags.HostBit
                            | VkAccessFlags.HostWriteBit -> VkPipelineStageFlags.HostBit
                            | VkAccessFlags.IndexReadBit -> VkPipelineStageFlags.VertexInputBit
                            | VkAccessFlags.IndirectCommandReadBit -> VkPipelineStageFlags.DrawIndirectBit
                            | VkAccessFlags.ShaderReadBit -> VkPipelineStageFlags.VertexShaderBit
                            | VkAccessFlags.ShaderWriteBit -> VkPipelineStageFlags.VertexShaderBit
                            | VkAccessFlags.TransferReadBit -> VkPipelineStageFlags.TransferBit
                            | VkAccessFlags.TransferWriteBit -> VkPipelineStageFlags.TransferBit
                            | VkAccessFlags.UniformReadBit -> VkPipelineStageFlags.VertexShaderBit
                            | VkAccessFlags.VertexAttributeReadBit -> VkPipelineStageFlags.VertexInputBit
                            | _ -> VkPipelineStageFlags.None


                    x.PipelineBarrier(
                        srcStage,
                        dstStage,
                        [||],
                        [| barrier |],
                        [||]
                    )

                member x.SetBuffer(b : Buffer, offset : int64, size : int64, pattern : byte[]) =
                    let value = BitConverter.ToUInt32(pattern, 0)
                    x.FillBuffer(b.Handle, uint64 offset, uint64 size, value)

                member x.ImageBarrier(img : ImageSubresourceRange, src : VkAccessFlags, dst : VkAccessFlags) =

                    let srcStage =
                        match src with
                            | VkAccessFlags.TransferReadBit | VkAccessFlags.TransferWriteBit -> VkPipelineStageFlags.TransferBit
                            | _ -> VkPipelineStageFlags.ComputeShaderBit
                            
                    let dstStage =
                        match dst with
                            | VkAccessFlags.TransferReadBit | VkAccessFlags.TransferWriteBit -> VkPipelineStageFlags.TransferBit
                            | _ -> VkPipelineStageFlags.ComputeShaderBit

                    x.PipelineBarrier(
                        srcStage, dstStage,
                        [||],
                        [||],
                        [|
                            VkImageMemoryBarrier(
                                src, dst,
                                VkImageLayout.General, VkImageLayout.General,
                                VK_QUEUE_FAMILY_IGNORED,
                                VK_QUEUE_FAMILY_IGNORED,
                                img.Image.Handle,
                                img.VkImageSubresourceRange
                            )
                        |]
                    )

            type ComputeProgram(stream : VKVM.CommandStream, state : CompilerState) =
                inherit ComputeProgram<unit>()

                
                static let commandPools =
                    System.Collections.Concurrent.ConcurrentDictionary<Device, ThreadLocal<CommandPool>>()

                static let getPool (device : Device) =
                    let threadLocal = 
                        commandPools.GetOrAdd(device, fun device ->
                            new ThreadLocal<CommandPool>(fun () ->
                                let pool = device.GraphicsFamily.CreateCommandPool(CommandPoolFlags.Transient)
                                device.OnDispose.Add (fun () -> pool.Dispose())
                                pool
                            )
                        )
                    threadLocal.Value
            
            
                let device = state.device

                let mutable dirty = 1
                let changed () = Interlocked.Exchange(&dirty, 1) |> ignore

                let subscriptions =
                    state.inputs |> HashSet.toList |> List.map (fun i -> i.Changed.Subscribe changed)

                let uploads =
                    state.uploads |> List.map (fun (src, dst) ->
                        match src with
                            | HostMemory.Unmanaged pSrc ->
                                fun () -> dst.Memory.Mapped (fun pDst -> Marshal.Copy(pSrc, pDst, dst.Size))
                            | HostMemory.Managed (src, srcOffset) ->
                                let srcOffset = nativeint srcOffset * nativeint (Marshal.SizeOf (src.GetType().GetElementType()))
                                fun () -> 
                                    dst.Memory.Mapped (fun pDst ->
                                        let gc = GCHandle.Alloc(src, GCHandleType.Pinned)
                                        try 
                                            Marshal.Copy(gc.AddrOfPinnedObject() + srcOffset, pDst, dst.Size)
                                        finally
                                            gc.Free()
                                    )
                    )

                let downloads =
                    state.downloads |> List.map (fun (src, dst) ->
                        match dst with
                            | HostMemory.Unmanaged pDst ->
                                fun () -> src.Memory.Mapped (fun pSrc -> Marshal.Copy(pSrc, pDst, src.Size))
                            | HostMemory.Managed(dst, dstOffset) ->
                                let dstOffset = nativeint dstOffset * nativeint (Marshal.SizeOf (dst.GetType().GetElementType()))
                                fun () -> 
                                    src.Memory.Mapped (fun pSrc ->
                                        let gc = GCHandle.Alloc(dst, GCHandleType.Pinned)
                                        try 
                                            Marshal.Copy(pSrc, gc.AddrOfPinnedObject() + dstOffset, src.Size)
                                        finally
                                            gc.Free()
                                    )
                    )

                do  for (image, init) in HashMap.toSeq state.initialLayouts do
                        match HashMap.tryFind image state.imageLayouts with
                            | Some current when current<> init ->
                                stream.TransformLayout(image, current, init) |> ignore
                            | _ ->
                                ()

                member x.State = state
                member x.Stream = stream

                member x.Upload() =
                    for u in uploads do u()
                
                    
                member x.Download() =
                    for u in downloads do u()
                

                override x.Release() =
                    for (_,b) in state.uploads do device.Delete b
                    for (b,_) in state.downloads do device.Delete b
                    stream.Dispose()

                override x.RunUnit(queries : IQuery) =
                    let isChanged = Interlocked.Exchange(&dirty, 0) = 1
                    let vulkanQueries = queries.ToVulkanQuery()

                    for u in uploads do u()
                
                    let pool = getPool device
                    use cmd = pool.CreateCommandBuffer(CommandBufferLevel.Primary)

                    queries.Begin()

                    cmd.Begin(CommandBufferUsage.OneTimeSubmit)

                    for q in vulkanQueries do
                        q.Begin cmd

                    cmd.AppendCommand()
                    stream.Run(cmd.Handle)

                    for q in vulkanQueries do
                        q.End cmd

                    cmd.End()

                    queries.End()

                    device.GraphicsFamily.RunSynchronously cmd

                    for d in downloads do d()


        let toCommand (cmd : ComputeCommand) (device : Device) =
            match cmd with
                | ComputeCommand.BindCmd shader ->
                    Command.Bind (unbox shader)

                | ComputeCommand.SetInputCmd input ->
                    Command.SetInputs (unbox input)

                | ComputeCommand.DispatchCmd groups ->
                    Command.Dispatch groups

                | ComputeCommand.CopyBufferCmd(src, dst) ->
                    let srcBuffer = src.Buffer |> unbox<Buffer>
                    let dstBuffer = dst.Buffer |> unbox<Buffer>
                    Command.Copy(srcBuffer, int64 src.Offset, dstBuffer, int64 dst.Offset, min (int64 src.Size) (int64 dst.Size))

                | ComputeCommand.DownloadBufferCmd(src, dst) ->
                    let srcBuffer = src.Buffer |> unbox<Buffer>
                    match dst with
                        | HostMemory.Unmanaged dst ->
                            command {
                                let temp = device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferDstBit (int64 src.Size)
                                try 
                                    do! Command.Copy(srcBuffer, int64 src.Offset, temp, 0L, temp.Size)
                                finally
                                    temp.Memory.Mapped (fun src -> Marshal.Copy(src, dst, temp.Size))
                                    device.Delete temp
                            }
                        | HostMemory.Managed(dst, dstOffset) ->
                            let elementSize = dst.GetType().GetElementType() |> Marshal.SizeOf |> nativeint
                            command {
                                let temp = device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferDstBit (int64 src.Size)
                                try 
                                    do! Command.Copy(srcBuffer, int64 src.Offset, temp, 0L, temp.Size)
                                finally
                                    temp.Memory.Mapped (fun src -> 
                                        let gc = GCHandle.Alloc(dst, GCHandleType.Pinned)
                                        try 
                                            let dst = gc.AddrOfPinnedObject() + (nativeint dstOffset * elementSize)
                                            Marshal.Copy(src, dst, temp.Size)
                                        finally 
                                            gc.Free()
                                    )
                                    device.Delete temp
                            }
                            
                | ComputeCommand.UploadBufferCmd(src, dst) ->
                    command {
                        let size = int64 dst.Size
                        let temp = device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferSrcBit size
                        try
                            temp.Memory.Mapped (fun dst ->
                                match src with
                                    | HostMemory.Unmanaged src -> 
                                        Marshal.Copy(src, dst, size)
                                    | HostMemory.Managed(src, srcOffset) ->
                                        let elementSize = src.GetType().GetElementType() |> Marshal.SizeOf |> nativeint
                                        let gc = GCHandle.Alloc(src, GCHandleType.Pinned)
                                        try
                                            let src = gc.AddrOfPinnedObject() + (nativeint srcOffset * elementSize)
                                            Marshal.Copy(src, dst, size)
                                        finally
                                            gc.Free()
                            ) 
                            
                            do! Command.Copy(temp, 0L, unbox<Buffer> dst.Buffer, int64 dst.Offset, int64 temp.Size)     
                        finally
                            device.Delete temp
                    }

                | ComputeCommand.CopyImageCmd(src, srcOffset, dst, dstOffset, size) ->
                    let src = ImageSubresourceLayers.ofFramebufferOutput src
                    let dst = ImageSubresourceLayers.ofFramebufferOutput dst
                    Command.Copy(src, srcOffset, dst, dstOffset, size)

                | ComputeCommand.TransformLayoutCmd(tex, layout) ->
                    let tex = unbox<Image> tex
                    let layout = TextureLayout.toImageLayout layout
                    Command.TransformLayout(tex, layout)

                | ComputeCommand.TransformSubLayoutCmd(range, srcLayout, dstLayout) ->
                    let tex = unbox<Image> range.Texture
                    let srcLayout = TextureLayout.toImageLayout srcLayout
                    let dstLayout = TextureLayout.toImageLayout dstLayout
                    let res = tex.[ImageAspect.ofTextureAspect range.Aspect, range.Levels.Min .. range.Levels.Max, range.Slices.Min .. range.Slices.Max]
                    Command.TransformLayout(res, srcLayout, dstLayout)


                | ComputeCommand.SyncBufferCmd(b, src, dst) ->
                    Command.Sync(unbox b, accessFlags src, accessFlags dst)

                | ComputeCommand.SyncImageCmd(i, src, dst) ->
                    let i = unbox<Image> i
                    Command.ImageBarrier(i.[ImageAspect.Color], accessFlags src, accessFlags dst)

                | ComputeCommand.SetBufferCmd(b, pattern) ->
                    Command.SetBuffer(unbox b.Buffer, int64 b.Offset, int64 b.Size, pattern)

                | ComputeCommand.ExecuteCmd other ->
                    match other with 
                        | :? ComputeProgram as o ->
                            { new Command() with
                                override x.Compatible = QueueFlags.All
                                override x.Enqueue(cmd : CommandBuffer) =
                                    o.Upload()
                                    cmd.AppendCommand()
                                    o.Stream.Run(cmd.Handle)
                                    Disposable.Custom o.Download
                            }
                        | _ ->
                            failf "not implemented"


        let private compileS (cmd : ComputeCommand) (stream : VKVM.CommandStream) : State<CompilerState, unit> =
            state {
                match cmd with
                    | ComputeCommand.BindCmd shader ->
                        let shader = unbox<ComputeShader> shader
                        stream.BindPipeline(VkPipelineBindPoint.Compute, shader.Handle) |> ignore

                    | ComputeCommand.SetInputCmd input ->
                        let input = unbox<InputBinding> input
                        do! CompilerState.addInput input
                        stream.BindDescriptorSets(VkPipelineBindPoint.Compute, input.Layout.Handle, 0u, input.Sets |> Array.map (fun s -> s.Handle), [||]) |> ignore

                    | ComputeCommand.DispatchCmd groups ->
                        stream.Dispatch(uint32 groups.X, uint32 groups.Y, uint32 groups.Z) |> ignore

                    | ComputeCommand.CopyBufferCmd(src, dst) ->
                        let srcBuffer = src.Buffer |> unbox<Buffer>
                        let dstBuffer = dst.Buffer |> unbox<Buffer>

                        let regions =
                            [|
                                VkBufferCopy(
                                    uint64 src.Offset,
                                    uint64 dst.Offset,
                                    uint64 (min src.Size dst.Size)   
                                )
                            |]

                        stream.CopyBuffer(srcBuffer.Handle, dstBuffer.Handle, regions) |> ignore

                    | ComputeCommand.DownloadBufferCmd(src, dst) ->
                        let srcBuffer = src.Buffer |> unbox<Buffer>
                        do! CompilerState.download stream srcBuffer src.Offset dst src.Size
                        
                    | ComputeCommand.UploadBufferCmd(src, dst) ->
                        let dstBuffer = dst.Buffer |> unbox<Buffer>
                        do! CompilerState.upload stream src dstBuffer dst.Offset dst.Size

                    | ComputeCommand.CopyImageCmd(src, srcOffset, dst, dstOffset, size) ->
                        let src = ImageSubresourceLayers.ofFramebufferOutput src
                        let dst = ImageSubresourceLayers.ofFramebufferOutput dst

                        stream.CopyImage(
                            src.Image.Handle, VkImageLayout.TransferSrcOptimal, 
                            dst.Image.Handle, VkImageLayout.TransferDstOptimal, 
                            [|
                                VkImageCopy(
                                    src.VkImageSubresourceLayers,
                                    VkOffset3D(srcOffset.X, srcOffset.Y, srcOffset.Z),
                                    dst.VkImageSubresourceLayers,
                                    VkOffset3D(dstOffset.X, dstOffset.Y, dstOffset.Z),
                                    VkExtent3D(size.X, size.Y, size.Z)
                                )
                            |]
                        ) |> ignore

                    | ComputeCommand.TransformLayoutCmd(tex, layout) ->
                        let tex = unbox<Image> tex
                        let newLayout = TextureLayout.toImageLayout layout
                        let! oldLayout = CompilerState.transformLayout tex newLayout
                        if oldLayout <> newLayout then
                            stream.TransformLayout(tex, oldLayout, newLayout) |> ignore
            
                    | ComputeCommand.TransformSubLayoutCmd(range, srcLayout, dstLayout) ->
                        let tex = unbox<Image> range.Texture
                        let srcLayout = TextureLayout.toImageLayout srcLayout
                        let dstLayout = TextureLayout.toImageLayout dstLayout
                        let res = tex.[ImageAspect.ofTextureAspect range.Aspect, range.Levels.Min .. range.Levels.Max, range.Slices.Min .. range.Slices.Max]
                        stream.TransformLayout(res, srcLayout, dstLayout) |> ignore

                    | ComputeCommand.SyncBufferCmd(b, src, dst) ->
                        stream.Sync(unbox b, accessFlags src, accessFlags dst) |> ignore
                        
                    | ComputeCommand.SyncImageCmd(i, src, dst) ->
                        let i = unbox<Image> i
                        stream.ImageBarrier(i.[ImageAspect.Color], accessFlags src, accessFlags dst) |> ignore

                    | ComputeCommand.SetBufferCmd(b, value) ->
                        stream.SetBuffer(unbox b.Buffer, int64 b.Offset, int64 b.Size, value) |> ignore

                    | ComputeCommand.ExecuteCmd other ->
                        match other with
                            | :? ComputeProgram as other ->
                                do! State.modify (fun s ->
                                    let o = other.State
                                    { s with
                                        uploads = s.uploads @ o.uploads
                                        downloads = s.downloads @ o.downloads
                                        inputs = HashSet.union s.inputs o.inputs
                                    }
                                )
                                stream.Call(other.Stream) |> ignore
                            | _ ->
                                failf "not implemented"

            }

        let compile (cmds : list<ComputeCommand>) (device : Device) =
            let stream = new VKVM.CommandStream()
            
            let mutable state = { device = device; uploads = []; downloads = []; inputs = HashSet.empty; imageLayouts = HashMap.empty; initialLayouts = HashMap.empty }
            for cmd in cmds do
                let c = compileS cmd stream
                c.Run(&state)

            new ComputeProgram(stream, state) :> ComputeProgram<unit>
    
        let run (cmds : list<ComputeCommand>) (queries : IQuery) (device : Device) =
            let vulkanQueries = queries.ToVulkanQuery()

            queries.Begin()

            device.perform {
                for q in vulkanQueries do
                    do! Command.Begin q

                for cmd in cmds do
                    do! toCommand cmd device

                for q in vulkanQueries do
                    do! Command.End q
            }

            queries.End()
            
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ComputeShader =  
    open System.IO

    type private FailReason =
        | NoCache
        | BrokenCache
        | NoAccess

    type private LoadResult<'a> =
        | Failed of FailReason
        | Loaded of 'a

    let private cache = Symbol.Create "ComputeShaderCache" 

    let private main = CStr.malloc "main"

    let delete (shader : ComputeShader) =
        shader.Device.RemoveCached(cache, shader)

    let toByteArray (shader : ComputeShader) =
        ShaderProgram.pickler.Pickle( 
            (
                shader.ShaderModule.SpirV,
                shader.Interface,
                shader.GroupSize,
                shader.GLSL
            ) 
        )

    let private tryOfByteArray (data : byte[]) (device : Device) =
        try
            let (spirv : byte[], iface : FShade.GLSL.GLSLShaderInterface, groupSize : V3i, glsl : Option<string>) = 
                ShaderProgram.pickler.UnPickle data
             
            let module_ =
                device.CreateShaderModule(ShaderStage.Compute, spirv, iface)

            let shader = 
                 Shader(module_, ShaderStage.Compute, iface)

            let layout =
                device.CreatePipelineLayout([| shader |], 1, Set.empty)
                
            native {
                let shaderInfo =
                    VkPipelineShaderStageCreateInfo(
                        VkPipelineShaderStageCreateFlags.MinValue,
                        VkShaderStageFlags.ComputeBit,
                        module_.Handle,
                        main,
                        NativePtr.zero
                    )

                let! pPipelineInfo =
                    VkComputePipelineCreateInfo(
                        VkPipelineCreateFlags.None,
                        shaderInfo,
                        layout.Handle,
                        VkPipeline.Null,
                        0
                    )

                let! pHandle = VkPipeline.Null
                VkRaw.vkCreateComputePipelines(device.Handle, VkPipelineCache.Null, 1u, pPipelineInfo, NativePtr.zero, pHandle)
                    |> check "could not create compute pipeline"

                let textureNames =
                    iface.shaderSamplers |> Seq.collect (fun samName ->
                        let sam = iface.program.samplers.[samName]
                        sam.samplerTextures |> Seq.mapi (fun i (texName, state) ->
                            (samName, i), texName
                        )
                    )
                    |> Map.ofSeq

                let samplers =
                    iface.shaderSamplers |> Seq.collect (fun samName ->
                        let sam = iface.program.samplers.[samName]
                        sam.samplerTextures |> Seq.mapi (fun i (_, state) ->
                            (samName, i), device.CreateSampler state.SamplerState
                        )
                    )
                    |> Map.ofSeq
                
                return ComputeShader(device, module_, layout, !!pHandle, textureNames, samplers, groupSize, glsl)
                    |> LoadResult.Loaded
            }
        with _ ->
            Failed BrokenCache

    let private tryRead (file : string) (device : Device) =
        try
            if File.Exists file then
                let data = File.ReadAllBytes file
                tryOfByteArray data device
            else
                Failed NoCache
        with _ ->
            Failed NoAccess
            
    let private write (failReason : FailReason) (file : string) (shader : ComputeShader) =
        try
            let data = toByteArray shader
            
            if File.Exists file then
                match failReason with
                    | BrokenCache -> 
                        File.WriteAllBytes(file, data)
                    | _ ->
                        let fileData = File.ReadAllBytes file
                        if data <> fileData then
                            failf "[Vulkan] bad compute cache: %s" (Path.GetFileName file)
                        else
                            Log.warn "[Vulkan] duplicate cache write on %s" (Path.GetFileName file)
            else
                File.WriteAllBytes(file, data)
        with _ ->
            ()

    let private ofFShadeInternal (shader : FShade.ComputeShader) (device : Device) =
        let glsl = shader |> FShade.ComputeShader.toModule |> ModuleCompiler.compileGLSLVulkan
        
        ShaderProgram.logLines glsl.code

        let localSize =
            if shader.csLocalSize.AllGreater 0 then shader.csLocalSize
            else V3i.III

        let sm = ShaderModule.ofGLSL ShaderStage.Compute glsl device

        match sm.TryGetShader ShaderStage.Compute with
            | (true, shaderInfo) ->
                let layout =
                    device.CreatePipelineLayout([| shaderInfo |], 1, Set.empty)

                let shaderInfo =
                    VkPipelineShaderStageCreateInfo(
                        VkPipelineShaderStageCreateFlags.MinValue,
                        VkShaderStageFlags.ComputeBit,
                        sm.Handle,
                        main,
                        NativePtr.zero
                    )

                native {
                    let! pPipelineInfo =
                        VkComputePipelineCreateInfo(
                            VkPipelineCreateFlags.None,
                            shaderInfo,
                            layout.Handle,
                            VkPipeline.Null,
                            0
                        )

                    let! pHandle = VkPipeline.Null
                    VkRaw.vkCreateComputePipelines(device.Handle, VkPipelineCache.Null, 1u, pPipelineInfo, NativePtr.zero, pHandle)
                        |> check "could not create compute pipeline"

               
                    let samplers =
                        shader.csSamplerStates |> Map.map (fun _ s -> device.CreateSampler s.SamplerState)
                

                    return ComputeShader(device, sm, layout, !!pHandle, shader.csTextureNames, samplers, shader.csLocalSize, Some glsl.code)
                }
            | _ ->
                failf "could not create compute shader"

    let ofFShade (shader : FShade.ComputeShader) (device : Device) =
        device.GetCached(cache, shader, fun shader ->
            match device.ShaderCachePath with
                | Some shaderCachePath ->
                    let fileName = ShaderProgram.hashFileName (shader.csId, shader.csLocalSize)
                    let file = Path.Combine(shaderCachePath, fileName + ".compute")

                    match tryRead file device with
                        | Loaded loaded -> 
                            
                            if device.ValidateShaderCaches then
                                let temp = ofFShadeInternal shader device
                                let real = toByteArray loaded
                                let should = toByteArray temp
                                temp.Destroy()

                                if real <> should then
                                    let tmp = Path.GetTempFileName()
                                    let tmpReal = tmp + ".real"
                                    let tmpShould = tmp + ".should"
                                    File.WriteAllBytes(tmpReal, real)
                                    File.WriteAllBytes(tmpShould, should)
                                    failf "invalid cache for ComputeShader: real: %s vs. should: %s" tmpReal tmpShould
                                    
                            loaded.CacheName <- cache
                            loaded.RefCount <- 1 // leak
                            loaded
                        | Failed reason ->
                            let shader = ofFShadeInternal shader device
                            write reason file shader
                            shader.CacheName <- cache
                            shader.RefCount <- 1 // leak
                            shader


                | None -> 
                    let shader = ofFShadeInternal shader device
                    shader.CacheName <- cache
                    shader.RefCount <- 1 // leak
                    shader
        )

    let ofFunction (f : 'a -> 'b) (device : Device) =
        let shader = FShade.ComputeShader.ofFunction device.PhysicalDevice.Limits.Compute.MaxWorkGroupSize f
        ofFShade shader device
        
    let private (|UniformBufferBinding|StorageBufferBinding|SampledImageBinding|StorageImageBinding|Other|) (b : DescriptorSetLayoutBinding) =
        match b.DescriptorType with
            | VkDescriptorType.UniformBuffer ->
                match b.Parameter with
                    | UniformBlockParameter p -> UniformBufferBinding p
                    | _ -> Other
            | VkDescriptorType.StorageBuffer ->
                match b.Parameter with
                    | StorageBufferParameter p ->
                        StorageBufferBinding(p.ssbName, p.ssbType)
                    | _ ->
                        Other

            | VkDescriptorType.CombinedImageSampler ->
                match b.Parameter with
                    | SamplerParameter i -> SampledImageBinding i
                    | _ -> Other

            | VkDescriptorType.StorageImage ->
                match b.Parameter with
                    | ImageParameter i -> StorageImageBinding i
                    | _ -> Other

            | _ ->
                Other

    let newInputBinding (shader : ComputeShader) =
        let device = shader.Device
        let references = Dict<string, list<BindingReference>>()
        let setLayouts = shader.Layout.DescriptorSetLayouts
        let sets = Array.zeroCreate setLayouts.Length

        let buffers = List<UniformBuffer>()
        let mutable imageArrays : MapExt<int * int, Option<VkImageLayout * ImageView * Sampler>[]> = MapExt.empty

        for si in 0 .. setLayouts.Length - 1 do
            let setLayout = setLayouts.[si]
            let set = device.CreateDescriptorSet setLayout

            let descriptors = List()

            for bi in 0 .. setLayout.Bindings.Length - 1 do
                let binding = setLayout.Bindings.[bi]
                match binding with
                    | UniformBufferBinding layout ->
                        let buffer = device.CreateUniformBuffer layout
                        buffers.Add buffer

                        for field in layout.ubFields do
                            let name = 
                                if field.ufName.StartsWith "cs_" then field.ufName.Substring 3
                                else field.ufName
                            
                            let reference = UniformRef(buffer, field.ufOffset, field.ufType)
                            references.[name] <- [reference]
                            
                        descriptors.Add (Descriptor.UniformBuffer(bi, buffer))

                    | StorageBufferBinding (name, elementType) ->
                        let reference = StorageBufferRef(si, bi, elementType)
                        references.[name] <- [reference]
                        //descriptors.[bi] <- Descriptor.StorageBuffer(bi, Buffer(device, VkBuffer.Null, DevicePtr.Null))

                    | SampledImageBinding img ->
                        let name = img.samplerName
                        let images : Option<VkImageLayout * ImageView * Sampler>[] = Array.zeroCreate img.samplerCount 
                        for i in 0 .. img.samplerCount - 1 do
                            match Map.tryFind (name, i) shader.Samplers, Map.tryFind (name, i) shader.TextureNames with
                                | Some sampler, Some texName ->
                                    let reference = SampledImageRef(si, bi, i, img.samplerType, sampler)
                                    let old = references.GetOrCreate(texName, fun _ -> [])
                                    references.[texName] <- reference :: old
                                | _ ->
                                    let sampler =
                                        { SamplerState.Default with Filter = TextureFilter.MinMagLinearMipPoint }
                                        |> device.CreateSampler


                                    let reference = SampledImageRef(si, bi, i, img.samplerType, sampler)
                                    let old = references.GetOrCreate(name, fun _ -> [])
                                    references.[name] <- reference :: old

                        imageArrays <- MapExt.add (si, bi) images imageArrays
                        //descriptors.[bi] <- Descriptor.CombinedImageSampler(bi, images)

                    | StorageImageBinding i ->
                        let name = 
                            if i.imageName.StartsWith "cs_" then i.imageName.Substring 3
                            else i.imageName

                        let reference = StorageImageRef(si, bi, i.imageType)
                        references.[name] <- [reference]
                        //descriptors.[bi] <- Descriptor.StorageImage(bi, Unchecked.defaultof<_>)

                    | Other -> ()

            set.Update(CSharpList.toArray descriptors)
            sets.[si] <- set

        new InputBinding(shader, sets, Dict.toMap references, imageArrays, buffers)