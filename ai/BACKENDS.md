# OpenGL and Vulkan Backends

Reference for backend-specific implementations, context management, and runtime selection.

---

## 1. Backend Architecture

Both OpenGL (GL) and Vulkan backends implement `IRuntime` with different strategies:

| Aspect | OpenGL | Vulkan |
|--------|--------|--------|
| **State Model** | Stateful (glState tracks current bindings) | Stateless (commands recorded to buffer) |
| **Context** | `ContextHandle` (thread-local) | `VkDevice` (thread-safe) |
| **Concurrency** | Multi-context per GPU, reader-writer locks | Single device, lock-free command buffer pools |
| **Pipeline Caching** | Per-(signature, effect, topology) | Per-(signature, effect, topology) + VkPipeline caching |
| **Shader Format** | GLSL (runtime compilation via FShade) | SPIR-V (runtime compilation via FShade) |
| **Memory Model** | OpenGL memory implicit, manual management | VMA (Vulkan Memory Allocator) |
| **Resource Sharing** | Shared across contexts in pool | Shared via device handles |
| **Debug Support** | GL debug output callbacks | VkDebugUtilsMessenger |

**Common Interface:** Both implement `IRuntime` + `IFramebufferRuntime` + `ITextureRuntime` + `IBufferRuntime` + `IComputeRuntime`.

---

## 2. OpenGL Backend

### Context Creation and Initialization

```fsharp
open Aardvark.Rendering
open Aardvark.Rendering.GL

// Step 1: Create an OS-specific context (GLFW, WPF, WinForms)
let ctx = new Context(...)

// Step 2: Create runtime with optional debug level
let runtime = new Runtime(DebugLevel.Normal)

// Step 3: Initialize runtime (binds context, loads GL extensions)
runtime.Initialize(ctx)

// Now runtime is ready for resource creation
let buffer = runtime.CreateBuffer(1024UL, BufferUsage.All, BufferStorage.Device)
```

**DebugLevel enum:**
- `None`: No debugging, max performance
- `Minimal`: Errors/warnings logged, no exceptions
- `Normal`: Detailed info, exceptions on GPU errors
- `Full`: Complete debug info, KHR_debug callbacks enabled, 20-30% perf hit

### GL.Runtime Class Structure

**Key members:**
- `Context : ContextHandle` - currently bound context
- `ContextLock : IDisposable` - acquires context for thread safety
- `ResourceContexts : ContextHandle[]` - background contexts for resource creation
- `DefaultFramebuffer : IFramebuffer` - main window framebuffer
- `MemoryInfo : MemoryUsage` - texture/buffer allocation tracking

### Resource Management (Multi-Context)

GL uses **context pooling** for resource creation without blocking render thread:

```fsharp
// Main render thread
use _ = runtime.ContextLock
task.Run(token, renderToken, output)

// Resource creation (background thread)
// Automatically uses context from pool
let tex = runtime.CreateTexture(...)
let buf = runtime.CreateBuffer(...)
```

**Multi-context rules:**
- Resources created in any context become shared via `GL_ARB_shared_contexts`
- VAOs are **NOT** shared; each context builds its own
- Textures, buffers, samplers, programs **ARE** shared
- GL state (`glBindBuffer`, `glBindTexture`, etc.) is per-context

### Shader Compilation Pipeline

```fsharp
// FShade effect (F# DSL)
let myEffect = effect {
    vertex fun v -> { v with pos = uniform.ProjectionMatrix * v.pos }
    fragment fun _ -> C4f.White
}

// Compile for specific framebuffer and topology
let surface = runtime.PrepareEffect(fboSig, myEffect, IndexedGeometryMode.TriangleList)
// ↓ Internally:
// 1. Translate F# quotations to GLSL
// 2. Compile GLSL via GL shader compiler
// 3. Link into program
// 4. Cache by (signature, effect, topology)
```

### GLVM Integration

GLVM is a **native C++ instruction-based command buffer** that optimizes GL state changes:

```fsharp
// P/Invoke wrapper (Aardvark.Rendering.GL.Core.GLVM)
[<DllImport("glvm")>]
extern void vmInit()  // MUST call after GL context is current

// GLVM reduces redundant state changes
// Example: drawing 1000 objects with same shader
// Without GLVM: 1000 × glUseProgram calls
// With GLVM: 1 × glUseProgram + 1000 draw calls (state cached)
```

