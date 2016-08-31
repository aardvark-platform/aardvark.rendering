#ifndef __GNUC__
#include "stdafx.h"
#endif

#include "State.h"
#include "glvm.h"

#ifdef __GNUC__

static void* getProc(const char* name)
{
	void* ptr = (void*)glXGetProcAddressARB((const GLubyte*)name);
	if(ptr == nullptr)
		printf("could not import function %s\n", name);

	printf("function address for %s: %lX\n", name, (unsigned long int)ptr);

	return ptr;
}


#else

static PROC getProc(LPCSTR name)
{
	auto ptr = wglGetProcAddress(name);

	if (ptr == nullptr)
		printf("could not import function %s\n", name);

	return ptr;
}

#endif
#define trace(a) printf(a)
#define endtrace(a) { printf("%s: %d\n", (a), glGetError()); glFlush(); glFinish(); }



static bool initialized = false;

DllExport(void) vmInit()
{
	//printf("asdasd\n");
	if (initialized)
		return;

	initialized = true;

	#ifndef __GNUC__
	glActiveTexture = (PFNGLACTIVETEXTUREPROC)getProc("glActiveTexture");
	glBlendColor = (PFNGLBLENDCOLORPROC)getProc("glBlendColor");
	#endif

	glBindVertexArray = (PFNGLBINDVERTEXARRAYPROC)getProc("glBindVertexArray");
	glUseProgram = (PFNGLUSEPROGRAMPROC)getProc("glUseProgram");
	glBindSampler = (PFNGLBINDSAMPLERPROC)getProc("glBindSampler");
	glBindBuffer = (PFNGLBINDBUFFERPROC)getProc("glBindBuffer");
	glBindBufferBase = (PFNGLBINDBUFFERBASEPROC)getProc("glBindBufferBase");
	glBindBufferRange = (PFNGLBINDBUFFERRANGEPROC)getProc("glBindBufferRange");
	glBindFramebuffer = (PFNGLBINDFRAMEBUFFERPROC)getProc("glBindFramebuffer");
	glBlendFuncSeparate = (PFNGLBLENDFUNCSEPARATEPROC)getProc("glBlendFuncSeparate");
	glBlendEquationSeparate = (PFNGLBLENDEQUATIONSEPARATEPROC)getProc("glBlendEquationSeparate");
	glStencilFuncSeparate = (PFNGLSTENCILFUNCSEPARATEPROC)getProc("glStencilFuncSeparate");
	glStencilOpSeparate = (PFNGLSTENCILOPSEPARATEPROC)getProc("glStencilOpSeparate");
	glPatchParameteri = (PFNGLPATCHPARAMETERIPROC)getProc("glPatchParameteri");
	glDrawArraysInstanced = (PFNGLDRAWARRAYSINSTANCEDPROC)getProc("glDrawArraysInstanced");
	glDrawElementsBaseVertex = (PFNGLDRAWELEMENTSBASEVERTEXPROC)getProc("glDrawElementsBaseVertex");
	glDrawArraysInstancedBaseInstance = (PFNGLDRAWARRAYSINSTANCEDBASEINSTANCEPROC)getProc("glDrawArraysInstancedBaseInstance");
	glDrawElementsInstancedBaseVertexBaseInstance = (PFNGLDRAWELEMENTSINSTANCEDBASEVERTEXBASEINSTANCEPROC)getProc("glDrawElementsInstancedBaseVertexBaseInstance");
	glVertexAttribPointer = (PFNGLVERTEXATTRIBPOINTERPROC)getProc("glVertexAttribPointer");
	glUniform1fv = (PFNGLUNIFORM1FVPROC)getProc("glUniform1fv");
	glUniform1iv = (PFNGLUNIFORM1IVPROC)getProc("glUniform1iv");
	glUniform2fv = (PFNGLUNIFORM2FVPROC)getProc("glUniform2fv");
	glUniform2iv = (PFNGLUNIFORM2IVPROC)getProc("glUniform2iv");
	glUniform3fv = (PFNGLUNIFORM3FVPROC)getProc("glUniform3fv");
	glUniform3iv = (PFNGLUNIFORM3IVPROC)getProc("glUniform3iv");
	glUniform4fv = (PFNGLUNIFORM4FVPROC)getProc("glUniform4fv");
	glUniform4iv = (PFNGLUNIFORM4IVPROC)getProc("glUniform4iv");
	glUniformMatrix2fv = (PFNGLUNIFORMMATRIX2FVPROC)getProc("glUniformMatrix2fv");
	glUniformMatrix3fv = (PFNGLUNIFORMMATRIX3FVPROC)getProc("glUniformMatrix3fv");
	glUniformMatrix4fv = (PFNGLUNIFORMMATRIX4FVPROC)getProc("glUniformMatrix4fv");
	glVertexAttrib1f = (PFNGLVERTEXATTRIB1FPROC)getProc("glVertexAttrib1f");
	glVertexAttrib2f = (PFNGLVERTEXATTRIB2FPROC)getProc("glVertexAttrib2f");
	glVertexAttrib3f = (PFNGLVERTEXATTRIB3FPROC)getProc("glVertexAttrib3f");
	glVertexAttrib4f = (PFNGLVERTEXATTRIB4FPROC)getProc("glVertexAttrib4f");

	glMultiDrawArraysIndirect = (PFNGLMULTIDRAWARRAYSINDIRECTPROC)getProc("glMultiDrawArraysIndirect");
	glMultiDrawElementsIndirect = (PFNGLMULTIDRAWELEMENTSINDIRECTPROC)getProc("glMultiDrawElementsIndirect");
	glColorMaski = (PFNGLCOLORMASKIPROC)getProc("glColorMaski");
	glDrawBuffers = (PFNGLDRAWBUFFERSPROC)getProc("glDrawBuffers");
	glMapBufferRange = (PFNGLMAPBUFFERRANGEPROC)getProc("glMapBufferRange");
	glUnmapBuffer = (PFNGLUNMAPBUFFERPROC)getProc("glUnmapBuffer");
	glGetBufferParameteriv = (PFNGLGETBUFFERPARAMETERIVPROC)getProc("glGetBufferParameteriv");

	glDrawElementsInstanced = (PFNGLDRAWELEMENTSINSTANCEDPROC)getProc("glDrawElementsInstanced");


}

