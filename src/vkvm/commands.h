#pragma once

#ifdef __GNUC__
#include <stdio.h>
#include <vulkan/vulkan.h>
#define DllExport(t) extern "C" t
#else
#include "stdafx.h"
#include <stdio.h>
#include <vulkan.h>
#endif


typedef struct CommandFragment_ {
	uint32_t					CommandCount;
	void*						Commands;
	struct CommandFragment_*	Next;
} CommandFragment;


enum CommandType {
	CmdBindPipeline = 1,
	CmdSetViewport = 2,
	CmdSetScissor = 3,
	CmdSetLineWidth = 4,
	CmdSetDepthBias = 5,
	CmdSetBlendConstants = 6,
	CmdSetDepthBounds = 7,
	CmdSetStencilCompareMask = 8,
	CmdSetStencilWriteMask = 9,
	CmdSetStencilReference = 10,
	CmdBindDescriptorSets = 11,
	CmdBindIndexBuffer = 12,
	CmdBindVertexBuffers = 13,
	CmdDraw = 14,
	CmdDrawIndexed = 15,
	CmdDrawIndirect = 16,
	CmdDrawIndexedIndirect = 17,
	CmdDispatch = 18,
	CmdDispatchIndirect = 19,
	CmdCopyBuffer = 20,
	CmdCopyImage = 21,
	CmdBlitImage = 22,
	CmdCopyBufferToImage = 23,
	CmdCopyImageToBuffer = 24,
	CmdUpdateBuffer = 25,
	CmdFillBuffer = 26,
	CmdClearColorImage = 27,
	CmdClearDepthStencilImage = 28,
	CmdClearAttachments = 29,
	CmdResolveImage = 30,
	CmdSetEvent = 31,
	CmdResetEvent = 32,
	CmdWaitEvents = 33,
	CmdPipelineBarrier = 34,
	CmdBeginQuery = 35,
	CmdEndQuery = 36,
	CmdResetQueryPool = 37,
	CmdWriteTimestamp = 38,
	CmdCopyQueryPoolResults = 39,
	CmdPushConstants = 40,
	CmdBeginRenderPass = 41,
	CmdNextSubpass = 42,
	CmdEndRenderPass = 43,
	CmdExecuteCommands = 44,

	CmdCallFragment = 100,
	CmdCustom = 101
};

#define DEFCMD(n,a) typedef struct { uint32_t Length; CommandType OpCode; a## } n##Command;
#define DEFCMD0(n) typedef struct { uint32_t Length; CommandType OpCode; } n##Command;

typedef struct {
	uint32_t				EventCount;
	VkEvent*				Events;
	VkPipelineStageFlags	SrcStageMask;
	VkPipelineStageFlags	DstStageMask;
	uint32_t				MemoryBarrierCount;
	VkMemoryBarrier*		MemoryBarriers;
	uint32_t				BufferMemoryBarrierCount;
	VkBufferMemoryBarrier*	BufferMemoryBarriers;
	uint32_t				ImageMemoryBarrierCount;
	VkImageMemoryBarrier*	ImageMemoryBarriers;
} WaitEventsArgs;

typedef struct {
	VkPipelineStageFlags	SrcStageMask;
	VkPipelineStageFlags	DstStageMask;
	VkDependencyFlags		DependencyFlags;
	uint32_t				MemoryBarrierCount;
	VkMemoryBarrier*		MemoryBarriers;
	uint32_t				BufferMemoryBarrierCount;
	VkBufferMemoryBarrier*	BufferMemoryBarriers;
	uint32_t				ImageMemoryBarrierCount;
	VkImageMemoryBarrier*	ImageMemoryBarriers;
} PipelineBarrierArgs;

DEFCMD(BindPipeline,
	VkPipelineBindPoint		PipelineBindPoint;
	VkPipeline				Pipeline;
)

DEFCMD(SetViewport,
	uint32_t				FirstViewport;
	uint32_t				ViewportCount;
	VkViewport*				Viewports;
)

DEFCMD(SetScissor,
	uint32_t				FirstScissor;
	uint32_t				ScissorCount;
	VkRect2D*				Scissors;
)

DEFCMD(SetLineWidth,
	float					LineWidth;
)

DEFCMD(SetDepthBias,
	float					DepthBiasConstantFactor;
	float					DepthBiasClamp;
	float					DepthBiasSlopeFactor;
)

DEFCMD(SetBlendConstants,
	float					BlendConstants[4];
)

DEFCMD(SetDepthBounds,
	float					MinDepth;
	float					MaxDepth;
)

DEFCMD(SetStencilCompareMask,
	VkStencilFaceFlags		FaceMask;
	uint32_t				CompareMask;
)

DEFCMD(SetStencilWriteMask,
	VkStencilFaceFlags		FaceMask;
	uint32_t				WriteMask;
)

DEFCMD(SetStencilReference,
	VkStencilFaceFlags		FaceMask;
	uint32_t				Reference;
)