**GLVM caveats:**
- Must be initialized AFTER GL context creation (function pointers loaded)
- VAOs created by GLVM are context-specific; switching contexts invalidates cache
- Redundancy checks may skip necessary calls if external code modifies GL state

### Debug Configuration

```fsharp
let runtime = new Runtime(DebugLevel.Full)
// Enables:
// - GL_KHR_debug callbacks for errors/warnings
// - Slower validation in buffer/texture operations
// - Named resources for debug tools (RenderDoc, etc.)
```

Access debug output:

```fsharp
// Via callback in Core/DebugOutput.fs
// Errors logged to console/log file
// Use RenderDoc or APITrace to capture GL calls
```

---

## 3. Vulkan Backend

### Device Creation and Initialization

```fsharp
open Aardvark.Rendering.Vulkan

// Step 1: Create or provide VkInstance and VkPhysicalDevice
let instance = VkInstance(...)
let physicalDevice = instance.EnumeratePhysicalDevices() |> Array.head

// Step 2: Create VkDevice
let device = Device.Create(physicalDevice, ...)

// Step 3: Create runtime
let runtime = new Runtime(device)
// Runtime initializes:
// - VMA allocator
// - Command pool(s)
// - Descriptor pool(s)
// - Validation layers (if debug)
```

### Vulkan.Runtime Class Structure

**Key members:**
- `Device : VkDevice` - logical device
- `PhysicalDevice : VkPhysicalDevice` - GPU properties
- `GraphicsQueue : VkQueue` - main render queue
- `Allocator : VmaAllocator` - Vulkan Memory Allocator
- `DescriptorSetAllocator : ...` - reusable descriptor sets
- `PipelineCache : VkPipelineCache` - compiled pipeline storage

### Vulkan Command Encoding

Vulkan records commands into `VkCommandBuffer` via native VKVM library:

```fsharp
// Render task internally records:
// vkCmdBindPipeline
// vkCmdBindDescriptorSets
// vkCmdBindVertexBuffers / vkCmdBindIndexBuffer
// vkCmdDrawIndexed (or vkCmdDraw)
// vkCmdPipelineBarrier (implicit resource barriers)
```

**Advantages over GL:**
- Commands recorded once, replayed many times (frame N and N+1 can reuse same buffer)
- Multi-threading: record commands on multiple threads in parallel
- Explicit synchronization: no implicit state tracking

### VKVM Integration

VKVM is a **Vulkan helper library** (P/Invoke via `Aardvark.Rendering.Vulkan.Wrapper`):

```fsharp
// Low-level resource binding
VKVM.vmBindDescriptorSets(...)
VKVM.vmBindIndexBuffer(...)
VKVM.vmBindVertexBuffers(...)
VKVM.vmDraw(...)  // unified interface for direct/indirect, indexed/non-indexed

// VMA integration
VKVM.vmaCreateBuffer(...)
VKVM.vmaMappedMemory(...)
```

**Key struct: DrawCall (union)**

```fsharp
[<StructLayout(LayoutKind.Explicit)>]
type DrawCall =
    struct
        [<FieldOffset(0)>] val mutable IsIndirect : uint8
        [<FieldOffset(1)>] val mutable IsIndexed  : uint8
        // Direct mode: array of DrawCallInfo
        [<FieldOffset(8)>] val mutable DrawCalls  : nativeptr<DrawCallInfo>
        // Indirect mode: buffer + offset
        [<FieldOffset(8)>] val mutable DrawCallBuffer : DrawCallBuffer
    end
```

### Shader Compilation to SPIR-V

```fsharp
let myEffect = effect {
    vertex fun v -> { v with pos = uniform.ProjectionMatrix * v.pos }
    fragment fun _ -> C4f.White
}

let surface = runtime.PrepareEffect(fboSig, myEffect, mode)
// ↓ Internally:
// 1. Translate F# quotations to GLSL
// 2. Compile GLSL → SPIR-V via glslang
// 3. Reflect SPIR-V for descriptor sets
// 4. Create VkShaderModule
// 5. Cache pipeline state
```

### Pipeline Caching

Vulkan caches `VkPipeline` objects (expensive to create):