DllExport(Fragment*) vmCreate()
{
	Fragment* ptr = new Fragment();
	ptr->Instructions = std::vector<std::vector<Instruction>>();
	ptr->Next = nullptr;
	return ptr;
}

DllExport(void) vmDelete(Fragment* frag)
{
	frag->Instructions.clear();
	frag->Next = nullptr;
	delete frag;
}

DllExport(bool) vmHasNext(Fragment* frag)
{
	return frag->Next != nullptr;
}

DllExport(Fragment*) vmGetNext(Fragment* frag)
{
	return frag->Next;
}


DllExport(void) vmLink(Fragment* left, Fragment* right)
{
	left->Next = right;
}

DllExport(void) vmUnlink(Fragment* left)
{
	left->Next = nullptr;
}


DllExport(int) vmNewBlock(Fragment* frag)
{
	int s = (int)frag->Instructions.size();
	frag->Instructions.push_back(std::vector<Instruction>());
	return s;
}

DllExport(void) vmClearBlock(Fragment* frag, int block)
{
	frag->Instructions[block].clear();
}

DllExport(void) vmAppend1(Fragment* frag, int block, InstructionCode code, intptr_t arg0)
{
	frag->Instructions[block].push_back({ code, arg0, 0, 0, 0, 0 });
}

DllExport(void) vmAppend2(Fragment* frag, int block, InstructionCode code, intptr_t arg0, intptr_t arg1)
{
	frag->Instructions[block].push_back({ code, arg0, arg1, 0, 0, 0 });
}

DllExport(void) vmAppend3(Fragment* frag, int block, InstructionCode code, intptr_t arg0, intptr_t arg1, intptr_t arg2)
{
	frag->Instructions[block].push_back({ code, arg0, arg1, arg2, 0, 0 });
}

DllExport(void) vmAppend4(Fragment* frag, int block, InstructionCode code, intptr_t arg0, intptr_t arg1, intptr_t arg2, intptr_t arg3)
{
	frag->Instructions[block].push_back({ code, arg0, arg1, arg2, arg3, 0 });
}

DllExport(void) vmAppend5(Fragment* frag, int block, InstructionCode code, intptr_t arg0, intptr_t arg1, intptr_t arg2, intptr_t arg3, intptr_t arg4)
{
	frag->Instructions[block].push_back({ code, arg0, arg1, arg2, arg3, arg4 });
}

DllExport(void) vmClear(Fragment* frag)
{
	frag->Instructions.clear();
}

