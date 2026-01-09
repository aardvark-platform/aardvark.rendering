# AI Agent Reference

Primary entry point for AI agents working with Aardvark.Rendering.

## Commands

| Task | Command | Notes |
|------|---------|-------|
| Restore | `dotnet tool restore && dotnet paket restore` | Restores tools + packages |
| Build (Windows) | `.\build.cmd` | Builds src/Aardvark.Rendering.sln |
| Build (Linux/macOS) | `./build.sh` | Builds src/Aardvark.Rendering.NonWindows.slnf |
| Build single project | `dotnet build src/ProjectName/ProjectName.fsproj` | Single project |
| Test | `dotnet test src/Tests/Aardvark.Rendering.Tests/Aardvark.Rendering.Tests.fsproj` | Expecto framework |
| Verify | `.\build.cmd && dotnet test src/Tests/Aardvark.Rendering.Tests/Aardvark.Rendering.Tests.fsproj` | Build + test |
| Build GLVM | `src/GLVM/build.cmd` (Win) / `src/GLVM/build.sh` (Unix) | Native OpenGL wrapper |
| Build VKVM | `src/VKVM/build.cmd` (Win) / `src/VKVM/build.sh` (Unix) | Native Vulkan wrapper |

## Dependency Management (Paket)

| Task | Command |
|------|---------|
| Add package | Edit `paket.dependencies`, run `dotnet paket update` |
| Update all | `dotnet paket update` |
| Update single | `dotnet paket update PackageName` |
| Why dependency | `dotnet paket why PackageName` |

**Rules:**
- NEVER use `dotnet restore`; always use `dotnet paket restore`
- `paket.lock` is committed; don't manually edit
- Add per-project deps to `paket.references` (not .fsproj)
- Paket resolves transitives; list only direct deps

## File Ownership

| Change Type | Files to Modify | Files to NOT Touch |
|-------------|-----------------|-------------------|
| Rendering feature | `src/Aardvark.Rendering/**` | Native libs (lib/Native/) |
| Scene graph | `src/Aardvark.SceneGraph/**` | |
| GL backend | `src/Aardvark.Rendering.GL/**` | |
| Vulkan backend | `src/Aardvark.Rendering.Vulkan/**` | |
| Application/windowing | `src/Application/**` | |
| Add test | `src/Tests/**` | |
| Native GLVM | `src/GLVM/**` | Run build.cmd/sh after |
| Native VKVM | `src/VKVM/**` | Run build.cmd/sh after |
| AI documentation | `ai/**` | |
| Package dependencies | `paket.dependencies`, `paket.lock` | |
| CI/CD workflows | `.github/workflows/**` | |

## Pre-Commit Checklist

- [ ] `.\build.cmd` succeeds (or `./build.sh` on Unix)
- [ ] `dotnet test src/Tests/Aardvark.Rendering.Tests/Aardvark.Rendering.Tests.fsproj` passes
- [ ] No new warnings introduced

## Framework & SDK

- .NET SDK: 8.0+ (see global.json, rollForward: latestFeature)
- Target Frameworks: net471, net8.0, net8.0-windows10.0.17763.0, netstandard2.0
- F# Core: >= 8.0.0, F# language version 8
- Windows-only projects: WPF, WinForms variants (use NonWindows.slnf on Linux/macOS)
- Compiler: Enable `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in new projects

## Common Failures

| Symptom | Cause | Fix |
|---------|-------|-----|
| "dependency X not found" | paket.lock out of sync | `dotnet paket update && dotnet paket restore` |
| WPF project fails on Linux | Wrong solution | Use `src/Aardvark.Rendering.NonWindows.slnf` |
| DllNotFoundException (glvm/vkvm) | Native libs missing | Run `src/GLVM/build.sh` or `src/VKVM/build.sh` |
| SDK 8.0.0 not found | Old .NET SDK | Install .NET 8.0+ |
| Silk.NET version conflict | Pinned at 2.22.0 | Check `paket why Silk.NET.*` |
| Tests not discovered | Test SDK issue | Ensure Expecto test adapter in paket.references |
| Shader code not printing | Cache hit | Enable `PrintShaderCode` and delete shader cache or use `VerifyShaderCacheIntegrity = true` |
| Validation layer errors | Vulkan debugging | See ai/DEBUG.md for ValidationLayerConfig and DebugReportConfig |

## Project Structure

```
src/
├── Aardvark.Rendering/          # Core rendering interfaces
├── Aardvark.Rendering.Common/   # Shared types, enums
├── Aardvark.Rendering.GL/       # OpenGL backend
├── Aardvark.Rendering.Vulkan/   # Vulkan backend
├── Aardvark.SceneGraph/         # Scene graph with ISg interface
├── Aardvark.SceneGraph.Assimp/  # Model loading
├── Application/                 # Windowing (Slim, WPF, WinForms, OpenVR)
├── Tests/                       # Expecto tests
├── GLVM/                        # Native C++ OpenGL wrapper
├── VKVM/                        # Native C++ Vulkan wrapper
├── Examples (netcore)/          # Example applications
└── *.sln, *.slnf                # Solutions
```

## Key Concepts

- **Language**: F# codebase using FSharp.Data.Adaptive for incremental computation
- **Shaders**: Written in F# via FShade DSL, not raw GLSL
- **Resources**: Ref-counted (Acquire/Release), not garbage collected
- **Native libs**: GLVM/VKVM are pre-built; only rebuild if modifying C++ source

## For Consumer Projects

AI agents working on projects that **use** Aardvark.Rendering should note these patterns.

### Common Consumer Patterns

| Pattern | Description | Look For |
|---------|-------------|----------|
| Adaptify | Model type code generation | `.g.fs` files, `[<ModelType>]` attributes |
| Aardium | Electron-based desktop wrapper | `aardium/` directory |
| FDA | Incremental computation | `aval`, `aset`, `transact` usage |

### Adaptify Workflow

Projects using `[<ModelType>]` require regeneration after model changes:

```
dotnet adaptify --lenses --local --force
```

Generated files (`.g.fs`) should not be edited manually.

### Version Compatibility

| Aardvark.Rendering | .NET SDK | Notes |
|--------------------|----------|-------|
| 5.5.x | 8.0+ | Stable, most consumer projects |
| 5.6.x-prerelease | 8.0+ | Pre-release features |

### Consumer Debugging

| Symptom | Likely Cause | Fix |
|---------|--------------|-----|
| Shader compilation fails | FShade version mismatch | `dotnet paket why FShade` |
| Scene not rendering | Missing applicator | Check SG-PATTERNS.md gotchas |
| Adaptive updates stale | Missing transact/force | Wrap mutations in `transact` |
| Native crash (GLVM/VKVM) | ABI mismatch | Update Aardvark packages |

### Application Layer Decision

| Type | Package | Use When |
|------|---------|----------|
| Slim.GL | Application.Slim.GL | Headless, CLI, offscreen |
| Slim.Vulkan | Application.Slim.Vulkan | Headless with Vulkan |
| Aardium | Application.Slim + Aardium | Web UI + native rendering |
| WinForms | Application.WinForms | Windows desktop embedding |
| WPF | Application.WPF | Windows XAML integration |

### Consumer Documentation

| Document | Purpose |
|----------|---------|
| ai/CONSUMER-PATTERNS.md | Application init, scene graph, adaptive patterns |
| ai/CSHARP-INTEGRATION.md | C# projects using Aardvark |
| ai/TEMPLATE-CONSUMER.md | Starting template for consumer AGENTS.md |

Copy `ai/TEMPLATE-CONSUMER.md` to your project's `AGENTS.md` and customize.
