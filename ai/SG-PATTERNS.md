# Scene Graph Patterns

Usage patterns, shader features, and common pitfalls for scene graph development.

## Usage Patterns

### Primary Pattern: Dynamic Collections

Most complex scenes use **adaptive sets** to dynamically add/remove objects:

```fsharp
let bodies : cset<BodyDesc> = cset []

let scene =
    bodies
    |> ASet.map (fun body ->
        createSphere body.radius
        |> Sg.trafo body.transform
        |> Sg.uniform "Color" body.color
        |> Sg.texture DefaultSemantic.DiffuseColorTexture body.texture
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.diffuseTexture
        }
    )
    |> Sg.set
```

**Why this pattern**:
- Adding/removing bodies only updates affected scene graph nodes
- Each body's properties (transform, color, texture) can be adaptive
- Avoids rebuilding entire scene on changes

**Alternative (less efficient)**:
```fsharp
// AVOID: Rebuilds entire list on any change
let scene =
    bodies |> AVal.map (fun bodySet ->
        bodySet
        |> Seq.map createBodySg
        |> Sg.ofSeq
    )
    |> Sg.dynamic
```

### Basic Draw Call

```fsharp
let positions = [| V3f(-1,-1,0); V3f(1,-1,0); V3f(0,1,0) |]
let colors = [| C4b.Red; C4b.Green; C4b.Blue |]

let triangle =
    Sg.draw IndexedGeometryMode.TriangleList
    |> Sg.vertexAttribute' DefaultSemantic.Positions positions
    |> Sg.vertexAttribute' DefaultSemantic.Colors colors
    |> Sg.shader {
        do! DefaultSurfaces.trafo
        do! DefaultSurfaces.vertexColor
    }
```

### IndexedGeometry

```fsharp
let cube = IndexedGeometry.unitCube

let scene =
    Sg.ofIndexedGeometry cube
    |> Sg.trafo modelTrafo
    |> Sg.shader {
        do! DefaultSurfaces.trafo
        do! DefaultSurfaces.constantColor C4f.White
    }
```

### Instancing

```fsharp
// Per-instance transformations
let trafos = AVal.constant [|
    Trafo3d.Translation(1.0, 0.0, 0.0)
    Trafo3d.Translation(-1.0, 0.0, 0.0)
    Trafo3d.Translation(0.0, 1.0, 0.0)
|]

let instances =
    Sg.ofIndexedGeometry sphere
    |> Sg.shader { do! DefaultSurfaces.trafo }
    |> Sg.instancedGeometry trafos
```

Source: `src/Aardvark.SceneGraph/SgFSharp.fs:876`

### Dynamic Scenes

```fsharp
let currentMode = cval Wireframe

let scene =
    currentMode |> AVal.map (function
        | Wireframe -> wireframeScene
        | Shaded -> shadedScene
        | Textured -> texturedScene
    )
    |> Sg.dynamic
```

### Conditional Rendering

```fsharp
let isVisible = cval true

let scene =
    geometry
    |> Sg.shader { (* effects *) }
    |> Sg.onOff isVisible
```

### Transparent Objects

```fsharp
// marks the subtree's render objects for the transparency pass of the render task
let scene = Sg.ofList [ walls; glass |> Sg.transparent ]

// Sg.compile uses Transparency.Auto; Sg.compile' takes the technique explicitly
let task = Sg.compile' runtime signature Transparency.WeightedBlended scene
```

See RENDER-CORE.md (Transparency) for the techniques and the `AARDVARK_OIT` override.

### Adaptive Collections

```fsharp
let objects : cset<GameObject> = cset []

let scene =
    objects
    |> ASet.map (fun obj ->
        obj.Geometry
        |> Sg.trafo obj.Transform
        |> Sg.shader { (* effects *) }
    )
    |> Sg.set
```

### Custom Uniforms

```fsharp
// Scalar uniform
sg |> Sg.uniform "Time" time

// Typed uniform
sg |> Sg.uniform (TypedSymbol<V3d>("LightDirection")) lightDir

// Scope-dependent uniform (changes per scope)
sg |> Sg.scopeDependentTexture "ShadowMap" (fun scope ->
    let lightIndex = scope?LightIndex
    shadowMaps.[lightIndex]
)
```

Source: `src/Aardvark.SceneGraph/SgFSharp.fs:132-211`

## Storage Buffers in Shaders

Storage buffers (SSBOs) provide variable-length arrays in shaders. Access via `uniform?StorageBuffer?Name` syntax.

### Setup

```fsharp
// 1. Create storage buffer
let buffer : IBuffer = runtime.CreateBuffer(data)

// 2. Attach to scene graph
sg |> Sg.uniform "ProjectedImagesLocalTrafos" buffer
```

### Shader Access

```fsharp
type UniformScope with
    member x.StorageBuffer : Shader<Buffer<'T>> =
        uniform?StorageBuffer

type Vertex = { [<Position>] p : V4d }

let shaderWithStorageBuffer (v : Vertex) =
    fragment {
        let trafos = uniform.StorageBuffer?ProjectedImagesLocalTrafos
        let trafo = trafos.[index]
        // Use trafo...
        return v.p
    }
```

**Key points**:
- `uniform?StorageBuffer?Name` retrieves `Buffer<'T>`
- Index with `buffer.[i]` in shader code
- Buffer type must match shader usage
- No compile-time length checks

