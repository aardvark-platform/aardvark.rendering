#pragma once

#ifdef __APPLE__
#include <opengl/gl.h>
#include <stdio.h>
#include <string.h>
#elif __GNUC__
#include <GL/gl.h>
#include <stdio.h>
#include <string.h>
#else
#define _SILENCE_STDEXT_HASH_DEPRECATION_WARNINGS
#include "stdafx.h"
#include <windows.h>
#include <gl/GL.h>
#endif

#include <vector>
#include <unordered_set>
#include <unordered_map>
#include <tuple>



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
	int64_t Count;
	DrawCallInfo* Infos;
} DrawCallInfoList;

typedef  struct {
	int  Count;
	int  InstanceCount;
	int  First;
	int  BaseInstance;
} DrawArraysIndirectCommand;

typedef  struct {
	int  Count;
	int  InstanceCount;
	int  FirstIndex;
	int  BaseVertex;
	int  BaseInstance;
} DrawElementsIndirectCommand;

typedef struct {
	int Enabled;
	GLenum SourceFactor;
	GLenum DestFactor;
	GLenum Operation;
	GLenum SourceFactorAlpha;
	GLenum DestFactorAlpha;
	GLenum OperationAlpha;
} BlendMode;

typedef struct {
	int Enabled;
	GLenum Cmp;
	int32_t Mask;
	uint32_t Reference;
	GLenum OpStencilFail;
	GLenum OpDepthFail;
	GLenum OpPass;
} StencilMode;

typedef struct {
	GLenum Mode;
	int PatchVertices;
} BeginMode;

typedef struct {
	float Constant;
	float SlopeScale;
	float Clamp;
} DepthBiasInfo;

struct VertexAttribValue
{
	float 		X;
	float 		Y;
	float 		Z;
	float 		W;
};

typedef struct {
	uint32_t 			Index;
	int					Size;
	int					Divisor;
	GLenum				Type;
	int					Normalized;
	int					Stride;
	int					Offset;
	int					Buffer;
} VertexBufferBinding;

typedef struct {
	uint32_t			Index;
	float 				X;
	float 				Y;
	float 				Z;
	float 				W;
} VertexValueBinding;

typedef struct {
	int						IndexBuffer;
	int						BufferBindingCount;
	VertexBufferBinding*	BufferBindings;
	int						ValueBindingCount;
	VertexValueBinding*		ValueBindings;
	int						VAO;
	void*					VAOContext;
} VertexInputBinding;

class State
{
private:
	int removedInstructions;

	intptr_t currentVertexArray;
	intptr_t currentProgram;
	intptr_t currentActiveTexture;
	intptr_t currentDepthFunc;
	intptr_t currentCullFace;

	intptr_t currentDepthMask;
	intptr_t currentStencilMask;
	std::unordered_map<intptr_t, int> currentColorMask;
	std::vector<GLenum> currentDrawBuffers;

	std::tuple<intptr_t, intptr_t> currentPolygonMode;
	std::tuple<intptr_t, intptr_t, intptr_t, intptr_t> blendFunc;
	std::tuple<intptr_t, intptr_t> blendEquation;
	std::tuple<intptr_t, intptr_t, intptr_t, intptr_t> blendColor;
	std::tuple<intptr_t, intptr_t, intptr_t, intptr_t> stencilFunc;
	std::tuple<intptr_t, intptr_t, intptr_t, intptr_t> stencilOp;

	std::unordered_map<intptr_t, intptr_t> patchParameters;
	std::unordered_map<int, intptr_t> currentSampler;
	std::unordered_map<GLenum, std::unordered_map<int, intptr_t>> currentTexture;
	std::unordered_map<int, std::tuple<intptr_t, intptr_t, intptr_t>> currentBuffer;
	std::unordered_map<intptr_t, bool> modes;
	
	int* hDepthTest;
	GLenum* hCullFace;
	GLenum* hPolygonMode;
	StencilMode* hStencilModeFront;
	StencilMode* hStencilModeBack;
	int* hConservativeRaster;
	int* hMultisample;
	VertexInputBinding* currentVertexInput;

public:

	State();
	~State();

	void Reset();

	bool ShouldSetProgram(intptr_t program);
	bool ShouldSetVertexArray(intptr_t vao);
	bool ShouldSetActiveTexture(intptr_t unit);
	bool ShouldSetSampler(int index, intptr_t sampler);
	bool ShouldSetTexture(GLenum target, intptr_t sampler);
	bool ShouldSetBuffer(GLenum target, int index, intptr_t buffer, intptr_t offset, intptr_t size);
	bool ShouldEnable(intptr_t flag);
	bool ShouldDisable(intptr_t flag);
	bool ShouldSetDepthFunc(intptr_t func);
	bool ShouldSetCullFace(intptr_t face);
	bool ShouldSetPolygonMode(intptr_t face, intptr_t mode);
	bool ShouldSetBlendFunc(intptr_t srcRgb, intptr_t dstRgb, intptr_t srcAlpha, intptr_t dstAlpha);
	bool ShouldSetBlendEquation(intptr_t arg0, intptr_t arg1);
	bool ShouldSetBlendColor(intptr_t arg0, intptr_t arg1, intptr_t arg2, intptr_t arg3);
	bool ShouldSetStencilFunc(intptr_t arg0, intptr_t arg1, intptr_t arg2, intptr_t arg3);
	bool ShouldSetStencilOp(intptr_t arg0, intptr_t arg1, intptr_t arg2, intptr_t arg3);
	bool ShouldSetPatchParameter(intptr_t parameter, intptr_t value);
	bool ShouldSetDepthMask(intptr_t depthMask);
	bool ShouldSetStencilMask(intptr_t depthMask);
	bool ShouldSetColorMask(intptr_t index, intptr_t r, intptr_t g, intptr_t b, intptr_t a);
	bool ShouldSetDrawBuffers(GLuint n, const GLenum* buffers);

	bool HShouldSetDepthTest(int* test);
	bool HShouldSetCullFace(GLenum* face);
	bool HShouldSetPolygonMode(GLenum* mode);
	bool HShouldSetBlendModes(int count, BlendMode** mode);
	bool HShouldSetStencilMode(StencilMode* front, StencilMode* back);
	bool HShouldBindVertexAttributes(VertexInputBinding* binding);
	bool HShouldSetConservativeRaster(int* enabled);
	bool HShouldSetMultisample(int* enabled);

	int GetRemovedInstructions();
};