void runInstruction(Instruction* i)
{
	switch (i->Code)
	{
	case BindVertexArray:
		glBindVertexArray((GLuint)i->Arg0);
		break;
	case BindProgram:
		glUseProgram((GLuint)i->Arg0);
		break;
	case ActiveTexture:
		glActiveTexture((GLenum)i->Arg0);
		break;
	case BindSampler:
		glBindSampler((GLuint)i->Arg0, (GLuint)i->Arg1);
		break;
	case BindTexture:
		glBindTexture((GLenum)i->Arg0, (GLuint)i->Arg1);
		break;
	case BindBufferBase:
		glBindBufferBase((GLenum)i->Arg0, (GLuint)i->Arg1, (GLuint)i->Arg2);
		break;
	case BindBufferRange:
		glBindBufferRange((GLenum)i->Arg0, (GLuint)i->Arg1, (GLuint)i->Arg2, (GLuint)i->Arg3, (GLsizeiptr)i->Arg4);
		break;
	case BindFramebuffer:
		glBindFramebuffer((GLenum)i->Arg0, (GLuint)i->Arg1);
		break;
	case Viewport:
		glViewport((GLint)i->Arg0, (GLint)i->Arg1, (GLint)i->Arg2, (GLint)i->Arg3);
		break;
	case Enable:
		glEnable((GLenum)i->Arg0);
		break;
	case Disable:
		glDisable((GLenum)i->Arg0);
		break;
	case DepthFunc:
		glDepthFunc((GLenum)i->Arg0);
		break;
	case CullFace:
		glCullFace((GLenum)i->Arg0);
		break;
	case BlendFuncSeparate:
		glBlendFuncSeparate((GLenum)i->Arg0, (GLenum)i->Arg1, (GLenum)i->Arg2, (GLenum)i->Arg3);
		break;
	case BlendEquationSeparate:
		glBlendEquationSeparate((GLenum)i->Arg0, (GLenum)i->Arg1);
		break;
	case BlendColor:
		glBlendColor((GLfloat)i->Arg0, (GLfloat)i->Arg1, (GLfloat)i->Arg2, (GLfloat)i->Arg3);
		break;
	case PolygonMode:
		glPolygonMode((GLenum)i->Arg0, (GLenum)i->Arg1);
		break;
	case StencilFuncSeparate:
		glStencilFuncSeparate((GLenum)i->Arg0, (GLenum)i->Arg1, (GLint)i->Arg2, (GLuint)i->Arg3);
		break;
	case StencilOpSeparate:
		glStencilOpSeparate((GLenum)i->Arg0, (GLenum)i->Arg1, (GLenum)i->Arg2, (GLenum)i->Arg3);
		break;
	case PatchParameter:
		glPatchParameteri((GLenum)i->Arg0, (GLint)i->Arg1);
		break;
	case DrawElements:
		glDrawElements((GLenum)i->Arg0, (GLsizei)i->Arg1, (GLenum)i->Arg2, (GLvoid*)i->Arg3);
		break;
	case DrawArrays:
		glDrawArrays((GLenum)i->Arg0, (GLint)i->Arg1, (GLsizei)i->Arg2);
		break;
	case DrawElementsInstanced:
		glDrawElementsInstanced((GLenum)i->Arg0, (GLsizei)i->Arg1, (GLenum)i->Arg2, (GLvoid*)i->Arg3, (GLuint)i->Arg4);
		break;
	case DrawArraysInstanced:
		glDrawArraysInstanced((GLenum)i->Arg0, (GLint)i->Arg1, (GLsizei)i->Arg2, (GLuint)i->Arg3);
		break;
	case Clear:
		glClear((GLbitfield)i->Arg0);
		break;
	case VertexAttribPointer:
		glVertexAttribPointer((GLuint)i->Arg0, (GLint)i->Arg1, (GLenum)i->Arg2, (GLboolean)i->Arg3, (GLsizei)i->Arg4, nullptr);
		break;
	case Uniform1fv:
		glUniform1fv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLfloat*)i->Arg2);
		break;
	case Uniform2fv:
		glUniform2fv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLfloat*)i->Arg2);
		break;
	case Uniform3fv:
		glUniform3fv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLfloat*)i->Arg2);
		break;
	case Uniform4fv:
		glUniform4fv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLfloat*)i->Arg2);
		break;
	case Uniform1iv:
		glUniform1iv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLint*)i->Arg2);
		break;
	case Uniform2iv:
		glUniform2iv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLint*)i->Arg2);
		break;
	case Uniform3iv:
		glUniform3iv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLint*)i->Arg2);
		break;
	case Uniform4iv:
		glUniform4iv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLint*)i->Arg2);
		break;
	case UniformMatrix2fv:
		glUniformMatrix2fv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLboolean)i->Arg2, (GLfloat*)i->Arg3);
		break;
	case UniformMatrix3fv:
		glUniformMatrix3fv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLboolean)i->Arg2, (GLfloat*)i->Arg3);
		break;
	case UniformMatrix4fv:
		glUniformMatrix4fv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLboolean)i->Arg2, (GLfloat*)i->Arg3);
		break;
	case TexParameteri:
		glTexParameteri((GLenum)i->Arg0, (GLenum)i->Arg1, (GLint)i->Arg2);
		break;
	case TexParameterf:
		glTexParameterf((GLenum)i->Arg0, (GLenum)i->Arg1, *((GLfloat*)&i->Arg2));
		break;
	case VertexAttrib1f:
		glVertexAttrib1f((GLuint)i->Arg0, *((GLfloat*)&i->Arg1));
		break;
	case VertexAttrib2f:
		glVertexAttrib2f((GLuint)i->Arg0, *((GLfloat*)&i->Arg1), *((GLfloat*)&i->Arg2));
		break;
	case VertexAttrib3f:
		glVertexAttrib3f((GLuint)i->Arg0, *((GLfloat*)&i->Arg1), *((GLfloat*)&i->Arg2), *((GLfloat*)&i->Arg3));
		break;
	case VertexAttrib4f:
		glVertexAttrib4f((GLuint)i->Arg0, *((GLfloat*)&i->Arg1), *((GLfloat*)&i->Arg2), *((GLfloat*)&i->Arg3), *((GLfloat*)&i->Arg4));
		break;
	case BindBuffer:
		glBindBuffer((GLenum)i->Arg0, (GLuint)i->Arg1);
		break;
	case MultiDrawArraysIndirect:
		glMultiDrawArraysIndirect((GLenum)i->Arg0, (const void*)i->Arg1, *((GLsizei*)i->Arg2), (GLsizei)i->Arg3);
		break;
	case MultiDrawElementsIndirect:
		glMultiDrawElementsIndirect((GLenum)i->Arg0, (GLenum)i->Arg1, (const void*)i->Arg2, *((GLsizei*)i->Arg3), (GLsizei)i->Arg4);
		break;
	case DepthMask:
		glDepthMask((GLboolean)i->Arg0);
		break;
	case ColorMask:
		glColorMaski((GLuint)i->Arg0, (GLboolean)i->Arg1, (GLboolean)i->Arg2, (GLboolean)i->Arg3, (GLboolean)i->Arg4);
		break;
	case StencilMask:
		glStencilMask((GLuint)i->Arg0);
		break;
	case DrawBuffers:
		glDrawBuffers((GLuint)i->Arg0, (const GLenum*)i->Arg1);
		break;
	default:
		printf("unknown instruction code: %d\n", i->Code);
		break;
	}
}

