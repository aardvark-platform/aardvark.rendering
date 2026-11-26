# Application Layer Reference

Windowing abstractions and rendering integration across OpenGL, Vulkan, WPF, WinForms, and OpenVR.

## Project Structure

| Project | Purpose | Platform | Backend |
|---------|---------|----------|---------|
| `Aardvark.Application` | Core interfaces (`IApplication`, `IRenderControl`, `IRenderTarget`, `IMessageLoop`) | All | Any |
| `Aardvark.Application.Utilities` | Window management helpers, synchronization contexts | All | Any |
| `Aardvark.Application.Slim` | GLFW windowing foundation | Win/Linux/macOS | Any |
| `Aardvark.Application.Slim.GL` | OpenGL backend for GLFW | Win/Linux/macOS | OpenGL |
| `Aardvark.Application.Slim.Vulkan` | Vulkan backend for GLFW | Win/Linux/macOS | Vulkan |
| `Aardvark.Application.WPF` | WPF controls and abstractions | Windows | Any |
| `Aardvark.Application.WPF.GL` | OpenGL rendering in WPF (threaded/sharing variants) | Windows | OpenGL |
| `Aardvark.Application.WinForms` | WinForms controls, keyboard/mouse handling | Windows | Any |
| `Aardvark.Application.WinForms.GL` | OpenGL rendering in WinForms | Windows | OpenGL |
| `Aardvark.Application.WinForms.Vulkan` | Vulkan rendering in WinForms | Windows | Vulkan |
| `Aardvark.Application.OpenVR` | VR abstractions, device tracking | All | Any |
| `Aardvark.Application.OpenVR.GL` | OpenGL backend for VR | All | OpenGL |
| `Aardvark.Application.OpenVR.Vulkan` | Vulkan backend for VR | All | Vulkan |

## Backend Selection

Backend is chosen at initialization and fixed for application lifetime.

### OpenGlApplication

```fsharp
open Aardvark.Application.Slim

// Basic initialization
let app = new OpenGlApplication()

// With debug and shader cache
let app = new OpenGlApplication(forceNvidia = false, debug = true, shaderCachePath = Some "cache/")

// Force NVIDIA GPU on Windows (Optimus systems)
let app = new OpenGlApplication(forceNvidia = true, debug = false)

// Hide Cocoa menu bar on macOS
let app = new OpenGlApplication(forceNvidia = true, debug = DebugLevel.None, shaderCachePath = Context.DefaultShaderCachePath, hideCocoaMenuBar = true)

// Create window
let win = app.CreateGameWindow(1024, 768)
win.RenderTask <- renderTask
win.Run()
```

**Initialization sequence**:
1. Probes OpenGL versions from `RuntimeConfig.MajorVersion.MinorVersion` (4.6, or 4.1 on macOS) down to 3.3 by creating offscreen windows
2. Queries `KHR_no_error` and debug-context support; a no-error context is used when `ErrorFlagCheck` is disabled
3. Creates resource context (first window becomes parent)
4. Installs OpenTK context delegate for GLFW interop

### VulkanApplication

```fsharp
open Aardvark.Application.Slim

// Basic initialization
let app = new VulkanApplication()

// With debug layers
let app = new VulkanApplication(debug = true)

// With custom extensions
let extensions = ["VK_KHR_ray_tracing_pipeline"]
let app = new VulkanApplication(debug = false, extensions = extensions)

// With device chooser
let chooser = { new IDeviceChooser with
    member _.Run(devices) = devices |> Array.maxBy (fun d -> d.Limits.Image.MaxDimension2D.X)
}
// an object expression bypasses the AARDVARK_VULKAN_DEVICE override and the cached choice;
// derive from DeviceChooser and override Choose to keep both
let app = new VulkanApplication(debug = false, deviceChooser = chooser)

// Create window
let win = app.CreateGameWindow(1024, 768)
win.RenderTask <- renderTask
win.Run()
```

**Initialization sequence**:
1. Aggregates surface extensions (`VK_KHR_surface`, platform variants, `VK_KHR_swapchain`)
2. Creates Vulkan instance via `HeadlessVulkanApplication`
3. Selects the physical device (`deviceChooser`, default `DeviceChooserAuto(preferDedicated = true)`; see BACKENDS.md, Device Selection)
4. Throws `NotSupportedException` when an instance extension GLFW requires is not enabled; sample-count limits are queried per window

## Slim Variants (GLFW)

Cross-platform windowing via GLFW 3.x (Silk.NET.GLFW). Supports Windows, Linux, macOS. The GLFW `Window` implements `IRenderWindow`, the `IRenderControl` variant with a `Run` loop.

