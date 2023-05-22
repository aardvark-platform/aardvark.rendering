namespace Aardvark.GPGPU

open Microsoft.FSharp.Quotations
open FShade.ExprHashExtensions
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open System
open System.Collections.Generic
open System.Runtime.CompilerServices

type IComputePrimitive =
    inherit IDisposable
    abstract member Task : IComputeTask
    abstract member RunUnit : renderToken: RenderToken -> unit

type IComputePrimitive<'T> =
    inherit IComputePrimitive
    abstract member Run : renderToken: RenderToken -> 'T

[<AutoOpen>]
module ComputeCommandPrimitiveFSharpExtensions =

    type IComputePrimitive with
        member x.Runtime = x.Task.Runtime

    type ComputeCommand with
        static member Execute(primitive : IComputePrimitive) =
            ComputeCommand.Execute(primitive.Task)

[<AutoOpen>]
module private ComputePrimitiveImplementation =

    type ComputePrimitive<'T>(task : IComputeTask) =

        abstract member GetResult : unit -> 'T
        default x.GetResult() = Unchecked.defaultof<'T>

        abstract member Release : unit -> unit
        default x.Release() = ()

        member x.Dispose() =
            task.Dispose()
            x.Release()

        member x.Run(renderToken : RenderToken) =
            task.Run(AdaptiveToken.Top, renderToken)
            x.GetResult()

        interface IComputePrimitive<'T> with
            member x.Task = task
            member x.Run(renderToken) = x.Run(renderToken)
            member x.RunUnit(renderToken) = x.Run(renderToken) |> ignore
            member x.Dispose() = x.Dispose()