DEFCMD(BindDescriptorSets,
	VkPipelineBindPoint		PipelineBindPoint;
	VkPipelineLayout		Layout;
	uint32_t				FirstSet;
	uint32_t				SetCount;
	VkDescriptorSet*		DescriptorSets;
	uint32_t				DynamicOffsetCount;
	uint32_t*				DynamicOffsets;
)

DEFCMD(BindIndexBuffer,
	VkBuffer				Buffer;
	VkDeviceSize			Offset;
	VkIndexType				IndexType;
)

DEFCMD(BindVertexBuffers,
	uint32_t				FirstBinding;
	uint32_t				BindingCount;
	VkBuffer*				Buffers;
	VkDeviceSize*			Offsets;
)

DEFCMD(Draw,
	uint32_t				VertexCount;
	uint32_t				InstanceCount;
	uint32_t				FirstVertex;
	uint32_t				FirstInstance;
)

DEFCMD(DrawIndexed,
	uint32_t				IndexCount;
	uint32_t				InstanceCount;
	uint32_t				FirstIndex;
	int32_t					VertexOffset;
	uint32_t				FirstInstance;
)

DEFCMD(DrawIndirect,
	VkBuffer				Buffer;
	VkDeviceSize			Offset;
	uint32_t				DrawCount;
	uint32_t				Stride;
)

DEFCMD(DrawIndexedIndirect,
	VkBuffer				Buffer;
	VkDeviceSize			Offset;
	uint32_t				DrawCount;
	uint32_t				Stride;
)

DEFCMD(Dispatch,
	uint32_t				GroupCountX;
	uint32_t				GroupCountY;
	uint32_t				GroupCountZ;
)

DEFCMD(DispatchIndirect,
	VkBuffer				Buffer;
	VkDeviceSize			Offset;
)

DEFCMD(CopyBuffer,
	VkBuffer				SrcBuffer;
	VkBuffer				DstBuffer;
	uint32_t				RegionCount;
	VkBufferCopy*			Regions;
)

DEFCMD(CopyImage,
	VkImage					SrcImage;
	VkImageLayout			SrcImageLayout;
	VkImage					DstImage;
	VkImageLayout			DstImageLayout;
	uint32_t				RegionCount;
	VkImageCopy*			Regions;
)


DEFCMD(BlitImage,
	VkImage					SrcImage;
	VkImageLayout			SrcImageLayout;
	VkImage					DstImage;
	VkImageLayout			DstImageLayout;
	uint32_t				RegionCount;
	VkImageBlit*			Regions;
	VkFilter				Filter;
)

DEFCMD(CopyBufferToImage,
	VkBuffer				SrcBuffer;
	VkImage					DstImage;
	VkImageLayout			DstImageLayout;
	uint32_t				RegionCount;
	VkBufferImageCopy*		Regions;
)

DEFCMD(CopyImageToBuffer,
	VkImage					SrcImage;
	VkImageLayout			SrcImageLayout;
	VkBuffer				DstBuffer;
	uint32_t				RegionCount;
	VkBufferImageCopy*		Regions;
)

DEFCMD(UpdateBuffer,
	VkBuffer				DstBuffer;
	VkDeviceSize			DstOffset;
	VkDeviceSize			DataSize;
	void*					Data;
)

DEFCMD(FillBuffer,
	VkBuffer				DstBuffer;
	VkDeviceSize			DstOffset;
	VkDeviceSize			Size;
	uint32_t				Data;
)

DEFCMD(ClearColorImage,
	VkImage						Image;
	VkImageLayout				ImageLayout;
	VkClearColorValue*			Color;
	uint32_t					RangeCount;
	VkImageSubresourceRange*	Ranges;
)

DEFCMD(ClearDepthStencilImage,
	VkImage						Image;
	VkImageLayout				ImageLayout;
	VkClearDepthStencilValue*	DepthStencil;
	uint32_t					RangeCount;
	VkImageSubresourceRange*	Ranges;
)

DEFCMD(ClearAttachments,
	uint32_t				AttachmentCount;
	VkClearAttachment*		Attachments;
	uint32_t				RectCount;
	VkClearRect*			Rects;
)


DEFCMD(ResolveImage,
	VkImage					SrcImage;
	VkImageLayout			SrcImageLayout;
	VkImage					DstImage;
	VkImageLayout			DstImageLayout;
	uint32_t				RegionCount;
	VkImageResolve*			Regions;
)

DEFCMD(SetEvent,
	VkEvent					Event;
	VkPipelineStageFlags	StageMask;
)

DEFCMD(ResetEvent,
	VkEvent					Event;
	VkPipelineStageFlags	StageMask;
)


DEFCMD(WaitEvents,
	uint32_t				EventCount;
	VkEvent*				Events;
	VkPipelineStageFlags	SrcStageMask;
	VkPipelineStageFlags	DstStageMask;
	uint32_t				MemoryBarrierCount;
	VkMemoryBarrier*		MemoryBarriers;
	uint32_t				BufferMemoryBarrierCount;
	VkBufferMemoryBarrier*	BufferMemoryBarriers;
	uint32_t				ImageMemoryBarrierCount;
	VkImageMemoryBarrier*	ImageMemoryBarriers;
)


