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

## See Also

- **SG-CORE.md** - ISg hierarchy, applicators, composition
- **SG-SEMANTICS.md** - Semantic system, FShade, custom applicators
- **RENDER-PATTERNS.md** - Rendering usage patterns
