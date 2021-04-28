#ifndef __GNUC__
#include "stdafx.h"
#endif

#include "State.h"

State::State()
{
	removedInstructions = 0;
	currentVertexArray = -1;
	currentProgram = -1;
	currentActiveTexture = -1;
	currentDepthFunc = -1;
	currentCullFace = -1;
	currentDepthMask = 1;
	currentStencilMask = 0xFFFFFFFF;
	currentVertexInput = nullptr;
	hConservativeRaster = nullptr;
	hMultisample = nullptr;

	currentColorMask = std::unordered_map<intptr_t, int>();
	currentDrawBuffers = std::vector<GLenum>();

	currentSampler = std::unordered_map<int, intptr_t>();
	currentTexture = std::unordered_map<GLenum, std::unordered_map<int, intptr_t>>();
	currentBuffer = std::unordered_map<int, std::tuple<intptr_t, intptr_t, intptr_t>>();
	modes = std::unordered_map<intptr_t, bool>();
	patchParameters = std::unordered_map<intptr_t, intptr_t>();

	currentPolygonMode = std::pair<intptr_t, intptr_t>(-1, -1);
	blendFunc = std::tuple<intptr_t, intptr_t, intptr_t, intptr_t>(-1, -1, -1, -1);
	blendEquation = std::tuple<intptr_t, intptr_t>(-1, -1);
	blendColor = std::tuple<intptr_t, intptr_t, intptr_t, intptr_t>(-1, -1, -1, -1);
	stencilFunc = std::tuple<intptr_t, intptr_t, intptr_t, intptr_t>(-1, -1, -1, -1);
	stencilOp = std::tuple<intptr_t, intptr_t, intptr_t, intptr_t>(-1, -1, -1, -1);	
	
}

State::~State()
{
	Reset();
}

void State::Reset()
{
	removedInstructions = 0;
	currentVertexArray = -1;
	currentProgram = -1;
	currentActiveTexture = -1;
	currentDepthFunc = -1;
	currentCullFace = -1;
	currentDepthMask = 1;
	currentStencilMask = 0xFFFFFFFF;
	currentVertexInput = nullptr;

	currentColorMask.clear();
	currentDrawBuffers.clear();

	currentSampler.clear();
	currentTexture.clear();
	currentBuffer.clear();
	modes.clear();
	patchParameters.clear();

	currentPolygonMode = std::pair<intptr_t, intptr_t>(-1, -1);
	blendFunc = std::tuple<intptr_t, intptr_t, intptr_t, intptr_t>(-1, -1, -1, -1);
	blendEquation = std::tuple<intptr_t, intptr_t>(-1, -1);
	blendColor = std::tuple<intptr_t, intptr_t, intptr_t, intptr_t>(-1, -1, -1, -1);
	stencilFunc = std::tuple<intptr_t, intptr_t, intptr_t, intptr_t>(-1, -1, -1, -1);
	stencilOp = std::tuple<intptr_t, intptr_t, intptr_t, intptr_t>(-1, -1, -1, -1);

	hDepthTest = nullptr;
	hCullFace = nullptr;
	hPolygonMode = nullptr;
	hStencilModeFront = nullptr;
	hStencilModeBack = nullptr;
	hConservativeRaster = nullptr;
	hMultisample = nullptr;
}


bool State::HShouldSetConservativeRaster(int* enabled)
{
	if (hConservativeRaster == nullptr || memcmp(hConservativeRaster, enabled, sizeof(int)) != 0)
	{
		hConservativeRaster = enabled;
		return true;
	}
	else 
	{
		removedInstructions++;
		return false;
	}
}


bool State::HShouldSetMultisample(int* enabled)
{
	if (hMultisample == nullptr || memcmp(hMultisample, enabled, sizeof(int)) != 0)
	{
		hMultisample = enabled;
		return true;
	}
	else
	{
		removedInstructions++;
		return false;
	}
}

bool State::HShouldSetDepthTest(int* test)
{
	if (hDepthTest == nullptr || memcmp(hDepthTest, test, sizeof(int)) != 0)
	{
		hDepthTest = test;
		return true;
	}
	else
	{
		removedInstructions++;
		return false;
	}
}

bool State::HShouldSetCullFace(GLenum* test)
{
	if (hCullFace == nullptr || *hCullFace != *test)
	{
		hCullFace = test;
		return true;
	}
	else
	{
		removedInstructions++;
		return false;
	}
}

bool State::HShouldSetPolygonMode(GLenum* test)
{
	if (hPolygonMode == nullptr || *hPolygonMode != *test)
	{
		hPolygonMode = test;
		return true;
	}
	else
	{
		removedInstructions++;
		return false;
	}
}

