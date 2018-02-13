namespace Examples


open System
open Aardvark.Base
open Aardvark.Base.Incremental

open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Base.Incremental.Operators
open Aardvark.Base.Rendering
open Aardvark.Base.ShaderReflection
open Aardvark.Rendering.Text
open System.Runtime.InteropServices
open Aardvark.SceneGraph
open Microsoft.FSharp.NativeInterop

#nowarn "9"
#nowarn "51"


module ComputeShader =

    module Shaders =
        open FShade

        [<LocalSize(X = 64)>]
        let normalized (a : V4d[]) (b : V4d[]) =
            compute {
                let i = getGlobalId().X
                b.[i] <- V4d(Vec.normalize a.[i].XYZ, 1.0)
            }



        let G = 0.01
        
        [<LocalSize(X = 64)>]
        let updateAcceleration (n : int) (pos : V4d[]) (acc : V4d[]) (masses : float[]) =
            compute {
                let i = getGlobalId().X
                if i < n then
                    let p = pos.[i].XYZ

                    let mi = masses.[i]
                    let mutable F = V3d.Zero
                    for j in 0 .. n - 1 do
                        if i <> j then
                            let o = pos.[j].XYZ

                            let diff = o - p
                            let l = Vec.lengthSquared diff
                            let dist = sqrt l
                            if dist > 0.2 then
                                let dir = diff / dist
                                F <- F + dir * ((mi * masses.[j] * G) / l)
                            
                    acc.[i] <- V4d(F / masses.[i], 0.0)

            }

        [<LocalSize(X = 64)>]
        let toTarget (n : int) (src : int[]) (bits : int[]) (targets : int[]) (result : int[]) =
            compute {
                let id = getGlobalId().X
                if id < n then
                    let inputValue = src.[id]
                    let targetLocation = targets.[id]-1
                    if bits.[id] = 1 then
                        result.[targetLocation] <- inputValue
            }
            
        [<LocalSize(X = 64)>]
        let step (n : int) (dt : float) (pos : V4d[]) (vel : V4d[]) (acc : V4d[]) =
            compute {
                let i = getGlobalId().X
                if i < n then
                    let p = pos.[i].XYZ
                    let v = vel.[i].XYZ
                    let a = acc.[i].XYZ

                    let p = p + v * dt //+ a * (0.5 * dt * dt)
                    let v = v + dt * a

                    pos.[i] <- V4d(p, 1.0)
                    vel.[i] <- V4d(v, 0.0)
            }


        type Vertex =
            {
                [<WorldPosition>]           wp : V4d
                [<Position>]                pos : V4d
                [<Semantic("Velocity")>]    vel : V3d
                [<Semantic("Mass")>]        mass  : float
                [<Color>]                   c  : V4d
                [<Semantic("Offset")>]      o : V4d
            }

        [<ReflectedDefinition>]
        let hsv2rgb (h : float) (s : float) (v : float) =
            let s = clamp 0.0 1.0 s
            let v = clamp 0.0 1.0 v

            let h = h % 1.0
            let h = if h < 0.0 then h + 1.0 else h
            let hi = floor ( h * 6.0 ) |> int
            let f = h * 6.0 - float hi
            let p = v * (1.0 - s)
            let q = v * (1.0 - s * f)
            let t = v * (1.0 - s * ( 1.0 - f ))
            match hi with
                | 1 -> V3d(q,v,p)
                | 2 -> V3d(p,v,t)
                | 3 -> V3d(p,q,v)
                | 4 -> V3d(t,p,v)
                | 5 -> V3d(v,p,q)
                | _ -> V3d(v,t,p)

        let instanceOffset (v : Vertex) =
            vertex {
                let magic : float = uniform?Magic
                let scale : float = uniform?Scale 

                let scale1 = scale * (1.0 + Fun.Log2 v.mass)

                return { v with pos = V4d(scale1 * v.pos.XYZ, v.pos.W) + V4d(v.o.XYZ,magic); c = V4d(hsv2rgb (v.mass / 100.0) 1.0 1.0, 1.0) }
            }

        let point (p : Point<Vertex>) =
            line {
                let t : float = uniform?Magic

                

                let vel = p.Value.vel
                let wp0 =  V4d(p.Value.wp.XYZ - 0.05 * vel, 1.0 + (t * 1.0E-50))

                let color = hsv2rgb (p.Value.mass / 100.0) 1.0 1.0

                yield { p.Value with c = V4d(color, 1.0) }
                yield { p.Value with wp = wp0; pos = uniform.ViewProjTrafo * wp0; c = V4d.OOOI }

            }

    open Aardvark.Application.OpenVR


    let benchmark (iter : int) (alternatives : list<string * (unit -> 'a)>) =
        let alternatives = Map.ofList alternatives
        let results = alternatives |> Map.map (fun _ _ -> Array.zeroCreate iter)

        for (name, run) in Map.toSeq alternatives do
            let res = results.[name]
            
            for i in 0 .. min (iter - 1) 10 do
                res.[i] <- run()

            let sw = System.Diagnostics.Stopwatch()
            sw.Start() 
            for i in 0 .. iter - 1 do
                res.[i] <- run()
            sw.Stop()
            Log.line "%s: %A" name (sw.MicroTime / iter)

        let res = results |> Map.toSeq |> Seq.map snd |> Seq.toArray
        let cnt = res.Length

        let mutable errorIndex = -1
        for i in 0 .. iter - 1 do
            let v = res.[0].[i]
            for j in 1 .. cnt - 1 do
                if res.[j].[i] <> v then errorIndex <- i

        if errorIndex >= 0 then
            let values = results |> Map.map (fun _ a -> a.[errorIndex])
            Log.warn "ERROR"
            for (name, value) in Map.toSeq values do
                Log.warn "%s: %A" name value
        else
            Log.line "OK"


                    
                




    type ViewMode =
        | Size = 0
        | Deviation = 1
        | Regions = 2

    module Detector =
        open FShade

        [<ReflectedDefinition>]
        let hsv2rgb (h : float) (s : float) (v : float) =
            let s = clamp 0.0 1.0 s
            let v = clamp 0.0 1.0 v

            let h = h % 1.0
            let h = if h < 0.0 then h + 1.0 else h
            let hi = floor ( h * 6.0 ) |> int
            let f = h * 6.0 - float hi
            let p = v * (1.0 - s)
            let q = v * (1.0 - s * f)
            let t = v * (1.0 - s * ( 1.0 - f ))
            match hi with
                | 1 -> V3d(q,v,p)
                | 2 -> V3d(p,v,t)
                | 3 -> V3d(p,q,v)
                | 4 -> V3d(t,p,v)
                | 5 -> V3d(v,p,q)
                | _ -> V3d(v,t,p)

        [<GLSLIntrinsic("floatBitsToInt({0})")>]
        let floatBitsToInt (v : float) : int = onlyInShaderCode "floatBitsToInt"
        
        [<GLSLIntrinsic("intBitsToFloat({0})")>]
        let intBitsToFloat (v : int) : float = onlyInShaderCode "intBitsToFloat"

        [<GLSLIntrinsic("atomicAdd({0}, {1})")>]
        let atomicAdd (l : int) (v : int) : int = onlyInShaderCode "atomicAdd"

        [<LocalSize(X = 8, Y = 8)>]
        let initRegions (regions : IntImage2d<Formats.r32i>) (colors : Image2d<Formats.r16>) (regionSum : IntImage2d<Formats.r32i>) (regionSumSq : IntImage2d<Formats.r32i>) (regionCount : IntImage2d<Formats.r32i>) (collapseImg : IntImage2d<Formats.r32i>) =
            compute {
                let size = regions.Size
                let id = getGlobalId().XY
                if id.X < size.X && id.Y < size.Y then
                    let v = colors.[id].X
                    regions.[id] <- V4i(id.Y * size.X + id.X, 0, 0, 0)
                    regionSum.[id] <- V4i(floatBitsToInt v, 0, 0, 0)
                    regionSumSq.[id] <- V4i(floatBitsToInt (v * v), 0, 0, 0)
                    regionCount.[id] <- V4i(1, 0, 0, 0)
                    collapseImg.[id] <- V4i(0,0,0,0)
            }

        [<LocalSize(X = 64)>]
        let regionMerge (colors : Image2d<Formats.r16>) (regions : IntImage2d<Formats.r32i>) (regionSum : IntImage2d<Formats.r32i>) (regionSumSq : IntImage2d<Formats.r32i>) (regionCount : IntImage2d<Formats.r32i>) (collapseImg : IntImage2d<Formats.r32i>) =
            compute {
                let threshold : float = uniform?Threshold
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

                    let lRegion = V2i(lr % size.X, lr / size.X)
                    let rRegion = V2i(rr % size.X, rr / size.X)

                    let lCnt = regionCount.[lRegion].X |> float
                    let rCnt = regionCount.[rRegion].X |> float

                    let lAvg = intBitsToFloat regionSum.[lRegion].X / lCnt
                    let rAvg = intBitsToFloat regionSum.[rRegion].X / rCnt

                    let lDev = intBitsToFloat regionSumSq.[lRegion].X / lCnt - lAvg |> sqrt
                    let rDev = intBitsToFloat regionSumSq.[rRegion].X / rCnt - lAvg |> sqrt

                    let lValue = colors.[lid].X
                    let rValue = colors.[rid].X

                    let distance = min (abs (lValue - rAvg)) (abs (rValue - lAvg))
                    if distance < threshold then
                        



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
                                src <- V2i(dst % size.X, dst / size.X)
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
                                        src <- V2i(r % size.X, r / size.X)
                                        
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
                    let rc = V2i(r % size.X, r / size.X)

                    let mutable last = 0
                    let mutable dst = collapseImg.[rc].X
                    while dst <> 0 do
                        last <- dst
                        dst <- collapseImg.[V2i((dst - 1) % size.X, (dst - 1) / size.X)].X
                    
                    if last <> 0 then
                        regions.[id] <- V4i(last - 1, 0, 0, 0)
            }

        [<LocalSize(X = 8, Y = 8)>]
        let sanitizeAverage (regionSum : IntImage2d<Formats.r32i>) (regionSumSq : IntImage2d<Formats.r32i>) (regionCount : IntImage2d<Formats.r32i>) (collapseImg : IntImage2d<Formats.r32i>) =
            compute {
                let size = regionSum.Size
                let rc = getGlobalId().XY

                if rc.X < size.X && rc.Y < size.Y then
                    let r = rc.Y * size.X + rc.X
                    let mutable last = 0
                    let mutable dst = collapseImg.[rc].X
                    while dst <> 0 do
                        last <- dst
                        dst <- collapseImg.[V2i((dst - 1) % size.X, (dst - 1) / size.X)].X
                        
                    if last <> 0 then
                        let dstI = last - 1
                        let dst = V2i(dstI % size.X, dstI / size.X)

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
                    let rCoord = V2i(rCode % size.X, rCode / size.X)

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


        let inputTexture =
            sampler2d {
                texture uniform?InputTexture
            }

        let infoTexture =
            sampler2d {
                texture uniform?ResultTexture
            }

        let regionTexture =
            intSampler2d {
                texture uniform?RegionTexture
            }

        let randomColors =
            sampler2d {
                texture uniform?RandomColors
            }

        let showOutput (v : Effects.Vertex) =
            fragment {
                let mode : ViewMode = uniform?Mode
                let threshold : float = uniform?Threshold
                let fade : float = uniform?Fade


                let size = infoTexture.Size
                let p = V2i (V2d size * v.tc)

                let inValue = V3d.III * inputTexture.[p].X

                let mutable color = inValue

                match mode with
                    | ViewMode.Regions ->
                        let rCode = regionTexture.[p].X
                        let rCoord = V2i(rCode % size.X, rCode / size.X)
                        let rColor = randomColors.[rCoord]

                        color <- rColor.XYZ

                    | ViewMode.Size ->
                        let value = infoTexture.[p]

                        let avg = value.X
                        let dev = value.Y
                        let cnt = packUnorm2x16 value.ZW

                        let area = float cnt / float (size.X * size.Y)
                        let cc = hsv2rgb (area * 0.5) 1.0 1.0

                        color <- cc

                    | _ ->
                        let value = infoTexture.[p]

                        let avg = value.X
                        let dev = value.Y
                        //let cnt = packUnorm2x16 value.ZW
                        
                        let rDev = dev / threshold
                        let cc = hsv2rgb (rDev * 0.5) 1.0 1.0
                        color <- cc


                let final = (1.0 - fade) * inValue + (fade) * color
                return V4d(final, 1.0)
            }

            
    let inline ceilDiv v a =
        if v % a = LanguagePrimitives.GenericZero then v / a
        else LanguagePrimitives.GenericOne + v / a


    type RegionMerge(runtime : IRuntime) =
        
        let initRegions = runtime.CreateComputeShader Detector.initRegions
        let regionMerge = runtime.CreateComputeShader Detector.regionMerge
        let sanitize = runtime.CreateComputeShader Detector.sanitize
        let sanitizeAvg = runtime.CreateComputeShader Detector.sanitizeAverage
        let writeOut = runtime.CreateComputeShader Detector.writeToOutput
        
        member x.Runtime = runtime
        member internal x.InitRegions = initRegions
        member internal x.RegionMerge = regionMerge
        member internal x.Sanitize = sanitize
        member internal x.SanitizeAvg = sanitizeAvg
        member internal x.WriteOut = writeOut

        member x.NewInstance(size : V2i) =
            new RegionMergeInstance(x, size)
        
        member x.Dispose() =
            runtime.DeleteComputeShader initRegions
            runtime.DeleteComputeShader regionMerge
            runtime.DeleteComputeShader sanitize
            runtime.DeleteComputeShader sanitizeAvg
            runtime.DeleteComputeShader writeOut

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    and RegionMergeInstance(parent : RegionMerge, size : V2i) =  
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


    let run() =
        let env = Environment.GetCommandLineArgs()

        let mutable path = "116.png"
        let mutable threshold = 0.01
        for i in 0 .. env.Length - 2 do
            if env.[i] = "-t" then
                match System.Double.TryParse(env.[i+1]) with
                    | (true, v) ->
                        threshold <- v
                    | _ ->
                        ()
            if env.[i] = "-i" then
                path <- env.[i+1]


        

        use app = new VulkanApplication(false)
        //use app = new OpenGlApplication(true)
        let runtime = app.Runtime :> IRuntime
        let win = app.CreateSimpleRenderWindow(1) 

        
        let par = ParallelPrimitives(runtime)


        let dataImg     = PixImage.Create(path).ToPixImage<uint16>(Col.Format.Gray) //PixImage<uint16>(Col.Format.Gray, Volume<uint16>(data, 4L, 4L, 1L))
        let size        = dataImg.Size

        use merge = new RegionMerge(runtime)
        use instance = merge.NewInstance size


        let randomColors =
            let rand = RandomSystem()
            let img = PixImage<byte>(Col.Format.RGBA, size)
            img.GetMatrix<C4b>().SetByIndex (fun (i : int64) ->
                rand.UniformC3f().ToC4b()
            ) |> ignore

            PixTexture2d(PixImageMipMap [| img :> PixImage|], TextureParams.empty) :> ITexture


        let img         = runtime.CreateTexture(size, TextureFormat.R16, 1, 1)
        let res         = runtime.CreateTexture(size, TextureFormat.Rgba16, 1, 1)
        let regions     = runtime.CreateTexture(size, TextureFormat.R32i, 1, 1)
        runtime.Upload(img, 0, 0, dataImg)

        
        let fade = Mod.init 1.0
        let mode = Mod.init ViewMode.Regions
        let threshold = Mod.init 0.01

        let textures =
            threshold |> Mod.map (fun threshold ->
                instance.Run(img, res, regions, threshold)
                res :> ITexture, regions :> ITexture
            )

        let info = textures |> Mod.map fst
        let regionIds = textures |> Mod.map snd


        win.Keyboard.KeyDown(Keys.M).Values.Add(fun _ ->
            transact (fun () ->
                mode.Value <- 
                    match mode.Value with
                        | ViewMode.Deviation -> ViewMode.Regions
                        | ViewMode.Regions -> ViewMode.Size
                        | _ -> ViewMode.Deviation
            )
        )

        win.Keyboard.KeyDown(Keys.Add).Values.Add(fun _ ->
            transact (fun () ->
                threshold.Value <- threshold.Value + 0.0001
                Log.line "threshold: %A" threshold.Value
            )
        )
        
        win.Keyboard.KeyDown(Keys.Subtract).Values.Add(fun _ ->
            transact (fun () ->
                threshold.Value <- threshold.Value - 0.0001
                Log.line "threshold: %A" threshold.Value
            )
        )

        win.Mouse.Scroll.Values.Add (fun delta ->
            transact (fun () ->
                let s = sign delta
                fade.Value <- clamp 0.0 1.0 (fade.Value + float s * 0.05)
                Log.line "fade: %A" fade.Value
            )
        )



        let old () = 
            let threshold = 0.01
            let sum         = runtime.CreateTexture(size, TextureFormat.R32i, 1, 1)
            let sumSq       = runtime.CreateTexture(size, TextureFormat.R32i, 1, 1)
            let cnt         = runtime.CreateTexture(size, TextureFormat.R32i, 1, 1)
            let regions     = runtime.CreateTexture(size, TextureFormat.R32i, 1, 1)
            let collapse    = runtime.CreateTexture(size, TextureFormat.R32i, 1, 1)


            let initRegions = runtime.CreateComputeShader Detector.initRegions
            let regionMerge = runtime.CreateComputeShader Detector.regionMerge
            let sanitize = runtime.CreateComputeShader Detector.sanitize
            let sanitizeAvg = runtime.CreateComputeShader Detector.sanitizeAverage
            let writeOut = runtime.CreateComputeShader Detector.writeToOutput

            let initIn = runtime.NewInputBinding initRegions
            initIn.["regions"] <- regions.[TextureAspect.Color, 0, 0]
            initIn.["colors"] <- img.[TextureAspect.Color, 0, 0]
            initIn.["regionSum"] <- sum.[TextureAspect.Color, 0, 0]
            initIn.["regionSumSq"] <- sumSq.[TextureAspect.Color, 0, 0]
            initIn.["regionCount"] <- cnt.[TextureAspect.Color, 0, 0]
            initIn.["collapseImg"] <- collapse.[TextureAspect.Color, 0, 0]
            initIn.Flush()


            let merges (size : V2i) =
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

            let merges = merges size

            printfn "threshold: %f" threshold
            for (s,dim) in merges do
                printfn "merge(%d, %d)" s dim

            let mergeInputs =
                merges |> List.map (fun (solvedSize, dim) ->
                    let mergeIn = runtime.NewInputBinding regionMerge
                    mergeIn.["Dimension"] <- dim
                    mergeIn.["SolvedSize"] <- solvedSize
                    mergeIn.["Threshold"] <- threshold
                    mergeIn.["colors"] <- img.[TextureAspect.Color, 0, 0]
                    mergeIn.["regions"] <- regions.[TextureAspect.Color, 0, 0]
                    mergeIn.["regionSum"] <- sum.[TextureAspect.Color, 0, 0]
                    mergeIn.["regionSumSq"] <- sumSq.[TextureAspect.Color, 0, 0]
                    mergeIn.["regionCount"] <- cnt.[TextureAspect.Color, 0, 0]
                    mergeIn.["collapseImg"] <- collapse.[TextureAspect.Color, 0, 0]
                    mergeIn.Flush()


                    let groups =
                        if dim = 0 then V2i(ceilDiv size.Y 64, ceilDiv (size.X - solvedSize) (2 * solvedSize))
                        else V2i(ceilDiv size.X 64, ceilDiv (size.Y - solvedSize) (2 * solvedSize))


                    mergeIn, groups
                )

        
            let sanitizeIn = runtime.NewInputBinding sanitize
            sanitizeIn.["regions"] <- regions.[TextureAspect.Color, 0, 0]
            sanitizeIn.["collapseImg"] <- collapse.[TextureAspect.Color, 0, 0]
            sanitizeIn.Flush()

        
            let sanitizeAvgIn = runtime.NewInputBinding sanitizeAvg
            sanitizeAvgIn.["regionSum"] <- sum.[TextureAspect.Color, 0, 0]
            sanitizeAvgIn.["regionSumSq"] <- sumSq.[TextureAspect.Color, 0, 0]
            sanitizeAvgIn.["regionCount"] <- cnt.[TextureAspect.Color, 0, 0]
            sanitizeAvgIn.["collapseImg"] <- collapse.[TextureAspect.Color, 0, 0]
            sanitizeAvgIn.Flush()

            let writeOutIn = runtime.NewInputBinding writeOut
            writeOutIn.["result"] <- res.[TextureAspect.Color, 0, 0]
            writeOutIn.["regions"] <- regions.[TextureAspect.Color, 0, 0]
            writeOutIn.["regionSum"] <- sum.[TextureAspect.Color, 0, 0]
            writeOutIn.["regionSumSq"] <- sumSq.[TextureAspect.Color, 0, 0]
            writeOutIn.["regionCount"] <- cnt.[TextureAspect.Color, 0, 0]
            writeOutIn.Flush()

            let prog = 
                runtime.Compile [
                    yield ComputeCommand.TransformLayout(img, TextureLayout.ShaderReadWrite)
                    yield ComputeCommand.TransformLayout(regions, TextureLayout.ShaderReadWrite)
                    yield ComputeCommand.TransformLayout(sum, TextureLayout.ShaderReadWrite)
                    yield ComputeCommand.TransformLayout(sumSq, TextureLayout.ShaderReadWrite)
                    yield ComputeCommand.TransformLayout(cnt, TextureLayout.ShaderReadWrite)
                    yield ComputeCommand.TransformLayout(collapse, TextureLayout.ShaderReadWrite)
                    yield ComputeCommand.TransformLayout(res, TextureLayout.ShaderReadWrite)

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
                
                    yield ComputeCommand.TransformLayout(res, TextureLayout.ShaderRead)
                ]

            prog.Run()

            Log.line "started"
            let sw = System.Diagnostics.Stopwatch.StartNew()
            for i in 1 .. 10 do
                prog.Run()
            sw.Stop()
            Log.line "took: %A" (sw.MicroTime / 10.0)
            printfn "done"


        let sg = 
            Sg.fullScreenQuad
                |> Sg.uniform "Mode" (mode |> Mod.map int)
                |> Sg.uniform "Threshold" threshold
                |> Sg.uniform "Fade" fade
                |> Sg.texture (Symbol.Create "RandomColors") (Mod.constant randomColors)
                |> Sg.texture (Symbol.Create "InputTexture") (Mod.constant (img :> ITexture))
                |> Sg.texture (Symbol.Create "ResultTexture") info
                |> Sg.texture (Symbol.Create "RegionTexture") regionIds
                |> Sg.shader {
                    do! Detector.showOutput
                }

        win.RenderTask <- Sg.compile win.Runtime win.FramebufferSignature sg
        win.Run()
        
//        let sumImg = PixImage<int32>(Col.Format.Gray, 4L, 4L)
//        runtime.Download(sum, 0, 0, sumImg) 
//        let cntImg = PixImage<int32>(Col.Format.Gray, 4L, 4L)
//        runtime.Download(cnt, 0, 0, cntImg) 
//
//        let div (s : int) (c : int) =
//            let mutable s = s
//            let s : float32 = NativePtr.read (NativePtr.cast &&s)
//            s / float32 c

//        let avg = Array.map2 div sumImg.Volume.Data cntImg.Volume.Data
//
//
//        printfn "avg:      %A" avg
//
//        let regionsImg = PixImage<int32>(Col.Format.Gray, regions.Size.XY)
//        runtime.Download(regions, 0, 0, regionsImg) 
//
//
//        let result = PixImage<byte>(Col.Format.RGBA, regionsImg.Size)
//
//        let cache = IntDict<C4b>()
//        let rand = RandomSystem()
//
//        let cache = IntDict<C4b>()
//        let getColor (r : int) =
//            cache.GetOrCreate(r, fun _ ->
//                rand.UniformC3f().ToC4b()
//            )
//            //HSVf(float32 (0.5 * float remap.[r] / float maxRegion), 1.0f, 1.0f).ToC3f().ToC4b()
//
//        result.GetMatrix<C4b>().SetMap(regionsImg.GetChannel(0L), fun (r : int) -> getColor r) |> ignore
//        result.SaveAsImage @"result.png"


        //printfn "regions:  %A" regionsImg.Volume.Data
//        
//        let dst = PixImage<int32>(Col.Format.Gray, 4L, 4L)
//        runtime.Download(collapse, 0, 0, dst) 
//        printfn "collapse: %A" dst.Volume.Data
        
        Environment.Exit 0





//
//
//        let data = PixImage<float32>(Col.Format.RGBA, V2i(1234, 3241))
//        let rand = RandomSystem()
//        data.GetMatrix<C4f>().SetByCoord(fun (c : V2l) ->
//            let r = rand.UniformFloat() // * 0.5f + 0.5f
//            let g = rand.UniformFloat() // * 0.5f + 0.5f
//            let b = rand.UniformFloat() // * 0.5f + 0.5f
//            C4f(r,g,b,1.0f)
//            //C4b(rand.UniformInt(256), rand.UniformInt(256), rand.UniformInt(256), 255)
//        ) |> ignore
//
//        
//        let f = data.Size.X * data.Size.Y |> float32
//        let cmp = 
//            let data = data.GetMatrix<C4f>()
//            let mutable sum = V4f.Zero
//            data.ForeachIndex(fun i ->
//                let v = data.[i] |> V4f
//                sum <- sum + v / f //(V4f v / V4f(255.0, 255.0, 255.0, 255.0))
//            )
//            sum
//
//
//        let img = runtime.PrepareTexture(PixTexture2d(PixImageMipMap [| data :> PixImage |], TextureParams.mipmapped))
//
//        let res = par.MapReduce(<@ fun _ (v : V4f) -> v / 3999394.0f @>, <@ (+) @>, img.[TextureAspect.Color, 0, 0])
//        printfn "CPU: %A" cmp
//        printfn "GPU: %A" res
//        
//        let data = PixVolume<float32>(Col.Format.RGBA, V3i(128,128,128))
//        
//        let rand = RandomSystem()
//        data.GetVolume<C4f>().SetByCoord(fun (c : V3l) ->
//            let r = rand.UniformFloat() // * 0.5f + 0.5f
//            let g = rand.UniformFloat() // * 0.5f + 0.5f
//            let b = rand.UniformFloat() // * 0.5f + 0.5f
//            C4f(r,g,b,1.0f)
//            //C4b(rand.UniformInt(256), rand.UniformInt(256), rand.UniformInt(256), 255)
//        ) |> ignore
//        
//        let cmp = 
//            let data = data.GetVolume<C4f>()
//            let mutable sum = V4f.Zero
//            data.ForeachIndex(fun i ->
//                let v = data.[i] |> V4f
//                sum <- sum + v / 2097152.0f //(V4f v / V4f(255.0, 255.0, 255.0, 255.0))
//            )
//            sum
//        
//        let img = runtime.PrepareTexture(PixTexture3d(data, TextureParams.mipmapped))
//        let res = par.MapReduce(<@ fun _ (v : V4f) -> v / 2097152.0f @>, <@ (+) @>, img.[TextureAspect.Color, 0, 0])
//        printfn "CPU: %A" cmp
//        printfn "GPU: %A" res

       // System.Environment.Exit 0

        let testArr = [| 100; 5; 10; 20; 1000; 3 |]
        let input = runtime.CreateBuffer<int>(testArr)
        let bits = runtime.CreateBuffer<int>(input.Count)
        let bitsum = runtime.CreateBuffer<int>(input.Count)
        let result = runtime.CreateBuffer<int>(testArr)

        par.CompileMap(<@ fun i e -> if e < 10 then 1 else 0 @>, input, bits).Run()
        let scanned = par.Scan(<@ (+) @>, bits, bitsum)
        let targetWriteShader = runtime.CreateComputeShader Shaders.toTarget
        let targetWrite = runtime.NewInputBinding targetWriteShader
        targetWrite.["src"] <- input
        targetWrite.["n"] <- input.Count
        targetWrite.["bits"] <- bits
        targetWrite.["targets"] <- bitsum
        targetWrite.["result"] <- result
        targetWrite.Flush()
        let ceilDiv (v : int) (d : int) =
            if v % d = 0 then v / d
            else 1 + v / d
        let mk =
             [
                ComputeCommand.Bind(targetWriteShader)
                ComputeCommand.SetInput targetWrite
                ComputeCommand.Dispatch (ceilDiv (int input.Count) 64)
             ]
        let program =
            runtime.Compile mk
        program.Run()

        let bits2 =  bits.Download()
        printfn "%A" bits2

        let scanned2 =  bitsum.Download()
        printfn "%A" scanned2
        let max = scanned2.[scanned2.Length-1]
        let result2 = result.[0..max-1].Download()
        printfn "%A" result2

        let cnt = 1 <<< 20
        let aa = Array.create cnt 1
        let ba = runtime.CreateBuffer<int>(aa)
        let bb = runtime.CreateBuffer<int> cnt

        let rand = Random()
        let mutable i = 0
        while true do
            
            let realCnt = 
                if i < DictConstant.PrimeSizes.Length && DictConstant.PrimeSizes.[i] <= uint32 cnt then
                    int DictConstant.PrimeSizes.[i]
                
                else 
                    abs (rand.Next(cnt)) + 1

            let map = <@ fun i a -> a @>
            let add = <@ (+) @>

            let ba = ba.[0..realCnt-1]
            let bb = bb.[0..realCnt-1]

            let bboida = ba.Download()


            use scan = par.CompileScan(add,ba,bb)

            let reference () =
                let res = Array.zeroCreate ba.Count
                let mutable sum = 0
                for i in 0 .. ba.Count - 1 do
                    sum <- sum + aa.[i]
                    res.[i] <- sum
                res
                    
            scan.Run()
            let arr = bb.Download()
            let ref = reference()
            if arr <> ref then
                Log.warn "cnt:  %A" realCnt
                Log.warn "scan: %A" arr.[arr.Length-1]
                Console.ReadLine() |> ignore
                //Log.warn "fold: %A" r
            else
                Log.line "OK (%A)" realCnt

            i <- i + 1
//        benchmark 100 [
//            
//            //"cpu", reference
//            //"gpu", suma.Run
//            //"gpui", fun () -> par.MapReduce(map, add, ba)
//        ]



//        let scanab = par.CompileScan(<@ (+) @>, ba, bb)
//        scanab.Run()
//
//        let data = bb.Download()
//        let expected = Array.init data.Length (fun i -> 1 + i)
//        if data <> expected then
//            printfn "bad"
//        else
//            printfn "good"
//            printfn "%A" data
        Environment.Exit 0



