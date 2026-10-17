namespace Aardvark.Rendering.Tests.Texture

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open FSharp.Data.Adaptive
open Expecto

module AdaptiveRenderbuffer =

    module private Cases =

        type private Parameters = { Size : V2i; Format : TextureFormat; Samples : int }

        type private Renderbuffer(runtime : ITextureRuntime, id : int, parameters : Parameters, dispose : unit -> unit) =
            let mutable retired = false
            let mutable disposals = 0
            let mutable retiredReads = 0
            let mutable retiredNames = 0
            let mutable name = null
            let read() = if retired then retiredReads <- retiredReads + 1
            member _.Parameters = parameters
            member _.Retired = retired
            member _.Disposals = disposals
            member _.RetiredReads = retiredReads
            member _.RetiredNames = retiredNames
            member _.NameValue = name
            interface IRenderbuffer with
                member _.Runtime = runtime
                member _.Size = read(); parameters.Size
                member _.Format = read(); parameters.Format
                member _.Samples = read(); parameters.Samples
                member _.Handle = uint64 id
                member _.Name
                    with get() = name
                    and set value =
                        if retired then retiredNames <- retiredNames + 1
                        name <- value
                member _.Dispose() =
                    disposals <- disposals + 1
                    dispose()
                    retired <- true

        let private unexpected() = failwith "Unexpected runtime operation"

        type private Runtime() =
            let requests = ResizeArray<Parameters>()
            let buffers = ResizeArray<Renderbuffer>()
            member val CreationError : exn option = None with get, set
            member val DisposalError : exn option = None with get, set
            member _.Requests = requests
            member _.Buffers = buffers
            member private this.Create(parameters) =
                requests.Add parameters
                Expect.isTrue (buffers |> Seq.forall _.Retired) "Creation must follow successful retirement"
                match this.CreationError with
                | Some error -> raise error
                | None ->
                    let buffer = new Renderbuffer(this, buffers.Count + 1, parameters, fun () ->
                        match this.DisposalError with Some error -> raise error | None -> ())
                    buffers.Add buffer
                    buffer :> IRenderbuffer
            interface ITextureRuntime with
                member this.CreateRenderbuffer(size, format, samples) = this.Create { Size = size; Format = format; Samples = samples }
                member _.CreateTexture(_, _, _, _, _) = unexpected()
                member _.CreateTextureArray(_, _, _, _, _, _) = unexpected()
                member _.PrepareTexture _ = unexpected()
                member _.Copy(_, _, _, _, _, _, _, _) = unexpected()
                member _.Blit(_, _, _, _) = unexpected()
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

        type private Fixture(initial : Parameters, owners : int) =
            let runtime = Runtime()
            let size = cval initial.Size
            let samples = cval initial.Samples
            let token = RenderToken.Zero
            let resource = (runtime :> ITextureRuntime).CreateRenderbuffer(size, initial.Format, samples)
            do for _ = 1 to owners do resource.Acquire()
            member _.Runtime = runtime
            member _.Resource = resource
            member _.Token = token
            member _.Parameters = { initial with Size = size.Value; Samples = samples.Value }
            member _.Get() = (resource :> IAdaptiveResource<IRenderbuffer>).GetValue(AdaptiveToken.Top, token)
            member _.Change(kind) =
                transact (fun () ->
                    size.Value <- if kind = "samples" then initial.Size else initial.Size + V2i(3, 5)
                    samples.Value <- if kind = "size" then initial.Samples else initial.Samples * 2)
            member _.Restore() = transact (fun () -> size.Value <- initial.Size; samples.Value <- initial.Samples)
            interface IDisposable with
                member _.Dispose() =
                    runtime.CreationError <- None
                    runtime.DisposalError <- None
                    resource.ReleaseAll()

        let private forFixtures action =
            for size in [V2i(1, 3); V2i(8); V2i(16, 9)] do
                for format in [TextureFormat.Rgba8; TextureFormat.Rgba16f; TextureFormat.R32f; TextureFormat.Depth24Stencil8] do
                    for samples in [1; 2; 4] do
                        for owners in [1; 2; 3] do
                            let parameters = { Size = size; Format = format; Samples = samples }
                            let context = sprintf "parameters=%A, owners=%d" parameters owners
                            use fixture = new Fixture(parameters, owners)
                            action context owners fixture

        let private expectFailure context (expected : exn) action =
            let actual = try action(); None with error -> Some error
            match actual with
            | Some error -> Expect.isTrue (obj.ReferenceEquals(error, expected)) (context + ": original exception identity")
            | None -> failtestf "%s: expected backend failure" context

        let private checkLive context (fixture : Fixture) (handle : IRenderbuffer) =
            let buffer = handle :?> Renderbuffer
            Expect.isFalse buffer.Retired (context + ": returned a retired renderbuffer")
            Expect.equal buffer.Parameters fixture.Parameters (context + ": creation parameters")
            Expect.equal handle.Name fixture.Resource.Name (context + ": stored name")
            Expect.isTrue (obj.ReferenceEquals(handle.Runtime, fixture.Resource.Runtime)) (context + ": runtime identity")
            Expect.equal (AVal.force fixture.Resource.Size) handle.Size (context + ": size metadata")
            Expect.equal (AVal.force fixture.Resource.Format) handle.Format (context + ": format metadata")
            Expect.equal (AVal.force fixture.Resource.Samples) handle.Samples (context + ": sample metadata")
            buffer

        let private checkRetired context (buffer : Renderbuffer) =
            Expect.isTrue buffer.Retired (context + ": old handle is retired")
            Expect.equal buffer.Disposals 1 (context + ": successful retirement occurs once")
            Expect.equal buffer.RetiredReads 0 (context + ": retired metadata is never consulted")
            Expect.equal buffer.RetiredNames 0 (context + ": retired names are never changed")

        let private failReplacement context (fixture : Fixture) kind =
            let first = fixture.Get()
            let old = checkLive context fixture first
            fixture.Change kind
            let error = InvalidOperationException("replacement creation")
            fixture.Runtime.CreationError <- Some error
            expectFailure context error (fun () -> fixture.Get() |> ignore)
            first, old

        let normalReplacement() =
            forFixtures (fun context owners fixture ->
                Expect.equal fixture.Runtime.Requests.Count 0 (context + ": Acquire is lazy")
                fixture.Resource.Name <- "initial"
                let first = fixture.Get()
                let old = checkLive context fixture first
                for read = 1 to 8 do
                    Expect.isTrue (obj.ReferenceEquals(first, fixture.Get())) (sprintf "%s, read=%d: cached identity" context read)
                transact fixture.Resource.MarkOutdated
                Expect.isTrue (obj.ReferenceEquals(first, fixture.Get())) (context + ": unchanged compute retains the handle")
                Expect.equal fixture.Runtime.Requests.Count 1 (context + ": cached reads do not allocate backend storage")
                Expect.equal old.Disposals 0 (context + ": cached reads do not dispose")
                fixture.Resource.Name <- "replacement"
                Expect.equal first.Name "replacement" (context + ": live naming")
                fixture.Change "size"
                let second = fixture.Get() |> checkLive context fixture
                checkRetired context old
                fixture.Change "both"
                let current = fixture.Get() |> checkLive context fixture
                checkRetired context second
                Expect.equal fixture.Token.TotalCreatedResources 1 (context + ": successful initial statistics")
                Expect.equal fixture.Token.TotalReplacedResources 2 (context + ": successful replacement statistics")
                for owner = 1 to owners - 1 do
                    fixture.Resource.Release()
                    Expect.equal current.Disposals 0 (sprintf "%s, released=%d: remaining owner retains storage" context owner)
                    Expect.isTrue (obj.ReferenceEquals(current, fixture.Get())) (context + ": remaining owner cached identity")
                fixture.Resource.Release()
                checkRetired context current
                fixture.Resource.Acquire()
                let next = fixture.Get() |> checkLive context fixture
                Expect.isFalse (obj.ReferenceEquals(current, next)) (context + ": reacquisition creates fresh storage")
                Expect.equal fixture.Token.TotalCreatedResources 2 (context + ": reacquisition creation statistics")
                Expect.equal fixture.Token.TotalReplacedResources 2 (context + ": reacquisition is not replacement"))

        let replacementFailureRetry() =
            forFixtures (fun context _ fixture ->
                for kind in ["size"; "samples"; "both"] do
                    for failures in [1; 3] do
                        let context = sprintf "%s, change=%s, failures=%d" context kind failures
                        let _, old = failReplacement context fixture kind
                        for attempt = 2 to failures do
                            let error = InvalidOperationException(sprintf "retry %d" attempt)
                            fixture.Runtime.CreationError <- Some error
                            expectFailure context error (fun () -> fixture.Get() |> ignore)
                            checkRetired context old
                        fixture.Runtime.CreationError <- None
                        let current = fixture.Get() |> checkLive context fixture
                        checkRetired context old
                        Expect.isFalse (obj.ReferenceEquals(old, current)) (context + ": retry returns new storage")
                        fixture.Restore()
                        fixture.Get() |> checkLive context fixture |> ignore)

        let restoredParameters() =
            forFixtures (fun context _ fixture ->
                let first, old = failReplacement context fixture "both"
                fixture.Restore()
                fixture.Runtime.CreationError <- None
                let current = fixture.Get()
                checkLive context fixture current |> ignore
                Expect.isFalse (obj.ReferenceEquals(first, current)) (context + ": restored parameters cannot resurrect retired storage")
                checkRetired context old
                Expect.equal fixture.Runtime.Requests.Count 3 (context + ": restoration creates a fresh handle"))

        let pendingNames() =
            forFixtures (fun context _ fixture ->
                fixture.Resource.Name <- "old"
                let _, old = failReplacement context fixture "size"
                for name in [null; ""; "pending replacement"] do
                    fixture.Resource.Name <- name
                    Expect.equal fixture.Resource.Name name (sprintf "%s, name=%A: pending name" context name)
                    checkRetired context old
                    Expect.equal old.NameValue "old" (context + ": retired name unchanged")
                fixture.Runtime.CreationError <- None
                let current = fixture.Get() |> checkLive context fixture
                Expect.equal current.NameValue "pending replacement" (context + ": fresh handle receives the pending name")
                checkRetired context old)

        let releaseAfterFailure() =
            forFixtures (fun context owners fixture ->
                for releaseAll in [false; true] do
                    let context = sprintf "%s, ReleaseAll=%b" context releaseAll
                    let _, old = failReplacement context fixture "samples"
                    let requests = fixture.Runtime.Requests.Count
                    if releaseAll then fixture.Resource.ReleaseAll()
                    else for _ = 1 to owners do fixture.Resource.Release()
                    fixture.Resource.ReleaseAll()
                    checkRetired context old
                    Expect.equal fixture.Runtime.Requests.Count requests (context + ": release does not create")
                    fixture.Runtime.CreationError <- None
                    fixture.Resource.Name <- "reacquired"
                    for _ = 1 to owners do fixture.Resource.Acquire()
                    let current = fixture.Get() |> checkLive context fixture
                    Expect.isFalse (obj.ReferenceEquals(old, current)) (context + ": reacquisition after failure creates storage")
                    fixture.Restore()
                    fixture.Get() |> checkLive context fixture |> ignore)

        let initialFailure() =
            forFixtures (fun context _ fixture ->
                let error = InvalidOperationException("initial creation")
                fixture.Resource.Name <- "initial retry"
                fixture.Runtime.CreationError <- Some error
                for attempt = 1 to 3 do
                    expectFailure (sprintf "%s, attempt=%d" context attempt) error (fun () -> fixture.Get() |> ignore)
                Expect.equal fixture.Runtime.Buffers.Count 0 (context + ": failed initial attempts publish nothing")
                fixture.Resource.ReleaseAll()
                fixture.Resource.ReleaseAll()
                fixture.Runtime.CreationError <- None
                fixture.Resource.Acquire()
                let current = fixture.Get() |> checkLive context fixture
                Expect.equal fixture.Runtime.Requests.Count 4 (context + ": initial creation retries after reacquisition")
                fixture.Resource.Release()
                checkRetired context current)

        let disposalFailure() =
            forFixtures (fun context _ fixture ->
                let first = fixture.Get()
                let old = checkLive context fixture first
                fixture.Change "both"
                let error = InvalidOperationException("retirement")
                fixture.Runtime.DisposalError <- Some error
                expectFailure context error (fun () -> fixture.Get() |> ignore)
                Expect.isFalse old.Retired (context + ": unsuccessful deletion retains live ownership")
                Expect.equal fixture.Runtime.Requests.Count 1 (context + ": no create after failed deletion")
                fixture.Resource.Name <- "still owned"
                Expect.equal old.NameValue "still owned" (context + ": live naming remains available")
                fixture.Restore()
                Expect.isTrue (obj.ReferenceEquals(first, fixture.Get())) (context + ": restored parameters reuse the still-live handle")
                fixture.Change "both"
                fixture.Runtime.DisposalError <- None
                let current = fixture.Get() |> checkLive context fixture
                Expect.isFalse (obj.ReferenceEquals(old, current)) (context + ": replacement follows successful deletion retry")
                Expect.isTrue old.Retired (context + ": deletion is retried")
                Expect.equal old.Disposals 2 (context + ": failed and successful deletion attempts")
                Expect.equal fixture.Runtime.Requests.Count 2 (context + ": exactly one replacement creation"))

    let tests (target : TestTarget) =
        [
            "Cached reads replacement and ownership",     Cases.normalReplacement
            "Replacement failures permit retry",          Cases.replacementFailureRetry
            "Restored parameters create a fresh handle",  Cases.restoredParameters
            "Names remain pending after failed creation", Cases.pendingNames
            "Release and reacquisition after failure",    Cases.releaseAfterFailure
            "Initial creation failures remain retryable", Cases.initialFailure
            "Failed disposal retains live ownership",     Cases.disposalFailure
        ]
        |> prepareCasesCpu "AdaptiveRenderbuffer" target
