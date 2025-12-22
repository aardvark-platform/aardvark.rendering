# Scene Graph Core

Scene graph library for declarative 3D rendering. Uses attribute grammars for state propagation.

> Semantic rules (`[<Rule>]` types) are internal. Use `Sg.*` combinators for typical tasks.

## ISg Hierarchy

### Core Interfaces

| Interface | Purpose | Members |
|-----------|---------|---------|
| `ISg` | Root scene graph marker | Empty interface |
| `IApplicator` | Single-child wrapper | `Child : aval<ISg>` |
| `IGroup` | Multi-child container | `Children : aset<ISg>` |

Source: `src/Aardvark.SceneGraph/Core/Core.fs`

### Leaf Nodes

| Type | Purpose | Key Properties |
|------|---------|----------------|
| `RenderNode` | Basic draw call | `DrawCallInfo`, `Mode : IndexedGeometryMode` |
| `IndirectRenderNode` | GPU-driven rendering | `Indirect : aval<IndirectBuffer>`, `Mode` |
| `RenderObjectNode` | Pre-built objects | `Objects : aset<IRenderObject>` |
| `DelayNode` | Lazy generation | `Generator : Ag.Scope -> ISg` |
| `AdapterNode` | Custom semantics | `Node : obj` |

Source: `src/Aardvark.SceneGraph/Core/Sg.fs`

## Applicator Nodes

All applicators inherit from `AbstractApplicator`, wrap a single child, and propagate state down the tree.

### Geometry

| Applicator | Purpose | F# Builder |
|------------|---------|------------|
| `VertexAttributeApplicator` | Vertex data (positions, normals, etc.) | `Sg.vertexAttribute` |
| `VertexIndexApplicator` | Index buffer | `Sg.index` |
| `InstanceAttributeApplicator` | Per-instance data | `Sg.instanceAttribute` |

```fsharp
// Vertex attributes
sg |> Sg.vertexAttribute DefaultSemantic.Positions positions
   |> Sg.vertexAttribute DefaultSemantic.Normals normals
   |> Sg.index indices

// Instance attributes for GPU instancing
sg |> Sg.instanceAttribute DefaultSemantic.InstanceTrafo trafos
```

### Transformations

| Applicator | Purpose | F# Builder |
|------------|---------|------------|
| `TrafoApplicator` | Model transformation | `Sg.trafo`, `Sg.transform` |
| `ViewTrafoApplicator` | View transformation | `Sg.viewTrafo` |
| `ProjectionTrafoApplicator` | Projection transformation | `Sg.projTrafo` |

```fsharp
// Model transformation (adaptive)
sg |> Sg.trafo modelTrafo

// Static transformation
sg |> Sg.transform (Trafo3d.Translation(1.0, 2.0, 3.0))

// Camera setup
sg |> Sg.camera camera  // Sets both view and proj
```

### Rendering State

| Applicator | Purpose | F# Builder |
|------------|---------|------------|
| `SurfaceApplicator` | Shader effects | `Sg.shader`, `Sg.effect`, `Sg.surface` |
| `PassApplicator` | Render pass | `Sg.pass` |
| `UniformApplicator` | Shader uniforms | `Sg.uniform` |
| `TextureApplicator` | Texture uniforms | `Sg.texture`, `Sg.diffuseTexture` |

```fsharp
// Shader effects
sg |> Sg.effect [effect1; effect2]

// Uniforms
sg |> Sg.uniform "ModelColor" color
   |> Sg.uniform "Roughness" roughness

// Textures
sg |> Sg.diffuseTexture texture
   |> Sg.texture "NormalMap" normalMap
```

### Blend State

| Applicator | Purpose | F# Builder |
|------------|---------|------------|
| `BlendModeApplicator` | Global blend mode | `Sg.blendMode` |
| `BlendConstantApplicator` | Blend constant color | `Sg.blendConstant` |
| `ColorWriteMaskApplicator` | Color channel masking | `Sg.colorMask`, `Sg.colorWrite` |
| `AttachmentBlendModeApplicator` | Per-attachment blending | `Sg.blendModes` |
| `AttachmentColorWriteMaskApplicator` | Per-attachment masking | `Sg.colorMasks` |

```fsharp
// Global blending
sg |> Sg.blendMode' BlendMode.Blend

// Per-attachment
sg |> Sg.blendModes' (Map.ofList [
    DefaultSemantic.Colors, BlendMode.Blend
    sym"Glow", BlendMode.Add
])
```

### Depth State