### Window Configuration

```fsharp
open Aardvark.Glfw

let config = {
    WindowConfig.Default with
        title        = "Aardvark App"
        width        = 1920
        height       = 1080
        samples      = 8            // MSAA samples
        resizable    = true
        vsync        = true
        physicalSize = false        // macOS retina: true = native resolution
        transparent  = false        // Composited framebuffer
        stereo       = false        // OpenGL quad-buffer stereo
}

let win = app.CreateGameWindow(config)
```

### Window Events

```fsharp
// Keyboard
win.KeyDown.Add (fun e ->
    printfn "Key: %A, Shift: %b, Ctrl: %b" e.Key e.Shift e.Ctrl
)

// Mouse
win.MouseDown.Add (fun e ->
    printfn "Button: %A at %A" e.Button e.Position
)

// Resize
win.Resize.Add (fun e ->
    printfn "Framebuffer: %A, Physical: %A, Window: %A"
        e.FramebufferSize e.PhysicalSize e.WindowSize
)

// Focus
win.FocusChanged.Add (fun hasFocus ->
    printfn "Focus: %b" hasFocus
)

// Drag and drop
win.DropFiles.Add (fun files ->
    for file in files do
        printfn "Dropped: %s" file
)
```

When focus is lost, application backends release all held keys and mouse buttons before publishing the `false` focus event. These cancellation releases update adaptive input state and emit ordinary `Up` events, but do not synthesize `Click` or `DoubleClick`; the next completed click starts a new click sequence.

### Rendering Modes

```fsharp
// On-demand rendering (default)
win.RenderAsFastAsPossible <- false

// Continuous rendering
win.RenderAsFastAsPossible <- true

// VSync control (OpenGL only; Vulkan vsync is a swapchain property)
win.VSync <- true

// GPU timing
win.MeasureGpuTime <- true
printfn "Avg frame time: %A, GPU time: %A"
    win.AverageFrameTime win.AverageGPUFrameTime
```

### Threading Model

```fsharp
// GLFW requires main thread for all windowing operations
// Instance.Invoke marshals to main thread (blocks caller)

let app = new OpenGlApplication()

// Safe from any thread
app.Instance.Invoke (fun () ->
    let win = app.CreateGameWindow(1024, 768)
    win.Title <- "Created on main thread"
    win
)

// Async enqueue (non-blocking)
app.Instance.Post (fun () ->
    printfn "Runs on main thread eventually"
)

// Check if on main thread
if app.Instance.IsMainThread then
    printfn "Already on main thread"
```

### Keyboard Shortcuts (Built-in)

| Shortcut | Action |
|----------|--------|
| Ctrl+Shift+R | Toggle render-as-fast-as-possible |
| Ctrl+Shift+V | Toggle VSync |
| Ctrl+Shift+G | Toggle GPU time measurement |
| Ctrl+Shift+Enter | Toggle fullscreen |

### Gamepad Support

```fsharp
// Up to 16 simultaneous gamepads
win.Gamepads |> AMap.toAVal |> AVal.map (fun gamepads ->
    gamepads |> HashMap.iter (fun id gamepad ->
        let pos = gamepad.LeftStick |> AVal.force
        printfn "Gamepad %s left stick: %A" id pos
    )
)
```

## WPF Integration (Windows Only)

### Basic Usage

```fsharp
open Aardvark.Application.WPF

// In XAML
<Border x:Name="renderControl" />

// In code-behind
let app = new Aardvark.Application.WPF.OpenGlApplication()
let ctrl = RenderControl()
app.Initialize(ctrl, samples = 8)   // creates the OpenGlRenderControl implementation
ctrl.RenderTask <- renderTask
renderControl.Child <- ctrl
```

### Variants

| Type | Use Case | Threading |
|------|----------|-----------|
| `RenderControl` | Basic rendering | UI thread |
| `ThreadedRenderControl` | Background rendering | Dedicated render thread |
| `OpenGlSharingRenderControl` | D3D/GL interop | UI thread with WGL_NV_DX_interop |

**OpenGlSharingRenderControl**: Best WPF integration, uses `WGL_NV_DX_interop` to share textures between Direct3D and OpenGL. Requires a driver with that extension; selected with `Config.useSharingControl`.

## WinForms Integration (Windows Only)

### OpenGL

```fsharp
open Aardvark.Application.WinForms

let app = new OpenGlApplication()
let ctrl = new RenderControl()
app.Initialize(ctrl, samples = 1)
ctrl.RenderTask <- renderTask
ctrl.Dock <- DockStyle.Fill
form.Controls.Add(ctrl)
```

