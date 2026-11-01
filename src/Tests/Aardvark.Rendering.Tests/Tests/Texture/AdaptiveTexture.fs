namespace Aardvark.Rendering.Tests.Texture

open System
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open FSharp.Data.Adaptive
open Expecto

module AdaptiveTexture =

    module private Cases =

        [<MethodImpl(MethodImplOptions.NoInlining)>]
        let private throwNaming (error : exn) : unit = raise error

        type private Parameters =
            { IsArray : bool
              Size : V3i
              Dimension : TextureDimension
              Format : TextureFormat
              Levels : int
              Samples : int
              Count : int }

        type private Texture(runtime : ITextureRuntime, id : int, parameters : Parameters,
                             onName : unit -> unit, onDispose : bool -> unit) =
            let mutable disposed = false
            let mutable disposalCalls = 0
            let mutable name = null
            let mutable named = false
            let mutable namingCalls = 0

            member _.Parameters = parameters
            member _.IsDisposed = disposed
            member _.DisposalCalls = disposalCalls
            member _.WasNamed = named
            member _.NamingCalls = namingCalls

            interface IBackendTexture with
                member _.Runtime = runtime
                member _.Dimension = parameters.Dimension
                member _.Format = parameters.Format
                member _.Samples = parameters.Samples
                member _.Count = parameters.Count
                member _.MipMapLevels = parameters.Levels
                member _.Size = parameters.Size
                member _.Handle = uint64 id
                member _.WantMipMaps = parameters.Levels > 1
                member _.Name
                    with get() = name
                    and set value =
                        Expect.isFalse disposed "Naming a retired texture"
                        namingCalls <- namingCalls + 1
                        onName()
                        name <- value
                        named <- true
                member _.Dispose() =
                    disposalCalls <- disposalCalls + 1
                    onDispose named
                    disposed <- true

        let private unexpected() = failwith "Unexpected runtime operation"

        type private Runtime() =
            let requests = ResizeArray<Parameters>()
            let textures = ResizeArray<Texture>()

            member val CreationError : exn option = None with get, set
            member val DisposalError : exn option = None with get, set
            member val NamingError : exn option = None with get, set
            member val NamingCleanupError : exn option = None with get, set
            member _.Requests = requests
            member _.Textures = textures

            member private this.Create(parameters : Parameters) =
                requests.Add parameters
                Expect.isTrue (textures |> Seq.forall (fun t -> not t.WasNamed || t.IsDisposed))
                    "Replacement creation was attempted before retiring the previously published texture"
                match this.CreationError with
                | Some error -> raise error
                | None ->
                    let texture =
                        new Texture(this, textures.Count + 1, parameters,
                            (fun () ->
                                match this.NamingError with
                                | Some error -> throwNaming error
                                | None -> ()),
                            (fun named ->
                                let error = if not named && this.NamingCleanupError.IsSome then this.NamingCleanupError else this.DisposalError
                                match error with
                                | Some error -> raise error
                                | None -> ()))
                    textures.Add texture
                    texture :> IBackendTexture

            interface ITextureRuntime with
                member this.CreateTexture(size, dimension, format, levels, samples) =
                    this.Create { IsArray = false; Size = size; Dimension = dimension; Format = format
                                  Levels = levels; Samples = samples; Count = 1 }
                member this.CreateTextureArray(size, dimension, format, levels, samples, count) =
                    this.Create { IsArray = true; Size = size; Dimension = dimension; Format = format
                                  Levels = levels; Samples = samples; Count = count }
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

        type private Fixture(isArray : bool) =
            let runtime = Runtime()
            let size = cval (V3i(8, 8, 1))
            let levels = cval 1
            let samples = cval 1
            let count = cval 2
            let token = RenderToken.Zero
            let resource =
                let runtime = runtime :> ITextureRuntime
                if isArray then
                    runtime.CreateTextureArray(size, TextureDimension.Texture2D, TextureFormat.Rgba8,
                                               levels = levels, samples = samples, count = count)
                else
                    runtime.CreateTexture(size, TextureDimension.Texture2D, TextureFormat.Rgba8,
                                          levels = levels, samples = samples)
            do resource.Acquire()

            member _.Runtime = runtime
            member _.Resource = resource
            member _.Token = token
            member _.Get() = resource.GetValue(AdaptiveToken.Top, token)
            member _.Parameters =
                { IsArray = isArray; Size = size.Value; Dimension = TextureDimension.Texture2D
                  Format = TextureFormat.Rgba8; Levels = levels.Value; Samples = samples.Value
                  Count = if isArray then count.Value else 1 }

            member _.Change() =
                transact (fun () ->
                    size.Value <- V3i(16, 8, 1)
                    levels.Value <- 2
                    count.Value <- 3)

            member _.Restore() =
                transact (fun () ->
                    size.Value <- V3i(8, 8, 1)
                    levels.Value <- 1
                    samples.Value <- 1
                    count.Value <- 2)

            member _.ChangeSamples() =
                transact (fun () ->
                    levels.Value <- 1
                    samples.Value <- 2)

            interface IDisposable with
                member _.Dispose() =
                    runtime.CreationError <- None
                    runtime.DisposalError <- None
                    runtime.NamingError <- None
                    runtime.NamingCleanupError <- None
                    resource.ReleaseAll()

        let private checkLive context (fixture : Fixture) (handle : IBackendTexture) =
            let texture = handle :?> Texture
            Expect.isFalse texture.IsDisposed $"{context}: returned a retired texture"
            Expect.equal texture.Parameters fixture.Parameters $"{context}: incorrect creation parameters"
            Expect.isTrue (obj.ReferenceEquals(handle.Runtime, fixture.Resource.Runtime)) $"{context}: incorrect runtime"
            Expect.equal handle.Name fixture.Resource.Name $"{context}: name was not preserved"
            texture

        let private expectFailure context (expected : exn) action =
            let actual =
                try action(); None
                with error -> Some error
            match actual with
            | Some error -> Expect.isTrue (obj.ReferenceEquals(error, expected)) $"{context}: exception identity changed ({error})"
            | None -> failtestf "%s: expected resource failure" context

        let private expectNamingFailure context (expected : exn) action =
            expectFailure context expected action
            Expect.stringContains expected.StackTrace "throwNaming" (context + ": original naming stack retained")

        let private prepareReplacement context replacement (fixture : Fixture) =
            if replacement then
                let previous = fixture.Get() |> checkLive context fixture
                fixture.Change()
                Some previous
            else None

        let private checkRetired context previous =
            previous |> Option.iter (fun (texture : Texture) ->
                Expect.isTrue texture.IsDisposed (context + ": published predecessor retired")
                Expect.equal texture.DisposalCalls 1 (context + ": predecessor retired exactly once"))

        let normalReplacement() =
            for isArray in [false; true] do
                let context = $"array = {isArray}"
                use fixture = new Fixture(isArray)
                Expect.equal fixture.Runtime.Requests.Count 0 $"{context}: Acquire should not allocate a backend texture"
                fixture.Resource.Name <- "initial"
                let first = fixture.Get()
                let retired = checkLive context fixture first
                Expect.equal fixture.Token.TotalCreatedResources 1 $"{context}: initial creation statistics"

                for read in 1 .. 8 do
                    Expect.isTrue (obj.ReferenceEquals(first, fixture.Get())) $"{context}, read = {read}: cached handle changed"
                transact (fun () -> fixture.Resource.MarkOutdated())
                Expect.isTrue (obj.ReferenceEquals(first, fixture.Get())) $"{context}: unchanged parameters caused replacement"
                Expect.equal fixture.Runtime.Requests.Count 1 $"{context}: unchanged reads allocated"
                Expect.equal retired.DisposalCalls 0 $"{context}: unchanged reads disposed the texture"
                Expect.equal fixture.Token.TotalReplacedResources 0 $"{context}: unchanged reads changed statistics"

                fixture.Resource.Name <- "replacement"
                Expect.equal first.Name "replacement" $"{context}: live name update was lost"
                fixture.Change()
                let second = fixture.Get()
                let replaced = checkLive context fixture second
                Expect.isFalse (obj.ReferenceEquals(first, second)) $"{context}: replacement reused the retired handle"
                Expect.equal retired.DisposalCalls 1 $"{context}: previous texture not disposed once"

                fixture.ChangeSamples()
                let third = fixture.Get() |> checkLive context fixture
                Expect.equal replaced.DisposalCalls 1 $"{context}: sample change did not retire its texture"
                Expect.equal fixture.Token.TotalCreatedResources 1 $"{context}: replacement counted as creation"
                Expect.equal fixture.Token.TotalReplacedResources 2 $"{context}: replacement statistics changed"
                fixture.Resource.Release()
                Expect.equal third.DisposalCalls 1 $"{context}: final release did not dispose current texture"

        let replacementFailureRetry() =
            for isArray in [false; true] do
                for failures in [1; 3] do
                    let context = $"array = {isArray}, failures = {failures}"
                    use fixture = new Fixture(isArray)
                    fixture.Resource.Name <- "retry"
                    let first = fixture.Get()
                    let retired = checkLive context fixture first
                    fixture.Change()

                    for attempt in 1 .. failures do
                        let error = InvalidOperationException($"replacement attempt {attempt}")
                        fixture.Runtime.CreationError <- Some error
                        expectFailure $"{context}, attempt = {attempt}" error (fun () -> fixture.Get() |> ignore)
                        Expect.isTrue retired.IsDisposed $"{context}: previous texture was not retired"
                        Expect.equal retired.DisposalCalls 1 $"{context}, attempt = {attempt}: retired texture disposed again"

                    fixture.Runtime.CreationError <- None
                    let recovered = fixture.Get()
                    let current = checkLive context fixture recovered
                    Expect.isFalse (obj.ReferenceEquals(first, recovered)) $"{context}: retry returned the retired handle"
                    Expect.equal retired.DisposalCalls 1 $"{context}: retry disposed the retired handle again"
                    Expect.equal fixture.Runtime.Requests.Count (failures + 2) $"{context}: incorrect creation attempt count"
                    Expect.equal fixture.Runtime.Textures.Count 2 $"{context}: failed attempts created a texture"
                    fixture.Resource.Release()
                    Expect.equal current.DisposalCalls 1 $"{context}: recovered texture not released"
                    Expect.equal retired.DisposalCalls 1 $"{context}: release revisited the retired handle"

        let restoredParameters() =
            for isArray in [false; true] do
                let context = $"array = {isArray}"
                use fixture = new Fixture(isArray)
                let first = fixture.Get()
                let retired = checkLive context fixture first
                fixture.Change()
                let error = InvalidOperationException("replacement")
                fixture.Runtime.CreationError <- Some error
                expectFailure context error (fun () -> fixture.Get() |> ignore)

                fixture.Restore()
                fixture.Runtime.CreationError <- None
                let recovered = fixture.Get()
                checkLive context fixture recovered |> ignore
                Expect.isFalse (obj.ReferenceEquals(first, recovered)) $"{context}: restoring parameters resurrected the retired handle"
                Expect.equal retired.DisposalCalls 1 $"{context}: restoring parameters disposed the old handle again"
                Expect.equal fixture.Runtime.Requests.Count 3 $"{context}: restoring parameters skipped fresh creation"

        let releaseAfterFailure() =
            for isArray in [false; true] do
                for releaseAll in [false; true] do
                    let context = $"array = {isArray}, ReleaseAll = {releaseAll}"
                    use fixture = new Fixture(isArray)
                    if releaseAll then fixture.Resource.Acquire()
                    let retired = fixture.Get() |> checkLive context fixture
                    fixture.Change()
                    let error = InvalidOperationException("replacement")
                    fixture.Runtime.CreationError <- Some error
                    expectFailure context error (fun () -> fixture.Get() |> ignore)

                    if releaseAll then fixture.Resource.ReleaseAll()
                    else fixture.Resource.Release()
                    Expect.equal retired.DisposalCalls 1 $"{context}: final release disposed a retired texture again"
                    Expect.equal fixture.Runtime.Requests.Count 2 $"{context}: release attempted creation"
                    fixture.Resource.ReleaseAll()
                    Expect.equal retired.DisposalCalls 1 $"{context}: repeated ReleaseAll disposed a retired texture"

        let initialFailure() =
            for isArray in [false; true] do
                let context = $"array = {isArray}"
                use fixture = new Fixture(isArray)
                let error = InvalidOperationException("initial creation")
                fixture.Runtime.CreationError <- Some error
                expectFailure context error (fun () -> fixture.Get() |> ignore)
                Expect.equal fixture.Runtime.Textures.Count 0 $"{context}: failed initial creation published a texture"
                fixture.Resource.ReleaseAll()

                fixture.Runtime.CreationError <- None
                fixture.Resource.Acquire()
                let current = fixture.Get() |> checkLive context fixture
                Expect.equal fixture.Runtime.Requests.Count 2 $"{context}: reacquisition did not retry creation"
                fixture.Resource.Release()
                Expect.equal current.DisposalCalls 1 $"{context}: recovered initial texture not released"

        let ownershipAndReacquisition() =
            for isArray in [false; true] do
                let context = $"array = {isArray}"
                use fixture = new Fixture(isArray)
                fixture.Resource.Acquire()
                let first = fixture.Get()
                let previous = checkLive context fixture first
                fixture.Resource.Release()
                Expect.equal previous.DisposalCalls 0 $"{context}: release ignored another owner"
                Expect.isTrue (obj.ReferenceEquals(first, fixture.Get())) $"{context}: remaining owner lost its handle"
                fixture.Resource.Acquire()
                fixture.Resource.ReleaseAll()
                fixture.Resource.ReleaseAll()
                Expect.equal previous.DisposalCalls 1 $"{context}: ReleaseAll did not retire once"

                fixture.Resource.Name <- "reacquired"
                fixture.Resource.Acquire()
                let second = fixture.Get()
                let current = checkLive context fixture second
                Expect.isFalse (obj.ReferenceEquals(first, second)) $"{context}: reacquisition returned an old handle"
                fixture.Resource.Release()
                Expect.equal current.DisposalCalls 1 $"{context}: final owner did not release its handle"
                Expect.equal fixture.Token.TotalCreatedResources 2 $"{context}: reacquisition statistics"
                Expect.equal fixture.Token.TotalReplacedResources 0 $"{context}: reacquisition counted as replacement"

        let disposalFailure() =
            for isArray in [false; true] do
                let context = $"array = {isArray}"
                use fixture = new Fixture(isArray)
                let first = fixture.Get()
                let previous = checkLive context fixture first
                fixture.Change()
                let error = InvalidOperationException("disposal")
                fixture.Runtime.DisposalError <- Some error
                expectFailure context error (fun () -> fixture.Get() |> ignore)
                Expect.isFalse previous.IsDisposed $"{context}: failed disposal lost the live texture"
                Expect.equal fixture.Runtime.Requests.Count 1 $"{context}: replacement ran after failed disposal"

                fixture.Runtime.DisposalError <- None
                let replacement = fixture.Get()
                checkLive context fixture replacement |> ignore
                Expect.isTrue previous.IsDisposed $"{context}: disposal was not retried"
                Expect.equal previous.DisposalCalls 2 $"{context}: failed disposal was forgotten"
                Expect.equal fixture.Runtime.Requests.Count 2 $"{context}: replacement creation was not retried"

        let namingFailureRetry() =
            for isArray in [false; true] do
                for replacement in [false; true] do
                    for failures in [1; 3] do
                        for name in [null; "named resource"] do
                            let context = $"array={isArray}, replacement={replacement}, failures={failures}, named={not (isNull name)}"
                            use fixture = new Fixture(isArray)
                            fixture.Resource.Name <- name
                            let previous = prepareReplacement context replacement fixture
                            for attempt in 1 .. failures do
                                let context = $"{context}, attempt={attempt}"
                                let error = InvalidOperationException($"name attempt {attempt}")
                                fixture.Runtime.NamingError <- Some error
                                expectNamingFailure context error (fun () -> fixture.Get() |> ignore)
                                let failed = fixture.Runtime.Textures |> Seq.last
                                Expect.equal failed.NamingCalls 1 (context + ": naming attempted once")
                                Expect.isFalse failed.WasNamed (context + ": failed name not accepted")
                                Expect.equal failed.DisposalCalls 1 (context + ": unpublished texture cleaned once")
                                Expect.isTrue failed.IsDisposed (context + ": unpublished texture leaked")
                                Expect.isTrue fixture.Resource.OutOfDate (context + ": failed evaluation permits retry")
                                checkRetired context previous
                                Expect.equal fixture.Token.TotalCreatedResources attempt (context + ": creation statistics retain attempt semantics")
                                Expect.equal fixture.Token.TotalReplacedResources (if replacement then 1 else 0) (context + ": replacement statistics")

                            fixture.Runtime.NamingError <- None
                            let handle = fixture.Get()
                            let current = checkLive context fixture handle
                            Expect.equal current.NamingCalls 1 (context + ": name assigned before publication")
                            let attempts = failures + (if replacement then 2 else 1)
                            Expect.equal fixture.Runtime.Requests.Count attempts (context + ": retry without input change creates a fresh handle")
                            Expect.equal fixture.Runtime.Textures.Count attempts (context + ": each naming failure had an allocated handle")
                            for _ in 1 .. 8 do
                                Expect.isTrue (obj.ReferenceEquals(fixture.Get(), handle)) (context + ": successful retry remains cached")
                            Expect.equal current.NamingCalls 1 (context + ": cached reads do not rename")
                            Expect.equal fixture.Token.TotalCreatedResources (failures + 1) (context + ": successful retry creation statistics")
                            fixture.Resource.Release()
                            Expect.isTrue (fixture.Runtime.Textures |> Seq.forall (fun t -> t.IsDisposed && t.DisposalCalls = 1))
                                (context + ": every allocated texture retired exactly once")

        let releaseAfterNamingFailure() =
            for isArray in [false; true] do
                for replacement in [false; true] do
                    for releaseAll in [false; true] do
                        for owners in [1; 2] do
                            let context = $"array={isArray}, replacement={replacement}, ReleaseAll={releaseAll}, owners={owners}"
                            use fixture = new Fixture(isArray)
                            for _ in 2 .. owners do fixture.Resource.Acquire()
                            fixture.Resource.Name <- "release after failure"
                            let previous = prepareReplacement context replacement fixture
                            for attempt in 1 .. 2 do
                                let context = $"{context}, attempt={attempt}"
                                let error = InvalidOperationException($"name attempt {attempt}")
                                fixture.Runtime.NamingError <- Some error
                                expectNamingFailure context error (fun () -> fixture.Get() |> ignore)
                            let requests = fixture.Runtime.Requests.Count
                            let stats = fixture.Token.TotalCreatedResources, fixture.Token.TotalReplacedResources
                            if releaseAll then fixture.Resource.ReleaseAll()
                            else for _ in 1 .. owners do fixture.Resource.Release()
                            fixture.Resource.ReleaseAll()
                            fixture.Resource.ReleaseAll()
                            checkRetired context previous
                            Expect.equal fixture.Runtime.Requests.Count requests (context + ": release does not allocate")
                            Expect.equal (fixture.Token.TotalCreatedResources, fixture.Token.TotalReplacedResources) stats (context + ": release statistics unchanged")
                            Expect.isTrue (fixture.Runtime.Textures |> Seq.forall (fun t -> t.IsDisposed && t.DisposalCalls = 1))
                                (context + ": release does not revisit unpublished or retired handles")
                            fixture.Runtime.NamingError <- None
                            fixture.Resource.Acquire()
                            let current = fixture.Get() |> checkLive context fixture
                            fixture.Resource.Release()
                            Expect.equal current.DisposalCalls 1 (context + ": reacquired resource releases normally")
                            Expect.isTrue (fixture.Runtime.Textures |> Seq.forall (fun t -> t.DisposalCalls = 1)) (context + ": no duplicate cleanup after reacquisition")

        let namingAndCleanupFailure() =
            for isArray in [false; true] do
                for replacement in [false; true] do
                    for failures in [1; 3] do
                        let context = $"array={isArray}, replacement={replacement}, failures={failures}, cleanup throws"
                        use fixture = new Fixture(isArray)
                        fixture.Resource.Name <- "secondary cleanup failure"
                        let previous = prepareReplacement context replacement fixture
                        fixture.Runtime.NamingCleanupError <- Some (ApplicationException("cleanup"))
                        for attempt in 1 .. failures do
                            let context = $"{context}, attempt={attempt}"
                            let error = InvalidOperationException($"primary name attempt {attempt}")
                            fixture.Runtime.NamingError <- Some error
                            expectNamingFailure context error (fun () -> fixture.Get() |> ignore)
                            let failed = fixture.Runtime.Textures |> Seq.last
                            Expect.equal failed.DisposalCalls 1 (context + ": failing cleanup attempted exactly once")
                            Expect.isFalse failed.IsDisposed (context + ": backend cleanup failure is not successful disposal")
                            checkRetired context previous
                        let abandoned = fixture.Runtime.Textures |> Seq.filter (fun t -> not t.WasNamed) |> Seq.toArray
                        fixture.Runtime.NamingError <- None
                        fixture.Runtime.NamingCleanupError <- None
                        let current = fixture.Get() |> checkLive context fixture
                        Expect.equal current.NamingCalls 1 (context + ": retry publishes only the newly named handle")
                        fixture.Resource.ReleaseAll()
                        fixture.Resource.ReleaseAll()
                        Expect.equal current.DisposalCalls 1 (context + ": successful retry released once")
                        for failed in abandoned do
                            Expect.equal failed.DisposalCalls 1 (context + ": unpublished failing cleanup is not retried by release")

        let liveNamingFailure() =
            for isArray in [false; true] do
                let context = $"array={isArray}, live rename"
                use fixture = new Fixture(isArray)
                fixture.Resource.Name <- "initial"
                let handle = fixture.Get()
                let texture = checkLive context fixture handle
                let error = InvalidOperationException("live rename")
                fixture.Runtime.NamingError <- Some error
                expectNamingFailure context error (fun () -> fixture.Resource.Name <- "attempted")
                Expect.equal fixture.Resource.Name "attempted" (context + ": requested name retained")
                Expect.equal handle.Name "initial" (context + ": backend retains its previous name")
                Expect.isTrue (obj.ReferenceEquals(fixture.Get(), handle)) (context + ": published handle remains cached")
                Expect.equal texture.DisposalCalls 0 (context + ": live naming failure does not delete published ownership")
                fixture.Runtime.NamingError <- None
                fixture.Resource.Name <- "recovered"
                checkLive context fixture handle |> ignore
                Expect.equal texture.NamingCalls 3 (context + ": initial, failed and recovered assignments")
                Expect.equal fixture.Runtime.Requests.Count 1 (context + ": rename does not recreate")
                Expect.equal fixture.Token.TotalCreatedResources 1 (context + ": creation statistics")
                Expect.equal fixture.Token.TotalReplacedResources 0 (context + ": replacement statistics")
                fixture.Resource.Release()
                Expect.equal texture.DisposalCalls 1 (context + ": final release retains ownership")

    let tests (target : TestTarget) =
        [
            "Normal replacement and cached reads",       Cases.normalReplacement
            "Replacement failures permit retry",         Cases.replacementFailureRetry
            "Restored parameters create a live texture", Cases.restoredParameters
            "Final release after failed creation",       Cases.releaseAfterFailure
            "Initial failure and reacquisition",         Cases.initialFailure
            "Multiple owners and reacquisition",         Cases.ownershipAndReacquisition
            "Failed disposal retains ownership",         Cases.disposalFailure

            "Naming failures clean unpublished handles",   Cases.namingFailureRetry
            "Release after failed naming",                 Cases.releaseAfterNamingFailure
            "Naming errors survive cleanup failures",      Cases.namingAndCleanupFailure
            "Live naming failure retains published owner", Cases.liveNamingFailure
        ]
        |> prepareCasesCpu "AdaptiveTexture" target
