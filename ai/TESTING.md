# Testing

Use the shared target-aware test framework for tests in
`src/Tests/Aardvark.Rendering.Tests/Tests/`. It keeps CPU-only, backend-independent,
and backend-specific tests under one registration model.

## Test model

A test-group entry point has type `TestTarget -> Test`. `Program.fs` passes entry points
to `testAllTargets`, which instantiates them for every target:

- `Cpu`
- `GL`
- `GLFW`
- `OpenTK`
- `Vulkan`

The resulting names have the form `<category>.<target>.<group>.<case>`, for example
`Buffers.GL.Download.Array uint8` or `IndexedGeometry.Cpu.Operations.Clone.deep`.
Reserve `.` exclusively for group separators. A group or case name may use it to add
another level, never as ordinary punctuation or a decimal point. For example,
`Rotate by 3.1415` is an invalid name; use a descriptive name such as `Rotate by pi` and
put numeric parameter values in assertion or exception context.

## Choose the narrowest preparation helper

| Test scope | Helper | Active targets |
|------------|--------|----------------|
| CPU only | `prepareCasesCpu` | `Cpu` |
| Regular GPU backends | `prepareCasesGpu` | `GL`, `Vulkan` |
| Explicit target set | `prepareCasesFor` | Targets supplied by the caller |
| Mixed CPU/GPU cases | `prepareCases` | Cases selected by the entry point |
| Explicit targets, no runtime | `prepareCasesWithoutRuntimeFor` | Targets supplied by the caller |

`prepareCasesCpu` and `prepareCasesWithoutRuntimeFor` accept `unit -> unit`
implementations. The other helpers accept `IRuntime -> unit` implementations.

`prepareCasesWithoutRuntimeFor` selects targets like `prepareCasesFor` but runs the
cases without creating a test application. Use it for tests that need a backend to be
present on the machine but must not have a runtime alive while they run, such as the
Vulkan entry point tests in `Tests/Other/VulkanEntryPoints.fs`, which assert on the
state of the Vulkan loader. Do not use `prepareCasesCpu` for such tests: the CPU target
runs on machines without a GPU driver.

Use `target.IsGpu` for logic shared by the regular OpenGL and Vulkan targets. `GLFW` and
`OpenTK` are special OpenGL framework targets; use them only when testing behavior of
those frameworks, such as context creation.

For a mixed group, select cases in the list expression. CPU implementations in that list
must still have type `IRuntime -> unit`; use an ignored argument when the runtime is not
needed:

```fsharp
module private Cases =
    let cpuCase _ =
        // CPU-only assertions
        ()

    let gpuCase (runtime : IRuntime) =
        // Backend assertions
        ()

let tests (target : TestTarget) =
    [
        if target = TestTarget.Cpu then
            "CPU behavior", Cases.cpuCase

        if target.IsGpu then
            "GPU behavior", Cases.gpuCase
    ]
    |> prepareCases "Some group" target
```

The framework supplies a dummy `IRuntime` for `Cpu` when `prepareCases` is used. Calling
any runtime member on that dummy raises `NotSupportedException`; CPU cases must ignore it.

## File and module structure

A test group belongs to the category represented by the first folder below `Tests/` and
uses the matching namespace. For example, a file in `Tests/Texture/` uses:

```fsharp
namespace Aardvark.Rendering.Tests.Texture
```

A subfolder below a category may add a namespace segment with its name, and its groups
carry that name as a prefix. For example, the files in `Tests/Rendering/SceneGraph/` use
`namespace Aardvark.Rendering.Tests.Rendering.SceneGraph` and register groups such as
`SceneGraph.Picking`, discovered as `Rendering.Cpu.SceneGraph.Picking.<case>`.

Put test implementations in a `Cases` module. Another descriptive module is acceptable
when a large group has meaningful subgroups. Only actual test-case implementations should
be non-private within these modules; mark helper functions, fixtures, and other bindings
`private`, even when the module itself is private.

Keep the entry point at the bottom of the file. Align the name/implementation columns
for readability. Logical subgroups may be separated by a blank line and aligned
independently, as in `Tests/Texture/Upload.fs`:

```fsharp
module SomeGroup =

    module private Cases =
        let private samples = [1; 2]

        let private check value =
            // shared assertions
            ()

        let first () =
            for value in samples do check value

        let second () =
            check 3

        let boundary () =
            check 0

    let tests (target : TestTarget) =
        [
            "first behavior",  Cases.first
            "second behavior", Cases.second

            "boundary behavior", Cases.boundary
        ]
        |> prepareCasesCpu "Some group" target
```

Use explicit, descriptive literal names paired with named case implementations in the
registration list. Do not generate names or expand registrations with loops, `yield!`, or
case-list factories. Conditional target selection, as shown above, is still appropriate.
Keep equivalent builder/alias and parameter iteration inside the case implementations.

