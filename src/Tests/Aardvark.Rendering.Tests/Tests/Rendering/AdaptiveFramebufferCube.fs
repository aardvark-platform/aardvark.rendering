namespace Aardvark.Rendering.Tests.Rendering

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open FSharp.Data.Adaptive
open Expecto

module AdaptiveFramebufferCube =

    module private Cases =

        type private Output(runtime : ITextureRuntime, slot : int, depth : bool) =
            member _.Slot = slot
            interface IFramebufferOutput with
                member _.Runtime = runtime
                member _.Size = V2i(64 >>> (slot / 6))
                member _.Format = if depth then TextureFormat.Depth24Stencil8 else TextureFormat.Rgba8
                member _.Samples = 1

        type private Attachment(initial : IFramebufferOutput) =
            inherit AdaptiveResource<IFramebufferOutput>()
            let value = cval initial
            member val Failure : exn option = None with get, set
            member val Creates = 0 with get, set
            member val Destroys = 0 with get, set
            member val Computes = 0 with get, set
            member val LastToken = RenderToken.Empty with get, set
            member _.Value = value.Value
            member _.Update(output) = transact (fun () -> value.Value <- output)
            override x.Create() = x.Creates <- x.Creates + 1
            override x.Destroy() = x.Destroys <- x.Destroys + 1
            override x.Compute(token, rt) =
                x.Computes <- x.Computes + 1
                x.LastToken <- rt
                match x.Failure with
                | Some error -> raise error
                | None -> value.GetValue token

        type private Signature(runtime : IFramebufferRuntime) =
            member val Disposals = 0 with get, set
            interface IFramebufferSignature with
                member _.Runtime = runtime
                member _.Samples = 1
                member _.LayerCount = 1
                member _.PerLayerUniforms = Set.empty
                member _.ColorAttachments = Map.ofList [0, { Name = DefaultSemantic.Colors; Format = TextureFormat.Rgba8 }]
                member _.DepthStencilAttachment = Some TextureFormat.Depth24Stencil8
                member x.Dispose() = x.Disposals <- x.Disposals + 1

        type private Framebuffer(signature : IFramebufferSignature, attachments : Map<Symbol, IFramebufferOutput>, id : int) =
            member val Disposals = 0 with get, set
            member _.Slot = (attachments.[DefaultSemantic.Colors] :?> Output).Slot
            interface IFramebuffer with
                member _.Signature = signature
                member _.Size = attachments.[DefaultSemantic.Colors].Size
                member _.Handle = uint64 id
                member _.Attachments = attachments
                member x.Dispose() = x.Disposals <- x.Disposals + 1

        let private unexpected() = failwith "Unexpected runtime operation"

        type private Runtime() =
            let attempts = ResizeArray<int>()
            let frames = ResizeArray<Framebuffer>()
            member val Failure : (int * exn) option = None with get, set
            member _.Attempts = attempts
            member _.Frames = frames
            interface IFramebufferRuntime with
                member _.DeviceCount = 1
                member _.ShaderDepthRange = Range1f(-1.0f, 1.0f)
                member _.SupportsLayeredShaderInputs = false
                member _.CreateFramebufferSignature(_, _, _, _, _) = unexpected()
                member x.CreateFramebuffer(signature, attachments) =
                    let slot = (attachments.[DefaultSemantic.Colors] :?> Output).Slot
                    attempts.Add slot
                    match x.Failure with
                    | Some (failedSlot, error) when failedSlot = slot -> raise error
                    | _ ->
                        let frame = new Framebuffer(signature, attachments, frames.Count + 1)
                        frames.Add frame
                        frame :> IFramebuffer
                member _.Copy(_, _) = unexpected()
                member _.ReadPixels(_, _, _, _) = unexpected()
                member _.Clear(_, _) = unexpected()
            interface ITextureRuntime with
                member _.CreateTexture(_, _, _, _, _) = unexpected()
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

        type private FailureSite = Color | Depth | Framebuffer

        type private Fixture(levels : int, shared : bool) =
            let runtime = Runtime()
            let signature = new Signature(runtime)
            let token = RenderToken.Zero
            let sources =
                Array.init (if shared then 1 else 6 * levels) (fun slot ->
                    [| Attachment(Output(runtime, slot, false)); Attachment(Output(runtime, slot, true)) |])
            let inputs =
                CubeMap.init levels (fun face level ->
                    let slot = if shared then 0 else level * 6 + int face
                    Map.ofList [DefaultSemantic.Colors, sources.[slot].[0]; DefaultSemantic.DepthStencil, sources.[slot].[1]])
            let resource = (runtime :> IFramebufferRuntime).CreateFramebufferCube(signature, inputs)
            do resource.Acquire()

            new(levels) = new Fixture(levels, false)
            member _.Runtime = runtime
            member _.Signature = signature
            member _.Token = token
            member _.Resource = resource
            member _.Sources = sources |> Array.collect id
            member _.Count = 6 * levels
            member _.Get() = resource.GetValue(AdaptiveToken.Top, token)
            member _.Expected(slot, depth) = sources.[if shared then 0 else slot].[if depth then 1 else 0].Value
            member _.SetFailure(slot, site, error) =
                match site with
                | Color -> sources.[slot].[0].Failure <- Some error
                | Depth -> sources.[slot].[1].Failure <- Some error
                | Framebuffer -> runtime.Failure <- Some(slot, error)
            member _.ClearFailure() =
                runtime.Failure <- None
                for pair in sources do
                    for source in pair do source.Failure <- None
            member _.Update(slot, depth) =
                sources.[slot].[if depth then 1 else 0].Update(Output(runtime, slot, depth))

            interface IDisposable with
                member x.Dispose() =
                    x.ClearFailure()
                    // Original-source controls can throw during destruction. Always
                    // drain the in-memory fixture without masking their first failure.
                    try resource.ReleaseAll() with _ -> ()
                    for source in x.Sources do source.ReleaseAll()
                    for frame in runtime.Frames do
                        if frame.Disposals = 0 then (frame :> IFramebuffer).Dispose()
                    (signature :> IFramebufferSignature).Dispose()

        let private sites = [Color; Depth; Framebuffer]

        let private failure context (expected : exn) action =
            let actual = try action(); None with error -> Some error
            match actual with
            | None -> failtestf "%s: returned an incomplete cube instead of failing" context
            | Some error ->
                Expect.isTrue (obj.ReferenceEquals(expected, error)) $"{context}: failure identity changed ({error})"

        let private complete context (fixture : Fixture) =
            let result = fixture.Get()
            Expect.equal result.Levels (fixture.Count / 6) $"{context}: mip count"
            Expect.equal result.Data.Length fixture.Count $"{context}: slot count"
            for slot in 0 .. fixture.Count - 1 do
                let face, level = enum<CubeSide>(slot % 6), slot / 6
                let handle = result.[face, level]
                Expect.isNotNull (box handle) $"{context}, slot={slot}: unmaterialized result"
                let frame = handle :?> Framebuffer
                Expect.equal frame.Disposals 0 $"{context}, slot={slot}: disposed result"
                Expect.isTrue (obj.ReferenceEquals(handle.Signature, fixture.Signature)) $"{context}, slot={slot}: signature"
                Expect.equal handle.Size (fixture.Expected(slot, false).Size) $"{context}, slot={slot}: extent"
                Expect.equal handle.Attachments.Count 2 $"{context}, slot={slot}: attachment count"
                for depth, semantic in [false, DefaultSemantic.Colors; true, DefaultSemantic.DepthStencil] do
                    Expect.isTrue (obj.ReferenceEquals(handle.Attachments.[semantic], fixture.Expected(slot, depth)))
                        $"{context}, slot={slot}, depth={depth}: attachment identity/order"
            for source in fixture.Sources do
                Expect.isTrue (obj.ReferenceEquals(source.LastToken, fixture.Token)) $"{context}: render token was not propagated"
            result

        let private released context (fixture : Fixture) generations =
            for frame in fixture.Runtime.Frames do
                Expect.equal frame.Disposals 1 $"{context}, slot={frame.Slot}: owned handle disposal"
            for source in fixture.Sources do
                Expect.equal source.Creates generations $"{context}: attachment acquisition"
                Expect.equal source.Destroys generations $"{context}: attachment release balance"
            Expect.equal fixture.Signature.Disposals 0 $"{context}: caller signature was disposed"

        let private failInitial context (fixture : Fixture) slot site repeats =
            let error = InvalidOperationException($"initial slot {slot}")
            fixture.SetFailure(slot, site, error)
            for attempt in 1 .. repeats do
                failure $"{context}, attempt={attempt}" error (fun () -> fixture.Get() |> ignore)
                Expect.equal fixture.Runtime.Frames.Count slot $"{context}, attempt={attempt}: retained prefix size"
                for frame in fixture.Runtime.Frames do
                    Expect.equal frame.Disposals 0 $"{context}, attempt={attempt}: prefix was retired"
                let attempts = Array.append [|0 .. slot - 1|] (if site = Framebuffer then Array.create attempt slot else [||])
                Expect.sequenceEqual fixture.Runtime.Attempts attempts $"{context}, attempt={attempt}: materialization order"
                let counted = slot + (if site = Framebuffer then attempt else 0)
                Expect.equal fixture.Token.TotalCreatedResources counted $"{context}, attempt={attempt}: creation accounting"
                Expect.equal fixture.Token.TotalReplacedResources 0 $"{context}, attempt={attempt}: failure counted as replacement"
                for source in fixture.Sources do
                    Expect.equal source.Creates 1 $"{context}: attachment reacquired during retry"
                    Expect.equal source.Destroys 0 $"{context}: attachment released during retry"
            fixture.Runtime.Frames.ToArray()

        let retry() =
            for levels in 1 .. 3 do
                for slot in 0 .. 6 * levels - 1 do
                    for site in sites do
                        for repeats in [1; 3] do
                            let context = $"levels={levels}, slot={slot}, site={site}, repeats={repeats}"
                            use fixture = new Fixture(levels)
                            let prefix = failInitial context fixture slot site repeats
                            fixture.ClearFailure()
                            let result = complete context fixture
                            for index in 0 .. prefix.Length - 1 do
                                Expect.isTrue (obj.ReferenceEquals(prefix.[index], result.Data.[index])) $"{context}: unchanged prefix recreated"
                            Expect.equal fixture.Runtime.Frames.Count fixture.Count $"{context}: retry allocated surplus handles"
                            Expect.sequenceEqual (fixture.Runtime.Frames |> Seq.map _.Slot) [0 .. fixture.Count - 1]
                                $"{context}: face/mip creation order"
                            Expect.equal fixture.Token.TotalCreatedResources (fixture.Count + (if site = Framebuffer then repeats else 0))
                                $"{context}: retry creation accounting"
                            Expect.equal fixture.Token.TotalReplacedResources 0 $"{context}: retry replacement accounting"
                            fixture.Resource.Release()
                            released context fixture 1

        let releaseAfterFailure() =
            for levels in 1 .. 3 do
                for slot in 0 .. 6 * levels - 1 do
                    for site in sites do
                        for releaseAll in [false; true] do
                            for owners in [1; 2] do
                                let context = $"levels={levels}, slot={slot}, site={site}, ReleaseAll={releaseAll}, owners={owners}"
                                use fixture = new Fixture(levels)
                                if owners = 2 then fixture.Resource.Acquire()
                                let prefix = failInitial context fixture slot site 1
                                if releaseAll then fixture.Resource.ReleaseAll()
                                else
                                    if owners = 2 then
                                        fixture.Resource.Release()
                                        for frame in prefix do Expect.equal frame.Disposals 0 $"{context}: remaining owner lost its prefix"
                                    fixture.Resource.Release()
                                released context fixture 1
                                fixture.Resource.ReleaseAll()
                                released context fixture 1

                                fixture.ClearFailure()
                                fixture.Resource.Acquire()
                                let result = complete context fixture
                                for index in 0 .. prefix.Length - 1 do
                                    Expect.isFalse (obj.ReferenceEquals(prefix.[index], result.Data.[index])) $"{context}: reacquisition reused retired prefix"
                                Expect.equal fixture.Runtime.Frames.Count (slot + fixture.Count) $"{context}: reacquisition did not materialize all slots"
                                fixture.Resource.Release()
                                released context fixture 2

        let changedPrefix() =
            for levels in 1 .. 3 do
                for slot in 1 .. 6 * levels - 1 do
                    for site in sites do
                        let context = $"levels={levels}, slot={slot}, site={site}"
                        use fixture = new Fixture(levels)
                        let prefix = failInitial context fixture slot site 1
                        fixture.Update(slot - 1, false)
                        fixture.ClearFailure()
                        let result = complete context fixture
                        for index in 0 .. prefix.Length - 1 do
                            Expect.equal prefix.[index].Disposals (if index = slot - 1 then 1 else 0) $"{context}, index={index}: prefix replacement"
                            Expect.equal (obj.ReferenceEquals(prefix.[index], result.Data.[index])) (index <> slot - 1)
                                $"{context}, index={index}: unchanged prefix identity"
                        Expect.equal fixture.Runtime.Frames.Count (fixture.Count + 1) $"{context}: replacement allocation count"
                        Expect.equal fixture.Token.TotalCreatedResources (fixture.Count + (if site = Framebuffer then 1 else 0))
                            $"{context}: missing-slot accounting"
                        Expect.equal fixture.Token.TotalReplacedResources 1 $"{context}: completed-slot replacement accounting"
                        fixture.Resource.Release()
                        released context fixture 1

        let cachedAndInvalidated() =
            for levels in 1 .. 3 do
                for shared in [false; true] do
                    let context = $"levels={levels}, shared={shared}"
                    use fixture = new Fixture(levels, shared)
                    let first = complete context fixture
                    Expect.sequenceEqual (fixture.Runtime.Frames |> Seq.map _.Slot)
                        (Array.init fixture.Count (fun slot -> if shared then 0 else slot)) $"{context}: face/mip creation order"
                    for _ in 1 .. 8 do
                        let cached = complete context fixture
                        Expect.isTrue (obj.ReferenceEquals(first.Data, cached.Data)) $"{context}: cached cube array changed"
                    for _ in 1 .. 3 do
                        transact fixture.Resource.MarkOutdated
                        let unchanged = complete context fixture
                        for slot in 0 .. fixture.Count - 1 do
                            Expect.isTrue (obj.ReferenceEquals(first.Data.[slot], unchanged.Data.[slot])) $"{context}, slot={slot}: unchanged handle replaced"
                    Expect.equal fixture.Runtime.Frames.Count fixture.Count $"{context}: unchanged reads allocated handles"
                    Expect.equal fixture.Token.TotalCreatedResources fixture.Count $"{context}: successful creation accounting"
                    Expect.equal fixture.Token.TotalReplacedResources 0 $"{context}: unchanged replacement accounting"
                    for source in fixture.Sources do Expect.equal source.Computes 1 $"{context}: cached attachment reevaluated"
                    fixture.Resource.Release()
                    released context fixture 1

        let singleSlotUpdates() =
            for levels in 1 .. 3 do
                for slot in 0 .. 6 * levels - 1 do
                    for depth in [false; true] do
                        let context = $"levels={levels}, slot={slot}, depth={depth}"
                        use fixture = new Fixture(levels)
                        let mutable previous = complete context fixture
                        for version in 1 .. 3 do
                            fixture.Update(slot, depth)
                            let current = complete context fixture
                            for index in 0 .. fixture.Count - 1 do
                                let retired = previous.Data.[index] :?> Framebuffer
                                Expect.equal retired.Disposals (if index = slot then 1 else 0)
                                    $"{context}, version={version}, index={index}: replacement disposal"
                                Expect.equal (obj.ReferenceEquals(previous.Data.[index], current.Data.[index])) (index <> slot)
                                    $"{context}, version={version}, index={index}: replacement identity"
                            previous <- current
                        Expect.equal fixture.Runtime.Frames.Count (fixture.Count + 3) $"{context}: replacement allocation count"
                        Expect.equal fixture.Token.TotalCreatedResources fixture.Count $"{context}: creation accounting"
                        Expect.equal fixture.Token.TotalReplacedResources 3 $"{context}: replacement accounting"
                        fixture.Resource.Release()
                        released context fixture 1

        let ownershipAndReacquisition() =
            for levels in 1 .. 3 do
                for materialized in [false; true] do
                    let context = $"levels={levels}, materialized={materialized}"
                    use fixture = new Fixture(levels)
                    fixture.Resource.Acquire()
                    if materialized then complete context fixture |> ignore
                    fixture.Resource.Release()
                    for source in fixture.Sources do Expect.equal source.Destroys 0 $"{context}: remaining owner lost attachment"
                    for frame in fixture.Runtime.Frames do Expect.equal frame.Disposals 0 $"{context}: remaining owner lost handle"
                    fixture.Resource.ReleaseAll()
                    released context fixture 1
                    fixture.Resource.ReleaseAll()
                    released context fixture 1
                    fixture.Resource.Acquire()
                    complete context fixture |> ignore
                    fixture.Resource.Release()
                    released context fixture 2

    let tests (target : TestTarget) =
        [
            "Failed initial slots retry without losing the prefix", Cases.retry
            "Release after failure permits reacquisition",          Cases.releaseAfterFailure
            "Changed completed slots retain replacement behavior",  Cases.changedPrefix
            "Cached and unchanged invalidated evaluations",         Cases.cachedAndInvalidated
            "Single-slot updates preserve other handles",           Cases.singleSlotUpdates
            "Multiple owners and unmaterialized reacquisition",     Cases.ownershipAndReacquisition
        ]
        |> prepareCasesCpu "AdaptiveFramebufferCube" target
