﻿namespace Aardvark.Base

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



module SegmentationShaders =
    open FShade


    [<GLSLIntrinsic("floatBitsToInt({0})")>]
    let floatBitsToInt (v : float) : int = onlyInShaderCode "floatBitsToInt"
        
    [<GLSLIntrinsic("intBitsToFloat({0})")>]
    let intBitsToFloat (v : int) : float = onlyInShaderCode "intBitsToFloat"

    [<GLSLIntrinsic("atomicAdd({0}, {1})")>]
    let atomicAdd (l : int) (v : int) : int = onlyInShaderCode "atomicAdd"

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
    let storageCoord2d (color : int) (size : V2i) =
        V2i(color % size.X, color / size.X)

    [<LocalSize(X = 8, Y = 8)>]
    let initRegions (regions : IntImage2d<Formats.r32i>) (colors : Image2d<Formats.r16>) (regionSum : IntImage2d<Formats.r32i>) (regionSumSq : IntImage2d<Formats.r32i>) (regionCount : IntImage2d<Formats.r32i>) (collapseImg : IntImage2d<Formats.r32i>) =
        compute {
            let size = regions.Size
            let id = getGlobalId().XY
            if id.X < size.X && id.Y < size.Y then
                let v = colors.[id].X
                regions.[id] <- V4i(newColor2d id size, 0, 0, 0)
                regionSum.[id] <- V4i(floatBitsToInt v, 0, 0, 0)
                regionSumSq.[id] <- V4i(floatBitsToInt (v * v), 0, 0, 0)
                regionCount.[id] <- V4i(1, 0, 0, 0)
                collapseImg.[id] <- V4i(0,0,0,0)
        }

    [<LocalSize(X = 64)>]
    let regionMerge (identical : Expr<RegionInfo -> float -> RegionInfo -> float -> bool>) (colors : Image2d<Formats.r16>) (regions : IntImage2d<Formats.r32i>) (regionSum : IntImage2d<Formats.r32i>) (regionSumSq : IntImage2d<Formats.r32i>) (regionCount : IntImage2d<Formats.r32i>) (collapseImg : IntImage2d<Formats.r32i>) =
        compute {
            let solvedSize : int = uniform?SolvedSize
            let dim : int = uniform?Dimension
            let size = colors.Size

            let id = getGlobalId().XY

            let lid =
                if dim = 0 then V2i(id.Y * solvedSize * 2 + (solvedSize - 1), id.X)
                else V2i(id.X, id.Y * solvedSize * 2 + (solvedSize - 1))

            let rid = 
                if dim = 0 then lid + V2i.IO
                else lid + V2i.OI


            if rid.X < size.X && rid.Y < size.Y then
                let lr = regions.[lid].X
                let rr = regions.[rid].X

                let lRegion = storageCoord2d lr size 
                let rRegion = storageCoord2d rr size 

                let lCnt = regionCount.[lRegion].X
                let rCnt = regionCount.[rRegion].X

                
                let lSum = intBitsToFloat regionSum.[lRegion].X 
                let lSumSq = intBitsToFloat regionSumSq.[lRegion].X 
                let rSum = intBitsToFloat regionSum.[rRegion].X 
                let rSumSq =intBitsToFloat regionSumSq.[rRegion].X 

                let lAvg = lSum / float lCnt
                let rAvg = rSum / float rCnt

                let lVar = if lCnt < 2 then 0.0 else (lSumSq + lSum*lSum * (1.0 - 2.0 / float lCnt)) / (float lCnt - 1.0)
                let rVar = if rCnt < 2 then 0.0 else (rSumSq + rSum*rSum * (1.0 - 2.0 / float rCnt)) / (float rCnt - 1.0)

                let lValue = colors.[lid].X
                let rValue = colors.[rid].X

                let lRegionInfo = { average = lAvg; variance = lVar; count = lCnt }
                let rRegionInfo = { average = rAvg; variance = rVar; count = rCnt }
                let identical = (%identical) lRegionInfo lValue rRegionInfo rValue

                if identical then
                        
                    let mutable srcI = rr
                    let mutable src = rRegion
                    let mutable dst = lr

                    let mutable finished = false
                    let mutable o = collapseImg.[src].X
                            
                    while not finished && srcI <> dst do
                        if o = dst + 1 then
                            finished <- true

                        elif o <> 0 && o < dst + 1 then
                            // dst -> o
                            srcI <- dst
                            src <- storageCoord2d dst size
                            dst <- o - 1
                            o <- collapseImg.[src].X

                        else
                            let r = collapseImg.AtomicCompareExchange(src, o, dst + 1)
                            if r = o then
                                if r = 0 then
                                    finished <- true
                                else
                                    let r = r - 1
                                    // r -> dst
                                    srcI <- r
                                    src <- storageCoord2d r size
                                        
                            else
                                o <- r


                ()

        }
            
    [<LocalSize(X = 8, Y = 8)>]
    let sanitize  (regions : IntImage2d<Formats.r32i>) (collapseImg : IntImage2d<Formats.r32i>) =
        compute {
            let id = getGlobalId().XY
            let size = regions.Size

            if id.X < size.X && id.Y < size.Y then
                let r = regions.[id].X
                let rc = storageCoord2d r size 

                let mutable last = 0
                let mutable dst = collapseImg.[rc].X
                while dst <> 0 do
                    last <- dst
                    dst <- collapseImg.[storageCoord2d (dst - 1) size].X
                    
                if last <> 0 then
                    regions.[id] <- V4i(last - 1, 0, 0, 0)
        }

    [<LocalSize(X = 8, Y = 8)>]
    let sanitizeAverage (regionSum : IntImage2d<Formats.r32i>) (regionSumSq : IntImage2d<Formats.r32i>) (regionCount : IntImage2d<Formats.r32i>) (collapseImg : IntImage2d<Formats.r32i>) =
        compute {
            let size = regionSum.Size
            let rc = getGlobalId().XY

            if rc.X < size.X && rc.Y < size.Y then
                let mutable last = 0
                let mutable dst = collapseImg.[rc].X
                while dst <> 0 do
                    last <- dst
                    dst <- collapseImg.[storageCoord2d (dst - 1) size].X 
                        
                if last <> 0 then
                    let dstI = last - 1
                    let dst = storageCoord2d dstI size

                    let srcSum = intBitsToFloat regionSum.[rc].X
                    let srcSumSq = intBitsToFloat regionSumSq.[rc].X
                    let srcCnt = regionCount.[rc].X


                    regionCount.AtomicAdd(dst, srcCnt) |> ignore

                    let mutable o = regionSum.[dst].X
                    let mutable n = intBitsToFloat o + srcSum
                    let mutable r = regionSum.AtomicCompareExchange(dst, o, floatBitsToInt n)
                    while r <> o do
                        o <- r
                        n <- intBitsToFloat o + srcSum
                        r <- regionSum.AtomicCompareExchange(dst, o, floatBitsToInt n)
                            
                    let mutable o = regionSumSq.[dst].X
                    let mutable n = intBitsToFloat o + srcSumSq
                    let mutable r = regionSumSq.AtomicCompareExchange(dst, o, floatBitsToInt n)
                    while r <> o do
                        o <- r
                        n <- intBitsToFloat o + srcSumSq
                        r <- regionSumSq.AtomicCompareExchange(dst, o, floatBitsToInt n)

                collapseImg.[rc] <- V4i(0,0,0,0)

                ()



        }
            
    [<LocalSize(X = 8, Y = 8)>]
    let writeToOutput (result : Image2d<Formats.rgba16>) (regionSum : IntImage2d<Formats.r32i>) (regionSumSq : IntImage2d<Formats.r32i>) (regionCount : IntImage2d<Formats.r32i>) (regions : IntImage2d<Formats.r32i>) =
        compute {
            let id = getGlobalId().XY
            let size = regions.Size

            if id.X < size.X && id.Y < size.Y then
                    
                let rCode = regions.[id].X
                let rCoord = storageCoord2d rCode size

                let sum     = intBitsToFloat regionSum.[rCoord].X
                let sumSq   = intBitsToFloat regionSumSq.[rCoord].X
                let cnt     = regionCount.[rCoord].X
                    

                let avg = sum / float cnt
                let dev = sumSq / float cnt - (avg * avg) |> sqrt
                let area = float cnt / float (size.X * size.Y)

                let hl = unpackUnorm2x16 (uint32 cnt)
                result.[id] <- V4d(avg, dev, hl.X, hl.Y)

                    
                ()


        }


        
        
    [<ReflectedDefinition>]
    let newColor3d (coord : V3i) (size : V3i) =
        coord.Z * (size.X * size.Y) + coord.Y * size.X + coord.X 

    [<ReflectedDefinition>]
    let storageCoord3d (color : int) (size : V3i) =
        let x = color % size.X
        let r = color / size.X
        let y = r % size.Y
        let z = r / size.Y
        V3i(x,y,z)


    [<LocalSize(X = 4, Y = 4, Z = 4)>]
    let initRegions3d (regions : IntImage3d<Formats.r32i>) (colors : Image3d<Formats.r16>) (regionSum : IntImage3d<Formats.r32i>) (regionSumSq : IntImage3d<Formats.r32i>) (regionCount : IntImage3d<Formats.r32i>) (collapseImg : IntImage3d<Formats.r32i>) =
        compute {
            let size = regions.Size
            let id = getGlobalId()
            if id.X < size.X && id.Y < size.Y && id.Z < size.Z then
                let v = colors.[id].X
                regions.[id] <- V4i(newColor3d id size, 0, 0, 0)
                regionSum.[id] <- V4i(floatBitsToInt v, 0, 0, 0)
                regionSumSq.[id] <- V4i(floatBitsToInt (v * v), 0, 0, 0)
                regionCount.[id] <- V4i(1, 0, 0, 0)
                collapseImg.[id] <- V4i(0,0,0,0)
        }

    [<LocalSize(X = 8, Y = 8)>]
    let regionMerge3d (identical : Expr<RegionInfo -> float -> RegionInfo -> float -> bool>) (colors : Image3d<Formats.r16>) (regions : IntImage3d<Formats.r32i>) (regionSum : IntImage3d<Formats.r32i>) (regionSumSq : IntImage3d<Formats.r32i>) (regionCount : IntImage3d<Formats.r32i>) (collapseImg : IntImage3d<Formats.r32i>) =
        compute {
            let solvedSize : int = uniform?SolvedSize
            let dim : int = uniform?Dimension
            let size = colors.Size

            let id = getGlobalId()

            let lid =
                if dim = 0 then V3i(id.Z * solvedSize * 2 + (solvedSize - 1), id.X, id.Y)
                elif dim = 1 then V3i(id.X, id.Z * solvedSize * 2 + (solvedSize - 1), id.Y)
                else V3i(id.X, id.Y, id.Z * solvedSize * 2 + (solvedSize - 1))

            let rid = 
                if dim = 0 then lid + V3i.IOO
                elif dim = 1 then lid + V3i.OIO
                else lid + V3i.OOI


            if rid.X < size.X && rid.Y < size.Y && rid.Z < size.Z then
                let lr = regions.[lid].X
                let rr = regions.[rid].X

                let lRegion = storageCoord3d lr size 
                let rRegion = storageCoord3d rr size 

                let lCnt = regionCount.[lRegion].X
                let rCnt = regionCount.[rRegion].X

                
                let lSum = intBitsToFloat regionSum.[lRegion].X 
                let lSumSq = intBitsToFloat regionSumSq.[lRegion].X 
                let rSum = intBitsToFloat regionSum.[rRegion].X 
                let rSumSq =intBitsToFloat regionSumSq.[rRegion].X 

                let lAvg = lSum / float lCnt
                let rAvg = rSum / float rCnt

                let lVar = if lCnt < 2 then 0.0 else (lSumSq + lSum*lSum * (1.0 - 2.0 / float lCnt)) / (float lCnt - 1.0)
                let rVar = if rCnt < 2 then 0.0 else (rSumSq + rSum*rSum * (1.0 - 2.0 / float rCnt)) / (float rCnt - 1.0)

                let lValue = colors.[lid].X
                let rValue = colors.[rid].X

                let lRegionInfo = { average = lAvg; variance = lVar; count = lCnt }
                let rRegionInfo = { average = rAvg; variance = rVar; count = rCnt }
                let identical = (%identical) lRegionInfo lValue rRegionInfo rValue

                if identical then
                        
                    let mutable srcI = rr
                    let mutable src = rRegion
                    let mutable dst = lr

                    let mutable finished = false
                    let mutable o = collapseImg.[src].X
                            
                    while not finished && srcI <> dst do
                        if o = dst + 1 then
                            finished <- true

                        elif o <> 0 && o < dst + 1 then
                            // dst -> o
                            srcI <- dst
                            src <- storageCoord3d dst size
                            dst <- o - 1
                            o <- collapseImg.[src].X

                        else
                            let r = collapseImg.AtomicCompareExchange(src, o, dst + 1)
                            if r = o then
                                if r = 0 then
                                    finished <- true
                                else
                                    let r = r - 1
                                    // r -> dst
                                    srcI <- r
                                    src <- storageCoord3d r size
                                        
                            else
                                o <- r


                ()

        }
            
    [<LocalSize(X = 4, Y = 4, Z = 4)>]
    let sanitize3d  (regions : IntImage3d<Formats.r32i>) (collapseImg : IntImage3d<Formats.r32i>) =
        compute {
            let id = getGlobalId()
            let size = regions.Size

            if id.X < size.X && id.Y < size.Y && id.Z < size.Z then
                let r = regions.[id].X
                let rc = storageCoord3d r size 

                let mutable last = 0
                let mutable dst = collapseImg.[rc].X
                while dst <> 0 do
                    last <- dst
                    dst <- collapseImg.[storageCoord3d (dst - 1) size].X
                    
                if last <> 0 then
                    regions.[id] <- V4i(last - 1, 0, 0, 0)
        }

    [<LocalSize(X = 4, Y = 4, Z = 4)>]
    let sanitizeAverage3d (regionSum : IntImage3d<Formats.r32i>) (regionSumSq : IntImage3d<Formats.r32i>) (regionCount : IntImage3d<Formats.r32i>) (collapseImg : IntImage3d<Formats.r32i>) =
        compute {
            let size = regionSum.Size
            let rc = getGlobalId()

            if rc.X < size.X && rc.Y < size.Y && rc.Z < size.Z then
                let mutable last = 0
                let mutable dst = collapseImg.[rc].X
                while dst <> 0 do
                    last <- dst
                    dst <- collapseImg.[storageCoord3d (dst - 1) size].X 
                        
                if last <> 0 then
                    let dstI = last - 1
                    let dst = storageCoord3d dstI size

                    let srcSum = intBitsToFloat regionSum.[rc].X
                    let srcSumSq = intBitsToFloat regionSumSq.[rc].X
                    let srcCnt = regionCount.[rc].X


                    regionCount.AtomicAdd(dst, srcCnt) |> ignore

                    let mutable o = regionSum.[dst].X
                    let mutable n = intBitsToFloat o + srcSum
                    let mutable r = regionSum.AtomicCompareExchange(dst, o, floatBitsToInt n)
                    while r <> o do
                        o <- r
                        n <- intBitsToFloat o + srcSum
                        r <- regionSum.AtomicCompareExchange(dst, o, floatBitsToInt n)
                            
                    let mutable o = regionSumSq.[dst].X
                    let mutable n = intBitsToFloat o + srcSumSq
                    let mutable r = regionSumSq.AtomicCompareExchange(dst, o, floatBitsToInt n)
                    while r <> o do
                        o <- r
                        n <- intBitsToFloat o + srcSumSq
                        r <- regionSumSq.AtomicCompareExchange(dst, o, floatBitsToInt n)

                collapseImg.[rc] <- V4i(0,0,0,0)

                ()



        }
            
    [<LocalSize(X = 4, Y = 4, Z = 4)>]
    let writeToOutput3d (result : Image3d<Formats.rgba16>) (regionSum : IntImage3d<Formats.r32i>) (regionSumSq : IntImage3d<Formats.r32i>) (regionCount : IntImage3d<Formats.r32i>) (regions : IntImage3d<Formats.r32i>) =
        compute {
            let id = getGlobalId()
            let size = regions.Size

            if id.X < size.X && id.Y < size.Y && id.Z < size.Z then
                    
                let rCode = regions.[id].X
                let rCoord = storageCoord3d rCode size

                let sum     = intBitsToFloat regionSum.[rCoord].X
                let sumSq   = intBitsToFloat regionSumSq.[rCoord].X
                let cnt     = regionCount.[rCoord].X
                    

                let avg = sum / float cnt
                let dev = sumSq / float cnt - (avg * avg) |> sqrt
                let area = float cnt / float (size.X * size.Y)

                let hl = unpackUnorm2x16 (uint32 cnt)
                result.[id] <- V4d(avg, dev, hl.X, hl.Y)

                    
                ()


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
            <@ fun (lRegion : RegionInfo) (lValue : float) (rRegion : RegionInfo) (rValue : float) ->
                let threshold : float = uniform?Threshold
                min (abs (lValue - lRegion.average)) (abs (rValue - rRegion.average)) < threshold
            @>

            SegmentMergeMode.AvgToAvg,
            <@ fun (lRegion : RegionInfo) (lValue : float) (rRegion : RegionInfo) (rValue : float) ->
                let threshold : float = uniform?Threshold
                abs (rRegion.average - lRegion.average) < threshold
            @>
            
            SegmentMergeMode.TTest,
            <@ fun (lRegion : RegionInfo) (lValue : float) (rRegion : RegionInfo) (rValue : float) ->
                
                let alpha : float = uniform?Threshold

                if lRegion.count < 2 && rRegion.count < 2 then
                    let d = abs (lRegion.average - rRegion.average)
                    d < alpha

                elif lRegion.count < 2 then

                    let test = abs (rRegion.average - lRegion.average) / sqrt (lRegion.variance)



                    let test = abs (rRegion.average - lRegion.average) / sqrt (rRegion.variance / float rRegion.count)
                    let ny = float (rRegion.count - 1)
                    let t = tInv alpha ny
                    test >= t


                elif rRegion.count < 2 then
                    let test = abs (lRegion.average - rRegion.average) / sqrt (lRegion.variance / float lRegion.count)
                    let ny = float (lRegion.count - 1)
                    let t = tInv alpha ny
                    test >= t


                else
                    let v1 = lRegion.variance
                    let v2 = rRegion.variance
                    let N1 = float lRegion.count
                    let N2 = float rRegion.count

                    let a = (v1 / N1 + v2 / N2)
                    let test = (lRegion.average - rRegion.average) / a
                    let ny = (a * a) / (v1*v1 / (N1*N1*(N1 - 1.0)) + (v2*v2 / (N2*N2*(N2-1.0))))

                    let t = tInv alpha ny
                    test >= t
            @>
        ]

