# Native Components Reference

> If modifying C++ in `src/GLVM/` or `src/VKVM/`, read "When to Rebuild" first.

## GLVM Overview

GLVM (OpenGL Virtual Machine) is a native C++ library providing instruction-based batching and optimization of OpenGL state changes.

**Purpose:**
- Eliminates redundant GL state changes via runtime tracking
- Cross-platform GL extension loading (wglGetProcAddress, glXGetProcAddressARB, mach-o dyld)
- Fragment-based instruction encoding for command batching
- Per-context VAO management with automatic lifecycle tracking

**Instruction System:**

| Component | Description | Location |
|-----------|-------------|----------|
| Fragment | Linked list of instruction blocks (vector<vector<Instruction>>) | `src/GLVM/glvm.h:223-226` |
| Instruction | Opcode + 6 intptr_t arguments | `src/GLVM/glvm.h:212-220` |
| InstructionCode | Enum of 57+ GL commands (BindVertexArray, DrawElements, etc.) | `src/GLVM/glvm.h:127-202` |

**Execution Modes:**

```cpp
typedef enum {
    NoOptimization           = 0x00000,  // Execute all instructions
    RuntimeRedundancyChecks  = 0x00001,  // Skip redundant state changes
    RuntimeStateSorting      = 0x00002   // Reserved
} VMMode;
```

**State Tracking** (`src/GLVM/State.h:132-209`):
- Caches GL state: VAO, program, textures, samplers, buffers, blend modes, depth/stencil, etc.
- `ShouldSet*` methods compare incoming vs cached state
- Returns `false` + increments `removedInstructions` on match
- Per-fragment state reset after execution

**High-Level Commands** (prefix `H`):
- `HDrawArrays`: Batches array of DrawCallInfo, handles instancing/base instance
- `HBindVertexAttributes`: Lazy VAO creation per GL context, manages buffer/value bindings
- `HSetBlendModes`: Per-attachment blend state with glEnablei/glBlendFuncSeparatei

**Fragment API Example:**

```fsharp
open Aardvark.Rendering.GL

// Initialize GL function pointers (requires active GL context)
GLVM.vmInit()

// Create fragment
let frag = GLVM.vmCreate()
let block = GLVM.vmNewBlock(frag)

// Encode instructions
GLVM.vmAppend1(frag, block, int InstructionCode.BindVertexArray, nativeint vaoHandle)
GLVM.vmAppend1(frag, block, int InstructionCode.BindProgram, nativeint programId)

// Execute with redundancy checks
let mutable stats = Unchecked.defaultof<VMStats>
GLVM.vmRun(frag, VMMode.RuntimeRedundancyChecks, &stats)

printfn "Removed %d redundant calls" stats.RemovedInstructions

// Cleanup
GLVM.vmDelete(frag)
```

---

## VKVM Overview

VKVM (Vulkan Virtual Machine) provides helper functions for Vulkan resource binding, draw call abstraction, and VMA memory management.

**Purpose:**
- Unified API for direct/indirect draw calls (indexed/non-indexed variants)
- Descriptor set/vertex/index buffer binding helpers
- Command encoding system for serializing Vulkan commands
- VMA (Vulkan Memory Allocator) integration

**Resource Binding Helpers:**

| Function | Signature | Description |
|----------|-----------|-------------|
| `vmBindDescriptorSets` | `(VkCommandBuffer, DescriptorSetBinding*)` | Binds descriptor set array |
| `vmBindIndexBuffer` | `(VkCommandBuffer, IndexBufferBinding*)` | Binds index buffer + offset + type |
| `vmBindVertexBuffers` | `(VkCommandBuffer, VertexBufferBinding*)` | Binds vertex buffer arrays + offsets |
| `vmDraw` | `(VkCommandBuffer, RuntimeStats*, int*, DrawCall*)` | Unified draw (direct/indirect, indexed/non-indexed) |

**DrawCall Union** (`src/VKVM/vkvm.h:27-39`):

```cpp
typedef struct {
    uint8_t IsIndirect;
    uint8_t IsIndexed;
    int     Count;
    union {
        DrawCallInfo*  DrawCalls;         // Direct mode
        struct {
            VkBuffer Handle;
            uint64_t Offset;
            int      Stride;
        } DrawCallBuffer;                 // Indirect mode
    };
} DrawCall;
```

