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

struct DrawCallBuffer {
	VkBuffer Handle;
	uint64_t Offset;
	int		 Stride;
};

typedef struct {
	uint8_t		IsIndirect;
	uint8_t		IsIndexed;
	int			Count;
	union {
		DrawCallInfo*  DrawCalls;
		DrawCallBuffer DrawCallBuffer;
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


DllExport(void) vmBindDescriptorSets(VkCommandBuffer commandBuffer, DescriptorSetBinding* binding);
DllExport(void) vmBindIndexBuffer(VkCommandBuffer commandBuffer, IndexBufferBinding* indexBuffer);
DllExport(void) vmBindVertexBuffers(VkCommandBuffer commandBuffer, VertexBufferBinding* binding);
DllExport(void) vmDraw(VkCommandBuffer commandBuffer, RuntimeStats* stats, int* isActive, DrawCall* call);

