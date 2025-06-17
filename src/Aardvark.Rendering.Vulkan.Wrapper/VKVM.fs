namespace Aardvark.Rendering.Vulkan

open System
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Rendering
open Microsoft.FSharp.NativeInterop

#nowarn "9"
#nowarn "51"

[<AutoOpen>]
module IndirectCommands =

    [<StructLayout(LayoutKind.Sequential)>]
    type DrawCall =
        struct
            val mutable public IsIndirect       : int
            val mutable public IsIndexed        : int
            val mutable public IndirectBuffer   : VkBuffer
            val mutable public IndirectCount    : int
            val mutable public DrawCallCount    : int
            val mutable public DrawCalls        : nativeptr<DrawCallInfo>


            static member Indirect (indexed : bool, ib : VkBuffer, count : int) =
                new DrawCall(true, indexed, ib, count, 0, NativePtr.zero)

            static member Direct (indexed : bool, calls : DrawCallInfo[]) =
                let pCalls = NativePtr.alloc calls.Length
                for i in 0 .. calls.Length-1 do
                    NativePtr.set pCalls i calls.[i]
                new DrawCall(false, indexed, VkBuffer.Null, 0, calls.Length, pCalls)
                
            member x.Dispose() =
                if not (NativePtr.isNull x.DrawCalls) then
                    NativePtr.free x.DrawCalls

                x.IndirectBuffer <- VkBuffer.Null
                x.IndirectCount <- 0
                x.DrawCalls <- NativePtr.zero
                x.DrawCallCount <- 0

            interface IDisposable with
                member x.Dispose() = x.Dispose()

            private new(isIndirect : bool, isIndexed : bool, ib : VkBuffer, ibc : int, callCount : int, pCalls : nativeptr<DrawCallInfo>) =
                {
                    IsIndirect = (if isIndirect then 1 else 0)
                    IsIndexed = (if isIndexed then 1 else 0)
                    IndirectBuffer = ib
                    IndirectCount = ibc
                    DrawCallCount = callCount
                    DrawCalls = pCalls
                }

        end


    [<StructLayout(LayoutKind.Sequential)>]
    type VertexBufferBinding =
        struct
            val mutable public FirstBinding : int
            val mutable public BindingCount : int
            val mutable public Buffers : nativeptr<VkBuffer>
            val mutable public Offsets : nativeptr<uint64>

            member x.Dispose() =
                if not (NativePtr.isNull x.Buffers) then
                    NativePtr.free x.Buffers
                    x.Buffers <- NativePtr.zero

                if not (NativePtr.isNull x.Offsets) then
                    NativePtr.free x.Offsets
                    x.Offsets <- NativePtr.zero

                x.FirstBinding <- 0
                x.BindingCount <- 0

            interface IDisposable with
                member x.Dispose() = x.Dispose()

            member x.TryUpdate(first : int, buffers : array<VkBuffer>, offsets : int64[]) =
                if x.FirstBinding = first && buffers.Length = x.BindingCount then
                    let count = x.BindingCount
                    for i in 0 .. count-1 do
                        NativePtr.set x.Buffers i (buffers.[i])
                        NativePtr.set x.Offsets i (uint64 offsets.[i])
                    true
                else
                    false

            new(first : int, buffers : array<VkBuffer>, offsets : int64[]) =
                let count = buffers.Length
                let pBuffers = NativePtr.alloc count
                let pOffsets = NativePtr.alloc count

                for i in 0 .. count-1 do
                    NativePtr.set pBuffers i (buffers.[i])
                    NativePtr.set pOffsets i (uint64 offsets.[i])

                {
                    FirstBinding = first
                    BindingCount = count
                    Buffers = pBuffers
                    Offsets = pOffsets
                }

            new(first : int, buffersAndOffsets : array<VkBuffer * int64>) =
                let count = buffersAndOffsets.Length
                let pBuffers = NativePtr.alloc count
                let pOffsets = NativePtr.alloc count

                for i in 0 .. buffersAndOffsets.Length-1 do
                    let (b, o) = buffersAndOffsets.[i]
                    NativePtr.set pBuffers i b
                    NativePtr.set pOffsets i (uint64 o)

                {
                    FirstBinding = first
                    BindingCount = count
                    Buffers = pBuffers
                    Offsets = pOffsets
                }
        end


    [<StructLayout(LayoutKind.Sequential)>]
    type DescriptorSetBinding =
        struct
            val mutable public FirstIndex : int
            val mutable public Count : int
            val mutable public BindPoint : VkPipelineBindPoint
            val mutable public Layout : VkPipelineLayout
            val mutable public Sets : nativeptr<VkDescriptorSet>

            member x.Dispose() =
                if not (NativePtr.isNull x.Sets) then
                    NativePtr.free x.Sets
                    x.Sets <- NativePtr.zero

                x.Layout <- VkPipelineLayout.Null
                x.FirstIndex <- 0
                x.Count <- 0

            interface IDisposable with
                member x.Dispose() = x.Dispose()

            new(bindPoint : VkPipelineBindPoint, layout : VkPipelineLayout, first : int, sets : array<VkDescriptorSet>) =
                let count = sets.Length
                let pSets = NativePtr.alloc count

                for i in 0 .. count-1 do
                    let s = sets.[i]
                    NativePtr.set pSets i s

                {
                    FirstIndex = first
                    Count = count
                    BindPoint = bindPoint
                    Layout = layout
                    Sets = pSets
                }

            new(bindPoint : VkPipelineBindPoint, layout : VkPipelineLayout, first : int, count : int) =
                let pSets = NativePtr.alloc count

                {
                    FirstIndex = first
                    Count = count
                    BindPoint = bindPoint
                    Layout = layout
                    Sets = pSets
                }
        end


    [<StructLayout(LayoutKind.Sequential)>]
    type IndexBufferBinding =
        struct
            val mutable public Buffer : VkBuffer
            val mutable public Offset : VkDeviceSize
            val mutable public Type : VkIndexType

            new(b : VkBuffer, t : VkIndexType) = { Buffer = b; Offset = 0UL; Type = t }
        end
    