Statistics runNoRedundancyChecks(Fragment* frag)
{
	int total = 0;
	Fragment* current = frag;
	while (current != nullptr)
	{
		for (auto itb = current->Instructions.begin(); itb != current->Instructions.end(); ++itb)
		{
			for (auto it = itb->begin(); it != itb->end(); ++it)
			{
				runInstruction(&(*it));
				total++;
			}
		}
		current = current->Next;
	}

	return { total, 0 };
}

Statistics runRedundancyChecks(Fragment* frag)
{
	State state;
	int totalInstructions = 0;

	Fragment* current = frag;
	while (current != nullptr)
	{
		for (auto itb = current->Instructions.begin(); itb != current->Instructions.end(); ++itb)
		{
			for (auto it = itb->begin(); it != itb->end(); ++it)
			{
				totalInstructions++;
				Instruction* i = &(*it);

				intptr_t arg0 = i->Arg0;

				switch (i->Code)
				{
				case BindVertexArray:
					if (state.ShouldSetVertexArray(arg0))
					{
						glBindVertexArray((GLuint)arg0);
					}
					break;
				case BindProgram:
					if (state.ShouldSetProgram(arg0))
					{
						glUseProgram((GLuint)arg0);
					}
					break;
				case ActiveTexture:
					if (state.ShouldSetActiveTexture(arg0))
					{
						glActiveTexture((GLenum)arg0);
					}
					break;
				case BindSampler:
					if (state.ShouldSetSampler((int)arg0, i->Arg1))
					{
						glBindSampler((GLuint)arg0, (GLuint)i->Arg1);
					}
					break;
				case BindTexture:
					if (state.ShouldSetTexture((GLenum)arg0, i->Arg1))
					{
						glBindTexture((GLenum)arg0, (GLuint)i->Arg1);
					}
					break;
				case BindBufferBase:
					if (state.ShouldSetBuffer((GLenum)arg0, (int)i->Arg1, i->Arg2, 0, 0))
					{
						glBindBufferBase((GLenum)arg0, (GLuint)i->Arg1, (GLuint)i->Arg2);
					}
					break;
				case BindBufferRange:
					if (state.ShouldSetBuffer((GLenum)arg0, (int)i->Arg1, i->Arg2, i->Arg3, i->Arg4))
					{
						glBindBufferRange((GLenum)arg0, (GLuint)i->Arg1, (GLuint)i->Arg2, (GLuint)i->Arg3, (GLsizeiptr)i->Arg4);
					}
					break;
				case Enable:
					if (state.ShouldEnable(arg0))
					{
						glEnable((GLenum)arg0);
					}
					break;
				case Disable:
					if (state.ShouldDisable(arg0))
					{
						glDisable((GLenum)arg0);
					}
					break;
				case DepthFunc:
					if (state.ShouldSetDepthFunc(arg0))
					{
						glDepthFunc((GLenum)arg0);
					}
					break;
				case CullFace:
					if (state.ShouldSetCullFace(arg0))
					{
						glCullFace((GLenum)arg0);
					}
					break;
				case BlendFuncSeparate:
					if (state.ShouldSetBlendFunc(arg0, i->Arg1, i->Arg2, i->Arg3))
					{
						glBlendFuncSeparate((GLenum)arg0, (GLenum)i->Arg1, (GLenum)i->Arg2, (GLenum)i->Arg3);
					}
					break;
				case BlendEquationSeparate:
					if (state.ShouldSetBlendEquation(arg0, i->Arg1))
					{
						glBlendEquationSeparate((GLenum)arg0, (GLenum)i->Arg1);
					}
					break;
				case BlendColor:
					if (state.ShouldSetBlendColor(arg0, i->Arg1, i->Arg2, i->Arg3))
					{
						glBlendColor((GLfloat)arg0, (GLfloat)i->Arg1, (GLfloat)i->Arg2, (GLfloat)i->Arg3);
					}
					break;
				case PolygonMode:
					if (state.ShouldSetPolygonMode(arg0, i->Arg1))
					{
						glPolygonMode((GLenum)arg0, (GLenum)i->Arg1);
					}
					break;
				case StencilFuncSeparate:
					if (state.ShouldSetStencilFunc(arg0, i->Arg1, i->Arg2, i->Arg3))
					{
						glStencilFuncSeparate((GLenum)arg0, (GLenum)i->Arg1, (GLint)i->Arg2, (GLuint)i->Arg3);
					}
					break;
				case StencilOpSeparate:
					if (state.ShouldSetStencilOp(arg0, i->Arg1, i->Arg2, i->Arg3))
					{
						glStencilOpSeparate((GLenum)arg0, (GLenum)i->Arg1, (GLenum)i->Arg2, (GLenum)i->Arg3);
					}
					break;
				case PatchParameter:
					if (state.ShouldSetPatchParameter(arg0, i->Arg1))
					{
						glPatchParameteri((GLenum)arg0, (GLint)i->Arg1);
					}
					break;

				case DepthMask:
					if (state.ShouldSetDepthMask(arg0))
					{
						glDepthMask((GLboolean)arg0);
					}
					break;
				case StencilMask:
					if (state.ShouldSetStencilMask(arg0))
					{
						glStencilMask((GLuint)arg0);
					}
					break;
				case ColorMask:
					if (state.ShouldSetColorMask(arg0, i->Arg1, i->Arg2, i->Arg3, i->Arg4))
					{
						glColorMaski((GLuint)arg0, (GLboolean)i->Arg1, (GLboolean)i->Arg2, (GLboolean)i->Arg3, (GLboolean)i->Arg4);
					}
					break;

				case DrawBuffers:
					if (state.ShouldSetDrawBuffers((GLuint)arg0, (const GLenum*)i->Arg1))
					{
						glDrawBuffers((GLsizei)arg0, (const GLenum*)i->Arg1);
					}
					break;

				case BindFramebuffer:
					glBindFramebuffer((GLenum)i->Arg0, (GLuint)i->Arg1);
					break;
				case Viewport:
					glViewport((GLint)i->Arg0, (GLint)i->Arg1, (GLint)i->Arg2, (GLint)i->Arg3);
					break;
				case DrawElements:
					glDrawElements((GLenum)i->Arg0, (GLsizei)i->Arg1, (GLenum)i->Arg2, (GLvoid*)i->Arg3);
					break;
				case DrawArrays:
					glDrawArrays((GLenum)i->Arg0, (GLint)i->Arg1, (GLsizei)i->Arg2);
					break;
				case DrawElementsInstanced:
					glDrawElementsInstanced((GLenum)i->Arg0, (GLsizei)i->Arg1, (GLenum)i->Arg2, (GLvoid*)i->Arg3, (GLuint)i->Arg4);
					break;
				case DrawArraysInstanced:
					glDrawArraysInstanced((GLenum)i->Arg0, (GLint)i->Arg1, (GLsizei)i->Arg2, (GLuint)i->Arg3);
					break;
				case Clear:
					glClear((GLbitfield)i->Arg0);
					break;
				case VertexAttribPointer:
					glVertexAttribPointer((GLuint)i->Arg0, (GLint)i->Arg1, (GLenum)i->Arg2, (GLboolean)i->Arg3, (GLsizei)i->Arg4, nullptr);
					break;
				case Uniform1fv:
					glUniform1fv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLfloat*)i->Arg2);
					break;
				case Uniform2fv:
					glUniform2fv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLfloat*)i->Arg2);
					break;
				case Uniform3fv:
					glUniform3fv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLfloat*)i->Arg2);
					break;
				case Uniform4fv:
					glUniform4fv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLfloat*)i->Arg2);
					break;
				case Uniform1iv:
					glUniform1iv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLint*)i->Arg2);
					break;
				case Uniform2iv:
					glUniform2iv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLint*)i->Arg2);
					break;
				case Uniform3iv:
					glUniform3iv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLint*)i->Arg2);
					break;
				case Uniform4iv:
					glUniform4iv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLint*)i->Arg2);
					break;
				case UniformMatrix2fv:
					glUniformMatrix2fv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLboolean)i->Arg2, (GLfloat*)i->Arg3);
					break;
				case UniformMatrix3fv:
					glUniformMatrix3fv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLboolean)i->Arg2, (GLfloat*)i->Arg3);
					break;
				case UniformMatrix4fv:
					glUniformMatrix4fv((GLint)i->Arg0, (GLsizei)i->Arg1, (GLboolean)i->Arg2, (GLfloat*)i->Arg3);
					break;

				case TexParameteri:
					glTexParameteri((GLenum)i->Arg0, (GLenum)i->Arg1, (GLint)i->Arg2);
					break;
				case TexParameterf:
					glTexParameterf((GLenum)i->Arg0, (GLenum)i->Arg1, *((GLfloat*)&i->Arg2));
					break;
				case VertexAttrib1f:
					glVertexAttrib1f((GLuint)i->Arg0, *((GLfloat*)&i->Arg1));
					break;
				case VertexAttrib2f:
					glVertexAttrib2f((GLuint)i->Arg0, *((GLfloat*)&i->Arg1), *((GLfloat*)&i->Arg2));
					break;
				case VertexAttrib3f:
					glVertexAttrib3f((GLuint)i->Arg0, *((GLfloat*)&i->Arg1), *((GLfloat*)&i->Arg2), *((GLfloat*)&i->Arg3));
					break;
				case VertexAttrib4f:
					glVertexAttrib4f((GLuint)i->Arg0, *((GLfloat*)&i->Arg1), *((GLfloat*)&i->Arg2), *((GLfloat*)&i->Arg3), *((GLfloat*)&i->Arg4));
					break;

				case BindBuffer:
					glBindBuffer((GLenum)i->Arg0, (GLuint)i->Arg1);
					break;

				case MultiDrawArraysIndirect:
					glMultiDrawArraysIndirect((GLenum)i->Arg0, (const void*)i->Arg1, *((GLsizei*)i->Arg2), (GLsizei)i->Arg3);
					break;
				case MultiDrawElementsIndirect:
					glMultiDrawElementsIndirect((GLenum)i->Arg0, (GLenum)i->Arg1, (const void*)i->Arg2, *((GLsizei*)i->Arg3), (GLsizei)i->Arg4);
					break;

				default:
					printf("unknown instruction code: %d\n", i->Code);
					break;
				}

			}
		}
		current = current->Next;
	}

	int rem = state.GetRemovedInstructions();
	state.Reset();
	return { totalInstructions, rem };
}


