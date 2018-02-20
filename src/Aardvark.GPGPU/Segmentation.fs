namespace Aardvark.Base

open System
open Microsoft.FSharp.Quotations
open System.Collections.Generic
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop
open Aardvark.Base.Rendering

#nowarn "9"
#nowarn "51"


[<RequireQualifiedAccess>]
type SegmentMergeMode =
    | ValueToAvg
    | AvgToAvg
    | TTest


[<StructLayout(LayoutKind.Sequential); StructuredFormatDisplay("{AsString}")>]
type RegionStats =
    struct
        val mutable public Count : int
        val mutable public SurfaceCount : int
        val mutable public Id : int
        val mutable public Average : float32
        val mutable public StdDev : float32

        member private x.AsString = x.ToString()

        [<ReflectedDefinition>]
        new(count, surface, id, avg, dev) = { Count = count; SurfaceCount = surface; Id = id; Average = avg; StdDev = dev }

        override x.ToString() =
            sprintf "{ Count = %A; SurfaceCount = %A; Average = %A; StdDev = %A; Id = %A }" x.Count x.SurfaceCount x.Average x.StdDev x.Id

    end


module SegmentationShaders =
    open FShade

    [<StructLayout(LayoutKind.Explicit, Size = 24)>]
    type Config =
        struct
            [<FieldOffset(0)>]
            val mutable public Offset : V4i
           
            [<FieldOffset(16)>]
            val mutable public Threshold : float32

            [<FieldOffset(20)>]
            val mutable public Alpha : float32

            new(o,t,a) = { Offset = o; Threshold = t; Alpha = a }

        end

    [<GLSLIntrinsic("floatBitsToInt({0})")>]
    let floatBitsToInt (v : float) : int = onlyInShaderCode "floatBitsToInt"
        
    [<GLSLIntrinsic("intBitsToFloat({0})")>]
    let intBitsToFloat (v : int) : float = onlyInShaderCode "intBitsToFloat"

    [<GLSLIntrinsic("atomicAdd({0}, {1})")>]
    let atomicAdd (l : int) (v : int) : int = onlyInShaderCode "atomicAdd"
    
    [<GLSLIntrinsic("atomicCompSwap({0}, {1}, {2})")>]
    let atomicCompareExchange (a : int) (b : int) (c : int) : int = onlyInShaderCode "atomicCompareExchange"

    type RegionInfo =
        {
            average : float
            variance : float
            count : int
        }

    [<ReflectedDefinition>]
    let newColor2d (coord : V2i) (size : V2i) =
        coord.Y * size.X + coord.X 
        
    [<ReflectedDefinition>]
    let newColor3d (coord : V3i) (size : V3i) =
        coord.Z * size.X * size.Y + coord.Y * size.X + coord.X 



    [<LocalSize(X = 8, Y = 8)>]
    let initRegions (config : Config[]) (regions : IntImage2d<Formats.r32i>) (colors : Image2d<Formats.r16>) (infos : V4i[]) =
        compute {
            let offset = config.[0].Offset.XY
            let blockSize : V2i = uniform?BlockSize
            let size = colors.Size


            let id = getGlobalId().XY
            if id.X < blockSize.X && id.Y < blockSize.Y then
                let cid = offset + id
                let mutable v = 0.0
                let mutable cnt = 0
                let color = newColor2d id blockSize
                if cid.X < size.X && cid.Y < size.Y then
                    v <- colors.[cid].X
                    cnt <- 1
                    regions.[cid] <- V4i(color, 0, 0, 0)

                infos.[color] <- V4i(cnt, floatBitsToInt v, floatBitsToInt (v * v), 0)
        }

    [<LocalSize(X = 64)>]
    let regionMerge (identical : Expr<Config -> RegionInfo -> float -> RegionInfo -> float -> bool>) (config : Config[])  (colors : Image2d<Formats.r16>) (regions : IntImage2d<Formats.r32i>) (infos : V4i[]) =
        compute {
            let solvedSize : int = uniform?SolvedSize
            let dim : int = uniform?Dimension
            let colorSize = colors.Size
            let cfg = config.[0]
            let offset = cfg.Offset.XY
            let blockSize : V2i = uniform?BlockSize

            let id = getGlobalId().XY

            let lid =
                if dim = 0 then V2i(id.Y * solvedSize * 2 + (solvedSize - 1), id.X)
                else V2i(id.X, id.Y * solvedSize * 2 + (solvedSize - 1))

            let rid = 
                if dim = 0 then lid + V2i.IO
                else lid + V2i.OI


            if rid.X < blockSize.X && rid.Y < blockSize.Y then
                let clid = offset + lid
                let crid = offset + rid

                let lRegion = if clid.X < colorSize.X && clid.Y < colorSize.Y then regions.[clid].X else -1
                let rRegion = if crid.X < colorSize.X && crid.Y < colorSize.Y then regions.[crid].X else -1

                if lRegion >= 0 && rRegion >= 0 then

                    let lCnt = infos.[lRegion].X
                    let rCnt = infos.[rRegion].X

                    let lSum = intBitsToFloat infos.[lRegion].Y
                    let lSumSq = intBitsToFloat infos.[lRegion].Z
                    let rSum = intBitsToFloat infos.[rRegion].Y
                    let rSumSq =intBitsToFloat infos.[rRegion].Z

                    let lAvg = lSum / float lCnt
                    let rAvg = rSum / float rCnt

                    let lVar = if lCnt < 2 then 0.0 else (lSumSq - float lCnt*lAvg*lAvg) / (float (lCnt - 1))
                    let rVar = if rCnt < 2 then 0.0 else (rSumSq - float rCnt*rAvg*rAvg) / (float (rCnt - 1))

                    let lValue = colors.[clid].X
                    let rValue = colors.[crid].X

                    let lRegionInfo = { average = lAvg; variance = lVar; count = lCnt }
                    let rRegionInfo = { average = rAvg; variance = rVar; count = rCnt }

   
                    let identical = (%identical) cfg lRegionInfo lValue rRegionInfo rValue

                    if identical then
                        
                        let mutable src0 = rRegion
                        let mutable dst0 = lRegion

                        let mutable finished = false
                        let mutable o1 = infos.[src0].W
                            
                        while not finished && src0 <> dst0 do
                            if o1 = dst0 + 1 then
                                finished <- true

                            elif o1 <> 0 && o1 < dst0 + 1 then
                                // 6 -> 3
                                // ? 6 -> 5
                                // ==> 5 -> 3

                                // dst -> o
                                src0 <- dst0
                                dst0 <- o1 - 1
                                o1 <- infos.[src0].W

                            else
                                let r1 = atomicCompareExchange infos.[src0].W o1 (dst0 + 1) //collapseImg.AtomicCompareExchange(src, o, dst + 1)
                                if r1 = o1 then
                                    if r1 = 0 then
                                        finished <- true
                                    else
                                        // r -> dst
                                        src0 <- r1 - 1
                                        // 6 -> 5
                                        // ? 6 -> 3
                                        // 5 -> 3

                                        
                                else
                                    o1 <- r1


                ()

        }
            
    [<LocalSize(X = 8, Y = 8)>]
    let sanitize (config : Config[]) (regions : IntImage2d<Formats.r32i>) (infos : V4i[]) =
        compute {
            let regionSize = regions.Size
            let offset = config.[0].Offset.XY
            let id = offset + getGlobalId().XY

            if id.X < regionSize.X && id.Y < regionSize.Y then
                let r = regions.[id].X

                let mutable last1 = 0
                let mutable dst1 = infos.[r].W //collapseImg.[rc].X
                while dst1 <> 0 do
                    last1 <- dst1
                    dst1 <- infos.[(dst1 - 1)].W //collapseImg.[storageCoord2d (dst - 1) blockSize].X
                    
                if last1 <> 0 then
                    regions.[id] <- V4i(last1 - 1, 0, 0, 0)
        }

    [<LocalSize(X = 8, Y = 8)>]
    let remapRegionsScanned (scannedValid : int[]) (regions : IntImage2d<Formats.r32i>) =
        compute {
            let size = regions.Size
            let regionOffset : int = uniform?RegionOffset
            let imageOffset : V2i = uniform?ImageOffset

            let id = getGlobalId().XY
            let cid = imageOffset + id
            if cid.X < size.X && cid.Y < size.Y then
                let rCode = regions.[cid].X
                
                let last = if rCode > 0 then scannedValid.[rCode - 1] else 0
                let self = scannedValid.[rCode]
                if self > last then
                    regions.[cid] <- V4i(regionOffset + last, 0, 0, 0)
        }

    [<LocalSize(X = 8, Y = 8)>]
    let calculateSurfaceArea (regions : IntImage2d<Formats.r32i>) (stats : RegionStats[]) =
        compute {
            let size = regions.Size
            let id = getGlobalId().XY
            if id.X < size.X && id.Y < size.Y then
                let rCode = regions.[id].X

                let mutable isBorder = false
                if id.X > 0 && regions.[id - V2i.IO].X <> rCode then isBorder <- true
                if not isBorder && id.Y > 0 && regions.[id - V2i.OI].X <> rCode then isBorder <- true
                if not isBorder && id.X < size.X - 1 && regions.[id + V2i.IO].X <> rCode then isBorder <- true
                if not isBorder && id.Y < size.Y - 1 && regions.[id + V2i.OI].X <> rCode then isBorder <- true

                if isBorder then
                    let dummy = atomicAdd stats.[rCode].SurfaceCount 1
                    ()

                ()
        }



    [<LocalSize(X = 4, Y = 4, Z = 4)>]
    let initRegions3d (config : Config[]) (regions : IntImage3d<Formats.r32i>) (colors : Image3d<Formats.r16>) (infos : V4i[]) =
        compute {
            let offset = config.[0].Offset.XYZ
            let blockSize : V3i = uniform?BlockSize
            let size = colors.Size


            let id = getGlobalId()
            if id.X < blockSize.X && id.Y < blockSize.Y && id.Z < blockSize.Z then
                let cid = offset + id
                let mutable v = 0.0
                let mutable cnt = 0
                let color = newColor3d id blockSize
                if cid.X < size.X && cid.Y < size.Y && cid.Z < size.Z then
                    v <- colors.[cid].X
                    cnt <- 1
                    regions.[cid] <- V4i(color, 0, 0, 0)

                infos.[color] <- V4i(cnt, floatBitsToInt v, floatBitsToInt (v * v), 0)
        }

    [<LocalSize(X = 8, Y = 8)>]
    let regionMerge3d (identical : Expr<Config -> RegionInfo -> float -> RegionInfo -> float -> bool>) (config : Config[])  (colors : Image3d<Formats.r16>) (regions : IntImage3d<Formats.r32i>) (infos : V4i[]) =
        compute {
            let solvedSize : int = uniform?SolvedSize
            let dim : int = uniform?Dimension
            let colorSize = colors.Size
            let cfg = config.[0]
            let offset = cfg.Offset.XYZ
            let blockSize : V3i = uniform?BlockSize

            let id = getGlobalId()

            let lid =
                if dim = 0 then V3i(id.Z * solvedSize * 2 + (solvedSize - 1), id.X, id.Y)
                elif dim = 1 then V3i(id.X, id.Z * solvedSize * 2 + (solvedSize - 1), id.Y)
                else V3i(id.X, id.Y, id.Z * solvedSize * 2 + (solvedSize - 1))

            let rid = 
                if dim = 0 then lid + V3i.IOO
                elif dim = 1 then  lid + V3i.OIO
                else lid + V3i.OOI


            if rid.X < blockSize.X && rid.Y < blockSize.Y && rid.Z < blockSize.Z then
                let clid = offset + lid
                let crid = offset + rid

                let lRegion = if clid.X < colorSize.X && clid.Y < colorSize.Y && clid.Z < colorSize.Z then regions.[clid].X else -1
                let rRegion = if crid.X < colorSize.X && crid.Y < colorSize.Y && crid.Z < colorSize.Z then regions.[crid].X else -1

                if lRegion >= 0 && rRegion >= 0 then

                    let lCnt = infos.[lRegion].X
                    let rCnt = infos.[rRegion].X

                    let lSum = intBitsToFloat infos.[lRegion].Y
                    let lSumSq = intBitsToFloat infos.[lRegion].Z
                    let rSum = intBitsToFloat infos.[rRegion].Y
                    let rSumSq = intBitsToFloat infos.[rRegion].Z

                    let lAvg = lSum / float lCnt
                    let rAvg = rSum / float rCnt

                    let lVar = if lCnt < 2 then 0.0 else (lSumSq - float lCnt*lAvg*lAvg) / (float (lCnt - 1))
                    let rVar = if rCnt < 2 then 0.0 else (rSumSq - float rCnt*rAvg*rAvg) / (float (rCnt - 1))

                    let lValue = colors.[clid].X
                    let rValue = colors.[crid].X

                    let lRegionInfo = { average = lAvg; variance = lVar; count = lCnt }
                    let rRegionInfo = { average = rAvg; variance = rVar; count = rCnt }

   
                    let identical = (%identical) cfg lRegionInfo lValue rRegionInfo rValue

                    if identical then
                        
                        let mutable src0 = rRegion
                        let mutable dst0 = lRegion

                        let mutable finished = false
                        let mutable o1 = infos.[src0].W
                            
                        while not finished && src0 <> dst0 do
                            if o1 = dst0 + 1 then
                                finished <- true

                            elif o1 <> 0 && o1 < dst0 + 1 then
                                // 6 -> 3
                                // ? 6 -> 5
                                // ==> 5 -> 3

                                // dst -> o
                                src0 <- dst0
                                dst0 <- o1 - 1
                                o1 <- infos.[src0].W

                            else
                                let r1 = atomicCompareExchange infos.[src0].W o1 (dst0 + 1) //collapseImg.AtomicCompareExchange(src, o, dst + 1)
                                if r1 = o1 then
                                    if r1 = 0 then
                                        finished <- true
                                    else
                                        // r -> dst
                                        src0 <- r1 - 1
                                        // 6 -> 5
                                        // ? 6 -> 3
                                        // 5 -> 3

                                        
                                else
                                    o1 <- r1


                ()

        }
           
    [<LocalSize(X = 4, Y = 4, Z = 4)>]
    let sanitize3d (config : Config[]) (regions : IntImage3d<Formats.r32i>) (infos : V4i[]) =
        compute {
            let regionSize = regions.Size
            let offset = config.[0].Offset.XYZ
            let id = offset + getGlobalId()

            if id.X < regionSize.X && id.Y < regionSize.Y && id.Z < regionSize.Z then
                let r = regions.[id].X

                let mutable last1 = 0
                let mutable dst1 = infos.[r].W //collapseImg.[rc].X
                while dst1 <> 0 do
                    last1 <- dst1
                    dst1 <- infos.[(dst1 - 1)].W //collapseImg.[storageCoord2d (dst - 1) blockSize].X
                    
                if last1 <> 0 then
                    regions.[id] <- V4i(last1 - 1, 0, 0, 0)
        }

    [<LocalSize(X = 4, Y = 4, Z = 4)>]
    let remapRegionsScanned3d (scannedValid : int[]) (regions : IntImage3d<Formats.r32i>) =
        compute {
            let size = regions.Size
            let regionOffset : int = uniform?RegionOffset
            let imageOffset : V3i = uniform?ImageOffset

            let id = getGlobalId()
            let cid = imageOffset + id
            if cid.X < size.X && cid.Y < size.Y && cid.Z < size.Z then
                let rCode = regions.[cid].X
                
                let last = if rCode > 0 then scannedValid.[rCode - 1] else 0
                let self = scannedValid.[rCode]
                if self > last then
                    regions.[cid] <- V4i(regionOffset + last, 0, 0, 0)
        }

    [<LocalSize(X = 4, Y = 4, Z = 4)>]
    let calculateSurfaceArea3d (regions : IntImage3d<Formats.r32i>) (stats : RegionStats[]) =
        compute {
            let size = regions.Size
            let id = getGlobalId()
            if id.X < size.X && id.Y < size.Y && id.Z < size.Z then
                let rCode = regions.[id].X

                let mutable isBorder = false
                if id.X > 0 && regions.[id - V3i.IOO].X <> rCode then isBorder <- true
                if not isBorder && id.Y > 0 && regions.[id - V3i.OIO].X <> rCode then isBorder <- true
                if not isBorder && id.Z > 0 && regions.[id - V3i.OOI].X <> rCode then isBorder <- true
                if not isBorder && id.X < size.X - 1 && regions.[id + V3i.IOO].X <> rCode then isBorder <- true
                if not isBorder && id.Y < size.Y - 1 && regions.[id + V3i.OIO].X <> rCode then isBorder <- true
                if not isBorder && id.Z < size.Z - 1 && regions.[id + V3i.OIO].X <> rCode then isBorder <- true

                if isBorder then
                    let dummy = atomicAdd stats.[rCode].SurfaceCount 1
                    ()

                ()
        }



    [<LocalSize(X = 64)>]
    let regionsValid (infos : V4i[]) (valid : int[]) =
        compute {
            let count : int = uniform?RegionCount
            let id = getGlobalId().X
            if id < count then
                let cnt = infos.[id].X
                valid.[id] <- (if cnt > 0 then 1 else 0)
        }

    [<LocalSize(X = 64)>]
    let sanitizeAverage (infos : V4i[]) =
        compute {
            let count : int = uniform?RegionCount
            let rCode = getGlobalId().X


            if rCode < count then
                let srcCnt = infos.[rCode].X
                if srcCnt > 0 then
                    let mutable last1 = 0
                    let mutable dst1 = infos.[rCode].W
                    while dst1 <> 0 do
                        last1 <- dst1
                        dst1 <- infos.[(dst1 - 1)].W
                        
                    if last1 <> 0 then
                        let dst0 = last1 - 1

                        let srcSum = intBitsToFloat infos.[rCode].Y
                        let srcSumSq = intBitsToFloat infos.[rCode].Z

                        atomicAdd infos.[dst0].X srcCnt |> ignore

                        let mutable o = infos.[dst0].Y
                        let mutable n = intBitsToFloat o + srcSum
                        let mutable r = atomicCompareExchange infos.[dst0].Y o (floatBitsToInt n)
                        while r <> o do
                            o <- r
                            n <- intBitsToFloat o + srcSum
                            r <- atomicCompareExchange infos.[dst0].Y o (floatBitsToInt n)
                            
                        let mutable o = infos.[dst0].Z
                        let mutable n = intBitsToFloat o + srcSumSq
                        let mutable r = atomicCompareExchange infos.[dst0].Z o (floatBitsToInt n)
                        while r <> o do
                            o <- r
                            n <- intBitsToFloat o + srcSumSq
                            r <- atomicCompareExchange infos.[dst0].Z o (floatBitsToInt n)
                            


                        infos.[rCode].X <- 0
                        //collapseImg.[rc] <- V4i.Zero

                    ()



        }

    [<LocalSize(X = 64)>]
    let toRegionStats (count : int) (infos : V4i[]) (stats : RegionStats[]) =
        compute {
            let id = getGlobalId().X
            if id < count then
                let info = infos.[id]
                let cnt = info.X
                let sum = intBitsToFloat info.Y
                let sumSq = intBitsToFloat info.Z

                let avg = sum / float cnt
                let var = if cnt < 2 then 0.0 else (sumSq - float cnt*avg*avg) / (float (cnt - 1))
                let dev = var |> sqrt
                let mutable res = Unchecked.defaultof<RegionStats>
                res.Count <- cnt
                res.SurfaceCount <- 0
                res.Id <- id
                res.Average <- float32 avg
                res.StdDev <- float32 dev
                stats.[id] <- res
        }
        
    [<LocalSize(X = 64)>]
    let copyRegionsScanned (src : V4i[]) (dst : V4i[]) (scannedValid : int[]) =
        compute {
            let count : int = uniform?RegionCount
            let regionOffset : int = uniform?RegionOffset

            let id = getGlobalId().X
            if id < count then
                let last = if id > 0 then scannedValid.[id - 1] else 0
                let self = scannedValid.[id]

                if self > last then
                    dst.[regionOffset + last] <- V4i(src.[id].XYZ, 0)
        }








    [<LocalSize(X = 64)>]
    let allocateCompactRegions (infos : V4i[]) (denseIds : int[]) (denseIdCount : int[]) =
        compute {
            let count : int = uniform?RegionCount
            let id = getGlobalId().X

            if id < count then
                let cnt = infos.[id].X
                if cnt > 0 then
                    // allocate a slot
                    let resId = atomicAdd denseIdCount.[0] 1
                    denseIds.[id] <- resId
                else
                    denseIds.[id] <- -1

        }

    [<LocalSize(X = 64)>]
    let storeCompactRegions (store : V4i[]) (denseIds : int[]) (count : int) (infos : V4i[]) =
        compute {
            let id = getGlobalId().X
            if id < count then
                let denseId = denseIds.[id]
                if denseId <> -1 then
                    store.[denseId] <- V4i(infos.[id].XYZ, 0)
        }

    [<LocalSize(X = 8, Y = 8)>]
    let remapRegions (denseIds : int[]) (regions : IntImage2d<Formats.r32i>) =
        compute {
            let id = getGlobalId().XY
            let regionSize = regions.Size
            let offset : V2i = uniform?Offset

            let cid = offset + id

            if cid.X < regionSize.X && cid.Y < regionSize.Y then
                let oCode = regions.[cid].X
                let denseId = denseIds.[oCode]
                let nCode =
                    if denseId < 0 then oCode
                    else denseIds.[oCode]
                regions.[cid] <- V4i(nCode, 0, 0, 0)
        }
        

    let tDistr =
        sampler2d {
            texture uniform?TDistr
            filter Filter.MinMagLinear
        }

    [<ReflectedDefinition>]
    let tInv (alpha : float) (ny : float) =
        let tc =
            V2d(
                (clamp 0.001 0.5 alpha - 0.001) / 0.499,
                (clamp 1.0 128.0 ny - 1.0) / 127.0
            )

        tDistr.SampleLevel(tc, 0.0).X

    let distanceFunction =
        Map.ofList [
            SegmentMergeMode.ValueToAvg, 
            <@ fun (config : Config) (lRegion : RegionInfo) (lValue : float) (rRegion : RegionInfo) (rValue : float) ->
                min (abs (lValue - lRegion.average)) (abs (rValue - rRegion.average)) < float config.Threshold
            @>

            SegmentMergeMode.AvgToAvg,
            <@ fun (config : Config) (lRegion : RegionInfo) (lValue : float) (rRegion : RegionInfo) (rValue : float) ->
                abs (rRegion.average - lRegion.average) < float config.Threshold
            @>
            
            SegmentMergeMode.TTest,
            <@ fun (config : Config) (lRegion : RegionInfo) (_ : float) (rRegion : RegionInfo) (_ : float) ->
                
                let threshold = float config.Threshold
                let alpha = float config.Alpha

                let minCnt = if alpha < 0.0 then 100000000 else 2


                if lRegion.count < minCnt || rRegion.count < minCnt then
                    let d = abs (lRegion.average - rRegion.average)
                    d < threshold

                else
                    let dGroups = abs (lRegion.average - rRegion.average)
                    let maxVar = max lRegion.variance rRegion.variance

                    (dGroups / sqrt maxVar) < alpha


                     






//                elif lRegion.count < minCnt then
//                    let test = abs (rRegion.average - lRegion.average) / sqrt (rRegion.variance / float rRegion.count)
//                    let ny = float (rRegion.count - 1)
//                    let t = tInv alpha ny
//                    test <= t
//
//
//                elif rRegion.count < minCnt then
//                    let test = abs (lRegion.average - rRegion.average) / sqrt (lRegion.variance / float lRegion.count)
//                    let ny = float (lRegion.count - 1)
//                    let t = tInv alpha ny
//                    test <= t
//
//
//                else
//                    let v1 = lRegion.variance
//                    let v2 = rRegion.variance
//                    let N1 = float lRegion.count
//                    let N2 = float rRegion.count
//
//                    let a = (v1 / N1 + v2 / N2)
//                    let test = (lRegion.average - rRegion.average) / a
//                    let ny = (a * a) / (v1*v1 / (N1*N1*(N1 - 1.0)) + (v2*v2 / (N2*N2*(N2-1.0))))
//
//                    let t = tInv alpha ny
//                    test <= t
            @>
        ]