type RegionMerge(runtime : IRuntime, mergeMode : SegmentMergeMode) =
        

    let tDistr = 
        let img = PixImage<float32>(Col.Format.Gray, Volume<float32>(TDistribution.Data, V3i(500, 128, 1)))
        let tex = runtime.CreateTexture(img.Size, TextureFormat.R32f, 1, 1)
        runtime.Upload(tex, 0, 0, img)
        tex

    let initRegions = runtime.CreateComputeShader SegmentationShaders.initRegions
    let regionMerge = runtime.CreateComputeShader (SegmentationShaders.regionMerge SegmentationShaders.distanceFunction.[mergeMode])
    let sanitize = runtime.CreateComputeShader SegmentationShaders.sanitize
    let sanitizeAvg = runtime.CreateComputeShader SegmentationShaders.sanitizeAverage
    let writeOut = runtime.CreateComputeShader SegmentationShaders.writeToOutput

    let initRegions3d = runtime.CreateComputeShader SegmentationShaders.initRegions3d
    let regionMerge3d = runtime.CreateComputeShader (SegmentationShaders.regionMerge3d SegmentationShaders.distanceFunction.[mergeMode])
    let sanitize3d = runtime.CreateComputeShader SegmentationShaders.sanitize3d
    let sanitizeAvg3d = runtime.CreateComputeShader SegmentationShaders.sanitizeAverage3d
    let writeOut3d = runtime.CreateComputeShader SegmentationShaders.writeToOutput3d
        

    member x.TDistrTexture = tDistr
    member x.Runtime = runtime
    member internal x.InitRegions = initRegions
    member internal x.RegionMerge = regionMerge
    member internal x.Sanitize = sanitize
    member internal x.SanitizeAvg = sanitizeAvg
    member internal x.WriteOut = writeOut

    member internal x.InitRegions3d = initRegions3d
    member internal x.RegionMerge3d = regionMerge3d
    member internal x.Sanitize3d = sanitize3d
    member internal x.SanitizeAvg3d = sanitizeAvg3d
    member internal x.WriteOut3d = writeOut3d

    member x.NewInstance(size : V2i) =
        new RegionMergeInstance2d(x, size)
        
    member x.NewInstance(size : V3i) =
        new RegionMergeInstance3d(x, size)
        
    member x.Dispose() =
        runtime.DeleteComputeShader initRegions
        runtime.DeleteComputeShader regionMerge
        runtime.DeleteComputeShader sanitize
        runtime.DeleteComputeShader sanitizeAvg
        runtime.DeleteComputeShader writeOut

    interface IDisposable with
        member x.Dispose() = x.Dispose()

