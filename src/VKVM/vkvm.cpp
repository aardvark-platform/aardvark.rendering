#ifndef __GNUC__
#include "stdafx.h"
#endif

#include "vkvm.h"
#include <stdio.h>

DllExport(bool) vmInit(VkDevice device, PFN_vkGetDeviceProcAddr vkGetDeviceProcAddr, VKVM** ppVkvm)
{
	if (device == nullptr || vkGetDeviceProcAddr == nullptr) return false;

	bool success = true;

	auto getProcAddr = [&success, device, vkGetDeviceProcAddr](const char* name)
	{
		auto ptr = vkGetDeviceProcAddr(device, name);

		if (ptr == nullptr)
		{
			success = false;
			printf("[VKVM] Failed to load device function pointer for '%s'\n", name);
		}

		return ptr;
	};

	VKVM* pVkvm = new VKVM();
    pVkvm->vkCmdBindDescriptorSets     = reinterpret_cast<PFN_vkCmdBindDescriptorSets>     (getProcAddr("vkCmdBindDescriptorSets"));
    pVkvm->vkCmdBindIndexBuffer        = reinterpret_cast<PFN_vkCmdBindIndexBuffer>        (getProcAddr("vkCmdBindIndexBuffer"));
    pVkvm->vkCmdBindVertexBuffers      = reinterpret_cast<PFN_vkCmdBindVertexBuffers>      (getProcAddr("vkCmdBindVertexBuffers"));
    pVkvm->vkCmdDrawIndexedIndirect    = reinterpret_cast<PFN_vkCmdDrawIndexedIndirect>    (getProcAddr("vkCmdDrawIndexedIndirect"));
    pVkvm->vkCmdDrawIndirect           = reinterpret_cast<PFN_vkCmdDrawIndirect>           (getProcAddr("vkCmdDrawIndirect"));
    pVkvm->vkCmdDrawIndexed            = reinterpret_cast<PFN_vkCmdDrawIndexed>            (getProcAddr("vkCmdDrawIndexed"));
    pVkvm->vkCmdDraw                   = reinterpret_cast<PFN_vkCmdDraw>                   (getProcAddr("vkCmdDraw"));
    pVkvm->vkCmdBindPipeline           = reinterpret_cast<PFN_vkCmdBindPipeline>           (getProcAddr("vkCmdBindPipeline"));
    pVkvm->vkCmdSetViewport            = reinterpret_cast<PFN_vkCmdSetViewport>            (getProcAddr("vkCmdSetViewport"));
    pVkvm->vkCmdSetScissor             = reinterpret_cast<PFN_vkCmdSetScissor>             (getProcAddr("vkCmdSetScissor"));
    pVkvm->vkCmdSetLineWidth           = reinterpret_cast<PFN_vkCmdSetLineWidth>           (getProcAddr("vkCmdSetLineWidth"));
    pVkvm->vkCmdSetDepthBias           = reinterpret_cast<PFN_vkCmdSetDepthBias>           (getProcAddr("vkCmdSetDepthBias"));
    pVkvm->vkCmdSetBlendConstants      = reinterpret_cast<PFN_vkCmdSetBlendConstants>      (getProcAddr("vkCmdSetBlendConstants"));
    pVkvm->vkCmdSetDepthBounds         = reinterpret_cast<PFN_vkCmdSetDepthBounds>         (getProcAddr("vkCmdSetDepthBounds"));
    pVkvm->vkCmdSetStencilCompareMask  = reinterpret_cast<PFN_vkCmdSetStencilCompareMask>  (getProcAddr("vkCmdSetStencilCompareMask"));
    pVkvm->vkCmdSetStencilWriteMask    = reinterpret_cast<PFN_vkCmdSetStencilWriteMask>    (getProcAddr("vkCmdSetStencilWriteMask"));
    pVkvm->vkCmdSetStencilReference    = reinterpret_cast<PFN_vkCmdSetStencilReference>    (getProcAddr("vkCmdSetStencilReference"));
    pVkvm->vkCmdDispatch               = reinterpret_cast<PFN_vkCmdDispatch>               (getProcAddr("vkCmdDispatch"));
    pVkvm->vkCmdDispatchIndirect       = reinterpret_cast<PFN_vkCmdDispatchIndirect>       (getProcAddr("vkCmdDispatchIndirect"));
    pVkvm->vkCmdCopyBuffer             = reinterpret_cast<PFN_vkCmdCopyBuffer>             (getProcAddr("vkCmdCopyBuffer"));
    pVkvm->vkCmdCopyImage              = reinterpret_cast<PFN_vkCmdCopyImage>              (getProcAddr("vkCmdCopyImage"));
    pVkvm->vkCmdBlitImage              = reinterpret_cast<PFN_vkCmdBlitImage>              (getProcAddr("vkCmdBlitImage"));
    pVkvm->vkCmdCopyBufferToImage      = reinterpret_cast<PFN_vkCmdCopyBufferToImage>      (getProcAddr("vkCmdCopyBufferToImage"));
    pVkvm->vkCmdCopyImageToBuffer      = reinterpret_cast<PFN_vkCmdCopyImageToBuffer>      (getProcAddr("vkCmdCopyImageToBuffer"));
    pVkvm->vkCmdUpdateBuffer           = reinterpret_cast<PFN_vkCmdUpdateBuffer>           (getProcAddr("vkCmdUpdateBuffer"));
    pVkvm->vkCmdFillBuffer             = reinterpret_cast<PFN_vkCmdFillBuffer>             (getProcAddr("vkCmdFillBuffer"));
    pVkvm->vkCmdClearColorImage        = reinterpret_cast<PFN_vkCmdClearColorImage>        (getProcAddr("vkCmdClearColorImage"));
    pVkvm->vkCmdClearDepthStencilImage = reinterpret_cast<PFN_vkCmdClearDepthStencilImage> (getProcAddr("vkCmdClearDepthStencilImage"));
    pVkvm->vkCmdClearAttachments       = reinterpret_cast<PFN_vkCmdClearAttachments>       (getProcAddr("vkCmdClearAttachments"));
    pVkvm->vkCmdResolveImage           = reinterpret_cast<PFN_vkCmdResolveImage>           (getProcAddr("vkCmdResolveImage"));
    pVkvm->vkCmdSetEvent               = reinterpret_cast<PFN_vkCmdSetEvent>               (getProcAddr("vkCmdSetEvent"));
    pVkvm->vkCmdResetEvent             = reinterpret_cast<PFN_vkCmdResetEvent>             (getProcAddr("vkCmdResetEvent"));
    pVkvm->vkCmdWaitEvents             = reinterpret_cast<PFN_vkCmdWaitEvents>             (getProcAddr("vkCmdWaitEvents"));
    pVkvm->vkCmdPipelineBarrier        = reinterpret_cast<PFN_vkCmdPipelineBarrier>        (getProcAddr("vkCmdPipelineBarrier"));
    pVkvm->vkCmdBeginQuery             = reinterpret_cast<PFN_vkCmdBeginQuery>             (getProcAddr("vkCmdBeginQuery"));
    pVkvm->vkCmdEndQuery               = reinterpret_cast<PFN_vkCmdEndQuery>               (getProcAddr("vkCmdEndQuery"));
    pVkvm->vkCmdResetQueryPool         = reinterpret_cast<PFN_vkCmdResetQueryPool>         (getProcAddr("vkCmdResetQueryPool"));
    pVkvm->vkCmdWriteTimestamp         = reinterpret_cast<PFN_vkCmdWriteTimestamp>         (getProcAddr("vkCmdWriteTimestamp"));
    pVkvm->vkCmdCopyQueryPoolResults   = reinterpret_cast<PFN_vkCmdCopyQueryPoolResults>   (getProcAddr("vkCmdCopyQueryPoolResults"));
    pVkvm->vkCmdPushConstants          = reinterpret_cast<PFN_vkCmdPushConstants>          (getProcAddr("vkCmdPushConstants"));
    pVkvm->vkCmdBeginRenderPass        = reinterpret_cast<PFN_vkCmdBeginRenderPass>        (getProcAddr("vkCmdBeginRenderPass"));
    pVkvm->vkCmdNextSubpass            = reinterpret_cast<PFN_vkCmdNextSubpass>            (getProcAddr("vkCmdNextSubpass"));
    pVkvm->vkCmdEndRenderPass          = reinterpret_cast<PFN_vkCmdEndRenderPass>          (getProcAddr("vkCmdEndRenderPass"));
    pVkvm->vkCmdExecuteCommands        = reinterpret_cast<PFN_vkCmdExecuteCommands>        (getProcAddr("vkCmdExecuteCommands"));

	*ppVkvm = pVkvm;
	return success;
}

