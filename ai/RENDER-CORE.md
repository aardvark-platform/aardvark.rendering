# Rendering Core APIs

Backend-agnostic rendering interfaces implemented by OpenGL and Vulkan backends. Built on FSharp.Data.Adaptive for incremental computation.

## Runtime Hierarchy

| Interface | Capabilities |
|-----------|-------------|
| `IRuntime` | Top-level interface, composes all runtime capabilities |
| `IFramebufferRuntime` | Framebuffer, texture, renderbuffer creation and management |
| `ITextureRuntime` | Texture operations: upload, download, copy, blit, mipmaps |
| `IBufferRuntime` | Buffer operations: create, upload, download, copy |
| `IComputeRuntime` | Compute shader compilation and execution |
| `IQueryRuntime` | GPU queries: occlusion, timing, pipeline statistics |
| `IRaytracingRuntime` | Raytracing pipelines and acceleration structures (Vulkan only) |
| `ILodRuntime` | Level-of-detail rendering support |

**Inheritance chain:** `IRuntime` -> `IFramebufferRuntime` -> `ITextureRuntime` -> `IBufferRuntime`

### Key IRuntime Methods

```fsharp
PrepareEffect(signature, effect, topology) -> IBackendSurface
PrepareRenderObject(signature, renderObject) -> IPreparedRenderObject
CompileRender(signature, renderObjects) -> IRenderTask
CompileClear(signature, clearValues) -> IRenderTask
CreateFramebufferSignature(colorAttachments, depthAttachment, samples, layers) -> IFramebufferSignature
CreateFramebuffer(signature, attachments) -> IFramebuffer
```

### Backend Implementations

```fsharp
// OpenGL 3.3+
let ctx = new Context(...)
let runtime = new Runtime(DebugLevel.Normal)
runtime.Initialize(ctx)

// Vulkan
let device = Device.Create(...)
let runtime = new Runtime(device)
```

## Resources

### IBuffer Hierarchy

| Type | Purpose | Members |
|------|---------|---------|
| `IBuffer` | Marker interface | (empty) |
| `INativeBuffer` | Native memory buffer | `SizeInBytes`, `Use(action)` |
| `IBackendBuffer` | GPU buffer resource | `Runtime`, `Handle`, `Name`, `Offset`, `SizeInBytes` |
| `IBufferRange` | Buffer sub-range | `Buffer`, `Offset`, `SizeInBytes` |

### BufferView

Describes how buffer elements are accessed.

| Member | Type | Description |
|--------|------|-------------|
| `Buffer` | `aval<IBuffer>` | Adaptive buffer reference |
| `ElementType` | `Type` | Element type (may differ from buffer type) |
| `Stride` | `int` | Bytes between elements (0 = tightly packed) |
| `Offset` | `int` | Start offset in bytes |
| `Normalized` | `bool` | Integer to float conversion for shaders |
| `SingleValue` | `IAdaptiveValue option` | For constant attributes |

**BufferStorage:**
- `Device` - GPU-local, occasional host access
- `Host` - CPU-visible, frequent host access

**BufferUsage flags:** `Index`, `Indirect`, `Vertex`, `Uniform`, `Storage`, `Read`, `Write`, `AccelerationStructure`

### ITexture Types

| Type | Members |
|------|---------|
| `ITexture` | `WantMipMaps` |
| `IBackendTexture` | `Dimension`, `Format`, `Samples`, `Count`, `MipMapLevels`, `Size`, `Handle`, `Name` |
| `ITextureRange` | `Texture`, `Aspect`, `Levels`, `Slices` |
| `ITextureSubResource` | Combines `ITextureSlice` and `ITextureLevel` |

**TextureAspect:** `Color`, `Depth`, `Stencil`

### IFramebuffer Types

```fsharp
type IFramebufferSignature =
    abstract member Runtime : IFramebufferRuntime
    abstract member Samples : int
    abstract member ColorAttachments : Map<int, AttachmentSignature>
    abstract member DepthStencilAttachment : Option<TextureFormat>
    abstract member LayerCount : int
    abstract member PerLayerUniforms : Set<string>

type IFramebuffer =
    abstract member Signature : IFramebufferSignature
    abstract member Size : V2i
    abstract member Handle : uint64
    abstract member Attachments : Map<Symbol, IFramebufferOutput>
```

### Resource Lifecycle

Resources use manual ref-counting via `Acquire/Release`:

```fsharp
// IAdaptiveResource interface
type IAdaptiveResource<'T> =
    inherit IAdaptiveValue<'T>
    abstract member Acquire : unit -> unit
    abstract member Release : unit -> unit
    abstract member GetValue : AdaptiveToken * RenderToken -> 'T

// Usage
resource.Acquire()  // increment ref count
try
    let value = resource.GetValue(token, renderToken)
    // use value
finally
    resource.Release()  // decrement, destroy if 0
```

**AdaptiveResource module:**
```fsharp
let mapped = AdaptiveResource.map (fun x -> transform x) input
let bound = AdaptiveResource.bind (fun x -> getResource x) input
let merged = AdaptiveResource.map2 (fun a b -> combine a b) input1 input2
```

## Render Objects

Encapsulates all state for a draw call.

### RenderObject Members