and RegionMergeInstance3d(parent : RegionMerge, size : V3i) =  
    static let buildMerges (size : V3i) =
        let rec merges (s : V3i) (size : V3i) =
            match s.X < size.X, s.Y < size.Y, s.Z < size.Z with
                | true, true, true ->
                    (s.X, 0) :: (s.Y, 1) :: (s.Z, 2) :: merges (2 * s) size

                | true, true, false ->
                    (s.X, 0) :: (s.Y, 1) :: merges (V3i(s.X * 2, s.Y * 2, s.Z)) size
                    
                | true, false, true ->
                    (s.X, 0) :: (s.Z, 2) :: merges (V3i(s.X * 2, s.Y, s.Z * 2)) size
                    
                | false, true, true ->
                    (s.Y, 1) :: (s.Z, 2) :: merges (V3i(s.X, s.Y * 2, s.Z * 2)) size

                | true, false, false ->
                    (s.X, 0) :: merges (V3i(s.X * 2, s.Y, s.Z)) size
                    
                | false, true, false ->
                    (s.Y, 1) :: merges (V3i(s.X, s.Y * 2, s.Z)) size

                | false, false, true ->
                    (s.Z, 2) :: merges (V3i(s.X, s.Y, s.Z * 2)) size

                | false, false, false ->
                    []


        merges V3i.III size

    let runtime = parent.Runtime
    let initRegions = parent.InitRegions3d
    let regionMerge = parent.RegionMerge3d
    let sanitize = parent.Sanitize3d
    let sanitizeAvg = parent.SanitizeAvg3d
    let writeOut = parent.WriteOut3d

     
    let sum         = runtime.CreateTexture(size, TextureDimension.Texture3D, TextureFormat.R32i, 1, 1, 1)
    let sumSq       = runtime.CreateTexture(size, TextureDimension.Texture3D, TextureFormat.R32i, 1, 1, 1)
    let cnt         = runtime.CreateTexture(size, TextureDimension.Texture3D, TextureFormat.R32i, 1, 1, 1)
    let regions     = runtime.CreateTexture(size, TextureDimension.Texture3D, TextureFormat.R32i, 1, 1, 1)
    let collapse    = runtime.CreateTexture(size, TextureDimension.Texture3D, TextureFormat.R32i, 1, 1, 1)
    
    let initIn = 
        let binding = runtime.NewInputBinding initRegions
        binding.["regions"] <- regions.[TextureAspect.Color, 0, 0]
        binding.["regionSum"] <- sum.[TextureAspect.Color, 0, 0]
        binding.["regionSumSq"] <- sumSq.[TextureAspect.Color, 0, 0]
        binding.["regionCount"] <- cnt.[TextureAspect.Color, 0, 0]
        binding.["collapseImg"] <- collapse.[TextureAspect.Color, 0, 0]
        binding.Flush()
        binding

    let mergeInputs =
        buildMerges size |> List.map (fun (solvedSize, dim) ->
            let mergeIn = runtime.NewInputBinding regionMerge
            
            mergeIn.["Dimension"] <- dim
            mergeIn.["SolvedSize"] <- solvedSize
            mergeIn.["Threshold"] <- 0.01
            mergeIn.["regions"] <- regions.[TextureAspect.Color, 0, 0]
            mergeIn.["regionSum"] <- sum.[TextureAspect.Color, 0, 0]
            mergeIn.["regionSumSq"] <- sumSq.[TextureAspect.Color, 0, 0]
            mergeIn.["regionCount"] <- cnt.[TextureAspect.Color, 0, 0]
            mergeIn.["collapseImg"] <- collapse.[TextureAspect.Color, 0, 0]
            mergeIn.["TDistr"] <- parent.TDistrTexture
            mergeIn.Flush()


            let groups =
                if dim = 0 then V3i(ceilDiv size.Y 8, ceilDiv size.Z 8, ceilDiv (size.X - solvedSize) (2 * solvedSize))
                elif dim = 1 then V3i(ceilDiv size.X 8, ceilDiv size.Z 8, ceilDiv (size.Y - solvedSize) (2 * solvedSize))
                else V3i(ceilDiv size.X 8, ceilDiv size.Y 8, ceilDiv (size.Z - solvedSize) (2 * solvedSize))


            mergeIn, groups
        )

    let sanitizeIn = 
        let binding = runtime.NewInputBinding sanitize
        binding.["regions"] <- regions.[TextureAspect.Color, 0, 0]
        binding.["collapseImg"] <- collapse.[TextureAspect.Color, 0, 0]
        binding.Flush()
        binding

    let sanitizeAvgIn = 
        let binding = runtime.NewInputBinding sanitizeAvg
        binding.["regionSum"] <- sum.[TextureAspect.Color, 0, 0]
        binding.["regionSumSq"] <- sumSq.[TextureAspect.Color, 0, 0]
        binding.["regionCount"] <- cnt.[TextureAspect.Color, 0, 0]
        binding.["collapseImg"] <- collapse.[TextureAspect.Color, 0, 0]
        binding.Flush()
        binding

    let writeOutIn = 
        let binding = runtime.NewInputBinding writeOut
        binding.["regions"] <- regions.[TextureAspect.Color, 0, 0]
        binding.["regionSum"] <- sum.[TextureAspect.Color, 0, 0]
        binding.["regionSumSq"] <- sumSq.[TextureAspect.Color, 0, 0]
        binding.["regionCount"] <- cnt.[TextureAspect.Color, 0, 0]
        binding.Flush()
        binding
            
    let mutable currentInput = None
    let mutable currentOutput = None
    let mutable currentThreshold = 0.01

    let setThreshold (value : float) =
        if currentThreshold <> value then
            for m,_ in mergeInputs do 
                m.["Threshold"] <- value
                m.Flush()
            currentThreshold <- value

    let setIO (input : IBackendTexture) (output : IBackendTexture)=
        if currentInput <> Some input then
            initIn.["colors"] <- input.[TextureAspect.Color, 0, 0]
            initIn.Flush()
            for m,_ in mergeInputs do 
                m.["colors"] <- input.[TextureAspect.Color, 0, 0]
                m.Flush()
            currentInput <- Some input

        if currentOutput <> Some output then
            writeOutIn.["result"] <- output.[TextureAspect.Color, 0, 0]
            writeOutIn.Flush()
            currentOutput <- Some output

    let program = 
        runtime.Compile [

            yield ComputeCommand.TransformLayout(regions, TextureLayout.ShaderReadWrite)
            yield ComputeCommand.TransformLayout(sum, TextureLayout.ShaderReadWrite)
            yield ComputeCommand.TransformLayout(sumSq, TextureLayout.ShaderReadWrite)
            yield ComputeCommand.TransformLayout(cnt, TextureLayout.ShaderReadWrite)
            yield ComputeCommand.TransformLayout(collapse, TextureLayout.ShaderReadWrite)

            yield ComputeCommand.Bind initRegions
            yield ComputeCommand.SetInput initIn
            yield ComputeCommand.Dispatch (V3i(ceilDiv size.X 4, ceilDiv size.Y 4, ceilDiv size.Z 4))
            yield ComputeCommand.Sync regions
            yield ComputeCommand.Sync sum
            yield ComputeCommand.Sync cnt
            // TODO: init sum and cnt


            for mergeIn, groupCount in mergeInputs do
                yield ComputeCommand.Bind regionMerge
                yield ComputeCommand.SetInput mergeIn
                yield ComputeCommand.Dispatch groupCount

                yield ComputeCommand.Sync collapse

                yield ComputeCommand.Bind sanitize
                yield ComputeCommand.SetInput sanitizeIn
                yield ComputeCommand.Dispatch (V3i(ceilDiv size.X 4, ceilDiv size.Y 4, ceilDiv size.Z 4))

                yield ComputeCommand.Bind sanitizeAvg
                yield ComputeCommand.SetInput sanitizeAvgIn
                yield ComputeCommand.Dispatch (V3i(ceilDiv size.X 4, ceilDiv size.Y 4, ceilDiv size.Z 4))

                yield ComputeCommand.Sync regions
                yield ComputeCommand.Sync sum
                yield ComputeCommand.Sync sumSq
                yield ComputeCommand.Sync cnt


            yield ComputeCommand.Bind writeOut
            yield ComputeCommand.SetInput writeOutIn
            yield ComputeCommand.Dispatch (V3i(ceilDiv size.X 4, ceilDiv size.Y 4, ceilDiv size.Z 4))
                
        ]

    member x.Run(input : IBackendTexture, output : IBackendTexture, threshold : float) =
        lock x (fun () ->
            setIO input output
            setThreshold threshold

            runtime.Run [
                ComputeCommand.TransformLayout(input, TextureLayout.ShaderReadWrite)
                ComputeCommand.TransformLayout(output, TextureLayout.ShaderReadWrite)
                ComputeCommand.Execute program
                ComputeCommand.TransformLayout(input, TextureLayout.ShaderRead)
                ComputeCommand.TransformLayout(output, TextureLayout.ShaderRead)
            ]

        )

    member x.Run(input : IBackendTexture, output : IBackendTexture, outputRegions : IBackendTexture, threshold : float) =
        lock x (fun () ->
            setIO input output
            setThreshold threshold

            runtime.Run [
                ComputeCommand.TransformLayout(input, TextureLayout.ShaderReadWrite)
                ComputeCommand.TransformLayout(output, TextureLayout.ShaderReadWrite)
                ComputeCommand.Execute program
                ComputeCommand.TransformLayout(input, TextureLayout.ShaderRead)
                ComputeCommand.TransformLayout(output, TextureLayout.ShaderRead)
                ComputeCommand.TransformLayout(regions, TextureLayout.TransferRead)
                ComputeCommand.TransformLayout(outputRegions, TextureLayout.TransferWrite)
                ComputeCommand.Copy(regions.[TextureAspect.Color, 0, 0], V3i.Zero, outputRegions.[TextureAspect.Color, 0, 0], V3i.Zero, size)
                ComputeCommand.TransformLayout(regions, TextureLayout.ShaderReadWrite)
                ComputeCommand.TransformLayout(outputRegions, TextureLayout.ShaderRead)
            ]

        )

    member x.Dispose() =
        runtime.DeleteTexture sum
        runtime.DeleteTexture sumSq
        runtime.DeleteTexture cnt
        runtime.DeleteTexture regions
        runtime.DeleteTexture collapse

        for m,_ in mergeInputs do m.Dispose()
        initIn.Dispose()
        sanitizeIn.Dispose()
        sanitizeAvgIn.Dispose()
        writeOutIn.Dispose()

    interface IDisposable with
        member x.Dispose() = x.Dispose()