Source: FShade/Aardvark storage buffer conventions

## Unbounded Arrays in Shaders

A sampler array or storage-buffer array without a declared size is indexed with a non-uniform value in the shader and bound as one descriptor array (Vulkan with descriptor indexing only; MoltenVK and GL do not support it).

```fsharp
// sampler array without a declared size
let private textures =
    sampler2d {
        textureArray uniform?Textures -1
        filter Filter.MinMagMipLinear
    }

// storage-buffer array: one buffer per element
type UniformScope with
    member x.Geom : V4f[][] = uniform?StorageBuffer?Geom

// supplied as an array of buffers; textures as (int * aval<ITexture>)[] or amap<int, ITexture>
sg |> Sg.uniform' "Geom" (buffers : IBuffer[])
```

The capacity of such an array is `RuntimeConfig.UnboundedArrayCapacity` (default 4096, clamped to the device limit); binding a longer array fails. Descriptor sets are allocated at the number of elements actually used and grow on demand.

## Conditional Shader Features

Use boolean uniforms to toggle shader features at runtime.

### Pattern

```fsharp
// Uniform declaration
type UniformScope with
    member x.HasDiffuseTexture : bool = uniform?HasDiffuseTexture

// Shader code
let diffuseFragment (v : Vertex) =
    fragment {
        let color =
            if uniform.HasDiffuseTexture then
                diffuseSampler.Sample(v.tc)
            else
                V4d(1.0, 1.0, 1.0, 1.0)
        return color
    }

// Scene graph setup
sg |> Sg.texture' DefaultSemantic.DiffuseColorTexture texture
   |> Sg.uniform' "HasDiffuseTexture" true
```

**Efficiency**:
- Branches compile to shader variants (static branching if possible)
- No runtime cost when branch condition is uniform
- Prefer over multiple shader effects when features overlap

**Common use cases**:
- Optional textures (diffuse, normal, specular)
- Debug visualizations
- Feature toggles (shadows, lighting models)

## Specialization Constants

A uniform declared in the `SpecializationConstant` scope is baked into the compiled shader instead of being read from a buffer. Use it for values that are fixed per object and that should fold at compile time (array sizes, decoder selection, feature switches that must not branch). Supported types are the scalars `bool`, `int`, `uint32`, `float32` and `float` (double, requires `SupportsShaderFloat64`).

```fsharp
// Shader declaration: the SpecializationConstant scope marks the member
type UniformScope with
    member x.Layers : int = uniform?SpecializationConstant?Layers

// Supplied like any other uniform
sg |> Sg.uniform' "Layers" 4
```

**Semantics**:
- The value comes from the uniform provider by name, like push constants. An unset constant keeps the shader's default, which is zero. A value that cannot be written as the declared type throws when the object is prepared.
- Every distinct set of value sources (the adaptive values the provider returns) is a distinct pipeline, and a changed value recompiles the pipeline, so a specialization constant is far heavier than a uniform. Do not drive one from a per-frame value.
- **Vulkan**: the constants become SPIR-V specialization constants. The specialized pipeline is compiled on a background thread while the object renders through the unspecialized pipeline, i.e. with the defaults, until the compile finishes. This avoids a stall on the render thread but means the first frames after creation or after a value change show the defaults. Design the shader so that the defaults are acceptable, or poll the output in tests (see `Tests/Rendering/SpecializationConstants.fs`).
- **GL**: the scope is an ordinary uniform block. The values are plain uniforms, applied on the first frame; nothing is folded.

The GPU heap uses the mechanism for its per-attribute type ids (`HeapTid0..7`, `HeapTidIdx` in `src/Aardvark.SceneGraph/Heap/HeapPool.fs`): a concrete id folds the attribute decoder to one typed arm, the default 0 reads the id from the allocation header at runtime. Both produce the same pixels, so the asynchronous specialization is invisible there.

## Gotchas

| # | Issue | Fix |
|---|-------|-----|
| 1 | Duplicate AVal.map | Cache BufferView in `ConditionalWeakTable`, don't recreate on each call |
| 2 | Semantic Evaluation Order | Inherited (Uniforms, Surface) before Synthesized (ModelTrafo, BoundingBox) |
| 3 | Dynamic Member `?` Safety | Typos fail at runtime; use typed extension properties instead |
| 4 | Child Scope Propagation | Always set `child?Attribute <- scope.Attribute` in custom rules |
| 5 | RenderObject Mutation | Use `RenderObject.Clone(ro)` before modifying shared instances |
| 6 | Buffer Type Matching | Pattern match `ArrayBuffer`, `SingleValueBuffer`; don't assume type |
| 7 | Constant vs Adaptive | Use constant constructor for static, adaptive for changing values |
| 8 | ASet Referential Equality | Return child aset directly; `ASet.map id` breaks equality |
| 9 | DefaultSemantic Symbols | Use `DefaultSemantic.Positions` not `"Positions"` (catches typos) |
| 10 | Effect Composition Order | Last effect wins; use `Sg.afterEffect` for post-child overrides |
| 11 | Specialization Constant Defaults | Vulkan renders the shader defaults until the background compile finishes; make defaults acceptable or poll in tests |

## See Also

- **SG-CORE.md** - ISg hierarchy, applicators, composition
- **SG-SEMANTICS.md** - Semantic system, FShade, custom applicators
- **RENDER-PATTERNS.md** - Rendering usage patterns
