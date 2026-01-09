# Debugging and Diagnostic Tools

Reference for debug configurations, shader inspection, validation layers, and external debugger integration.

---

## 1. Debug Configuration Overview

Both OpenGL and Vulkan backends support debugging through two approaches:

| Approach | Usage | When to Use |
|----------|-------|-------------|
| **DebugLevel enum** | Cross-backend, 4 preset levels | Quick setup, switching between GL/Vulkan |
| **Backend-specific DebugConfig** | Fine-grained control | Custom debug needs, production monitoring |

**DebugLevel enum** (defined in [src/Aardvark.Rendering/Runtime/Runtime.fs](../src/Aardvark.Rendering/Runtime/Runtime.fs)):

```fsharp
type DebugLevel =
    | None    // No debugging, max performance
    | Minimal // Errors and warnings logged
    | Normal  // Detailed info, exceptions on errors
    | Full    // All debug features (20-30% performance impact)
```

**Usage:**

```fsharp
// Simple initialization
let runtime = new GL.Runtime(DebugLevel.Normal)

// Or for Vulkan
let runtime = new Vulkan.Runtime(device, DebugLevel.Normal)
```

**Performance Impact:**
- `None`: No overhead
- `Minimal`/`Normal`: <5% overhead (logging only)
- `Full`: 20-30% overhead (all validation, synchronous callbacks, task logging)

---

## 2. OpenGL Debug Configuration

Source: [src/Aardvark.Rendering.GL/Core/Config.fs](../src/Aardvark.Rendering.GL/Core/Config.fs)

### 2.1 DebugLevel Presets

| Level | DebugOutput | ErrorFlagCheck | PrintShaderCode | DebugLabels | DebugRenderTasks |
|-------|-------------|----------------|-----------------|-------------|------------------|
| None | Disabled | Disabled | No | No | No |
| Minimal | Warnings/Errors | PrintError | No | No | No |
| Normal | Low severity+ | ThrowOnError | Yes | Yes | No |
| Full | All messages | ThrowOnError | Yes | Yes | Yes |

### 2.2 Fine-Grained DebugConfig

```fsharp
type DebugConfig =
    {
        // GL_KHR_debug callback configuration
        DebugOutput : DebugOutputConfig option

        // RenderDoc/Nsight resource naming
        DebugLabels : bool

        // Error checking mode
        ErrorFlagCheck : ErrorFlagCheck

        // Print GL calls during render task execution
        DebugRenderTasks : bool

        // Print GL calls during compute task execution
        DebugComputeTasks : bool

        // Print GLSL code during compilation
        PrintShaderCode : bool

        // Warn about double-precision attributes
        DoubleAttributePerformanceWarning : bool
    }
```

**ErrorFlagCheck modes:**
- `Disabled`: No error checking (production)
- `PrintError`: Log errors without exceptions
- `ThrowOnError`: Raise exception on GL errors (recommended for development)

**Custom configuration example:**

```fsharp
let customDebug = {
    DebugConfig.None with
        PrintShaderCode = true
        ErrorFlagCheck = ErrorFlagCheck.ThrowOnError
        DebugLabels = true
}
let runtime = new GL.Runtime(customDebug)
```

### 2.3 DebugOutput Verbosity

```fsharp
type DebugOutputSeverity =
    | Notification = 0  // All messages
    | Low          = 1  // Performance hints, minor issues
    | Medium       = 2  // Potential problems
    | High         = 3  // Errors only

type DebugOutputConfig =
    {
        Verbosity : DebugOutputSeverity
        Synchronous : bool  // true = immediate feedback, false = async (less overhead)
    }
```

**Presets:**
- `DebugOutputConfig.Minimal`: High severity (errors), synchronous
- `DebugOutputConfig.Normal`: Low severity, synchronous
- `DebugOutputConfig.Full`: All notifications, synchronous

---

## 3. Vulkan Debug Configuration

Source: [src/Aardvark.Rendering.Vulkan/Core/Common/Config.fs](../src/Aardvark.Rendering.Vulkan/Core/Common/Config.fs)

### 3.1 DebugLevel Presets

| Level | ValidationLayer | DebugReport | PrintShaderCode | GenerateShaderDebugInfo | OptimizeShaders |
|-------|-----------------|-------------|-----------------|-------------------------|-----------------|
| None | Disabled | Disabled | No | No | Yes |
| Minimal | Standard | Warning+ | No | No | Yes |
| Normal | Standard | Information+ | Yes | No | Yes |
| Full | Full (all features) | Debug (all messages) | Yes | Yes | No |