DllExport(void) vmRunSingle(Fragment* frag)
{
	if (!initialized)
	{
		printf("vm not initialized\n");
		return;
	}

	for (auto itb = frag->Instructions.begin(); itb != frag->Instructions.end(); ++itb)
	{
		for (auto it = itb->begin(); it != itb->end(); ++it)
		{
			runInstruction(&(*it));
		}
	}
}

DllExport(void) vmRun(Fragment* frag, VMMode mode, Statistics& stats)
{
	//DebugBreak();
	if (!initialized)
	{
		printf("vm not initialized\n");
		return;
	}

	if ((mode & RuntimeRedundancyChecks) != 0)
	{
		auto s = runRedundancyChecks(frag);
		stats = s;
	}
	else
	{
		auto s = runNoRedundancyChecks(frag);
		stats = s;
	}
}







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
	GLenum CmpFront;
	int32_t MaskFront;
	uint32_t ReferenceFront;
	GLenum CmpBack;
	int32_t MaskBack;
	uint32_t ReferenceBack;
	GLenum OpFrontSF;
	GLenum OpFrontDF;
	GLenum OpFrontPass;
	GLenum OpBackSF;
	GLenum OpBackDF;
	GLenum OpBackPass;
} StencilMode;

typedef struct {
	GLenum Mode;
	int PatchVertices;
} BeginMode;


