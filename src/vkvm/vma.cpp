#ifndef __GNUC__
#include "stdafx.h"
#define VMA_CALL_PRE  __declspec(dllexport)
#define VMA_CALL_POST __cdecl
#endif

#define VMA_IMPLEMENTATION
#define VMA_VULKAN_VERSION 1003000
#include <vma/vk_mem_alloc.h>