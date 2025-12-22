# AI Agent Reference - [PROJECT NAME]

Primary entry point for AI agents working with this project.

## Commands

| Task | Command | Notes |
|------|---------|-------|
| Restore | `dotnet tool restore && dotnet paket restore` | |
| Build | `dotnet build src/[SOLUTION].sln` | |
| Test | `dotnet test src/Tests/[TESTS].fsproj` | |

## Model Types (Adaptify)

If using `[<ModelType>]` attributes:

1. Generated files end in `.g.fs` - do not edit
2. After modifying model types: `dotnet adaptify --lenses --local --force`
3. Commit both source and generated files

## Project Structure

```
src/
├── [Project.App]/      # Main application
├── [Project.Core]/     # Core logic
└── Tests/              # Test projects
```

## Aardvark Dependencies

Using Aardvark.Rendering ~X.Y.Z

Aardvark docs: https://github.com/aardvark-platform/aardvark.rendering/blob/master/ai/README.md

## Pre-Commit Checklist

- [ ] Build succeeds
- [ ] Tests pass
- [ ] Adaptify regenerated if model types changed
- [ ] No new warnings

## Rendering Layer

| Component | Location | Notes |
|-----------|----------|-------|
| Application init | [path] | OpenGL / Vulkan / Aardium |
| Main scene graph | [path] | Primary Sg.* composition |
| Custom applicators | [path] | If extending Sg.* |
| Shaders | [path] | FShade effects |

## Threading Model

- Rendering thread: [single / multi]
- Adaptive updates: [where transact calls happen]
- GPU resource creation: [where runtime.* calls happen]

## Common Issues

| Symptom | Fix |
|---------|-----|
| Scene not updating | Verify `transact` wraps model changes |
| Shader compilation fails | Check FShade version compatibility |
| Black screen | Verify camera setup and projection |
| Performance issues | Profile adaptive dependency chains |
| [Add project-specific issues] | |

## Aardvark Reference

- Patterns: https://github.com/aardvark-platform/aardvark.rendering/blob/master/ai/CONSUMER-PATTERNS.md
- C# integration: https://github.com/aardvark-platform/aardvark.rendering/blob/master/ai/CSHARP-INTEGRATION.md
- Full docs: https://github.com/aardvark-platform/aardvark.rendering/blob/master/ai/README.md
