#pragma once

#ifdef __APPLE__
#include <opengl/gl3.h>
#define DllExport(t) extern "C" t
#elif __GNUC__
#include <GL/gl.h>
#include <GL/glx.h>
#define DllExport(t) extern "C" t
#else
#include "stdafx.h"
#include <stdio.h>
#include "glext.h"
#include <gl/GL.h>
#define DllExport(t) extern "C"  __declspec( dllexport ) t __cdecl
#endif

#include <vector>

#ifndef __APPLE__
#ifdef __GNUC__
static PFNGLACTIVETEXTUREPROC							glActiveTexture;
static PFNGLBLENDCOLORPROC								glBlendColor;
#endif
static PFNGLBINDVERTEXARRAYPROC							glBindVertexArray;
static PFNGLUSEPROGRAMPROC								glUseProgram;
static PFNGLBINDSAMPLERPROC								glBindSampler;
static PFNGLBINDBUFFERPROC								glBindBuffer;
static PFNGLBINDBUFFERBASEPROC							glBindBufferBase;
static PFNGLBINDBUFFERRANGEPROC							glBindBufferRange;
static PFNGLBINDFRAMEBUFFERPROC							glBindFramebuffer;
static PFNGLBLENDFUNCSEPARATEPROC						glBlendFuncSeparate;
static PFNGLBLENDEQUATIONSEPARATEPROC					glBlendEquationSeparate;
static PFNGLSTENCILFUNCSEPARATEPROC						glStencilFuncSeparate;
static PFNGLSTENCILOPSEPARATEPROC						glStencilOpSeparate;
static PFNGLPATCHPARAMETERIPROC							glPatchParameteri;
static PFNGLDRAWARRAYSINSTANCEDPROC						glDrawArraysInstanced;
static PFNGLVERTEXATTRIBPOINTERPROC						glVertexAttribPointer;
static PFNGLUNIFORM1FVPROC								glUniform1fv;
static PFNGLUNIFORM1IVPROC								glUniform1iv;
static PFNGLUNIFORM2FVPROC								glUniform2fv;
static PFNGLUNIFORM2IVPROC								glUniform2iv;
static PFNGLUNIFORM3FVPROC								glUniform3fv;
static PFNGLUNIFORM3IVPROC								glUniform3iv;
static PFNGLUNIFORM4FVPROC								glUniform4fv;
static PFNGLUNIFORM4IVPROC								glUniform4iv;
static PFNGLUNIFORMMATRIX2FVPROC						glUniformMatrix2fv;
static PFNGLUNIFORMMATRIX3FVPROC						glUniformMatrix3fv;
static PFNGLUNIFORMMATRIX4FVPROC						glUniformMatrix4fv;
static PFNGLVERTEXATTRIB1FPROC							glVertexAttrib1f;
static PFNGLVERTEXATTRIB2FPROC							glVertexAttrib2f;
static PFNGLVERTEXATTRIB3FPROC							glVertexAttrib3f;
static PFNGLVERTEXATTRIB4FPROC							glVertexAttrib4f;
static PFNGLCOLORMASKIPROC								glColorMaski;
static PFNGLDRAWBUFFERSPROC								glDrawBuffers;
static PFNGLMAPBUFFERRANGEPROC							glMapBufferRange;
static PFNGLUNMAPBUFFERPROC								glUnmapBuffer;
static PFNGLGETBUFFERPARAMETERIVPROC					glGetBufferParameteriv;
static PFNGLDRAWELEMENTSBASEVERTEXPROC					glDrawElementsBaseVertex;
static PFNGLDRAWELEMENTSINSTANCEDPROC					glDrawElementsInstanced;
#else
typedef void (APIENTRYP PFNGLDRAWARRAYSINSTANCEDBASEINSTANCEPROC) (GLenum mode, GLint first, GLsizei count, GLsizei primcount, GLuint baseinstance);
typedef void (APIENTRYP PFNGLDRAWELEMENTSINSTANCEDBASEVERTEXBASEINSTANCEPROC) (GLenum mode, GLsizei count, GLenum type, const void *indices, GLsizei primcount, GLint basevertex, GLuint baseinstance);
typedef void           (APIENTRYP PFNGLMULTIDRAWARRAYSINDIRECTPROC) (GLenum mode, const void *indirect, GLsizei drawcount, GLsizei stride);
typedef void           (APIENTRYP PFNGLMULTIDRAWELEMENTSINDIRECTPROC) (GLenum mode, GLenum type, const void *indirect, GLsizei drawcount, GLsizei stride);
#endif


static PFNGLDRAWARRAYSINSTANCEDBASEINSTANCEPROC			glDrawArraysInstancedBaseInstance;
static PFNGLDRAWELEMENTSINSTANCEDBASEVERTEXBASEINSTANCEPROC		glDrawElementsInstancedBaseVertexBaseInstance;
static PFNGLMULTIDRAWARRAYSINDIRECTPROC					glMultiDrawArraysIndirect;
static PFNGLMULTIDRAWELEMENTSINDIRECTPROC				glMultiDrawElementsIndirect;