```fsharp
// Pipeline state: (renderPass, layout, shaders, viewport, blend, rasterizer, ...)
// Identical state reuses cached pipeline
// VKVM + VkPipelineCache persist between frames
```

### Debug Configuration

```fsharp
let device = Device.Create(physicalDevice, enableValidation = true)
// Enables:
// - VK_LAYER_KHRONOS_validation layer
// - VkDebugUtilsMessenger callbacks
// - GPU-assisted validation (if available)
// - Warnings on incorrect usage
```

---

## 4. Choosing a Backend

**For most new projects, either backend works.** Both implement `IRuntime` and are interchangeable. When no specific constraints apply, Vulkan is slightly preferred.

### Decision Factors

| Constraint | Backend | Reason |
|------------|---------|--------|
| Raytracing (RTX) | Vulkan | GL lacks raytracing API |
| Legacy/older hardware | GL | Better driver compatibility |
| macOS + geometry shaders | GL | MoltenVK lacks geometry shader support |
| macOS + compute shaders | Vulkan | GL on macOS lacks compute |
| Multi-threaded rendering | Vulkan | GL context is thread-bound |
| Debugging | GL | Better error messages |

### macOS Feature Trade-off

| Feature | GL | Vulkan (MoltenVK) |
|---------|:--:|:-----------------:|
| Geometry Shaders | ✓ | ✗ |
| Compute Shaders | ✗ | ✓ |

### Feature Comparison

| Feature | GL | Vulkan |
|---------|----|----|
| Raytracing | ✗ | ✓ (VK_KHR_ray_tracing) |
| Sparse Textures | ✗ | ✓ (VK_EXT_sparse_binding) |
| Conservative Rasterization | ✓ (NV ext) | ✓ |
| Multi-queue | ✗ | ✓ |
| Async Compute | ✗ | ✓ |
| Bindless Rendering | ✗ | ✓ (VK_EXT_descriptor_indexing) |
| Indirect Rendering | ✓ | ✓ |

---

## 5. Context and Device Management

### Thread Safety

**OpenGL:**
```fsharp
// GL is stateful and thread-bound
use _ = runtime.ContextLock  // Acquires context on current thread
let data = runtime.Download(buffer, offset, dst, size)
// Without lock: may crash or corrupt state
```

**Vulkan:**
```fsharp
// VkDevice is thread-safe; no lock needed for resource creation
let buffer = runtime.CreateBuffer(...)
let texture = runtime.CreateTexture(...)
// Safe from multiple threads
// Command recording may need per-thread pools
```

### Resource Sharing Between Contexts

**GL (multi-context):**
```fsharp
let ctx1 = new Context(...)
let ctx2 = new Context(..., shareContext = Some ctx1)

let runtime1 = new Runtime(DebugLevel.Normal)
runtime1.Initialize(ctx1)

let runtime2 = new Runtime(DebugLevel.Normal)
runtime2.Initialize(ctx2)

// ctx1 and ctx2 share buffers, textures, programs
// NOT VAOs (must rebuild per-context)
let buf = runtime1.CreateBuffer(...)
use _ = runtime2.ContextLock
runtime2.Upload(buf, data, 0UL, size)  // Same buffer, different context
```

**Vulkan (single device):**
```fsharp
let device = Device.Create(...)
let runtime = new Runtime(device)

// All resources exist in single device; no sharing concept
let buf1 = runtime.CreateBuffer(...)
let buf2 = runtime.CreateBuffer(...)
// buf1 and buf2 coexist in device memory
```

---

## 6. Resource Caching

### Shader/Pipeline Caching (Both Backends)

Compiled shaders and pipelines cached by key:

```fsharp
type CacheKey = {
    FramebufferSignature : IFramebufferSignature
    Effect : Effect
    Topology : IndexedGeometryMode
}

// First call: compiles shader, caches
let surface1 = runtime.PrepareEffect(sig, effect, mode)

// Second call: reuses cached shader
let surface2 = runtime.PrepareEffect(sig, effect, mode)

// Different topology: new compilation
let surface3 = runtime.PrepareEffect(sig, effect, mode2)
```

**GL caching location:** `src/Aardvark.Rendering.GL/Management/ResourceCache.fs`

**Vulkan caching location:** `src/Aardvark.Rendering.Vulkan/Management/ResourceManager.fs`

### VAO Caching (GL Only)

