namespace Aardvark.Rendering.Tests.Compute

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Application
open Expecto

module PushConstants =

    type MyStruct =
        struct
            val mutable Foo : M33f
            val mutable Bar : M33f
            new (f, b) = { Foo = f; Bar = b }
        end

    type MyRecord =
        { Foo : M33f
          Bar : M33f }

    module private Shader =
        open FShade

        type UniformScope with
            member this.Index        : int            = this?PushConstant?Index
            member this.IndexArr     : Arr<N<3>, int> = this?PushConstant?IndexArr
            member this.IndexVec     : V3i            = this?PushConstant?IndexVec
            member this.IndexMat     : M33f           = this?PushConstant?IndexMat
            member this.MyStruct     : MyStruct       = this?PushConstant?MyStruct
            member this.MyRecord     : MyRecord       = this?PushConstant?MyRecord
            member this.OutputBuffer : int[]          = this?StorageBuffer?OutputBuffer

        [<LocalSize(X = 1)>]
        let writeIndex () =
            compute {
                uniform.OutputBuffer.[uniform.Index] <- uniform.Index + 1
            }

        [<LocalSize(X = 1)>]
        let writeIndexArr () =
            compute {
                for i = unroll 0 to 2 do
                    uniform.OutputBuffer.[uniform.IndexArr.[i]] <- uniform.Index + i
            }

        [<LocalSize(X = 1)>]
        let writeIndexVec () =
            compute {
                uniform.OutputBuffer.[uniform.Index] <- uniform.IndexVec.X * uniform.IndexVec.Y * uniform.IndexVec.Z
            }

        [<LocalSize(X = 1)>]
        let writeIndexMat () =
            compute {
                let m = V3i uniform.IndexMat.C0 + V3i uniform.IndexMat.C1 + V3i uniform.IndexMat.C2
                uniform.OutputBuffer.[uniform.Index] <- Vec.dot m V3i.One
            }

        [<LocalSize(X = 1)>]
        let writeIndexStruct () =
            compute {
                let f = V3i uniform.MyStruct.Foo.C0 + V3i uniform.MyStruct.Foo.C1 + V3i uniform.MyStruct.Foo.C2
                let b = V3i uniform.MyStruct.Bar.C0 + V3i uniform.MyStruct.Bar.C1 + V3i uniform.MyStruct.Bar.C2
                uniform.OutputBuffer.[uniform.Index] <- Vec.dot (f + b) V3i.One
            }

        [<LocalSize(X = 1)>]
        let writeIndexRecord () =
            compute {
                let f = V3i uniform.MyRecord.Foo.C0 + V3i uniform.MyRecord.Foo.C1 + V3i uniform.MyRecord.Foo.C2
                let b = V3i uniform.MyRecord.Bar.C0 + V3i uniform.MyRecord.Bar.C1 + V3i uniform.MyRecord.Bar.C2
                uniform.OutputBuffer.[uniform.Index] <- Vec.dot (f + b) V3i.One
            }

    module Cases =

        let private perform (shader: 'T1 -> 'T2) (run : IComputeShader -> IComputeInputBinding -> int -> int[]) (runtime : IRuntime) =
            use output = runtime.CreateBuffer<int> 12
            use shader = runtime.CreateComputeShader(shader)
            let input = shader.CreateInputBinding <| uniformMap { buffer "OutputBuffer" output }

            let expected = run shader input output.Count
            let result = output.Download()

            Expect.equal result expected "Unexpected result"

        let int32 =
            perform Shader.writeIndex (fun shader input count ->
                let index = shader.GetConstant<int> "Index"

                shader.Runtime.Run [
                    ComputeCommand.Bind shader
                    ComputeCommand.SetInput input

                    for i = 0 to count - 1 do
                        ComputeCommand.SetConstant(index, i)
                        ComputeCommand.Dispatch 1
                ]

                Array.init count ((+) 1)
            )

        let int32Arr =
            perform Shader.writeIndexArr (fun shader input count ->
                let index = shader.GetConstant<int> "Index"
                let indexArr = shader.GetConstant<int[]> "IndexArr"

                shader.Runtime.Run [
                    ComputeCommand.Bind shader
                    ComputeCommand.SetInput input

                    for i = 0 to 3 do
                        ComputeCommand.SetConstant(index, i * 3 + 1)
                        ComputeCommand.SetConstant(indexArr, [| i * 3; i * 3 + 1; i * 3 + 2 |])
                        ComputeCommand.Dispatch 1
                ]

                Array.init count ((+) 1)
            )

        let v3i =
            perform Shader.writeIndexVec (fun shader input count ->
                let index = shader.GetConstant<int> "Index"
                let indexVec = shader.GetConstant<V3i> "IndexVec"

                shader.Runtime.Run [
                    ComputeCommand.Bind shader
                    ComputeCommand.SetInput input

                    for i = 0 to count - 1 do
                        ComputeCommand.SetConstant(index, i)
                        ComputeCommand.SetConstant(indexVec, V3i(i + 1, i + 2, i + 3))
                        ComputeCommand.Dispatch 1
                ]

                Array.init count (fun i -> (i + 1) * (i + 2) * (i + 3))
            )

        let m33f =
            perform Shader.writeIndexMat (fun shader input count ->
                let index = shader.GetConstant<int> "Index"
                let indexMat = shader.GetConstant<M33f> "IndexMat"

                shader.Runtime.Run [
                    ComputeCommand.Bind shader
                    ComputeCommand.SetInput input

                    for i = 0 to count - 1 do
                        let m = m33f <| Array.init 9 ((+) i >> float32)

                        ComputeCommand.SetConstant(index, i)
                        ComputeCommand.SetConstant(indexMat, m)
                        ComputeCommand.Dispatch 1
                ]

                Array.init count (fun i ->
                    let m = M33i (Array.init 9 ((+) i))
                    Vec.dot (m.C0 + m.C1 + m.C2) V3i.One
                )
            )

        let customStruct =
            perform Shader.writeIndexStruct (fun shader input count ->
                let index = shader.GetConstant<int> "Index"
                let myStruct = shader.GetConstant<MyStruct> "MyStruct"

                shader.Runtime.Run [
                    ComputeCommand.Bind shader
                    ComputeCommand.SetInput input

                    for i = 0 to count - 1 do
                        let f = Array.init 9 ((+) i >> float32)
                        let b = Array.init 9 ((+) i >> (*) 3 >> float32)

                        ComputeCommand.SetConstant(index, i)
                        ComputeCommand.SetConstant(myStruct, MyStruct(M33f f, M33f b))
                        ComputeCommand.Dispatch 1
                ]

                Array.init count (fun i ->
                    let mf = M33i (Array.init 9 ((+) i))
                    let mb = M33i (Array.init 9 ((+) i >> (*) 3))
                    let f = mf.C0 + mf.C1 + mf.C2
                    let b = mb.C0 + mb.C1 + mb.C2
                    Vec.dot (f + b) V3i.One
                )
            )

        let customRecord =
            perform Shader.writeIndexRecord (fun shader input count ->
                let index = shader.GetConstant<int> "Index"
                let myStruct = shader.GetConstant<MyRecord> "MyRecord"

                shader.Runtime.Run [
                    ComputeCommand.Bind shader
                    ComputeCommand.SetInput input

                    for i = 0 to count - 1 do
                        let f = Array.init 9 ((+) i >> float32)
                        let b = Array.init 9 ((+) i >> (*) 3 >> float32)

                        ComputeCommand.SetConstant(index, i)
                        ComputeCommand.SetConstant(myStruct, { Foo = M33f f; Bar = M33f b })
                        ComputeCommand.Dispatch 1
                ]

                Array.init count (fun i ->
                    let mf = M33i (Array.init 9 ((+) i))
                    let mb = M33i (Array.init 9 ((+) i >> (*) 3))
                    let f = mf.C0 + mf.C1 + mf.C2
                    let b = mb.C0 + mb.C1 + mb.C2
                    Vec.dot (f + b) V3i.One
                )
            )

    let tests (backend : Backend) =
        [
            if (backend = Backend.Vulkan) then
                "Int32",          Cases.int32
                "Int32 array",    Cases.int32Arr
                "V3i",            Cases.v3i
                "M33f",           Cases.m33f
                "Struct",         Cases.customStruct
                "Record",         Cases.customRecord
        ]
        |> prepareComputeCases backend "Push constants"