// enum holding the available instruction codes
typedef enum {
	BindVertexArray = 1,
	BindProgram = 2,
	ActiveTexture = 3,
	BindSampler = 4,
	BindTexture = 5,
	BindBufferBase = 6,
	BindBufferRange = 7,
	BindFramebuffer = 8,
	Viewport = 9,
	Enable = 10,
	Disable = 11,
	DepthFunc = 12,
	CullFace = 13,
	BlendFuncSeparate = 14,
	BlendEquationSeparate = 15,
	BlendColor = 16,
	PolygonMode = 17,
	StencilFuncSeparate = 18,
	StencilOpSeparate = 19,
	PatchParameter = 20,
	DrawElements = 21,
	DrawArrays = 22,
	DrawElementsInstanced = 23,
	DrawArraysInstanced = 24,
	Clear = 25,
	BindImageTexture = 26,
	ClearColor = 27,
	ClearDepth = 28,
	GetError = 29,
	BindBuffer = 30,
	VertexAttribPointer = 31,
	VertexAttribDivisor = 32,
	EnableVertexAttribArray = 33,
	DisableVertexAttribArray = 34,
	Uniform1fv = 35,
	Uniform1iv = 36,
	Uniform2fv = 37,
	Uniform2iv = 38,
	Uniform3fv = 39,
	Uniform3iv = 40,
	Uniform4fv = 41,
	Uniform4iv = 42,
	UniformMatrix2fv = 43,
	UniformMatrix3fv = 44,
	UniformMatrix4fv = 45,
	TexParameteri = 46,
	TexParameterf = 47,
	VertexAttrib1f = 48,
	VertexAttrib2f = 49,
	VertexAttrib3f = 50,
	VertexAttrib4f = 51,
	MultiDrawArraysIndirect = 52,
	MultiDrawElementsIndirect = 53,
	DepthMask = 54,
	ColorMask = 55,
	StencilMask = 56,
	DrawBuffers = 57,

	HDrawArrays = 100,
	HDrawElements = 101,
	HDrawArraysIndirect = 102,
	HDrawElementsIndirect = 103,
	HSetDepthTest = 104,
	HSetCullFace = 105,
	HSetPolygonMode = 106,
	HSetBlendMode = 107,
	HSetStencilMode = 108


} InstructionCode;

// enum controlling the current execution mode
typedef enum {
	NoOptimization = 0x00000,
	RuntimeRedundancyChecks = 0x00001,
	RuntimeStateSorting = 0x00002 
} VMMode;

// an instruction consists of a code and up to 5 arguments. 
typedef struct {
	InstructionCode Code;
	intptr_t Arg0;
	intptr_t Arg1;
	intptr_t Arg2;
	intptr_t Arg3;
	intptr_t Arg4;
	intptr_t Arg5;
} Instruction;

// a fragment consists of a substructured vector of instructions
typedef struct FragStruct {
	std::vector<std::vector<Instruction>> Instructions;
	struct FragStruct* Next;
} Fragment;

// runtime statistics
typedef struct {
	int TotalInstructions;
	int RemovedInstructions;
} Statistics;

DllExport(void) vmInit();
DllExport(Fragment*) vmCreate();
DllExport(void) vmDelete(Fragment* frag);
DllExport(bool) vmHasNext(Fragment* frag);
DllExport(Fragment*) vmGetNext(Fragment* frag);
DllExport(void) vmLink(Fragment* left, Fragment* right);
DllExport(void) vmUnlink(Fragment* left);
DllExport(int) vmNewBlock(Fragment* frag);
DllExport(void) vmClearBlock(Fragment* frag, int block);
DllExport(void) vmAppend1(Fragment* frag, int block, InstructionCode code, intptr_t arg0);
DllExport(void) vmAppend2(Fragment* frag, int block, InstructionCode code, intptr_t arg0, intptr_t arg1);
DllExport(void) vmAppend3(Fragment* frag, int block, InstructionCode code, intptr_t arg0, intptr_t arg1, intptr_t arg2);
DllExport(void) vmAppend4(Fragment* frag, int block, InstructionCode code, intptr_t arg0, intptr_t arg1, intptr_t arg2, intptr_t arg3);
DllExport(void) vmAppend5(Fragment* frag, int block, InstructionCode code, intptr_t arg0, intptr_t arg1, intptr_t arg2, intptr_t arg3, intptr_t arg4);
DllExport(void) vmAppend6(Fragment* frag, int block, InstructionCode code, intptr_t arg0, intptr_t arg1, intptr_t arg2, intptr_t arg3, intptr_t arg4, intptr_t arg5);
DllExport(void) vmClear(Fragment* frag);
DllExport(void) vmRunSingle(Fragment* frag);
DllExport(void) vmRun(Fragment* frag, VMMode mode, Statistics& stats);

DllExport(void) hglDrawArrays(RuntimeStats* stats, int* isActive, BeginMode* mode, DrawCallInfoList* infos);
DllExport(void) hglDrawElements(RuntimeStats* stats, int* isActive, BeginMode* mode, GLenum indexType, DrawCallInfoList* infos);
DllExport(void) hglDrawArraysIndirect(RuntimeStats* stats, int* isActive, BeginMode* mode, GLint* count, GLuint buffer);
DllExport(void) hglDrawElementsIndirect(RuntimeStats* stats, int* isActive, BeginMode* mode, GLenum indexType, GLint* count, GLuint buffer);
DllExport(void) hglSetDepthTest(DepthTestMode* mode);
DllExport(void) hglSetCullFace(GLenum* face);
DllExport(void) hglSetPolygonMode(GLenum* mode);
DllExport(void) hglSetBlendMode(BlendMode* mode);
DllExport(void) hglSetStencilMode(StencilMode* mode);
DllExport(void) hglBindVertexArray(int* vao);
