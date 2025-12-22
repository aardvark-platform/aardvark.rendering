# Aardvark.Rendering AI Reference

Index for AI coding assistants. Read only the doc you need.

## By Task

| Task | Document | Size |
|------|----------|------|
| ISg hierarchy, applicators, composition | SG-CORE.md | ~4 KB |
| Semantic system, FShade, custom applicators | SG-SEMANTICS.md | ~5 KB |
| Scene graph usage patterns, gotchas | SG-PATTERNS.md | ~6 KB |
| IRuntime, resources, render objects, tasks | RENDER-CORE.md | ~6 KB |
| Rendering usage patterns, offscreen, gotchas | RENDER-PATTERNS.md | ~6 KB |
| OpenGL and Vulkan backends | BACKENDS.md | ~8 KB |
| Windowing, application layer, platforms | APPLICATION.md | ~7 KB |
| Native C++ components (GLVM, VKVM) | NATIVE.md | ~6 KB |
| Consumer project patterns | CONSUMER-PATTERNS.md | ~4 KB |
| C# integration | CSHARP-INTEGRATION.md | ~3 KB |
| Creating consumer project docs | TEMPLATE-CONSUMER.md | ~2 KB |

## By Type

### Scene Graph
- `ISg`, `IApplicator`, `IGroup` -> SG-CORE.md
- `Sg.*` (TrafoApplicator, SurfaceApplicator, etc.) -> SG-CORE.md
- `Ag.Scope`, Semantic Rules -> SG-SEMANTICS.md
- FShade `effect { }` composition -> SG-SEMANTICS.md
- Custom applicators -> SG-SEMANTICS.md
- `DefaultSemantic`, usage examples -> SG-PATTERNS.md
- Storage buffers, conditional features -> SG-PATTERNS.md

### Rendering Core
- `IRuntime`, `IBackendSurface`, `IRenderTask` -> RENDER-CORE.md
- `IBuffer`, `ITexture`, `IFramebuffer` -> RENDER-CORE.md
- `RenderObject`, `Surface`, `DrawCalls` -> RENDER-CORE.md
- `BlendState`, `DepthState`, `RasterizerState` -> RENDER-CORE.md
- `IUniformProvider`, `IAttributeProvider` -> RENDER-CORE.md
- Usage examples, compute shaders -> RENDER-PATTERNS.md
- Offscreen rendering, large-scale scenes -> RENDER-PATTERNS.md

### Backends
- `Aardvark.Rendering.GL.Runtime` -> BACKENDS.md
- `Aardvark.Rendering.Vulkan.Runtime` -> BACKENDS.md
- Context management, resource caching -> BACKENDS.md

### Application
- `OpenGlApplication`, `VulkanApplication` -> APPLICATION.md
- `IRenderWindow`, `IRenderControl` -> APPLICATION.md
- GLFW, WPF, WinForms integration -> APPLICATION.md
- OpenVR -> APPLICATION.md

### Native
- GLVM (GL command batching) -> NATIVE.md
- VKVM (Vulkan helpers) -> NATIVE.md
- P/Invoke patterns -> NATIVE.md

### Consumer Integration
- Application initialization patterns -> CONSUMER-PATTERNS.md
- C# adaptive values, scene graph -> CSHARP-INTEGRATION.md
- Consumer AGENTS.md template -> TEMPLATE-CONSUMER.md

## Quick Reference

- **Incremental computation**: Uses `FSharp.Data.Adaptive` (`aval`, `aset`, `alist`)
- **Shader DSL**: FShade (`effect { vertex ...; fragment ... }`)
- **Package manager**: Paket (not NuGet directly)
- **Build**: `.\build.cmd` (Windows) / `./build.sh` (Unix)

## When to Read

| Task | Document |
|------|----------|
| ISg types, applicator nodes, scene composition | SG-CORE.md |
| Attribute grammars, semantic rules, FShade effects | SG-SEMANTICS.md |
| Scene graph examples, storage buffers, gotchas | SG-PATTERNS.md |
| Draw calls, buffers, textures, framebuffers, tasks | RENDER-CORE.md |
| Rendering examples, offscreen, large scenes, gotchas | RENDER-PATTERNS.md |
| Runtime init, context issues, backend bugs | BACKENDS.md |
| Windowing, input, VR, platform issues | APPLICATION.md |
| Native C++ (GLVM/VKVM) | NATIVE.md |