DllExport(void) hglDrawArrays(int* isActive, BeginMode* mode, DrawCallInfoList* infos)
{
	trace("hglDrawArrays\n");
	if (!*isActive) return;

	auto cnt = (int)infos->Count;
	auto info = infos->Infos;
	auto m = mode->Mode;
	auto v = mode->PatchVertices;
	if (m == GL_PATCHES) glPatchParameteri(GL_PATCH_VERTICES, v);

	for (int i = 0; i < cnt; i++, info += 1)
	{
		if (info->InstanceCount != 1 || info->FirstInstance != 0)
		{
			glDrawArraysInstancedBaseInstance(m, info->FirstIndex, info->FaceVertexCount, info->InstanceCount, info->FirstInstance);
		}
		else
		{
			glDrawArrays(m, info->FirstIndex, info->FaceVertexCount);
		}
	}
	endtrace("hglDrawArrays")
}

DllExport(void) hglDrawElements(int* isActive, BeginMode* mode, GLenum indexType, DrawCallInfoList* infos)
{
	trace("hglDrawElements\n");
	if (!*isActive) return;

	auto cnt = infos->Count;
	auto info = infos->Infos;
	auto m = mode->Mode;
	auto v = mode->PatchVertices;
	if (m == GL_PATCHES) glPatchParameteri(GL_PATCH_VERTICES, v);

	for (int i = 0; i < cnt; i++, info += 1)
	{
		if (info->InstanceCount != 1 || info->FirstInstance != 0)
		{
			glDrawElementsInstancedBaseVertexBaseInstance(m, info->FaceVertexCount, indexType, (const void*)(int64_t)info->FirstIndex, info->InstanceCount, info->BaseVertex, info->FirstInstance);
		}
		else
		{
			auto offset = int64_t(info->FirstIndex * sizeof(int));

			GLint ibo, size;
			glGetIntegerv(GL_ELEMENT_ARRAY_BUFFER_BINDING, &ibo);
			glGetBufferParameteriv(GL_ELEMENT_ARRAY_BUFFER, GL_BUFFER_SIZE, &size);
			printf("ibo %d: %d\n", ibo, size);

			auto ptr = (int*)glMapBufferRange(GL_ELEMENT_ARRAY_BUFFER, 0, size, GL_MAP_READ_BIT);

			int maxIndex = -1;
			int minIndex = 100000000;
			for(int i = 0; i < size / 4; i++)
			{ 
				maxIndex = max(maxIndex, ptr[i]);
				minIndex = min(minIndex, ptr[i]);
			}
			printf("index: (%d,%d)\n", minIndex, maxIndex);
			glUnmapBuffer(GL_ELEMENT_ARRAY_BUFFER);

			if (info->BaseVertex == 0)
			{
				printf("glDrawElements(%d, %d, %d, %p)\n", m, info->FaceVertexCount, indexType, (GLvoid*)offset);
				glDrawElements(m, info->FaceVertexCount, indexType, (GLvoid*)offset);
			}
			else
			{
				printf("glDrawElementsBaseVertex(%d, %d, %d, %p, %d)\n", m, info->FaceVertexCount, indexType, (GLvoid*)offset, info->BaseVertex);
				glDrawElementsBaseVertex(m, info->FaceVertexCount, indexType, (GLvoid*)offset, info->BaseVertex);
			}
		}
	}
	endtrace("a")
}