DEFCMD(PipelineBarrier,
	VkPipelineStageFlags	SrcStageMask;
	VkPipelineStageFlags	DstStageMask;
	VkDependencyFlags		DependencyFlags;
	uint32_t				MemoryBarrierCount;
	VkMemoryBarrier*		MemoryBarriers;
	uint32_t				BufferMemoryBarrierCount;
	VkBufferMemoryBarrier*	BufferMemoryBarriers;
	uint32_t				ImageMemoryBarrierCount;
	VkImageMemoryBarrier*	ImageMemoryBarriers;
)


DEFCMD(BeginQuery,
	VkQueryPool				QueryPool;
	uint32_t				Query;
	VkQueryControlFlags		Flags;
)

DEFCMD(EndQuery,
	VkQueryPool				QueryPool;
	uint32_t				Query;
)

DEFCMD(ResetQueryPool,
	VkQueryPool				QueryPool;
	uint32_t				FirstQuery;
	uint32_t				QueryCount;
)

DEFCMD(WriteTimestamp,
	VkPipelineStageFlagBits PipelineStage;
	VkQueryPool				QueryPool;
	uint32_t				Query;
)

DEFCMD(CopyQueryPoolResults,
	VkQueryPool				QueryPool;
	uint32_t				FirstQuery;
	uint32_t				QueryCount;
	VkBuffer				DstBuffer;
	VkDeviceSize			DstOffset;
	VkDeviceSize			Stride;
	VkQueryResultFlags		Flags;
)

DEFCMD(PushConstants,
	VkPipelineLayout		Layout;
	VkShaderStageFlags		StageFlags;
	uint32_t				Offset;
	uint32_t				Size;
	void*					Values;
)

DEFCMD(BeginRenderPass,
	VkRenderPassBeginInfo*	RenderPassBegin;
	VkSubpassContents		Contents;
)

DEFCMD(NextSubpass,
	VkSubpassContents		Contents;
)

DEFCMD0(EndRenderPass)

DEFCMD(ExecuteCommands,
	uint32_t				CommandBufferCount;
	VkCommandBuffer*		CommandBuffers;
)

DEFCMD(CallFragment,
	CommandFragment*        FragmentToCall;
)

DEFCMD(Custom,
	void (*Run)(VkCommandBuffer);
)

/*
union CommandData {
	BindPipelineCommand				BindPipeline;
	SetViewportCommand				SetViewport;
	SetScissorCommand				SetScissor;
	SetLineWidthCommand				SetLineWidth;
	SetDepthBiasCommand				SetDepthBias;
	SetBlendConstantsCommand		SetBlendConstants;
	SetDepthBoundsCommand			SetDepthBounds;
	SetStencilCompareMaskCommand	SetStencilCompareMask;
	SetStencilWriteMaskCommand		SetStencilWriteMask;
	SetStencilReferenceCommand		SetStencilReference;
	BindDescriptorSetsCommand		BindDescriptorSets;
	BindIndexBufferCommand			BindIndexBuffer;
	BindVertexBuffersCommand		BindVertexBuffers;
	DrawCommand						Draw;
	DrawIndexedCommand				DrawIndexed;
	DrawIndirectCommand				DrawIndirect;
	DrawIndexedIndirectCommand		DrawIndexedIndirect;
	DispatchCommand					Dispatch;
	DispatchIndirectCommand			DispatchIndirect;
	CopyBufferCommand				CopyBuffer;
	CopyImageCommand				CopyImage;
	BlitImageCommand				BlitImage;
	CopyBufferToImageCommand		CopyBufferToImage;
	CopyImageToBufferCommand		CopyImageToBuffer;
	UpdateBufferCommand				UpdateBuffer;
	FillBufferCommand				FillBuffer;
	ClearColorImageCommand			ClearColorImage;
	ClearDepthStencilImageCommand	ClearDepthStencilImage;
	ClearAttachmentsCommand			ClearAttachments;
	ResolveImageCommand				ResolveImage;
	SetEventCommand					SetEvent;
	ResetEventCommand				ResetEvent;
	WaitEventsCommand				WaitEvents;
	PipelineBarrierCommand			PipelineBarrier;
	BeginQueryCommand				BeginQuery;
	EndQueryCommand					EndQuery;
	ResetQueryPoolCommand			ResetQueryPool;
	WriteTimestampCommand			WriteTimestamp;
	CopyQueryPoolResultsCommand		CopyQueryPoolResults;
	PushConstantsCommand			PushConstants;
	BeginRenderPassCommand			BeginRenderPass;
	NextSubpassCommand				NextSubpass;
	EndRenderPassCommand			EndRenderPass;
	ExecuteCommandsCommand			ExecuteCommands;
};

typedef struct {
	CommandType Type;
	union CommandData Data;
} Command;
*/

DllExport(void) vmRun(VkCommandBuffer buffer, CommandFragment* fragment);