void vmFree(const VKVM* pVkvm)
{
	delete pVkvm;
}

void vmBindDescriptorSets(const VKVM* pVkvm, VkCommandBuffer commandBuffer, DescriptorSetBinding* binding)
{
	if (binding->Count == 0)return;
	pVkvm->vkCmdBindDescriptorSets(commandBuffer, binding->BindPoint, binding->Layout, binding->FirstIndex, binding->Count, binding->Sets, 0, nullptr);
}

void vmBindIndexBuffer(const VKVM* pVkvm, VkCommandBuffer commandBuffer, IndexBufferBinding* indexBuffer)
{
	pVkvm->vkCmdBindIndexBuffer(commandBuffer, indexBuffer->Buffer, indexBuffer->Offset, indexBuffer->Type);
}

void vmBindVertexBuffers(const VKVM* pVkvm, VkCommandBuffer commandBuffer, VertexBufferBinding* binding)
{
	if (binding->BindingCount == 0)return;
	pVkvm->vkCmdBindVertexBuffers(commandBuffer, binding->FirstBinding, binding->BindingCount, binding->Buffers, binding->Offsets);
}

void vmDraw(const VKVM* pVkvm, VkCommandBuffer commandBuffer, RuntimeStats* stats, int* isActive, DrawCall* call)
{
	if (!*isActive || call->Count == 0) return;

	if (call->IsIndirect)
	{
		stats->DrawCalls++;
		stats->EffectiveDrawCalls += call->Count;
		const auto& buffer = call->DrawCallBuffer;

		if (call->IsIndexed)
			pVkvm->vkCmdDrawIndexedIndirect(commandBuffer, buffer.Handle, buffer.Offset, call->Count, buffer.Stride);
		else
			pVkvm->vkCmdDrawIndirect(commandBuffer, buffer.Handle, buffer.Offset, call->Count, buffer.Stride);
	}
	else
	{
		auto info = call->DrawCalls;
		auto count = call->Count;
		auto indexed = call->IsIndexed;
		stats->DrawCalls += count;

		for (int i = 0; i < count; i++, info += 1)
		{
			auto instanceCount = info->InstanceCount;
			auto faceVertexCount = info->FaceVertexCount;
			if (instanceCount != 0 && faceVertexCount != 0)
			{
				stats->EffectiveDrawCalls += instanceCount;
				if (indexed)
					pVkvm->vkCmdDrawIndexed(commandBuffer, faceVertexCount, instanceCount, info->FirstIndex, info->BaseVertex, info->FirstInstance);
				else
					pVkvm->vkCmdDraw(commandBuffer, faceVertexCount, instanceCount, info->FirstIndex, info->FirstInstance);
			}
		}
	}
}
