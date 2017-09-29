#ifndef __GNUC__
#include "stdafx.h"
#endif

#include "commands.h"
#include <stdio.h>

#define get(t,v) ((t##Command*)(v)) 

static void enqueueCommand (VkCommandBuffer buffer, CommandType op, void* data)
{
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
			get(BindDescriptorSets, data)->DescriptorSets,
			get(BindDescriptorSets, data)->DynamicOffsetCount,
			get(BindDescriptorSets, data)->DynamicOffsets
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
			get(BindVertexBuffers, data)->Buffers,
			get(BindVertexBuffers, data)->Offsets
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
			get(CopyBuffer, data)->Regions
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
			get(CopyImage, data)->Regions
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
			get(BlitImage, data)->Regions,
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
			get(CopyBufferToImage, data)->Regions
		);
		break;
	case CmdCopyImageToBuffer:
		vkCmdCopyImageToBuffer(
			buffer,
			get(CopyImageToBuffer, data)->SrcImage,
			get(CopyImageToBuffer, data)->SrcImageLayout,
			get(CopyImageToBuffer, data)->DstBuffer,
			get(CopyImageToBuffer, data)->RegionCount,
			get(CopyImageToBuffer, data)->Regions
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
			get(ClearColorImage, data)->Ranges
		);
		break;
	case CmdClearDepthStencilImage:
		vkCmdClearDepthStencilImage(
			buffer,
			get(ClearDepthStencilImage, data)->Image,
			get(ClearDepthStencilImage, data)->ImageLayout,
			get(ClearDepthStencilImage, data)->DepthStencil,
			get(ClearDepthStencilImage, data)->RangeCount,
			get(ClearDepthStencilImage, data)->Ranges
		);
		break;
	case CmdClearAttachments:
		vkCmdClearAttachments(
			buffer,
			get(ClearAttachments, data)->AttachmentCount,
			get(ClearAttachments, data)->Attachments,
			get(ClearAttachments, data)->RectCount,
			get(ClearAttachments, data)->Rects
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
			get(ResolveImage, data)->Regions
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
			get(WaitEvents, data)->MemoryBarriers,
			get(WaitEvents, data)->BufferMemoryBarrierCount,
			get(WaitEvents, data)->BufferMemoryBarriers,
			get(WaitEvents, data)->ImageMemoryBarrierCount,
			get(WaitEvents, data)->ImageMemoryBarriers
		);
		break;
	case CmdPipelineBarrier:
		vkCmdPipelineBarrier(
			buffer,
			get(PipelineBarrier, data)->SrcStageMask,
			get(PipelineBarrier, data)->DstStageMask,
			get(PipelineBarrier, data)->DependencyFlags,
			get(PipelineBarrier, data)->MemoryBarrierCount,
			get(PipelineBarrier, data)->MemoryBarriers,
			get(PipelineBarrier, data)->BufferMemoryBarrierCount,
			get(PipelineBarrier, data)->BufferMemoryBarriers,
			get(PipelineBarrier, data)->ImageMemoryBarrierCount,
			get(PipelineBarrier, data)->ImageMemoryBarriers
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
			get(BeginRenderPass, data)->RenderPassBegin,
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
			get(ExecuteCommands, data)->CommandBuffers
		);
		break;

	case CmdCallFragment:
		vmRun(buffer, get(CallFragment, data)->FragmentToCall);
		break;

	case CmdCustom:
		get(Custom, data)->Run(buffer);
		break;

	default:
		break;
	}
}

DllExport(void) vmRun(VkCommandBuffer buffer, CommandFragment* fragment)
{
	while (fragment)
	{
		auto ptr = (char*)fragment->Commands;

		for (int i = 0; i < (int)fragment->CommandCount; i++)
		{
			auto length = *(uint32_t*)(ptr);
			auto op = *(CommandType*)(ptr + 4);

			enqueueCommand(buffer, op, (void*)ptr);

			ptr = ptr + length;
		}

		fragment = fragment->Next;
	}
}