**F# Union Layout** (`src/Aardvark.Rendering.Vulkan.Wrapper/VKVM.fs`):

```fsharp
[<StructLayout(LayoutKind.Explicit)>]
type DrawCall =
    struct
        [<FieldOffset(0)>] val mutable IsIndirect     : uint8
        [<FieldOffset(1)>] val mutable IsIndexed      : uint8
        [<FieldOffset(4)>] val mutable Count          : int
        [<FieldOffset(8)>] val mutable DrawCalls      : nativeptr<DrawCallInfo>
        [<FieldOffset(8)>] val mutable DrawCallBuffer : DrawCallBuffer

        static member Direct(calls: DrawCallInfo[], indexed: bool) =
            let mutable dc = Unchecked.defaultof<DrawCall>
            dc.IsIndirect <- 0uy
            dc.IsIndexed  <- if indexed then 1uy else 0uy
            dc.Count      <- calls.Length
            dc.DrawCalls  <- NativePtr.alloc dc.Count
            for i = 0 to dc.Count - 1 do dc.DrawCalls.[i] <- calls.[i]
            dc

        member x.Dispose() =
            if x.IsIndirect = 0uy && not <| NativePtr.isNullPtr x.DrawCalls then
                NativePtr.free x.DrawCalls
```

**Command Encoding System** (`src/VKVM/commands.h`):
- 44+ Vulkan command types: CmdBindPipeline, CmdDraw, CmdDispatch, CmdCopyBuffer, CmdPipelineBarrier, etc.
- Commands stored as length-prefixed structs: `{ uint32_t Length; CommandType OpCode; ...args }`
- `CommandFragment` linked list for chaining
- `vmRun(VkCommandBuffer, CommandFragment*)` executes fragment

**VMA Integration** (`src/VKVM/vma.cpp`):
- Single-file VMA implementation (`#define VMA_IMPLEMENTATION`)
- Exports allocator functions as DLL exports (Windows: `__declspec(dllexport)`)
- Debug logging enabled only in `_DEBUG` builds

---

## Build Instructions

### Prerequisites

| Platform | GLVM Requirements | VKVM Requirements |
|----------|-------------------|-------------------|
| Windows  | CMake 3.15+, Visual Studio 2019+ | + Vulkan SDK |
| Linux    | CMake 3.15+, GCC/G++, `libgl-dev` | + Vulkan SDK |
| macOS    | CMake 3.15+, Xcode CLI tools | + Vulkan SDK |

### Build Commands

| Platform | Component | Command | Output |
|----------|-----------|---------|--------|
| Windows  | GLVM | `cd src\GLVM && build.cmd` | `lib/Native/Aardvark.Rendering.GL/windows/AMD64/glvm.dll` |
| Windows  | VKVM | `cd src\VKVM && build.cmd` | `lib/Native/Aardvark.Rendering.Vulkan/windows/AMD64/vkvm.dll` |
| Linux    | GLVM | `cd src/GLVM && ./build.sh` | `lib/Native/Aardvark.Rendering.GL/linux/AMD64/libglvm.so` |
| Linux    | VKVM | `cd src/VKVM && ./build.sh` | `lib/Native/Aardvark.Rendering.Vulkan/linux/AMD64/libvkvm.so` |
| macOS x64 | GLVM | `cd src/GLVM && ./build.sh` | `lib/Native/Aardvark.Rendering.GL/mac/AMD64/libglvm.dylib` |
| macOS ARM64 | GLVM | `cd src/GLVM && ./build.sh` | `lib/Native/Aardvark.Rendering.GL/mac/ARM64/libglvm.dylib` |
| macOS x64 | VKVM | `cd src/VKVM && ./build.sh` | `lib/Native/Aardvark.Rendering.Vulkan/mac/AMD64/libvkvm.dylib` |
| macOS ARM64 | VKVM | `cd src/VKVM && ./build.sh` | `lib/Native/Aardvark.Rendering.Vulkan/mac/ARM64/libvkvm.dylib` |

