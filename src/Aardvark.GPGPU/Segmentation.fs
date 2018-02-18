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
        val mutable internal StoreI : V4i
        val mutable internal StoreF : V4f

        member x.Count
            with get() = x.StoreI.X
            and set v = x.StoreI.X <- v

        member x.SurfaceCount
            with get() = x.StoreI.Y
            and set v = x.StoreI.Y <- v

        member x.Id
            with get() = x.StoreI.Z
            and set v = x.StoreI.Z <- v

        member x.Average
            with get() = x.StoreF.X
            and set v = x.StoreF.X <- v

        member x.StdDev
            with get() = x.StoreF.Y
            and set v = x.StoreF.Y <- v


        member private x.AsString = x.ToString()

        [<ReflectedDefinition>]
        new(count, surface, id, avg, dev) = { StoreI = V4i(count, surface, id, 0); StoreF = V4f(avg, dev, 0.0f, 0.0f) }

        override x.ToString() =
            sprintf "{ Count = %A; SurfaceCount = %A; Average = %A; StdDev = %A; Id = %A }" x.Count x.SurfaceCount x.Average x.StdDev x.Id

    end



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
            let offset : V2i = uniform?Offset
            let blockSize = regionSum.Size
            let size = colors.Size

            let id = getGlobalId().XY
            if id.X < blockSize.X && id.Y < blockSize.Y then
                let cid = offset + id
                let mutable v = 0.0
                let mutable cnt = 0
                if cid.X < size.X && cid.Y < size.Y then
                    v <- colors.[cid].X
                    cnt <- 1
                    regions.[cid] <- V4i(newColor2d id blockSize, 0, 0, 0)

                regionSum.[id] <- V4i(floatBitsToInt v, 0, 0, 0)
                regionSumSq.[id] <- V4i(floatBitsToInt (v * v), 0, 0, 0)
                regionCount.[id] <- V4i(cnt, 0, 0, 0)
                collapseImg.[id] <- V4i(0,0,0,0)
        }

    [<LocalSize(X = 64)>]
    let regionMerge (identical : Expr<RegionInfo -> float -> RegionInfo -> float -> bool>) (colors : Image2d<Formats.r16>) (regions : IntImage2d<Formats.r32i>) (regionSum : IntImage2d<Formats.r32i>) (regionSumSq : IntImage2d<Formats.r32i>) (regionCount : IntImage2d<Formats.r32i>) (collapseImg : IntImage2d<Formats.r32i>) =
        compute {
            let solvedSize : int = uniform?SolvedSize
            let dim : int = uniform?Dimension
            let colorSize = colors.Size
            let offset : V2i = uniform?Offset
            let blockSize = regionSum.Size

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

                let lr = if clid.X < colorSize.X && clid.Y < colorSize.Y then regions.[clid].X else -1
                let rr = if crid.X < colorSize.X && crid.Y < colorSize.Y then regions.[crid].X else -1

                if lr >= 0 && rr >= 0 then

                    let lRegion = storageCoord2d lr blockSize 
                    let rRegion = storageCoord2d rr blockSize 

                    let lCnt = regionCount.[lRegion].X
                    let rCnt = regionCount.[rRegion].X

                
                    let lSum = intBitsToFloat regionSum.[lRegion].X 
                    let lSumSq = intBitsToFloat regionSumSq.[lRegion].X 
                    let rSum = intBitsToFloat regionSum.[rRegion].X 
                    let rSumSq =intBitsToFloat regionSumSq.[rRegion].X 

                    let lAvg = lSum / float lCnt
                    let rAvg = rSum / float rCnt

                    let lVar = if lCnt < 2 then 0.0 else (lSumSq - float lCnt*lAvg*lAvg) / (float (lCnt - 1))
                    let rVar = if rCnt < 2 then 0.0 else (rSumSq - float rCnt*rAvg*rAvg) / (float (rCnt - 1))

                    let lValue = colors.[clid].X
                    let rValue = colors.[crid].X

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
                                src <- storageCoord2d dst blockSize
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
                                        src <- storageCoord2d r blockSize
                                        
                                else
                                    o <- r


                ()

        }
            
    [<LocalSize(X = 8, Y = 8)>]
    let sanitize  (regions : IntImage2d<Formats.r32i>) (collapseImg : IntImage2d<Formats.r32i>) =
        compute {
            let regionSize = regions.Size
            let offset : V2i = uniform?Offset
            let blockSize = collapseImg.Size
            let id = offset + getGlobalId().XY

            if id.X < regionSize.X && id.Y < regionSize.Y then
                let r = regions.[id].X
                let rc = storageCoord2d r blockSize 

                let mutable last = 0
                let mutable dst = collapseImg.[rc].X
                while dst <> 0 do
                    last <- dst
                    dst <- collapseImg.[storageCoord2d (dst - 1) blockSize].X
                    
                if last <> 0 then
                    regions.[id] <- V4i(last - 1, 0, 0, 0)
        }

    [<LocalSize(X = 8, Y = 8)>]
    let sanitizeAverage (regionSum : IntImage2d<Formats.r32i>) (regionSumSq : IntImage2d<Formats.r32i>) (regionCount : IntImage2d<Formats.r32i>) (collapseImg : IntImage2d<Formats.r32i>) =
        compute {
            let size = regionSum.Size
            let rc = getGlobalId().XY


            if rc.X < size.X && rc.Y < size.Y then
                let srcCnt = regionCount.[rc].X
                if srcCnt > 0 then
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



                        regionCount.[rc] <- V4i.Zero
                        //collapseImg.[rc] <- V4i.Zero

                    ()



        }
           
    [<LocalSize(X = 8, Y = 8)>]
    let calculateSurfaceArea (regions : IntImage2d<Formats.r32i>) (regionSurfaces : IntImage2d<Formats.r32i>) =
        compute {
            let id = getGlobalId().XY
            let regionSize = regions.Size
            let offset : V2i = uniform?Offset
            let blockSize = regionSurfaces.Size
            let cid = offset + id

            if id.X < blockSize.X && id.Y < blockSize.Y && cid.X < regionSize.X && cid.Y < regionSize.Y then
                let r = regions.[id].X
                let rc = storageCoord2d r blockSize 

                let mutable area = 4

                if id.X > 0 && regions.[cid - V2i.IO].X = r then area <- area - 1
                if id.X < blockSize.X - 1 && cid.X < regionSize.X - 1 && regions.[cid + V2i.IO].X = r then area <- area - 1
                if id.Y > 0 && regions.[cid - V2i.OI].X = r then area <- area - 1
                if id.Y < blockSize.Y - 1 && cid.Y < regionSize.Y - 1 && regions.[cid + V2i.OI].X = r then area <- area - 1

                if area > 0 then
                    let res = regionSurfaces.AtomicAdd(rc, 1)
                    ()
        }
       

    [<LocalSize(X = 8, Y = 8)>]
    let allocateCompactRegions (regionCounts : IntImage2d<Formats.r32i>) (remapImage : IntImage2d<Formats.r32i>) (denseIds : int[]) (denseIdCount : int[]) =
        compute {
            let offset : int = uniform?Offset
            let id = getGlobalId().XY
            let size = regionCounts.Size

            if id.X < size.X && id.Y < size.Y then

                let cnt = regionCounts.[id].X
                if cnt > 0 then
                    let r = newColor2d id size
                    
                    // allocate a slot
                    let resId = atomicAdd denseIdCount.[0] 1
                    denseIds.[resId - offset] <- r

                    // store the remapping-id
                    remapImage.[id] <- V4i(resId, 0, 0, 0)


                ()

        }
            
    [<LocalSize(X = 64)>]
    let storeCompactRegions (store : RegionStats[]) (denseIds : int[]) (count : int) (regionSum : IntImage2d<Formats.r32i>) (regionSumSq : IntImage2d<Formats.r32i>) (regionCount : IntImage2d<Formats.r32i>) (regionSurfaces : IntImage2d<Formats.r32i>) =
        compute {
            let offset : int = uniform?Offset
            let blockSize = regionSum.Size
            let id = getGlobalId().X
            if id < count then
                let oCode = denseIds.[id]
                let oRegion = storageCoord2d oCode blockSize

                let cnt = regionCount.[oRegion].X
                let surface = regionSurfaces.[oRegion].X
                let sum = intBitsToFloat regionSum.[oRegion].X
                let sumSq = intBitsToFloat regionSumSq.[oRegion].X

                let avg = sum / float cnt
                let var = if cnt < 2 then 0.0 else (sumSq - float cnt*avg*avg) / (float (cnt - 1))
                let dev = var |> sqrt
                let mutable res = Unchecked.defaultof<RegionStats>
                res.StoreI <- V4i(cnt, surface, offset + id, 0)
                res.StoreF <- V4f(float32 avg, float32 dev, 0.0f, 0.0f)
                store.[offset + id] <- res

        }

    [<LocalSize(X = 8, Y = 8)>]
    let remapRegions (remapImage : IntImage2d<Formats.r32i>) (regions : IntImage2d<Formats.r32i>) =
        compute {
            let id = getGlobalId().XY
            let regionSize = regions.Size
            let blockSize = remapImage.Size
            let offset : V2i = uniform?Offset

            let cid = offset + id

            if cid.X < regionSize.X && cid.Y < regionSize.Y then
                let oCode = regions.[cid].X
                let oRegion = storageCoord2d oCode blockSize
                let nCode = remapImage.[oRegion].X
                regions.[cid] <- V4i(nCode, 0, 0, 0)
        }


    [<LocalSize(X = 8, Y = 8)>]
    let writeToOutput (result : Image2d<Formats.rgba16>) (regionSum : IntImage2d<Formats.r32i>) (regionSumSq : IntImage2d<Formats.r32i>) (regionCount : IntImage2d<Formats.r32i>) (regions : IntImage2d<Formats.r32i>) (regionSurfaces : IntImage2d<Formats.r32i>) =
        compute {
            let id = getGlobalId().XY
            let size = regions.Size

            if id.X < size.X && id.Y < size.Y then
                    
                let rCode = regions.[id].X
                let rCoord = storageCoord2d rCode size

                let sum     = intBitsToFloat regionSum.[rCoord].X
                let sumSq   = intBitsToFloat regionSumSq.[rCoord].X
                let cnt     = regionCount.[rCoord].X
                let surface = regionSurfaces.[rCoord].X


              
                let avg = sum / float cnt
                let var = if cnt < 2 then 0.0 else (sumSq - float cnt*avg*avg) / (float (cnt - 1))
                let dev = var |> sqrt


                let rSurface = float surface /  float cnt


                let hl = unpackUnorm2x16 (uint32 cnt)
                result.[id] <- V4d(avg, rSurface, hl.X, hl.Y)

                    
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

                let lVar = if lCnt < 2 then 0.0 else (lSumSq - float lCnt*lAvg*lAvg) / (float (lCnt - 1))
                let rVar = if rCnt < 2 then 0.0 else (rSumSq - float rCnt*rAvg*rAvg) / (float (rCnt - 1))

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
    let calculateSurfaceArea3d (regions : IntImage3d<Formats.r32i>) (regionSurfaces : IntImage3d<Formats.r32i>) =
        compute {
            let id = getGlobalId()
            let size = regions.Size

            if id.X < size.X && id.Y < size.Y && id.Z < size.Z then
                let r = regions.[id].X
                let rc = storageCoord3d r size 

                let mutable area = 6
                if id.X > 0 && regions.[id - V3i.IOO].X = r then area <- area - 1
                if id.X < size.X - 1 && regions.[id + V3i.IOO].X = r then area <- area - 1
                if id.Y > 0 && regions.[id - V3i.OIO].X = r then area <- area - 1
                if id.Y < size.Y - 1 && regions.[id + V3i.OIO].X = r then area <- area - 1
                if id.Z > 0 && regions.[id - V3i.OOI].X = r then area <- area - 1
                if id.Z < size.Z - 1 && regions.[id + V3i.OOI].X = r then area <- area - 1

                if area > 0 then
                    let r = regionSurfaces.AtomicAdd(rc, 1)
                    ()
                else
                    let r = regionSurfaces.AtomicAdd(rc, -3)
                    ()
        }
            
    [<LocalSize(X = 4, Y = 4, Z = 4)>]
    let writeToOutput3d (result : Image3d<Formats.rgba16>) (regionSum : IntImage3d<Formats.r32i>) (regionSumSq : IntImage3d<Formats.r32i>) (regionCount : IntImage3d<Formats.r32i>) (regions : IntImage3d<Formats.r32i>) (regionSurfaces : IntImage3d<Formats.r32i>) =
        compute {
            let id = getGlobalId()
            let size = regions.Size

            if id.X < size.X && id.Y < size.Y && id.Z < size.Z then
                    
                let rCode = regions.[id].X
                let rCoord = storageCoord3d rCode size

                let sum     = intBitsToFloat regionSum.[rCoord].X
                let sumSq   = intBitsToFloat regionSumSq.[rCoord].X
                let cnt     = regionCount.[rCoord].X
                let surface = regionSurfaces.[rCoord].X

                let avg = sum / float cnt
                let var = if cnt < 2 then 0.0 else (sumSq - float cnt*avg*avg) / (float (cnt - 1))
                let dev = sqrt var

                let rSurface = float surface / float cnt

                let hl = unpackUnorm2x16 (uint32 cnt)
                result.[id] <- V4d(avg, rSurface, hl.X, hl.Y)



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
            <@ fun (lRegion : RegionInfo) (_ : float) (rRegion : RegionInfo) (_ : float) ->
                
                let threshold : float = uniform?Threshold
                let alpha : float = uniform?Alpha

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
    let writeOut = runtime.CreateComputeShader SegmentationShaders.writeToOutput
    let calculateSurface = runtime.CreateComputeShader SegmentationShaders.calculateSurfaceArea
    
    let allocateCompactRegions = runtime.CreateComputeShader SegmentationShaders.allocateCompactRegions
    let remapRegions = runtime.CreateComputeShader SegmentationShaders.remapRegions
    let storeCompactRegions = runtime.CreateComputeShader SegmentationShaders.storeCompactRegions
   
    member x.Runtime = runtime
    member x.InitRegions = initRegions
    member x.RegionMerge = regionMerge
    member x.Sanitize = sanitize
    member x.SanitizeAvg = sanitizeAvg
    member x.CalculateSurface = calculateSurface
    member x.WriteOut = writeOut
    
    member x.AllocateCompactRegions = allocateCompactRegions
    member x.StoreCompactRegions = storeCompactRegions
    member x.RemapRegions = remapRegions

    member x.Dispose() =
        runtime.DeleteComputeShader initRegions
        runtime.DeleteComputeShader regionMerge
        runtime.DeleteComputeShader sanitize
        runtime.DeleteComputeShader sanitizeAvg
        runtime.DeleteComputeShader calculateSurface
        runtime.DeleteComputeShader writeOut
        runtime.DeleteComputeShader allocateCompactRegions
        runtime.DeleteComputeShader storeCompactRegions
        runtime.DeleteComputeShader remapRegions

    interface IDisposable with
        member x.Dispose() = x.Dispose()

type internal RegionMergeKernels3d(runtime : IRuntime, mergeMode : SegmentMergeMode) =
    let initRegions = runtime.CreateComputeShader SegmentationShaders.initRegions3d
    let regionMerge = runtime.CreateComputeShader (SegmentationShaders.regionMerge3d SegmentationShaders.distanceFunction.[mergeMode])
    let sanitize = runtime.CreateComputeShader SegmentationShaders.sanitize3d
    let sanitizeAvg = runtime.CreateComputeShader SegmentationShaders.sanitizeAverage3d
    let writeOut = runtime.CreateComputeShader SegmentationShaders.writeToOutput3d
    let calculateSurface = runtime.CreateComputeShader SegmentationShaders.calculateSurfaceArea3d
    
    member x.Runtime = runtime
    member x.InitRegions = initRegions
    member x.RegionMerge = regionMerge
    member x.Sanitize = sanitize
    member x.SanitizeAvg = sanitizeAvg
    member x.CalculateSurface = calculateSurface
    member x.WriteOut = writeOut
    
    member x.Dispose() =
        runtime.DeleteComputeShader initRegions
        runtime.DeleteComputeShader regionMerge
        runtime.DeleteComputeShader sanitize
        runtime.DeleteComputeShader sanitizeAvg
        runtime.DeleteComputeShader calculateSurface
        runtime.DeleteComputeShader writeOut

    interface IDisposable with
        member x.Dispose() = x.Dispose()

type RegionMerge (runtime : IRuntime, mergeMode : SegmentMergeMode) =

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
        new RegionMergeInstance2d(kernels2d.Value, tDistr, size)
        
    member x.NewInstance(size : V3i) =
        new RegionMergeInstance3d(kernels3d.Value, tDistr, size)
        
    member x.Dispose() =
        if kernels2d.IsValueCreated then kernels2d.Value.Dispose()
        if kernels3d.IsValueCreated then kernels3d.Value.Dispose()

    interface IDisposable with
        member x.Dispose() = x.Dispose()

and RegionMergeInstance3d internal(parent : RegionMergeKernels3d, tDistr : IBackendTexture, size : V3i) =  
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
    let initRegions = parent.InitRegions
    let regionMerge = parent.RegionMerge
    let sanitize = parent.Sanitize
    let sanitizeAvg = parent.SanitizeAvg
    let surface = parent.CalculateSurface
    let writeOut = parent.WriteOut

     
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
            mergeIn.["TDistr"] <- tDistr
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

    
    let surfaceIn =
        let binding = runtime.NewInputBinding surface
        binding.["regions"] <- regions.[TextureAspect.Color, 0, 0]
        binding.["regionSurfaces"] <- collapse.[TextureAspect.Color, 0, 0]
        binding.Flush()
        binding

    let writeOutIn = 
        let binding = runtime.NewInputBinding writeOut
        binding.["regions"] <- regions.[TextureAspect.Color, 0, 0]
        binding.["regionSum"] <- sum.[TextureAspect.Color, 0, 0]
        binding.["regionSumSq"] <- sumSq.[TextureAspect.Color, 0, 0]
        binding.["regionCount"] <- cnt.[TextureAspect.Color, 0, 0]
        binding.["regionSurfaces"] <- collapse.[TextureAspect.Color, 0, 0]
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
                

            yield ComputeCommand.Bind surface
            yield ComputeCommand.SetInput surfaceIn
            yield ComputeCommand.Dispatch (V3i(ceilDiv size.X 4, ceilDiv size.Y 4, ceilDiv size.Z 4))
            yield ComputeCommand.Sync collapse 


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

and RegionMergeInstance2d internal(parent : RegionMergeKernels2d, tDistr : IBackendTexture, size : V2i) =  
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
    let surface = parent.CalculateSurface
        
    let sum         = runtime.CreateTexture(size, TextureFormat.R32i, 1, 1)
    let sumSq       = runtime.CreateTexture(size, TextureFormat.R32i, 1, 1)
    let cnt         = runtime.CreateTexture(size, TextureFormat.R32i, 1, 1)
    let regions     = runtime.CreateTexture(size, TextureFormat.R32i, 1, 1)
    let collapse    = runtime.CreateTexture(size, TextureFormat.R32i, 1, 1)
    let remap       = runtime.CreateTexture(size, TextureFormat.R32i, 1, 1)

    let compactIds      = runtime.CreateBuffer<RegionStats>(size.X * size.Y)
    let compactIdCount  = runtime.CreateBuffer<int>(1)

    let initIn = 
        let binding = runtime.NewInputBinding initRegions
        binding.["regions"] <- regions.[TextureAspect.Color, 0, 0]
        binding.["Offset"] <- V2i.Zero
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
            mergeIn.["Offset"] <- V2i.Zero
            mergeIn.["SolvedSize"] <- solvedSize
            mergeIn.["Threshold"] <- 0.01
            mergeIn.["Alpha"] <- 0.05
            mergeIn.["regions"] <- regions.[TextureAspect.Color, 0, 0]
            mergeIn.["regionSum"] <- sum.[TextureAspect.Color, 0, 0]
            mergeIn.["regionSumSq"] <- sumSq.[TextureAspect.Color, 0, 0]
            mergeIn.["regionCount"] <- cnt.[TextureAspect.Color, 0, 0]
            mergeIn.["collapseImg"] <- collapse.[TextureAspect.Color, 0, 0]
            mergeIn.["TDistr"] <- tDistr
            mergeIn.Flush()


            let groups =
                if dim = 0 then V2i(ceilDiv size.Y 64, ceilDiv (size.X - solvedSize) (2 * solvedSize))
                else V2i(ceilDiv size.X 64, ceilDiv (size.Y - solvedSize) (2 * solvedSize))


            mergeIn, groups
        )

    let sanitizeIn = 
        let binding = runtime.NewInputBinding sanitize
        binding.["Offset"] <- V2i.Zero
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

    let surfaceIn =
        let binding = runtime.NewInputBinding surface
        binding.["Offset"] <- V2i.Zero
        binding.["regions"] <- regions.[TextureAspect.Color, 0, 0]
        binding.["regionSurfaces"] <- collapse.[TextureAspect.Color, 0, 0]
        binding.Flush()
        binding

    let writeOutIn = 
        let binding = runtime.NewInputBinding writeOut
        binding.["regions"] <- regions.[TextureAspect.Color, 0, 0]
        binding.["regionSum"] <- sum.[TextureAspect.Color, 0, 0]
        binding.["regionSumSq"] <- sumSq.[TextureAspect.Color, 0, 0]
        binding.["regionCount"] <- cnt.[TextureAspect.Color, 0, 0]
        binding.["regionSurfaces"] <- collapse.[TextureAspect.Color, 0, 0]
        binding.Flush()
        binding
            

    let allocateCompactRegionsIn =
        let binding = runtime.NewInputBinding parent.AllocateCompactRegions
        binding.["Offset"] <- 0
        binding.["regionCounts"] <- cnt.[TextureAspect.Color, 0, 0]
        binding.["remapImage"] <- remap.[TextureAspect.Color, 0, 0]
        binding.["denseIds"] <- compactIds
        binding.["denseIdCount"] <- compactIdCount
        binding.Flush()
        binding

    let remapRegionsIn =
        let binding = runtime.NewInputBinding parent.RemapRegions
        binding.["Offset"] <- V2i.Zero
        binding.["regions"] <- regions.[TextureAspect.Color, 0, 0]
        binding.["remapImage"] <- remap.[TextureAspect.Color, 0, 0]
        binding.Flush()
        binding

    let storeCompactRegionsIn =
        let binding = runtime.NewInputBinding parent.StoreCompactRegions
        binding.["denseIds"] <- compactIds
        binding.["count"] <- 0
        binding.["Offset"] <- 0
        binding.["regionSum"] <- sum.[TextureAspect.Color, 0, 0]
        binding.["regionSumSq"] <- sumSq.[TextureAspect.Color, 0, 0]
        binding.["regionCount"] <- cnt.[TextureAspect.Color, 0, 0]
        binding.["regionSurfaces"] <- collapse.[TextureAspect.Color, 0, 0]
        binding.Flush()
        binding


    let mutable currentInput = None
    let mutable currentOutput = None
    let mutable currentThreshold = 0.01
    let mutable currentAlpha = 0.05
    let mutable currentOffset = V2i.Zero
    let mutable currentRegions = regions


    let setOffset (v : V2i) =
        if v <> currentOffset then
            currentOffset <- v
            initIn.["Offset"] <- v
            initIn.Flush()

            for (m,_) in mergeInputs do
                m.["Offset"] <- v
                m.Flush()

            sanitizeIn.["Offset"] <- v
            sanitizeIn.Flush()

            surfaceIn.["Offset"] <- v
            surfaceIn.Flush()

            remapRegionsIn.["Offset"] <- v
            remapRegionsIn.Flush()

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

            surfaceIn.["regions"] <- r.[TextureAspect.Color, 0, 0]
            surfaceIn.Flush()

            remapRegionsIn.["regions"] <- r.[TextureAspect.Color, 0, 0]
            remapRegionsIn.Flush()
        

    let setThreshold (value : float) (alpha : float) =
        if currentThreshold <> value || currentAlpha <> alpha then
            for m,_ in mergeInputs do 
                m.["Threshold"] <- value
                m.["Alpha"] <- alpha
                m.Flush()
            currentThreshold <- value
            currentAlpha <- alpha

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

    let solveProgram =
        runtime.Compile [
        
            yield ComputeCommand.TransformLayout(remap, TextureLayout.ShaderReadWrite)
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


        ]
        

    let program = 
        runtime.Compile [
        
            yield ComputeCommand.TransformLayout(remap, TextureLayout.ShaderReadWrite)
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


            yield ComputeCommand.Bind surface
            yield ComputeCommand.SetInput surfaceIn
            yield ComputeCommand.Dispatch (V2i(ceilDiv size.X 8, ceilDiv size.Y 8))
            yield ComputeCommand.Sync collapse 


            
            yield ComputeCommand.Zero(compactIdCount)
            yield ComputeCommand.Sync(compactIdCount.Buffer)
            yield ComputeCommand.Sync cnt

            yield ComputeCommand.Bind parent.AllocateCompactRegions
            yield ComputeCommand.SetInput allocateCompactRegionsIn
            yield ComputeCommand.Dispatch (V2i(ceilDiv size.X 8, ceilDiv size.Y 8))
        ]
        

//    [<LocalSize(X = 8, Y = 8)>]
//    let allocateCompactRegions (regionCounts : IntImage2d<Formats.r32i>) (remapImage : IntImage2d<Formats.r32i>) (denseIds : int[]) (denseIdCount : int[])
//    [<LocalSize(X = 64)>]
//    let storeCompactRegions (store : RegionStats[]) (denseIds : int[]) (count : int) (regionSum : IntImage2d<Formats.r32i>) (regionSumSq : IntImage2d<Formats.r32i>) (regionCount : IntImage2d<Formats.r32i>) (regionSurfaces : IntImage2d<Formats.r32i>)
//    [<LocalSize(X = 8, Y = 8)>]
//    let remapRegions (remapImage : IntImage2d<Formats.r32i>) (regions : IntImage2d<Formats.r32i>) 


    member x.RunBuffer2(input : IBackendTexture, threshold : float, alpha : float) =
        lock x (fun () ->
            let inputSize = input.Size.XY
            setThreshold threshold alpha
                

            let resultImage = runtime.CreateTexture(input.Size.XY, TextureFormat.R32i, 1, 1)
            setIO input input
            setRegions resultImage
            let blocks = V2i(ceilDiv inputSize.X size.X, ceilDiv inputSize.Y size.Y)

            let mutable resultBuffer : Option<IBuffer<RegionStats>> = None
            let mutable currentCount = 0
            compactIdCount.Upload [| 0 |]

            for bx in 0 .. blocks.X - 1 do
                for by in 0 .. blocks.Y - 1 do
                    let o = V2i(bx,by) * size
                    setOffset o

                    
                    storeCompactRegionsIn.Flush()
                    allocateCompactRegionsIn.["Offset"] <- currentCount
                    allocateCompactRegionsIn.Flush()

                    runtime.Run [
                        ComputeCommand.TransformLayout(input, TextureLayout.ShaderReadWrite)
                        ComputeCommand.Execute solveProgram

                        ComputeCommand.Bind parent.AllocateCompactRegions
                        ComputeCommand.SetInput allocateCompactRegionsIn
                        ComputeCommand.Dispatch (V2i(ceilDiv size.X 8, ceilDiv size.Y 8))

                    ]

                    let newCnt = compactIdCount.Download().[0]
                    let n = runtime.CreateBuffer<RegionStats>(newCnt)
                    
                    storeCompactRegionsIn.["Offset"] <- currentCount
                    storeCompactRegionsIn.["count"] <- newCnt - currentCount
                    storeCompactRegionsIn.["store"] <- n
                    storeCompactRegionsIn.Flush()

                    runtime.Run [
                        match resultBuffer with
                            | Some o -> 
                                yield ComputeCommand.Copy(o, n)
                            | None -> 
                                ()

                        yield ComputeCommand.Bind parent.StoreCompactRegions
                        yield ComputeCommand.SetInput storeCompactRegionsIn
                        yield ComputeCommand.Dispatch (ceilDiv (newCnt - currentCount) 64)
                            
                        yield ComputeCommand.Bind parent.RemapRegions
                        yield ComputeCommand.SetInput remapRegionsIn
                        yield ComputeCommand.Dispatch (V2i(ceilDiv size.X 8, ceilDiv size.Y 8))
                    ]
                        

                    match resultBuffer with
                        | Some o -> o.Dispose()
                        | _ -> ()

                    resultBuffer <- Some n
                    currentCount <- newCnt

            resultBuffer.Value, resultImage

        )


    member x.RunBuffer(input : IBackendTexture, threshold : float, alpha : float) =
        lock x (fun () ->
            setIO input input
            setThreshold threshold alpha

            runtime.Run [
                ComputeCommand.TransformLayout(input, TextureLayout.ShaderReadWrite)
                ComputeCommand.Execute program
                ComputeCommand.TransformLayout(input, TextureLayout.ShaderRead)
            ]

            let regionCount = compactIdCount.Download().[0]
            let resultBuffer    = runtime.CreateBuffer<RegionStats>(regionCount)
            let resultImage     = runtime.CreateTexture(input.Size.XY, TextureFormat.R32i, 1, 1)
            storeCompactRegionsIn.["store"] <- resultBuffer
            storeCompactRegionsIn.["count"] <- regionCount
            storeCompactRegionsIn.Flush()

            runtime.Run [
                
                ComputeCommand.Sync(compactIds.Buffer)
                ComputeCommand.Sync(compactIdCount.Buffer)
                ComputeCommand.Sync remap

                ComputeCommand.Bind parent.StoreCompactRegions
                ComputeCommand.SetInput storeCompactRegionsIn
                ComputeCommand.Dispatch (ceilDiv regionCount 64)

                ComputeCommand.Bind parent.RemapRegions
                ComputeCommand.SetInput remapRegionsIn
                ComputeCommand.Dispatch (V2i(ceilDiv size.X 8, ceilDiv size.Y 8))
                
                ComputeCommand.Sync resultBuffer.Buffer

                ComputeCommand.TransformLayout (regions, TextureLayout.TransferRead)
                ComputeCommand.TransformLayout (resultImage, TextureLayout.TransferWrite)
                ComputeCommand.Copy(regions.[TextureAspect.Color, 0, 0], V3i.Zero, resultImage.[TextureAspect.Color, 0, 0], V3i.Zero, V3i(size, 1))
                ComputeCommand.TransformLayout (regions, TextureLayout.ShaderReadWrite)
            ]


            resultImage, resultBuffer
        )

    member x.Run(input : IBackendTexture, output : IBackendTexture, threshold : float, alpha : float) =
        lock x (fun () ->
            setIO input output
            setThreshold threshold alpha

            runtime.Run [
                ComputeCommand.TransformLayout(input, TextureLayout.ShaderReadWrite)
                ComputeCommand.TransformLayout(output, TextureLayout.ShaderReadWrite)
                ComputeCommand.Execute program

                ComputeCommand.Bind writeOut
                ComputeCommand.SetInput writeOutIn
                ComputeCommand.Dispatch (V2i(ceilDiv size.X 8, ceilDiv size.Y 8))

                ComputeCommand.TransformLayout(input, TextureLayout.ShaderRead)
                ComputeCommand.TransformLayout(output, TextureLayout.ShaderRead)
            ]

        )

    member x.Run(input : IBackendTexture, output : IBackendTexture, outputRegions : IBackendTexture, threshold : float, alpha : float) =
        lock x (fun () ->
            setIO input output
            setThreshold threshold alpha

            runtime.Run [
                ComputeCommand.TransformLayout(input, TextureLayout.ShaderReadWrite)
                ComputeCommand.TransformLayout(output, TextureLayout.ShaderReadWrite)
                ComputeCommand.Execute program

                ComputeCommand.Bind writeOut
                ComputeCommand.SetInput writeOutIn
                ComputeCommand.Dispatch (V2i(ceilDiv size.X 8, ceilDiv size.Y 8))

                ComputeCommand.TransformLayout(input, TextureLayout.ShaderRead)
                ComputeCommand.TransformLayout(output, TextureLayout.ShaderRead)
                ComputeCommand.TransformLayout(regions, TextureLayout.TransferRead)
                ComputeCommand.TransformLayout(outputRegions, TextureLayout.TransferWrite)
                ComputeCommand.Copy(regions.[TextureAspect.Color, 0, 0], V3i.Zero, outputRegions.[TextureAspect.Color, 0, 0], V3i.Zero, V3i(size, 1))
                ComputeCommand.TransformLayout(regions, TextureLayout.ShaderReadWrite)
                ComputeCommand.TransformLayout(outputRegions, TextureLayout.ShaderRead)
            ]
        )

    member x.Run(input : IBackendTexture, output : IBackendTexture, threshold : float) =
        x.Run(input, output, threshold, currentAlpha)
        
    member x.Run(input : IBackendTexture, output : IBackendTexture, outputRegions : IBackendTexture, threshold : float) =
        x.Run(input, output, outputRegions, threshold, currentAlpha)

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

