# Consumer Integration Patterns

Patterns for projects that use Aardvark.Rendering as a dependency.

## Application Initialization

### Slim.GL (Headless)

When: Standalone viewers, CLI tools, offscreen rendering.

```fsharp
let app = new OpenGlApplication()
let win = app.CreateGameWindow(4)  // samples

let runtime = app.Runtime
let signature = runtime.CreateFramebufferSignature [
    DefaultSemantic.Colors, TextureFormat.Rgba8
    DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8
]

let sg = ... // scene graph
let task = sg |> Sg.compile runtime signature

win.RenderTask <- task
win.Run()
```

### Aardium (Desktop)

When: Web UI with native rendering backend.

```fsharp
let app = new OpenGlApplication()
let instance = app |> App.start

// Suave server for web UI
let webApp = ...
Aardium.run { ... }
```

### WinForms (Embedded)

When: Windows desktop apps with rendering panels.

```fsharp
let ctrl = new Aardvark.Application.WinForms.RenderControl()
ctrl.RenderTask <- task
form.Controls.Add(ctrl)
```

## Scene Graph Composition

Pipeline pattern:

```fsharp
let sg =
    Sg.ofList geometries
    |> Sg.trafo modelTrafo
    |> Sg.viewTrafo viewTrafo
    |> Sg.projTrafo projTrafo
    |> Sg.shader {
        do! DefaultSurfaces.trafo
        do! DefaultSurfaces.vertexColor
    }

let task = sg |> Sg.compile runtime signature
```

Multi-pass with effects:

```fsharp
let effects = [
    DefaultSurfaces.trafo
    DefaultSurfaces.diffuseTexture
    DefaultSurfaces.simpleLighting
]

let sg =
    geometry
    |> Sg.effect effects
    |> Sg.uniform "LightPosition" lightPos
```

## Adaptive Update Flow

1. Wrap state changes in `transact`:
   ```fsharp
   transact (fun () -> model.Value <- newModel)
   ```

2. Derived `aval` values automatically invalidate.

3. Scene graph applicators observe changes via adaptive bindings.

4. Next frame picks up updated values.

### Debugging Stale Scenes

| Symptom | Check |
|---------|-------|
| Scene not updating | Verify `transact` wraps mutations |
| Partial updates | Check adaptive dependency chain |
| Flickering | Look for competing transactions |

Debug current values:

```fsharp
let currentValue = AVal.force someAdaptiveValue
printfn "Current: %A" currentValue
```

## Custom Applicators

When built-in `Sg.*` combinators are insufficient:

```fsharp
type MyApplicator(child : aval<ISg>) =
    inherit Sg.AbstractApplicator(child)

    member x.MyAttribute = ...

[<Rule>]
type MySem() =
    member x.MyAttribute(app : MyApplicator, scope : Ag.Scope) =
        app.MyAttribute
```

Use `Ag.Scope` for attribute passing between nodes.

## Resource Management

Acquire/Release pattern for GPU resources:

```fsharp
let buffer = runtime.CreateBuffer(data)
// use buffer...
buffer.Dispose()  // explicit disposal required
```

Avoid `using` blocks in async code; call `Dispose` explicitly.

## See Also

- SG-CORE.md - Scene graph fundamentals
- SG-PATTERNS.md - Scene graph gotchas
- RENDER-PATTERNS.md - Rendering examples
- APPLICATION.md - Windowing details