**Build Process:**
1. Delete `build/` directory
2. Configure CMake Release build
3. Build with platform toolchain (MSVC, GCC, Clang)
4. Install to `lib/Native/{component}/{os}/{arch}/`

**Architecture Detection** (`CMakeLists.txt`):
```cmake
execute_process(COMMAND uname -m OUTPUT_VARIABLE ARCH)
string(REGEX REPLACE "\n$" "" ARCH "${ARCH}")
string(REGEX REPLACE "x86_64" "AMD64" ARCH "${ARCH}")
```

### CI Workflows

| Workflow | File | Trigger | Platforms | Output |
|----------|------|---------|-----------|--------|
| GLVM | `.github/workflows/glvm.yml` | `workflow_dispatch` | windows-2022, ubuntu-22.04, macos-15-intel, macos-14 | PR: "[GLVM] Update native libraries" |
| VKVM | `.github/workflows/vkvm.yml` | `workflow_dispatch` | windows-2022, ubuntu-22.04, macos-15-intel, macos-14 | PR: "[VKVM] Update native libraries" |

---

## P/Invoke Integration

### GLVM F# Wrapper

**Basic Pattern** (`src/Aardvark.Rendering.GL/Core/GLVM.fs:22-90`):

```fsharp
module GLVM =
    open System.Runtime.InteropServices
    open System.Runtime.CompilerServices
    open System.Security

    [<Literal>]
    let lib = "glvm"

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl); SuppressUnmanagedCodeSecurity>]
    extern void vmInit()

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl); SuppressUnmanagedCodeSecurity>]
    extern FragmentPtr vmCreate()

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl); SuppressUnmanagedCodeSecurity>]
    extern void vmDelete(FragmentPtr frag)

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl); SuppressUnmanagedCodeSecurity>]
    extern void vmRun(FragmentPtr frag, VMMode mode, VMStats& stats)
```

**Key Patterns:**
- `CallingConvention.Cdecl`: Matches C++ `extern "C"` (default on Unix, explicit `__cdecl` on Windows)
- `SuppressUnmanagedCodeSecurity`: Skips security stack walks (use only for trusted code)
- `FragmentPtr = nativeint`: Platform-sized pointer (32-bit or 64-bit)
- `VMStats& stats`: Pass-by-reference for output parameters

**Library Loading:**
- Windows: `glvm.dll` → `lib/Native/Aardvark.Rendering.GL/windows/AMD64/glvm.dll`
- Linux: `libglvm.so` → `lib/Native/Aardvark.Rendering.GL/linux/AMD64/libglvm.so`
- macOS: `libglvm.dylib` → `lib/Native/Aardvark.Rendering.GL/mac/{AMD64|ARM64}/libglvm.dylib`

### VKVM F# Wrapper

**Struct Layout - Sequential** (simple structs):

```fsharp
[<StructLayout(LayoutKind.Sequential)>]
type VertexBufferBinding =
    struct
        val mutable public FirstBinding : int
        val mutable public BindingCount : int
        val mutable public Buffers      : nativeptr<VkBuffer>
        val mutable public Offsets      : nativeptr<uint64>

        new (count: int, first: int) =
            {
                FirstBinding = first
                BindingCount = count
                Buffers      = NativePtr.alloc count
                Offsets      = NativePtr.alloc count
            }

        member this.Dispose() =
            if not <| NativePtr.isNullPtr this.Buffers then
                NativePtr.free this.Buffers
            if not <| NativePtr.isNullPtr this.Offsets then
                NativePtr.free this.Offsets
```

**Struct Layout - Explicit** (C-style unions):

```fsharp
[<StructLayout(LayoutKind.Explicit)>]
type DrawCall =
    struct
        [<FieldOffset(0)>] val mutable IsIndirect     : uint8
        [<FieldOffset(1)>] val mutable IsIndexed      : uint8
        [<FieldOffset(4)>] val mutable Count          : int
        [<FieldOffset(8)>] val mutable DrawCalls      : nativeptr<DrawCallInfo>
        [<FieldOffset(8)>] val mutable DrawCallBuffer : DrawCallBuffer  // Union overlay
```

**Manual Memory Management:**

