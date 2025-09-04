#ifndef __GNUC__
#include "stdafx.h"
#endif

#include "vkvm.h"
#include <stdio.h>


DllExport(void) vmBindDescriptorSets(VkCommandBuffer commandBuffer, DescriptorSetBinding* binding)
{
	if (binding->Count == 0)return;
	vkCmdBindDescriptorSets(commandBuffer, binding->BindPoint, binding->Layout, binding->FirstIndex, binding->Count, binding->Sets, 0, nullptr);
}

DllExport(void) vmBindIndexBuffer(VkCommandBuffer commandBuffer, IndexBufferBinding* indexBuffer)
{
	vkCmdBindIndexBuffer(commandBuffer, indexBuffer->Buffer, indexBuffer->Offset, indexBuffer->Type);
}

DllExport(void) vmBindVertexBuffers(VkCommandBuffer commandBuffer, VertexBufferBinding* binding)
{
	if (binding->BindingCount == 0)return;
	vkCmdBindVertexBuffers(commandBuffer, binding->FirstBinding, binding->BindingCount, binding->Buffers, binding->Offsets);
}

DllExport(void) vmDraw(VkCommandBuffer commandBuffer, RuntimeStats* stats, int* isActive, DrawCall* call)
{
	if (!*isActive || call->Count == 0) return;

	if (call->IsIndirect)
	{
		stats->DrawCalls++;
		stats->EffectiveDrawCalls += call->Count;
		const auto& buffer = call->DrawCallBuffer;

		if (call->IsIndexed)
			vkCmdDrawIndexedIndirect(commandBuffer, buffer.Handle, buffer.Offset, call->Count, buffer.Stride);
		else
			vkCmdDrawIndirect(commandBuffer, buffer.Handle, buffer.Offset, call->Count, buffer.Stride);
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
					vkCmdDrawIndexed(commandBuffer, faceVertexCount, instanceCount, info->FirstIndex, info->BaseVertex, info->FirstInstance);
				else
					vkCmdDraw(commandBuffer, faceVertexCount, instanceCount, info->FirstIndex, info->FirstInstance);
			}
		}
	}
}
