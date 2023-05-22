namespace Aardvark.Rendering.Tests.Rendering

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.Application
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
              shaders = Unchecked.defaultof<_> }

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
            use backendBuffer = runtime.CreateBuffer(128n)
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

    let tests (backend : Backend) =
        [
            "Flush",          Cases.flush
            "Type safety",    Cases.typeSafety
            "Buffers",        Cases.buffers
            "Textures",       Cases.textures
            "Texture levels", Cases.textureLevels
        ]
        |> prepareCases backend "Mutable input binding"