GLVM caches Vertex Array Objects per-context:

```fsharp
// First render: creates VAO via hglBindVertexAttributes
task.Run(token, renderToken, output)

// Second frame (same context): reuses VAO
task.Run(token, renderToken, output)

// Context switched: VAO invalidated, regenerates
```

### Descriptor Set Caching (Vulkan Only)

Reusable descriptor sets pool:

```fsharp
// Resources with same layout reuse descriptor sets
// Reduces VkAllocateDescriptorSets calls
```

---

## 7. Debug Configuration

### DebugLevel (Both Backends)

```fsharp
type DebugLevel =
    | None       // No checks, max perf
    | Minimal    // Errors logged
    | Normal     // Info + exceptions on GPU errors
    | Full       // Complete debugging, callbacks enabled
```

**GL specific:**
```fsharp
let runtime = new Runtime(DebugLevel.Full)
// Enables GL_KHR_debug callbacks
// Named objects for RenderDoc
// Validation of buffer ranges, texture sizes, etc.
```

**Vulkan specific:**
```fsharp
let device = Device.Create(physicalDevice, enableValidation = true)
// Enables VK_LAYER_KHRONOS_validation
// VkDebugUtilsMessenger callbacks
// GPU-assisted validation (if Nvidia/AMD)
```

### RenderDoc Integration

Both backends output debug names:

```fsharp
buffer.Name <- "VertexBuffer_Mesh_A"
texture.Name <- "Diffuse_Texture_1024x1024"

// In RenderDoc: F12 to capture frame
// Inspect resources by name
```

---

## 8. Usage Patterns

### Pattern 1: Creating a GL Renderer

```fsharp
open Aardvark.Rendering.GL
open Aardvark.Application

// Create windowed application (handles context creation)
let app = OpenGlApplication()

// Runtime auto-initialized by application
let runtime = app.Runtime

// Create resources
let fboSig = runtime.CreateFramebufferSignature(
    [DefaultSemantic.Colors, TextureFormat.Rgba8],
    TextureFormat.Depth24Stencil8,
    1
)

let effect = effect {
    vertex fun v ->
        { v with pos = uniform.ModelViewProj * v.pos }
    fragment fun v ->
        v.color
}

let ro = RenderObject()
ro.Surface <- Surface.Effect effect
ro.VertexAttributes <- AttributeProvider.ofMap <| Map.ofList [
    DefaultSemantic.Positions, positionBuffer
]
ro.Uniforms <- UniformProvider.ofMap <| Map.ofList [
    Symbol.Create "ModelViewProj", AVal.constant m44f
]

let task = runtime.CompileRender(fboSig, cset [ro])
task.Update(AdaptiveToken.Top, RenderToken.Empty)
task.Run(AdaptiveToken.Top, RenderToken.Empty, OutputDescription.ofFramebuffer fbo)
```

### Pattern 2: Creating a Vulkan Renderer

```fsharp
open Aardvark.Rendering.Vulkan

// Manual device creation
let instance = VkInstance(...)
let physicalDevice = instance.EnumeratePhysicalDevices() |> Array.head
let device = Device.Create(physicalDevice)
let runtime = new Runtime(device)

// Rest same as GL (IRuntime is backend-agnostic)
let fboSig = runtime.CreateFramebufferSignature(...)
let effect = effect { ... }
let ro = RenderObject()
// ...
```

### Pattern 3: Switching Between Backends at Runtime

```fsharp
type BackendType = GL | Vulkan

let createRuntime (backend: BackendType) =
    match backend with
    | GL ->
        let app = OpenGlApplication()
        app.Runtime :> IRuntime
    | Vulkan ->
        let instance = VkInstance(...)
        let device = Device.Create(...)
        new Runtime(device) :> IRuntime

// Code using `runtime` works with both
let runtime = createRuntime GL
let buffer = runtime.CreateBuffer(size, usage, storage)
```

---

> Dual runtime is advanced; most applications use a single backend.

## 9. Dual Runtime Management

Run OpenGL and Vulkan simultaneously for hybrid rendering:

```fsharp
// Create both runtimes
let glApp = new OpenGlApplication()
let glRuntime = glApp.Runtime

let vkApp = new HeadlessVulkanApplication()
let vkRuntime = vkApp.Runtime

// Check Vulkan capabilities
if vkRuntime.SupportsRaytracing && vkRuntime.MaxRayRecursionDepth >= 30 then
    // Use Vulkan for raytracing
    let raytracingTask = createRaytracingTask vkRuntime scene
    // Use GL for rasterization
    let rasterTask = createRasterTask glRuntime scene
    // Combine outputs...
else
    vkApp.Dispose()  // Fall back to GL-only
```

Use cases:
- Vulkan raytracing + GL rasterization
- Feature detection and graceful fallback
- Resource sharing between backends (advanced)

---

## 10. Reversed Depth Configuration

Improves depth precision for large scenes:

```fsharp
// Configure before runtime creation
RuntimeConfig.DepthRange <- DepthRange.ZeroToOne

// Create runtime
let app = new OpenGlApplication()

// Use reversed projection matrix
let reversedProj =
    Frustum.perspective fov aspect 0.1 10000.0
    |> Frustum.projTrafoReversed  // Near=1, Far=0

// Depth test must be Greater (not Less)
scene
|> Sg.depthTest (AVal.constant DepthTest.Greater)
```

Benefits:
- Better precision at distance (exponential distribution)
- Reduces z-fighting in large scenes
- Required for planetary/architectural scales

Shader implications:
- Depth comparison reversed
- `gl_FragCoord.z` range is [1, 0] not [0, 1]

---

## 11. RuntimeConfig Reference

Configure before creating any runtime:

| Setting | Default | Purpose |
|---------|---------|---------|
| `DepthRange` | `MinusOneToOne` | Depth buffer range (use `ZeroToOne` for reversed) |
| `NumberOfResourceContexts` | 1 | Parallel resource creation contexts |
| `AllowConcurrentResourceAccess` | false | Thread-safe resource access |
| `SyncUploadsAndFrames` | true | Sync texture uploads with frames |
| `SuppressSparseBuffers` | false | Disable sparse buffer extensions |
| `UseNewRenderTask` | false | New render task implementation |
| `PreferHostSideTextureCompression` | true | CPU-side texture compression |

```fsharp
// Example: High-performance configuration
RuntimeConfig.DepthRange <- DepthRange.ZeroToOne
RuntimeConfig.NumberOfResourceContexts <- 2
RuntimeConfig.AllowConcurrentResourceAccess <- true
RuntimeConfig.SyncUploadsAndFrames <- false
RuntimeConfig.SuppressSparseBuffers <- true
```

---

## 12. Gotchas

| # | Issue | Fix |
|---|-------|-----|
| 1 | Context Binding (GL) | Call `runtime.ContextLock` before GL operations |
| 2 | VAO Invalidation (GL) | VAOs per-context; call `hglCleanup` on context destroy |
| 3 | GLVM Init Order (GL) | `vmInit()` after GL context is current, not before |
| 4 | Vulkan Pipeline Overhead | Call `PrepareEffect` at init, not in render loop |
| 5 | Depth Range Mismatch | GL: [-1,1]→[0,1]; Vulkan: [0,1] directly; mixing causes z-fighting |
| 6 | SPIR-V Reflection (Vulkan) | Missing uniforms cause silent binding failures |
| 7 | Descriptor Set Reuse (Vulkan) | Allocate per-instance if resources differ per frame |
| 8 | GL State Tracking Gaps | Reset state if mixing manual GL calls with GLVM |
| 9 | Missing Validation (Vulkan) | Enable `VK_LAYER_KHRONOS_validation` in debug builds |
| 10 | Shader Compile Timing | Lazy compile on first use; precompile for consistent frame times |
| 11 | Dual Runtime Disposal | Dispose unused runtime immediately to free GPU memory |
| 12 | RuntimeConfig Timing | Set before creating runtime; frozen after constructor |
| 13 | Reversed Depth | Use `DepthTest.Greater` not `Less` when reversed depth enabled |

---

## 13. See Also

- **RENDERING.md** — Core `IRuntime`, `IBuffer`, `ITexture`, `RenderObject` interfaces
- **NATIVE.md** — GLVM/VKVM internals, P/Invoke patterns, native build process
- **APPLICATION.md** — Windowed app integration, context creation, GLFW/WPF/WinForms
- **SCENEGRAPH.md** — Scene composition with Sg.*, effects in AST form