type internal RegionMergeKernels2d(runtime : IRuntime, mergeMode : SegmentMergeMode) =
    let initRegions = runtime.CreateComputeShader SegmentationShaders.initRegions
    let regionMerge = runtime.CreateComputeShader (SegmentationShaders.regionMerge SegmentationShaders.distanceFunction.[mergeMode])
    let sanitize = runtime.CreateComputeShader SegmentationShaders.sanitize
    let sanitizeAvg = runtime.CreateComputeShader SegmentationShaders.sanitizeAverage
    
    let allocateCompactRegions = runtime.CreateComputeShader SegmentationShaders.allocateCompactRegions
    let remapRegions = runtime.CreateComputeShader SegmentationShaders.remapRegions
    let storeCompactRegions = runtime.CreateComputeShader SegmentationShaders.storeCompactRegions
    let toRegionStats = runtime.CreateComputeShader SegmentationShaders.toRegionStats
    let calculateSurfaceArea = runtime.CreateComputeShader SegmentationShaders.calculateSurfaceArea

    let regionsValid = runtime.CreateComputeShader SegmentationShaders.regionsValid
    let copyRegionsScanned = runtime.CreateComputeShader SegmentationShaders.copyRegionsScanned
    let remapRegionsScanned = runtime.CreateComputeShader SegmentationShaders.remapRegionsScanned

    member x.Runtime = runtime
    member x.InitRegions = initRegions
    member x.RegionMerge = regionMerge
    member x.Sanitize = sanitize
    member x.SanitizeAvg = sanitizeAvg
    
    member x.AllocateCompactRegions = allocateCompactRegions
    member x.StoreCompactRegions = storeCompactRegions
    member x.RemapRegions = remapRegions
    member x.ToRegionStats = toRegionStats
    member x.CalculateSurfaceArea = calculateSurfaceArea

    member x.RegionsValid = regionsValid
    member x.CopyRegionsScanned = copyRegionsScanned
    member x.RemapRegionsScanned = remapRegionsScanned

    member x.Dispose() =
        runtime.DeleteComputeShader initRegions
        runtime.DeleteComputeShader regionMerge
        runtime.DeleteComputeShader sanitize
        runtime.DeleteComputeShader sanitizeAvg
        runtime.DeleteComputeShader allocateCompactRegions
        runtime.DeleteComputeShader storeCompactRegions
        runtime.DeleteComputeShader remapRegions
        runtime.DeleteComputeShader toRegionStats
        runtime.DeleteComputeShader calculateSurfaceArea
        runtime.DeleteComputeShader regionsValid
        runtime.DeleteComputeShader copyRegionsScanned
        runtime.DeleteComputeShader remapRegionsScanned

    interface IDisposable with
        member x.Dispose() = x.Dispose()

