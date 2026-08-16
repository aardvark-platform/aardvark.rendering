namespace Aardvark.Rendering.Tests

open System
open System.Collections.Generic
open System.Reflection
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Raytracing
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Raytracing
open Aardvark.Application
open Expecto
open FSharp.Data.Adaptive

module ``Managed Pool Tests`` =

    type RecordingBuffer(runtime : IBufferRuntime, size : uint64) =
        let mutable name = null

        interface IBackendBuffer with
            member _.Runtime = runtime
            member _.Handle = 0UL
            member this.Buffer = this
            member _.Offset = 0UL
            member _.SizeInBytes = size
            member _.Name with get() = name and set value = name <- value
            member _.Dispose() = ()

    [<AllowNullLiteral>]
    type RuntimeState() =
        let mutable uploads = 0
        let mutable failUpload = 0

        member _.FailAfter(additionalUploads : int) =
            failUpload <- uploads + additionalUploads

        member _.ClearFailure() =
            failUpload <- 0

        member _.Upload() =
            uploads <- uploads + 1
            if uploads = failUpload then
                raise <| InvalidOperationException(sprintf "injected upload failure %d" uploads)

    [<AllowNullLiteral>]
    type RuntimeProxy() =
        inherit DispatchProxy()

        member val State : RuntimeState = null with get, set

        override this.Invoke(methodInfo : MethodInfo, args : obj[]) =
            match methodInfo.Name with
            | "get_DebugLabelsEnabled" -> box false
            | "get_SupportsRaytracing" -> box true
            | "CreateBuffer" ->
                let runtime = box this :?> IRuntime
                new RecordingBuffer(runtime, unbox<uint64> args.[0]) :> IBackendBuffer |> box
            | "Upload" ->
                this.State.Upload()
                box ()
            | "Copy"
            | "Download" -> box ()
            | "DownloadAsync" -> box (fun () -> ())
            | name -> failwithf "Unexpected mock runtime call: %s" name

    type CountingBufferResource(data : Array) =
        inherit AdaptiveResource<IBuffer>()

        let mutable creates = 0
        let mutable destroys = 0

        member _.Creates = creates
        member _.Destroys = destroys

        override _.Create() = creates <- creates + 1
        override _.Destroy() = destroys <- destroys + 1
        override _.Compute(_, _) = ArrayBuffer(data) :> IBuffer

    [<AutoOpen>]
    module private Helpers =

        let vertexSemantic = Sym.ofString "PoolVertex"
        let instanceSemantic = Sym.ofString "PoolInstance"
        let faceSemantic = Sym.ofString "PoolFace"
        let geometrySemantic = Sym.ofString "PoolGeometry"

        let createRuntime() =
            let state = RuntimeState()
            let runtime = DispatchProxy.Create<IRuntime, RuntimeProxy>()
            (box runtime :?> RuntimeProxy).State <- state
            runtime, state

        let dictionary (entries : seq<'K * 'V>) =
            let result = Dictionary<'K, 'V>()
            for key, value in entries do result.[key] <- value
            result

        let positions (tag : int) =
            [| V3f(float32 tag, 0.0f, 0.0f); V3f(float32 tag, 1.0f, 0.0f); V3f(float32 tag, 0.0f, 1.0f) |]

        let rasterSignature =
            { IndexType = typeof<int>
              VertexAttributeTypes = Map.ofList [vertexSemantic, typeof<V3f>]
              InstanceAttributeTypes = Map.ofList [instanceSemantic, typeof<int>] }

        let rasterGeometry vertex instance indices =
            AdaptiveGeometry(
                3, 3, ValueSome indices,
                dictionary [vertexSemantic, vertex],
                dictionary [instanceSemantic, instance]
            )

        let validRasterGeometry (tag : int) =
            rasterGeometry
                (BufferView.ofArray (positions tag))
                (AVal.constant tag :> IAdaptiveValue)
                (BufferView.ofArray [| 0; 1; 2 |])

        let invalidRasterGeometry stage (tag : int) =
            let vertex =
                if stage = "vertex" then BufferView.ofArray [| Guid.NewGuid(); Guid.NewGuid(); Guid.NewGuid() |]
                else BufferView.ofArray (positions tag)

            let instance =
                if stage = "instance" then AVal.constant (Guid.NewGuid()) :> IAdaptiveValue
                else AVal.constant tag :> IAdaptiveValue

            let indices =
                if stage = "index" then BufferView.ofArray [| Guid.NewGuid(); Guid.NewGuid(); Guid.NewGuid() |]
                else BufferView.ofArray [| 0; 1; 2 |]

            rasterGeometry vertex instance indices

        let traceSignature =
            { IndexType = IndexType.Int32
              VertexAttributeTypes = Map.ofList [vertexSemantic, typeof<V3f>]
              FaceAttributeTypes = Map.ofList [faceSemantic, typeof<int>]
              GeometryAttributeTypes = Map.ofList [geometrySemantic, typeof<int>]
              InstanceAttributeTypes = Map.ofList [instanceSemantic, typeof<int>] }

        let traceObject (tag : int) =
            let attributes = SymbolDict<Array>()
            attributes.[DefaultSemantic.Positions] <- positions tag
            let geometry = IndexedGeometry(IndexedGeometryMode.TriangleList, [| 0; 1; 2 |], attributes, SymbolDict<obj>())

            geometry
            |> TraceObject.ofIndexedGeometry GeometryFlags.Opaque Trafo3d.Identity
            |> TraceObject.vertexAttribute (vertexSemantic, BufferView.ofArray (positions tag))
            |> TraceObject.faceAttribute (faceSemantic, BufferView.ofArray [| tag |])
            |> TraceObject.geometryAttribute (geometrySemantic, ([tag] : int list))
            |> TraceObject.instanceAttribute (instanceSemantic, AVal.constant tag)

        let expectFailure action =
            Expect.throws (fun _ -> action()) "Expected the processing stage to fail"

    module Cases =

        let rasterConversionRollback() =
            let runtime, _ = createRuntime()
            use pool = new ManagedPool(runtime, rasterSignature)

            for stage in [| "vertex"; "instance"; "index" |] do
                expectFailure (fun () -> pool.Add(invalidRasterGeometry stage 1) |> ignore)
                Expect.equal pool.Count 0 (sprintf "%s failure changed the pool count" stage)

                use call = pool.Add(validRasterGeometry 20)
                Expect.equal call.Call.BaseVertex 0 (sprintf "%s failure leaked the vertex range" stage)
                Expect.equal call.Call.FirstInstance 0 (sprintf "%s failure leaked the instance range" stage)
                Expect.equal call.Call.FirstIndex 0 (sprintf "%s failure leaked the index range" stage)

        let rasterSharedRollback() =
            let runtime, state = createRuntime()
            use pool = new ManagedPool(runtime, rasterSignature)
            let geometry = validRasterGeometry 1
            use existing = pool.Add geometry

            state.FailAfter 2
            expectFailure (fun () -> pool.Add geometry |> ignore)
            state.ClearFailure()

            Expect.equal pool.Count 1 "A failed shared addition changed existing content"
            use duplicate = pool.Add geometry
            Expect.equal duplicate.Call existing.Call "Shared layout reference counts were not restored"
            Expect.equal pool.Count 2 "The existing object was disturbed"

        let rasterAdaptiveRollback() =
            let runtime, _ = createRuntime()
            use pool = new ManagedPool(runtime, rasterSignature)
            let source = CountingBufferResource(positions 1)
            let vertex = BufferView(source :> aval<IBuffer>, typeof<V3f>)
            let bad = rasterGeometry vertex (AVal.constant (Guid.NewGuid()) :> IAdaptiveValue) (BufferView.ofArray [| 0; 1; 2 |])

            expectFailure (fun () -> pool.Add bad |> ignore)
            Expect.equal source.Creates 1 "The adaptive buffer was not acquired"
            Expect.equal source.Destroys 1 "Rollback retained the adaptive buffer dependency"

        let traceStageRollback() =
            for stage, upload in [| "geometry attribute", 1; "instance attribute", 2; "vertex attribute", 3; "index data", 4; "face attribute", 5; "geometry record", 6 |] do
                let runtime, state = createRuntime()
                use pool = new ManagedTracePool(runtime, traceSignature)
                let obj = traceObject 1

                state.FailAfter upload
                expectFailure (fun () -> pool.Add obj |> ignore)
                Expect.equal pool.Count 0 (sprintf "%s failure changed the pool count" stage)
                state.ClearFailure()

                use added = pool.Add(traceObject 20)
                Expect.equal added.Index 0 (sprintf "%s failure leaked the geometry range" stage)

        let traceRepeatedAndSharedRollback() =
            let runtime, state = createRuntime()
            use pool = new ManagedTracePool(runtime, traceSignature)
            let obj = traceObject 1
            use existing = pool.Add obj

            for _ = 1 to 3 do
                state.FailAfter 6
                expectFailure (fun () -> pool.Add obj |> ignore)
                state.ClearFailure()
                Expect.equal pool.Count 1 "A repeated failure changed existing content"

            use duplicate = pool.Add obj
            Expect.equal existing.Index 0 "The existing object moved"
            Expect.equal duplicate.Index 1 "Repeated failures leaked the next geometry range"
            Expect.equal pool.Count 2 "A failed trace addition disturbed the existing object"

        let traceAdaptiveRollback() =
            let runtime, state = createRuntime()
            use pool = new ManagedTracePool(runtime, traceSignature)
            let source = CountingBufferResource(positions 1)
            let obj = traceObject 1
            obj.VertexAttributes.[0].[vertexSemantic] <- BufferView(source :> aval<IBuffer>, typeof<V3f>)

            state.FailAfter 4
            expectFailure (fun () -> pool.Add obj |> ignore)
            Expect.equal source.Creates 1 "The trace vertex dependency was not acquired"
            Expect.equal source.Destroys 1 "Trace rollback retained the adaptive dependency"

        let integrationRaster (runtime : IRuntime) =
            use pool = new ManagedPool(runtime, rasterSignature)
            expectFailure (fun () -> pool.Add(invalidRasterGeometry "instance" 1) |> ignore)
            Expect.equal pool.Count 0 "Failed integration addition changed the pool count"
            use call = pool.Add(validRasterGeometry 2)
            Expect.equal call.Call.BaseVertex 0 "Failed integration addition leaked a vertex range"

        let integrationTrace (runtime : IRuntime) =
            if not runtime.SupportsRaytracing then skiptest "Ray tracing is not supported"

            use pool = new ManagedTracePool(runtime, traceSignature)
            let bad = traceObject 1
            bad.FaceAttributes.[0].[faceSemantic] <- BufferView.ofArray [| Guid.NewGuid() |]
            expectFailure (fun () -> pool.Add bad |> ignore)
            Expect.equal pool.Count 0 "Failed trace integration addition changed the pool count"
            use added = pool.Add(traceObject 2)
            Expect.equal added.Index 0 "Failed trace integration addition leaked a geometry range"

    [<Tests>]
    let tests =
        testList "Pools.ManagedPool.Rollback" [
            testCase "Raster conversion stages" Cases.rasterConversionRollback
            testCase "Raster shared layouts" Cases.rasterSharedRollback
            testCase "Raster adaptive dependencies" Cases.rasterAdaptiveRollback
            testCase "Trace processing stages" Cases.traceStageRollback
            testCase "Trace repeated and shared layouts" Cases.traceRepeatedAndSharedRollback
            testCase "Trace adaptive dependencies" Cases.traceAdaptiveRollback
        ]


module ``Managed Pool Integration Tests`` =

    [<Tests>]
    let testsGL =
        prepareCases Backend.GL "[GL] Pools.ManagedPool.Integration" [
            "Raster managed buffers", ``Managed Pool Tests``.Cases.integrationRaster
        ]

    [<Tests>]
    let testsVulkan =
        prepareCases Backend.Vulkan "[Vulkan] Pools.ManagedPool.Integration" [
            "Raster managed buffers", ``Managed Pool Tests``.Cases.integrationRaster
            "Ray-tracing managed buffers", ``Managed Pool Tests``.Cases.integrationTrace
        ]
