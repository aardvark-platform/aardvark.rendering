# Scene Graph Semantics

Attribute grammar evaluation and shader integration for scene graphs.

## Semantic System

Scene graphs use **attribute grammars** (Aardvark.Base.Ag) for evaluation.

### Ag.Scope

`Ag.Scope` carries inherited and synthesized attributes down/up the tree:

| Attribute | Type | Direction | Purpose |
|-----------|------|-----------|---------|
| `ModelTrafo` | `aval<Trafo3d>` | Synthesized | Accumulated model transformation |
| `ViewTrafo` | `aval<Trafo3d>` | Inherited | View transformation |
| `ProjTrafo` | `aval<Trafo3d>` | Inherited | Projection transformation |
| `Surface` | `Surface` | Inherited | Shader effects |
| `Uniforms` | `list<IUniformProvider>` | Inherited | Shader uniform providers |
| `VertexAttributes` | `Map<Symbol, BufferView>` | Inherited | Vertex data |
| `InstanceAttributes` | `Map<Symbol, BufferView>` | Inherited | Instance data |
| `VertexIndexBuffer` | `Option<BufferView>` | Inherited | Index buffer |
| `FaceVertexCount` | `aval<int>` | Synthesized | Vertex count |
| `BlendMode`, `DepthTest`, etc. | Various | Inherited | Render state |

Source: `src/Aardvark.SceneGraph/Semantics/`

### Semantic Rules

Defined with `[<Rule>]` attribute:

```fsharp
[<Rule>]
type TrafoSem() =
    // Inherited: propagate trafo stack to child
    member x.ModelTrafoStack(t : Sg.TrafoApplicator, scope : Ag.Scope) =
        t.Child?ModelTrafoStack <- t.Trafo::scope.ModelTrafoStack

    // Synthesized: compute ModelTrafo from stack
    member x.ModelTrafo(e : obj, scope : Ag.Scope) =
        let stack = scope.ModelTrafoStack
        flattenStack stack
```

Source: `src/Aardvark.SceneGraph/Semantics/Trafo.fs`

### Dynamic Member Access

Use `?` operator to access semantic attributes:

```fsharp
// Get bounding box
let bb = sg?GlobalBoundingBox(Ag.Scope.Root) : aval<Box3d>

// Set child attribute
applicator.Child?ModelTrafoStack <- newStack
```

**Warning**: `?` bypasses type checking. Typos fail at runtime.

### RenderObjects Semantic

Primary semantic: converts ISg to IRenderObject:

```fsharp
[<Rule>]
type RenderObjectSem() =
    // IApplicator: delegate to child
    member x.RenderObjects(a : IApplicator, scope : Ag.Scope) : aset<IRenderObject> =
        aset {
            let! c = a.Child
            yield! c.RenderObjects(scope)
        }

    // IGroup: union all children
    member x.RenderObjects(g : IGroup, scope : Ag.Scope) : aset<IRenderObject> =
        aset {
            for c in g.Children do
                yield! c.RenderObjects(scope)
        }

    // RenderNode: create RenderObject from scope state
    member x.RenderObjects(r : Sg.RenderNode, scope : Ag.Scope) : aset<IRenderObject> =
        let rj = RenderObject.ofScope scope
        rj.DrawCalls <- DrawCalls.Direct r.DrawCallInfo
        rj.Mode <- r.Mode
        ASet.single (rj :> IRenderObject)
```

Source: `src/Aardvark.SceneGraph/Semantics/RenderObject.fs`

### Picking Semantic and Cache

`Sg.requirePicking` enables the `PickObjects` semantic. Triangle leaves derive an object-space
`Pickable` from their draw call, position/index buffer views, and topology. Building and indexing
that CPU geometry is expensive, so the semantic uses a process-wide structural memoization cache.

The important behavior is **shared-geometry reuse**, not object identity:

- independently constructed leaves with equivalent draw calls, buffer views, and topology reuse the
  same object-space picking representation
- repeated construction from shared `IndexedGeometry` can therefore reuse downloaded geometry and
  its acceleration structure
- model transformations are applied after this cache, so differently transformed instances can
  share the object-space representation
- different geometry, draw ranges, or topologies must not alias
- adaptive draw-call and buffer changes must continue to propagate through a reused entry

The current implementation is a static `Dictionary` using the structural equality of its complete
leaf key. Structurally unique entries are not evicted and normally remain until the scene-graph
assembly load context or process ends. This is a known implementation tradeoff and a potential TODO
pending evidence from realistic unload/reload workloads; permanent retention is not a public API
guarantee.

