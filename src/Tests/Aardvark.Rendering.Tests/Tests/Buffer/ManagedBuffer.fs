namespace Aardvark.Rendering.Tests.Buffer

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.Application
open FSharp.Data.Adaptive
open Expecto

module ManagedBuffer =

    module Cases =

        let private test<'T when 'T : unmanaged> (runtime: IRuntime) (action: IManagedBuffer<'T> -> unit) =
            let buffer = runtime.CreateManagedBuffer<'T>()
            buffer.Acquire()
            try action buffer
            finally buffer.Release()

        let set (runtime: IRuntime) =
            test<V4f> runtime (fun buffer ->
                let d1 = V4f(1, 2, 3, 4)
                let d3 = V4f(4, 3, 2, 1)

                buffer.Set(d1, 1UL)
                buffer.Set(d3, 3UL)
                let data = buffer.GetValue().Coerce<V4f>().Download()

                Expect.isGreaterThanOrEqual data.Length 4 "Unexpected size"
                Expect.equal data.[1] d1 "Unexpected data at index 1"
                Expect.equal data.[3] d3 "Unexpected data at index 3"
            )

        let setArray (runtime: IRuntime) =
            test<V4f> runtime (fun buffer ->
                let a = [| V4f.One; V4f.Half |]
                let b = [| V4f.IOII; V4f.IIIO; V4f.IIOO |]

                buffer.Set(a, Range1ul(0UL, 0UL))
                buffer.Set(a, Range1ul(1UL, 2UL))
                buffer.Set(b, Range1ul(5UL, 9UL)) // data length < range -> repeat values
                let data = buffer.GetValue().Coerce<V4f>().Download()

                Expect.isGreaterThanOrEqual data.Length 10 "Unexpected size"
                Expect.equal data.[0] a.[0] "Unexpected data at index 0"
                Expect.equal data.[1] a.[0] "Unexpected data at index 1"
                Expect.equal data.[2] a.[1] "Unexpected data at index 2"

                for i = 5 to 9 do
                    let j = (i - 5) % b.Length
                    Expect.equal data.[i] b.[j] $"Unexpected data at index {i}"
            )

        let add (runtime: IRuntime) =
            test<V4f> runtime (fun buffer ->
                let a = cval V4f.IIOO
                let b = cval V4d.Half

                let _  = buffer.Add(a, 1UL)
                let db = buffer.Add(b, 3UL)

                let check (a: V4f) (b: V4d) =
                    let data = buffer.GetValue().Coerce<V4f>().Download()
                    Expect.isGreaterThanOrEqual data.Length 4 "Unexpected size"
                    Expect.equal data.[1] a "Unexpected data at index 1"
                    Expect.equal data.[3] (V4f b) "Unexpected data at index 3"

                check a.Value b.Value

                // Changes should propagate
                transact (fun _ ->
                    a.Value <- a.Value + 1.0f
                    b.Value <- b.Value + 5.0
                )
                check a.Value b.Value

                // Dispose writer for b -> change to b should not propagate
                db.Dispose()
                let pb = b.Value
                transact (fun _ ->
                    a.Value <- a.Value * 2.0f
                    b.Value <- b.Value * 2.0
                )
                check a.Value pb
            )

        let addBuffer (runtime: IRuntime) =
            test<V4f> runtime (fun buffer ->
                let a = cval [| V4f.IIOO; V4f.IOIO |]
                let b = cval [| V4d.Half; V4d.OOII |]
                let ba = BufferView(a |> AVal.map (fun arr -> ArrayBuffer arr :> IBuffer), typeof<V4f>)
                let bb = BufferView(b |> AVal.map (fun arr -> ArrayBuffer arr :> IBuffer), typeof<V4d>)

                let _  = buffer.Add(ba, Range1ul(1UL, 2UL))
                let db = buffer.Add(bb, Range1ul(5UL, 6UL))

                let check (a: V4f[]) (b: V4d[]) =
                    let data = buffer.GetValue().Coerce<V4f>().Download()
                    Expect.isGreaterThanOrEqual data.Length 7 "Unexpected size"
                    Expect.equal data.[1] a.[0] "Unexpected data at index 1"
                    Expect.equal data.[2] a.[1] "Unexpected data at index 2"
                    Expect.equal data.[5] (V4f b.[0]) "Unexpected data at index 5"
                    Expect.equal data.[6] (V4f b.[1]) "Unexpected data at index 6"

                check a.Value b.Value

                // Changes should propagate
                transact (fun _ ->
                    a.Value <- a.Value |> Array.map ((+) 1.0f)
                    b.Value <- b.Value |> Array.map ((+) 5.0)
                )
                check a.Value b.Value

                // Dispose writer for b -> change to b should not propagate
                db.Dispose()
                let pb = b.Value
                transact (fun _ ->
                    a.Value <- a.Value |> Array.map ((*) 2.0f)
                    b.Value <- b.Value |> Array.map ((*) 2.0)
                )
                check a.Value pb
            )

    let tests (backend: Backend) =
        [
            "Set",        Cases.set
            "Set array",  Cases.setArray
            "Add",        Cases.add
            "Add buffer", Cases.addBuffer
        ]
        |> prepareCases backend "ManagedBuffer"