type internal RegionMergeKernels3d(runtime : IRuntime, mergeMode : SegmentMergeMode) =
    let initRegions = runtime.CreateComputeShader SegmentationShaders.initRegions3d
    let regionMerge = runtime.CreateComputeShader (SegmentationShaders.regionMerge3d SegmentationShaders.distanceFunction.[mergeMode])
    let sanitize = runtime.CreateComputeShader SegmentationShaders.sanitize3d
    let sanitizeAvg = runtime.CreateComputeShader SegmentationShaders.sanitizeAverage
    
    let toRegionStats = runtime.CreateComputeShader SegmentationShaders.toRegionStats
    let calculateSurfaceArea = runtime.CreateComputeShader SegmentationShaders.calculateSurfaceArea3d

    let regionsValid = runtime.CreateComputeShader SegmentationShaders.regionsValid
    let copyRegionsScanned = runtime.CreateComputeShader SegmentationShaders.copyRegionsScanned
    let remapRegionsScanned = runtime.CreateComputeShader SegmentationShaders.remapRegionsScanned3d

    member x.Runtime = runtime
    member x.InitRegions = initRegions
    member x.RegionMerge = regionMerge
    member x.Sanitize = sanitize
    member x.SanitizeAvg = sanitizeAvg
    
    member x.ToRegionStats = toRegionStats
    member x.CalculateSurfaceArea = calculateSurfaceArea

    member x.RegionsValid = regionsValid
    member x.CopyRegionsScanned = copyRegionsScanned
    member x.RemapRegionsScanned = remapRegionsScanned

    member x.Dispose() =
        runtime.DeleteComputeShader initRegions
        runtime.DeleteComputeShader regionMerge
        runtime.DeleteComputeShader sanitize
        runtime.DeleteComputeShader sanitizeAvg
        runtime.DeleteComputeShader toRegionStats
        runtime.DeleteComputeShader calculateSurfaceArea
        runtime.DeleteComputeShader regionsValid
        runtime.DeleteComputeShader copyRegionsScanned
        runtime.DeleteComputeShader remapRegionsScanned

    interface IDisposable with
        member x.Dispose() = x.Dispose()

