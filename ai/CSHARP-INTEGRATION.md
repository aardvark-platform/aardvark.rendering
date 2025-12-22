# C# Integration with Aardvark.Rendering

For C# projects using Aardvark.

## Package References

Required packages:

```
FSharp.Data.Adaptive
CSharp.Data.Adaptive
Aardvark.Base
Aardvark.Rendering
Aardvark.SceneGraph
```

## Adaptive Values in C#

F# uses `aval<'T>`, C# uses equivalent types from `CSharp.Data.Adaptive`:

```csharp
using FSharp.Data.Adaptive;

// Create mutable adaptive value
var position = new ChangeableValue<V3d>(V3d.Zero);

// Derive computed value
var transformed = position.Map(p => M44d.Translation(p));

// Read current value
V3d current = position.GetValue();

// Update (triggers scene refresh)
using (Adaptive.Transact())
{
    position.Value = new V3d(1, 0, 0);
}
```

### Adaptive Collections

```csharp
// Mutable set
var items = new ChangeableHashSet<string>();

// Add/remove in transaction
using (Adaptive.Transact())
{
    items.Add("item1");
    items.Remove("item2");
}

// Derive from set
var count = items.MapToValue(s => s.Count);
```

## Scene Graph in C#

Use extension methods from `Aardvark.SceneGraph`:

```csharp
using Aardvark.SceneGraph;
using Aardvark.Rendering;

var box = Sg.box(
    AVal.constant(C4b.Red),
    AVal.constant(Box3d.Unit)
);

var sg = box
    .Trafo(transform)
    .Shader(DefaultSurfaces.trafo)
    .Shader(DefaultSurfaces.vertexColor);

var task = sg.Compile(runtime, signature);
```

### Common Extension Methods

| Method | Purpose |
|--------|---------|
| `.Trafo(aval)` | Apply transformation |
| `.Shader(effect)` | Add shader effect |
| `.Uniform(name, aval)` | Set uniform value |
| `.DiffuseTexture(aval)` | Set diffuse texture |
| `.Compile(runtime, sig)` | Compile to render task |

## Interop with F# Rendering

C# application code can:
- Create adaptive values consumed by F# scene graphs
- Call F# rendering modules directly
- Use extension methods from `Aardvark.SceneGraph`

F# rendering code handles:
- Shader compilation (FShade)
- Low-level resource management
- Backend-specific optimizations

### Calling F# Modules

```csharp
// F# module functions are static methods
var result = SomeModule.someFunction(arg1, arg2);

// F# option types
var opt = FSharpOption<int>.Some(42);
if (FSharpOption<int>.get_IsSome(opt))
{
    int value = opt.Value;
}
```

## Threading Model

- Render thread: managed by application layer
- Adaptive updates: wrap in `Adaptive.Transact()`
- GPU resources: create on render thread or use runtime APIs

```csharp
// Safe cross-thread update
Application.Current.Dispatcher.Invoke(() =>
{
    using (Adaptive.Transact())
    {
        model.Value = newValue;
    }
});
```

## Common Issues

| Symptom | Fix |
|---------|-----|
| `FSharp.Core` version conflict | Ensure consistent version across projects |
| Missing extension methods | Add `using Aardvark.SceneGraph;` |
| Adaptive values not updating | Verify `Transact()` wraps changes |
| Null reference in F# interop | Use `FSharpOption` for optional values |

## See Also

- CONSUMER-PATTERNS.md - F# patterns (adaptable to C#)
- SG-CORE.md - Scene graph fundamentals
- RENDER-CORE.md - Runtime and resources