### 3.2 Fine-Grained DebugConfig

```fsharp
type DebugConfig =
    {
        // Validation layer callback verbosity
        DebugReport : DebugReportConfig option

        // RenderDoc/Nsight resource naming
        DebugLabels : bool

        // Validation layer features
        ValidationLayer : ValidationLayerConfig option

        // Recompile and verify shader caches
        VerifyShaderCacheIntegrity : bool

        // Print GLSL before SPIR-V compilation
        PrintShaderCode : bool

        // Log render task recompilation reasons
        PrintRenderTaskRecompile : bool

        // Log raytracing acceleration structure compaction
        PrintAccelerationStructureCompactionInfo : bool

        // Instance/device info verbosity (0-4 scale)
        PlatformInformationVerbosity : int

        // Generate SPIR-V debug info (for Nsight source debugging)
        GenerateShaderDebugInfo : bool

        // Enable SPIR-V optimization
        OptimizeShaders : bool
    }
```

**Custom configuration example:**

```fsharp
let customDebug = {
    Vulkan.DebugConfig.Normal with
        PrintRenderTaskRecompile = true
        GenerateShaderDebugInfo = true
        OptimizeShaders = false  // Easier debugging
}
```

### 3.3 Validation Layers

#### DebugReportConfig

Controls callback verbosity:

```fsharp
type DebugReportVerbosity =
    | Error       = 1  // Errors only
    | Warning     = 2  // Errors + warnings
    | Information = 3  // + informational messages
    | Debug       = 4  // All messages

type DebugReportConfig =
    {
        Verbosity : DebugReportVerbosity
        BreakOnError : bool              // Trigger debugger breakpoint
        TraceObjectHandles : bool        // Track object creation origins
    }
```

**Presets:**
- `DebugReportConfig.Minimal`: Warning+, no breakpoints
- `DebugReportConfig.Normal`: Information+, breakpoints enabled
- `DebugReportConfig.Full`: Debug (all), breakpoints, object tracing

#### ValidationLayerConfig

Controls validation features:

```fsharp
type ShaderValidation =
    | Disabled    = 0  // No shader validation
    | DebugPrint  = 1  // Enable Debug.Printfn() in shaders
    | GpuAssisted = 2  // GPU-based diagnostics

type ValidationLayerConfig =
    {
        ThreadSafetyValidation : bool        // Detect threading violations
        ObjectLifetimesValidation : bool     // Detect use-after-free
        ShaderBasedValidation : ShaderValidation
        SynchronizationValidation : bool     // Detect race conditions
        BestPracticesValidation : bool       // Performance warnings
        RaytracingValidation : bool          // VK_NV_ray_tracing_validation
    }
```

**Presets:**
- `ValidationLayerConfig.Standard`: Thread safety + object lifetimes only
- `ValidationLayerConfig.DebugPrint`: Standard + shader printf support
- `ValidationLayerConfig.Full`: All features enabled

**Example - Enable shader printf:**

```fsharp
let debugConfig = {
    Vulkan.DebugConfig.Normal with
        ValidationLayer = Some Vulkan.ValidationLayerConfig.DebugPrint
}
```

---

## 4. Shader Debugging

### 4.1 Printing Shader Code

Both backends print GLSL code to the console when `PrintShaderCode = true`.

**When compilation occurs:**
- Lazy: First use of shader (after scene compilation)
- Cached: Not printed when loaded from cache (delete cache to force recompilation)

**OpenGL example:**

```fsharp
let runtime = new GL.Runtime({ DebugConfig.None with PrintShaderCode = true })

// Compile render task (triggers shader compilation)
let task = runtime.CompileRender(signature, scene)
// GLSL printed to console with line numbers
```

**Vulkan example:**

```fsharp
let device = // ... create Vulkan device
let runtime = new Vulkan.Runtime(device, {
    DebugConfig.None with PrintShaderCode = true
})

// GLSL printed before SPIR-V compilation
```

**Output format:**

```
Compiling shader:

     1  #version 450
     2  layout(location = 0) in vec3 Position;
     3  layout(location = 1) in vec4 Color;
     4  ...
```

### 4.2 Shader Printf (Vulkan Only)

Vulkan validation layers support `Debug.Printfn()` for runtime shader debugging.

**Requirements:**
1. Vulkan backend (not OpenGL)
2. `ValidationLayer = Some ValidationLayerConfig.DebugPrint`
3. Use `FShade.Debug.Printfn()` in shader code

