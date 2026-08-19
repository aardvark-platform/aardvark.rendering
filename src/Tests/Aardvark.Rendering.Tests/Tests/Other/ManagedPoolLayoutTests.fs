namespace Aardvark.Rendering.Tests

open System
open System.Collections.Generic
open System.Reflection
open System.Runtime.InteropServices
open Aardvark.Application
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Raytracing
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Raytracing
open Expecto
open FSharp.Data.Adaptive

module ``Managed Pool Layout Tests`` =

    type private LayoutManager<'T when 'T : equality>(comparer : IEqualityComparer<'T>) =
        let managerType =
            let assembly = typeof<ManagedPool>.Assembly
            let utilities = assembly.GetType("Aardvark.SceneGraph.ManagedPoolUtilities")
            Expect.isNotNull utilities "Failed to get managed-pool utilities"

            let generic = utilities.GetNestedType("LayoutManager`1", BindingFlags.NonPublic)
            Expect.isNotNull generic "Failed to get layout-manager type"
            generic.MakeGenericType [| typeof<'T> |]

        let instance = Activator.CreateInstance(managerType, [| comparer :> obj |])
        let methodFlags = BindingFlags.Instance ||| BindingFlags.Public ||| BindingFlags.NonPublic
        let alloc = managerType.GetMethod("Alloc", methodFlags)
        let tryAlloc = managerType.GetMethod("TryAlloc", methodFlags)
        let free = managerType.GetMethod("Free", methodFlags)

        new () = LayoutManager<'T>(EqualityComparer<'T>.Default)

        member _.Alloc(key : 'T, size : int) =
            alloc.Invoke(instance, [| box key; box size |]) |> unbox<managedptr>

        member _.TryAlloc(key : 'T, size : int) =
            tryAlloc.Invoke(instance, [| box key; box size |]) |> unbox<bool * managedptr>

        member _.Free(value : managedptr) =
            free.Invoke(instance, [| box value |]) |> ignore

    [<Struct>]
    type Upload =
        { Offset : uint64
          Size : uint64
          Data : byte[] }

    type private RecordingBuffer(runtime : IBufferRuntime, size : uint64) =
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
        let uploads = ResizeArray<Upload>()

        member _.Uploads = uploads.ToArray()

        member _.RecordUpload(source : nativeint, offset : uint64, size : uint64) =
            let data = Array.zeroCreate<byte> (int size)
            Marshal.Copy(source, data, 0, data.Length)
            uploads.Add { Offset = offset; Size = size; Data = data }

    [<AllowNullLiteral>]
    type private RuntimeProxy() =
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
                this.State.RecordUpload(unbox args.[0], unbox args.[2], unbox args.[3])
                box ()
            | "Copy"
            | "Download" -> box ()
            | "DownloadAsync" -> box (fun () -> ())
            | name -> failwithf "Unexpected mock runtime call: %s" name

    [<AutoOpen>]
    module Helpers =

        let vertexSemantic = Sym.ofString "ManagedPoolLayoutVertex"

        let dictionary (entries : seq<'K * 'V>) =
            let result = Dictionary<'K, 'V>()
            for key, value in entries do result.[key] <- value
            result :> IDictionary<'K, 'V>

        let createRuntime() =
            let state = RuntimeState()
            let runtime = DispatchProxy.Create<IRuntime, RuntimeProxy>()
            (box runtime :?> RuntimeProxy).State <- state
            runtime, state

        let positions =
            [| V3f(0.0f, 0.0f, 0.0f)
               V3f(1.0f, 0.0f, 0.0f)
               V3f(0.0f, 1.0f, 0.0f)
               V3f(2.0f, 0.0f, 0.0f)
               V3f(3.0f, 0.0f, 0.0f)
               V3f(2.0f, 1.0f, 0.0f) |]

        let indices = [| 0; 1; 2; 3; 4; 5 |]

        let rasterSignature =
            { IndexType = typeof<int>
              VertexAttributeTypes = Map.ofList [vertexSemantic, typeof<V3f>]
              InstanceAttributeTypes = Map.empty }

        let traceSignature =
            { IndexType = IndexType.Int32
              VertexAttributeTypes = Map.ofList [vertexSemantic, typeof<V3f>]
              FaceAttributeTypes = Map.empty
              GeometryAttributeTypes = Map.empty
              InstanceAttributeTypes = Map.empty }

        let createRasterGeometry count attributes =
            AdaptiveGeometry(
                count, count, ValueNone,
                attributes,
                dictionary Seq.empty
            )

        let createTraceObject count (indexBuffer : aval<IBuffer>) (vertexBuffer : aval<IBuffer>) attributes =
            let vertices = AdaptiveVertexData(vertexBuffer, uint32 count, 0UL, uint64 sizeof<V3f>)
            let indices = AdaptiveIndexData(IndexType.Int32, indexBuffer, uint32 count, 0UL)
            let mesh = AdaptiveTriangleMesh(vertices, indices, Trafo3d.Identity)

            AdaptiveTraceGeometry.Triangles [| mesh |]
            |> TraceObject.ofAdaptiveGeometry
            |> TraceObject.vertexAttributes attributes

        let expectSeparateRanges (left : managedptr) (right : managedptr) message =
            let leftMin = int64 left.Offset
            let leftMax = leftMin + int64 left.Size
            let rightMin = int64 right.Offset
            let rightMax = rightMin + int64 right.Size
            Expect.isTrue (leftMax <= rightMin || rightMax <= leftMin) message

        let expectUpload (state : RuntimeState) size offset message =
            state.Uploads
            |> Array.exists (fun upload -> upload.Size = size && upload.Offset = offset)
            |> fun found -> Expect.isTrue found message

        let expectGeometryUpload (state : RuntimeState) offset firstIndex baseVertex message =
            let upload =
                state.Uploads
                |> Array.tryFind (fun upload -> upload.Size = uint64 sizeof<TraceGeometryInfo> && upload.Offset = offset)

            match upload with
            | Some upload ->
                Expect.equal (BitConverter.ToInt32(upload.Data, 0)) firstIndex $"{message}: unexpected index offset"
                Expect.equal (BitConverter.ToInt32(upload.Data, 4)) baseVertex $"{message}: unexpected vertex offset"
            | None ->
                failtest $"{message}: geometry upload not found"

        let download<'T when 'T : unmanaged> (buffer : aval<IBuffer>) =
            (buffer.GetValue() :?> IBackendBuffer).Coerce<'T>().Download()

        let rasterPoolSizeAware (runtime : IRuntime) =
            use pool = new ManagedPool(runtime, rasterSignature)
            let view = BufferView.ofArray positions
            let attributes = dictionary [vertexSemantic, view]

            use small = pool.Add(createRasterGeometry 3 attributes)
            use large = pool.Add(createRasterGeometry 6 attributes)
            use shared = pool.Add(createRasterGeometry 3 attributes)

            Expect.equal small.Call.BaseVertex 0 "Unexpected first vertex offset"
            Expect.equal large.Call.BaseVertex 3 "Different vertex counts shared a layout"
            Expect.equal shared.Call.BaseVertex small.Call.BaseVertex "Equal content and size did not share the layout"
            Expect.equal small.Call.FirstIndex 0 "Unexpected first index offset"
            Expect.equal large.Call.FirstIndex 3 "Different index counts overlapped"
            Expect.equal shared.Call.FirstIndex small.Call.FirstIndex "Equal generated indices did not share the layout"

            let vertexData =
                match pool.VertexAttributes.TryGetAttribute vertexSemantic with
                | ValueSome value -> download<V3f> value.Buffer
                | ValueNone -> failtest "Missing pooled vertex attribute"

            Expect.isGreaterThanOrEqual vertexData.Length 9 "Pooled vertex buffer is too small"
            Expect.sequenceEqual vertexData.[0..2] positions.[0..2] "Unexpected small vertex upload"
            Expect.sequenceEqual vertexData.[3..8] positions "Unexpected large vertex upload"

            let indexData = download<int> pool.IndexBuffer.Buffer
            Expect.isGreaterThanOrEqual indexData.Length 9 "Pooled index buffer is too small"
            Expect.sequenceEqual indexData.[0..2] indices.[0..2] "Unexpected small generated indices"
            Expect.sequenceEqual indexData.[3..8] indices "Unexpected large generated indices"

        let tracePoolSizeAware (forceAccelerationStructures : bool) (runtime : IRuntime) =
            if forceAccelerationStructures && not runtime.SupportsRaytracing then
                skiptest "Ray tracing is not supported"

            use pool = new ManagedTracePool(runtime, traceSignature)
            let indexBuffer = AVal.constant (ArrayBuffer indices :> IBuffer)
            let vertexBuffer = AVal.constant (ArrayBuffer positions :> IBuffer)
            let attributes = dictionary [vertexSemantic, BufferView.ofArray positions]

            use small = pool.Add(createTraceObject 3 indexBuffer vertexBuffer attributes)
            use large = pool.Add(createTraceObject 6 indexBuffer vertexBuffer attributes)
            use shared = pool.Add(createTraceObject 3 indexBuffer vertexBuffer attributes)

            Expect.equal small.Index 0 "Unexpected first geometry offset"
            Expect.equal large.Index 1 "Different logical counts shared a geometry record"
            Expect.equal shared.Index 2 "Trace object geometry records overlap"

            if forceAccelerationStructures then
                small.Geometry.GetValue() |> ignore
                large.Geometry.GetValue() |> ignore

            let indexData = download<int> pool.IndexBuffer
            Expect.isGreaterThanOrEqual indexData.Length 9 "Trace index buffer is too small"
            Expect.sequenceEqual indexData.[0..2] indices.[0..2] "Unexpected small trace index upload"
            Expect.sequenceEqual indexData.[3..8] indices "Unexpected large trace index upload"

            let vertexData = download<V3f> (pool.GetVertexAttribute vertexSemantic)
            Expect.isGreaterThanOrEqual vertexData.Length 9 "Trace vertex buffer is too small"
            Expect.sequenceEqual vertexData.[0..2] positions.[0..2] "Unexpected small trace vertex upload"
            Expect.sequenceEqual vertexData.[3..8] positions "Unexpected large trace vertex upload"

            let geometryData = download<TraceGeometryInfo> pool.GeometryBuffer
            Expect.isGreaterThanOrEqual geometryData.Length 3 "Trace geometry buffer is too small"
            Expect.equal geometryData.[0].FirstIndex 0 "Unexpected first trace index offset"
            Expect.equal geometryData.[0].BaseVertex 0 "Unexpected first trace vertex offset"
            Expect.equal geometryData.[1].FirstIndex 3 "Trace index ranges overlap"
            Expect.equal geometryData.[1].BaseVertex 3 "Trace vertex ranges overlap"
            Expect.equal geometryData.[2].FirstIndex geometryData.[0].FirstIndex "Same-size trace index layout was not shared"
            Expect.equal geometryData.[2].BaseVertex geometryData.[0].BaseVertex "Same-size trace vertex layout was not shared"

    module Cases =

        let allocSizeAware() =
            let manager = LayoutManager<string>()
            let small = manager.Alloc("shared", 3)
            let large = manager.Alloc("shared", 7)
            let smallAgain = manager.Alloc("shared", 3)
            let largeAgain = manager.Alloc("shared", 7)

            Expect.equal smallAgain small "Equal key and size did not share an allocation"
            Expect.equal largeAgain large "Equal key and size did not share an allocation"
            expectSeparateRanges small large "Equal keys with different sizes overlap"

            manager.Free small
            manager.Free large
            manager.Free largeAgain

            let largeWasNew, freshLarge = manager.TryAlloc("shared", 7)
            Expect.isTrue largeWasNew "Released large key remained registered"
            expectSeparateRanges smallAgain freshLarge "Fresh large range overlaps the retained small range"
            manager.Free freshLarge

            let replacementLarge = manager.Alloc("replacement-large", 7)
            expectSeparateRanges smallAgain replacementLarge "Replacement large range overlaps the retained small range"
            Expect.equal (int64 replacementLarge.Offset + int64 replacementLarge.Size) 10L "Released large capacity was not reused immediately"

            manager.Free smallAgain
            let smallWasNew, freshSmall = manager.TryAlloc("shared", 3)
            Expect.isTrue smallWasNew "Released small key remained registered"
            manager.Free freshSmall

            let replacementSmall = manager.Alloc("replacement-small", 3)
            expectSeparateRanges replacementLarge replacementSmall "Replacement small range overlaps the retained large range"
            let replacementEnd =
                max
                    (int64 replacementLarge.Offset + int64 replacementLarge.Size)
                    (int64 replacementSmall.Offset + int64 replacementSmall.Size)
            Expect.equal replacementEnd 10L "Released small capacity was not reused immediately"

            manager.Free replacementLarge
            manager.Free replacementSmall

        let tryAllocSizeAware() =
            let manager = LayoutManager<string>()
            let isSmallNew, small = manager.TryAlloc("shared", 4)
            let isSmallAgainNew, smallAgain = manager.TryAlloc("shared", 4)
            let isLargeNew, large = manager.TryAlloc("shared", 9)
            let isLargeAgainNew, largeAgain = manager.TryAlloc("shared", 9)

            Expect.isTrue isSmallNew "First small allocation was reported as shared"
            Expect.isFalse isSmallAgainNew "Second small allocation was reported as new"
            Expect.isTrue isLargeNew "First large allocation was reported as shared"
            Expect.isFalse isLargeAgainNew "Second large allocation was reported as new"
            Expect.equal smallAgain small "TryAlloc did not reuse equal key and size"
            Expect.equal largeAgain large "TryAlloc did not reuse equal key and size"
            expectSeparateRanges small large "TryAlloc ranges with different sizes overlap"

            manager.Free large
            manager.Free small
            manager.Free largeAgain
            manager.Free smallAgain

            let sharedIsNew, freshShared = manager.TryAlloc("shared", 4)
            Expect.isTrue sharedIsNew "Released TryAlloc key remained registered"
            manager.Free freshShared

            let isNew, reused = manager.TryAlloc("replacement", 4)
            Expect.isTrue isNew "Released range remained registered"
            Expect.equal reused.Offset 0n "TryAlloc did not reuse a released range"
            manager.Free reused

        let customComparer() =
            let comparer = StringComparer.OrdinalIgnoreCase :> IEqualityComparer<string>
            let manager = LayoutManager<string>(comparer)
            let first = manager.Alloc("Content", 5)
            let equal = manager.Alloc("content", 5)
            let differentSize = manager.Alloc("CONTENT", 8)

            Expect.equal equal first "Configured content comparer was not used"
            expectSeparateRanges first differentSize "Size was ignored with a custom comparer"

            manager.Free first
            manager.Free equal
            manager.Free differentSize

        let rasterMock() =
            let runtime, state = createRuntime()
            use pool = new ManagedPool(runtime, rasterSignature)
            let attributes = dictionary [vertexSemantic, BufferView.ofArray positions]

            use small = pool.Add(createRasterGeometry 3 attributes)
            use large = pool.Add(createRasterGeometry 6 attributes)
            use shared = pool.Add(createRasterGeometry 3 attributes)

            Expect.equal small.Call.BaseVertex 0 "Unexpected first raster vertex offset"
            Expect.equal large.Call.BaseVertex 3 "Raster vertex ranges with different counts overlap"
            Expect.equal shared.Call.BaseVertex small.Call.BaseVertex "Same-size raster layout was not shared"
            expectUpload state 36UL 0UL "Missing small raster vertex upload"
            expectUpload state 72UL 36UL "Large raster vertex upload aliases the small range"

        let traceMock() =
            let runtime, state = createRuntime()
            use pool = new ManagedTracePool(runtime, traceSignature)
            let indexBuffer = AVal.constant (ArrayBuffer indices :> IBuffer)
            let vertexBuffer = AVal.constant (ArrayBuffer positions :> IBuffer)
            let attributes = dictionary [vertexSemantic, BufferView.ofArray positions]

            use small = pool.Add(createTraceObject 3 indexBuffer vertexBuffer attributes)
            use large = pool.Add(createTraceObject 6 indexBuffer vertexBuffer attributes)
            use shared = pool.Add(createTraceObject 3 indexBuffer vertexBuffer attributes)

            Expect.equal small.Index 0 "Unexpected first trace geometry offset"
            Expect.equal large.Index 1 "Trace geometries with different counts overlap"
            Expect.equal shared.Index 2 "Trace object geometry records overlap"
            expectUpload state 12UL 0UL "Missing small trace index upload"
            expectUpload state 24UL 12UL "Large trace index upload aliases the small range"
            expectUpload state 36UL 0UL "Missing small trace vertex upload"
            expectUpload state 72UL 36UL "Large trace vertex upload aliases the small range"
            expectGeometryUpload state 0UL 0 0 "Small trace geometry"
            expectGeometryUpload state 20UL 3 3 "Large trace geometry"
            expectGeometryUpload state 40UL 0 0 "Shared trace geometry"

        let indexDataValueSemantics() =
            let buffer = ArrayBuffer indices :> IBuffer
            let first = IndexData(IndexType.Int32, buffer, 3u, 0UL)
            let equal = IndexData(IndexType.Int32, buffer, 3u, 0UL)
            let differentCount = IndexData(IndexType.Int32, buffer, 6u, 0UL)

            Expect.isTrue (first.Equals equal) "Equal immutable index descriptors compare unequal"
            Expect.equal (first.GetHashCode()) (equal.GetHashCode()) "Equal immutable index hashes differ"
            Expect.isFalse (first.Equals differentCount) "Immutable index equality ignores count"
            Expect.notEqual (first.GetHashCode()) (differentCount.GetHashCode()) "Immutable index hash ignores count"

            let adaptiveBuffer = AVal.constant buffer
            let adaptiveFirst = AdaptiveIndexData(IndexType.Int32, adaptiveBuffer, 3u, 0UL)
            let adaptiveEqual = AdaptiveIndexData(IndexType.Int32, adaptiveBuffer, 3u, 0UL)
            let adaptiveDifferentCount = AdaptiveIndexData(IndexType.Int32, adaptiveBuffer, 6u, 0UL)

            Expect.isTrue (adaptiveFirst.Equals adaptiveEqual) "Equal adaptive index descriptors compare unequal"
            Expect.equal (adaptiveFirst.GetHashCode()) (adaptiveEqual.GetHashCode()) "Equal adaptive index hashes differ"
            Expect.isFalse (adaptiveFirst.Equals adaptiveDifferentCount) "Adaptive index equality ignores count"
            Expect.notEqual (adaptiveFirst.GetHashCode()) (adaptiveDifferentCount.GetHashCode()) "Adaptive index hash ignores count"

    [<Tests>]
    let tests =
        testList "Pools.ManagedPool.Layout" [
            testCase "Alloc size-aware reuse and free order" Cases.allocSizeAware
            testCase "TryAlloc size-aware reuse and free order" Cases.tryAllocSizeAware
            testCase "Custom content comparer" Cases.customComparer
            testCase "Raster pool shared view with different counts" Cases.rasterMock
            testCase "Trace pool shared views with different counts" Cases.traceMock
            testCase "Index descriptor value semantics" Cases.indexDataValueSemantics
        ]


module ``Managed Pool Layout Integration Tests`` =

    [<Tests>]
    let testsGL =
        prepareCases Backend.GL "[GL] Pools.ManagedPool.Layout" [
            "Raster size-aware layout", ``Managed Pool Layout Tests``.Helpers.rasterPoolSizeAware
        ]

    [<Tests>]
    let testsVulkan =
        prepareCases Backend.Vulkan "[Vulkan] Pools.ManagedPool.Layout" [
            "Raster size-aware layout", ``Managed Pool Layout Tests``.Helpers.rasterPoolSizeAware
            "Trace size-aware layout", ``Managed Pool Layout Tests``.Helpers.tracePoolSizeAware true
        ]