```fsharp
open Microsoft.FSharp.NativeInterop

#nowarn "9"  // Suppress nativeptr warnings

let mutable binding = VertexBufferBinding(count = 2, first = 0)
NativePtr.set binding.Buffers 0 vertexBuffer0
NativePtr.set binding.Buffers 1 vertexBuffer1
NativePtr.set binding.Offsets 0 0UL
NativePtr.set binding.Offsets 1 1024UL

// Use binding...

binding.Dispose()  // Free native memory
```

---

## When to Rebuild

### GLVM Rebuild Triggers

| Change | Reason |
|--------|--------|
| Add instruction code | Update `InstructionCode` enum, `runInstruction` switch, `runRedundancyChecks` switch |
| Add state tracking | Extend `State` class with new `ShouldSet*` method |
| Modify VAO management | Update `hglBindVertexAttributes`, `hglCleanup`, `hglDeleteVAO` |
| Support new GL extension | Add dynamic loading in `vmInit`, fallback logic |

### VKVM Rebuild Triggers

| Change | Reason |
|--------|--------|
| Add Vulkan command | Add to `CommandType` enum, define command struct, update `enqueueCommand` switch |
| Update VMA version | Replace `vma/vk_mem_alloc.h` |
| Modify DrawCall structure | Update C++ struct + F# struct layout |
| Vulkan API update | Recompile against new Vulkan headers |

### Post-Rebuild Process

1. **Local Test:** Verify build on all target platforms (Windows, Linux, macOS x64/ARM64)
2. **CI Trigger:** Run `.github/workflows/{glvm|vkvm}.yml` via `workflow_dispatch`
3. **Review PR:** CI creates PR with updated binaries in `lib/Native/`
4. **Merge:** Update repository with new native libraries

### Debugging Native Crashes

| Platform | Tool | Command |
|----------|------|---------|
| Windows | WinDbg/VS Debugger | PDBs generated in Debug builds (`glvm.pdb`, `vkvm.pdb`) |
| Linux | GDB | `gdb --args dotnet MyApp.dll` |
| macOS | LLDB | `lldb -- dotnet MyApp.dll` |
| Cross-platform | printf | Enable trace macros in `glvm.cpp:52-53` (`#define trace(a) { printf(a); }`) |

---

## Gotchas

| # | Issue | Fix |
|---|-------|-----|
| 1 | GL Context Requirement | `vmInit()` requires active GL context; call after `glfwMakeContextCurrent` |
| 2 | Multi-Context VAO | VAOs are context-specific; call `hglCleanup(ctx)` on context destroy |
| 3 | State Cache Leaks | External GL calls bypass GLVM cache; use `VMMode.NoOptimization` or separate fragments |
| 4 | Missing Extensions | Fallback to slow loops (10-100x slower); check `glMultiDrawArraysIndirect != nullptr` |
| 5 | Vulkan SDK Version | Pin version in CMakeLists.txt; CI uses latest, local may differ |
| 6 | Union Layout | `sizeof(DrawCall)` must be 16 bytes; verify with static_assert |
| 7 | VMA Debug Logging | Disabled in Release; enable `VMA_DEBUG_LOG_FORMAT` or use validation layers |
| 8 | CMake Arch Detection | May fail on RISC-V/PowerPC; manually set `cmake -DARCH=AMD64` |
| 9 | Static CRT (Windows) | Mixing static/dynamic CRT causes heap corruption; use `MultiThreadedDLL` |
| 10 | P/Invoke Convention | Always specify `CallingConvention.Cdecl`; default `StdCall` causes stack corruption |

---

## See Also

- **BACKENDS.md** - OpenGL and Vulkan backend architecture (dependency on GLVM/VKVM)
- **Build workflows:** `.github/workflows/glvm.yml`, `.github/workflows/vkvm.yml`
- **Source code:**
  - `src/GLVM/` - GLVM C++ implementation
  - `src/VKVM/` - VKVM C++ implementation
  - `src/Aardvark.Rendering.GL/Core/GLVM.fs` - F# wrapper
  - `src/Aardvark.Rendering.Vulkan.Wrapper/VKVM.fs` - F# wrapper
