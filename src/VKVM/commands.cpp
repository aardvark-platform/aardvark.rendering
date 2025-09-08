#ifndef __GNUC__
#include "stdafx.h"
#endif

#include "commands.h"
#include <stdio.h>
#include <tuple>
#include <unordered_set>

#define get(t,v) ((t##Command*)(v)) 
#define getptr(t,v,r) (r*)(((char*)((t##Command*)data)->v) + (intptr_t)data) 

typedef struct {
	VkPipeline CurrentPipeline;
} CommandState;

static void enqueueCommand (CommandState* state, VkCommandBuffer buffer, CommandType op, void* data)
{
	VkPipeline pipe;

	switch (op)
	{
	case CmdBindPipeline:
		vkCmdBindPipeline(
			buffer,
			get(BindPipeline, data)->PipelineBindPoint,
			get(BindPipeline, data)->Pipeline
		);
		break;
	case CmdSetViewport:
		vkCmdSetViewport(
			buffer,
			get(SetViewport, data)->FirstViewport,
			get(SetViewport, data)->ViewportCount,
			get(SetViewport, data)->Viewports
		);
		break;
	case CmdSetScissor:
		vkCmdSetScissor(
			buffer,
			get(SetScissor, data)->FirstScissor,
			get(SetScissor, data)->ScissorCount,
			get(SetScissor, data)->Scissors
		);
		break;
	case CmdSetLineWidth:
		vkCmdSetLineWidth(
			buffer,
			get(SetLineWidth, data)->LineWidth
		);
		break;
	case CmdSetDepthBias:
		vkCmdSetDepthBias(
			buffer,
			get(SetDepthBias, data)->DepthBiasConstantFactor,
			get(SetDepthBias, data)->DepthBiasClamp,
			get(SetDepthBias, data)->DepthBiasSlopeFactor
		);
		break;
	case CmdSetBlendConstants:
		vkCmdSetBlendConstants(
			buffer,
			get(SetBlendConstants, data)->BlendConstants
		);
		break;
	case CmdSetDepthBounds:
		vkCmdSetDepthBounds(
			buffer,
			get(SetDepthBounds, data)->MinDepth,
			get(SetDepthBounds, data)->MaxDepth
		);
		break;
	case CmdSetStencilCompareMask:
		vkCmdSetStencilCompareMask(
			buffer,
			get(SetStencilCompareMask, data)->FaceMask,
			get(SetStencilCompareMask, data)->CompareMask
		);
		break;
	case CmdSetStencilWriteMask:
		vkCmdSetStencilWriteMask(
			buffer,
			get(SetStencilWriteMask, data)->FaceMask,
			get(SetStencilWriteMask, data)->WriteMask
		);
		break;
	case CmdSetStencilReference:
		vkCmdSetStencilReference(
			buffer,
			get(SetStencilReference, data)->FaceMask,
			get(SetStencilReference, data)->Reference
		);
		break;
	case CmdBindDescriptorSets:
		vkCmdBindDescriptorSets(
			buffer,
			get(BindDescriptorSets, data)->PipelineBindPoint,
			get(BindDescriptorSets, data)->Layout,
			get(BindDescriptorSets, data)->FirstSet,
			get(BindDescriptorSets, data)->SetCount,
			getptr(BindDescriptorSets, DescriptorSets, VkDescriptorSet),
			get(BindDescriptorSets, data)->DynamicOffsetCount,
			getptr(BindDescriptorSets, DynamicOffsets, uint32_t)
		);
		break;
	case CmdBindIndexBuffer:
		vkCmdBindIndexBuffer(
			buffer,
			get(BindIndexBuffer, data)->Buffer,
			get(BindIndexBuffer, data)->Offset,
			get(BindIndexBuffer, data)->IndexType
		);
		break;
	case CmdBindVertexBuffers:
		vkCmdBindVertexBuffers(
			buffer,
			get(BindVertexBuffers, data)->FirstBinding,
			get(BindVertexBuffers, data)->BindingCount,
			getptr(BindVertexBuffers, Buffers, VkBuffer),
			getptr(BindVertexBuffers, Offsets, VkDeviceSize)
		);
		break;
	case CmdDraw:
		vkCmdDraw(
			buffer,
			get(Draw, data)->VertexCount,
			get(Draw, data)->InstanceCount,
			get(Draw, data)->FirstVertex,
			get(Draw, data)->FirstInstance
		);
		break;
	case CmdDrawIndexed:
		vkCmdDrawIndexed(
			buffer,
			get(DrawIndexed, data)->IndexCount,
			get(DrawIndexed, data)->InstanceCount,
			get(DrawIndexed, data)->FirstIndex,
			get(DrawIndexed, data)->VertexOffset,
			get(DrawIndexed, data)->FirstInstance
		);
		break;
	case CmdDrawIndirect:
		vkCmdDrawIndirect(
			buffer,
			get(DrawIndirect, data)->Buffer,
			get(DrawIndirect, data)->Offset,
			get(DrawIndirect, data)->DrawCount,
			get(DrawIndirect, data)->Stride
		);
		break;
	case CmdDrawIndexedIndirect:
		vkCmdDrawIndexedIndirect(
			buffer,
			get(DrawIndexedIndirect, data)->Buffer,
			get(DrawIndexedIndirect, data)->Offset,
			get(DrawIndexedIndirect, data)->DrawCount,
			get(DrawIndexedIndirect, data)->Stride
		);
		break;
	case CmdDispatch:
		vkCmdDispatch(
			buffer,
			get(Dispatch, data)->GroupCountX,
			get(Dispatch, data)->GroupCountY,
			get(Dispatch, data)->GroupCountZ
		);
		break;
	case CmdDispatchIndirect:
		vkCmdDispatchIndirect(
			buffer,
			get(DispatchIndirect, data)->Buffer,
			get(DispatchIndirect, data)->Offset
		);
		break;
	case CmdCopyBuffer:
		vkCmdCopyBuffer(
			buffer,
			get(CopyBuffer, data)->SrcBuffer,
			get(CopyBuffer, data)->DstBuffer,
			get(CopyBuffer, data)->RegionCount,
			getptr(CopyBuffer, Regions, VkBufferCopy)
		);
		break;
	case CmdCopyImage:
		vkCmdCopyImage(
			buffer,
			get(CopyImage, data)->SrcImage,
			get(CopyImage, data)->SrcImageLayout,
			get(CopyImage, data)->DstImage,
			get(CopyImage, data)->DstImageLayout,
			get(CopyImage, data)->RegionCount,
			getptr(CopyImage, Regions, VkImageCopy)
		);
		break;
	case CmdBlitImage:
		vkCmdBlitImage(
			buffer,
			get(BlitImage, data)->SrcImage,
			get(BlitImage, data)->SrcImageLayout,
			get(BlitImage, data)->DstImage,
			get(BlitImage, data)->DstImageLayout,
			get(BlitImage, data)->RegionCount,
			getptr(BlitImage, Regions, VkImageBlit),
			get(BlitImage, data)->Filter
		);
		break;
	case CmdCopyBufferToImage:
		vkCmdCopyBufferToImage(
			buffer,
			get(CopyBufferToImage, data)->SrcBuffer,
			get(CopyBufferToImage, data)->DstImage,
			get(CopyBufferToImage, data)->DstImageLayout,
			get(CopyBufferToImage, data)->RegionCount,
			getptr(CopyBufferToImage, Regions, VkBufferImageCopy)
		);
		break;
	case CmdCopyImageToBuffer:
		vkCmdCopyImageToBuffer(
			buffer,
			get(CopyImageToBuffer, data)->SrcImage,
			get(CopyImageToBuffer, data)->SrcImageLayout,
			get(CopyImageToBuffer, data)->DstBuffer,
			get(CopyImageToBuffer, data)->RegionCount,
			getptr(CopyImageToBuffer, Regions, VkBufferImageCopy)
		);
		break;
	case CmdUpdateBuffer:
		vkCmdUpdateBuffer(
			buffer,
			get(UpdateBuffer, data)->DstBuffer,
			get(UpdateBuffer, data)->DstOffset,
			get(UpdateBuffer, data)->DataSize,
			get(UpdateBuffer, data)->Data
		);
		break;
	case CmdFillBuffer:
		vkCmdFillBuffer(
			buffer,
			get(FillBuffer, data)->DstBuffer,
			get(FillBuffer, data)->DstOffset,
			get(FillBuffer, data)->Size,
			get(FillBuffer, data)->Data
		);
		break;
	case CmdClearColorImage:
		vkCmdClearColorImage(
			buffer,
			get(ClearColorImage, data)->Image,
			get(ClearColorImage, data)->ImageLayout,
			get(ClearColorImage, data)->Color,
			get(ClearColorImage, data)->RangeCount,
			getptr(ClearColorImage, Ranges, VkImageSubresourceRange)
		);
		break;
	case CmdClearDepthStencilImage:
		vkCmdClearDepthStencilImage(
			buffer,
			get(ClearDepthStencilImage, data)->Image,
			get(ClearDepthStencilImage, data)->ImageLayout,
			get(ClearDepthStencilImage, data)->DepthStencil,
			get(ClearDepthStencilImage, data)->RangeCount,
			getptr(ClearDepthStencilImage, Ranges, VkImageSubresourceRange)
		);
		break;
	case CmdClearAttachments:
		vkCmdClearAttachments(
			buffer,
			get(ClearAttachments, data)->AttachmentCount,
			getptr(ClearAttachments, Attachments, VkClearAttachment),
			get(ClearAttachments, data)->RectCount,
			getptr(ClearAttachments, Rects, VkClearRect)
		);
		break;
	case CmdResolveImage:
		vkCmdResolveImage(
			buffer,
			get(ResolveImage, data)->SrcImage,
			get(ResolveImage, data)->SrcImageLayout,
			get(ResolveImage, data)->DstImage,
			get(ResolveImage, data)->DstImageLayout,
			get(ResolveImage, data)->RegionCount,
			getptr(ResolveImage, Regions, VkImageResolve)
		);
		break;
	case CmdSetEvent:
		vkCmdSetEvent(
			buffer,
			get(SetEvent, data)->Event,
			get(SetEvent, data)->StageMask
		);
		break;
	case CmdResetEvent:
		vkCmdResetEvent(
			buffer,
			get(ResetEvent, data)->Event,
			get(ResetEvent, data)->StageMask
		);
		break;
	case CmdWaitEvents:
		vkCmdWaitEvents(
			buffer,
			get(WaitEvents, data)->EventCount,
			get(WaitEvents, data)->Events,
			get(WaitEvents, data)->SrcStageMask,
			get(WaitEvents, data)->DstStageMask,
			get(WaitEvents, data)->MemoryBarrierCount,
			getptr(WaitEvents, MemoryBarriers, VkMemoryBarrier),
			get(WaitEvents, data)->BufferMemoryBarrierCount,
			getptr(WaitEvents, BufferMemoryBarriers, VkBufferMemoryBarrier),
			get(WaitEvents, data)->ImageMemoryBarrierCount,
			getptr(WaitEvents, ImageMemoryBarriers, VkImageMemoryBarrier)
		);
		break;
	case CmdPipelineBarrier:
		vkCmdPipelineBarrier(
			buffer,
			get(PipelineBarrier, data)->SrcStageMask,
			get(PipelineBarrier, data)->DstStageMask,
			get(PipelineBarrier, data)->DependencyFlags,
			get(PipelineBarrier, data)->MemoryBarrierCount,
			getptr(PipelineBarrier, MemoryBarriers, VkMemoryBarrier),
			get(PipelineBarrier, data)->BufferMemoryBarrierCount,
			getptr(PipelineBarrier, BufferMemoryBarriers, VkBufferMemoryBarrier),
			get(PipelineBarrier, data)->ImageMemoryBarrierCount,
			getptr(PipelineBarrier, ImageMemoryBarriers, VkImageMemoryBarrier)
		);
		break;
	case CmdBeginQuery:
		vkCmdBeginQuery(
			buffer,
			get(BeginQuery, data)->QueryPool,
			get(BeginQuery, data)->Query,
			get(BeginQuery, data)->Flags
		);
		break;
	case CmdEndQuery:
		vkCmdEndQuery(
			buffer,
			get(EndQuery, data)->QueryPool,
			get(EndQuery, data)->Query
		);
		break;
	case CmdResetQueryPool:
		vkCmdResetQueryPool(
			buffer,
			get(ResetQueryPool, data)->QueryPool,
			get(ResetQueryPool, data)->FirstQuery,
			get(ResetQueryPool, data)->QueryCount
		);
		break;
	case CmdWriteTimestamp:
		vkCmdWriteTimestamp(
			buffer,
			get(WriteTimestamp, data)->PipelineStage,
			get(WriteTimestamp, data)->QueryPool,
			get(WriteTimestamp, data)->Query
		);
		break;
	case CmdCopyQueryPoolResults:
		vkCmdCopyQueryPoolResults(
			buffer,
			get(CopyQueryPoolResults, data)->QueryPool,
			get(CopyQueryPoolResults, data)->FirstQuery,
			get(CopyQueryPoolResults, data)->QueryCount,
			get(CopyQueryPoolResults, data)->DstBuffer,
			get(CopyQueryPoolResults, data)->DstOffset,
			get(CopyQueryPoolResults, data)->Stride,
			get(CopyQueryPoolResults, data)->Flags
		);
		break;
	case CmdPushConstants:
		vkCmdPushConstants(
			buffer,
			get(PushConstants, data)->Layout,
			get(PushConstants, data)->StageFlags,
			get(PushConstants, data)->Offset,
			get(PushConstants, data)->Size,
			get(PushConstants, data)->Values
		);
		break;
	case CmdBeginRenderPass:
		vkCmdBeginRenderPass(
			buffer,
			getptr(BeginRenderPass, RenderPassBegin, VkRenderPassBeginInfo),
			get(BeginRenderPass, data)->Contents
		);
		break;
	case CmdNextSubpass:
		vkCmdNextSubpass(
			buffer,
			get(NextSubpass, data)->Contents
		);
		break;
	case CmdEndRenderPass:
		vkCmdEndRenderPass(buffer);
		break;
	case CmdExecuteCommands:
		vkCmdExecuteCommands(
			buffer,
			get(ExecuteCommands, data)->CommandBufferCount,
			getptr(ExecuteCommands, CommandBuffers, VkCommandBuffer)
		);
		break;

	case CmdCallFragment:
		vmRun(buffer, get(CallFragment, data)->FragmentToCall);
		break;

	case CmdCustom:
		get(Custom, data)->Run(buffer);
		break;

	case CmdIndirectBindPipeline:
		pipe = *get(IndirectBindPipeline, data)->Pipeline;
		if (state->CurrentPipeline != pipe) {
			state->CurrentPipeline = pipe;
			vkCmdBindPipeline(
				buffer,
				get(IndirectBindPipeline, data)->PipelineBindPoint,
				pipe
			);
		}
		break;
	case CmdIndirectBindDescriptorSets:
		vmBindDescriptorSets(
			buffer,
			get(IndirectBindDescriptorSets, data)->Binding
		);
		break;
	case CmdIndirectBindIndexBuffer:
		vmBindIndexBuffer(
			buffer,
			get(IndirectBindIndexBuffer, data)->Binding
		);
		break;
	case CmdIndirectBindVertexBuffers:
		vmBindVertexBuffers(
			buffer,
			get(IndirectBindVertexBuffers, data)->Binding
		);
		break;
	case CmdIndirectDraw:
		vmDraw(
			buffer,
			get(IndirectDraw, data)->Stats,
			get(IndirectDraw, data)->IsActive,
			get(IndirectDraw, data)->Calls
		);
		break;


	default:
		break;
	}
}

#undef get
#undef getptr

DllExport(void) vmRun(VkCommandBuffer buffer, CommandFragment* fragment)
{
#ifdef _DEBUG
	std::unordered_set<CommandFragment*> set;
#endif

	CommandState state = { VK_NULL_HANDLE };
	while (fragment)
	{
#ifdef _DEBUG
		auto res = set.insert(fragment);
		if (!std::get<1>(res)) {
			printf("[VKVM] loop detected!!!!!!!\n");
			break;
		}
#endif

		auto ptr = (char*)fragment->Commands;

		for (int i = 0; i < (int)fragment->CommandCount; i++)
		{
			auto length = *(uint32_t*)(ptr);
			auto op = *(CommandType*)(ptr + 4);

			enqueueCommand(&state, buffer, op, (void*)ptr);

			ptr = ptr + length;
		}

		fragment = fragment->Next;
	}
}

