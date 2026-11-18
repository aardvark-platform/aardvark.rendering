namespace Aardvark.Rendering.Tests.Compute

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open FSharp.Data.Adaptive
open Expecto
open System

module MutableInputBinding =

    module Cases =
        open FShade.GLSL

        let private emptyInterface =
            { inputs = []
              outputs = []
              samplers = MapExt.empty
              images = MapExt.empty
              storageBuffers = MapExt.empty
              uniformBuffers = MapExt.empty
              accelerationStructures = MapExt.empty
              shaders = Unchecked.defaultof<_>
              samplerStates = MapExt.empty
              textures = MapExt.empty }

        let private createDummyShader (buffers : string list) (runtime : IRuntime) =
            let buffers =
                buffers
                |> List.map (fun name -> name, Unchecked.defaultof<_>)
                |> MapExt.ofList

            { new IComputeShader with
                member x.Runtime = runtime
                member x.LocalSize = V3i.One
                member x.Interface = { emptyInterface with storageBuffers = buffers }
                member x.Dispose() = () }

        let private flushTest'<'T when 'T : equality> (isBuffer : bool) (value1 : obj) (test1 : obj) (value2 : obj) (test2 : obj) (runtime : IRuntime) =
            use shader = createDummyShader (if isBuffer then ["test"] else []) runtime
            use inputs = runtime.CreateInputBinding(shader)

            inputs.["test"] <- value1

            let aval =
                let v = inputs.TryGetValue("test").Value
                Expect.equal v.ContentType typeof<'T> "Unexpected aval content type"
                unbox<aval<'T>> v

            Expect.equal (aval.GetValue()) Unchecked.defaultof<'T> "Unexpected default value"

            inputs.Flush()

            Expect.equal (aval.GetValue()) (unbox<'T> test1) "Unexpected value after first flush"

            inputs.["test"] <- value2
            inputs.Flush()

            Expect.equal (aval.GetValue()) (unbox<'T> test2) "Unexpected value after second flush"

        let private flushTest<'T when 'T : equality> (value1 : obj) (value2 : obj) =
            flushTest'<'T> false value1 value1 value2 value2

        let flush =
            flushTest<int> 42 64

        let typeSafety (runtime : IRuntime) =
            use shader = createDummyShader [] runtime
            use inputs = runtime.CreateInputBinding(shader)

            inputs.["test"] <- 54
            Expect.equal (inputs.TryGetValue("test").Value.ContentType) typeof<int32> "Unexpected aval content type"

            Expect.throwsT<ArgumentException> (fun _ ->
                inputs.["test"] <- 54.0
            ) "Type check failed"

        let buffers (runtime : IRuntime) =
            use backendBuffer = runtime.CreateBuffer(128UL)
            let array = [| 12; 13 |]
            let arrayBuffer = ArrayBuffer array
            runtime |> flushTest'<IBuffer> true backendBuffer backendBuffer arrayBuffer arrayBuffer
            runtime |> flushTest'<IBuffer> true backendBuffer backendBuffer array arrayBuffer
            runtime |> flushTest'<IBuffer> true array arrayBuffer backendBuffer backendBuffer

        let textures (runtime : IRuntime) =
            use backendTexture = runtime.CreateTexture1D(42, TextureFormat.Rgba8)
            let fileTexture = FileTexture("/dev/null")
            runtime |> flushTest<ITexture> backendTexture fileTexture

        let textureLevels (runtime : IRuntime) =
            use backendTexture = runtime.CreateTexture1D(42, TextureFormat.Rgba8)
            let subres = backendTexture.[TextureAspect.Color, 0, 0]
            let level = backendTexture.[TextureAspect.Color, 0]
            runtime |> flushTest<ITextureLevel> subres level

        let private metadataShader buffers =
            let buffers = buffers |> List.map (fun name -> name, Unchecked.defaultof<_>) |> MapExt.ofList
            { new IComputeShader with
                member _.Runtime = failtest "Metadata-only shader must not access a runtime"
                member _.LocalSize = V3i.One
                member _.Interface = { emptyInterface with storageBuffers = buffers }
                member _.Dispose() = () }

        let private createBinding shader =
            (Unchecked.defaultof<IComputeRuntime>).CreateInputBinding(shader)

        let private value<'T> (inputs : MutableComputeInputBinding) name =
            inputs.TryGetValue(name).Value |> unbox<aval<'T>> |> AVal.force

        let private subscribe kind (input : aval<int>) action =
            if kind = "marking" then input.AddMarkingCallback action
            else input.AddCallback(fun _ -> action())

        let private callbackQueue kind =
            let errors = ResizeArray<string>()
            for destination in ["source"; "existing"; "created"] do
                for initial in [0; 17; -9] do
                    for writes in [1; 3] do
                        use shader = metadataShader []
                        use inputs = createBinding shader
                        inputs.["source"] <- initial
                        inputs.["existing"] <- 5
                        inputs.Flush()
                        let source = inputs.TryGetValue("source").Value |> unbox<aval<int>>
                        source |> AVal.force |> ignore
                        let context = $"{kind}, destination={destination}, initial={initial}, writes={writes}"
                        let mutable armed = false
                        let mutable callbacks = 0
                        use subscription = subscribe kind source (fun () ->
                            if armed then
                                armed <- false
                                callbacks <- callbacks + 1
                                for i in 1 .. writes do inputs.[destination] <- initial + 100 + i)
                        armed <- true
                        inputs.["source"] <- initial + 1
                        inputs.Flush()
                        Expect.equal callbacks 1 (context + ": callback reached")
                        let before = if destination = "source" then initial + 1 elif destination = "existing" then 5 else 0
                        Expect.equal (value<int> inputs destination) before (context + ": callback assignment remains unapplied")
                        inputs.Flush()
                        let actual = value<int> inputs destination
                        let expected = initial + 100 + writes
                        if actual <> expected then errors.Add $"{context}: next Flush expected {expected}, actual {actual}"
                        inputs.Flush()
                        Expect.equal (value<int> inputs destination) actual (context + ": empty Flush is inert")
                        Expect.equal callbacks 1 (context + ": callback does not recursively apply queued values")
            Expect.isTrue (errors.Count = 0) (String.concat "\n" errors)

        let markingCallbacks() = callbackQueue "marking"
        let valueCallbacks() = callbackQueue "value"

        [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
        let private throwCallback (error : exn) = raise error

        let callbackFailures() =
            for kind in ["marking"; "value"] do
                for destination in ["source"; "existing"; "created"] do
                    use shader = metadataShader []
                    use inputs = createBinding shader
                    inputs.["source"] <- 1
                    inputs.["existing"] <- 5
                    inputs.Flush()
                    let source = inputs.TryGetValue("source").Value |> unbox<aval<int>>
                    source |> AVal.force |> ignore
                    let context = $"{kind}, destination={destination}"
                    let error = InvalidOperationException("callback failure")
                    let mutable armed = false
                    use subscription = subscribe kind source (fun () ->
                        if armed then
                            armed <- false
                            inputs.[destination] <- 21
                            inputs.[destination] <- 22
                            throwCallback error)
                    armed <- true
                    inputs.["source"] <- 2
                    let running = Transaction.Running
                    let actual =
                        try
                            try inputs.Flush(); None
                            with error -> Some error
                        finally
                            // A throwing marking callback can leave the library's ambient transaction set.
                            Transaction.Running <- running
                    Expect.isTrue (actual |> Option.exists (fun actual -> obj.ReferenceEquals(actual, error))) (context + ": original callback exception propagates")
                    Expect.stringContains error.StackTrace "throwCallback" (context + ": original throw stack")
                    subscription.Dispose()
                    Expect.equal (value<int> inputs "source") 2 (context + ": applied batch remains applied")
                    inputs.Flush()
                    Expect.equal (value<int> inputs destination) 22 (context + ": callback's last assignment survives failure")
                    inputs.Flush()
                    Expect.equal (value<int> inputs destination) 22 (context + ": no replay after recovery")

        let sequentialControls() =
            use shader = metadataShader ["buffer"]
            use inputs = createBinding shader
            Expect.isTrue (obj.ReferenceEquals(inputs.Shader, shader)) "Binding retains its shader"
            Expect.isTrue (inputs.TryGetValue("missing").IsNone) "Unknown input has no adaptive value"
            inputs.Flush()
            inputs.["scalar"] <- 1
            inputs.["scalar"] <- 2
            let scalar = inputs.TryGetValue("scalar").Value |> unbox<aval<int>>
            Expect.equal (AVal.force scalar) 0 "Assignments are deferred until Flush"
            inputs.Flush()
            Expect.equal (AVal.force scalar) 2 "Sequential assignments use the last value"
            let mutable callbacks = 0
            use subscription = scalar.AddCallback(fun _ -> callbacks <- callbacks + 1)
            for _ in 1 .. 3 do inputs.Flush()
            Expect.equal callbacks 1 "Empty Flush does not notify"
            inputs.["scalar"] <- 3
            inputs.Flush()
            Expect.equal (AVal.force scalar) 3 "Later sequential assignment is applied"
            Expect.equal callbacks 2 "Changed sequential value notifies once"
            Expect.isTrue (obj.ReferenceEquals(scalar, inputs.TryGetValue("scalar").Value)) "Adaptive identity stays stable"
            Expect.throwsT<ArgumentException> (fun () -> inputs.["scalar"] <- 3.0) "Type mismatch is rejected"
            let nullError =
                try inputs.["scalar"] <- null; None
                with :? ArgumentNullException as error -> Some error
            Expect.equal (nullError |> Option.map _.ParamName) (Some "value") "Null values retain their parameter error"
            inputs.Flush()
            Expect.equal (AVal.force scalar) 3 "Rejected assignments do not change pending values"

            let first, second = [|1; 2|], [|7; 8; 9|]
            inputs.["buffer"] <- first
            inputs.["array"] <- first
            inputs.Flush()
            Expect.equal (inputs.TryGetValue("buffer").Value.ContentType) typeof<IBuffer> "Storage arrays use the buffer base type"
            let buffer = value<IBuffer> inputs "buffer" :?> ArrayBuffer
            Expect.isTrue (obj.ReferenceEquals(buffer.Data, first)) "Storage array wrapping retains its data"
            Expect.isTrue (obj.ReferenceEquals(value<int[]> inputs "array", first)) "Ordinary arrays are not wrapped"
            inputs.["buffer"] <- second
            inputs.Flush()
            Expect.isTrue (obj.ReferenceEquals((value<IBuffer> inputs "buffer" :?> ArrayBuffer).Data, second)) "Later storage arrays retain wrapping"

            for kind in ["marking"; "value"] do
                let mutable armed = false
                use latest = subscribe kind scalar (fun () ->
                    if armed then
                        armed <- false
                        inputs.["scalar"] <- 40)
                AVal.force scalar |> ignore
                armed <- true
                inputs.["scalar"] <- value<int> inputs "scalar" + 1
                inputs.Flush()
                inputs.["scalar"] <- 50
                inputs.Flush()
                Expect.equal (AVal.force scalar) 50 $"{kind}: caller assignment supersedes callback assignment"

    let tests (target: TestTarget) =
        if target = TestTarget.Cpu then
            [
                "Marking callbacks retain queued values",        Cases.markingCallbacks
                "Value callbacks retain queued values",          Cases.valueCallbacks
                "Callback failures preserve queued assignments", Cases.callbackFailures
                "Sequential type buffer and empty controls",     Cases.sequentialControls
            ]
            |> prepareCasesCpu "Mutable input binding" target
        else
            [
                "Flush",          Cases.flush
                "Type safety",    Cases.typeSafety
                "Buffers",        Cases.buffers
                "Textures",       Cases.textures
                "Texture levels", Cases.textureLevels
            ]
            |> prepareCasesCompute "Mutable input binding" target