[<AbstractClass; Sealed; Extension>]
type IComputePrimitveExtensions private() =

    [<Extension>]
    static member RunUnit(primitive : IComputePrimitive) =
        primitive.RunUnit RenderToken.Empty

    [<Extension>]
    static member Run(primitive : IComputePrimitive<'T>) =
        primitive.Run RenderToken.Empty

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


    [<LocalSize(X = halfScanSize)>]
    let reduceKernel (add : Expr<'a -> 'a -> 'a>) (inputData : 'a[]) (outputData : 'a[]) =
        compute {
            let inputOffset : int = uniform?Arguments?inputOffset
            let inputDelta : int = uniform?Arguments?inputDelta
            let inputSize : int = uniform?Arguments?inputSize

            let mem : 'a[] = allocateShared scanSize
            let group = getWorkGroupId().X
            let tid =  getLocalId().X

            let lai = 2 * tid
            let lbi = lai + 1
            let ai  = scanSize * group + lai
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
            let tid =  getLocalId().X

            let lai = 2 * tid
            let lbi = lai + 1
            let ai  = scanSize * group + lai
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


    let inputImage2d =
        sampler2d {
            texture uniform?InputTexture
            filter Filter.MinMagMipPoint
            addressU WrapMode.Border
            addressV WrapMode.Border
            borderColor (C4f(0.0f, 0.0f, 0.0f, 0.0f))
        }

    let imageScanSize2d = V2i(16, 8)

    [<LocalSize(X = 8, Y = 8)>]
    let mapReduceImageKernel2d (map : Expr<V3i -> V4f -> 'b>) (add : Expr<'b -> 'b -> 'b>) (numGroups : V2i) (inputSize : V2i) (outputData : 'b[]) =
        compute {
            let s = inputSize
            let ggc = numGroups
            let lc = getLocalId().XY
            let gc = getWorkGroupId().XY
            let tid = lc.Y * 8 + lc.X

            let group = gc.Y * ggc.X + gc.X

            let ai = gc * imageScanSize2d + lc * V2i(2,1)
            let bi = ai + V2i(1,0)
            let lai = lc.Y * 16 + lc.X * 2
            let lbi = lai + 1

            let mem : 'b[] = allocateShared 128

            if Vec.allSmaller ai s then mem.[lai] <- (%map) ai.XYO (V4f (inputImage2d.Read(ai, 0)))
            if Vec.allSmaller bi s then mem.[lbi] <- (%map) bi.XYO (V4f (inputImage2d.Read(bi, 0)))

            let localCount = min imageScanSize2d (s - gc * imageScanSize2d)

            barrier()
            let mutable s = 64
            while s > 0 do
                if tid < s then
                    let bi = tid + s
                    let b = V2i(bi % 16, bi / 16)
                    if Vec.allSmaller b localCount then
                        mem.[tid] <- (%add) mem.[tid] mem.[bi]

                s <- s >>> 1
                barrier()

            if tid = 0 then
                outputData.[group] <- mem.[0]
        }
 
    let inputImage3d =
        sampler3d {
            texture uniform?InputTexture
            filter Filter.MinMagMipPoint
            addressU WrapMode.Border
            addressV WrapMode.Border
            addressW WrapMode.Border
            borderColor (C4f(0.0, 0.0, 0.0, 0.0))
        }

    let imageScanSize3d = V3i(8, 4, 2)

    [<LocalSize(X = 4, Y = 4, Z = 2)>]
    let mapReduceImageKernel3d (map : Expr<V3i -> V4f -> 'b>) (add : Expr<'b -> 'b -> 'b>) (numGroups : V3i) (inputSize : V3i) (outputData : 'b[]) =
        compute {
            let s = inputSize
            let ggc = numGroups
            let lc = getLocalId()
            let gc = getWorkGroupId()
            let tid = lc.Z * 16 + lc.Y * 4 + lc.X
            
            let group = gc.Z * (ggc.X * ggc.Y) + gc.Y * ggc.X + gc.X

            let ai = gc * imageScanSize3d + lc * V3i(2,1,1)
            let bi = ai + V3i(1,0,0)
            let lai = lc.Z * 32 + lc.Y * 8 + lc.X * 2
            let lbi = lai + 1

            let mem : 'b[] = allocateShared 64

            if Vec.allSmaller ai s then mem.[lai] <- (%map) ai (V4f (inputImage3d.Read(ai, 0)))
            if Vec.allSmaller bi s then mem.[lbi] <- (%map) bi (V4f (inputImage3d.Read(bi, 0)))

            let localCount = min imageScanSize3d (s - gc * imageScanSize3d)
            
            barrier()
            let mutable s = 32
            while s > 0 do
                if tid < s then
                    let bi = tid + s
                    let b = V3i(bi % 8, (bi / 8) % 4, bi / 32)
                    if Vec.allSmaller b localCount then
                        mem.[tid] <- (%add) mem.[tid] mem.[bi]

                s <- s >>> 1
                barrier()

            if tid = 0 then
                outputData.[group] <- mem.[0]

        }


    [<LocalSize(X = 64)>]
    let map (map : Expr<int -> 'a -> 'b>) (src : 'a[]) (dst : 'b[]) =
        compute {
            let id = getGlobalId().X
            let srcOffset : int = uniform?SrcOffset
            let srcDelta : int = uniform?SrcDelta
            let srcCnt : int = uniform?SrcCount
            
            let dstOffset : int = uniform?DstOffset
            let dstDelta : int = uniform?DstDelta

            if id < srcCnt then
                dst.[dstOffset + id * dstDelta] <- (%map) id src.[srcOffset + id * srcDelta]
        }

    [<ReflectedDefinition; Inline>]
    let mk2d (dim : int) (x : int) (y : int) =
        if dim = 0 then V2i(x,y)
        else V2i(y,x)
        
    [<ReflectedDefinition; Inline>]
    let mk3d (dim : int) (x : int) (yz : V2i) =
        if dim = 0 then V3i(x, yz.X, yz.Y)
        elif dim = 1 then V3i(yz.X, x, yz.Y)
        else V3i(yz.X, yz.Y, x)

    [<LocalSize(X = halfScanSize, Y = 1)>]
    let scanImageKernel2dTexture (add : Expr<V4d -> V4d -> V4d>) (dim : int) (outputImage : Image2d<Formats.rgba32f>) =
        compute {
            let mem : V4d[] = allocateShared scanSize
            let gid = getGlobalId().X
            let group = getWorkGroupId().X
            let inputSize = inputImage2d.Size.[dim]
            let y = getGlobalId().Y

            let gid0 = gid
            let lid0 =  getLocalId().X

            let lai = lid0
            let lbi = lid0 + halfScanSize
            let ai  = 2 * gid0 - lid0 
            let bi  = ai + halfScanSize 

            if ai < inputSize then mem.[lai] <- inputImage2d.[mk2d dim ai y]
            if bi < inputSize then mem.[lbi] <- inputImage2d.[mk2d dim bi y]

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
                outputImage.[mk2d dim lgid y] <- mem.[llid]
                //(%write) outputImage lgid y mem.[llid]

            if rgid < inputSize then
                outputImage.[mk2d dim rgid y] <- mem.[rlid]
                //(%write) outputImage rgid y mem.[rlid]
        }

    [<LocalSize(X = halfScanSize, Y = 1, Z = 1)>]
    let scan3dKernel (add : Expr<'a -> 'a -> 'a>) (read : Expr<V3i -> 'a>) (write : Expr<V3i -> 'a -> unit>) (dimension : int) =
        compute {
            let Offset : int = uniform?Arguments?Offset
            let Delta : int = uniform?Arguments?Delta
            let Size : int = uniform?Arguments?Size
            
            let mem : 'a[] = allocateShared scanSize
            let gid = getGlobalId().X
            let group = getWorkGroupId().X
            //let inputSize = inOutImage.Size.[dimension]
            let yz = getGlobalId().YZ

            let gid0 = gid
            let lid0 =  getLocalId().X

            let lai = lid0
            let lbi = lid0 + halfScanSize
            let ai  = 2 * gid0 - lid0 
            let bi  = ai + halfScanSize 

            if ai < Size then mem.[lai] <- (%read) (mk3d dimension (Offset + ai * Delta) yz) 
            if bi < Size then mem.[lbi] <- (%read) (mk3d dimension (Offset + bi * Delta) yz)

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

            if lgid < Size then
                (%write) (mk3d dimension (Offset + lgid * Delta) yz) mem.[llid]

            if rgid < Size then
                (%write) (mk3d dimension (Offset + rgid * Delta) yz) mem.[rlid]
        }

    [<LocalSize(X = halfScanSize)>]
    let fixup3dKernel (add : Expr<'a -> 'a -> 'a>) (readInput : Expr<V3i -> 'a>) (addToOutput : Expr<V3i -> 'a -> unit>) (dimension : int) =
        compute {
            let inputOffset : int = uniform?Arguments?inputOffset
            let inputDelta : int = uniform?Arguments?inputDelta
            let outputOffset : int = uniform?Arguments?outputOffset
            let outputDelta : int = uniform?Arguments?outputDelta
            let groupSize : int = uniform?Arguments?groupSize
            let count : int = uniform?Arguments?count

            let yz = getGlobalId().YZ
            let id = getGlobalId().X + groupSize

            if id < count then
                let block = id / groupSize - 1
              
                let iid = inputOffset + block * inputDelta
                let oid = outputOffset + id * outputDelta

                if id % groupSize <> groupSize - 1 then
                    let v = (%readInput) (mk3d dimension iid yz)
                    (%addToOutput) (mk3d dimension oid yz) v
//                    let oc = mk2d dimension oid y
//                    inOutImage.[oc] <- (%add) inOutImage.[mk2d dimension iid y] inOutImage.[oc]

        }


    [<LocalSize(X = halfScanSize, Y = 1)>]
    let scanImageKernel2d (add : Expr<V4d -> V4d -> V4d>) (dimension : int) (inOutImage : Image2d<Formats.rgba32f>) =
        compute {
            let Offset : int = uniform?Arguments?Offset
            let Delta : int = uniform?Arguments?Delta
            let Size : int = uniform?Arguments?Size
            
            let mem : V4d[] = allocateShared scanSize
            let gid = getGlobalId().X
            let group = getWorkGroupId().X
            //let inputSize = inOutImage.Size.[dimension]
            let y = getGlobalId().Y

            let gid0 = gid
            let lid0 =  getLocalId().X

            let lai = lid0
            let lbi = lid0 + halfScanSize
            let ai  = 2 * gid0 - lid0 
            let bi  = ai + halfScanSize 

            if ai < Size then mem.[lai] <- inOutImage.[mk2d dimension (Offset + ai * Delta) y]
            if bi < Size then mem.[lbi] <- inOutImage.[mk2d dimension (Offset + bi * Delta) y]

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

            if lgid < Size then
                inOutImage.[mk2d dimension (Offset + lgid * Delta) y] <- mem.[llid]
                //(%write) outputImage lgid y mem.[llid]

            if rgid < Size then
                inOutImage.[mk2d dimension (Offset + rgid * Delta) y] <- mem.[rlid]
                //(%write) outputImage rgid y mem.[rlid]
        }


    [<LocalSize(X = halfScanSize)>]
    let fixupImageKernel2d (add : Expr<V4d -> V4d -> V4d>) (dimension : int) (inOutImage : Image2d<Formats.rgba32f>) =
        compute {
            let inputOffset : int = uniform?Arguments?inputOffset
            let inputDelta : int = uniform?Arguments?inputDelta
            let outputOffset : int = uniform?Arguments?outputOffset
            let outputDelta : int = uniform?Arguments?outputDelta
            let groupSize : int = uniform?Arguments?groupSize
            let count : int = uniform?Arguments?count

            let y = getGlobalId().Y
            let id = getGlobalId().X + groupSize

            if id < count then
                let block = id / groupSize - 1
              
                let iid = inputOffset + block * inputDelta
                let oid = outputOffset + id * outputDelta

                if id % groupSize <> groupSize - 1 then
                    let oc = mk2d dimension oid y
                    inOutImage.[oc] <- (%add) inOutImage.[mk2d dimension iid y] inOutImage.[oc]

        }


module private ImageIO =
    open FShade

    let inputSampler2d =
        sampler2d {
            texture uniform?inputTexture
            filter Filter.MinMagMipPoint
        }
        
    let inputSampler3d =
        sampler3d {
            texture uniform?inputTexture
            filter Filter.MinMagMipPoint
        }

    type UniformScope with
        member x.ImageHeight : int = x?Arguments?imageHeight
        member x.Image2d : Image2d<Formats.rgba32f> = x?inOutImage
        member x.Image3d : Image3d<Formats.rgba32f> = x?inOutImage


    let writeImage2d                                    = <@ fun (c : V3i) (v : V4d) -> uniform.Image2d.[c.XY] <- v @>
    let readSampler2d                                   = <@ fun (c : V3i) -> inputSampler2d.[c.XY] @>
    let readImage2d                                     = <@ fun (c : V3i) -> uniform.Image2d.[c.XY] @>
    let addToImage2d (add : Expr<V4d -> V4d -> V4d>)    = <@ fun (c : V3i) (v : V4d) -> uniform.Image2d.[c.XY] <- (%add) v uniform.Image2d.[c.XY] @>

    let writeImage3d                                    = <@ fun (c : V3i) (v : V4d) -> uniform.Image3d.[c] <- v @>
    let readSampler3d                                   = <@ fun (c : V3i) -> inputSampler3d.[c] @>
    let readImage3d                                     = <@ fun (c : V3i) -> uniform.Image3d.[c] @>
    let addToImage3d (add : Expr<V4d -> V4d -> V4d>)    = <@ fun (c : V3i) (v : V4d) -> uniform.Image3d.[c] <- (%add) v uniform.Image3d.[c] @>



type private Map<'a, 'b when 'a : unmanaged and 'b : unmanaged>(runtime : IComputeRuntime, map : Expr<int -> 'a -> 'b>) =
    
    static let ceilDiv (v : int) (d : int) =
        if v % d = 0 then v / d
        else 1 + v / d

    let map    = runtime.CreateComputeShader (Kernels.map map)

    let build (src : IBufferVector<'a>) (dst : IBufferVector<'b>) =
        let args = runtime.CreateInputBinding(map)
        
        args.["src"] <- src.Buffer
        args.["SrcOffset"] <- src.Origin
        args.["SrcDelta"] <- src.Delta
        args.["SrcCount"] <- src.Count
        
        args.["dst"] <- dst.Buffer
        args.["DstOffset"] <- dst.Origin
        args.["DstDelta"] <- dst.Delta
        args.Flush()
        
        args, [
            ComputeCommand.Bind map
            ComputeCommand.SetInput args
            ComputeCommand.Dispatch(ceilDiv (int src.Count) 64)
            ComputeCommand.Sync(dst.Buffer)
        ]



    member x.Compile(input : IBufferVector<'a>, output : IBufferVector<'b>) : IComputePrimitive<unit> =
        let args, cmd = build input output
        let task = runtime.CompileCompute cmd
        { new ComputePrimitive<unit>(task) with
            member x.Release() = args.Dispose() }

    member x.Run(input : IBufferVector<'a>, output : IBufferVector<'b>, renderToken : RenderToken) =
        let args, cmd = build input output
        runtime.Run(cmd, renderToken)
        args.Dispose()

    member x.Dispose() =
        map.Dispose()

    interface IDisposable with
        member x.Dispose() = x.Dispose()

        

type private Scan<'a when 'a : unmanaged>(runtime : IComputeRuntime, add : Expr<'a -> 'a -> 'a>) =

    static let ceilDiv (v : int) (d : int) =
        if v % d = 0 then v / d
        else 1 + v / d

    let scan    = runtime.CreateComputeShader (Kernels.scanKernel add)
    let fixup   = runtime.CreateComputeShader (Kernels.fixupKernel add)

    let release() =
        runtime.DeleteComputeShader scan
        runtime.DeleteComputeShader  fixup

    let rec build (args : HashSet<MutableComputeInputBinding>) (input : IBufferVector<'a>) (output : IBufferVector<'a>) =
        let cnt = int input.Count
        if cnt > 1 then
            let args0 = runtime.CreateInputBinding(scan)

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

                let args1 = runtime.CreateInputBinding fixup
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
                    if int output.Count > Kernels.scanSize then
                        yield ComputeCommand.Bind fixup
                        yield ComputeCommand.SetInput args1
                        yield ComputeCommand.Dispatch(ceilDiv (int output.Count - Kernels.scanSize) Kernels.halfScanSize)
                        yield ComputeCommand.Sync(output.Buffer)
                ]
            else
                cmd
        else
            []
        
    member x.Compile (input : IBufferVector<'a>, output : IBufferVector<'a>) : IComputePrimitive<unit> =
        let args = System.Collections.Generic.HashSet<MutableComputeInputBinding>()
        let cmd = build args input output
        let task = runtime.CompileCompute cmd
        { new ComputePrimitive<unit>(task) with
            member x.Release() =
                for a in args do a.Dispose()
                args.Clear()
        }
        
    member x.Run (input : IBufferVector<'a>, output : IBufferVector<'a>, renderToken : RenderToken) =
        let args = System.Collections.Generic.HashSet<MutableComputeInputBinding>()
        let cmd = build args input output
        runtime.Run(cmd, renderToken)
        for a in args do a.Dispose()
        args.Clear()
  
    member x.Dispose() =
        release()

    interface IDisposable with
        member x.Dispose() = x.Dispose()

type private Reduce<'a when 'a : unmanaged>(runtime : IComputeRuntime, add : Expr<'a -> 'a -> 'a>) =
  
    static let ceilDiv (v : int) (d : int) =
        if v % d = 0 then v / d
        else 1 + v / d

    let reduce  = runtime.CreateComputeShader (Kernels.reduceKernel add)

    let rec build (args : HashSet<System.IDisposable>) (input : IBufferVector<'a>) (target : 'a[]) =
        let cnt = int input.Count
        if cnt > 1 then
            let args0 = runtime.CreateInputBinding(reduce)
            
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
                    ComputeCommand.Dispatch(groupCount)
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
                ComputeCommand.Download(b.[input.Origin .. input.Origin], target)
            ]
 
    member x.ReduceShader = reduce

    member x.Run(input : IBufferVector<'a>, renderToken : RenderToken) =
        let args = System.Collections.Generic.HashSet<System.IDisposable>()
        let target : 'a[] = Array.zeroCreate 1
        let cmd = build args input target
        runtime.Run(cmd, renderToken)
        for a in args do a.Dispose()
        target.[0]
        
    member x.Compile (input : IBufferVector<'a>) : IComputePrimitive<'a> =
        let args = System.Collections.Generic.HashSet<System.IDisposable>()
        let target : 'a[] = Array.zeroCreate 1
        let cmd = build args input target
        let task = runtime.CompileCompute cmd 
 
        { new ComputePrimitive<'a>(task) with
            member x.Release() =
                for a in args do a.Dispose()
                args.Clear()
            member x.GetResult() =
                target.[0]
        }

    member x.Dispose() =
        reduce.Dispose()

    interface IDisposable with
        member x.Dispose() = x.Dispose()
    
type private MapReduce<'a, 'b when 'a : unmanaged and 'b : unmanaged>(runtime : IComputeRuntime, reduce : Reduce<'b>, map : Expr<int -> 'a -> 'b>, add : Expr<'b -> 'b -> 'b>) =
  
    static let ceilDiv (v : int) (d : int) =
        if v % d = 0 then v / d
        else 1 + v / d

    let mapReduce   = runtime.CreateComputeShader (Kernels.mapReduceKernel map add)
    let reduce      = reduce.ReduceShader

    let rec build (args : HashSet<System.IDisposable>) (input : IBufferVector<'b>) (target : 'b[]) =
        let cnt = int input.Count
        if cnt > 1 then
            let args0 = runtime.CreateInputBinding(reduce)
            
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
                ComputeCommand.Download(b.[input.Origin .. input.Origin], target)
            ]
    
    let buildTop (args : HashSet<System.IDisposable>) (input : IBufferVector<'a>) (target : 'b[]) =
        let cnt = int input.Count
        let args0 = runtime.CreateInputBinding(mapReduce)
            
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

    member x.Run(input : IBufferVector<'a>, renderToken : RenderToken) =
        let args = System.Collections.Generic.HashSet<System.IDisposable>()
        let target : 'b[] = Array.zeroCreate 1
        let cmd = buildTop args input target
        runtime.Run(cmd, renderToken)
        for a in args do a.Dispose()
        target.[0]
        
    member x.Compile (input : IBufferVector<'a>) : IComputePrimitive<'b> =
        let args = System.Collections.Generic.HashSet<System.IDisposable>()
        let target : 'b[] = Array.zeroCreate 1
        let cmd = buildTop args input target
        let task = runtime.CompileCompute cmd 

        { new ComputePrimitive<'b>(task) with
            member x.Release() =
                for a in args do a.Dispose()
                args.Clear()
            member x.GetResult() =
                target.[0]
        }

    member x.Dispose() =
        mapReduce.Dispose()

    interface IDisposable with
        member x.Dispose() = x.Dispose()
 
type private MapReduceImage<'b when 'b : unmanaged>(runtime : IComputeRuntime, reduce : Reduce<'b>, map : Expr<V3i -> V4f -> 'b>, add : Expr<'b -> 'b -> 'b>) =
  
    static let ceilDiv (v : int) (d : int) =
        if v % d = 0 then v / d
        else 1 + v / d

    static let ceilDiv2 (v : V2i) (d : V2i) =
        V2i(ceilDiv v.X d.X, ceilDiv v.Y d.Y)
   
    static let ceilDiv3 (v : V3i) (d : V3i) =
        V3i(ceilDiv v.X d.X, ceilDiv v.Y d.Y, ceilDiv v.Z d.Z)
   
    let mapReduce2d = runtime.CreateComputeShader (Kernels.mapReduceImageKernel2d map add)
    let mapReduce3d = runtime.CreateComputeShader (Kernels.mapReduceImageKernel3d map add)
    let reduce      = reduce.ReduceShader

    let rec build (args : HashSet<System.IDisposable>) (input : IBufferVector<'b>) (target : 'b[]) =
        let cnt = int input.Count
        if cnt > 1 then
            let args0 = runtime.CreateInputBinding(reduce)
            
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
                    ComputeCommand.Dispatch(groupCount)
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
                ComputeCommand.Download(b.[input.Origin .. input.Origin], target)
            ]
    
    let buildTop (args : HashSet<System.IDisposable>) (input : ITextureSubResource) (target : 'b[]) =
        let dimensions =
            match input.Texture.Dimension with
                | TextureDimension.Texture2D | TextureDimension.TextureCube -> 2
                | TextureDimension.Texture3D -> 3
                | d -> 
                    failwithf "cannot reduce image with dimension: %A" d

        match dimensions with
        | 2 -> 
            let args0 = runtime.CreateInputBinding(mapReduce2d)
            
            let size = input.Size.XY
            let groupCount = ceilDiv2 size Kernels.imageScanSize2d
            let temp = runtime.CreateBuffer<'b>(groupCount.X * groupCount.Y)

            args0.["InputTexture"] <- input
            args0.["outputData"] <- temp
            args0.["numGroups"] <- groupCount
            args0.["inputSize"] <- size
            args0.Flush()

            args.Add args0 |> ignore
            args.Add temp |> ignore

            let cmd =
                [
                    ComputeCommand.Bind mapReduce2d
                    ComputeCommand.SetInput args0
                    ComputeCommand.Dispatch(groupCount)
                    ComputeCommand.Sync(temp.Buffer)
                ]

            if temp.Count > 0 then
                [
                    yield! cmd
                    yield! build args temp target
                ]
            else
                cmd
        | 3 ->
            let args0 = runtime.CreateInputBinding(mapReduce3d)
            
            let size = input.Size
            let groupCount = ceilDiv3 size Kernels.imageScanSize3d
            let temp = runtime.CreateBuffer<'b>(groupCount.X * groupCount.Y * groupCount.Z)
            
            args0.["InputTexture"] <- input
            args0.["outputData"] <- temp
            args0.["numGroups"] <- groupCount
            args0.["inputSize"] <- size
            args0.Flush()

            args.Add args0 |> ignore
            args.Add temp |> ignore

            let cmd =
                [
                    ComputeCommand.Bind mapReduce3d
                    ComputeCommand.SetInput args0
                    ComputeCommand.Dispatch(groupCount)
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
        | d ->  
            failwithf "cannot reduce image with dimension: %A" d

    member x.Run(input : ITextureSubResource, renderToken : RenderToken) =
        let args = System.Collections.Generic.HashSet<System.IDisposable>()
        let target : 'b[] = Array.zeroCreate 1
        let cmd = buildTop args input target
        runtime.Run(cmd, renderToken)
        for a in args do a.Dispose()
        target.[0]
        
    member x.Compile (input : ITextureSubResource) : IComputePrimitive<'b> =
        let args = System.Collections.Generic.HashSet<System.IDisposable>()
        let target : 'b[] = Array.zeroCreate 1
        let cmd = buildTop args input target
        let task = runtime.CompileCompute cmd 

        { new ComputePrimitive<'b>(task) with
            member x.Release() =
                for a in args do a.Dispose()
                args.Clear()
            member x.GetResult() =
                target.[0]
        }

    member x.Dispose() =
        mapReduce2d.Dispose()
        mapReduce3d.Dispose()

    interface IDisposable with
        member x.Dispose() = x.Dispose()
  
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

    member x.Dispose() =
        for KeyValue(_, obj) in store do
            match obj with
            | :? IDisposable as d -> d.Dispose()
            | _ -> ()

    interface IDisposable with
        member x.Dispose() = x.Dispose()

type private Add<'a>() =
    static let addMeth = System.Type.GetType("Microsoft.FSharp.Core.Operators, FSharp.Core").GetMethod("op_Addition")
    
    static let add : Expr<'a -> 'a -> 'a> = 
        let m = addMeth.MakeGenericMethod [| typeof<'a>; typeof<'a>; typeof<'a> |]
        let l = Var("l", typeof<'a>)
        let r = Var("r", typeof<'a>)
        Expr.Cast <| 
            Expr.Lambda(l, 
                Expr.Lambda(r, 
                    Expr.Call(m, [Expr.Var l; Expr.Var r])
                )
            )

    static member Expr = add

type private ScanRange = { offset : int; delta : int; count : int }

type private ScanImage2d(runtime : IComputeRuntime, add : Expr<V4d -> V4d -> V4d>) =

    static let ceilDiv (v : int) (d : int) =
        if v % d = 0 then v / d
        else 1 + v / d

    let scanTexture2d     = lazy (runtime.CreateComputeShader (Kernels.scan3dKernel add ImageIO.readSampler2d ImageIO.writeImage2d))
    let scan2d            = lazy (runtime.CreateComputeShader (Kernels.scan3dKernel add ImageIO.readImage2d ImageIO.writeImage2d))
    let fixup2d           = lazy (runtime.CreateComputeShader (Kernels.fixup3dKernel add ImageIO.readImage2d (ImageIO.addToImage2d add)))
    
    let scanTexture3d     = lazy (runtime.CreateComputeShader (Kernels.scan3dKernel add ImageIO.readSampler3d ImageIO.writeImage3d))
    let scan3d            = lazy (runtime.CreateComputeShader (Kernels.scan3dKernel add ImageIO.readImage3d ImageIO.writeImage3d))
    let fixup3d           = lazy (runtime.CreateComputeShader (Kernels.fixup3dKernel add ImageIO.readImage3d (ImageIO.addToImage3d add)))

    let scanTexture =
        LookupTable.lookupTable [
            2, scanTexture2d
            3, scanTexture3d
        ]

    let scan =
        LookupTable.lookupTable [
            2, scan2d
            3, scan3d
        ]

    let fixup =
        LookupTable.lookupTable [
            2, fixup2d
            3, fixup3d
        ]


    let next (range : ScanRange) =
        // (Kernels.scanSize - 1) + x * Kernels.scanSize <= range.count - 1
        // x * Kernels.scanSize <= range.count - 1 - Kernels.scanSize + 1
        // x <= (range.count - Kernels.scanSize) / Kernels.scanSize
        let innerCount = 1 + (range.count - Kernels.scanSize) / Kernels.scanSize
        let innerDelta = range.delta * Kernels.scanSize
        let innerOffset = range.offset + range.delta * (Kernels.scanSize - 1)
        { offset = innerOffset; delta = innerDelta; count = innerCount }



    let release() =
        if scanTexture2d.IsValueCreated then runtime.DeleteComputeShader scanTexture2d.Value
        if scan2d.IsValueCreated then runtime.DeleteComputeShader scan2d.Value
        if fixup2d.IsValueCreated then runtime.DeleteComputeShader fixup2d.Value
        if scanTexture3d.IsValueCreated then runtime.DeleteComputeShader scanTexture3d.Value
        if scan3d.IsValueCreated then runtime.DeleteComputeShader scan3d.Value
        if fixup3d.IsValueCreated then runtime.DeleteComputeShader fixup3d.Value


    let scanBlocks (args : HashSet<MutableComputeInputBinding>) (imgDim : int) (dim : int) (image : ITextureSubResource) (range : ScanRange) =
        let scan = scan(imgDim).Value

        let input = runtime.CreateInputBinding(scan)
        input.["inOutImage"] <- image
        input.["dimension"] <- dim
        input.["Offset"] <- range.offset
        input.["Size"] <- range.count
        input.["Delta"] <- range.delta
        input.Flush()
        args.Add input |> ignore
        
        let rest =
            match dim with
                | 0 -> image.Size.YZ
                | 1 -> image.Size.XZ
                | 2 -> image.Size.XY
                | _ -> failwith "[GPGPU] invalid dimension"

        [
            ComputeCommand.Sync(image.Texture)
            ComputeCommand.Bind scan
            ComputeCommand.SetInput input
            ComputeCommand.Dispatch(V3i(ceilDiv range.count Kernels.scanSize, rest.X, rest.Y))
        ]

    let repair (args : HashSet<MutableComputeInputBinding>) (imgDim : int) (dim : int) (image : ITextureSubResource) (range : ScanRange) =
        let fixup = fixup(imgDim).Value

        let innerRange = next range

        let args1 = runtime.CreateInputBinding fixup
        args1.["inOutImage"] <- image
        args1.["dimension"] <- dim
        args1.["inputOffset"] <- innerRange.offset
        args1.["inputDelta"] <- innerRange.delta
        args1.["outputOffset"] <- range.offset
        args1.["outputDelta"] <- range.delta
        args1.["count"] <- range.count
        args1.["groupSize"] <- Kernels.scanSize
        args1.Flush()
        args.Add args1 |> ignore

        let rest =
            match dim with
                | 0 -> image.Size.YZ
                | 1 -> image.Size.XZ
                | 2 -> image.Size.XY
                | _ -> failwith "[GPGPU] invalid dimension"

        [
            // fix the shit
            ComputeCommand.Sync(image.Texture)
            ComputeCommand.Bind fixup
            ComputeCommand.SetInput args1
            ComputeCommand.Dispatch(V3i(ceilDiv (range.count - Kernels.scanSize) Kernels.halfScanSize, rest.X, rest.Y))
        ]

    let rec buildDim (args : HashSet<MutableComputeInputBinding>) (imgDim : int) (dim : int) (image : ITextureSubResource) (range : ScanRange) =
        
        if range.count <= 1 then
            []
        else
            [
                yield! scanBlocks args imgDim dim image range

                let innerRange = next range
                yield! buildDim args imgDim dim image innerRange

                if range.count > Kernels.scanSize then
                    yield! repair args imgDim dim image range

            ]

        

    let rec build (args : HashSet<MutableComputeInputBinding>) (input : ITextureSubResource) (output : ITextureSubResource) =
        let imageDim = 
            match input.Texture.Dimension with
                | TextureDimension.Texture2D -> 2
                | TextureDimension.Texture3D -> 3
                | d -> failwithf "invalid texture dimension in scan: %A" d

        let scanTexture = scanTexture(imageDim).Value

        let xInput = runtime.CreateInputBinding(scanTexture)
        xInput.["inputTexture"] <- input.Texture
        xInput.["inOutImage"] <- output
        xInput.["dimension"] <- 0
        xInput.["Offset"] <- 0
        xInput.["Size"] <- input.Size.X
        xInput.["Delta"] <- 1
        xInput.Flush()

        [
            yield ComputeCommand.TransformLayout(input.Texture, TextureLayout.ShaderRead)
            yield ComputeCommand.TransformLayout(output.Texture, TextureLayout.ShaderReadWrite)

            yield ComputeCommand.Bind scanTexture
            yield ComputeCommand.SetInput xInput
            yield ComputeCommand.Dispatch(V3i(ceilDiv input.Size.X Kernels.scanSize, input.Size.Y, input.Size.Z))
            yield ComputeCommand.Sync(output.Texture)

            let xRange = { offset = 0; delta = 1; count = input.Size.X }
            yield! buildDim args imageDim 0 output (next xRange)
            if xRange.count > Kernels.scanSize then
                yield! repair args imageDim 0 output xRange

            for dim in 1 .. imageDim-1 do
                let dimRange = { offset = 0; delta = 1; count = input.Size.[dim] }
                yield! buildDim args imageDim dim output dimRange
        ]



        
    member x.Compile (input : ITextureSubResource, output : ITextureSubResource) : IComputePrimitive<unit> =
        let args = System.Collections.Generic.HashSet<MutableComputeInputBinding>()
        let cmd = build args input output
        let task = runtime.CompileCompute cmd
        
        { new ComputePrimitive<unit>(task) with
            member x.Release() =
                for a in args do a.Dispose()
                args.Clear()
        }
        
    member x.Run (input : ITextureSubResource, output : ITextureSubResource, renderToken : RenderToken) =
        let args = System.Collections.Generic.HashSet<MutableComputeInputBinding>()
        let cmd = build args input output
        runtime.Run(cmd, renderToken)
        for a in args do a.Dispose()
        args.Clear()
  
    member x.Dispose() =
        release()

    interface IDisposable with
        member x.Dispose() = x.Dispose()


[<AbstractClass>]
type private Existential<'r>() =
    static let visitMeth = typeof<Existential<'r>>.GetMethod "Visit"
    
    abstract member Visit<'a when 'a : unmanaged> : Option<'a> -> 'r

    member x.Run(t : System.Type) =
        let m = visitMeth.MakeGenericMethod [| t |]
        m.Invoke(x, [| null |]) |> unbox<'r>

    static member Run(t : System.Type, e : Existential<'r>) =
        e.Run t

type ParallelPrimitives(runtime : IComputeRuntime) =
    do FShade.Serializer.Init()

    let sumCache = ConcurrentDict<System.Type, obj>(Dict())

    let mapCache = new ExpressionCache()
    let scanCache = new ExpressionCache()
    let reduceCache = new ExpressionCache()
    let mapReduceCache = new ExpressionCache()
    let scanImage2dCache = new ExpressionCache()

    let getMapper (map : Expr<int -> 'a -> 'b>) = mapCache.GetOrCreate(map, fun map -> new Map<'a, 'b>(runtime, map))
    let getScanner (add : Expr<'a -> 'a -> 'a>) = scanCache.GetOrCreate(add, fun add -> new Scan<'a>(runtime, add))
    let getReducer (add : Expr<'a -> 'a -> 'a>) = reduceCache.GetOrCreate(add, fun add -> new Reduce<'a>(runtime, add))
    let getMapReducer (map : Expr<int -> 'a -> 'b>) (add : Expr<'b -> 'b -> 'b>) =
        mapReduceCache.GetOrCreate(map, add, fun map add -> 
            let reducer = getReducer add
            new MapReduce<'a, 'b>(runtime, reducer, map, add)
        )

    let getImageScanner2d (add : Expr<V4d -> V4d -> V4d>) = scanImage2dCache.GetOrCreate(add, fun add -> new ScanImage2d(runtime, add))

    let getImageMapReducer (map : Expr<V3i -> V4f -> 'b>) (add : Expr<'b -> 'b -> 'b>) =
        mapReduceCache.GetOrCreate(map, add, fun map add -> 
            let reducer = getReducer add
            new MapReduceImage<'b>(runtime, reducer, map, add)
        )

    let getSum (t : System.Type) =
        sumCache.GetOrCreate(t, fun t ->
            Existential.Run(t, 
                { new Existential<obj>() with
                    member x.Visit(o : Option<'a>) =
                        getReducer Add<'a>.Expr :> obj
                }
            )
        )

    member x.Runtime = runtime

    member x.CompileScan(add : Expr<'a -> 'a -> 'a>, input : IBufferVector<'a>, output : IBufferVector<'a>) =
        let scanner = getScanner add
        scanner.Compile(input, output)

    member x.CompileScan(add : Expr<V4d -> V4d -> V4d>, input : ITextureSubResource, output : ITextureSubResource) =
        let scanner = getImageScanner2d add
        scanner.Compile(input, output)

    member x.CompileFold(add : Expr<'a -> 'a -> 'a>, input : IBufferVector<'a>) =
        let reducer = getReducer add
        reducer.Compile(input)

    member x.CompileMapReduce(map : Expr<int -> 'a -> 'b>, add : Expr<'b -> 'b -> 'b>, input : IBufferVector<'a>) =
        let reducer = getMapReducer map add
        reducer.Compile(input)

    member x.CompileMapReduce(map : Expr<V3i -> V4f -> 'b>, add : Expr<'b -> 'b -> 'b>, input : ITextureSubResource) =
        let reducer = getImageMapReducer map add
        reducer.Compile(input)

    member x.CompileMap(map : Expr<int -> 'a -> 'b>, input : IBufferVector<'a>, output : IBufferVector<'b>) =
        let mapper = getMapper map
        mapper.Compile(input, output)

    member x.CompileSum(b : IBufferVector<'a>) =
        let s = getSum typeof<'a> |> unbox<Reduce<'a>>
        s.Compile(b)

    // Overloads with render token
    member x.Scan(add : Expr<'a -> 'a -> 'a>, input : IBufferVector<'a>, output : IBufferVector<'a>, renderToken : RenderToken) =
        let scanner = getScanner add
        scanner.Run(input, output, renderToken)

    member x.Scan(add : Expr<V4d -> V4d -> V4d>, input : ITextureSubResource, output : ITextureSubResource, renderToken : RenderToken) =
        let scanner = getImageScanner2d add
        scanner.Run(input, output, renderToken)

    member x.Fold(add : Expr<'a -> 'a -> 'a>, input : IBufferVector<'a>, renderToken : RenderToken) =
        let folder = getReducer add
        folder.Run(input, renderToken)

    member x.MapReduce(map : Expr<int -> 'a -> 'b>, add : Expr<'b -> 'b -> 'b>, input : IBufferVector<'a>, renderToken : RenderToken) =
        let reducer = getMapReducer map add
        reducer.Run(input, renderToken)

    member x.MapReduce(map : Expr<V3i -> V4f -> 'b>, add : Expr<'b -> 'b -> 'b>, input : ITextureSubResource, renderToken : RenderToken) =
        let reducer = getImageMapReducer map add
        reducer.Run(input, renderToken)

    member x.Sum(input : ITextureSubResource, renderToken : RenderToken) =
        if input.Size.AllGreaterOrEqual 1 then
            let reducer = getImageMapReducer <@ fun _ v -> v @> <@ (+) @>
            reducer.Run(input, renderToken)
        else
            V4f.Zero

    member x.Min(input : ITextureSubResource, renderToken : RenderToken) =
        let reducer = getImageMapReducer <@ fun _ v -> v @> <@ min @>
        reducer.Run(input, renderToken)

    member x.Map(map : Expr<int -> 'a -> 'b>, input : IBufferVector<'a>, output : IBufferVector<'b>, renderToken : RenderToken) =
        let mapper = getMapper map
        mapper.Run(input, output, renderToken)

    member x.Sum(b : IBufferVector<'a>, renderToken : RenderToken) : 'a =
        let s = getSum typeof<'a> |> unbox<Reduce<'a>>
        s.Run(b, renderToken)

    // Overloads without render token
    member x.Scan(add : Expr<'a -> 'a -> 'a>, input : IBufferVector<'a>, output : IBufferVector<'a>) =
        x.Scan(add, input, output, RenderToken.Empty)

    member x.Scan(add : Expr<V4d -> V4d -> V4d>, input : ITextureSubResource, output : ITextureSubResource) =
        x.Scan(add, input, output, RenderToken.Empty)

    member x.Fold(add : Expr<'a -> 'a -> 'a>, input : IBufferVector<'a>) =
        x.Fold(add, input, RenderToken.Empty)

    member x.MapReduce(map : Expr<int -> 'a -> 'b>, add : Expr<'b -> 'b -> 'b>, input : IBufferVector<'a>) =
        x.MapReduce(map, add, input, RenderToken.Empty)

    member x.MapReduce(map : Expr<V3i -> V4f -> 'b>, add : Expr<'b -> 'b -> 'b>, input : ITextureSubResource) =
        x.MapReduce(map, add, input, RenderToken.Empty)

    member x.Sum(input : ITextureSubResource) =
        x.Sum(input, RenderToken.Empty)

    member x.Min(input : ITextureSubResource) =
        x.Min(input, RenderToken.Empty)

    member x.Map(map : Expr<int -> 'a -> 'b>, input : IBufferVector<'a>, output : IBufferVector<'b>) =
        x.Map(map, input, output, RenderToken.Empty)

    member x.Sum(b : IBufferVector<'a>) =
        x.Sum(b, RenderToken.Empty)

    member x.Dispose() =
        sumCache.Clear()
        mapCache.Dispose()
        scanCache.Dispose()
        reduceCache.Dispose()
        mapReduceCache.Dispose()
        scanImage2dCache.Dispose()

    interface IDisposable with
        member x.Dispose() = x.Dispose()