### Vulkan

```fsharp
open Aardvark.Application.WinForms

let app = new VulkanApplication()
let ctrl = new RenderControl()
app.Initialize(ctrl, samples = 1)
ctrl.RenderTask <- renderTask
ctrl.Dock <- DockStyle.Fill
form.Controls.Add(ctrl)
```

## OpenVR Integration

Backend-agnostic VR with GL and Vulkan implementations.

### Device Tracking

```fsharp
open Aardvark.Application.OpenVR

type VrDeviceType =
    | Other = 0
    | Hmd = 1
    | Controller = 2
    | TrackingReference = 3

// Motion state is adaptive
type MotionState() =
    member x.IsValid : aval<bool>
    member x.Pose : aval<Trafo3d>
    member x.Velocity : aval<V3d>
    member x.AngularVelocity : aval<V3d>
```

### Coordinate System Conversion

OpenVR uses different coordinate system than Aardvark. Automatic conversion:

```fsharp
// OpenVR → Aardvark (applied automatically)
let oursToTheirs = Trafo3d.FromBasis(V3d.IOO, V3d.OOI, V3d.OIO, V3d.Zero)
pose.Value <- Trafo3d.FromBasis(V3d.IOO, -V3d.OOI, V3d.OIO, V3d.Zero) * t * flip
```

### Stereo Rendering

**GL Backend**: Uses texture arrays (layer 0 = left eye, layer 1 = right eye)

**Vulkan Backend**: Same layer-based approach

```fsharp
// Per-layer uniforms (automatically handled)
let perLayerUniforms = [
    "ProjTrafo"; "ViewTrafo"; "ModelViewTrafo"
    "ViewProjTrafo"; "ModelViewProjTrafo"
    "ProjTrafoInv"; "ViewTrafoInv"; "ModelViewTrafoInv"
    "ViewProjTrafoInv"; "ModelViewProjTrafoInv"
]
```

## Platform Considerations

### Windows

```fsharp
// Detect platform
if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
    // NVIDIA Optimus: force NVIDIA GPU
    DynamicLinker.tryLoadLibrary "nvapi64.dll" |> ignore

// Virtual key conversion for accurate keyboard mapping
let key = Aardvark.Application.KeyConverter.keyFromVirtualKey virtualKey
```

**Window frame border**: Windows 10 has GLFW bug #539 (incorrect frame size reporting). Workaround in `getFrameBorder` (`GLFW.fs`), applied on Windows 10 only.

### macOS

```fsharp
// Retina display handling
let config = {
    WindowConfig.Default with
        physicalSize = true  // Use native resolution (2x for retina)
}

// Hide Cocoa menu bar
let app = new OpenGlApplication(forceNvidia = true, debug = DebugLevel.None, shaderCachePath = Context.DefaultShaderCachePath, hideCocoaMenuBar = true)

// Mouse coordinates are already multiplied by the window's content scale; no manual scaling
```

**Limitations**:
- Window icons silently ignored (cannot set on macOS)
- The `CocoaRetinaFramebuffer` hint is set from `physicalSize`

### Linux

Standard GLFW behavior. X11 or Wayland surfaces. Window icons work correctly.

## Usage Patterns

### Complete Window Setup

```fsharp
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Application.Slim
open FSharp.Data.Adaptive

[<EntryPoint>]
let main argv =
    Aardvark.Init()

    use app = new OpenGlApplication()
    use win = app.CreateGameWindow(1024, 768)

    // Setup scene
    let sg =
        Sg.box (AVal.constant C4b.Red) (AVal.constant Box3d.Unit)
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.vertexColor
        }

    // Create render task
    let task =
        app.Runtime.CompileRender(win.FramebufferSignature, sg)

    win.RenderTask <- task
    win.Run()
    0
```

### Camera Control

```fsharp
open Aardvark.Application

let cameraController =
    AVal.integrate initialView win.Time [
        DefaultCameraController.controlWSAD win.Keyboard win.Time
        DefaultCameraController.controlLookAround win.Mouse
    ]

let camera =
    cameraController |> AVal.map (fun f ->
        CameraView.lookAt (V3d(3,3,3)) V3d.Zero V3d.OOI
        |> f
    )
```

### Multi-Window Rendering

```fsharp
use app = new OpenGlApplication()
let win1 = app.CreateGameWindow(800, 600)
let win2 = app.CreateGameWindow(800, 600)

win1.RenderTask <- task1
win2.RenderTask <- task2

// Run multiple windows
app.Instance.Run(win1, win2)
```

