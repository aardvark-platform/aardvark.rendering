// vkvm.cpp : Defines the exported functions for the DLL application.
//

#include "stdafx.h"


#include "vkvm.h"


DllExport(void) vmBindPipeline(VkCommandBuffer commandBuffer, VkPipeline* pipeline)
{
	vkCmdBindPipeline(commandBuffer, VK_PIPELINE_BIND_POINT_GRAPHICS, *pipeline);
}

DllExport(void) vmBindDescriptorSets(VkCommandBuffer commandBuffer, DescriptorSetBinding* binding)
{
	if (binding->Count == 0)return;
	vkCmdBindDescriptorSets(commandBuffer, VK_PIPELINE_BIND_POINT_GRAPHICS, binding->Layout, binding->FirstIndex, binding->Count, binding->Sets, 0, nullptr);
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
	if (!*isActive)return;

	if (call->IsIndirect)
	{
		if (call->IndirectCount == 0)return;
		stats->DrawCalls++;
		stats->EffectiveDrawCalls += call->IndirectCount;

		if (call->IsIndexed)
			vkCmdDrawIndexedIndirect(commandBuffer, call->IndirectBuffer, 0, call->IndirectCount, sizeof(DrawCallInfo));
		else
			vkCmdDrawIndirect(commandBuffer, call->IndirectBuffer, 0, call->IndirectCount, sizeof(DrawCallInfo));
	}
	else
	{
		auto info = call->DrawCalls;
		auto count = call->DrawCallCount;
		auto indexed = call->IsIndexed;

		stats->DrawCalls += count;
		for (int i = 0; i < count; i++, info += 1)
		{
			stats->EffectiveDrawCalls += info->InstanceCount;
			if (indexed)
				vkCmdDrawIndexed(commandBuffer, info->FaceVertexCount, info->InstanceCount, info->FirstIndex, info->BaseVertex, info->FirstInstance);
			else
				vkCmdDraw(commandBuffer, info->FaceVertexCount, info->InstanceCount, info->FirstIndex, info->FirstInstance);
		}
	}
}