| Applicator | Purpose | F# Builder |
|------------|---------|------------|
| `DepthTestApplicator` | Depth comparison | `Sg.depthTest` |
| `DepthWriteMaskApplicator` | Depth write enable | `Sg.depthWrite` |
| `DepthBiasApplicator` | Depth bias/offset | `Sg.depthBias` |
| `DepthClampApplicator` | Depth clamping | `Sg.depthClamp` |

```fsharp
sg |> Sg.depthTest' DepthTest.Less
   |> Sg.depthWrite' true
```

### Stencil State

| Applicator | Purpose | F# Builder |
|------------|---------|------------|
| `StencilModeFrontApplicator` | Front-face stencil | `Sg.stencilModeFront` |
| `StencilModeBackApplicator` | Back-face stencil | `Sg.stencilModeBack` |
| `StencilWriteMaskFrontApplicator` | Front-face write mask | `Sg.stencilWriteMaskFront` |
| `StencilWriteMaskBackApplicator` | Back-face write mask | `Sg.stencilWriteMaskBack` |

```fsharp
// Same mode for front and back
sg |> Sg.stencilMode' stencilMode
   |> Sg.stencilWrite' enabled
```

### Rasterizer State

| Applicator | Purpose | F# Builder |
|------------|---------|------------|
| `CullModeApplicator` | Face culling | `Sg.cullMode` |
| `FrontFacingApplicator` | Winding order | `Sg.frontFacing` |
| `FillModeApplicator` | Fill vs wireframe | `Sg.fillMode` |
| `MultisampleApplicator` | MSAA enable | `Sg.multisample` |
| `ConservativeRasterApplicator` | Conservative raster | `Sg.conservativeRaster` |

```fsharp
sg |> Sg.cullMode' CullMode.Back
   |> Sg.fillMode' FillMode.Fill
```

### Viewport and Scissor

| Applicator | Purpose | F# Builder |
|------------|---------|------------|
| `ViewportApplicator` | Viewport region | `Sg.viewport` |
| `ScissorApplicator` | Scissor region | `Sg.scissor` |

```fsharp
sg |> Sg.viewport (AVal.constant (Box2i(V2i.Zero, V2i(1920, 1080))))
   |> Sg.scissor' (Box2i(V2i(100, 100), V2i(800, 600)))
```

### Control Flow

| Applicator | Purpose | F# Builder |
|------------|---------|------------|
| `OnOffNode` | Conditional visibility | `Sg.onOff` |
| `DynamicNode` | Dynamic scene switching | `Sg.dynamic` |
| `ActivationApplicator` | Lifecycle callbacks | `Sg.onActivation` |

```fsharp
// Conditional rendering
sg |> Sg.onOff isVisible

// Dynamic scene graph
sg |> Sg.dynamic currentScene

// Activation hooks
sg |> Sg.onActivation (fun () ->
    // Called when render objects prepared
    { new IDisposable with
        member _.Dispose() = (* cleanup *) }
)
```

## Composition

### Combining Scenes

| Function | Purpose | Example |
|----------|---------|---------|
| `Sg.ofSeq` | Combine sequence | `Sg.ofSeq [sg1; sg2; sg3]` |
| `Sg.ofList` | Combine list | `Sg.ofList scenes` |
| `Sg.ofArray` | Combine array | `Sg.ofArray [|sg1; sg2|]` |
| `Sg.set` | From adaptive set | `Sg.set dynamicScenes` |
| `Sg.andAlso` | Combine two | `sg1 \|> Sg.andAlso sg2` |
| `Sg.empty` | Empty scene | `Sg.empty` |

```fsharp
// Static composition
let scene =
    Sg.ofList [
        sphere
        cube
        grid
    ]

// Adaptive composition
let dynamicScene =
    objects
    |> ASet.map createSceneGraph
    |> Sg.set
```

### Pipe-Forward Pattern

Scene graphs compose left-to-right using `|>`:

```fsharp
let scene =
    IndexedGeometry.unitCube
    |> Sg.ofIndexedGeometry
    |> Sg.shader {
        do! DefaultSurfaces.trafo
        do! DefaultSurfaces.constantColor C4f.Red
    }
    |> Sg.trafo modelTrafo
    |> Sg.cullMode' CullMode.Back
    |> Sg.blendMode' BlendMode.Blend
```

State accumulates as you pipe through applicators.

## See Also

- **SG-SEMANTICS.md** - Semantic system, FShade, custom applicators
- **SG-PATTERNS.md** - Usage patterns, gotchas

**Key Files**:
- `src/Aardvark.SceneGraph/Core/Core.fs` - ISg interfaces
- `src/Aardvark.SceneGraph/Core/Sg.fs` - Concrete node types
- `src/Aardvark.SceneGraph/SgFSharp.fs` - F# builder API