DllExport(void) hglDrawArraysIndirect(int* isActive, BeginMode* mode, GLint* count, GLint stride, GLuint buffer)
{
	trace("hglDrawArraysIndirect\n");
	auto m = mode->Mode;
	auto v = mode->PatchVertices;
	if (m == GL_PATCHES) glPatchParameteri(GL_PATCH_VERTICES, v);

	if (glMultiDrawArraysIndirect == nullptr)
	{	
		GLint size = 0;
		glBindBuffer(GL_COPY_READ_BUFFER, buffer);
		glGetBufferParameteriv(GL_COPY_READ_BUFFER, GL_BUFFER_SIZE, &size);
		auto indirect = (DrawArraysIndirectCommand*)glMapBufferRange(GL_COPY_READ_BUFFER, 0, size, GL_MAP_READ_BIT);
		auto drawcount = *count;
		GLsizei n;
		for (n = 0; n < drawcount; n++)
		{
			const DrawArraysIndirectCommand  *cmd;
			if (stride != 0)
			{
				cmd = (DrawArraysIndirectCommand*)((char*)indirect + n * stride);
			}
			else
			{
				cmd = (DrawArraysIndirectCommand*)indirect + n;
			}

			glDrawArraysInstancedBaseInstance(m, cmd->First, cmd->Count, cmd->InstanceCount, cmd->BaseInstance);
		}
	
		glUnmapBuffer(GL_COPY_READ_BUFFER);
		glBindBuffer(GL_COPY_READ_BUFFER, 0);
	}
	else
	{
		auto cnt = *count;
		glBindBuffer(GL_DRAW_INDIRECT_BUFFER, buffer);
		glMultiDrawArraysIndirect(m, nullptr, cnt, stride);
		glBindBuffer(GL_DRAW_INDIRECT_BUFFER, 0);
	}
	endtrace("a")
}