and RegionMergeInstance2d(parent : RegionMerge, size : V2i) =  
    static let buildMerges (size : V2i) =
        let rec merges (s : V2i) (size : V2i) =
            if s.X < size.X && s.Y < size.Y then
                (s.X, 0) :: (s.Y, 1) :: merges (2 * s) size

            elif s.X < size.X then
                (s.X, 0) :: merges (V2i(s.X * 2, s.Y)) size 

            elif s.Y < size.Y then
                (s.Y, 1) :: merges (V2i(s.X, 2 * s.Y)) size
            else
                []

        merges V2i.II size

    let runtime = parent.Runtime
    let initRegions = parent.InitRegions
    let regionMerge = parent.RegionMerge
    let sanitize = parent.Sanitize
    let sanitizeAvg = parent.SanitizeAvg
    let writeOut = parent.WriteOut
        
    let sum         = runtime.CreateTexture(size, TextureFormat.R32i, 1, 1)
    let sumSq       = runtime.CreateTexture(size, TextureFormat.R32i, 1, 1)
    let cnt         = runtime.CreateTexture(size, TextureFormat.R32i, 1, 1)
    let regions     = runtime.CreateTexture(size, TextureFormat.R32i, 1, 1)
    let collapse    = runtime.CreateTexture(size, TextureFormat.R32i, 1, 1)

    let initIn = 
        let binding = runtime.NewInputBinding initRegions
        binding.["regions"] <- regions.[TextureAspect.Color, 0, 0]
        //binding.["colors"] <- img.[TextureAspect.Color, 0, 0]
        binding.["regionSum"] <- sum.[TextureAspect.Color, 0, 0]
        binding.["regionSumSq"] <- sumSq.[TextureAspect.Color, 0, 0]
        binding.["regionCount"] <- cnt.[TextureAspect.Color, 0, 0]
        binding.["collapseImg"] <- collapse.[TextureAspect.Color, 0, 0]
        binding.Flush()
        binding

    let mergeInputs =
        buildMerges size |> List.map (fun (solvedSize, dim) ->
            let mergeIn = runtime.NewInputBinding regionMerge
            
            mergeIn.["Dimension"] <- dim
            mergeIn.["SolvedSize"] <- solvedSize
            mergeIn.["Threshold"] <- 0.01
            mergeIn.["regions"] <- regions.[TextureAspect.Color, 0, 0]
            mergeIn.["regionSum"] <- sum.[TextureAspect.Color, 0, 0]
            mergeIn.["regionSumSq"] <- sumSq.[TextureAspect.Color, 0, 0]
            mergeIn.["regionCount"] <- cnt.[TextureAspect.Color, 0, 0]
            mergeIn.["collapseImg"] <- collapse.[TextureAspect.Color, 0, 0]
            mergeIn.["TDistr"] <- parent.TDistrTexture
            mergeIn.Flush()


            let groups =
                if dim = 0 then V2i(ceilDiv size.Y 64, ceilDiv (size.X - solvedSize) (2 * solvedSize))
                else V2i(ceilDiv size.X 64, ceilDiv (size.Y - solvedSize) (2 * solvedSize))


            mergeIn, groups
        )

    let sanitizeIn = 
        let binding = runtime.NewInputBinding sanitize
        binding.["regions"] <- regions.[TextureAspect.Color, 0, 0]
        binding.["collapseImg"] <- collapse.[TextureAspect.Color, 0, 0]
        binding.Flush()
        binding

    let sanitizeAvgIn = 
        let binding = runtime.NewInputBinding sanitizeAvg
        binding.["regionSum"] <- sum.[TextureAspect.Color, 0, 0]
        binding.["regionSumSq"] <- sumSq.[TextureAspect.Color, 0, 0]
        binding.["regionCount"] <- cnt.[TextureAspect.Color, 0, 0]
        binding.["collapseImg"] <- collapse.[TextureAspect.Color, 0, 0]
        binding.Flush()
        binding

    let writeOutIn = 
        let binding = runtime.NewInputBinding writeOut
        binding.["regions"] <- regions.[TextureAspect.Color, 0, 0]
        binding.["regionSum"] <- sum.[TextureAspect.Color, 0, 0]
        binding.["regionSumSq"] <- sumSq.[TextureAspect.Color, 0, 0]
        binding.["regionCount"] <- cnt.[TextureAspect.Color, 0, 0]
        binding.Flush()
        binding
            
    let mutable currentInput = None
    let mutable currentOutput = None
    let mutable currentThreshold = 0.01

    let setThreshold (value : float) =
        if currentThreshold <> value then
            for m,_ in mergeInputs do 
                m.["Threshold"] <- value
                m.Flush()
            currentThreshold <- value

    let setIO (input : IBackendTexture) (output : IBackendTexture)=
        if currentInput <> Some input then
            initIn.["colors"] <- input.[TextureAspect.Color, 0, 0]
            initIn.Flush()
            for m,_ in mergeInputs do 
                m.["colors"] <- input.[TextureAspect.Color, 0, 0]
                m.Flush()
            currentInput <- Some input

        if currentOutput <> Some output then
            writeOutIn.["result"] <- output.[TextureAspect.Color, 0, 0]
            writeOutIn.Flush()
            currentOutput <- Some output


    let program = 
        runtime.Compile [

            yield ComputeCommand.TransformLayout(regions, TextureLayout.ShaderReadWrite)
            yield ComputeCommand.TransformLayout(sum, TextureLayout.ShaderReadWrite)
            yield ComputeCommand.TransformLayout(sumSq, TextureLayout.ShaderReadWrite)
            yield ComputeCommand.TransformLayout(cnt, TextureLayout.ShaderReadWrite)
            yield ComputeCommand.TransformLayout(collapse, TextureLayout.ShaderReadWrite)

            yield ComputeCommand.Bind initRegions
            yield ComputeCommand.SetInput initIn
            yield ComputeCommand.Dispatch (V2i(ceilDiv size.X 8, ceilDiv size.Y 8))
            yield ComputeCommand.Sync regions
            yield ComputeCommand.Sync sum
            yield ComputeCommand.Sync cnt
            // TODO: init sum and cnt


            for mergeIn, groupCount in mergeInputs do
                yield ComputeCommand.Bind regionMerge
                yield ComputeCommand.SetInput mergeIn
                yield ComputeCommand.Dispatch groupCount

                yield ComputeCommand.Sync collapse

                yield ComputeCommand.Bind sanitize
                yield ComputeCommand.SetInput sanitizeIn
                yield ComputeCommand.Dispatch (V2i(ceilDiv size.X 8, ceilDiv size.Y 8))

                yield ComputeCommand.Bind sanitizeAvg
                yield ComputeCommand.SetInput sanitizeAvgIn
                yield ComputeCommand.Dispatch (V2i(ceilDiv size.X 8, ceilDiv size.Y 8))

                yield ComputeCommand.Sync regions
                yield ComputeCommand.Sync sum
                yield ComputeCommand.Sync sumSq
                yield ComputeCommand.Sync cnt


            yield ComputeCommand.Bind writeOut
            yield ComputeCommand.SetInput writeOutIn
            yield ComputeCommand.Dispatch (V2i(ceilDiv size.X 8, ceilDiv size.Y 8))
                
        ]

    member x.Run(input : IBackendTexture, output : IBackendTexture, threshold : float) =
        lock x (fun () ->
            setIO input output
            setThreshold threshold

            runtime.Run [
                ComputeCommand.TransformLayout(input, TextureLayout.ShaderReadWrite)
                ComputeCommand.TransformLayout(output, TextureLayout.ShaderReadWrite)
                ComputeCommand.Execute program
                ComputeCommand.TransformLayout(input, TextureLayout.ShaderRead)
                ComputeCommand.TransformLayout(output, TextureLayout.ShaderRead)
            ]

        )

    member x.Run(input : IBackendTexture, output : IBackendTexture, outputRegions : IBackendTexture, threshold : float) =
        lock x (fun () ->
            setIO input output
            setThreshold threshold

            runtime.Run [
                ComputeCommand.TransformLayout(input, TextureLayout.ShaderReadWrite)
                ComputeCommand.TransformLayout(output, TextureLayout.ShaderReadWrite)
                ComputeCommand.Execute program
                ComputeCommand.TransformLayout(input, TextureLayout.ShaderRead)
                ComputeCommand.TransformLayout(output, TextureLayout.ShaderRead)
                ComputeCommand.TransformLayout(regions, TextureLayout.TransferRead)
                ComputeCommand.TransformLayout(outputRegions, TextureLayout.TransferWrite)
                ComputeCommand.Copy(regions.[TextureAspect.Color, 0, 0], V3i.Zero, outputRegions.[TextureAspect.Color, 0, 0], V3i.Zero, V3i(size, 1))
                ComputeCommand.TransformLayout(regions, TextureLayout.ShaderReadWrite)
                ComputeCommand.TransformLayout(outputRegions, TextureLayout.ShaderRead)
            ]

        )

    member x.Dispose() =
        runtime.DeleteTexture sum
        runtime.DeleteTexture sumSq
        runtime.DeleteTexture cnt
        runtime.DeleteTexture regions
        runtime.DeleteTexture collapse

        for m,_ in mergeInputs do m.Dispose()
        initIn.Dispose()
        sanitizeIn.Dispose()
        sanitizeAvgIn.Dispose()
        writeOutIn.Dispose()

    interface IDisposable with
        member x.Dispose() = x.Dispose()