**Complete working example** (from [src/Scratch (netcore)/21 - DebugPrint/Program.fs](../src/Scratch%20(netcore)/21%20-%20DebugPrint/Program.fs)):

```fsharp
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open Aardvark.Application

module Config =
    let Backend = Backend.Vulkan

    let Debug : IDebugConfig =
        { Vulkan.DebugConfig.Normal with
            ValidationLayer = Some Vulkan.ValidationLayerConfig.DebugPrint
            PrintRenderTaskRecompile = false
            VerifyShaderCacheIntegrity = true }

module Shader =
    open FShade

    let printPosition (v : Effects.Vertex) =
        vertex {
            Debug.Printfn("P = %v4f", v.pos)
            return v
        }

[<EntryPoint>]
let main argv =
    Aardvark.Init()

    let win =
        window {
            backend Config.Backend
            display Display.Mono
            debug Config.Debug
            samples 8
        }

    let sg =
        Sg.quad
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! Shader.printPosition
                do! DefaultSurfaces.constantColor C4f.IndianRed
                do! DefaultSurfaces.simpleLighting
            }

    win.Scene <- sg
    win.Run()
    0
```

**Format string syntax:**
- `%v4f` - V4f vector
- `%v3f` - V3f vector
- `%v2f` - V2f vector
- `%f` - float
- `%d` - int

**Output appears in validation layer messages**, not standard console.

**Important:** OpenGL does not support shader printf.

---

## 5. External Debuggers

### 5.1 RenderDoc

RenderDoc integration requires named resources.

**Configuration:**

```fsharp
let runtime = new Runtime({ DebugConfig.None with DebugLabels = true })
```

**Naming resources:**

```fsharp
let buffer = runtime.CreateBuffer(...)
buffer.Name <- "VertexBuffer_Terrain"

let texture = runtime.CreateTexture(...)
texture.Name <- "Diffuse_Character"
```

**Workflow:**
1. Run application with `DebugLabels = true`
2. Launch RenderDoc, inject into process
3. Press F12 to capture frame
4. Resources show with assigned names in capture

**Works with:** Both OpenGL and Vulkan backends

### 5.2 NVIDIA Nsight Graphics

Nsight Graphics supports source-level shader debugging with Vulkan.

**Configuration:**

```fsharp
let nsightDebug = {
    Vulkan.DebugConfig.Normal with
        GenerateShaderDebugInfo = true  // Embed SPIR-V debug info
        OptimizeShaders = false          // Disable optimization
        DebugLabels = true               // Resource naming
}
```

**What `GenerateShaderDebugInfo` enables:**
- SPIR-V source line mapping
- Variable inspection in shader debugger
- Step-through GLSL source

**Workflow:**
1. Run application with above config
2. Launch Nsight Graphics
3. Attach to process, capture frame
4. Select shader, click "Debug" to step through source

**Vulkan-only feature.** OpenGL shader debugging uses different tools.

---

## 6. Common Debug Workflows

### 6.1 "Shader Not Compiling"

```fsharp
// 1. Enable shader code printing
let runtime = new Runtime({ DebugConfig.None with PrintShaderCode = true })

// 2. Compile render task (triggers compilation)
let task = runtime.CompileRender(signature, scene)

// 3. Check console for GLSL output with line numbers
// 4. Look for compilation errors in log

// If shader is cached, delete cache to force recompilation:
// Delete: %TEMP%/Aardvark/ShaderCache/ (Windows)
```

### 6.2 "Validation Errors (Vulkan)"

```fsharp
// Start with full validation
let debug = {
    Vulkan.DebugConfig.Full with
        DebugReport = Some {
            DebugReportConfig.Full with
                Verbosity = DebugReportVerbosity.Information // Reduce noise
        }
}

// For synchronization issues specifically:
let syncDebug = {
    Vulkan.DebugConfig.Normal with
        ValidationLayer = Some {
            ValidationLayerConfig.Standard with
                SynchronizationValidation = true
        }
}
```

### 6.3 "Shader Printf Not Working"

**Checklist:**
1. Vulkan backend? (OpenGL not supported)
2. `ValidationLayer = Some ValidationLayerConfig.DebugPrint`?
3. Using `Debug.Printfn()` not regular `printfn`?
4. Checking validation layer output (not standard console)?
5. Shader actually executed? (check with frame capture)

**Working reference:** [src/Scratch (netcore)/21 - DebugPrint/Program.fs](../src/Scratch%20(netcore)/21%20-%20DebugPrint/Program.fs)

### 6.4 "Performance Profiling"