### Background Thread Safety

```fsharp
// Wrong: crashes or deadlocks
Task.Run (fun () ->
    win.Title <- "Crash!"  // GLFW not thread-safe
)

// Correct: marshal to main thread
Task.Run (fun () ->
    app.Instance.Invoke (fun () ->
        win.Title <- "Safe"
    )
)
```

## Camera Controllers

Built-in camera controllers for interactive navigation:

```fsharp
// Free-fly camera (WASD + mouse)
let view =
    DefaultCameraController.control win.Mouse win.Keyboard win.Time initialView

// Orbit around a world-space point, preserving the camera's distance while dragging.
let orbitCenter = V3d(100.0, 200.0, 30.0)
let orbitInitialView =
    CameraView.lookAt (orbitCenter + V3d(0.0, -10.0, 3.0)) orbitCenter V3d.OOI
let view =
    AVal.integrate orbitInitialView win.Time [
        DefaultCameraController.controlOrbitAround win.Mouse (AVal.constant orbitCenter)
        DefaultCameraController.controlZoom win.Mouse
    ]

// Combined with speed control
let view =
    adaptive {
        let! speed = cameraSpeed
        match! cameraMode with
        | FreeFly ->
            return! DefaultCameraController.controlExt (float speed) win.Mouse win.Keyboard win.Time initialView
        | Orbit ->
            return! AVal.integrate initialView win.Time [
                DefaultCameraController.controlZoom win.Mouse   // controlZoomWithSpeed takes a cval<float>
                DefaultCameraController.controlOrbitAround win.Mouse center
            ]
    }
```

Available controllers:
| Controller | Purpose |
|------------|---------|
| `control` | Free-fly: WASD, mouse look, pan, zoom and scroll |
| `controlExt speed` | Free-fly with speed parameter |
| `controlOrbitAround` | Left-drag orbit around an adaptive world-space point |
| `controlZoom` / `controlZoomWithSpeed` | Mouse wheel zoom |
| `controllScroll` / `controllScrollWithSpeed` | Scroll-based movement |

---

## Manual Render Control

For performance-critical applications, disable automatic rendering:

```fsharp
// WinForms
let glCtrl = new OpenGlRenderControl(app.Runtime, debug = DebugLevel.None, samples = 1)
glCtrl.OnPaintRender <- false      // Don't render on Paint
glCtrl.AutoInvalidate <- false     // Don't auto-invalidate

// Manual render with locking
member x.Render() =
    lock x (fun () ->
        glCtrl.Render()
    )

// Manual invalidation when needed
member x.RequestRender() =
    glCtrl.Invalidate()
```

When to use:
- Complex multi-view applications
- Integration with external event loops
- Custom frame pacing requirements
- Avoiding unnecessary redraws

---

## Gotchas

| # | Issue | Fix |
|---|-------|-----|
| 1 | SubSampling Not Implemented | The GLFW window fails for values other than 1.0; the OpenVR applications implement it through `AdjustSize` |
| 2 | Main Thread Requirement | Use `app.Instance.Invoke` from background threads; GLFW requires main thread |
| 3 | Context Sharing Breaks | `RuntimeConfig.RobustContextSharing` (default true) serializes context creation; keep it on with multiple WinForms/WPF controls |
| 4 | Retina/HiDPI Scaling | Three sizes: windowSize, physicalSize, framebufferSize; mouse coordinates are already scaled |
| 5 | OpenGL Version Probing | Probes from `RuntimeConfig.MajorVersion.MinorVersion` (4.6, 4.1 on macOS) down to 3.3; fails if none is supported |
| 6 | Swapchain Recreation | Every resize recreates swapchain (expensive); clamp resize frequency |
| 7 | Incomplete Key Mappings | Equal, Menu, Brackets map to `Keys.None`; use `GetKeyName` workaround |
| 8 | VSync Context Switch | VSync applies to OpenGL only and makes the context current; avoid frequent toggling |
| 9 | WPF Control Init Once | `Implementation` can only be set once; create new control for backend switch |
| 10 | Win10 Frame Border Bug | GLFW #539; workaround in `getFrameBorder` (GLFW.fs), Windows 10 only |
| 11 | AutoInvalidate | Default true re-invalidates only while the task is still out of date; `RenderContinuously` is the continuous mode |

## See Also

- **RENDER-CORE.md**: `IRuntime`, `IRenderTask`, resource management
- **BACKENDS.md**: OpenGL and Vulkan runtime implementations
- **SG-CORE.md**: Scene graph composition for render tasks