bool State::HShouldSetBlendModes(int count, BlendMode** test)
{
	// TODO: Implement or remove because interpreter is dead?
	return true;
}

bool State::HShouldSetStencilMode(StencilMode* front, StencilMode* back)
{
	if (hStencilModeFront == nullptr || memcmp(hStencilModeFront, front, sizeof(StencilMode)) != 0 ||
		hStencilModeBack == nullptr || memcmp(hStencilModeBack, back, sizeof(StencilMode)) != 0)
	{
		hStencilModeFront = front;
		hStencilModeBack = back;
		return true;
	}
	else
	{
		removedInstructions++;
		return false;
	}
}



bool State::ShouldSetProgram(intptr_t program)
{
	if (currentProgram != program)
	{
		currentProgram = program;
		return true;
	}
	else
	{
		removedInstructions++;
		return false;
	}
}

bool State::ShouldSetVertexArray(intptr_t vao)
{
	if (currentVertexArray != vao)
	{
		currentVertexArray = vao;
		return true;
	}
	else
	{
		removedInstructions++;
		return false;
	}
}

bool State::ShouldSetActiveTexture(intptr_t unit)
{
	if (currentActiveTexture != unit)
	{
		currentActiveTexture = unit;
		return true;
	}
	else
	{
		removedInstructions++;
		return false;
	}
}

bool State::ShouldSetStencilMask(intptr_t mask)
{
	if (currentStencilMask != mask)
	{
		currentStencilMask = mask;
		return true;
	}
	else
	{
		removedInstructions++;
		return false;
	}
}

bool State::ShouldSetDepthMask(intptr_t mask)
{
	if (currentDepthMask != mask)
	{
		currentDepthMask = mask;
		return true;
	}
	else
	{
		removedInstructions++;
		return false;
	}
}

bool State::ShouldSetDrawBuffers(GLuint n, const GLenum* buffers)
{
	if (currentDrawBuffers.size() == n)
	{
		bool equal = true;
		for (GLuint i = 0; i < n; i++)
		{
			if (buffers[i] != currentDrawBuffers[i])
			{
				equal = false;
				break;
			}
		}
		if (equal)
		{
			removedInstructions++;
			return false;
		}

	}

	currentDrawBuffers.clear();
	for (GLuint i = 0; i < n; i++)
	{
		currentDrawBuffers.push_back(buffers[i]);
	}
	return true;
}

bool State::ShouldSetColorMask(intptr_t index, intptr_t r, intptr_t g, intptr_t b, intptr_t a)
{
	int mask = ((r & 1) << 3) | ((g & 1 << 2)) | ((b & 1) << 1) | (a & 1);

	auto res = currentColorMask.find(index);
	if (res != currentColorMask.end())
	{
		if (res->second != mask)
		{
			currentColorMask[index] = mask;
			return true;
		}
		else
		{
			removedInstructions++;
			return false;
		}
	}
	else
	{
		currentColorMask[index] = mask;
		return true;
	}


}


bool State::ShouldSetTexture(GLenum target, intptr_t texture)
{
	auto res = currentTexture.find(target);
	if (res != currentTexture.end())
	{
		auto res2 = res->second.find((int)currentActiveTexture);
		if (res2 != res->second.end())
		{
			if (res2->second != texture)
			{
				res->second[(int)currentActiveTexture] = texture;
				return true;
			}
			else
			{
				removedInstructions++;
				return false;
			}
		}
		else
		{
			res->second[(int)currentActiveTexture] = texture;
			return true;
		}
	}
	else
	{
		std::unordered_map<int, intptr_t> map;
		map[(int)currentActiveTexture] = texture;
		currentTexture[target] = map;
		return true;
	}
}

bool State::ShouldSetSampler(int index, intptr_t sampler)
{
	auto res = currentSampler.find(index);
	if (res != currentSampler.end())
	{
		if (res->second != sampler)
		{
			currentSampler[index] = sampler;
			return true;
		}
		else
		{
			removedInstructions++;
			return false;
		}
	}
	else
	{
		currentSampler[index] = sampler;
		return true;
	}
}

bool State::ShouldSetBuffer(GLenum target, int index, intptr_t buffer, intptr_t offset, intptr_t size)
{
	auto res = currentBuffer.find(index);
	if (res != currentBuffer.end())
	{
		if (std::get<0>(res->second) != buffer || std::get<1>(res->second) != offset || std::get<2>(res->second) != size)
		{
			currentBuffer[index] = std::make_tuple(buffer, offset, size);
			return true;
		}
		else
		{
			removedInstructions++;
			return false;
		}
	}
	else
	{
		currentBuffer[index] = std::make_tuple(buffer, offset, size);
		return true;
	}
}