Do not inline test implementations in the registration list. Do not use Expecto
constructors such as `testList` or `testCase`, and do not add `[<Tests>]` attributes in
ordinary test files. `Program.fs` is the only registration point that uses `[<Tests>]`;
the shared framework in `Tests/Common.fs` owns the Expecto construction details.

## Cases and parameter matrices

Keep one discoverable case for one behavior. Exercise equivalent values, dimensions,
formats, or other parameters inside that case instead of publishing a separate test for
every value. Materially different setup or behavior may remain separate.

Parameter loops must remain readable. Prefer named data, helpers, and behavior-oriented
case functions over nested anonymous lambdas. When consolidating a matrix, include the
relevant parameter values in assertion or exception context so a failure still identifies
the failing input.

Temporary granular cases are fine while diagnosing a change, but consolidate equivalent
matrices before committing. Recheck the discovered names and count after consolidation so
no behavior disappears accidentally.

## Registration

`Program.fs` is the complete inventory. Add the group's `tests` entry point to the
appropriate category passed to `testAllTargets`; do not create a second registration
mechanism.

Representative examples:

- Mixed CPU/GPU selection: `Tests/Buffer/ManagedBuffer.fs`
- Parameterized cases: `Tests/Rendering/SceneGraph/Picking.fs`
- Dotted group/case names: `Tests/Texture/Compression.fs`

## Environment Variables

Library switches are read through the `EnvVar` registry (see DEBUG.md), which caches a
variable on first access. A test that needs a switch sets the registry property and resets
it to `null` in a `finally` block; `Environment.SetEnvironmentVariable` has no effect once
the variable has been read:

```fsharp
EnvVar.SOME_SWITCH <- "1"
try run ()
finally EnvVar.SOME_SWITCH <- null
```

## Leak Checks

The Vulkan backend counts live resources and descriptor sets device-wide in
`Resource.LiveCount` and `DescriptorSet.LiveCount` (`src/Aardvark.Rendering.Vulkan/Resources/`).
A leak test renders a few warm-up frames, records both counts, runs the per-frame path
under test and expects the counts to stay flat.

## Attribute-Grammar Rules

`TestApplication.init` makes the test assembly the introspection entry assembly before
`Aardvark.Init()`, so Ag rules and plugins of every assembly the test project references
(for example `Aardvark.Rendering.Text`) are discovered. A test that needs the rules of
another assembly only needs a project reference to it. Ag builds its rule table once per
process, so assemblies loaded later are not seen.

## Validation

Restore and run the test project with the repository commands:

```sh
dotnet tool restore
dotnet paket restore
dotnet test src/Tests/Aardvark.Rendering.Tests/Aardvark.Rendering.Tests.fsproj
```

Also build with `./build.sh` on Linux/macOS or `build.cmd` on Windows. Before publishing,
confirm the affected targets pass and that discovered names match the intended consolidated
case structure.

## Running a subset of tests

Besides `dotnet test`, the test project has its own runner in `Program.fs`. It runs tests
one after another on the current thread and prints a summary of passed, failed, and
skipped tests. Pass options after `--` (required for `--help`, which `dotnet run` would
otherwise handle itself):

```sh
dotnet run --project src/Tests/Aardvark.Rendering.Tests -- --target=cpu --filter=Buffers
```

| Option | Meaning |
|--------|---------|
| `--target=<target>` | Run only these targets: `cpu`, `gl`, `glfw`, `opentk`, `vulkan`. Leave it out to run every target. |
| `--filter=<string>` | Only run tests whose full name `<category>.<target>.<group>.<case>` contains `<string>`. |
| `--exclude=<string>` | Do not run tests whose full name contains `<string>`. |
| `--test=<name>` | Only run the test with exactly this full name. |
| `--stop-on-fail` | Stop at the first failing test. |
| `--list` | Print the full names of the selected tests and their count without running them. Use it to check a selection before a run. |
| `--help` | Print the options. |

Options with values can be repeated. A test runs if it matches at least one `--filter`
or `--test` (or neither is given) and no `--exclude`. `--filter`, `--exclude`, and
`--test` ignore case, and so does `--target`:

```sh
# Texture tests on GL and Vulkan, without the compression cases
dotnet run --project src/Tests/Aardvark.Rendering.Tests -- --target=gl --target=vulkan --filter=Textures --exclude=Compression
```

Arguments that take a value must use the `--name=value` form. Quote values that contain
spaces, e.g. `"--test=Buffers.GL.Download.Array uint8"`. The runner exits with an error
when it gets an unknown argument or target, or when the options select no tests (`--list`
still prints an empty list). It returns 0 when every test passed and 1
when any test failed.