type RegionMerge (runtime : IRuntime, mergeMode : SegmentMergeMode) =
    let par = ParallelPrimitives(runtime)

    let tDistr = 
        let img = PixImage<float32>(Col.Format.Gray, Volume<float32>(TDistribution.Data, V3i(500, 128, 1)))
        let tex = runtime.CreateTexture(img.Size, TextureFormat.R32f, 1, 1)
        runtime.Upload(tex, 0, 0, img)
        tex

    let kernels2d = lazy (new RegionMergeKernels2d(runtime, mergeMode))
    let kernels3d = lazy (new RegionMergeKernels3d(runtime, mergeMode))

    member x.TDistrTexture = tDistr
    member x.Runtime = runtime
    
    member x.NewInstance(size : V2i) =
        new RegionMergeInstance2d(kernels2d.Value, par, tDistr, size)
        
    member x.NewInstance(size : V3i) =
        new RegionMergeInstance3d(kernels3d.Value, par, tDistr, size)
        
    member x.Dispose() =
        if kernels2d.IsValueCreated then kernels2d.Value.Dispose()
        if kernels3d.IsValueCreated then kernels3d.Value.Dispose()

    interface IDisposable with
        member x.Dispose() = x.Dispose()

and RegionCompactor2d internal(parent : RegionMergeKernels2d, par : ParallelPrimitives, blockSize : V2i, initialCapacity : int) =
    let runtime = parent.Runtime

    let valid = runtime.CreateBuffer<int>(blockSize.X * blockSize.Y)
    let mutable store = runtime.CreateBuffer<V4i>(initialCapacity)
    let mutable count = 0
    let scanValid = par.CompileScan(<@ (+) @>, valid, valid)

    let regionsValidIn =
        let binding = runtime.NewInputBinding parent.RegionsValid
        binding.["valid"] <- valid
        binding.["RegionCount"] <- valid.Count
        binding
        
    let copyRegionsScannedIn =
        let binding = runtime.NewInputBinding parent.CopyRegionsScanned
        binding.["scannedValid"] <- valid
        binding.["RegionCount"] <- valid.Count
        binding.["RegionOffset"] <- 0
        binding.["dst"] <- store
        binding
        
    let remapRegionsScannedIn =
        let binding = runtime.NewInputBinding parent.RemapRegionsScanned
        binding.["RegionOffset"] <- 0
        binding.["scannedValid"] <- valid
        binding.["ImageOffset"] <- V2i.Zero
        binding

    let runScan =
        runtime.Compile [
            ComputeCommand.Bind parent.RegionsValid
            ComputeCommand.SetInput regionsValidIn
            ComputeCommand.Dispatch (ceilDiv valid.Count 64)
            ComputeCommand.Sync valid.Buffer

            ComputeCommand.Execute scanValid
        ]

    let copy =
        runtime.Compile [
            ComputeCommand.Bind parent.CopyRegionsScanned
            ComputeCommand.SetInput copyRegionsScannedIn
            ComputeCommand.Dispatch (ceilDiv valid.Count 64)

        ]

    let mutable lastInfos = None

    member x.Clear() =
        if store.Count > initialCapacity then
            store.Dispose()
            store <- runtime.CreateBuffer<V4i>(initialCapacity)
        count <- 0

    member x.AddRange(infos : IBufferRange<V4i>, regions : IBackendTexture, imageOffset : V2i) =

        let subInfos =
            if infos.Count > valid.Count then
                [0 .. valid.Count .. infos.Count - 1] |> List.map (fun o -> 
                    let size = min valid.Count (infos.Count - o)
                    infos.[o..o+size-1]
                )
            else
                [infos]
        for infos in subInfos do
            if lastInfos <> Some infos then
                if infos.Count > valid.Count then
                    failwithf "[RegionCompactor] large infos not implemented"

                let offset = int (infos.Offset / nativeint sizeof<V4i>)
                lastInfos <- Some infos
                regionsValidIn.["infos"] <- infos
                regionsValidIn.["RegionCount"] <- infos.Count
                regionsValidIn.Flush()
            
                copyRegionsScannedIn.["src"] <- infos
                copyRegionsScannedIn.["RegionCount"] <- infos.Count
                copyRegionsScannedIn.Flush()

            runScan.Run()

            let additional = 
                let last = infos.Count - 1 
                valid.[last .. last].Download().[0]

            let newCount = count + additional

            if newCount > store.Count then
                let newCapacity = Fun.NextPowerOfTwo newCount
                let newStore = runtime.CreateBuffer<V4i>(newCapacity)
                if count > 0 then
                    runtime.Run [
                        ComputeCommand.Copy(store.[0..count-1], newStore.[0..count-1])
                    ]

                store.Dispose()
                store <- newStore

        
            copyRegionsScannedIn.["dst"] <- store
            copyRegionsScannedIn.["RegionOffset"] <- count
            copyRegionsScannedIn.Flush()

            remapRegionsScannedIn.["regions"] <- regions.[TextureAspect.Color, 0, 0]
            remapRegionsScannedIn.["ImageOffset"] <- imageOffset
            remapRegionsScannedIn.["RegionOffset"] <- count
            remapRegionsScannedIn.Flush()

            runtime.Run [
                ComputeCommand.Execute copy
            
                ComputeCommand.Bind parent.RemapRegionsScanned
                ComputeCommand.SetInput remapRegionsScannedIn
                ComputeCommand.Dispatch (V2i(ceilDiv blockSize.X 8, ceilDiv blockSize.Y 8))
            ]
            count <- newCount

    member x.Buffer = store.[0..count-1]