Do not replace the cache with a reference-identity lookup such as a directly keyed
`ConditionalWeakTable`: normal scene-graph constructors can create distinct adaptive wrappers and
`BufferView` objects that are structurally equal. Such a change silently turns shared-geometry hits
into misses. Any lifetime change must first preserve the representative reuse and non-aliasing
regressions and demonstrate a real retained-memory problem.

Source: `src/Aardvark.SceneGraph/HighLevelSceneGraph/Picking.fs`

## FShade Integration

### Effect Application

**Computation Expression**:

```fsharp
sg |> Sg.shader {
    do! DefaultSurfaces.trafo
    do! DefaultSurfaces.diffuseTexture
    do! DefaultSurfaces.simpleLighting
}
```

**Direct Effect Composition**:

```fsharp
let myEffect = FShade.Effect.compose [
    effect1
    effect2
    effect3
]

sg |> Sg.effect [myEffect]
```

**Effect Pool** (runtime selection):

```fsharp
let effects = [|
    unlitEffect
    litEffect
    wireframeEffect
|]

sg |> Sg.effectPool effects activeEffectIndex
```

Source: `src/Aardvark.SceneGraph/SgFSharp.fs:782-797`

### Surface Types

| Surface | Purpose |
|---------|---------|
| `Surface.Effect effect` | FShade effect |
| `Surface.None` | No shader |

Applied via `SurfaceApplicator`, propagates down tree.

### Shader Composition Order

Effects compose in order - **last writer wins**:

```fsharp
// color2 overrides color1
sg |> Sg.effect [
    colorEffect C4f.Red    // Overridden
    colorEffect C4f.Blue   // Active
]
```

### Post-Composition Effects

Use `AfterSg` to apply effects after child evaluation:

```fsharp
sg |> Sg.afterEffect [postProcessEffect]
```

Source: `src/Aardvark.SceneGraph/ShaderCompositions.fs:40`

## Custom Applicator Chaining

Custom applicators propagate state down the tree. Chain them to combine effects.

### Basic Custom Applicator

```fsharp
type StableViewProjTrafo(child : ISg, shadowViewProjTrafo : aval<Trafo3d>) =
    inherit Sg.AbstractApplicator(child)
    member x.ShadowViewProjTrafo = shadowViewProjTrafo

[<Rule>]
type StableTrafoSemantics() =
    member x.StableModelViewProjTexture(app : StableViewProjTrafo, scope : Ag.Scope) =
        let trafo =
            (scope.ModelTrafo, app.ShadowViewProjTrafo)
            ||> AVal.map2 (*)
        app.Child?StableModelViewProjTexture <- trafo
```

### Chaining Pattern

```fsharp
// Create applicators
let planetApplicator planet sg = PlanetApplicator(sg, planet) :> ISg
let imageApplicator images sg = ProjectedImageApplicator(sg, images) :> ISg

// Chain them
scene
|> planetApplicator currentPlanet
|> imageApplicator projectedImages
|> Sg.shader {
    do! planetShader       // Accesses planet attributes
    do! imageShader        // Accesses image attributes
}
```

**Scope flow**:
1. `PlanetApplicator` sets `scope?PlanetRadius`, `scope?PlanetCenter`
2. Child receives modified scope
3. `ProjectedImageApplicator` reads planet attributes, adds image attributes
4. Shader accesses both via `uniform.PlanetRadius`, `uniform.ImageTransform`

**Critical**: Always propagate inherited attributes to child:

```fsharp
[<Rule>]
type CustomSem() =
    member x.ModelTrafoStack(c : CustomApplicator, scope : Ag.Scope) =
        // MUST propagate to child!
        c.Child?ModelTrafoStack <- scope.ModelTrafoStack
```

Source: Attribute grammar semantics in `src/Aardvark.SceneGraph/Semantics/`

## See Also

- **SG-CORE.md** - ISg hierarchy, applicators, composition
- **SG-PATTERNS.md** - Usage patterns, gotchas

**Key Files**:
- `src/Aardvark.SceneGraph/Semantics/RenderObject.fs` - ISg to IRenderObject
- `src/Aardvark.SceneGraph/Semantics/Trafo.fs` - Transformation semantics
- `src/Aardvark.SceneGraph/ShaderCompositions.fs` - Effect composition
