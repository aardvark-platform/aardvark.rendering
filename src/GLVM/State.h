#pragma once

#include <windows.h>
#include <gl/GL.h>
#include <unordered_map>
#include <unordered_set>

class State
{
private:
	int removedInstructions;

	intptr_t currentVertexArray;
	intptr_t currentProgram;
	intptr_t currentActiveTexture;
	intptr_t currentDepthFunc;
	intptr_t currentCullFace;

	std::tuple<intptr_t, intptr_t> currentPolygonMode;
	std::tuple<intptr_t, intptr_t, intptr_t, intptr_t> blendFunc;
	std::tuple<intptr_t, intptr_t> blendEquation;
	std::tuple<intptr_t, intptr_t, intptr_t, intptr_t> blendColor;
	std::tuple<intptr_t, intptr_t, intptr_t, intptr_t> stencilFunc;
	std::tuple<intptr_t, intptr_t, intptr_t, intptr_t> stencilOp;

	std::unordered_map<intptr_t, intptr_t> patchParameters;
	std::unordered_map<int, intptr_t> currentSampler;
	std::unordered_map<GLenum, std::unordered_map<int, intptr_t>> currentTexture;
	std::unordered_map<int, intptr_t> currentBuffer;
	std::unordered_map<intptr_t, bool> modes;
	

public:

	State();
	~State();

	void Reset();

	bool ShouldSetProgram(intptr_t program);
	bool ShouldSetVertexArray(intptr_t vao);
	bool ShouldSetActiveTexture(intptr_t unit);
	bool ShouldSetSampler(int index, intptr_t sampler);
	bool ShouldSetTexture(GLenum target, intptr_t sampler);
	bool ShouldSetBuffer(GLenum target, int index, intptr_t buffer);
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

	int GetRemovedInstructions();
};
