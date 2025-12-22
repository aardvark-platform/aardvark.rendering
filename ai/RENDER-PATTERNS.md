# Rendering Patterns

Usage patterns for rendering APIs, offscreen rendering, and common pitfalls.

## Usage Patterns

### Complete Triangle Example

```fsharp
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive

// Define shader
let effect =
    FShade.Effect.ofFunction <| fun (v : Vertex) ->
        fragment {
            return v.c
        }

// Create resources
let positions = [| V3f(-1, -1, 0); V3f(1, -1, 0); V3f(0, 1, 0) |]
let colors = [| C4b.Red; C4b.Green; C4b.Blue |]

let posBuffer = runtime.PrepareBuffer(ArrayBuffer(positions), BufferUsage.Vertex)
let colorBuffer = runtime.PrepareBuffer(ArrayBuffer(colors), BufferUsage.Vertex)

// Create attributes
let attributes = AttributeProvider.ofMap <| Map.ofList [
    DefaultSemantic.Positions, BufferView(posBuffer, typeof<V3f>)
    DefaultSemantic.Colors, BufferView(colorBuffer, typeof<C4b>, normalized=true)
]

// Create render object
let ro = RenderObject()
ro.Mode <- IndexedGeometryMode.TriangleList
ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(0, 3) |])
ro.Surface <- Surface.Effect effect
ro.VertexAttributes <- attributes

// Compile and render
let task = runtime.CompileRender(fboSignature, ASet.ofList [ro])
task.Update(AdaptiveToken.Top, RenderToken.Empty)
task.Run(AdaptiveToken.Top, RenderToken.Empty, OutputDescription.ofFramebuffer fbo)
```

### Indexed Mesh with Uniforms

```fsharp
// Create index buffer
let indices = [| 0; 1; 2; 2; 3; 0 |]
let indexBuffer = runtime.PrepareBuffer(ArrayBuffer(indices), BufferUsage.Index)

// Uniforms
let modelViewProj = AVal.constant (M44f.Identity)
let diffuseColor = AVal.constant (C4f.White)

let uniforms = UniformProvider.ofMap <| Map.ofList [
    Symbol.Create "ModelViewProj", modelViewProj :> IAdaptiveValue
    Symbol.Create "DiffuseColor", diffuseColor :> IAdaptiveValue
]

// Render object
let ro = RenderObject()
ro.Mode <- IndexedGeometryMode.TriangleList
ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(0, 6) |])
ro.Indices <- Some (BufferView(indexBuffer, typeof<int>))
ro.VertexAttributes <- vertexAttributes
ro.Uniforms <- uniforms
ro.Surface <- Surface.Effect effect
```

### Adaptive Rendering

```fsharp
// Changeable render object set
let renderObjects = cset [ro1; ro2; ro3]

// Compile task (updates automatically)
let task = runtime.CompileRender(fboSignature, renderObjects)

// Add/remove objects
transact (fun () ->
    renderObjects.Add(ro4) |> ignore
    renderObjects.Remove(ro1) |> ignore
)

// Update and render (picks up changes)
task.Update(AdaptiveToken.Top, RenderToken.Empty)
task.Run(AdaptiveToken.Top, RenderToken.Empty, output)
```

### Texture Upload/Download

```fsharp
// Create texture
let size = V3i(1024, 1024, 1)
let tex = runtime.CreateTexture(size, TextureDimension.Texture2D,
                                TextureFormat.Rgba8, levels=1, samples=1)

// Upload from PixImage
let img = PixImage<byte>(Col.Format.RGBA, size.XY)
runtime.PrepareTexture(PixTexture2d(PixImageMipMap [| img |], TextureParams.empty))

// Upload to specific level/slice
let tensor = NativeTensor4<byte>(...)
runtime.Upload(tex.[0, 0], tensor, Col.Format.RGBA, V3i.Zero, size)

// Download
let result = NativeTensor4<byte>(size.XYI, 4L)
runtime.Download(tex.[0, 0], result, Col.Format.RGBA, V3i.Zero, size)
```

### Framebuffer Creation

```fsharp
// Define signature
let colorAttachment = { Name = DefaultSemantic.Colors; Format = TextureFormat.Rgba8 }
let signature = runtime.CreateFramebufferSignature(
    Map.ofList [0, colorAttachment],
    Some TextureFormat.Depth24Stencil8,
    samples = 1,
    layers = 1
)

// Create attachments
let colorTex = runtime.CreateTexture(V3i(size, 1), TextureDimension.Texture2D,
                                     TextureFormat.Rgba8, 1, 1)
let depthTex = runtime.CreateTexture(V3i(size, 1), TextureDimension.Texture2D,
                                     TextureFormat.Depth24Stencil8, 1, 1)

// Create framebuffer
let attachments = Map.ofList [
    DefaultSemantic.Colors, colorTex.[TextureAspect.Color, 0, 0] :> IFramebufferOutput
    DefaultSemantic.DepthStencil, depthTex.[TextureAspect.Depth, 0, 0] :> IFramebufferOutput
]
let fbo = runtime.CreateFramebuffer(signature, attachments)
```

