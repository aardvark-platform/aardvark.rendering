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
        val mutable public StoreI : V4i
        val mutable public StoreF : V4f

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

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix); ReflectedDefinition>]
module RegionStats =
    open FShade
    [<Inline>]
    let inline count (s : RegionStats) = s.StoreI.X
    [<Inline>]
    let inline surfaceCount (s : RegionStats) = s.StoreI.Y
    [<Inline>]
    let inline id (s : RegionStats) = s.StoreI.Z
    [<Inline>]
    let inline average (s : RegionStats) = s.StoreF.X
    [<Inline>]
    let inline stddev (s : RegionStats) = s.StoreF.Y

module SegmentationShaders =
    open FShade

    [<StructLayout(LayoutKind.Explicit, Size = 24)>]
    type Config =
        struct
            [<FieldOffset(0)>]
            val mutable public Offset : V2i
           
            [<FieldOffset(8)>]
            val mutable public Threshold : float32

            [<FieldOffset(12)>]
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
    let storageCoord2d (color : int) (size : V2i) =
        V2i(color % size.X, color / size.X)

    [<LocalSize(X = 8, Y = 8)>]
    let initRegions (config : Config[]) (regions : IntImage2d<Formats.r32i>) (colors : Image2d<Formats.r16>) (infos : V4i[]) =
        compute {
            let offset = config.[0].Offset
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
            let offset = cfg.Offset
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
            let offset = config.[0].Offset
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
    let allocateCompactRegions (infos : V4i[]) (denseIds : int[]) (denseIdCount : int[]) =
        compute {
            let offset : int = uniform?Offset
            let count : int = uniform?RegionCount
            let id = getGlobalId().X

            if id < count then
                let cnt = infos.[id].X
                if cnt > 0 then
                    // allocate a slot
                    let resId = atomicAdd denseIdCount.[0] 1
                    denseIds.[resId - offset] <- id



                    // store the remapping-id
                    infos.[id].W <- resId
                    //remapImage.[id] <- V4i(resId, 0, 0, 0)


                ()

        }
            
    [<LocalSize(X = 64)>]
    let storeCompactRegions (store : V4i[]) (denseIds : int[]) (count : int) (infos : V4i[]) =
        compute {
            let offset : int = uniform?Offset
            let id = getGlobalId().X
            if id < count then
                let oCode = denseIds.[id]
    
                store.[offset + id] <- V4i(infos.[oCode].XYZ, 0)
                
//                let cnt = info.X
//                let sum = intBitsToFloat info.Y
//                let sumSq = intBitsToFloat info.Z
//
//                let avg = sum / float cnt
//                let var = if cnt < 2 then 0.0 else (sumSq - float cnt*avg*avg) / (float (cnt - 1))
//                let dev = var |> sqrt
//                let mutable res = Unchecked.defaultof<RegionStats>
//                res.StoreI <- V4i(cnt, 0, offset + id, 0)
//                res.StoreF <- V4f(float32 avg, float32 dev, 0.0f, 0.0f)
//                store.[offset + id] <- res

        }



    [<LocalSize(X = 8, Y = 8)>]
    let remapRegions (config : Config[]) (infos : V4i[]) (regions : IntImage2d<Formats.r32i>) =
        compute {
            let id = getGlobalId().XY
            let regionSize = regions.Size
            let offset = config.[0].Offset

            let cid = offset + id

            if cid.X < regionSize.X && cid.Y < regionSize.Y then
                let oCode = regions.[cid].X
                let nCode =  infos.[oCode].W
                regions.[cid] <- V4i(nCode, 0, 0, 0)
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
                res.StoreI <- V4i(cnt, 0, id, 0)
                res.StoreF <- V4f(float32 avg, float32 dev, 0.0f, 0.0f)
                stats.[id] <- res
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

    member x.Runtime = runtime
    member x.InitRegions = initRegions
    member x.RegionMerge = regionMerge
    member x.Sanitize = sanitize
    member x.SanitizeAvg = sanitizeAvg
    
    member x.AllocateCompactRegions = allocateCompactRegions
    member x.StoreCompactRegions = storeCompactRegions
    member x.RemapRegions = remapRegions
    member x.ToRegionStats = toRegionStats


    member x.Dispose() =
        runtime.DeleteComputeShader initRegions
        runtime.DeleteComputeShader regionMerge
        runtime.DeleteComputeShader sanitize
        runtime.DeleteComputeShader sanitizeAvg
        runtime.DeleteComputeShader allocateCompactRegions
        runtime.DeleteComputeShader storeCompactRegions
        runtime.DeleteComputeShader remapRegions
        runtime.DeleteComputeShader toRegionStats

    interface IDisposable with
        member x.Dispose() = x.Dispose()

type RegionMerge (runtime : IRuntime, mergeMode : SegmentMergeMode) =

    let tDistr = 
        let img = PixImage<float32>(Col.Format.Gray, Volume<float32>(TDistribution.Data, V3i(500, 128, 1)))
        let tex = runtime.CreateTexture(img.Size, TextureFormat.R32f, 1, 1)
        runtime.Upload(tex, 0, 0, img)
        tex

    let kernels2d = lazy (new RegionMergeKernels2d(runtime, mergeMode))

    member x.TDistrTexture = tDistr
    member x.Runtime = runtime
 
    member x.NewInstance(size : V2i) =
        new RegionMergeInstance2d(kernels2d.Value, tDistr, size)
        
    member x.Dispose() =
        if kernels2d.IsValueCreated then kernels2d.Value.Dispose()

    interface IDisposable with
        member x.Dispose() = x.Dispose()

and RegionMergeInstance2d internal(parent : RegionMergeKernels2d, tDistr : IBackendTexture, size : V2i) =  
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
    let initRegions = parent.InitRegions
    let regionMerge = parent.RegionMerge
    let sanitize = parent.Sanitize
    let sanitizeAvg = parent.SanitizeAvg
        
    let config          = runtime.CreateBuffer<SegmentationShaders.Config> [| SegmentationShaders.Config(V2i.Zero, 0.01f, 0.01f) |]
    let regions         = runtime.CreateTexture(V2i.II, TextureFormat.R32i, 1, 1)
    let compactIdCount  = runtime.CreateBuffer<int>(1)

    let infos           = runtime.CreateBuffer<V4i>(size.X * size.Y)
    let compactIds      = runtime.CreateBuffer<int>(size.X * size.Y)

    let initIn = 
        let binding = runtime.NewInputBinding initRegions
        binding.["regions"] <- regions.[TextureAspect.Color, 0, 0]
        binding.["config"] <- config
        binding.["infos"] <- infos
        binding.["BlockSize"] <- size
        binding.Flush()
        binding

    let mergeInputs =
        buildMerges size |> List.map (fun (solvedSize, dim) ->
            let mergeIn = runtime.NewInputBinding regionMerge
            
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
        let binding = runtime.NewInputBinding sanitize
        binding.["config"] <- config
        binding.["regions"] <- regions.[TextureAspect.Color, 0, 0]
        binding.["infos"] <- infos
        binding.Flush()
        binding

    let sanitizeAvgIn = 
        let binding = runtime.NewInputBinding sanitizeAvg
        binding.["infos"] <- infos
        binding.["RegionCount"] <- size.X * size.Y
        binding.Flush()
        binding

    let allocateCompactRegionsIn =
        let binding = runtime.NewInputBinding parent.AllocateCompactRegions
        binding.["Offset"] <- 0
        binding.["RegionCount"] <- size.X * size.Y
        binding.["infos"] <- infos
        binding.["denseIds"] <- compactIds
        binding.["denseIdCount"] <- compactIdCount
        binding.Flush()
        binding

    let remapRegionsIn =
        let binding = runtime.NewInputBinding parent.RemapRegions
        binding.["config"] <- config
        binding.["regions"] <- regions.[TextureAspect.Color, 0, 0]
        binding.["infos"] <- infos
        binding.Flush()
        binding

    let storeCompactRegionsIn =
        let binding = runtime.NewInputBinding parent.StoreCompactRegions
        binding.["denseIds"] <- compactIds
        binding.["count"] <- 0
        binding.["Offset"] <- 0
        binding.["infos"] <- infos
        binding.Flush()
        binding

    let toRegionStatsIn =
        let binding = runtime.NewInputBinding parent.ToRegionStats
        binding.["infos"] <- infos
        binding.["count"] <- 0
        binding.["stats"] <- infos
        binding.Flush()
        binding
        
    let mutable currentConfig = SegmentationShaders.Config(V2i.Zero, 0.01f, 0.01f)
    let mutable currentInput = None
    let mutable currentRegions = regions

    let setConfig (cfg : SegmentationShaders.Config) =
        if currentConfig <> cfg then
            currentConfig <- cfg
            config.Upload [| cfg |]
        
    let setOffset (v : V2i) =
        if v <> currentConfig.Offset then
            let cfg = SegmentationShaders.Config(v, currentConfig.Threshold, currentConfig.Alpha)
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

            remapRegionsIn.["regions"] <- r.[TextureAspect.Color, 0, 0]
            remapRegionsIn.Flush()
        

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
//
//    let solveProgram =
//        runtime.Compile [
//        
//            yield ComputeCommand.TransformLayout(regions, TextureLayout.ShaderReadWrite)
//
//            yield ComputeCommand.Bind initRegions
//            yield ComputeCommand.SetInput initIn
//            yield ComputeCommand.Dispatch (V2i(ceilDiv size.X 8, ceilDiv size.Y 8))
//            yield ComputeCommand.Sync regions
//            yield ComputeCommand.Sync infos.Buffer
//
//            for mergeIn, groupCount in mergeInputs do
//                yield ComputeCommand.Bind regionMerge
//                yield ComputeCommand.SetInput mergeIn
//                yield ComputeCommand.Dispatch groupCount
//                
//                yield ComputeCommand.Sync infos.Buffer
//
//                yield ComputeCommand.Bind sanitize
//                yield ComputeCommand.SetInput sanitizeIn
//                yield ComputeCommand.Dispatch (V2i(ceilDiv size.X 8, ceilDiv size.Y 8))
//
//                yield ComputeCommand.Bind sanitizeAvg
//                yield ComputeCommand.SetInput sanitizeAvgIn
//                yield ComputeCommand.Dispatch (ceilDiv (size.X * size.Y) 64)
//
//                yield ComputeCommand.Sync regions
//                yield ComputeCommand.Sync infos.Buffer
//
//
//        ]


//    [<LocalSize(X = 8, Y = 8)>]
//    let allocateCompactRegions (regionCounts : IntImage2d<Formats.r32i>) (remapImage : IntImage2d<Formats.r32i>) (denseIds : int[]) (denseIdCount : int[])
//    [<LocalSize(X = 64)>]
//    let storeCompactRegions (store : RegionStats[]) (denseIds : int[]) (count : int) (regionSum : IntImage2d<Formats.r32i>) (regionSumSq : IntImage2d<Formats.r32i>) (regionCount : IntImage2d<Formats.r32i>) (regionSurfaces : IntImage2d<Formats.r32i>)
//    [<LocalSize(X = 8, Y = 8)>]
//    let remapRegions (remapImage : IntImage2d<Formats.r32i>) (regions : IntImage2d<Formats.r32i>) 

    let program =  
        runtime.Compile [
            yield ComputeCommand.Bind initRegions
            yield ComputeCommand.SetInput initIn
            yield ComputeCommand.Dispatch (V2i(ceilDiv size.X 8, ceilDiv size.Y 8))
            yield ComputeCommand.Sync infos.Buffer

            for mergeIn, groupCount in mergeInputs do
                yield ComputeCommand.Bind regionMerge
                yield ComputeCommand.SetInput mergeIn
                yield ComputeCommand.Dispatch groupCount
                
                yield ComputeCommand.Sync infos.Buffer

                yield ComputeCommand.Bind sanitize
                yield ComputeCommand.SetInput sanitizeIn
                yield ComputeCommand.Dispatch (V2i(ceilDiv size.X 8, ceilDiv size.Y 8))

                yield ComputeCommand.Bind sanitizeAvg
                yield ComputeCommand.SetInput sanitizeAvgIn
                yield ComputeCommand.Dispatch (ceilDiv (size.X * size.Y) 64)

                yield ComputeCommand.Sync infos.Buffer

            yield ComputeCommand.Bind parent.AllocateCompactRegions
            yield ComputeCommand.SetInput allocateCompactRegionsIn
            yield ComputeCommand.Dispatch (ceilDiv (size.X * size.Y) 64)

        ]


    member x.Run(input : IBackendTexture, resultImage : IBackendTexture, threshold : float, alpha : float) =
        lock x (fun () ->
            let inputSize = input.Size.XY
            
            setInput input
            setRegions resultImage
            setThreshold threshold alpha

            let blocks = V2i(ceilDiv inputSize.X size.X, ceilDiv inputSize.Y size.Y)

            let mutable resultBuffer : Option<IBuffer<V4i>> = None
            let mutable currentCount = 0

            runtime.Run [
                ComputeCommand.TransformLayout(input, TextureLayout.ShaderReadWrite)
                ComputeCommand.TransformLayout(resultImage, TextureLayout.ShaderReadWrite)
                ComputeCommand.Zero compactIdCount
            ]

            for bx in 0 .. blocks.X - 1 do
                for by in 0 .. blocks.Y - 1 do
                    let o = V2i(bx,by) * size
                    setOffset o

                    allocateCompactRegionsIn.["Offset"] <- currentCount
                    allocateCompactRegionsIn.Flush()

                    program.Run()

                    let newCnt = compactIdCount.Download().[0]
                    let n = runtime.CreateBuffer<V4i>(newCnt)
                    
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

            let resultBuffer = resultBuffer.Value
            let remainingMerges = 
                createMerges size inputSize  |> List.map (fun (solvedSize, dim) ->
                    let mergeIn = runtime.NewInputBinding regionMerge
            
                    mergeIn.["Dimension"] <- dim
                    mergeIn.["config"] <- config
                    mergeIn.["SolvedSize"] <- solvedSize
                    mergeIn.["regions"] <- resultImage.[TextureAspect.Color, 0, 0]
                    mergeIn.["infos"] <- resultBuffer
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
            toRegionStatsIn.["infos"] <- resultBuffer
            toRegionStatsIn.["count"] <- resultBuffer.Count
            toRegionStatsIn.["stats"] <- statBuffer
            toRegionStatsIn.Flush()
            
            match remainingMerges with
                | [] ->
                    runtime.Run [
                        yield ComputeCommand.Bind parent.ToRegionStats
                        yield ComputeCommand.SetInput toRegionStatsIn
                        yield ComputeCommand.Dispatch (ceilDiv resultBuffer.Count 64)
                
                        yield ComputeCommand.TransformLayout(input, TextureLayout.ShaderRead)
                        yield ComputeCommand.TransformLayout(resultImage, TextureLayout.ShaderRead)
                    ]

                | _ -> 
                    setOffset V2i.Zero

                    let sanitizeIn = 
                        let binding = runtime.NewInputBinding sanitize
                        binding.["config"] <- config
                        binding.["regions"] <- resultImage.[TextureAspect.Color, 0, 0]
                        binding.["infos"] <- resultBuffer
                        binding.Flush()
                        binding

                    let sanitizeAvgIn = 
                        let binding = runtime.NewInputBinding sanitizeAvg
                        binding.["infos"] <- resultBuffer
                        binding.["RegionCount"] <- resultBuffer.Count
                        binding.Flush()
                        binding

                    runtime.Run [
                        for mergeIn, groupCount in remainingMerges do
                            yield ComputeCommand.Bind regionMerge
                            yield ComputeCommand.SetInput mergeIn
                            yield ComputeCommand.Dispatch groupCount
                
                            yield ComputeCommand.Sync resultBuffer.Buffer

                            yield ComputeCommand.Bind sanitize
                            yield ComputeCommand.SetInput sanitizeIn
                            yield ComputeCommand.Dispatch (V2i(ceilDiv inputSize.X 8, ceilDiv inputSize.Y 8))

                            yield ComputeCommand.Bind sanitizeAvg
                            yield ComputeCommand.SetInput sanitizeAvgIn
                            yield ComputeCommand.Dispatch (ceilDiv resultBuffer.Count 64)

                            yield ComputeCommand.Sync resultImage
                            yield ComputeCommand.Sync resultBuffer.Buffer
                    
                        yield ComputeCommand.Bind parent.ToRegionStats
                        yield ComputeCommand.SetInput toRegionStatsIn
                        yield ComputeCommand.Dispatch (ceilDiv resultBuffer.Count 64)
                
                
                        yield ComputeCommand.TransformLayout(input, TextureLayout.ShaderRead)
                        yield ComputeCommand.TransformLayout(resultImage, TextureLayout.ShaderRead)
                    ]

                    sanitizeIn.Dispose()
                    sanitizeAvgIn.Dispose()
                    remainingMerges |> List.iter (fun (i,_) -> i.Dispose())

            resultBuffer.Dispose()

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
        allocateCompactRegionsIn.Dispose()
        remapRegionsIn.Dispose()
        storeCompactRegionsIn.Dispose()
        
    interface IDisposable with
        member x.Dispose() = x.Dispose()