and RegionCompactor2dAtomic internal(parent : RegionMergeKernels2d, par : ParallelPrimitives, blockSize : V2i, initialCapacity : int) =
    let runtime = parent.Runtime

    let counter = runtime.CreateBuffer<int>(1)
    let mutable store = runtime.CreateBuffer<V4i>(initialCapacity)
    let mutable denseIds = runtime.CreateBuffer<int>(blockSize.X * blockSize.Y)
  

    let allocRegionsIn =
        let binding = runtime.NewInputBinding parent.AllocateCompactRegions
        binding.["denseIds"] <- denseIds
        binding.["denseIdCount"] <- counter
        binding.["RegionCount"] <- 0
        binding
        
    let storeRegionsIn =
        let binding = runtime.NewInputBinding parent.StoreCompactRegions
        binding.["store"] <- store
        binding.["denseIds"] <- denseIds
        binding.["count"] <- 0

        binding

    let remapIn =
        let binding = runtime.NewInputBinding parent.RemapRegions
        binding.["denseIds"] <- denseIds

        binding

    let alloc =
        runtime.Compile [
            ComputeCommand.Bind parent.AllocateCompactRegions
            ComputeCommand.SetInput allocRegionsIn
            ComputeCommand.Dispatch(ceilDiv denseIds.Count 64)
        ]

    let flush = 
        runtime.Compile [
            ComputeCommand.Bind parent.StoreCompactRegions
            ComputeCommand.SetInput storeRegionsIn
            ComputeCommand.Dispatch(ceilDiv denseIds.Count 64)
        ]

    let remap = 
        runtime.Compile [
            ComputeCommand.Bind parent.RemapRegions
            ComputeCommand.SetInput remapIn
            ComputeCommand.Dispatch(V2i(ceilDiv blockSize.X 8, ceilDiv blockSize.Y 8))
        ]

    member x.AddRange(infos : IBufferRange<V4i>, regions : IBackendTexture, imageOffset : V2i)  =
        let subInfos =
            if infos.Count > denseIds.Count then
                [0 .. denseIds.Count .. infos.Count - 1] |> List.map (fun o -> 
                    let size = min denseIds.Count (infos.Count - o)
                    infos.[o..o+size-1]
                )
            else
                [infos]

        for infos in subInfos do
            let offset = counter.Download().[0]

            allocRegionsIn.["infos"] <- infos
            allocRegionsIn.["RegionCount"] <- infos.Count
            allocRegionsIn.Flush()

            alloc.Run()

            let newCnt = counter.Download().[0]
            if newCnt > store.Count then
                let newCapacity = Fun.NextPowerOfTwo newCnt
                let newStore = runtime.CreateBuffer<V4i>(newCapacity)
                if offset > 0 then
                    runtime.Run [
                        ComputeCommand.Copy(store.[0..offset-1], newStore.[0..offset-1])
                    ]

                store.Dispose()
                store <- newStore


            storeRegionsIn.["infos"] <- infos
            storeRegionsIn.["count"] <- infos.Count
            storeRegionsIn.["store"] <- store
            storeRegionsIn.Flush()
            flush.Run()

            remapIn.["regions"] <- regions
            remapIn.["Offset"] <- imageOffset
            remapIn.Flush()
            remap.Run()

    member x.Clear() =
        if store.Count > initialCapacity then
            store.Dispose()
            store <- runtime.CreateBuffer<V4i>(initialCapacity)
        counter.Upload [| 0 |]

    member x.Buffer =
        let cnt = counter.Download().[0]
        store.[0 .. cnt - 1]

and RegionCompactor3d internal(parent : RegionMergeKernels3d, par : ParallelPrimitives, blockSize : V3i, initialCapacity : int) =
    let runtime = parent.Runtime

    let valid = runtime.CreateBuffer<int>(blockSize.X * blockSize.Y * blockSize.Z)
    let mutable store = runtime.CreateBuffer<V4i>(initialCapacity)
    let mutable count = 0
    let scanValid = par.CompileScan(<@ (+) @>, valid, valid)

    let regionsValidIn =
        let binding = runtime.NewInputBinding parent.RegionsValid
        binding.["valid"] <- valid
        binding.["RegionCount"] <- valid.Count
        binding
        
    let copyRegionsScannedIn =
        let binding = runtime.NewInputBinding parent.CopyRegionsScanned
        binding.["scannedValid"] <- valid
        binding.["RegionCount"] <- valid.Count
        binding.["RegionOffset"] <- 0
        binding.["dst"] <- store
        binding
        
    let remapRegionsScannedIn =
        let binding = runtime.NewInputBinding parent.RemapRegionsScanned
        binding.["RegionOffset"] <- 0
        binding.["scannedValid"] <- valid
        binding.["ImageOffset"] <- V3i.Zero
        binding

    let runScan =
        runtime.Compile [
            ComputeCommand.Bind parent.RegionsValid
            ComputeCommand.SetInput regionsValidIn
            ComputeCommand.Dispatch (ceilDiv valid.Count 64)
            ComputeCommand.Sync valid.Buffer

            ComputeCommand.Execute scanValid
        ]

    let copy =
        runtime.Compile [
            ComputeCommand.Bind parent.CopyRegionsScanned
            ComputeCommand.SetInput copyRegionsScannedIn
            ComputeCommand.Dispatch (ceilDiv valid.Count 64)

        ]

    let mutable lastInfos = None

    member x.Clear() =
        if store.Count > initialCapacity then
            store.Dispose()
            store <- runtime.CreateBuffer<V4i>(initialCapacity)
        count <- 0

    member x.AddRange(infos : IBufferRange<V4i>, regions : IBackendTexture, imageOffset : V3i) =

        let subInfos =
            if infos.Count > valid.Count then
                [0 .. valid.Count .. infos.Count - 1] |> List.map (fun o -> 
                    let size = min valid.Count (infos.Count - o)
                    infos.[o..o+size-1]
                )
            else
                [infos]
        for infos in subInfos do
            if lastInfos <> Some infos then
                if infos.Count > valid.Count then
                    failwithf "[RegionCompactor] large infos not implemented"

                let offset = int (infos.Offset / nativeint sizeof<V4i>)
                lastInfos <- Some infos
                regionsValidIn.["infos"] <- infos
                regionsValidIn.["RegionCount"] <- infos.Count
                regionsValidIn.Flush()
            
                copyRegionsScannedIn.["src"] <- infos
                copyRegionsScannedIn.["RegionCount"] <- infos.Count
                copyRegionsScannedIn.Flush()

            runScan.Run()

            let additional = 
                let last = infos.Count - 1 
                valid.[last .. last].Download().[0]

            let newCount = count + additional

            if newCount > store.Count then
                let newCapacity = Fun.NextPowerOfTwo newCount
                let newStore = runtime.CreateBuffer<V4i>(newCapacity)
                if count > 0 then
                    runtime.Run [
                        ComputeCommand.Copy(store.[0..count-1], newStore.[0..count-1])
                    ]

                store.Dispose()
                store <- newStore

        
            copyRegionsScannedIn.["dst"] <- store
            copyRegionsScannedIn.["RegionOffset"] <- count
            copyRegionsScannedIn.Flush()

            remapRegionsScannedIn.["regions"] <- regions.[TextureAspect.Color, 0, 0]
            remapRegionsScannedIn.["ImageOffset"] <- imageOffset
            remapRegionsScannedIn.["RegionOffset"] <- count
            remapRegionsScannedIn.Flush()

            runtime.Run [
                ComputeCommand.Execute copy
            
                ComputeCommand.Bind parent.RemapRegionsScanned
                ComputeCommand.SetInput remapRegionsScannedIn
                ComputeCommand.Dispatch (V3i(ceilDiv blockSize.X 4, ceilDiv blockSize.Y 4, ceilDiv blockSize.Z 4))
            ]
            count <- newCount

    member x.Buffer = store.[0..count-1]


