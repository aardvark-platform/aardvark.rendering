namespace Aardvark.Rendering.Tests.Compute

open Aardvark.Base
open Aardvark.GPGPU
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.Application
open Expecto

module ComputeSorting =

    module Cases =

        let bitonic (runtime : IRuntime) =
            use sorter = new BitonicSorter<float32>(runtime, <@ fun a b -> a < b @>)

            let input = Array.randomV4fs 1621 |> Array.map _.X
            let perm = sorter.CreatePermutation input

            let expected = input |> Array.sort
            let result = input |> Array.permute (fun i -> Array.findIndex ((=) i) perm)

            Expect.equal result expected "Result mismatch"

        let private radixSort (sort : RadixSort -> IBufferVector<'T> -> IBufferVector<'T>)
                              (generateData : int -> 'T[])
                              (runtime : IRuntime) =
            use radix = new RadixSort(runtime)

            let input = generateData 1621
            use inputBuffer = runtime.CreateBuffer(input)
            let inputVector = inputBuffer.SubVector(3, 7, 101)

            let outputVector = inputVector |> sort radix

            let result =
                outputVector.Buffer.Coerce<'T>().Download()
                |> Array.subvector outputVector.Origin outputVector.Delta outputVector.Count

            if outputVector.Buffer <> inputBuffer.Buffer then
                outputVector.Buffer.Dispose()

            let expected =
                input
                |> Array.subvector inputVector.Origin inputVector.Delta inputVector.Count
                |> Array.sort

            Expect.equal result expected "Result mismatch"

        let private radixSortOutOfPlace (sort : RadixSort -> IBufferVector<'T> -> IBufferVector<'T> -> unit) =
            radixSort (fun radix input ->
                let runtime = input.Buffer.Runtime
                let output = runtime.CreateBuffer<'T>(input.Count * 4).SubVector(3, 3, input.Count)
                sort radix input output
                output
            )

        let private radixSortPermute (permute : RadixSort -> IBufferVector<'T> -> IBufferVector<int> -> unit) =
            radixSort (fun radix input ->
                let runtime = input.Buffer.Runtime
                let output = runtime.CreateBuffer<'T>(input.Count)
                use permBuffer = runtime.CreateBuffer<int>(input.Count * 4)
                let permVector = permBuffer.SubVector(1, 3, input.Count)

                permute radix input permVector

                let data =
                    input.Buffer.Coerce<'T>().Download()
                    |> Array.subvector input.Origin input.Delta input.Count

                let perm =
                    permBuffer.Download()
                    |> Array.subvector permVector.Origin permVector.Delta permVector.Count

                let result = data |> Array.permute (fun i -> Array.findIndex ((=) i) perm)
                output.Upload(result)
                output
            )

        let radixSortInt32 = Array.randomInts |> radixSortOutOfPlace (fun r i o -> r.Sort(i, o))
        let radixSortUInt32 = (Array.random Rnd.uint32) |> radixSortOutOfPlace (fun r i o -> r.Sort(i, o))
        let radixSortFloat32 = (Array.random Rnd.float32) |> radixSortOutOfPlace (fun r i o -> r.Sort(i, o))

        let radixSortInPlaceInt32 = Array.randomInts |> radixSort (fun r i -> r.SortInPlace(i); i)
        let radixSortInPlaceUInt32 = (Array.random Rnd.uint32) |> radixSort (fun r i -> r.SortInPlace(i); i)
        let radixSortInPlaceFloat32 = (Array.random Rnd.float32) |> radixSort (fun r i -> r.SortInPlace(i); i)

        let radixSortPermuteInt32 = Array.randomInts |> radixSortPermute (fun r i p -> r.CreatePermutation(i, p))
        let radixSortPermuteUInt32 = (Array.random Rnd.uint32) |> radixSortPermute (fun r i p -> r.CreatePermutation(i, p))
        let radixSortPermuteFloat32 = (Array.random Rnd.float32) |> radixSortPermute (fun r i p -> r.CreatePermutation(i, p))

    let tests (backend : Backend) =
        [
            "Bitonic", Cases.bitonic

            "Radix sort out-of-place int32", Cases.radixSortInt32
            "Radix sort out-of-place uint32", Cases.radixSortUInt32
            "Radix sort out-of-place float32", Cases.radixSortFloat32

            "Radix sort in-place int32", Cases.radixSortInPlaceInt32
            "Radix sort in-place uint32", Cases.radixSortInPlaceUInt32
            "Radix sort in-place float32", Cases.radixSortInPlaceFloat32

            "Radix permute int32", Cases.radixSortPermuteInt32
            "Radix permute uint32", Cases.radixSortPermuteUInt32
            "Radix permute float32", Cases.radixSortPermuteFloat32
        ]
        |> prepareCases backend "Sorting"