| Member | Type | Purpose |
|--------|------|---------|
| `Id` | `RenderObjectId` | Unique ID for equality/hashing |
| `AttributeScope` | `Ag.Scope` | Attribute grammar scope for scoped uniforms |
| `IsActive` | `aval<bool>` | Enable/disable rendering |
| `RenderPass` | `RenderPass` | Render pass assignment |
| `DrawCalls` | `DrawCalls` | Direct or indirect draw calls |
| `Mode` | `IndexedGeometryMode` | Primitive topology |
| `Surface` | `Surface` | Shader specification |
| `DepthState` | `DepthState` | Depth test/write state |
| `BlendState` | `BlendState` | Blending configuration |
| `StencilState` | `StencilState` | Stencil operations |
| `RasterizerState` | `RasterizerState` | Culling, fill mode, etc. |
| `ViewportState` | `ViewportState` | Viewport/scissor override |
| `Indices` | `BufferView option` | Index buffer |
| `InstanceAttributes` | `IAttributeProvider` | Per-instance attributes |
| `VertexAttributes` | `IAttributeProvider` | Per-vertex attributes |
| `Uniforms` | `IUniformProvider` | Shader uniforms |
| `Activate` | `unit -> IDisposable` | Per-frame activation callback |

### Pipeline State Structs

```fsharp
[<Struct; CLIMutable>]
type DepthState = {
    Test : aval<DepthTest>
    Bias : aval<DepthBias>
    WriteMask : aval<bool>
    Clamp : aval<bool>
}

[<Struct; CLIMutable>]
type BlendState = {
    Mode : aval<BlendMode>
    ColorWriteMask : aval<ColorMask>
    ConstantColor : aval<C4f>
    AttachmentMode : aval<Map<Symbol, BlendMode>>
    AttachmentWriteMask : aval<Map<Symbol, ColorMask>>
}

[<Struct; CLIMutable>]
type RasterizerState = {
    CullMode : aval<CullMode>
    FrontFacing : aval<WindingOrder>
    FillMode : aval<FillMode>
    Multisample : aval<bool>
    ConservativeRaster : aval<bool>
}

[<Struct; CLIMutable>]
type ViewportState = {
    Viewport : aval<Box2i> option
    Scissor : aval<Box2i> option
}
```

### Surface Types

```fsharp
type Surface =
    | Effect of effect:Effect              // FShade effect
    | Dynamic of compile:Func<...>         // Dynamic compilation
    | Backend of surface:IBackendSurface   // Pre-compiled shader
    | None
```

### DrawCalls

```fsharp
type DrawCalls =
    | Direct of calls:aval<DrawCallInfo[]>
    | Indirect of buffer:aval<IndirectBuffer>
```

## Render Tasks

```fsharp
type IRenderTask =
    inherit IDisposable
    inherit IAdaptiveObject
    abstract member Id : RenderTaskId
    abstract member FramebufferSignature : Option<IFramebufferSignature>
    abstract member Runtime : Option<IRuntime>
    abstract member Update : AdaptiveToken * RenderToken -> unit
    abstract member Run : AdaptiveToken * RenderToken * OutputDescription -> unit
    abstract member FrameId : uint64
    abstract member Use : (unit -> 'a) -> 'a  // context locking
    abstract member Name : string with get, set
```

### Compilation

```fsharp
// From render object set
let task = runtime.CompileRender(fboSignature, renderObjects)

// Clear task
let clearTask = runtime.CompileClear(fboSignature, AVal.constant clearValues)
```

### Execution

```fsharp
// Update adaptive dependencies
task.Update(AdaptiveToken.Top, RenderToken.Empty)

// Run to framebuffer
task.Run(AdaptiveToken.Top, RenderToken.Empty, OutputDescription.ofFramebuffer fbo)

// With context lock
task.Use(fun () ->
    task.Run(token, renderToken, output)
)
```

### Built-in Implementations

| Type | Purpose |
|------|---------|
| `AdaptiveRenderTask` | Renders adaptive set of objects |
| `AListRenderTask` | Renders ordered list |
| `SequentialRenderTask` | Chains multiple tasks |
| `CustomRenderTask` | User-defined behavior |
| `ClearTask` | Framebuffer clearing |
| `EmptyRenderTask` | No-op placeholder |

## Providers

### IUniformProvider

Supplies shader uniforms by name.

```fsharp
type IUniformProvider =
    abstract member TryGetUniform : scope:Ag.Scope * name:Symbol -> IAdaptiveValue voption

// Create from map
let uniforms = UniformProvider.ofMap <| Map.ofList [
    Symbol.Create "ModelViewProj", AVal.constant mvp :> IAdaptiveValue
    Symbol.Create "DiffuseColor", AVal.constant (C4f.White) :> IAdaptiveValue
    Symbol.Create "DiffuseTexture", textureResource :> IAdaptiveValue
]

// Compose providers
let combined = UniformProvider.union globalUniforms objectUniforms
```

### IAttributeProvider

Supplies vertex/instance attributes by name.

```fsharp
type IAttributeProvider =
    abstract member All : seq<Symbol * BufferView>
    abstract member TryGetAttribute : name:Symbol -> BufferView voption

// Create from map
let attributes = AttributeProvider.ofMap <| Map.ofList [
    DefaultSemantic.Positions, BufferView(positionBuffer, typeof<V3f>)
    DefaultSemantic.Normals, BufferView(normalBuffer, typeof<V3f>)
    DefaultSemantic.Colors, BufferView(colorBuffer, typeof<C4b>, normalized=true)
]

// From arrays (auto-wraps in BufferView)
let attributes = AttributeProvider.ofMap <| Map.ofList [
    DefaultSemantic.Positions, positionArray :> Array
    DefaultSemantic.Normals, normalArray :> Array
]
```

## See Also

- **RENDER-PATTERNS.md** - Usage patterns, offscreen rendering, gotchas
- **BACKENDS.md** - OpenGL/Vulkan specifics
- **SG-CORE.md** - High-level scene management

**Source Files:**
- `src/Aardvark.Rendering/Runtime/Runtime.fs` (interfaces)
- `src/Aardvark.Rendering/Pipeline/RenderObject.fs`
- `src/Aardvark.Rendering/Resources/Adaptive/AdaptiveResource.fs`