```fsharp
// Production: no debug overhead
let runtime = new Runtime(DebugLevel.None)

// Development: balanced debugging
let runtime = new Runtime(DebugLevel.Normal)

// Deep debugging: expect 20-30% perf hit
let runtime = new Runtime(DebugLevel.Full)

// Custom: shader printing only (minimal overhead)
let customDebug = {
    DebugConfig.None with
        PrintShaderCode = true
        ErrorFlagCheck = ErrorFlagCheck.ThrowOnError
}
```

---

## 7. Quick Reference

### 7.1 Debug Flag Lookup

| Flag | Backend | Purpose | Performance Impact |
|------|---------|---------|-------------------|
| `PrintShaderCode` | Both | Print GLSL during compilation | None (one-time) |
| `DebugLabels` | Both | RenderDoc/Nsight resource names | Negligible |
| `DebugOutput` (GL) | GL | GL_KHR_debug callbacks | Low (async) to High (sync) |
| `ErrorFlagCheck` (GL) | GL | GL error flag checking | Low to Medium |
| `DebugRenderTasks` (GL) | GL | Log GL calls during rendering | High |
| `ValidationLayer` (Vulkan) | Vulkan | Vulkan validation features | Medium to High |
| `DebugReport` (Vulkan) | Vulkan | Validation callback verbosity | Low (async) to Medium (sync) |
| `GenerateShaderDebugInfo` (Vulkan) | Vulkan | Nsight source debugging | Low (larger SPIR-V) |
| `PrintRenderTaskRecompile` (Vulkan) | Vulkan | Log task recompilation | Negligible |
| `ShaderValidation.DebugPrint` (Vulkan) | Vulkan | `Debug.Printfn()` support | Medium |
| `OptimizeShaders` (Vulkan) | Vulkan | SPIR-V optimization | Negative (disabling aids debugging) |
| `VerifyShaderCacheIntegrity` (Vulkan) | Vulkan | Recompile cached shaders | High (startup) |

### 7.2 DebugLevel Comparison

| Feature | None | Minimal | Normal | Full |
|---------|------|---------|--------|------|
| **OpenGL** |
| DebugOutput | Disabled | Warnings/Errors | Low severity+ | All messages |
| ErrorFlagCheck | Disabled | PrintError | ThrowOnError | ThrowOnError |
| PrintShaderCode | No | No | Yes | Yes |
| DebugLabels | No | No | Yes | Yes |
| DebugRenderTasks | No | No | No | Yes |
| **Vulkan** |
| ValidationLayer | Disabled | Standard | Standard | Full |
| DebugReport | Disabled | Warning+ | Information+ | Debug (all) |
| PrintShaderCode | No | No | Yes | Yes |
| DebugLabels | No | No | Yes | Yes |
| GenerateShaderDebugInfo | No | No | No | Yes |
| OptimizeShaders | Yes | Yes | Yes | No |
| VerifyShaderCacheIntegrity | No | No | No | Yes |

---

## 8. Gotchas

| # | Issue | Fix |
|---|-------|-----|
| 1 | `PrintShaderCode` shows nothing | Shader loaded from cache; delete cache or use `VerifyShaderCacheIntegrity = true` |
| 2 | `Debug.Printfn()` produces no output | Vulkan only; requires `ValidationLayer.ShaderBasedValidation = DebugPrint` |
| 3 | Severe performance regression | `DebugLevel.Full` adds 20-30% overhead; use `Normal` for development |
| 4 | RenderDoc shows "Unnamed Buffer" | Set `DebugLabels = true` and assign `resource.Name` property |
| 5 | Nsight can't step through shader source | Set `GenerateShaderDebugInfo = true` + `OptimizeShaders = false` (Vulkan) |
| 6 | Validation errors flood console | Lower `DebugReport.Verbosity` to `Warning` or `Error` |
| 7 | GL errors not raising exceptions | Check `ErrorFlagCheck = ThrowOnError` not `PrintError` |
| 8 | Debug config ignored after startup | DebugConfig must be set before runtime creation; cannot change after constructor |

---

## 9. See Also

- **BACKENDS.md** — Runtime initialization, context management, backend selection
- **RENDER-PATTERNS.md** — Shader compilation, offscreen rendering, resource management
- **NATIVE.md** — GLVM/VKVM debugging, native layer troubleshooting
- **Scratch example: 21 - DebugPrint** — Working `Debug.Printfn()` example ([src/Scratch (netcore)/21 - DebugPrint/](../src/Scratch%20(netcore)/21%20-%20DebugPrint/))
