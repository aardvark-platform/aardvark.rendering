#ifndef __GNUC__
#include "stdafx.h"
#include <vulkan/vulkan_core.h>
#include <vulkan/vulkan_win32.h>
#define VMA_CALL_PRE  __declspec(dllexport)
#define VMA_CALL_POST __cdecl
#endif

#ifdef _DEBUG
#define VMA_DEBUG_LOG_FORMAT(format, ...) do { \
   printf((format), __VA_ARGS__); \
   printf("\n"); \
} while(false)
#endif

#define VMA_IMPLEMENTATION
#define VMA_STATIC_VULKAN_FUNCTIONS 0
#define VMA_DYNAMIC_VULKAN_FUNCTIONS 1
#include "vma/vk_mem_alloc.h"