and RegionMergeInstance2d internal(parent : RegionMergeKernels2d, par : ParallelPrimitives, tDistr : IBackendTexture, size : V2i) =  
    static let rec createMerges (s : V2i) (size : V2i) =
        if s.X < size.X && s.Y < size.Y then
            (s.X, 0) :: (s.Y, 1) :: createMerges (2 * s) size

        elif s.X < size.X then
            (s.X, 0) :: createMerges (V2i(s.X * 2, s.Y)) size 

        elif s.Y < size.Y then
            (s.Y, 1) :: createMerges (V2i(s.X, 2 * s.Y)) size
        else
            []

    static let buildMerges (size : V2i) = createMerges V2i.II size

    let runtime = parent.Runtime
        
    let config          = runtime.CreateBuffer<SegmentationShaders.Config> [| SegmentationShaders.Config(V4i.Zero, 0.01f, 0.01f) |]
    let regions         = runtime.CreateTexture(V2i.II, TextureFormat.R32i, 1, 1)

    let infos           = runtime.CreateBuffer<V4i>(size.X * size.Y)
    let compactor       = RegionCompactor2dAtomic(parent, par, size, 65536)

    let initIn = 
        let binding = runtime.NewInputBinding parent.InitRegions
        binding.["regions"] <- regions.[TextureAspect.Color, 0, 0]
        binding.["config"] <- config
        binding.["infos"] <- infos
        binding.["BlockSize"] <- size
        binding.Flush()
        binding

    let mergeInputs =
        buildMerges size |> List.map (fun (solvedSize, dim) ->
            let mergeIn = runtime.NewInputBinding parent.RegionMerge
            
            mergeIn.["Dimension"] <- dim
            mergeIn.["config"] <- config
            mergeIn.["SolvedSize"] <- solvedSize
            mergeIn.["regions"] <- regions.[TextureAspect.Color, 0, 0]
            mergeIn.["infos"] <- infos
            mergeIn.["TDistr"] <- tDistr
            mergeIn.["BlockSize"] <- size
            mergeIn.Flush()


            let groups =
                if dim = 0 then V2i(ceilDiv size.Y 64, ceilDiv (size.X - solvedSize) (2 * solvedSize))
                else V2i(ceilDiv size.X 64, ceilDiv (size.Y - solvedSize) (2 * solvedSize))


            mergeIn, groups
        )

    let sanitizeIn = 
        let binding = runtime.NewInputBinding parent.Sanitize
        binding.["config"] <- config
        binding.["regions"] <- regions.[TextureAspect.Color, 0, 0]
        binding.["infos"] <- infos
        binding.Flush()
        binding

    let sanitizeAvgIn = 
        let binding = runtime.NewInputBinding parent.SanitizeAvg
        binding.["infos"] <- infos
        binding.["RegionCount"] <- size.X * size.Y
        binding.Flush()
        binding

    let toRegionStatsIn =
        let binding = runtime.NewInputBinding parent.ToRegionStats
        binding.["infos"] <- infos
        binding.["count"] <- 0
        binding.["stats"] <- infos
        binding.Flush()
        binding

    let calculateSurfaceAreaIn =
        let binding = runtime.NewInputBinding parent.CalculateSurfaceArea
        binding

    let mutable currentConfig = SegmentationShaders.Config(V4i.Zero, 0.01f, 0.01f)
    let mutable currentInput = None
    let mutable currentRegions = regions

    let setConfig (cfg : SegmentationShaders.Config) =
        if currentConfig <> cfg then
            currentConfig <- cfg
            config.Upload [| cfg |]
        
    let setOffset (v : V2i) =
        if v <> currentConfig.Offset.XY then
            let cfg = SegmentationShaders.Config(V4i(v.X, v.Y, 0, 0), currentConfig.Threshold, currentConfig.Alpha)
            currentConfig <- cfg
            config.Upload [| cfg |]

    let setRegions (r : IBackendTexture) =
        if r <> currentRegions then
            currentRegions <- r
            initIn.["regions"] <- r.[TextureAspect.Color, 0, 0]
            initIn.Flush()

            for (m,_) in mergeInputs do
                m.["regions"] <- r.[TextureAspect.Color, 0, 0]
                m.Flush()

            sanitizeIn.["regions"] <- r.[TextureAspect.Color, 0, 0]
            sanitizeIn.Flush()

    let setThreshold (value : float) (alpha : float) =
        if currentConfig.Threshold <> float32 value || currentConfig.Alpha <> float32 alpha then
            let cfg = SegmentationShaders.Config(currentConfig.Offset, float32 value, float32 alpha)
            currentConfig <- cfg
            config.Upload [| cfg |]

    let setInput (input : IBackendTexture) =
        if currentInput <> Some input then
            initIn.["colors"] <- input.[TextureAspect.Color, 0, 0]
            initIn.Flush()
            for m,_ in mergeInputs do 
                m.["colors"] <- input.[TextureAspect.Color, 0, 0]
                m.Flush()
            currentInput <- Some input

    let program =  
        runtime.Compile [
            yield ComputeCommand.Bind parent.InitRegions
            yield ComputeCommand.SetInput initIn
            yield ComputeCommand.Dispatch (V2i(ceilDiv size.X 8, ceilDiv size.Y 8))
            yield ComputeCommand.Sync infos.Buffer

            for mergeIn, groupCount in mergeInputs do
                yield ComputeCommand.Bind parent.RegionMerge
                yield ComputeCommand.SetInput mergeIn
                yield ComputeCommand.Dispatch groupCount
                
                yield ComputeCommand.Sync infos.Buffer

                yield ComputeCommand.Bind parent.Sanitize
                yield ComputeCommand.SetInput sanitizeIn
                yield ComputeCommand.Dispatch (V2i(ceilDiv size.X 8, ceilDiv size.Y 8))

                yield ComputeCommand.Bind parent.SanitizeAvg
                yield ComputeCommand.SetInput sanitizeAvgIn
                yield ComputeCommand.Dispatch (ceilDiv (size.X * size.Y) 64)

                yield ComputeCommand.Sync infos.Buffer


        ]


    member x.Run(input : IBackendTexture, resultImage : IBackendTexture, threshold : float, alpha : float) =
        lock x (fun () ->
            let inputSize = input.Size.XY
            
            compactor.Clear()

            setInput input
            setRegions resultImage
            setThreshold threshold alpha

            let blocks = V2i(ceilDiv inputSize.X size.X, ceilDiv inputSize.Y size.Y)

            runtime.Run [
                ComputeCommand.TransformLayout(input, TextureLayout.ShaderReadWrite)
                ComputeCommand.TransformLayout(resultImage, TextureLayout.ShaderReadWrite)
            ]

            for bx in 0 .. blocks.X - 1 do
                for by in 0 .. blocks.Y - 1 do
                    let o = V2i(bx,by) * size
                    setOffset o
                    program.Run()
                    compactor.AddRange(infos, resultImage, o)
       
            let resultBuffer = compactor.Buffer
            let remainingMerges = 
                createMerges size inputSize  |> List.map (fun (solvedSize, dim) ->
                    let mergeIn = runtime.NewInputBinding parent.RegionMerge
            
                    mergeIn.["Dimension"] <- dim
                    mergeIn.["config"] <- config
                    mergeIn.["SolvedSize"] <- solvedSize
                    mergeIn.["regions"] <- resultImage.[TextureAspect.Color, 0, 0]
                    mergeIn.["infos"] <- resultBuffer.Buffer
                    mergeIn.["colors"] <- input.[TextureAspect.Color, 0, 0]
                    mergeIn.["TDistr"] <- tDistr
                    mergeIn.["BlockSize"] <- inputSize
                    mergeIn.Flush()


                    let groups =
                        if dim = 0 then V2i(ceilDiv inputSize.Y 64, ceilDiv (inputSize.X - solvedSize) (2 * solvedSize))
                        else V2i(ceilDiv inputSize.X 64, ceilDiv (inputSize.Y - solvedSize) (2 * solvedSize))

                    mergeIn, groups
                )

            let statBuffer = runtime.CreateBuffer<RegionStats>(resultBuffer.Count)
            toRegionStatsIn.["infos"] <- resultBuffer.Buffer
            toRegionStatsIn.["count"] <- resultBuffer.Count
            toRegionStatsIn.["stats"] <- statBuffer
            toRegionStatsIn.Flush()
            
            calculateSurfaceAreaIn.["regions"] <- resultImage.[TextureAspect.Color, 0, 0]
            calculateSurfaceAreaIn.["stats"] <- statBuffer
            calculateSurfaceAreaIn.Flush()

            match remainingMerges with
                | [] ->
                    runtime.Run [
                        yield ComputeCommand.Bind parent.ToRegionStats
                        yield ComputeCommand.SetInput toRegionStatsIn
                        yield ComputeCommand.Dispatch (ceilDiv resultBuffer.Count 64)

                        yield ComputeCommand.Sync statBuffer.Buffer
                        yield ComputeCommand.Bind parent.CalculateSurfaceArea
                        yield ComputeCommand.SetInput calculateSurfaceAreaIn
                        yield ComputeCommand.Dispatch(V2i(ceilDiv inputSize.X 8, ceilDiv inputSize.Y 8))
                        
                        yield ComputeCommand.TransformLayout(input, TextureLayout.ShaderRead)
                        yield ComputeCommand.TransformLayout(resultImage, TextureLayout.ShaderRead)
                    ]

                | _ -> 
                    setOffset V2i.Zero

                    let sanitizeIn = 
                        let binding = runtime.NewInputBinding parent.Sanitize
                        binding.["config"] <- config
                        binding.["regions"] <- resultImage.[TextureAspect.Color, 0, 0]
                        binding.["infos"] <- resultBuffer
                        binding.Flush()
                        binding

                    let sanitizeAvgIn = 
                        let binding = runtime.NewInputBinding parent.SanitizeAvg
                        binding.["infos"] <- resultBuffer
                        binding.["RegionCount"] <- resultBuffer.Count
                        binding.Flush()
                        binding

                    runtime.Run [
                        for mergeIn, groupCount in remainingMerges do
                            yield ComputeCommand.Bind parent.RegionMerge
                            yield ComputeCommand.SetInput mergeIn
                            yield ComputeCommand.Dispatch groupCount
                
                            yield ComputeCommand.Sync resultBuffer.Buffer

                            yield ComputeCommand.Bind parent.Sanitize
                            yield ComputeCommand.SetInput sanitizeIn
                            yield ComputeCommand.Dispatch (V2i(ceilDiv inputSize.X 8, ceilDiv inputSize.Y 8))

                            yield ComputeCommand.Bind parent.SanitizeAvg
                            yield ComputeCommand.SetInput sanitizeAvgIn
                            yield ComputeCommand.Dispatch (ceilDiv resultBuffer.Count 64)

                            yield ComputeCommand.Sync resultImage
                            yield ComputeCommand.Sync resultBuffer.Buffer
                    
                        yield ComputeCommand.Bind parent.ToRegionStats
                        yield ComputeCommand.SetInput toRegionStatsIn
                        yield ComputeCommand.Dispatch (ceilDiv resultBuffer.Count 64)
                
                        yield ComputeCommand.Sync statBuffer.Buffer
                        yield ComputeCommand.Bind parent.CalculateSurfaceArea
                        yield ComputeCommand.SetInput calculateSurfaceAreaIn
                        yield ComputeCommand.Dispatch(V2i(ceilDiv inputSize.X 8, ceilDiv inputSize.Y 8))
                
                        yield ComputeCommand.TransformLayout(input, TextureLayout.ShaderRead)
                        yield ComputeCommand.TransformLayout(resultImage, TextureLayout.ShaderRead)
                    ]

                    sanitizeIn.Dispose()
                    sanitizeAvgIn.Dispose()
                    remainingMerges |> List.iter (fun (i,_) -> i.Dispose())

            compactor.Clear()

            //resultBuffer.Value
            statBuffer

        )


    member x.Dispose() =
        runtime.DeleteTexture regions
        infos.Dispose()

        for m,_ in mergeInputs do m.Dispose()
        initIn.Dispose()
        sanitizeIn.Dispose()
        sanitizeAvgIn.Dispose()
        
    interface IDisposable with
        member x.Dispose() = x.Dispose()