module VKVM = 

    type CommandFragment =
        struct
            val mutable public CommandCount : uint32
            val mutable public Commands : nativeint
            val mutable public Next : nativeptr<CommandFragment>

            new(count, commands, next) = { CommandCount = count; Commands = commands; Next = next }
        end


    [<AutoOpen>]
    module Types = 
        type CommandType =
            | BindPipeline = 1
            | SetViewport = 2
            | SetScissor = 3
            | SetLineWidth = 4
            | SetDepthBias = 5
            | SetBlendConstants = 6
            | SetDepthBounds = 7
            | SetStencilCompareMask = 8
            | SetStencilWriteMask = 9
            | SetStencilReference = 10
            | BindDescriptorSets = 11
            | BindIndexBuffer = 12
            | BindVertexBuffers = 13
            | Draw = 14
            | DrawIndexed = 15
            | DrawIndirect = 16
            | DrawIndexedIndirect = 17
            | Dispatch = 18
            | DispatchIndirect = 19
            | CopyBuffer = 20
            | CopyImage = 21
            | BlitImage = 22
            | CopyBufferToImage = 23
            | CopyImageToBuffer = 24
            | UpdateBuffer = 25
            | FillBuffer = 26
            | ClearColorImage = 27
            | ClearDepthStencilImage = 28
            | ClearAttachments = 29
            | ResolveImage = 30
            | SetEvent = 31
            | ResetEvent = 32
            | WaitEvents = 33
            | PipelineBarrier = 34
            | BeginQuery = 35
            | EndQuery = 36
            | ResetQueryPool = 37
            | WriteTimestamp = 38
            | CopyQueryPoolResults = 39
            | PushConstants = 40
            | BeginRenderPass = 41
            | NextSubpass = 42
            | EndRenderPass = 43
            | ExecuteCommands = 44
 
            | CallFragment = 100
            | Custom = 101
    
            | IndirectBindPipeline = 102
            | IndirectBindDescriptorSets = 103
            | IndirectBindIndexBuffer = 104
            | IndirectBindVertexBuffers = 105
            | IndirectDraw = 106

        [<StructLayout(LayoutKind.Sequential)>]
        type BindPipelineCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public PipelineBindPoint : VkPipelineBindPoint
                val mutable public Pipeline : VkPipeline
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type SetViewportCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public FirstViewport : uint32
                val mutable public ViewportCount : uint32
                val mutable public Viewports : nativeptr<VkViewport>
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type SetScissorCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public FirstScissor : uint32
                val mutable public ScissorCount : uint32
                val mutable public Scissors : nativeptr<VkRect2D>
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type SetLineWidthCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public LineWidth : float32
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type SetDepthBiasCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public DepthBiasConstantFactor : float32
                val mutable public DepthBiasClamp : float32
                val mutable public DepthBiasSlopeFactor : float32
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type SetBlendConstantsCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public BlendConstants : C4f
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type SetDepthBoundsCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public MinDepth : float32
                val mutable public MaxDepth : float32
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type SetStencilCompareMaskCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public FaceMask : VkStencilFaceFlags
                val mutable public CompareMask : uint32
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type SetStencilWriteMaskCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public FaceMask : VkStencilFaceFlags
                val mutable public WriteMask : uint32
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type SetStencilReferenceCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public FaceMask : VkStencilFaceFlags
                val mutable public Reference : uint32
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type BindDescriptorSetsCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public PipelineBindPoint : VkPipelineBindPoint
                val mutable public Layout : VkPipelineLayout
                val mutable public FirstSet : uint32
                val mutable public SetCount : uint32
                val mutable public DescriptorSets : nativeptr<VkDescriptorSet>
                val mutable public DynamicOffsetCount : uint32
                val mutable public DynamicOffsets : nativeptr<uint32>
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type BindIndexBufferCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public Buffer : VkBuffer
                val mutable public Offset : VkDeviceSize
                val mutable public IndexType : VkIndexType
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type BindVertexBuffersCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public FirstBinding : uint32
                val mutable public BindingCount : uint32
                val mutable public Buffers : nativeptr<VkBuffer>
                val mutable public Offsets : nativeptr<VkDeviceSize>
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type DrawCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public VertexCount : uint32
                val mutable public InstanceCount : uint32
                val mutable public FirstVertex : uint32
                val mutable public FirstInstance : uint32
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type DrawIndexedCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public IndexCount : uint32
                val mutable public InstanceCount : uint32
                val mutable public FirstIndex : uint32
                val mutable public VertexOffset : int32
                val mutable public FirstInstance : uint32
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type DrawIndirectCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public Buffer : VkBuffer
                val mutable public Offset : VkDeviceSize
                val mutable public DrawCount : uint32
                val mutable public Stride : uint32
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type DrawIndexedIndirectCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public Buffer : VkBuffer
                val mutable public Offset : VkDeviceSize
                val mutable public DrawCount : uint32
                val mutable public Stride : uint32
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type DispatchCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public GroupCountX : uint32
                val mutable public GroupCountY : uint32
                val mutable public GroupCountZ : uint32
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type DispatchIndirectCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public Buffer : VkBuffer
                val mutable public Offset : VkDeviceSize
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type CopyBufferCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public SrcBuffer : VkBuffer
                val mutable public DstBuffer : VkBuffer
                val mutable public RegionCount : uint32
                val mutable public Regions : nativeptr<VkBufferCopy>
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type CopyImageCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public SrcImage : VkImage
                val mutable public SrcImageLayout : VkImageLayout
                val mutable public DstImage : VkImage
                val mutable public DstImageLayout : VkImageLayout
                val mutable public RegionCount : uint32
                val mutable public Regions : nativeptr<VkImageCopy>
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type BlitImageCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public SrcImage : VkImage
                val mutable public SrcImageLayout : VkImageLayout
                val mutable public DstImage : VkImage
                val mutable public DstImageLayout : VkImageLayout
                val mutable public RegionCount : uint32
                val mutable public Regions : nativeptr<VkImageBlit>
                val mutable public Filter : VkFilter
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type CopyBufferToImageCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public SrcBuffer : VkBuffer
                val mutable public DstImage : VkImage
                val mutable public DstImageLayout : VkImageLayout
                val mutable public RegionCount : uint32
                val mutable public Regions : nativeptr<VkBufferImageCopy>
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type CopyImageToBufferCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public SrcImage : VkImage
                val mutable public SrcImageLayout : VkImageLayout
                val mutable public DstBuffer : VkBuffer
                val mutable public RegionCount : uint32
                val mutable public Regions : nativeptr<VkBufferImageCopy>
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type UpdateBufferCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public DstBuffer : VkBuffer
                val mutable public DstOffset : VkDeviceSize
                val mutable public DataSize : VkDeviceSize
                val mutable public Data : nativeint
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type FillBufferCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public DstBuffer : VkBuffer
                val mutable public DstOffset : VkDeviceSize
                val mutable public Size : VkDeviceSize
                val mutable public Data : uint32
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type ClearColorImageCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public Image : VkImage
                val mutable public ImageLayout : VkImageLayout
                val mutable public Color : nativeptr<VkClearColorValue>
                val mutable public RangeCount : uint32
                val mutable public Ranges : nativeptr<VkImageSubresourceRange>
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type ClearDepthStencilImageCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public Image : VkImage
                val mutable public ImageLayout : VkImageLayout
                val mutable public DepthStencil : nativeptr<VkClearDepthStencilValue>
                val mutable public RangeCount : uint32
                val mutable public Ranges : nativeptr<VkImageSubresourceRange>
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type ClearAttachmentsCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public AttachmentCount : uint32
                val mutable public Attachments : nativeptr<VkClearAttachment>
                val mutable public RectCount : uint32
                val mutable public Rects : nativeptr<VkClearRect>
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type ResolveImageCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public SrcImage : VkImage
                val mutable public SrcImageLayout : VkImageLayout
                val mutable public DstImage : VkImage
                val mutable public DstImageLayout : VkImageLayout
                val mutable public RegionCount : uint32
                val mutable public Regions : nativeptr<VkImageResolve>
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type SetEventCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public Event : VkEvent
                val mutable public StageMask : VkPipelineStageFlags
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type ResetEventCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public Event : VkEvent
                val mutable public StageMask : VkPipelineStageFlags
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type WaitEventsCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public EventCount : uint32
                val mutable public Events : nativeptr<VkEvent>
                val mutable public SrcStageMask : VkPipelineStageFlags
                val mutable public DstStageMask : VkPipelineStageFlags
                val mutable public MemoryBarrierCount : uint32
                val mutable public MemoryBarriers : nativeptr<VkMemoryBarrier>
                val mutable public BufferMemoryBarrierCount : uint32
                val mutable public BufferMemoryBarriers : nativeptr<VkBufferMemoryBarrier>
                val mutable public ImageMemoryBarrierCount : uint32
                val mutable public ImageMemoryBarriers : nativeptr<VkImageMemoryBarrier>
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type PipelineBarrierCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public SrcStageMask : VkPipelineStageFlags
                val mutable public DstStageMask : VkPipelineStageFlags
                val mutable public DependencyFlags : VkDependencyFlags
                val mutable public MemoryBarrierCount : uint32
                val mutable public MemoryBarriers : nativeptr<VkMemoryBarrier>
                val mutable public BufferMemoryBarrierCount : uint32
                val mutable public BufferMemoryBarriers : nativeptr<VkBufferMemoryBarrier>
                val mutable public ImageMemoryBarrierCount : uint32
                val mutable public ImageMemoryBarriers : nativeptr<VkImageMemoryBarrier>
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type BeginQueryCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public QueryPool : VkQueryPool
                val mutable public Query : uint32
                val mutable public Flags : VkQueryControlFlags
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type EndQueryCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public QueryPool : VkQueryPool
                val mutable public Query : uint32
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type ResetQueryPoolCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public QueryPool : VkQueryPool
                val mutable public FirstQuery : uint32
                val mutable public QueryCount : uint32
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type WriteTimestampCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public PipelineStage : VkPipelineStageFlags
                val mutable public QueryPool : VkQueryPool
                val mutable public Query : uint32
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type CopyQueryPoolResultsCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public QueryPool : VkQueryPool
                val mutable public FirstQuery : uint32
                val mutable public QueryCount : uint32
                val mutable public DstBuffer : VkBuffer
                val mutable public DstOffset : VkDeviceSize
                val mutable public Stride : VkDeviceSize
                val mutable public Flags : VkQueryResultFlags
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type PushConstantsCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public Layout : VkPipelineLayout
                val mutable public StageFlags : VkShaderStageFlags
                val mutable public Offset : uint32
                val mutable public Size : uint32
                val mutable public Values : nativeint
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type BeginRenderPassCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public RenderPassBegin : nativeptr<VkRenderPassBeginInfo>
                val mutable public Contents : VkSubpassContents
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type NextSubpassCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public Contents : VkSubpassContents
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type EndRenderPassCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type ExecuteCommandsCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public CommandBufferCount : uint32
                val mutable public CommandBuffers : nativeptr<VkCommandBuffer>
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type CallFragmentCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public FragmentToCall : nativeptr<CommandFragment>
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type CustomCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType        
                val mutable public Run : nativeint
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type IndirectBindPipelineCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType  
                val mutable public PipelineBindPoint : VkPipelineBindPoint
                val mutable public Pipeline : nativeptr<VkPipeline>
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type IndirectBindDescriptorSetsCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType  
                val mutable public Binding : nativeptr<DescriptorSetBinding>
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type IndirectBindIndexBufferCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType  
                val mutable public Binding : nativeptr<IndexBufferBinding>
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type IndirectBindVertexBuffersCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType  
                val mutable public Binding : nativeptr<VertexBufferBinding>
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type IndirectDrawCommand =
            struct
                val mutable public Length : uint32
                val mutable public OpCode : CommandType  
                val mutable public Stats : nativeptr<V2i>
                val mutable public IsActive : nativeptr<int>
                val mutable public Calls : nativeptr<DrawCall>
            end

    [<AutoOpen>]
    module VM =
        [<DllImport("vkvm")>]
        extern void vmRun(VkCommandBuffer cmd, CommandFragment* fragment)

    [<AutoOpen>]
    module private Helpers =
        let inline usizeof<'a> = uint32 sizeof<'a>
        let inline nsizeof<'a> = nativeint sizeof<'a>

    type CommandStream() =
        static let ptrSize = nsizeof<nativeint>

        let mutable capacity = 0n
        let mutable length = 0n
        let mutable count = 0u
        let mutable position = 0n

        let mutable prev : CommandStream voption = ValueNone
        let mutable next : CommandStream voption = ValueNone

        let mutable handle = 
            let handle = NativePtr.alloc 1
            NativePtr.write handle (CommandFragment(0u, 0n, NativePtr.zero))
            handle

        let update (f : CommandFragment -> CommandFragment) =
            let h = NativePtr.read handle
            let r = f h
            NativePtr.write handle r

        member private x.HandleCount
            with get() : uint32 = NativePtr.read (NativePtr.cast handle)
            and set (v : uint32) = NativePtr.write (NativePtr.cast handle) v

        member private x.HandleNext
            with get() : nativeptr<CommandFragment> = NativePtr.read (NativePtr.ofNativeInt (8n + ptrSize + NativePtr.toNativeInt handle))
            and set (c : nativeptr<CommandFragment>) = NativePtr.write (NativePtr.ofNativeInt (8n + ptrSize + NativePtr.toNativeInt handle)) c

        member private x.HandleCommands
            with get() : nativeint = NativePtr.read (NativePtr.ofNativeInt (8n + NativePtr.toNativeInt handle))
            and set (c : nativeint) = NativePtr.write (NativePtr.ofNativeInt (8n + NativePtr.toNativeInt handle)) c
            
        member private x.Append<'r>(size : int, f : nativeint -> unit) =
            let size = nativeint size
            let e = position + size
            if e > capacity then
                let newCapacity = Fun.NextPowerOfTwo (int64 e) |> nativeint
                update (fun h ->
                    let mutable h = h
                    let ptr = 
                        if h.Commands = 0n then 
                            Marshal.AllocHGlobal(newCapacity)
                        else 
                            let n = Marshal.AllocHGlobal(newCapacity)
                            Marshal.Copy(h.Commands, n, length)
                            Marshal.FreeHGlobal h.Commands
                            n
                    h.Commands <- ptr
                    h
                )
                capacity <- newCapacity

            let ptr = x.HandleCommands + position
            f ptr

            let offset = position
            position <- e
            if e >= length then
                length <- e
                count <- count + 1u
                x.HandleCount <- count

            offset

        [<MethodImpl(MethodImplOptions.NoInlining)>]
        member private x.Append<'a when 'a : unmanaged>(data : byref<'a>) =
            let size : uint32 = NativePtr.read (NativePtr.cast &&data)
            let data = data
            x.Append(int size, fun ptr ->
                NativeInt.write ptr data
            )

        member x.Count = count
        member x.IsEmpty = count = 0u

        member x.Prev
            with get() = prev
            and private set p = prev <- p

        member x.Next
            with get() = next
            and set (n : CommandStream voption) =
                match n with
                | ValueSome n ->
                    n.Prev <- ValueSome x
                    next <- ValueSome n
                    x.HandleNext <- n.Handle
                | ValueNone ->
                    match next with
                    | ValueSome n ->
                        n.Prev <- ValueNone
                        next <- ValueNone
                    | ValueNone -> ()
                    x.HandleNext <- NativePtr.zero

        member x.Clear() =
            if count > 0u then
                update (fun f ->
                    let mutable f = f
                    if f.Commands <> 0n then
                        Marshal.FreeHGlobal f.Commands
                        f.Commands <- 0n
                    f.CommandCount <- 0u
                    capacity <- 0n
                    length <- 0n
                    count <- 0u
                    position <- 0n


                    f
                )

        member x.Dispose() =
            if NativePtr.isNull handle then
                Log.warn "double free"
            else
                x.Clear()
                NativePtr.free handle
                handle <- NativePtr.zero

        member x.BindPipeline(pipelineBindPoint : VkPipelineBindPoint, pipeline : VkPipeline) =
            let mutable cmd = 
                BindPipelineCommand(
                    Length = usizeof<BindPipelineCommand>,
                    OpCode = CommandType.BindPipeline,
                    PipelineBindPoint = pipelineBindPoint,
                    Pipeline = pipeline
                )
            x.Append(&cmd)
        
        member x.SetViewport(first : uint32, viewports : VkViewport[]) =
            let count = viewports.Length
            let size = sizeof<SetViewportCommand> + sizeof<VkViewport> * count
            x.Append(size, fun ptr ->
                let pViewports = NativePtr.ofNativeInt (ptr + nsizeof<SetViewportCommand>)

                let mutable cmd = Unchecked.defaultof<SetViewportCommand>
                cmd.Length <- uint32 size
                cmd.OpCode <- CommandType.SetViewport
                cmd.FirstViewport <- first
                cmd.ViewportCount <- uint32 count
                cmd.Viewports <- NativePtr.ofNativeInt (nsizeof<SetViewportCommand>)
                NativeInt.write ptr cmd

                for i in 0 .. count - 1 do
                    NativePtr.set pViewports i viewports.[i]

            )

        member x.SetScissor(first : uint32, scissors : VkRect2D[]) =
            let count = scissors.Length
            let size = sizeof<SetScissorCommand> + sizeof<VkRect2D> * count
            x.Append(size, fun ptr ->
                let pScissors = NativePtr.ofNativeInt (ptr + nativeint sizeof<SetScissorCommand>)
                
                let mutable cmd = Unchecked.defaultof<SetScissorCommand>
                cmd.Length <- uint32 size
                cmd.OpCode <- CommandType.SetScissor
                cmd.FirstScissor <- first
                cmd.ScissorCount <- uint32 count
                cmd.Scissors <- NativePtr.ofNativeInt (nativeint sizeof<SetScissorCommand>)
                NativeInt.write ptr cmd
                
                for i in 0 .. count - 1 do
                    NativePtr.set pScissors i scissors.[i]
            )      

        member x.SetLineWidth(width : float32) =
            let mutable cmd =
                SetLineWidthCommand(
                    Length = usizeof<SetLineWidthCommand>,
                    OpCode = CommandType.SetLineWidth,
                    LineWidth = width
                )
            x.Append(&cmd)

        member x.SetDepthBias(depthBiasConstantFactor : float32, depthBiasClamp : float32, depthBiasSlopeFactor : float32) =
            let mutable cmd =
                SetDepthBiasCommand(
                    Length = usizeof<SetDepthBiasCommand>,
                    OpCode = CommandType.SetDepthBias,
                    DepthBiasConstantFactor = depthBiasConstantFactor,
                    DepthBiasClamp = depthBiasClamp,
                    DepthBiasSlopeFactor = depthBiasSlopeFactor
                )
            x.Append(&cmd)

        member x.SetBlendConstants(color : C4f) =
            let mutable cmd =
                SetBlendConstantsCommand(
                    Length = usizeof<SetBlendConstantsCommand>,
                    OpCode = CommandType.SetBlendConstants,
                    BlendConstants = color
                )
            x.Append(&cmd)

        member x.SetDepthBounds(min : float32, max : float32) =
            let mutable cmd =
                SetDepthBoundsCommand(
                    Length = usizeof<SetDepthBoundsCommand>,
                    OpCode = CommandType.SetDepthBounds,
                    MinDepth = min,
                    MaxDepth = max
                )
            x.Append(&cmd)

        member x.SetStencilCompareMask(faceMask : VkStencilFaceFlags, compareMask : uint32) =
            let mutable cmd = 
                SetStencilCompareMaskCommand(
                    Length = usizeof<SetStencilCompareMaskCommand>,
                    OpCode = CommandType.SetStencilCompareMask,
                    FaceMask = faceMask,
                    CompareMask = compareMask
                )
            x.Append(&cmd)

        member x.SetStencilWriteMask(faceMask : VkStencilFaceFlags, writeMask : uint32) =
            let mutable cmd = 
                SetStencilWriteMaskCommand(
                    Length = usizeof<SetStencilWriteMaskCommand>,
                    OpCode = CommandType.SetStencilWriteMask,
                    FaceMask = faceMask,
                    WriteMask = writeMask
                )
            x.Append(&cmd)

        member x.SetStencilReference(faceMask : VkStencilFaceFlags, reference : uint32) =
            let mutable cmd = 
                SetStencilReferenceCommand(
                    Length = usizeof<SetStencilReferenceCommand>,
                    OpCode = CommandType.SetStencilReference,
                    FaceMask = faceMask,
                    Reference = reference
                )
            x.Append(&cmd)

        member x.BindDescriptorSets(bindPoint : VkPipelineBindPoint, layout : VkPipelineLayout, firstSet : uint32, sets : VkDescriptorSet[], dynamicOffsets : uint32[]) =
            let setCount = sets.Length
            let offsetCount = dynamicOffsets.Length

            let baseSize = sizeof<BindDescriptorSetsCommand>
            let setSize = sizeof<VkDescriptorSet> * setCount
            let offsetSize = sizeof<uint32> * offsetCount

            let size = baseSize + setSize + offsetSize
            x.Append(size, fun ptr ->
                let pSets = NativePtr.ofNativeInt (ptr + nativeint baseSize)
                let pOffsets = NativePtr.ofNativeInt (ptr + nativeint baseSize + nativeint setSize)

                let mutable cmd = Unchecked.defaultof<BindDescriptorSetsCommand>
                cmd.Length <- uint32 size
                cmd.OpCode <- CommandType.BindDescriptorSets
                cmd.PipelineBindPoint <- bindPoint
                cmd.Layout <- layout
                cmd.FirstSet <- firstSet
                cmd.SetCount <- uint32 setCount
                cmd.DescriptorSets <- NativePtr.ofNativeInt (nativeint baseSize)
                cmd.DynamicOffsetCount <- uint32 offsetCount
                cmd.DynamicOffsets <- NativePtr.ofNativeInt (nativeint baseSize + nativeint setSize)

                NativeInt.write ptr cmd

                for i in 0 .. setCount - 1 do
                    NativePtr.set pSets i sets.[i]

                for i in 0 .. offsetCount - 1 do
                    NativePtr.set pOffsets i dynamicOffsets.[i]

            )

        member x.BindIndexBuffer(buffer : VkBuffer, offset : VkDeviceSize, indexType : VkIndexType) =
            let mutable cmd =
                BindIndexBufferCommand(
                    Length = usizeof<BindIndexBufferCommand>,
                    OpCode = CommandType.BindIndexBuffer,
                    Buffer = buffer,
                    Offset = offset,
                    IndexType = indexType
                )
            x.Append(&cmd)

        member x.BindVertexBuffers(first : uint32, buffers : VkBuffer[], offsets : VkDeviceSize[]) =
            let count = min buffers.Length offsets.Length

            let baseSize = sizeof<BindVertexBuffersCommand>
            let bufferSize = count * sizeof<VkBuffer>
            let offsetSize = count * sizeof<VkDeviceSize>

            let size = baseSize + bufferSize + offsetSize
            x.Append(size, fun ptr ->
                let pBuffers = NativePtr.ofNativeInt (ptr + nativeint baseSize)
                let pOffsets = NativePtr.ofNativeInt (ptr + nativeint baseSize + nativeint bufferSize)
                
                let mutable cmd = Unchecked.defaultof<BindVertexBuffersCommand>
                cmd.Length <- uint32 size
                cmd.OpCode <- CommandType.BindVertexBuffers
                cmd.FirstBinding <- first
                cmd.BindingCount <- uint32 count
                cmd.Buffers <-  NativePtr.ofNativeInt (nativeint baseSize)
                cmd.Offsets <- NativePtr.ofNativeInt (nativeint baseSize + nativeint bufferSize)
                NativeInt.write ptr cmd

                for i in 0 .. count - 1 do
                    NativePtr.set pBuffers i buffers.[i]
                    NativePtr.set pOffsets i offsets.[i]

            )

        member x.Draw(vertexCount : uint32, instanceCount : uint32, firstVertex : uint32, firstInstance : uint32) =
            let mutable cmd =
                DrawCommand(
                    Length = usizeof<DrawCommand>,
                    OpCode = CommandType.Draw,
                    VertexCount = vertexCount,
                    InstanceCount = instanceCount,
                    FirstVertex = firstVertex,
                    FirstInstance = firstInstance
                )
            x.Append(&cmd)

        member x.DrawIndexed(indexCount : uint32, instanceCount : uint32, firstIndex : uint32, vertexOffset : int, firstInstance : uint32) =
            let mutable cmd =
                DrawIndexedCommand(
                    Length = usizeof<DrawIndexedCommand>,
                    OpCode = CommandType.DrawIndexed,
                    IndexCount = indexCount,
                    InstanceCount = instanceCount,
                    FirstIndex = firstIndex,
                    VertexOffset = vertexOffset,
                    FirstInstance = firstInstance
                )
            x.Append(&cmd)

        member x.DrawIndirect(buffer : VkBuffer, offset : VkDeviceSize, drawCount : uint32, stride : uint32) =
            let mutable cmd =
                DrawIndirectCommand(
                    Length = usizeof<DrawIndirectCommand>,
                    OpCode = CommandType.DrawIndirect,
                    Buffer = buffer,
                    Offset = offset,
                    DrawCount = drawCount,
                    Stride = stride
                )
            x.Append(&cmd)

        member x.DrawIndexedIndirect(buffer : VkBuffer, offset : VkDeviceSize, drawCount : uint32, stride : uint32) =
            let mutable cmd =
                DrawIndexedIndirectCommand(
                    Length = usizeof<DrawIndexedIndirectCommand>,
                    OpCode = CommandType.DrawIndexedIndirect,
                    Buffer = buffer,
                    Offset = offset,
                    DrawCount = drawCount,
                    Stride = stride
                )
            x.Append(&cmd)

        member x.Dispatch(gx : uint32, gy : uint32, gz : uint32) =
            let mutable cmd =
                DispatchCommand(
                    Length = usizeof<DispatchCommand>,
                    OpCode = CommandType.Dispatch,
                    GroupCountX = gx,
                    GroupCountY = gy,
                    GroupCountZ = gz
                )
            x.Append(&cmd)

        member x.DispatchIndirect(buffer : VkBuffer, offset : VkDeviceSize) =
            let mutable cmd =
                DispatchIndirectCommand(
                    Length = usizeof<DispatchIndirectCommand>,
                    OpCode = CommandType.DispatchIndirect,
                    Buffer = buffer,
                    Offset = offset
                )
            x.Append(&cmd)

        member x.CopyImage(src : VkImage, srcLayout : VkImageLayout, dst : VkImage, dstLayout : VkImageLayout, regions : VkImageCopy[]) =
            let regionCount = regions.Length
            let baseSize = sizeof<CopyImageCommand>
            let regionSize = regionCount * sizeof<VkImageCopy>
            
            let size = baseSize + regionSize
            x.Append(size, fun ptr ->
                let pRegions = NativePtr.ofNativeInt (ptr + nativeint baseSize)

                let mutable cmd = Unchecked.defaultof<CopyImageCommand>
                cmd.Length <- uint32 size
                cmd.OpCode <- CommandType.CopyImage
                cmd.SrcImage <- src
                cmd.SrcImageLayout <- srcLayout
                cmd.DstImage <- dst
                cmd.DstImageLayout <- dstLayout
                cmd.RegionCount <- uint32 regionCount
                cmd.Regions <- NativePtr.ofNativeInt (nativeint baseSize)
                NativeInt.write ptr cmd

                for i in 0 .. regionCount - 1 do
                    NativePtr.set pRegions i regions.[i]
            )

        member x.BlitImage(src : VkImage, srcLayout : VkImageLayout, dst : VkImage, dstLayout : VkImageLayout, regions : VkImageBlit[], filter : VkFilter) =
            let regionCount = regions.Length
            let baseSize = sizeof<BlitImageCommand>
            let regionSize = regionCount * sizeof<VkImageBlit>
            
            let size = baseSize + regionSize
            x.Append(size, fun ptr ->
                let pRegions = NativePtr.ofNativeInt (ptr + nativeint baseSize)

                let mutable cmd = Unchecked.defaultof<BlitImageCommand>
                cmd.Length <- uint32 size
                cmd.OpCode <- CommandType.BlitImage
                cmd.SrcImage <- src
                cmd.SrcImageLayout <- srcLayout
                cmd.DstImage <- dst
                cmd.DstImageLayout <- dstLayout
                cmd.RegionCount <- uint32 regionCount
                cmd.Regions <- NativePtr.ofNativeInt (nativeint baseSize)
                cmd.Filter <- filter
                NativeInt.write ptr cmd

                for i in 0 .. regionCount - 1 do
                    NativePtr.set pRegions i regions.[i]
            )
        
        member x.CopyBuffer(src : VkBuffer, dst : VkBuffer, regions : VkBufferCopy[]) =
            let regionCount = regions.Length
            let baseSize = sizeof<CopyBufferCommand>
            let regionSize = regionCount * sizeof<VkBufferCopy>
            
            let size = baseSize + regionSize
            x.Append(size, fun ptr ->
                let pRegions = NativePtr.ofNativeInt (ptr + nativeint baseSize)

                let mutable cmd = Unchecked.defaultof<CopyBufferCommand>
                cmd.Length <- uint32 size
                cmd.OpCode <- CommandType.CopyBuffer
                cmd.SrcBuffer <- src
                cmd.DstBuffer <- dst
                cmd.RegionCount <- uint32 regionCount
                cmd.Regions <- NativePtr.ofNativeInt (nativeint baseSize)

                NativeInt.write ptr cmd

                for i in 0 .. regionCount - 1 do
                    NativePtr.set pRegions i regions.[i]
            )
        
        member x.CopyBufferToImage(src : VkBuffer, dst : VkImage, dstLayout : VkImageLayout, regions : VkBufferImageCopy[]) =
            let regionCount = regions.Length
            let baseSize = sizeof<CopyBufferToImageCommand>
            let regionSize = regionCount * sizeof<VkBufferImageCopy>
            
            let size = baseSize + regionSize
            x.Append(size, fun ptr ->
                let pRegions = NativePtr.ofNativeInt (ptr + nativeint baseSize)

                let mutable cmd = Unchecked.defaultof<CopyBufferToImageCommand>
                cmd.Length <- uint32 size
                cmd.OpCode <- CommandType.CopyBufferToImage
                cmd.SrcBuffer <- src
                cmd.DstImage <- dst
                cmd.DstImageLayout <- dstLayout
                cmd.RegionCount <- uint32 regionCount
                cmd.Regions <- NativePtr.ofNativeInt (nativeint baseSize)

                NativeInt.write ptr cmd

                for i in 0 .. regionCount - 1 do
                    NativePtr.set pRegions i regions.[i]
            )
        
        member x.CopyImageToBuffer(src : VkImage, srcLayout : VkImageLayout, dst : VkBuffer, regions : VkBufferImageCopy[]) =
            let regionCount = regions.Length
            let baseSize = sizeof<CopyImageToBufferCommand>
            let regionSize = regionCount * sizeof<VkBufferImageCopy>
            
            let size = baseSize + regionSize
            x.Append(size, fun ptr ->
                let pRegions = NativePtr.ofNativeInt (ptr + nativeint baseSize)

                let mutable cmd = Unchecked.defaultof<CopyImageToBufferCommand>
                cmd.Length <- uint32 size
                cmd.OpCode <- CommandType.CopyImageToBuffer
                cmd.SrcImage <- src
                cmd.SrcImageLayout <- srcLayout
                cmd.DstBuffer <- dst
                cmd.RegionCount <- uint32 regionCount
                cmd.Regions <- NativePtr.ofNativeInt (nativeint baseSize)

                NativeInt.write ptr cmd

                for i in 0 .. regionCount - 1 do
                    NativePtr.set pRegions i regions.[i]
            )

        member x.UpdateBuffer(buffer : VkBuffer, offset : VkDeviceSize, size : VkDeviceSize, data : nativeint) =
            let mutable cmd =
                UpdateBufferCommand(
                    Length = usizeof<UpdateBufferCommand>,
                    OpCode = CommandType.UpdateBuffer,
                    DstBuffer = buffer,
                    DstOffset = offset,
                    DataSize = size,
                    Data = data
                )
            x.Append(&cmd)

        member x.FillBuffer(buffer : VkBuffer, offset : VkDeviceSize, size : VkDeviceSize, data : uint32) =
            let mutable cmd =
                FillBufferCommand(
                    Length = usizeof<FillBufferCommand>,
                    OpCode = CommandType.FillBuffer,
                    DstBuffer = buffer,
                    DstOffset = offset,
                    Size = size,
                    Data = data
                )
            x.Append(&cmd)

        member x.ClearColorImage(image : VkImage, layout : VkImageLayout, colors : VkClearColorValue[], ranges : VkImageSubresourceRange[]) =
            let count = min colors.Length ranges.Length

            let baseSize = sizeof<ClearColorImageCommand>
            let valueSize = count * sizeof<VkClearColorValue>
            let rangeSize = count * sizeof<VkImageSubresourceRange>

            let size = baseSize + valueSize + rangeSize
            x.Append(size, fun ptr ->
                let pValues = NativePtr.ofNativeInt (ptr + nativeint baseSize)
                let pRanges = NativePtr.ofNativeInt (ptr + nativeint baseSize + nativeint valueSize)

                let mutable cmd = Unchecked.defaultof<ClearColorImageCommand>
                cmd.Length <- uint32 size
                cmd.OpCode <- CommandType.ClearColorImage
                cmd.Image <- image
                cmd.ImageLayout <- layout
                cmd.Color <- NativePtr.ofNativeInt (nativeint baseSize)
                cmd.RangeCount <- uint32 count
                cmd.Ranges <- NativePtr.ofNativeInt (nativeint baseSize + nativeint valueSize)
                NativeInt.write ptr cmd

                for i in 0 .. count - 1 do
                    NativePtr.set pValues i colors.[i]
                    NativePtr.set pRanges i ranges.[i]
            )

        member x.ClearDepthStencilImage(image : VkImage, layout : VkImageLayout, values : VkClearDepthStencilValue[], ranges : VkImageSubresourceRange[]) =
            let count = min values.Length ranges.Length

            let baseSize = sizeof<ClearDepthStencilImageCommand>
            let valueSize = count * sizeof<VkClearDepthStencilValue>
            let rangeSize = count * sizeof<VkImageSubresourceRange>

            let size = baseSize + valueSize + rangeSize
            x.Append(size, fun ptr ->
                let pValues = NativePtr.ofNativeInt (ptr + nativeint baseSize)
                let pRanges = NativePtr.ofNativeInt (ptr + nativeint baseSize + nativeint valueSize)

                let mutable cmd = Unchecked.defaultof<ClearDepthStencilImageCommand>
                cmd.Length <- uint32 size
                cmd.OpCode <- CommandType.ClearDepthStencilImage
                cmd.Image <- image
                cmd.ImageLayout <- layout
                cmd.DepthStencil <- NativePtr.ofNativeInt (nativeint baseSize)
                cmd.RangeCount <- uint32 count
                cmd.Ranges <- NativePtr.ofNativeInt (nativeint baseSize + nativeint valueSize)
                NativeInt.write ptr cmd

                for i in 0 .. count - 1 do
                    NativePtr.set pValues i values.[i]
                    NativePtr.set pRanges i ranges.[i]
            )

        member x.ClearAttachments(attachments : VkClearAttachment[], rects : VkClearRect[]) =
            let attCount = attachments.Length
            let rectCount = rects.Length

            let baseSize = sizeof<ClearAttachmentsCommand>
            let attSize = attCount * sizeof<VkClearAttachment>
            let rectSize = rectCount * sizeof<VkClearRect>

            let size = baseSize + attSize + rectSize
            x.Append(size, fun ptr ->
                let pAtt = NativePtr.ofNativeInt (ptr + nativeint baseSize)
                let pRect = NativePtr.ofNativeInt (ptr + nativeint baseSize + nativeint attSize)

                let mutable cmd = Unchecked.defaultof<ClearAttachmentsCommand>
                cmd.Length <- uint32 size
                cmd.OpCode <- CommandType.ClearAttachments
                cmd.AttachmentCount <- uint32 attCount
                cmd.Attachments <- NativePtr.ofNativeInt (nativeint baseSize)
                cmd.RectCount <- uint32 rectCount
                cmd.Rects <- NativePtr.ofNativeInt (nativeint baseSize + nativeint attSize)
                NativeInt.write ptr cmd

                for i in 0 .. attCount - 1 do NativePtr.set pAtt i attachments.[i]
                for i in 0 .. rectCount - 1 do NativePtr.set pRect i rects.[i]
            )

        member x.ResolveImage(src : VkImage, srcLayout : VkImageLayout, dst : VkImage, dstLayout : VkImageLayout, regions : VkImageResolve[]) =
            let regionCount = regions.Length
            let baseSize = sizeof<ResolveImageCommand>
            let regionSize = regionCount * sizeof<VkImageResolve>
            
            let size = baseSize + regionSize
            x.Append(size, fun ptr ->
                let pRegions = NativePtr.ofNativeInt (ptr + nativeint baseSize)

                let mutable cmd = Unchecked.defaultof<ResolveImageCommand>
                cmd.Length <- uint32 size
                cmd.OpCode <- CommandType.ResolveImage
                cmd.SrcImage <- src
                cmd.SrcImageLayout <- srcLayout
                cmd.DstImage <- dst
                cmd.DstImageLayout <- dstLayout
                cmd.RegionCount <- uint32 regionCount
                cmd.Regions <- NativePtr.ofNativeInt (nativeint baseSize)
                NativeInt.write ptr cmd

                for i in 0 .. regionCount - 1 do
                    NativePtr.set pRegions i regions.[i]
            )

        member x.SetEvent(evt : VkEvent, stageMask : VkPipelineStageFlags) =
            let mutable cmd =
                SetEventCommand(
                    Length = usizeof<SetEventCommand>,
                    OpCode = CommandType.SetEvent,
                    Event = evt,
                    StageMask = stageMask
                )
            x.Append(&cmd)

        member x.ResetEvent(evt : VkEvent, stageMask : VkPipelineStageFlags) =
            let mutable cmd =
                ResetEventCommand(
                    Length = usizeof<ResetEventCommand>,
                    OpCode = CommandType.ResetEvent,
                    Event = evt,
                    StageMask = stageMask
                )
            x.Append(&cmd)

        member x.WaitEvents(events : VkEvent[], srcStageMask : VkPipelineStageFlags, dstStageMask : VkPipelineStageFlags, memoryBarriers : VkMemoryBarrier[], bufferBarriers : VkBufferMemoryBarrier[], imageBarriers : VkImageMemoryBarrier[]) =
            let eCount = events.Length
            let mCount = memoryBarriers.Length
            let bCount = bufferBarriers.Length
            let iCount = imageBarriers.Length

            let baseSize = sizeof<WaitEventsCommand>
            let eSize = eCount * sizeof<VkEvent>
            let mSize = mCount * sizeof<VkMemoryBarrier>
            let bSize = bCount * sizeof<VkBufferMemoryBarrier>
            let iSize = iCount * sizeof<VkImageMemoryBarrier>

            let size = baseSize + eSize + mSize + bSize + iSize
            x.Append(size, fun ptr ->
                let ePtr = NativePtr.ofNativeInt (ptr + nativeint baseSize)
                let mPtr = NativePtr.ofNativeInt (ptr + nativeint baseSize + nativeint eSize)
                let bPtr = NativePtr.ofNativeInt (ptr + nativeint baseSize + nativeint eSize + nativeint mSize)
                let iPtr = NativePtr.ofNativeInt (ptr + nativeint baseSize + nativeint eSize + nativeint mSize + nativeint bSize)

                let ePtr0 = NativePtr.ofNativeInt (nativeint baseSize)
                let mPtr0 = NativePtr.ofNativeInt (nativeint baseSize + nativeint eSize)
                let bPtr0 = NativePtr.ofNativeInt (nativeint baseSize + nativeint eSize + nativeint mSize)
                let iPtr0 = NativePtr.ofNativeInt (nativeint baseSize + nativeint eSize + nativeint mSize + nativeint bSize)


                let mutable cmd = Unchecked.defaultof<WaitEventsCommand>
                cmd.Length <- uint32 size
                cmd.OpCode <- CommandType.WaitEvents
                cmd.EventCount <- uint32 eCount
                cmd.Events <- ePtr0
                cmd.SrcStageMask <- srcStageMask
                cmd.DstStageMask <- dstStageMask
                cmd.MemoryBarrierCount <- uint32 mCount
                cmd.MemoryBarriers <- mPtr0
                cmd.BufferMemoryBarrierCount <- uint32 bCount
                cmd.BufferMemoryBarriers <- bPtr0
                cmd.ImageMemoryBarrierCount <- uint32 iCount
                cmd.ImageMemoryBarriers <- iPtr0
                NativeInt.write ptr cmd

                for i in 0 .. eCount - 1 do NativePtr.set ePtr i events.[i]
                for i in 0 .. mCount - 1 do NativePtr.set mPtr i memoryBarriers.[i]
                for i in 0 .. bCount - 1 do NativePtr.set bPtr i bufferBarriers.[i]
                for i in 0 .. iCount - 1 do NativePtr.set iPtr i imageBarriers.[i]
            )

        member x.PipelineBarrier(srcStageMask : VkPipelineStageFlags, dstStageMask : VkPipelineStageFlags, memoryBarriers : VkMemoryBarrier[], bufferBarriers : VkBufferMemoryBarrier[], imageBarriers : VkImageMemoryBarrier[]) =
            let mCount = memoryBarriers.Length
            let bCount = bufferBarriers.Length
            let iCount = imageBarriers.Length

            let baseSize = sizeof<WaitEventsCommand>
            let mSize = mCount * sizeof<VkMemoryBarrier>
            let bSize = bCount * sizeof<VkBufferMemoryBarrier>
            let iSize = iCount * sizeof<VkImageMemoryBarrier>

            let size = baseSize + mSize + bSize + iSize
            x.Append(size, fun ptr ->
                let mPtr = NativePtr.ofNativeInt (ptr + nativeint baseSize)
                let bPtr = NativePtr.ofNativeInt (ptr + nativeint baseSize + nativeint mSize)
                let iPtr = NativePtr.ofNativeInt (ptr + nativeint baseSize + nativeint mSize + nativeint bSize)
                let mPtr0 = NativePtr.ofNativeInt (nativeint baseSize)
                let bPtr0 = NativePtr.ofNativeInt (nativeint baseSize + nativeint mSize)
                let iPtr0 = NativePtr.ofNativeInt (nativeint baseSize + nativeint mSize + nativeint bSize)



                let mutable cmd = Unchecked.defaultof<PipelineBarrierCommand>
                cmd.Length <- uint32 size
                cmd.OpCode <- CommandType.PipelineBarrier
                cmd.SrcStageMask <- srcStageMask
                cmd.DstStageMask <- dstStageMask
                cmd.MemoryBarrierCount <- uint32 mCount
                cmd.MemoryBarriers <- mPtr0
                cmd.BufferMemoryBarrierCount <- uint32 bCount
                cmd.BufferMemoryBarriers <- bPtr0
                cmd.ImageMemoryBarrierCount <- uint32 iCount
                cmd.ImageMemoryBarriers <- iPtr0
                NativeInt.write ptr cmd

                for i in 0 .. mCount - 1 do NativePtr.set mPtr i memoryBarriers.[i]
                for i in 0 .. bCount - 1 do NativePtr.set bPtr i bufferBarriers.[i]
                for i in 0 .. iCount - 1 do NativePtr.set iPtr i imageBarriers.[i]
            )



        member x.BeginQuery(pool : VkQueryPool, query : uint32, flags : VkQueryControlFlags) =
            let mutable cmd =
                BeginQueryCommand(
                    Length = usizeof<BeginQueryCommand>,
                    OpCode = CommandType.BeginQuery,
                    QueryPool = pool,
                    Query = query,
                    Flags = flags
                )
            x.Append(&cmd)
            
        member x.EndQuery(pool : VkQueryPool, query : uint32) =
            let mutable cmd =
                EndQueryCommand(
                    Length = usizeof<EndQueryCommand>,
                    OpCode = CommandType.EndQuery,
                    QueryPool = pool,
                    Query = query
                )
            x.Append(&cmd)
                      
        member x.ResetQueryPool(pool : VkQueryPool, firstQuery : uint32, queryCount : uint32) =
            let mutable cmd =
                ResetQueryPoolCommand(
                    Length = usizeof<ResetQueryPoolCommand>,
                    OpCode = CommandType.ResetQueryPool,
                    QueryPool = pool,
                    FirstQuery = firstQuery,
                    QueryCount = queryCount
                )
            x.Append(&cmd)
                  
        member x.WriteTimestamp(stage : VkPipelineStageFlags, pool : VkQueryPool, query : uint32) =
            let mutable cmd =
                WriteTimestampCommand(
                    Length = usizeof<WriteTimestampCommand>,
                    OpCode = CommandType.WriteTimestamp,
                    PipelineStage = stage,
                    QueryPool = pool,
                    Query = query
                )
            x.Append(&cmd)

        member x.CopyQueryPoolResults(pool : VkQueryPool, firstQuery : uint32, queryCount : uint32, dstBuffer : VkBuffer, dstOffset : VkDeviceSize, stride : VkDeviceSize, flags : VkQueryResultFlags) =
            let mutable cmd =
                CopyQueryPoolResultsCommand(
                    Length = usizeof<CopyQueryPoolResultsCommand>,
                    OpCode = CommandType.CopyQueryPoolResults,
                    QueryPool = pool,
                    FirstQuery = firstQuery,
                    QueryCount = queryCount,
                    DstBuffer = dstBuffer,
                    DstOffset = dstOffset,
                    Stride = stride,
                    Flags = flags
                )
            x.Append(&cmd)

        member x.PushConstants(layout : VkPipelineLayout, stageFlags : VkShaderStageFlags, offset : uint32, size : uint32, values : nativeint) =
            let mutable cmd =
                PushConstantsCommand(
                    Length = usizeof<PushConstantsCommand>,
                    OpCode = CommandType.PushConstants,
                    Layout = layout,
                    StageFlags = stageFlags,
                    Offset = offset,
                    Size = size,
                    Values = values
                )
            x.Append(&cmd)

        member x.BeginRenderPass(beginInfo : VkRenderPassBeginInfo, contents : VkSubpassContents) =
            let baseSize = sizeof<BeginRenderPassCommand>
            let infoSize = sizeof<VkRenderPassBeginInfo>
            let size = baseSize + infoSize
            x.Append(size, fun ptr ->
                let pInfo = NativePtr.ofNativeInt(ptr + nativeint baseSize)
                
                let mutable cmd = Unchecked.defaultof<BeginRenderPassCommand>
                cmd.Length <- uint32 size
                cmd.OpCode <- CommandType.BeginRenderPass
                cmd.RenderPassBegin <- NativePtr.ofNativeInt(nativeint baseSize)
                cmd.Contents <- contents
                NativeInt.write ptr cmd

                NativePtr.write pInfo beginInfo
            )

        member x.NextSubpass(contents : VkSubpassContents) =
            let mutable cmd =
                NextSubpassCommand(
                    Length = usizeof<NextSubpassCommand>,
                    OpCode = CommandType.NextSubpass,
                    Contents = contents
                )
            x.Append(&cmd)

        member x.EndRenderPass(id : int) =
            let mutable cmd =
                EndRenderPassCommand(
                    Length = usizeof<EndRenderPassCommand>,
                    OpCode = CommandType.EndRenderPass
                )
            x.Append(&cmd)

        member x.ExecuteCommands(buffers : VkCommandBuffer[]) =
            let bCount = buffers.Length
            let baseSize = sizeof<ExecuteCommandsCommand>
            let bSize = bCount * sizeof<VkCommandBuffer>

            let size = baseSize + bSize
            x.Append(size, fun ptr ->
                let bPtr = NativePtr.ofNativeInt (ptr + nativeint baseSize)

                let mutable cmd = Unchecked.defaultof<ExecuteCommandsCommand>
                cmd.Length <- uint32 size
                cmd.OpCode <- CommandType.ExecuteCommands
                cmd.CommandBufferCount <- uint32 bCount
                cmd.CommandBuffers <- NativePtr.ofNativeInt (nativeint baseSize)
                NativeInt.write ptr cmd

                for i in 0 .. bCount - 1 do NativePtr.set bPtr i buffers.[i]

            )


        member x.Call(other : CommandStream) =
            let mutable cmd = Unchecked.defaultof<CallFragmentCommand>
            cmd.Length <- usizeof<CallFragmentCommand>
            cmd.OpCode <- CommandType.CallFragment
            cmd.FragmentToCall <- other.Handle
            x.Append(&cmd)

        member x.Custom(fptr : nativeint) =
            let mutable cmd = Unchecked.defaultof<CustomCommand>
            cmd.Length <- usizeof<CallFragmentCommand>
            cmd.OpCode <- CommandType.Custom
            cmd.Run <- fptr
            x.Append(&cmd)


        member x.IndirectBindPipeline(bindPoint : VkPipelineBindPoint, pointer : nativeptr<VkPipeline>) =
            let mutable cmd = Unchecked.defaultof<IndirectBindPipelineCommand>
            cmd.Length <- usizeof<IndirectBindPipelineCommand>
            cmd.OpCode <- CommandType.IndirectBindPipeline
            cmd.PipelineBindPoint <- bindPoint
            cmd.Pipeline <- pointer
            x.Append(&cmd)

        member x.IndirectBindDescriptorSets(pointer : nativeptr<DescriptorSetBinding>) =
            let mutable cmd = Unchecked.defaultof<IndirectBindDescriptorSetsCommand>
            cmd.Length <- usizeof<IndirectBindDescriptorSetsCommand>
            cmd.OpCode <- CommandType.IndirectBindDescriptorSets
            cmd.Binding <- pointer
            x.Append(&cmd)

        member x.IndirectBindIndexBuffer(pointer : nativeptr<IndexBufferBinding>) =
            let mutable cmd = Unchecked.defaultof<IndirectBindIndexBufferCommand>
            cmd.Length <- usizeof<IndirectBindIndexBufferCommand>
            cmd.OpCode <- CommandType.IndirectBindIndexBuffer
            cmd.Binding <- pointer
            x.Append(&cmd)

        member x.IndirectBindVertexBuffers(pointer : nativeptr<VertexBufferBinding>) =
            let mutable cmd = Unchecked.defaultof<IndirectBindVertexBuffersCommand>
            cmd.Length <- usizeof<IndirectBindVertexBuffersCommand>
            cmd.OpCode <- CommandType.IndirectBindVertexBuffers
            cmd.Binding <- pointer
            x.Append(&cmd)
            

        member x.IndirectDraw(stats : nativeptr<V2i>, isActive : nativeptr<int>, calls : nativeptr<DrawCall>) =
            let mutable cmd = Unchecked.defaultof<IndirectDrawCommand>
            cmd.Length <- usizeof<IndirectDrawCommand>
            cmd.OpCode <- CommandType.IndirectDraw
            cmd.Stats <- stats
            cmd.IsActive <- isActive
            cmd.Calls <- calls
            x.Append(&cmd)

        member x.Position
            with get() = position
            and set p = position <- p
            
        member x.SeekToBegin() =
            position <- 0n

        member x.SeekToEnd() =
            position <- length

        member x.Handle = handle


        member x.Run(cmd : VkCommandBuffer) =
            VM.vmRun(cmd, handle)

        interface IDisposable with
            member x.Dispose() = x.Dispose()
   
        interface ILinked<CommandStream> with
            member x.Prev
                with get() = x.Prev
                and set p = x.Prev <- p

            member x.Next
                with get() = x.Next
                and set n = x.Next <- n