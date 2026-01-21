#pragma once

#ifdef __GNUC__
#include <stdio.h>
#include <vulkan/vulkan.h>
#define DllExport(t) extern "C" t
#else
#include "stdafx.h"
#include <stdio.h>
#include <vulkan/vulkan.h>
#endif


typedef struct {
	int DrawCalls;
	int EffectiveDrawCalls;
} RuntimeStats;

typedef struct {
	int FaceVertexCount;
	int InstanceCount;
	int FirstIndex;
	int FirstInstance;
	int BaseVertex;
} DrawCallInfo;

typedef struct {
	uint8_t		IsIndirect;
	uint8_t		IsIndexed;
	int			Count;
	union {
		DrawCallInfo*  DrawCalls;
		struct {
			VkBuffer Handle;
			uint64_t Offset;
			int		 Stride;
		} DrawCallBuffer;
	};
} DrawCall;

typedef struct {
	int FirstBinding;
	int BindingCount;
	VkBuffer* Buffers;
	uint64_t* Offsets;
} VertexBufferBinding;

typedef struct {
	int FirstIndex;
	int Count;
	VkPipelineBindPoint BindPoint;
	VkPipelineLayout Layout;
	VkDescriptorSet* Sets;
} DescriptorSetBinding;

typedef struct {
	VkBuffer Buffer;
	uint64_t Offset;
	VkIndexType Type;
} IndexBufferBinding;

typedef struct {
	PFN_vkCmdBindDescriptorSets		vkCmdBindDescriptorSets;
	PFN_vkCmdBindIndexBuffer		vkCmdBindIndexBuffer;
	PFN_vkCmdBindVertexBuffers		vkCmdBindVertexBuffers;
	PFN_vkCmdDrawIndexedIndirect	vkCmdDrawIndexedIndirect;
	PFN_vkCmdDrawIndirect			vkCmdDrawIndirect;
	PFN_vkCmdDrawIndexed			vkCmdDrawIndexed;
	PFN_vkCmdDraw					vkCmdDraw;
	PFN_vkCmdBindPipeline			vkCmdBindPipeline;
	PFN_vkCmdSetViewport			vkCmdSetViewport;
	PFN_vkCmdSetScissor				vkCmdSetScissor;
	PFN_vkCmdSetLineWidth			vkCmdSetLineWidth;
	PFN_vkCmdSetDepthBias			vkCmdSetDepthBias;
	PFN_vkCmdSetBlendConstants		vkCmdSetBlendConstants;
	PFN_vkCmdSetDepthBounds			vkCmdSetDepthBounds;
	PFN_vkCmdSetStencilCompareMask	vkCmdSetStencilCompareMask;
	PFN_vkCmdSetStencilWriteMask	vkCmdSetStencilWriteMask;
	PFN_vkCmdSetStencilReference	vkCmdSetStencilReference;
	PFN_vkCmdDispatch				vkCmdDispatch;
	PFN_vkCmdDispatchIndirect		vkCmdDispatchIndirect;
	PFN_vkCmdCopyBuffer				vkCmdCopyBuffer;
	PFN_vkCmdCopyImage				vkCmdCopyImage;
	PFN_vkCmdBlitImage				vkCmdBlitImage;
	PFN_vkCmdCopyBufferToImage		vkCmdCopyBufferToImage;
	PFN_vkCmdCopyImageToBuffer		vkCmdCopyImageToBuffer;
	PFN_vkCmdUpdateBuffer			vkCmdUpdateBuffer;
	PFN_vkCmdFillBuffer				vkCmdFillBuffer;
	PFN_vkCmdClearColorImage		vkCmdClearColorImage;
	PFN_vkCmdClearDepthStencilImage	vkCmdClearDepthStencilImage;
	PFN_vkCmdClearAttachments		vkCmdClearAttachments;
	PFN_vkCmdResolveImage			vkCmdResolveImage;
	PFN_vkCmdSetEvent				vkCmdSetEvent;
	PFN_vkCmdResetEvent				vkCmdResetEvent;
	PFN_vkCmdWaitEvents				vkCmdWaitEvents;
	PFN_vkCmdPipelineBarrier		vkCmdPipelineBarrier;
	PFN_vkCmdBeginQuery				vkCmdBeginQuery;
	PFN_vkCmdEndQuery				vkCmdEndQuery;
	PFN_vkCmdResetQueryPool			vkCmdResetQueryPool;
	PFN_vkCmdWriteTimestamp			vkCmdWriteTimestamp;
	PFN_vkCmdCopyQueryPoolResults	vkCmdCopyQueryPoolResults;
	PFN_vkCmdPushConstants			vkCmdPushConstants;
	PFN_vkCmdBeginRenderPass		vkCmdBeginRenderPass;
	PFN_vkCmdNextSubpass			vkCmdNextSubpass;
	PFN_vkCmdEndRenderPass			vkCmdEndRenderPass;
	PFN_vkCmdExecuteCommands		vkCmdExecuteCommands;
} VKVM;

DllExport(bool) vmInit(VkDevice device, PFN_vkGetDeviceProcAddr vkGetDeviceProcAddr, VKVM** ppVkvm);
DllExport(void) vmFree(const VKVM* pVkvm);
void vmBindDescriptorSets(const VKVM* pVkvm, VkCommandBuffer commandBuffer, DescriptorSetBinding* binding);
void vmBindIndexBuffer(const VKVM* pVkvm, VkCommandBuffer commandBuffer, IndexBufferBinding* indexBuffer);
void vmBindVertexBuffers(const VKVM* pVkvm, VkCommandBuffer commandBuffer, VertexBufferBinding* binding);
void vmDraw(const VKVM* pVkvm, VkCommandBuffer commandBuffer, RuntimeStats* stats, int* isActive, DrawCall* call);