and RegionMergeInstance3d internal(parent : RegionMergeKernels3d, par : ParallelPrimitives, tDistr : IBackendTexture, size : V3i) =  
    static let rec createMerges (s : V3i) (size : V3i) =
        match s.X < size.X, s.Y < size.Y, s.Z < size.Z with
            | true, true, true ->
                (s.X, 0) :: (s.Y, 1) :: (s.Z, 2) :: createMerges (2 * s) size

            | true, true, false ->
                (s.X, 0) :: (s.Y, 1) :: createMerges (V3i(s.X * 2, s.Y * 2, s.Z)) size
                    
            | true, false, true ->
                (s.X, 0) :: (s.Z, 2) :: createMerges (V3i(s.X * 2, s.Y, s.Z * 2)) size
                    
            | false, true, true ->
                (s.Y, 1) :: (s.Z, 2) :: createMerges (V3i(s.X, s.Y * 2, s.Z * 2)) size

            | true, false, false ->
                (s.X, 0) :: createMerges (V3i(s.X * 2, s.Y, s.Z)) size
                    
            | false, true, false ->
                (s.Y, 1) :: createMerges (V3i(s.X, s.Y * 2, s.Z)) size

            | false, false, true ->
                (s.Z, 2) :: createMerges (V3i(s.X, s.Y, s.Z * 2)) size

            | false, false, false ->
                []

    static let buildMerges (size : V3i) = createMerges V3i.III size

    let runtime = parent.Runtime
        
    let config          = runtime.CreateBuffer<SegmentationShaders.Config> [| SegmentationShaders.Config(V4i.Zero, 0.01f, 0.01f) |]
    let regions         = runtime.CreateTexture(V3i.III, TextureDimension.Texture3D, TextureFormat.R32i, 1, 1, 1)

    let infos           = runtime.CreateBuffer<V4i>(size.X * size.Y * size.Z)
    let compactor       = RegionCompactor3d(parent, par, size, 65536)

    let initIn = 
        let binding = runtime.NewInputBinding parent.InitRegions
        binding.["regions"] <- regions.[TextureAspect.Color, 0, 0]
        binding.["config"] <- config
        binding.["infos"] <- infos
        binding.["BlockSize"] <- size
        binding.Flush()
        binding

    let mergeInputs =
        buildMerges size |> List.map (fun (solvedSize, dim) ->
            let mergeIn = runtime.NewInputBinding parent.RegionMerge
            
            mergeIn.["Dimension"] <- dim
            mergeIn.["config"] <- config
            mergeIn.["SolvedSize"] <- solvedSize
            mergeIn.["regions"] <- regions.[TextureAspect.Color, 0, 0]
            mergeIn.["infos"] <- infos
            mergeIn.["TDistr"] <- tDistr
            mergeIn.["BlockSize"] <- size
            mergeIn.Flush()


            let groups =
                if dim = 0 then V3i(ceilDiv size.Y 8, ceilDiv size.Z 8, ceilDiv (size.X - solvedSize) (2 * solvedSize))
                elif dim = 1 then V3i(ceilDiv size.X 8, ceilDiv size.Z 8, ceilDiv (size.Y - solvedSize) (2 * solvedSize))
                else V3i(ceilDiv size.X 8, ceilDiv size.Y 8, ceilDiv (size.Z - solvedSize) (2 * solvedSize))


            mergeIn, groups
        )

    let sanitizeIn = 
        let binding = runtime.NewInputBinding parent.Sanitize
        binding.["config"] <- config
        binding.["regions"] <- regions.[TextureAspect.Color, 0, 0]
        binding.["infos"] <- infos
        binding.Flush()
        binding

    let sanitizeAvgIn = 
        let binding = runtime.NewInputBinding parent.SanitizeAvg
        binding.["infos"] <- infos
        binding.["RegionCount"] <- size.X * size.Y * size.Z
        binding.Flush()
        binding

    let toRegionStatsIn =
        let binding = runtime.NewInputBinding parent.ToRegionStats
        binding.["infos"] <- infos
        binding.["count"] <- 0
        binding.["stats"] <- infos
        binding.Flush()
        binding

    let calculateSurfaceAreaIn =
        let binding = runtime.NewInputBinding parent.CalculateSurfaceArea
        binding

    let mutable currentConfig = SegmentationShaders.Config(V4i.Zero, 0.01f, 0.01f)
    let mutable currentInput = None
    let mutable currentRegions = regions

    let setConfig (cfg : SegmentationShaders.Config) =
        if currentConfig <> cfg then
            currentConfig <- cfg
            config.Upload [| cfg |]
        
    let setOffset (v : V3i) =
        if v <> currentConfig.Offset.XYZ then
            let cfg = SegmentationShaders.Config(V4i(v.X, v.Y, v.Z, 0), currentConfig.Threshold, currentConfig.Alpha)
            currentConfig <- cfg
            config.Upload [| cfg |]

    let setRegions (r : IBackendTexture) =
        if r <> currentRegions then
            currentRegions <- r
            initIn.["regions"] <- r.[TextureAspect.Color, 0, 0]
            initIn.Flush()

            for (m,_) in mergeInputs do
                m.["regions"] <- r.[TextureAspect.Color, 0, 0]
                m.Flush()

            sanitizeIn.["regions"] <- r.[TextureAspect.Color, 0, 0]
            sanitizeIn.Flush()

    let setThreshold (value : float) (alpha : float) =
        if currentConfig.Threshold <> float32 value || currentConfig.Alpha <> float32 alpha then
            let cfg = SegmentationShaders.Config(currentConfig.Offset, float32 value, float32 alpha)
            currentConfig <- cfg
            config.Upload [| cfg |]

    let setInput (input : IBackendTexture) =
        if currentInput <> Some input then
            initIn.["colors"] <- input.[TextureAspect.Color, 0, 0]
            initIn.Flush()
            for m,_ in mergeInputs do 
                m.["colors"] <- input.[TextureAspect.Color, 0, 0]
                m.Flush()
            currentInput <- Some input

    let program =  
        runtime.Compile [
            yield ComputeCommand.Bind parent.InitRegions
            yield ComputeCommand.SetInput initIn
            yield ComputeCommand.Dispatch (V3i(ceilDiv size.X 4, ceilDiv size.Y 4, ceilDiv size.Z 4))
            yield ComputeCommand.Sync infos.Buffer

            for mergeIn, groupCount in mergeInputs do
                yield ComputeCommand.Bind parent.RegionMerge
                yield ComputeCommand.SetInput mergeIn
                yield ComputeCommand.Dispatch groupCount
                
                yield ComputeCommand.Sync infos.Buffer

                yield ComputeCommand.Bind parent.Sanitize
                yield ComputeCommand.SetInput sanitizeIn
                yield ComputeCommand.Dispatch (V3i(ceilDiv size.X 4, ceilDiv size.Y 4, ceilDiv size.Z 4))

                yield ComputeCommand.Bind parent.SanitizeAvg
                yield ComputeCommand.SetInput sanitizeAvgIn
                yield ComputeCommand.Dispatch (ceilDiv (size.X * size.Y * size.Z) 64)

                yield ComputeCommand.Sync infos.Buffer


        ]


    member x.Run(input : IBackendTexture, resultImage : IBackendTexture, threshold : float, alpha : float) =
        lock x (fun () ->
            let inputSize = input.Size
            
            compactor.Clear()

            setInput input
            setRegions resultImage
            setThreshold threshold alpha

            let blocks = V3i(ceilDiv inputSize.X size.X, ceilDiv inputSize.Y size.Y, ceilDiv inputSize.Z size.Z)

            runtime.Run [
                ComputeCommand.TransformLayout(input, TextureLayout.ShaderReadWrite)
                ComputeCommand.TransformLayout(resultImage, TextureLayout.ShaderReadWrite)
            ]

            for bx in 0 .. blocks.X - 1 do
                for by in 0 .. blocks.Y - 1 do
                    for bz in 0 .. blocks.Y - 1 do
                        let o = V3i(bx,by,bz) * size
                        setOffset o
                        program.Run()
                        compactor.AddRange(infos, resultImage, o)
       
            let resultBuffer = compactor.Buffer
            let remainingMerges = 
                createMerges size inputSize  |> List.map (fun (solvedSize, dim) ->
                    let mergeIn = runtime.NewInputBinding parent.RegionMerge
            
                    mergeIn.["Dimension"] <- dim
                    mergeIn.["config"] <- config
                    mergeIn.["SolvedSize"] <- solvedSize
                    mergeIn.["regions"] <- resultImage.[TextureAspect.Color, 0, 0]
                    mergeIn.["infos"] <- resultBuffer.Buffer
                    mergeIn.["colors"] <- input.[TextureAspect.Color, 0, 0]
                    mergeIn.["TDistr"] <- tDistr
                    mergeIn.["BlockSize"] <- inputSize
                    mergeIn.Flush()

                    
                    let groups =
                        if dim = 0 then V3i(ceilDiv size.Y 8, ceilDiv size.Z 8, ceilDiv (size.X - solvedSize) (2 * solvedSize))
                        elif dim = 1 then V3i(ceilDiv size.X 8, ceilDiv size.Z 8, ceilDiv (size.Y - solvedSize) (2 * solvedSize))
                        else V3i(ceilDiv size.X 8, ceilDiv size.Y 8, ceilDiv (size.Z - solvedSize) (2 * solvedSize))


                    mergeIn, groups
                )

            let statBuffer = runtime.CreateBuffer<RegionStats>(resultBuffer.Count)
            toRegionStatsIn.["infos"] <- resultBuffer.Buffer
            toRegionStatsIn.["count"] <- resultBuffer.Count
            toRegionStatsIn.["stats"] <- statBuffer
            toRegionStatsIn.Flush()
            
            calculateSurfaceAreaIn.["regions"] <- resultImage.[TextureAspect.Color, 0, 0]
            calculateSurfaceAreaIn.["stats"] <- statBuffer
            calculateSurfaceAreaIn.Flush()

            match remainingMerges with
                | [] ->
                    runtime.Run [
                        yield ComputeCommand.Bind parent.ToRegionStats
                        yield ComputeCommand.SetInput toRegionStatsIn
                        yield ComputeCommand.Dispatch (ceilDiv resultBuffer.Count 64)

                        yield ComputeCommand.Sync statBuffer.Buffer
                        yield ComputeCommand.Bind parent.CalculateSurfaceArea
                        yield ComputeCommand.SetInput calculateSurfaceAreaIn
                        yield ComputeCommand.Dispatch(V3i(ceilDiv inputSize.X 4, ceilDiv inputSize.Y 4, ceilDiv inputSize.Z 4))
                        
                        yield ComputeCommand.TransformLayout(input, TextureLayout.ShaderRead)
                        yield ComputeCommand.TransformLayout(resultImage, TextureLayout.ShaderRead)
                    ]

                | _ -> 
                    setOffset V3i.Zero

                    let sanitizeIn = 
                        let binding = runtime.NewInputBinding parent.Sanitize
                        binding.["config"] <- config
                        binding.["regions"] <- resultImage.[TextureAspect.Color, 0, 0]
                        binding.["infos"] <- resultBuffer
                        binding.Flush()
                        binding

                    let sanitizeAvgIn = 
                        let binding = runtime.NewInputBinding parent.SanitizeAvg
                        binding.["infos"] <- resultBuffer
                        binding.["RegionCount"] <- resultBuffer.Count
                        binding.Flush()
                        binding

                    runtime.Run [
                        for mergeIn, groupCount in remainingMerges do
                            yield ComputeCommand.Bind parent.RegionMerge
                            yield ComputeCommand.SetInput mergeIn
                            yield ComputeCommand.Dispatch groupCount
                
                            yield ComputeCommand.Sync resultBuffer.Buffer

                            yield ComputeCommand.Bind parent.Sanitize
                            yield ComputeCommand.SetInput sanitizeIn
                            yield ComputeCommand.Dispatch (V3i(ceilDiv inputSize.X 4, ceilDiv inputSize.Y 4,  ceilDiv inputSize.Z 4))

                            yield ComputeCommand.Bind parent.SanitizeAvg
                            yield ComputeCommand.SetInput sanitizeAvgIn
                            yield ComputeCommand.Dispatch (ceilDiv resultBuffer.Count 64)

                            yield ComputeCommand.Sync resultImage
                            yield ComputeCommand.Sync resultBuffer.Buffer
                    
                        yield ComputeCommand.Bind parent.ToRegionStats
                        yield ComputeCommand.SetInput toRegionStatsIn
                        yield ComputeCommand.Dispatch (ceilDiv resultBuffer.Count 64)
                
                        yield ComputeCommand.Sync statBuffer.Buffer
                        yield ComputeCommand.Bind parent.CalculateSurfaceArea
                        yield ComputeCommand.SetInput calculateSurfaceAreaIn
                        yield ComputeCommand.Dispatch(V3i(ceilDiv inputSize.X 4, ceilDiv inputSize.Y 4, ceilDiv inputSize.Z 4))
                
                        yield ComputeCommand.TransformLayout(input, TextureLayout.ShaderRead)
                        yield ComputeCommand.TransformLayout(resultImage, TextureLayout.ShaderRead)
                    ]

                    sanitizeIn.Dispose()
                    sanitizeAvgIn.Dispose()
                    remainingMerges |> List.iter (fun (i,_) -> i.Dispose())

            compactor.Clear()

            //resultBuffer.Value
            statBuffer

        )


    member x.Dispose() =
        runtime.DeleteTexture regions
        infos.Dispose()

        for m,_ in mergeInputs do m.Dispose()
        initIn.Dispose()
        sanitizeIn.Dispose()
        sanitizeAvgIn.Dispose()
        
    interface IDisposable with
        member x.Dispose() = x.Dispose()