bool State::ShouldEnable(intptr_t flag)
{
	auto res = modes.find(flag);
	if (res == modes.end() || res->second != true)
	{
		modes[flag] = true;
		return true;
	}
	else
	{
		removedInstructions++;
		return false;
	}
}

bool State::ShouldDisable(intptr_t flag)
{
	auto res = modes.find(flag);
	if (res == modes.end() || res->second != false)
	{
		modes[flag] = false;
		return true;
	}
	else
	{
		removedInstructions++;
		return false;
	}
}

bool State::ShouldSetDepthFunc(intptr_t func)
{
	if (currentDepthFunc != func)
	{
		currentDepthFunc = func;
		return true;
	}
	else
	{
		removedInstructions++;
		return false;
	}
}

bool State::ShouldSetCullFace(intptr_t face)
{
	if (currentCullFace != face)
	{
		currentCullFace = face;
		return true;
	}
	else
	{
		removedInstructions++;
		return false;
	}
}

bool State::ShouldSetPolygonMode(intptr_t face, intptr_t mode)
{
	if (std::get<0>(currentPolygonMode) != face || std::get<1>(currentPolygonMode) != mode)
	{
		currentPolygonMode = std::make_tuple(face, mode);
		return true;
	}
	else
	{
		removedInstructions++;
		return false;
	}
}

bool State::ShouldSetBlendFunc(intptr_t srcRgb, intptr_t dstRgb, intptr_t srcAlpha, intptr_t dstAlpha)
{
	if (std::get<0>(blendFunc) != srcRgb || std::get<1>(blendFunc) != dstRgb || std::get<2>(blendFunc) != srcAlpha || std::get<3>(blendFunc) != dstAlpha)
	{
		blendFunc = std::make_tuple(srcRgb, dstRgb, srcAlpha, dstAlpha);
		return true;
	}
	else
	{
		removedInstructions++;
		return false;
	}
}

bool State::ShouldSetBlendEquation(intptr_t arg0, intptr_t arg1)
{
	if (std::get<0>(blendEquation) != arg0 || std::get<1>(blendEquation) != arg1)
	{
		blendEquation = std::make_tuple(arg0, arg1);
		return true;
	}
	else
	{
		removedInstructions++;
		return false;
	}
}

bool State::ShouldSetBlendColor(intptr_t arg0, intptr_t arg1, intptr_t arg2, intptr_t arg3)
{
	if (std::get<0>(blendColor) != arg0 || std::get<1>(blendColor) != arg1 || std::get<2>(blendColor) != arg2 || std::get<3>(blendColor) != arg3)
	{
		blendColor = std::make_tuple(arg0, arg1, arg2, arg3);
		return true;
	}
	else
	{
		removedInstructions++;
		return false;
	}
}

bool State::ShouldSetStencilFunc(intptr_t arg0, intptr_t arg1, intptr_t arg2, intptr_t arg3)
{
	if (std::get<0>(stencilFunc) != arg0 || std::get<1>(stencilFunc) != arg1 || std::get<2>(stencilFunc) != arg2 || std::get<3>(stencilFunc) != arg3)
	{
		stencilFunc = std::make_tuple(arg0, arg1, arg2, arg3);
		return true;
	}
	else
	{
		removedInstructions++;
		return false;
	}
}

bool State::ShouldSetStencilOp(intptr_t arg0, intptr_t arg1, intptr_t arg2, intptr_t arg3)
{
	if (std::get<0>(stencilOp) != arg0 || std::get<1>(stencilOp) != arg1 || std::get<2>(stencilOp) != arg2 || std::get<3>(stencilOp) != arg3)
	{
		stencilOp = std::make_tuple(arg0, arg1, arg2, arg3);
		return true;
	}
	else
	{
		removedInstructions++;
		return false;
	}
}

bool State::ShouldSetPatchParameter(intptr_t parameter, intptr_t value)
{
	auto res = patchParameters.find(parameter);
	if (res != patchParameters.end() && res->second == value)
	{
		removedInstructions++;
		return false;
	}
	else
	{
		patchParameters[parameter] = value;
		return true;
	}
}

int State::GetRemovedInstructions()
{
	return removedInstructions;
}

bool State::HShouldBindVertexAttributes(VertexInputBinding* binding)
{
	if (currentVertexInput != binding)
	{
		currentVertexInput = binding;
		return true;
	}
	else
	{
		removedInstructions++;
		return false;
	}
}
