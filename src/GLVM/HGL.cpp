#ifndef __GNUC__
#include "stdafx.h"
#endif

#include "glvm.h"

typedef struct {
	int FaceVertexCount;
	int InstanceCount;
	int FirstIndex;
	int FirstInstance;
	int BaseVertex;
} DrawCallInfo;

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

DllExport(void) hglDrawArrays(bool* isActive, GLenum* mode, int* count, DrawCallInfo* info)
{
	if (!*isActive) return;

	auto m = *mode;
	auto cnt = *count;
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
}

DllExport(void) hglDrawElements(bool* isActive, GLenum* mode, GLenum indexType, int* count, DrawCallInfo* info)
{
	if (!*isActive) return;

	auto m = *mode;
	auto cnt = *count;
	for (int i = 0; i < cnt; i++, info += 1)
	{
		if (info->InstanceCount != 1 || info->FirstInstance != 0)
		{
			glDrawElementsInstancedBaseVertexBaseInstance(m, info->FaceVertexCount, indexType, (const void*)(int64_t)info->FirstIndex, info->InstanceCount, info->BaseVertex, info->FirstInstance);
		}
		else
		{
			glDrawElementsBaseVertex(m, info->FaceVertexCount, indexType, (const void*)(int64_t)info->FirstIndex, info->BaseVertex);
		}
	}
}

DllExport(void) hglDrawArraysIndirect(bool* isActive, GLenum* mode, GLint* count, GLint stride, GLuint* buffer)
{
	if (glMultiDrawArraysIndirect == nullptr)
	{
		GLint size = 0;
		glBindBuffer(GL_COPY_READ_BUFFER, *buffer);
		glGetBufferParameteriv(GL_COPY_READ_BUFFER, GL_BUFFER_SIZE, &size);
		auto indirect = (DrawArraysIndirectCommand*)glMapBufferRange(GL_COPY_READ_BUFFER, 0, size, GL_MAP_READ_BIT);
		auto drawcount = *count;
		auto m = *mode;

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
		glBindBuffer(GL_DRAW_INDIRECT_BUFFER, *buffer);
		glMultiDrawArraysIndirect(*mode, nullptr, *count, stride);
		glBindBuffer(GL_DRAW_INDIRECT_BUFFER, 0);
	}
}

DllExport(void) hglDrawElementsIndirect(bool* isActive, GLenum* mode, GLenum indexType, GLint* count, GLint stride, GLuint* buffer)
{
	if (glMultiDrawElementsIndirect == nullptr)
	{
		GLint size = 0;
		glBindBuffer(GL_COPY_READ_BUFFER, *buffer);
		glGetBufferParameteriv(GL_COPY_READ_BUFFER, GL_BUFFER_SIZE, &size);
		auto indirect = (DrawElementsIndirectCommand*)glMapBufferRange(GL_COPY_READ_BUFFER, 0, size, GL_MAP_READ_BIT);
		auto drawcount = *count;
		auto m = *mode;

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
		glBindBuffer(GL_DRAW_INDIRECT_BUFFER, *buffer);
		glMultiDrawElementsIndirect(*mode, indexType, nullptr, *count, stride);
		glBindBuffer(GL_DRAW_INDIRECT_BUFFER, 0);
	}
}