namespace Aardvark.Rendering.Tests.Raytracing

open System
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Raytracing
open Aardvark.Rendering.Tests
open FSharp.Data.Adaptive
open Expecto

module TraceTo =

    module private Cases =

        let private semantic = Sym.ofString "OutputBuffer"
        let private unexpected() = failwith "Unexpected runtime operation"
        [<MethodImpl(MethodImplOptions.NoInlining)>]
        let private throwTask (error : exn) : unit = raise error
        [<MethodImpl(MethodImplOptions.NoInlining)>]
        let private throwTarget (error : exn) : unit = raise error

        type private Texture(runtime : ITextureRuntime, size : V3i, dimension, format, levels, samples, id : int, events : ResizeArray<string>) =
            let mutable name = null
            member _.Dimensions = size
            member val Disposals = 0 with get, set
            interface IBackendTexture with
                member _.Runtime = runtime
                member _.Dimension = dimension
                member _.Format = format
                member _.Samples = samples
                member _.Count = 1
                member _.MipMapLevels = levels
                member x.Size =
                    Expect.equal x.Disposals 0 "Reading a retired trace target"
                    size
                member _.Handle = uint64 id
                member _.WantMipMaps = levels > 1
                member _.Name with get() = name and set value = name <- value
                member x.Dispose() =
                    events.Add(sprintf "texture %d dispose" id)
                    x.Disposals <- x.Disposals + 1

        type private Target(size : cval<V3i>, create : V3i -> Texture, events : ResizeArray<string>) =
            inherit AdaptiveResource<Texture>()
            let mutable handle : Texture option = None
            member val Acquires = 0 with get, set
            member val Releases = 0 with get, set
            member val Owners = 0 with get, set
            member val Creates = 0 with get, set
            member val Destroys = 0 with get, set
            member val CleanupError : exn option = None with get, set
            member val Tokens = ResizeArray<RenderToken>()
            member _.Handle = handle
            override x.Create() =
                x.Creates <- x.Creates + 1
                events.Add "target create"
            override x.Destroy() =
                x.Destroys <- x.Destroys + 1
                events.Add "target release"
                handle |> Option.iter (fun t -> (t :> IBackendTexture).Dispose())
                handle <- None
                match x.CleanupError with Some error -> throwTarget error | None -> ()
            override _.Compute(token, _) =
                let requested = size.GetValue token
                match handle with
                | Some current when current.Dimensions = requested -> current
                | old ->
                    old |> Option.iter (fun t -> (t :> IBackendTexture).Dispose())
                    let current = create requested
                    handle <- Some current
                    current
            interface IAdaptiveResource with
                member x.Acquire() =
                    x.Acquires <- x.Acquires + 1
                    x.Owners <- x.Owners + 1
                    x.Acquire()
                member x.Release() =
                    x.Releases <- x.Releases + 1
                    x.Owners <- x.Owners - 1
                    x.Release()
                member x.ReleaseAll() =
                    x.Owners <- 0
                    x.ReleaseAll()
                member x.GetValue(token, rt) =
                    x.Tokens.Add rt
                    x.GetValue(token, rt) :> obj
            interface IAdaptiveResource<Texture> with
                member x.GetValue(token, rt) =
                    x.Tokens.Add rt
                    x.GetValue(token, rt)

        type private Task(commands : alist<RaytracingCommand>, uniform : IAdaptiveValue option, events : ResizeArray<string>, error : unit -> exn option) =
            inherit AdaptiveObject()
            let values = AList.toAVal commands
            let mutable name = null
            let mutable textures : Texture[] = [||]
            do uniform |> Option.iter (fun input -> input.Acquire())
            member val Runs = 0 with get, set
            member val Disposals = 0 with get, set
            member val LastToken = RenderToken.Empty with get, set
            member val Sizes = ResizeArray<V3i>()
            member val AliveAfterUniformRelease = true with get, set
            interface IRaytracingTask with
                member _.Name with get() = name and set value = name <- value
                member _.Update(_, _) = unexpected()
                member x.Run(token, rt) =
                    x.EvaluateAlways token (fun token ->
                        x.Runs <- x.Runs + 1
                        x.LastToken <- rt
                        match values.GetValue(token) |> Seq.toArray with
                        | [| RaytracingCommand.TransformLayoutCmd(before, src, write)
                             RaytracingCommand.TraceRaysCmd size
                             RaytracingCommand.TransformLayoutCmd(after, write', dst) |] ->
                            Expect.isTrue (obj.ReferenceEquals(before.Texture, after.Texture)) "Command target identity"
                            Expect.equal (src, write, write', dst) (TextureLayout.ShaderRead, TextureLayout.ShaderWrite, TextureLayout.ShaderWrite, TextureLayout.ShaderRead) "Command layout forwarding"
                            let target = before.Texture :?> Texture
                            Expect.equal target.Disposals 0 "Dispatch target must be alive"
                            Expect.equal size target.Dimensions "Commands must consume the current target size"
                            textures <- [|target|]
                            x.Sizes.Add size
                        | other -> failtestf "Unexpected trace commands: %A" other)
                member x.Dispose() =
                    events.Add "task dispose start"
                    x.Disposals <- x.Disposals + 1
                    uniform |> Option.iter (fun input -> input.Release())
                    x.AliveAfterUniformRelease <- textures |> Array.forall (fun t -> t.Disposals = 0)
                    events.Add "task dispose end"
                    match error() with Some error -> throwTask error | None -> ()

        type private Runtime() =
            let events = ResizeArray<string>()
            let textures = ResizeArray<Texture>()
            let tasks = ResizeArray<Task>()
            member val TaskError : exn option = None with get, set
            member _.Events = events
            member _.Textures = textures
            member _.Tasks = tasks
            member x.Texture(size, dimension, format, levels, samples) =
                let texture = new Texture(x, size, dimension, format, levels, samples, textures.Count + 1, events)
                textures.Add texture
                texture
            interface IRaytracingRuntime with
                member _.SupportsRaytracing = true
                member _.SupportsPositionFetch = false
                member _.SupportsInvocationReorder = false
                member _.SupportsMicromaps = false
                member _.MaxRayRecursionDepth = 4
                member _.GetMaxMicromapSubdivisionLevel _ = unexpected()
                member x.CompileTrace(pipeline, commands) =
                    let uniform = pipeline.Uniforms.TryGetUniform(Ag.Scope.Root, semantic) |> ValueOption.toOption
                    let task = new Task(commands, uniform, events, fun () -> x.TaskError)
                    tasks.Add task
                    task :> IRaytracingTask
            interface IAccelerationStructureRuntime with
                member _.CreateAccelerationStructure(_, _) = unexpected()
                member _.TryUpdateAccelerationStructure(_, _) = unexpected()
                member _.PrepareMicromap _ = unexpected()
            interface ITextureRuntime with
                member x.CreateTexture(size, dimension, format, levels, samples) = x.Texture(size, dimension, format, levels, samples) :> IBackendTexture
                member _.CreateTextureArray(_, _, _, _, _, _) = unexpected()
                member _.PrepareTexture _ = unexpected()
                member _.Copy(_, _, _, _, _, _, _, _) = unexpected()
                member _.Blit(_, _, _, _) = unexpected()
                member _.CreateRenderbuffer(_, _, _) = unexpected()
                member _.CreateStreamingTexture _ = unexpected()
                member _.CreateSparseTexture<'T when 'T : unmanaged>(_, _, _, _, _, _, _) : ISparseTexture<'T> = unexpected()
                member _.GenerateMipMaps _ = unexpected()
                member _.Download(_, _, _, _, _) = unexpected()
                member _.Upload(_, _, _, _, _) = unexpected()
                member _.DownloadStencil(_, _, _, _, _) = unexpected()
                member _.DownloadDepth(_, _, _, _, _) = unexpected()
                member _.Clear(_, _) = unexpected()
                member _.CreateTextureView(_, _, _, _) = unexpected()
            interface IBufferRuntime with
                member _.PrepareBuffer(_, _, _) = unexpected()
                member _.CreateBuffer(_, _, _) = unexpected()
                member _.Upload(_, _, _, _, _) = unexpected()
                member _.Download(_, _, _, _) = unexpected()
                member _.DownloadAsync(_, _, _, _) = unexpected()
                member _.Copy(_, _, _, _, _, _) = unexpected()

        let private pipeline uniforms =
            { Effect = Unchecked.defaultof<_>; Scenes = Map.empty; Uniforms = uniforms; MaxRecursionDepth = AVal.constant 1 }

        type private Fixture(route : string, uniform : bool) =
            let runtime = Runtime()
            let size = cval (V3i(2, 3, 1))
            let source = Target(size, (fun size -> runtime.Texture(size, TextureDimension.Texture2D, TextureFormat.Rgba8, 1, 1)), runtime.Events)
            let typed = AdaptiveResource.cast<IBackendTexture> source
            let uniforms = if uniform then UniformProvider.ofList [semantic, source :> IAdaptiveValue] else UniformProvider.Empty
            let pipeline = pipeline uniforms
            let result =
                match route with
                | "concrete" -> (runtime :> IRaytracingRuntime).TraceTo(source, pipeline)
                | "interface" -> (runtime :> IRaytracingRuntime).TraceTo(typed, pipeline)
                | _ -> AdaptiveTraceResult(runtime, pipeline, typed)
            member _.Runtime = runtime
            member _.Source = source
            member _.Size = size
            member _.Result = result
            member _.Get(rt) = result.GetValue(AdaptiveToken.Top, rt) :?> Texture
            interface IDisposable with
                member _.Dispose() =
                    runtime.TaskError <- None
                    source.CleanupError <- None
                    try result.ReleaseAll() with _ -> ()
                    for task in runtime.Tasks do if task.Disposals = 0 then (task :> IRaytracingTask).Dispose()
                    (source :> IAdaptiveResource).ReleaseAll()
                    for texture in runtime.Textures do if texture.Disposals = 0 then (texture :> IBackendTexture).Dispose()

        let private routes = ["concrete"; "interface"; "direct"]
        let private sourceResource (fixture : Fixture) = fixture.Source :> IAdaptiveResource
        let private doneTextures context (runtime : Runtime) =
            for texture in runtime.Textures do Expect.equal texture.Disposals 1 (context + ": no surviving final target handle")
            for task in runtime.Tasks do Expect.equal task.Disposals 1 (context + ": task disposed once")

        let ownerLifetimes() =
            for route in routes do
                for uniform in [false; true] do
                    for owners in [1; 2; 3] do
                        for external in [0; 1; 2] do
                            for all in [false; true] do
                                use fixture = new Fixture(route, uniform)
                                let source = sourceResource fixture
                                for _ = 1 to external do source.Acquire()
                                for lifetime = 1 to 3 do
                                    let context = sprintf "route=%s, uniform=%b, owners=%d, external=%d, all=%b, lifetime=%d" route uniform owners external all lifetime
                                    for _ = 1 to owners do fixture.Result.Acquire()
                                    Expect.equal fixture.Runtime.Tasks.Count (lifetime - 1) (context + ": compilation remains lazy")
                                    let value = fixture.Get(RenderToken.Zero)
                                    let acquired = fixture.Source.Acquires
                                    for _ = 1 to 5 do Expect.isTrue (obj.ReferenceEquals(value, fixture.Get(RenderToken.Empty))) (context + ": cached identity")
                                    Expect.equal fixture.Source.Acquires acquired (context + ": no cached acquisition churn")
                                    if all then fixture.Result.ReleaseAll()
                                    else
                                        for remaining = owners - 1 downto 0 do
                                            fixture.Result.Release()
                                            if remaining > 0 then Expect.equal value.Disposals 0 (context + ": remaining result owner keeps target alive")
                                    fixture.Result.ReleaseAll()
                                    Expect.equal fixture.Source.Owners external (context + ": only result/task references released")
                                    Expect.equal value.Disposals (if external = 0 then 1 else 0) (context + ": external target owner respected")
                                    Expect.equal fixture.Source.Acquires (external + lifetime * (if uniform then 2 else 1)) (context + ": balanced active acquisition")
                                    Expect.equal fixture.Source.Releases (lifetime * (if uniform then 2 else 1)) (context + ": balanced active release")
                                    let task = fixture.Runtime.Tasks.[lifetime - 1]
                                    Expect.isTrue task.AliveAfterUniformRelease (context + ": target retained until task disposal completes")
                                for _ = 1 to external do source.Release()
                                doneTextures route fixture.Runtime
                                Expect.equal fixture.Source.Creates fixture.Source.Destroys (route + ": balanced source lifetimes")
            for route in routes do
                for uniform in [false; true] do
                    use fixture = new Fixture(route, uniform)
                    let source = sourceResource fixture
                    let uniforms = if uniform then UniformProvider.ofList [semantic, fixture.Source :> IAdaptiveValue] else UniformProvider.Empty
                    let other = AdaptiveTraceResult(fixture.Runtime, pipeline uniforms, AdaptiveResource.cast<IBackendTexture> fixture.Source)
                    let context = sprintf "route=%s, uniform=%b, independent results" route uniform
                    source.Acquire()
                    try
                        fixture.Result.Acquire()
                        fixture.Result.Acquire()
                        other.Acquire()
                        other.Acquire()
                        source.Release()
                        Expect.equal fixture.Source.Owners 2 (context + ": both results survive external release before evaluation")
                        let texture = fixture.Get(RenderToken.Zero)
                        Expect.isTrue (obj.ReferenceEquals(texture, other.GetValue(AdaptiveToken.Top))) (context + ": shared target identity")
                        fixture.Result.Release()
                        fixture.Result.Release()
                        Expect.equal texture.Disposals 0 (context + ": independent result still owns the target")
                        let acquisitions = fixture.Source.Acquires
                        for _ = 1 to 8 do other.GetValue(AdaptiveToken.Top) |> ignore
                        Expect.equal fixture.Source.Acquires acquisitions (context + ": surviving result has no cached acquisition churn")
                        other.Release()
                        Expect.equal texture.Disposals 0 (context + ": surviving result still has an owner")
                        other.ReleaseAll()
                        Expect.equal fixture.Source.Acquires fixture.Source.Releases (context + ": all shared references balanced")
                        Expect.equal fixture.Source.Owners 0 (context + ": no surviving owner")
                        Expect.isNone fixture.Source.Handle (context + ": final target retired")
                        doneTextures context fixture.Runtime
                    finally
                        other.ReleaseAll()

        let releaseBeforeEvaluation() =
            for route in routes do
                for owners in [1; 2] do
                    for all in [false; true] do
                        use fixture = new Fixture(route, false)
                        for lifetime = 1 to 3 do
                            for _ = 1 to owners do fixture.Result.Acquire()
                            if all then fixture.Result.ReleaseAll()
                            else for _ = 1 to owners do fixture.Result.Release()
                            Expect.equal fixture.Source.Acquires lifetime (route + ": each unevaluated lifetime retains target")
                            Expect.equal fixture.Source.Releases lifetime (route + ": each unevaluated lifetime releases target")
                            Expect.equal fixture.Source.Owners 0 (route + ": no retained owner")
                            Expect.isNone fixture.Source.Handle (route + ": no materialized target")
                            Expect.equal fixture.Runtime.Tasks.Count 0 (route + ": no compilation before evaluation")
                        fixture.Result.Acquire()
                        fixture.Get(RenderToken.Zero) |> ignore
                        fixture.Result.ReleaseAll()
                        doneTextures route fixture.Runtime

        let sizesAndTokens() =
            for route in routes do
                for uniform in [false; true] do
                    use fixture = new Fixture(route, uniform)
                    fixture.Result.Acquire()
                    for size in [V3i(2, 3, 1); V3i(4, 2, 1); V3i.One; V3i(2, 3, 1)] do
                        let previous = fixture.Source.Handle
                        transact (fun () -> fixture.Size.Value <- size)
                        let rt = RenderToken.Zero
                        let texture = fixture.Get(rt)
                        let task = fixture.Runtime.Tasks.[0]
                        Expect.equal texture.Dimensions size (route + ": adaptive target size")
                        Expect.equal task.Sizes.[task.Sizes.Count - 1] size (route + ": command size follows target")
                        Expect.isTrue (obj.ReferenceEquals(task.LastToken, rt)) (route + ": task render token")
                        Expect.isTrue (fixture.Source.Tokens |> Seq.exists (fun actual -> obj.ReferenceEquals(actual, rt))) (route + ": target render token")
                        match previous with
                        | Some old when not (obj.ReferenceEquals(old, texture)) -> Expect.equal old.Disposals 1 (route + ": old size retired once")
                        | _ -> ()
                        let runs, acquisitions, gets = task.Runs, fixture.Source.Acquires, fixture.Source.Tokens.Count
                        for _ = 1 to 8 do fixture.Get(RenderToken.Empty) |> ignore
                        Expect.equal (task.Runs, fixture.Source.Acquires, fixture.Source.Tokens.Count) (runs, acquisitions, gets) (route + ": cached reads have no work or ownership churn")
                        Expect.equal fixture.Runtime.Tasks.Count 1 (route + ": size changes do not recompile")
                    fixture.Result.ReleaseAll()
                    doneTextures route fixture.Runtime

        let disposalFailures() =
            for route in routes do
                for uniform in [false; true] do
                    for all in [false; true] do
                        for external, taskFails, targetFails in [0, true, false; 0, true, true; 0, false, true; 1, true, false] do
                            use fixture = new Fixture(route, uniform)
                            let context = sprintf "route=%s, uniform=%b, all=%b, external=%d, taskFails=%b, targetFails=%b" route uniform all external taskFails targetFails
                            if external > 0 then (sourceResource fixture).Acquire()
                            let taskError = InvalidOperationException("task disposal")
                            let targetError = InvalidOperationException("target release")
                            fixture.Result.Acquire()
                            fixture.Get(RenderToken.Zero) |> ignore
                            fixture.Runtime.Events.Clear()
                            fixture.Runtime.TaskError <- if taskFails then Some taskError else None
                            fixture.Source.CleanupError <- if targetFails then Some targetError else None
                            let actual =
                                try
                                    if all then fixture.Result.ReleaseAll() else fixture.Result.Release()
                                    None
                                with error -> Some error
                            let expected = if taskFails then taskError else targetError
                            Expect.isTrue (actual |> Option.exists (fun e -> obj.ReferenceEquals(e, expected))) (context + ": disposal exception identity and primary precedence")
                            Expect.stringContains expected.StackTrace (if taskFails then "throwTask" else "throwTarget") (context + ": original throw stack")
                            Expect.equal fixture.Source.Owners external (context + ": only the result's target reference is released after task failure")
                            fixture.Result.ReleaseAll()
                            if external > 0 then
                                Expect.equal fixture.Source.Handle.Value.Disposals 0 (context + ": external owner survives failed task disposal")
                                (sourceResource fixture).Release()
                            Expect.isNone fixture.Source.Handle (context + ": cleanup retired the final handle")
                            Expect.sequenceEqual (fixture.Runtime.Events |> Seq.filter (fun e -> e = "task dispose start" || e = "task dispose end" || e = "target release"))
                                ["task dispose start"; "task dispose end"; "target release"] (context + ": dispose task before releasing target")
                            doneTextures context fixture.Runtime

        let plainAndStandalone() =
            for constant in [false; true] do
                for direct in [false; true] do
                    let runtime = Runtime()
                    let first = runtime.Texture(V3i(2, 3, 1), TextureDimension.Texture2D, TextureFormat.Rgba8, 1, 1)
                    let second = runtime.Texture(V3i(4, 2, 1), TextureDimension.Texture2D, TextureFormat.Rgba8, 1, 1)
                    let change = cval (first :> IBackendTexture)
                    let input = if constant then AVal.constant (first :> IBackendTexture) else change :> aval<_>
                    let result = if direct then AdaptiveTraceResult(runtime, pipeline UniformProvider.Empty, input) else (runtime :> IRaytracingRuntime).TraceTo(input, pipeline UniformProvider.Empty)
                    try
                        result.Acquire()
                        Expect.equal runtime.Tasks.Count 0 "Plain aval compilation remains lazy"
                        Expect.isTrue (obj.ReferenceEquals(result.GetValue(AdaptiveToken.Top), first)) "Plain aval initial target"
                        if not constant then
                            transact (fun () -> change.Value <- second)
                            Expect.isTrue (obj.ReferenceEquals(result.GetValue(AdaptiveToken.Top), second)) "Plain aval replacement"
                        result.ReleaseAll()
                        Expect.equal (first.Disposals, second.Disposals) (0, 0) "Plain aval textures remain caller-owned"
                    finally
                        result.ReleaseAll()
                        for texture in runtime.Textures do if texture.Disposals = 0 then (texture :> IBackendTexture).Dispose()
            for adaptive in [false; true] do
                use fixture = new Fixture("concrete", false)
                let source = sourceResource fixture
                source.Acquire()
                let task =
                    if adaptive then (fixture.Runtime :> IRaytracingRuntime).CompileTraceToTexture(pipeline UniformProvider.Empty, fixture.Source)
                    else (fixture.Runtime :> IRaytracingRuntime).CompileTraceToTexture(pipeline UniformProvider.Empty, fixture.Source.GetValue(AdaptiveToken.Top) :> IBackendTexture)
                task.Run()
                task.Dispose()
                Expect.equal fixture.Source.Acquires 1 "Standalone compilation does not acquire target"
                Expect.equal fixture.Source.Releases 0 "Standalone disposal does not release target"
                Expect.equal fixture.Source.Owners 1 "Standalone target remains externally owned"
                source.Release()
                doneTextures "standalone" fixture.Runtime

        let sizeCreatedUniformTarget() =
            for all in [false; true] do
                let runtime = Runtime()
                let size = cval (V2i(2, 3))
                let result = (runtime :> IRaytracingRuntime).TraceTo2D(size, TextureFormat.Rgba8, semantic, pipeline UniformProvider.Empty)
                try
                    result.Acquire()
                    result.Acquire()
                    Expect.equal runtime.Tasks.Count 0 "Size-created task compilation remains lazy"
                    for next in [V2i(2, 3); V2i(4, 2); V2i.One] do
                        transact (fun () -> size.Value <- next)
                        let rt = RenderToken.Zero
                        let texture = result.GetValue(AdaptiveToken.Top, rt)
                        Expect.equal texture.Size next.XYI "Size-created target dimensions"
                        Expect.equal runtime.Tasks.Count 1 "Size changes reuse compiled task"
                        let count = runtime.Textures.Count
                        for _ = 1 to 8 do result.GetValue(AdaptiveToken.Top) |> ignore
                        Expect.equal runtime.Textures.Count count "Size-created cached reads do not allocate handles"
                    if all then result.ReleaseAll()
                    else
                        result.Release()
                        Expect.equal runtime.Textures.[runtime.Textures.Count - 1].Disposals 0 "Second result owner retains size-created target"
                        result.Release()
                    Expect.isTrue runtime.Tasks.[0].AliveAfterUniformRelease "Trace result keeps target after pipeline uniform releases its reference"
                    doneTextures "size-created" runtime
                finally
                    result.ReleaseAll()
                    for task in runtime.Tasks do if task.Disposals = 0 then (task :> IRaytracingTask).Dispose()
                    for texture in runtime.Textures do if texture.Disposals = 0 then (texture :> IBackendTexture).Dispose()

    let tests (target : TestTarget) =
        [
            "Target lifetimes and owner isolation",           Cases.ownerLifetimes
            "Release before evaluation and repeated lives",   Cases.releaseBeforeEvaluation
            "Cached reads size changes and render tokens",    Cases.sizesAndTokens
            "Disposal failures release target references",    Cases.disposalFailures
            "Plain aval and standalone compilation controls", Cases.plainAndStandalone
            "Size-created target and pipeline uniforms",      Cases.sizeCreatedUniformTarget
        ]
        |> prepareCasesCpu "TraceTo" target
