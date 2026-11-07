namespace Aardvark.Rendering.Tests.Rendering

open System
open System.Collections.Generic
open System.Reflection
open System.Runtime.CompilerServices
open System.Runtime.ExceptionServices
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open FSharp.Data.Adaptive
open Expecto

module Screenshots =
    type private RecordingProxy() =
        inherit DispatchProxy()
        member val Handler : MethodInfo -> obj[] -> obj = fun method _ -> failwith method.Name with get, set
        override x.Invoke(method, args) = x.Handler method args

    let private proxy<'T> handler =
        let value = DispatchProxy.Create<'T, RecordingProxy>()
        (box value :?> RecordingProxy).Handler <- handler
        value

    let private same expected actual context =
        Expect.isTrue (obj.ReferenceEquals(expected, actual)) context

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    let private throwInjected (error : exn) = raise error

    type private Temporary =
        { Name : string; mutable Attempts : int; mutable Released : bool }

    type private Capture(samples, depthFormat : TextureFormat option, failure : string option, disposalErrors : string list) =
        let size = V2i(4, 3)
        let colorFormat = if depthFormat.IsSome then TextureFormat.Rgba16f else TextureFormat.Rgba8
        let events = ResizeArray<string>()
        let resources = ResizeArray<Temporary>()
        let current = Dictionary<string, Temporary>()
        let errors = disposalErrors |> List.map (fun name -> name, ApplicationException("dispose " + name) :> exn) |> Map.ofList
        let bodyError = ApplicationException("capture stage")
        let mutable borrowedDisposals = 0
        let mutable renderbuffers = 0
        let mutable framebuffer = Unchecked.defaultof<IFramebuffer>
        let mutable color = Unchecked.defaultof<IRenderbuffer>
        let mutable depth = Unchecked.defaultof<IRenderbuffer>
        let mutable texture = Unchecked.defaultof<IBackendTexture>
        let mutable runtime = Unchecked.defaultof<IRuntime>
        let mutable start = 0
        let mutable resourceStart = 0
        let live name =
            Expect.equal current.[name].Attempts 0 (name + " must remain live while needed")
        let reach name dependencies =
            for dependency in dependencies do live dependency
            events.Add name
            if failure = Some name then throwInjected bodyError
        let acquire name =
            let value = { Name = name; Attempts = 0; Released = false }
            current.Add(name, value)
            resources.Add value
            value
        let dispose value =
            if value.Name = "framebuffer" then live "color"; live "depth"
            if value.Name = "depth" && current.ContainsKey "framebuffer" then
                Expect.equal current.["framebuffer"].Attempts 1 "Framebuffer cleanup precedes attachment cleanup"
            if value.Name = "color" && current.ContainsKey "depth" then
                Expect.equal current.["depth"].Attempts 1 "Depth cleanup precedes color cleanup"
            value.Attempts <- value.Attempts + 1
            events.Add ("dispose " + value.Name)
            match Map.tryFind value.Name errors with
            | Some error -> throwInjected error
            | None -> value.Released <- true
        let signature = proxy<IFramebufferSignature> (fun method _ ->
            match method.Name with
            | "get_Runtime" -> box runtime
            | "get_ColorAttachments" -> box (Map.ofList [0, { Name = DefaultSemantic.Colors; Format = colorFormat }])
            | "get_DepthStencilAttachment" -> box depthFormat
            | "get_Samples" -> box samples
            | "get_LayerCount" -> box 1
            | "Dispose" -> borrowedDisposals <- borrowedDisposals + 1; null
            | _ -> failwith method.Name)
        let task temporary = proxy<IRenderTask> (fun method args ->
            match method.Name with
            | "get_Runtime" -> box (Some runtime)
            | "get_FramebufferSignature" -> box (Some signature)
            | "Run" ->
                reach (if temporary then "clear run" else "caller run") ["color"; "depth"; "clear"; "framebuffer"]
                let output = unbox<OutputDescription> args.[2]
                same framebuffer output.Framebuffer "Original output framebuffer"
                Expect.equal output.Viewport (Box2i.FromMinAndSize(V2i.Zero, size)) "Capture viewport"
                null
            | "Dispose" ->
                if temporary then dispose current.["clear"]
                else borrowedDisposals <- borrowedDisposals + 1
                null
            | _ -> failwith method.Name)
        let caller = task false
        do runtime <- proxy<IRuntime> (fun method args ->
            match method.Name with
            | "CreateRenderbuffer" ->
                let name = if renderbuffers = 0 then "color" else "depth"
                reach name (if renderbuffers = 0 then [] else ["color"])
                let format = if renderbuffers = 0 then colorFormat else defaultArg depthFormat TextureFormat.DepthComponent32
                Expect.equal (unbox<V2i> args.[0], unbox<TextureFormat> args.[1], unbox<int> args.[2]) (size, format, samples) "Renderbuffer parameters"
                let state = acquire name
                let buffer = proxy<IRenderbuffer> (fun method _ ->
                    match method.Name with
                    | "get_Runtime" -> box runtime
                    | "get_Format" -> box format
                    | "get_Samples" -> box samples
                    | "get_Size" -> live name; box size
                    | "Dispose" -> dispose state; null
                    | _ -> failwith method.Name)
                if renderbuffers = 0 then color <- buffer else depth <- buffer
                renderbuffers <- renderbuffers + 1
                box buffer
            | "CompileClear" ->
                reach "compile clear" ["color"; "depth"]
                same signature args.[0] "Borrowed signature used for clear"
                let values = AVal.force (unbox<aval<ClearValues>> args.[1])
                Expect.equal (values.Color.Value.Float, values.Depth) (V4f.Zero, Some 1.0f) "Original clear values"
                acquire "clear" |> ignore
                box (task true)
            | "CreateFramebuffer" ->
                reach "framebuffer" ["color"; "depth"; "clear"]
                same signature args.[0] "Borrowed framebuffer signature"
                let attachments = unbox<Map<Symbol, IFramebufferOutput>> args.[1]
                same color attachments.[DefaultSemantic.Colors] "Color attachment identity"
                same depth attachments.[DefaultSemantic.DepthStencil] "Depth attachment identity"
                let state = acquire "framebuffer"
                framebuffer <- proxy<IFramebuffer> (fun method _ ->
                    match method.Name with
                    | "get_Size" -> live "framebuffer"; box size
                    | "get_Signature" -> box signature
                    | "get_Attachments" -> box attachments
                    | "Dispose" -> dispose state; null
                    | _ -> failwith method.Name)
                box framebuffer
            | "CreateTexture" ->
                reach "texture" ["color"; "depth"; "clear"; "framebuffer"]
                Expect.equal (unbox<V3i> args.[0], unbox<TextureDimension> args.[1], unbox<TextureFormat> args.[2], unbox<int> args.[3], unbox<int> args.[4]) (size.XYI, TextureDimension.Texture2D, TextureFormat.Rgba8, 1, 1) "Resolve texture parameters"
                let state = acquire "texture"
                texture <- proxy<IBackendTexture> (fun method _ ->
                    match method.Name with
                    | "get_Runtime" -> box runtime
                    | "get_Format" -> box TextureFormat.Rgba8
                    | "get_Dimension" -> box TextureDimension.Texture2D
                    | "get_Size" -> live "texture"; box size.XYI
                    | "get_Samples" | "get_Count" | "get_MipMapLevels" -> box 1
                    | "Dispose" -> dispose state; null
                    | _ -> failwith method.Name)
                box texture
            | "Blit" ->
                reach "resolve" ["color"; "depth"; "clear"; "framebuffer"; "texture"]
                same color args.[0] "Resolve source"
                same texture (unbox<ITextureRange> args.[2]).Texture "Resolve destination"
                Expect.equal (unbox<Box3i> args.[1], unbox<Box3i> args.[3]) (Box3i.FromMinAndSize(V3i.Zero, size.XYI), Box3i.FromMinAndSize(V3i.Zero, size.XYI)) "Resolve regions"
                null
            | "Download" ->
                reach "download" ["color"; "depth"; "clear"; "framebuffer"; "texture"]
                same texture (unbox<ITextureSubResource> args.[0]).Texture "Download source"
                Expect.equal (unbox<Col.Format> args.[2], unbox<V3i> args.[3], unbox<V3i> args.[4]) (Col.Format.RGBA, V3i.Zero, size.XYI) "Download region and format"
                let mutable dst = unbox<NativeTensor4<byte>> args.[1]
                Expect.equal (dst.SX, dst.SY, dst.SZ, dst.SW) (4L, 3L, 1L, 4L) "Bounded download tensor"
                let pixel = [| 17uy; 83uy; 149uy; 255uy |]
                for y in 0 .. 2 do
                    for x in 0 .. 3 do
                        for c in 0 .. 3 do dst.[x, y, 0, c] <- pixel.[c]
                null
            | _ -> failwith method.Name)
        member _.Run() =
            current.Clear()
            renderbuffers <- 0
            start <- events.Count
            resourceStart <- resources.Count
            try Result.Ok (Aardvark.Application.Screenshot.renderToImage samples size caller)
            with error -> Result.Error error
        member _.Check(result : Result<PixImage, exn>, context : string) =
            let acquired = resources |> Seq.skip resourceStart |> Seq.toArray
            let stages = ["color"; "depth"; "compile clear"; "framebuffer"; "clear run"; "caller run"; "texture"; "resolve"; "download"]
            let expectedStages = match failure with None -> stages | Some stage -> (stages |> List.takeWhile ((<>) stage)) @ [stage]
            let actualStages = events |> Seq.skip start |> Seq.filter (fun s -> not (s.StartsWith "dispose "))
            Expect.sequenceEqual actualStages expectedStages (context + ": allocation and rendering order")
            for value in acquired do
                Expect.equal value.Attempts 1 $"{context}, {value.Name}: one disposal attempt"
                Expect.equal value.Released (not (errors.ContainsKey value.Name)) $"{context}, {value.Name}: failed disposal is not reported as release"
            let order = events |> Seq.skip start |> Seq.filter (fun s -> s.StartsWith "dispose ") |> Seq.toArray
            let expected = acquired |> Array.rev |> Array.map (fun value -> "dispose " + value.Name)
            Expect.sequenceEqual order expected (context + ": reverse acquisition cleanup order")
            Expect.equal borrowedDisposals 0 (context + ": caller task and signature remain borrowed")
            let expectedError = acquired |> Array.rev |> Array.choose (fun v -> Map.tryFind v.Name errors) |> Array.tryLast
            match expectedError, failure, result with
            | Some expected, _, Result.Error error -> same expected error (context + ": outermost scoped disposer error wins")
            | None, Some _, Result.Error error -> same bodyError error (context + ": original stage error")
            | None, None, Result.Ok _ -> ()
            | _ -> failtestf "%s: unexpected result %A" context result
            match result with
            | Result.Error error -> Expect.stringContains error.StackTrace "throwInjected" (context + ": original throw site")
            | _ -> ()
        member _.CheckReleased() =
            for value in resources do Expect.equal (value.Attempts, value.Released) (1, true) "Every capture releases each acquired temporary"

    module private Cases =
        let private checkImage (image : PixImage) (size : V2i) (color : C4b) context =
            Expect.equal (image.Size, image.PixFormat) (size, PixFormat.ByteRGBA) (context + ": image metadata")
            let pixels = (image :?> PixImage<byte>).GetMatrix<C4b>()
            for y in 0 .. size.Y - 1 do
                for x in 0 .. size.X - 1 do Expect.equal pixels.[x, y] color $"{context}, pixel={x}/{y}"

        let successful _ =
            let errors = ResizeArray<string>()
            for samples in [1; 4] do
                for depth in [None; Some TextureFormat.Depth24Stencil8] do
                    let capture = Capture(samples, depth, None, [])
                    let images = ResizeArray<PixImage>()
                    for i in 1 .. 3 do
                        let context = $"samples={samples}, depth={depth}, capture={i}"
                        let result = capture.Run()
                        try capture.Check(result, context)
                        with error -> errors.Add(context + ": " + error.Message)
                        match result with
                        | Result.Ok image -> images.Add image
                        | Result.Error error -> raise error
                    try capture.CheckReleased()
                    with error -> errors.Add($"samples={samples}, depth={depth}: {error.Message}")
                    for image in images do checkImage image (V2i(4, 3)) (C4b(17uy, 83uy, 149uy, 255uy)) "Retained image after later captures"
                    let first = images.[0] :?> PixImage<byte>
                    let second = images.[1] :?> PixImage<byte>
                    Expect.isFalse (obj.ReferenceEquals(first.Data, second.Data)) "Returned images own independent pixel storage"
                    let mutable pixels = first.GetMatrix<C4b>()
                    pixels.[0, 0] <- C4b.Black
                    checkImage second (V2i(4, 3)) (C4b(17uy, 83uy, 149uy, 255uy)) "Later image remains independent"
            Expect.isEmpty errors (String.concat "\n" errors)

        let private failures stages =
            let errors = ResizeArray<string>()
            for samples in [1; 4] do
                for stage in stages do
                    let context = $"samples={samples}, stage={stage}"
                    let capture = Capture(samples, None, Some stage, [])
                    try capture.Check(capture.Run(), context)
                    with error -> errors.Add(context + ": " + error.Message)
            Expect.isEmpty errors (String.concat "\n" errors)

        let setupFailures _ = failures ["depth"; "compile clear"; "framebuffer"]
        let executionFailures _ = failures ["clear run"; "caller run"; "texture"; "resolve"; "download"]

        let disposalFailures _ =
            let errors = ResizeArray<string>()
            for samples in [1; 4] do
                for body in [None; Some "download"] do
                    for disposers in [["texture"]; ["framebuffer"]; ["clear"]; ["depth"]; ["color"]; ["texture"; "framebuffer"; "clear"; "depth"; "color"]] do
                        let context = $"samples={samples}, body={body}, disposers={disposers}"
                        let capture = Capture(samples, None, body, disposers)
                        try capture.Check(capture.Run(), context)
                        with error -> errors.Add(context + ": " + error.Message)
            Expect.isEmpty errors (String.concat "\n" errors)

        let private invoke (target : obj) (method : MethodInfo) args =
            try method.Invoke(target, args)
            with :? TargetInvocationException as error ->
                ExceptionDispatchInfo.Capture(error.InnerException).Throw()
                Unchecked.defaultof<obj>

        let repeatedGpu (runtime : IRuntime) =
            let buffers = ResizeArray<IRenderbuffer>()
            let textures = ResizeArray<IBackendTexture>()
            let framebuffers = ResizeArray<IFramebuffer>()
            let clearDisposals = ResizeArray<int[]>()
            let mutable completed = 0
            let recording = proxy<IRuntime> (fun method args ->
                let mutable result = invoke runtime method args
                match method.Name with
                | "CreateRenderbuffer" -> buffers.Add(unbox result)
                | "CreateTexture" -> textures.Add(unbox result)
                | "CreateFramebuffer" -> framebuffers.Add(unbox result)
                | "CompileClear" ->
                    let clear = unbox<IRenderTask> result
                    let attempts = [| 0 |]
                    clearDisposals.Add attempts
                    result <- box (proxy<IRenderTask> (fun method args ->
                        if method.Name = "Dispose" then attempts.[0] <- attempts.[0] + 1
                        invoke clear method args))
                | _ -> ()
                result)
            let counts() =
                match runtime with
                | :? Aardvark.Rendering.GL.Runtime as gl ->
                    let m = gl.Context.MemoryUsage
                    [m.RenderBufferCount; m.TextureCount; m.VirtualFramebufferCount; m.PhysicalFramebufferCount]
                | _ -> []
            for samples in [1; 4] do
                use signature = runtime.CreateFramebufferSignature([DefaultSemantic.Colors, TextureFormat.Rgba8; DefaultSemantic.DepthStencil, TextureFormat.DepthComponent32], samples)
                let color = C4b(17uy, 83uy, 149uy, 255uy)
                use caller = runtime.CompileClear(signature, color)
                let mutable disposedCaller = 0
                let task = proxy<IRenderTask> (fun method args ->
                    match method.Name with
                    | "get_Runtime" -> box (Some recording)
                    | "Dispose" -> disposedCaller <- disposedCaller + 1; null
                    | _ -> invoke caller method args)
                let before = counts()
                let images = ResizeArray<PixImage * V2i>()
                for size in [V2i(13, 9); V2i(7, 5)] do
                    for repeat in 1 .. 4 do
                        let context = $"samples={samples}, size={size}, capture={repeat}"
                        let image = Aardvark.Application.Screenshot.renderToImage samples size task
                        images.Add(image, size)
                        completed <- completed + 1
                        checkImage image size color context
                        Expect.equal (counts()) before (context + ": backend temporary resource counts return to baseline")
                        Expect.equal disposedCaller 0 (context + ": borrowed task remains live")
                        Expect.equal (buffers.Count, textures.Count, framebuffers.Count, clearDisposals.Count) (2 * completed, completed, completed, completed) (context + ": temporary acquisitions per capture")
                        for attempts in clearDisposals do Expect.equal attempts.[0] 1 (context + ": compiled clear disposed once")
                        for buffer in buffers do Expect.equal buffer.Handle 0UL (context + ": no live temporary renderbuffer")
                        for texture in textures do Expect.equal texture.Handle 0UL (context + ": no live resolve texture")
                        for resource in Seq.concat [buffers |> Seq.map box; textures |> Seq.map box; framebuffers |> Seq.map box] do
                            match resource with
                            | :? Aardvark.Rendering.Vulkan.Resource as resource -> Expect.equal resource.ReferenceCount 0 (context + ": no temporary resource reference remains")
                            | _ -> ()
                for image, size in images do checkImage image size color "Retained downloaded image"

    let tests (target : TestTarget) =
        [
            if target = TestTarget.Cpu then
                "Successful captures retain images and release temporaries", Cases.successful
                "Setup failures release acquired temporaries",               Cases.setupFailures
                "Execution failures release acquired temporaries",           Cases.executionFailures
                "Disposal failures retain scoped unwind semantics",          Cases.disposalFailures
            if target.IsGpu then
                "Repeated captures release temporary resources", Cases.repeatedGpu
        ]
        |> prepareCases "Screenshots" target