### Compute Shader

```fsharp
// Define compute shader (FShade)
let computeShader =
    FShade.ComputeShader.ofFunction V3i(16, 16, 1) <| fun () ->
        compute {
            let id = getWorkGroupId()
            // ... shader code
        }

// Create and dispatch
let shader = runtime.CreateComputeShader(computeShader)
let binding = runtime.CreateInputBinding(shader, uniforms)

let task = runtime.CompileCompute(AList.ofList [
    ComputeCommand.BindCmd shader
    ComputeCommand.SetInputCmd binding
    ComputeCommand.DispatchCmd (V3i(64, 64, 1))
])

task.Run(AdaptiveToken.Top, RenderToken.Empty)
```

## Offscreen Rendering

Pattern for shadow maps, render-to-texture, deferred passes:

```fsharp
// Create framebuffer signature
let signature =
    runtime.CreateFramebufferSignature [
        DefaultSemantic.Colors, TextureFormat.Rgba8
        DefaultSemantic.DepthStencil, TextureFormat.DepthComponent32f
    ]

// Compile scene graph to render task
let renderTask =
    scene
    |> Sg.shader { do! DefaultSurfaces.stableTrafo; do! DefaultSurfaces.sgColor }
    |> Sg.viewTrafo viewMatrix
    |> Sg.projTrafo projMatrix
    |> Sg.compile runtime signature

// Render to textures
let colorTex, depthTex =
    renderTask
    |> RenderTask.renderToColorAndDepthWithClear (AVal.constant (V2i(2048, 2048))) clearValues

// Use in another scene
otherScene
|> Sg.texture DefaultSemantic.DiffuseColorTexture colorTex
```

Key APIs:
| API | Purpose |
|-----|---------|
| `runtime.CreateFramebufferSignature` | Define render target formats |
| `Sg.compile runtime signature` | Scene graph to IRenderTask |
| `RenderTask.renderToColor` | Single color output |
| `RenderTask.renderToColorAndDepth` | Color + depth outputs |
| `RenderTask.renderToColorAndDepthWithClear` | With clear values |

## Large-Scale Scenes

For planetary/astronomical scales, avoid floating-point precision loss:

```fsharp
// WRONG: Large coordinates cause jitter
let scene = objects |> Sg.trafo (Trafo3d.Translation(largeOffset))

// CORRECT: Offset geometry to origin, apply inverse as trafo
let offset = AVal.map (fun objs -> Trafo3d.Translation(objs.[0].Position)) objects

let scene =
    objects
    |> AVal.map (fun objs offset ->
        objs |> Array.map (fun o -> o.TransformedBy(offset.Inverse))
    )
    |> Sg.dynamic
    |> Sg.trafo offset  // Re-applies offset in shader with higher precision
```

Tips:
- Keep geometry near origin (within ~10,000 units)
- Use stable transformations: `DefaultSurfaces.stableTrafo`
- Scale via geometry, not model trafo, when possible

## Gotchas

| # | Issue | Fix |
|---|-------|-----|
| 1 | Missing Acquire/Release | Pair `Acquire()` with `Release()` in try/finally; leaks GPU memory otherwise |
| 2 | Wrong AdaptiveToken | Pass token through `GetValue(token)`, don't create new `AdaptiveToken.Top` |
| 3 | Thread/Context Safety | Use `runtime.ContextLock` before GL/Vulkan calls |
| 4 | Framebuffer Signature Mismatch | RenderObject must match signature (attachments, formats, samples) |
| 5 | Mip Level Size | Size at level n = `size / 2^n`; use `tex.GetSize(n)` for uploads |
| 6 | Empty DrawCalls Array | `DrawCalls.Direct [||]` renders nothing silently |
| 7 | RenderObject.Id Collision | Use `RenderObject.Clone(ro)` for new ID, not `RenderObject(ro)` |
| 8 | Activate Not Disposed | `Activate` must return `IDisposable` that releases resources |
| 9 | TextureAspect Mismatch | Aspect must match format (can't get depth from color texture) |
| 10 | Uniform Buffer Alignment | Use `[<Struct; StructLayout(LayoutKind.Sequential)>]` for std140/std430 |
| 11 | Offscreen Signature Mismatch | `Sg.compile` signature must match framebuffer formats exactly |
| 12 | RenderTask Disposal | Dispose offscreen tasks to free GPU textures |

## See Also

- **RENDER-CORE.md** - IRuntime, resources, render objects, tasks, providers
- **BACKENDS.md** - OpenGL/Vulkan specifics
- **SG-PATTERNS.md** - Scene graph usage patterns
- **FSharp.Data.Adaptive** - `aval`, `aset`, `alist`, `amap` documentation