DllExport(void) hglDrawElementsIndirect(int* isActive, BeginMode* mode, GLenum indexType, GLint* count, GLint stride, GLuint buffer)
{
	trace("hglDrawElementsIndirect\n");
	auto m = mode->Mode;
	auto v = mode->PatchVertices;
	if (m == GL_PATCHES) glPatchParameteri(GL_PATCH_VERTICES, v);

	if (glMultiDrawElementsIndirect == nullptr)
	{
		GLint size = 0;
		glBindBuffer(GL_COPY_READ_BUFFER, buffer);
		glGetBufferParameteriv(GL_COPY_READ_BUFFER, GL_BUFFER_SIZE, &size);
		auto indirect = (DrawElementsIndirectCommand*)glMapBufferRange(GL_COPY_READ_BUFFER, 0, size, GL_MAP_READ_BIT);
		auto drawcount = *count;

		GLsizei n;
		for (n = 0; n < drawcount; n++)
		{
			const DrawElementsIndirectCommand  *cmd;
			if (stride != 0)
			{
				cmd = (const DrawElementsIndirectCommand  *)((char*)indirect + n * stride);
			}
			else
			{
				cmd = (const DrawElementsIndirectCommand  *)indirect + n;
			}

			glDrawElementsInstancedBaseVertexBaseInstance(
				m,
				cmd->Count,
				indexType,
				(void*) (cmd->FirstIndex * sizeof(int)), // TODO: proper size of indexType
				cmd->InstanceCount,
				cmd->BaseVertex,
				cmd->BaseInstance
			);
		}

		glUnmapBuffer(GL_COPY_READ_BUFFER);
		glBindBuffer(GL_COPY_READ_BUFFER, 0);
	}
	else
	{
		glBindBuffer(GL_DRAW_INDIRECT_BUFFER, buffer);
		glMultiDrawElementsIndirect(m, indexType, nullptr, *count, stride);
		glBindBuffer(GL_DRAW_INDIRECT_BUFFER, 0);
	}
	endtrace("a")
}


DllExport(void) hglSetDepthTest(GLenum* mode)
{
	trace("hglSetDepthTest\n");
	auto m = *mode;
	if (m == 0)
	{
		glDisable(GL_DEPTH_TEST);
	}
	else
	{
		glEnable(GL_DEPTH_TEST);
		glDepthFunc(m);
	}
	endtrace("a")
}

DllExport(void) hglSetCullFace(GLenum* face)
{
	trace("hglSetCullFace\n");
	auto f = *face;
	if (f == 0)
	{
		glDisable(GL_CULL_FACE);
	}
	else
	{
		glEnable(GL_CULL_FACE);
		glCullFace(f);
	}
	endtrace("a")
}

DllExport(void) hglSetPolygonMode(GLenum* mode)
{
	trace("hglSetPolygonMode\n");
	glPolygonMode(GL_FRONT_AND_BACK, *mode);
	endtrace("a")
}

DllExport(void) hglSetBlendMode(BlendMode* mode)
{
	trace("hglSetBlendMode\n");
	if (!mode->Enabled)
	{
		glDisable(GL_BLEND);
	}
	else
	{
		glEnable(GL_BLEND);
		glBlendFuncSeparate(mode->SourceFactor, mode->DestFactor, mode->SourceFactorAlpha, mode->DestFactorAlpha);
		glBlendEquationSeparate(mode->Operation, mode->OperationAlpha);
	}
	endtrace("a")

}

DllExport(void) hglSetStencilMode(StencilMode* mode)
{
	trace("hglSetStencilMode\n");
	if (!mode->Enabled)
	{
		glDisable(GL_STENCIL_TEST);
	}
	else
	{
		glEnable(GL_STENCIL_TEST);

		glStencilFuncSeparate(GL_FRONT, mode->CmpFront, mode->ReferenceFront, mode->MaskFront);
		glStencilOpSeparate(GL_FRONT, mode->OpFrontSF, mode->OpFrontDF, mode->OpFrontPass);

		glStencilFuncSeparate(GL_BACK, mode->CmpBack, mode->ReferenceBack, mode->MaskBack);
		glStencilOpSeparate(GL_BACK, mode->OpBackSF, mode->OpBackDF, mode->OpBackPass);
	}
	endtrace("a")
}





