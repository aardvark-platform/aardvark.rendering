namespace Aardvark.Base

open Microsoft.FSharp.Quotations
open System.Collections.Generic
open FShade.ExprExtensions

module private Kernels =
    open FShade 
    
    [<Literal>]
    let scanSize = 128

    [<Literal>]
    let halfScanSize = 64

    [<LocalSize(X = halfScanSize)>]
    let scanKernel (add : Expr<'a -> 'a -> 'a>) (inputData : 'a[]) (outputData : 'a[]) =
        compute {
            let inputOffset : int = uniform?Arguments?inputOffset
            let inputDelta : int = uniform?Arguments?inputDelta
            let inputSize : int = uniform?Arguments?inputSize
            let outputOffset : int = uniform?Arguments?outputOffset
            let outputDelta : int = uniform?Arguments?outputDelta

            let mem : 'a[] = allocateShared scanSize
            let gid = getGlobalId().X
            let group = getWorkGroupId().X

            let gid0 = gid
            let lid0 =  getLocalId().X

            let lai = lid0
            let lbi = lid0 + halfScanSize
            let ai  = 2 * gid0 - lid0 
            let bi  = ai + halfScanSize 


            if ai < inputSize then mem.[lai] <- inputData.[inputOffset + ai * inputDelta]
            if bi < inputSize then mem.[lbi] <- inputData.[inputOffset + bi * inputDelta]

            //if lgid < inputSize then mem.[llid] <- inputData.[inputOffset + lgid * inputDelta]
            //if rgid < inputSize then mem.[rlid] <- inputData.[inputOffset + rgid * inputDelta]

            let lgid = 2 * gid0
            let rgid = lgid + 1
            let llid = 2 * lid0
            let rlid = llid + 1

            let mutable offset = 1
            let mutable d = halfScanSize
            while d > 0 do
                barrier()
                if lid0 < d then
                    let ai = offset * (llid + 1) - 1
                    let bi = offset * (rlid + 1) - 1
                    mem.[bi] <- (%add) mem.[ai] mem.[bi]
                d <- d >>> 1
                offset <- offset <<< 1

            d <- 2
            offset <- offset >>> 1

            while d < scanSize do
                offset <- offset >>> 1
                barrier()
                if lid0 < d - 1 then
                    let ai = offset*(llid + 2) - 1
                    let bi = offset*(rlid + 2) - 1

                    mem.[bi] <- (%add) mem.[bi] mem.[ai]

                d <- d <<< 1
            barrier()

            if lgid < inputSize then
                outputData.[outputOffset + lgid * outputDelta] <- mem.[llid]
            if rgid < inputSize then
                outputData.[outputOffset + rgid * outputDelta] <- mem.[rlid]

        }


    [<LocalSize(X = halfScanSize)>]
    let fixupKernel (add : Expr<'a -> 'a -> 'a>) (inputData : 'a[]) (outputData : 'a[]) =
        compute {
            let inputOffset : int = uniform?Arguments?inputOffset
            let inputDelta : int = uniform?Arguments?inputDelta
            let outputOffset : int = uniform?Arguments?outputOffset
            let outputDelta : int = uniform?Arguments?outputDelta
            let groupSize : int = uniform?Arguments?groupSize
            let count : int = uniform?Arguments?count

            let id = getGlobalId().X + groupSize

            if id < count then
                let block = id / groupSize - 1
              
                let iid = inputOffset + block * inputDelta
                let oid = outputOffset + id * outputDelta

                if id % groupSize <> groupSize - 1 then
                    outputData.[oid] <- (%add) inputData.[iid] outputData.[oid]

        }
      

    let inputImage =
        sampler2d {
            texture uniform?InputTexture
            filter Filter.MinMagMipPoint
            addressU WrapMode.Clamp
            addressV WrapMode.Clamp
        }

    [<LocalSize(X = halfScanSize)>]
    let reduceKernel (add : Expr<'a -> 'a -> 'a>) (inputData : 'a[]) (outputData : 'a[]) =
        compute {
            let inputOffset : int = uniform?Arguments?inputOffset
            let inputDelta : int = uniform?Arguments?inputDelta
            let inputSize : int = uniform?Arguments?inputSize

            let mem : 'a[] = allocateShared scanSize
            let group = getWorkGroupId().X

            let gid0 = getGlobalId().X
            let tid =  getLocalId().X

            let lai = 2 * tid
            let lbi = lai + 1
            let ai  = 2 * tid
            let bi  = ai + 1 

            let localCount = min scanSize (inputSize - group * scanSize)

            if ai < inputSize then mem.[lai] <- inputData.[inputOffset + ai * inputDelta]
            if bi < inputSize then mem.[lbi] <- inputData.[inputOffset + bi * inputDelta]

            barrier()
            let mutable s = halfScanSize
            while s > 0 do
                if tid < s then
                    let bi = tid + s
                    if bi < localCount then
                        mem.[tid] <- (%add) mem.[tid] mem.[bi]

                s <- s >>> 1
                barrier()


            if tid = 0 then
                outputData.[group] <- mem.[0]


        }

    [<LocalSize(X = halfScanSize)>]
    let mapReduceKernel (map : Expr<int -> 'a -> 'b>) (add : Expr<'b -> 'b -> 'b>) (inputData : 'a[]) (outputData : 'b[]) =
        compute {
            let inputOffset : int = uniform?Arguments?inputOffset
            let inputDelta : int = uniform?Arguments?inputDelta
            let inputSize : int = uniform?Arguments?inputSize

            let mem : 'b[] = allocateShared scanSize
            let group = getWorkGroupId().X

            let gid0 = getGlobalId().X
            let tid =  getLocalId().X

            let lai = 2 * tid
            let lbi = lai + 1
            let ai  = 2 * gid0
            let bi  = ai + 1 

            let localCount = min scanSize (inputSize - group * scanSize)

            if ai < inputSize then mem.[lai] <- (%map) ai inputData.[inputOffset + ai * inputDelta]
            if bi < inputSize then mem.[lbi] <- (%map) bi inputData.[inputOffset + bi * inputDelta]

            barrier()
            let mutable s = halfScanSize
            while s > 0 do
                if tid < s then
                    let bi = tid + s
                    if bi < localCount then
                        mem.[tid] <- (%add) mem.[tid] mem.[bi]

                s <- s >>> 1
                barrier()




            if tid = 0 then
                outputData.[group] <- mem.[0]


        }


type private Scan<'a when 'a : unmanaged>(runtime : IComputeRuntime, add : Expr<'a -> 'a -> 'a>) =

    static let ceilDiv (v : int) (d : int) =
        if v % d = 0 then v / d
        else 1 + v / d

    let scan    = runtime.CreateComputeShader (Kernels.scanKernel add)
    let fixup   = runtime.CreateComputeShader (Kernels.fixupKernel add)

    let release() =
        runtime.DeleteComputeShader scan
        runtime.DeleteComputeShader  fixup

    let rec build (args : HashSet<IComputeShaderInputBinding>) (input : IBufferVector<'a>) (output : IBufferVector<'a>) =
        let cnt = int input.Count
        if cnt > 1 then
            let args0 = runtime.NewInputBinding(scan)

            args0.["inputOffset"] <- input.Origin |> int
            args0.["inputDelta"] <- input.Delta |> int
            args0.["inputSize"] <- input.Count |> int
            args0.["inputData"] <- input.Buffer
            args0.["outputOffset"] <- output.Origin |> int
            args0.["outputDelta"] <- output.Delta |> int
            args0.["outputData"] <- output.Buffer
            args0.Flush()
            args.Add args0 |> ignore

            let cmd =
                [
                    ComputeCommand.Bind scan
                    ComputeCommand.SetInput args0
                    ComputeCommand.Dispatch(ceilDiv (int input.Count) Kernels.scanSize)
                    ComputeCommand.Sync(output.Buffer)
                ]

            let oSums = output.Skip(Kernels.scanSize - 1).Strided(Kernels.scanSize)

            if oSums.Count > 0 then
                let inner = build args oSums oSums

                let args1 = runtime.NewInputBinding fixup
                args1.["inputData"] <- oSums.Buffer
                args1.["inputOffset"] <- oSums.Origin |> int
                args1.["inputDelta"] <- oSums.Delta |> int
                args1.["outputData"] <- output.Buffer
                args1.["outputOffset"] <- output.Origin |> int
                args1.["outputDelta"] <- output.Delta |> int
                args1.["count"] <- output.Count |> int
                args1.["groupSize"] <- Kernels.scanSize
                args1.Flush()
                args.Add args1 |> ignore

                [
                    yield! cmd
                    yield! inner
                    yield ComputeCommand.Bind fixup
                    yield ComputeCommand.SetInput args1
                    yield ComputeCommand.Dispatch(ceilDiv (int output.Count - Kernels.scanSize) Kernels.halfScanSize)
                    yield ComputeCommand.Sync(output.Buffer)
                ]
            else
                cmd
        else
            []
        
    member x.Compile (input : IBufferVector<'a>, output : IBufferVector<'a>) =
        let args = System.Collections.Generic.HashSet<IComputeShaderInputBinding>()
        let cmd = build args input output
        let prog = runtime.Compile cmd

        { new ComputeProgram<unit>() with
            member x.Dispose() =
                prog.Dispose()
                for a in args do a.Dispose()
                args.Clear()
            member x.RunUnit() =
                prog.Run()
        }
        
    member x.Run (input : IBufferVector<'a>, output : IBufferVector<'a>) =
        let args = System.Collections.Generic.HashSet<IComputeShaderInputBinding>()
        let cmd = build args input output
        runtime.Run cmd
        for a in args do a.Dispose()
        args.Clear()
  
type private Reduce<'a when 'a : unmanaged>(runtime : IComputeRuntime, add : Expr<'a -> 'a -> 'a>) =
  
    static let ceilDiv (v : int) (d : int) =
        if v % d = 0 then v / d
        else 1 + v / d

    let reduce  = runtime.CreateComputeShader (Kernels.reduceKernel add)

    let rec build (args : HashSet<System.IDisposable>) (input : IBufferVector<'a>) (target : 'a[]) =
        let cnt = int input.Count
        if cnt > 1 then
            let args0 = runtime.NewInputBinding(reduce)
            
            let groupCount = ceilDiv (int input.Count) Kernels.scanSize
            let temp = runtime.CreateBuffer<'a>(groupCount)
            args0.["inputOffset"] <- input.Origin |> int
            args0.["inputDelta"] <- input.Delta |> int
            args0.["inputSize"] <- input.Count |> int
            args0.["inputData"] <- input.Buffer
            args0.["outputData"] <- temp
            args0.Flush()

            args.Add args0 |> ignore
            args.Add temp |> ignore

            let cmd =
                [
                    ComputeCommand.Bind reduce
                    ComputeCommand.SetInput args0
                    ComputeCommand.Dispatch(ceilDiv (int input.Count) Kernels.scanSize)
                    ComputeCommand.Sync(temp.Buffer)
                ]

            if temp.Count > 0 then
                let inner = build args temp target
                [
                    yield! cmd
                    yield! inner
                ]
            else
                cmd
        else
            let b = input.Buffer.Coerce<'a>()
            [
                ComputeCommand.Copy(b.[input.Origin .. input.Origin], target)
            ]
 
    member x.ReduceShader = reduce

    member x.Run(input : IBufferVector<'a>) =
        let args = System.Collections.Generic.HashSet<System.IDisposable>()
        let target : 'a[] = Array.zeroCreate 1
        let cmd = build args input target
        runtime.Run cmd
        for a in args do a.Dispose()
        target.[0]
        
    member x.Compile (input : IBufferVector<'a>) =
        let args = System.Collections.Generic.HashSet<System.IDisposable>()
        let target : 'a[] = Array.zeroCreate 1
        let cmd = build args input target
        let prog = runtime.Compile cmd 

        { new ComputeProgram<'a>() with
            member x.Dispose() =
                prog.Dispose()
                for a in args do a.Dispose()
                args.Clear()
            member x.Run() =
                prog.Run()
                target.[0]

        }
    
type private MapReduce<'a, 'b when 'a : unmanaged and 'b : unmanaged>(runtime : IComputeRuntime, reduce : Reduce<'b>, map : Expr<int -> 'a -> 'b>, add : Expr<'b -> 'b -> 'b>) =
  
    static let ceilDiv (v : int) (d : int) =
        if v % d = 0 then v / d
        else 1 + v / d

    let mapReduce   = runtime.CreateComputeShader (Kernels.mapReduceKernel map add)
    let reduce      = reduce.ReduceShader

    let rec build (args : HashSet<System.IDisposable>) (input : IBufferVector<'b>) (target : 'b[]) =
        let cnt = int input.Count
        if cnt > 1 then
            let args0 = runtime.NewInputBinding(reduce)
            
            let groupCount = ceilDiv (int input.Count) Kernels.scanSize
            let temp = runtime.CreateBuffer<'b>(groupCount)
            args0.["inputOffset"] <- input.Origin |> int
            args0.["inputDelta"] <- input.Delta |> int
            args0.["inputSize"] <- input.Count |> int
            args0.["inputData"] <- input.Buffer
            args0.["outputData"] <- temp
            args0.Flush()

            args.Add args0 |> ignore
            args.Add temp |> ignore

            let cmd =
                [
                    ComputeCommand.Bind reduce
                    ComputeCommand.SetInput args0
                    ComputeCommand.Dispatch(ceilDiv (int input.Count) Kernels.scanSize)
                    ComputeCommand.Sync(temp.Buffer)
                ]

            if temp.Count > 0 then
                let inner = build args temp target
                [
                    yield! cmd
                    yield! inner
                ]
            else
                cmd
        else
            let b = input.Buffer.Coerce<'b>()
            [
                ComputeCommand.Copy(b.[input.Origin .. input.Origin], target)
            ]
    
    let buildTop (args : HashSet<System.IDisposable>) (input : IBufferVector<'a>) (target : 'b[]) =
        let cnt = int input.Count
        let args0 = runtime.NewInputBinding(mapReduce)
            
        let groupCount = ceilDiv (int input.Count) Kernels.scanSize
        let temp = runtime.CreateBuffer<'b>(groupCount)
        args0.["inputOffset"] <- input.Origin |> int
        args0.["inputDelta"] <- input.Delta |> int
        args0.["inputSize"] <- input.Count |> int
        args0.["inputData"] <- input.Buffer
        args0.["outputData"] <- temp
        args0.Flush()

        args.Add args0 |> ignore
        args.Add temp |> ignore

        let cmd =
            [
                ComputeCommand.Bind mapReduce
                ComputeCommand.SetInput args0
                ComputeCommand.Dispatch(ceilDiv (int input.Count) Kernels.scanSize)
                ComputeCommand.Sync(temp.Buffer)
            ]

        if temp.Count > 0 then
            let inner = build args temp target
            [
                yield! cmd
                yield! inner
            ]
        else
            cmd

    member x.Run(input : IBufferVector<'a>) =
        let args = System.Collections.Generic.HashSet<System.IDisposable>()
        let target : 'b[] = Array.zeroCreate 1
        let cmd = buildTop args input target
        runtime.Run cmd
        for a in args do a.Dispose()
        target.[0]
        
    member x.Compile (input : IBufferVector<'a>) =
        let args = System.Collections.Generic.HashSet<System.IDisposable>()
        let target : 'b[] = Array.zeroCreate 1
        let cmd = buildTop args input target
        let prog = runtime.Compile cmd 

        { new ComputeProgram<'b>() with
            member x.Dispose() =
                prog.Dispose()
                for a in args do a.Dispose()
                args.Clear()
            member x.Run() =
                prog.Run()
                target.[0]

        }
 
    
type private ExpressionCache() =
    let store = System.Collections.Concurrent.ConcurrentDictionary<list<string>, obj>()
    
    member x.GetOrCreate(e : Expr<'a>, create : Expr<'a> -> 'b) =
        let hash = [ Expr.ComputeHash e ]
        store.GetOrAdd(hash, fun _ ->
            create e :> obj
        ) |> unbox<'b>


    member x.GetOrCreate(a : Expr<'a>, b : Expr<'b>, create : Expr<'a> -> Expr<'b> -> 'c) =
        let hash = [ Expr.ComputeHash a; Expr.ComputeHash b ]
        store.GetOrAdd(hash, fun _ ->
            create a b :> obj
        ) |> unbox<'c>

type ParallelPrimitives(runtime : IComputeRuntime) =
    
    let scanCache = ExpressionCache()
    let reduceCache = ExpressionCache()
    let mapReduceCache = ExpressionCache()

    let getScanner (add : Expr<'a -> 'a -> 'a>) = scanCache.GetOrCreate(add, fun add -> new Scan<'a>(runtime, add))
    let getReducer (add : Expr<'a -> 'a -> 'a>) = reduceCache.GetOrCreate(add, fun add -> new Reduce<'a>(runtime, add))
    let getMapReducer (map : Expr<int -> 'a -> 'b>) (add : Expr<'b -> 'b -> 'b>) =
        mapReduceCache.GetOrCreate(map, add, fun map add -> 
            let reducer = getReducer add
            new MapReduce<'a, 'b>(runtime, reducer, map, add)
        )


    member x.CompileScan(add : Expr<'a -> 'a -> 'a>, input : IBufferVector<'a>, output : IBufferVector<'a>) =
        let scanner = getScanner add
        scanner.Compile(input, output)

    member x.CompileFold(add : Expr<'a -> 'a -> 'a>, input : IBufferVector<'a>) =
        let reducer = getReducer add
        reducer.Compile(input)

    member x.CompileMapReduce(map : Expr<int -> 'a -> 'b>, add : Expr<'b -> 'b -> 'b>, input : IBufferVector<'a>) =
        let reducer = getMapReducer map add
        reducer.Compile(input)


    member x.Scan(add : Expr<'a -> 'a -> 'a>, input : IBufferVector<'a>, output : IBufferVector<'a>) =
        let scanner = getScanner add
        scanner.Run(input, output)

    member x.Fold(add : Expr<'a -> 'a -> 'a>, input : IBufferVector<'a>) =
        let folder = getReducer add
        folder.Run(input)
        
    member x.MapReduce(map : Expr<int -> 'a -> 'b>, add : Expr<'b -> 'b -> 'b>, input : IBufferVector<'a>) =
        let reducer = getMapReducer map add
        reducer.Run(input)

