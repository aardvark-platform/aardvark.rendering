#pragma once

#ifdef __GNUC__
#include <vulkan.h>
#define DllExport(t) extern "C" t
#else
#include "stdafx.h"
#include <stdio.h>
#include <vulkan.h>
#define DllExport(t) extern "C"  __declspec( dllexport ) t __cdecl
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
	int				IsIndirect;
	int				IsIndexed;
	VkBuffer		IndirectBuffer;
	int				IndirectCount;
	int				DrawCallCount;
	DrawCallInfo*	DrawCalls;
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
	VkPipelineLayout Layout;
	VkDescriptorSet* Sets;
} DescriptorSetBinding;

typedef struct {
	VkBuffer Buffer;
	uint64_t Offset;
	VkIndexType Type;
} IndexBufferBinding;

DllExport(void) vmBindPipeline(VkCommandBuffer commandBuffer, VkPipeline* pipeline);
DllExport(void) vmBindDescriptorSets(VkCommandBuffer commandBuffer, DescriptorSetBinding* binding);
DllExport(void) vmBindIndexBuffer(VkCommandBuffer commandBuffer, IndexBufferBinding* indexBuffer);
DllExport(void) vmBindVertexBuffers(VkCommandBuffer commandBuffer, VertexBufferBinding* binding);
DllExport(void) vmDraw(VkCommandBuffer commandBuffer, RuntimeStats* stats, int* isActive, DrawCall* call);

