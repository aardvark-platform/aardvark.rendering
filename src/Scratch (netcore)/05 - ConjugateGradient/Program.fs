open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application
open Microsoft.FSharp.Quotations
open ConjugateGradient
open Aardvark.Rendering.Vulkan
open Aardvark.Application.Slim
open System
open System.Collections.Concurrent


module ConjugateGradientShaders =
    open Microsoft.FSharp.Quotations
    open FShade

    [<LocalSize(X = 8, Y = 8)>]
    let polynomial2d<'f, 'v when 'f :> Formats.IFloatingFormat> (call : Expr<V2i -> 'v>) (toV4 : Expr<'v -> V4d>) (res : Image2d<'f>) =
        compute {
            let id = getGlobalId().XY
            if id.X < res.Size.X && id.Y < res.Size.Y then
                let v = (%call) id
                res.[id] <- (%toV4) v
        }
        

    let srcSampler =
        sampler2d {
            texture uniform?src
            addressU WrapMode.Clamp
            addressV WrapMode.Clamp
            filter Filter.MinMagLinear
        }
    let weightSampler =
        sampler2d {
            texture uniform?weight
            addressU WrapMode.Border
            addressV WrapMode.Border
            borderColor (C4f(0.0f, 0.0f, 0.0f, 0.0f))
            filter Filter.MinMagLinear
        }

    let weightTimesSrcSampler =
        sampler2d {
            texture uniform?weightTimesSrc
            addressU WrapMode.Border
            addressV WrapMode.Border
            borderColor (C4f(0.0f, 0.0f, 0.0f, 0.0f))
            filter Filter.MinMagLinear
        }

    [<LocalSize(X = 8, Y = 8)>]
    let restrict<'fmt when 'fmt :> Formats.IFloatingFormat> (dst : Image2d<'fmt>) =
        compute {
            let id = getGlobalId().XY
            let dstSize = dst.Size
            let srcSize = srcSampler.Size

            if id.X < dstSize.X && id.Y < dstSize.Y then
                let tc = (V2d(id) + V2d.Half) / V2d(dstSize)
                let d = 0.5 / V2d srcSize

                let vp0 = srcSampler.SampleLevel(tc + V2d( d.X,  0.0), 0.0)
                let vn0 = srcSampler.SampleLevel(tc + V2d(-d.X,  0.0), 0.0)
                let v0p = srcSampler.SampleLevel(tc + V2d( 0.0,  d.Y), 0.0)
                let v0n = srcSampler.SampleLevel(tc + V2d( 0.0, -d.Y), 0.0)
                let value = (vp0 + vn0 + v0n + v0p) / 4.0
                
                //let value = srcSampler.SampleLevel(tc, 0.0)

                dst.[id] <- value
        }


    [<LocalSize(X = 8, Y = 8)>]
    let mul2d<'fmt when 'fmt :> Formats.IFloatingFormat> (l : Image2d<'fmt>) (r : Image2d<'fmt>) (dst : Image2d<'fmt>) (dstWeight : Image2d<'fmt>) =
        compute {
            let id = getGlobalId().XY
            let dstSize = dst.Size
            if id.X < dstSize.X && id.Y < dstSize.Y then
                dst.[id] <- l.[id] * r.[id]
        }

    [<ReflectedDefinition>]
    let inline interpolate4 (v : V2d) (p00 : V4d) (p01 : V4d) (p10 : V4d) (p11 : V4d) =
        let px0 = p00 + v.X * (p10 - p00)
        let px1 = p01 + v.X * (p11 - p01)
        px0 + v.Y * (px1 - px0)

    [<ReflectedDefinition>]
    let inline interpolate1 (v : V2d) (p00 : float) (p01 : float) (p10 : float) (p11 : float) =
        let px0 = p00 + v.X * (p10 - p00)
        let px1 = p01 + v.X * (p11 - p01)
        px0 + v.Y * (px1 - px0)


    [<LocalSize(X = 8, Y = 8)>]
    let restrictWeight<'fmt when 'fmt :> Formats.IFloatingFormat> (dst : Image2d<'fmt>) (dstWeight : Image2d<'fmt>) =
        compute {
            let id = getGlobalId().XY
            let dstSize = dst.Size
            let srcSize = srcSampler.Size

            if id.X < dstSize.X && id.Y < dstSize.Y then
                let tc = (V2d(id) + V2d.Half) / V2d(dstSize)
                let d = 0.5 / V2d srcSize

                let wp0 = weightSampler.SampleLevel(tc + V2d( d.X,  0.0), 0.0).X
                let wn0 = weightSampler.SampleLevel(tc + V2d(-d.X,  0.0), 0.0).X
                let w0p = weightSampler.SampleLevel(tc + V2d( 0.0,  d.Y), 0.0).X
                let w0n = weightSampler.SampleLevel(tc + V2d( 0.0, -d.Y), 0.0).X
                let weightAvg = (wp0 + wn0 + w0n + w0p) / 4.0
                
                let vp0 = weightTimesSrcSampler.SampleLevel(tc + V2d( d.X,  0.0), 0.0)
                let vn0 = weightTimesSrcSampler.SampleLevel(tc + V2d(-d.X,  0.0), 0.0)
                let v0p = weightTimesSrcSampler.SampleLevel(tc + V2d( 0.0,  d.Y), 0.0)
                let v0n = weightTimesSrcSampler.SampleLevel(tc + V2d( 0.0, -d.Y), 0.0)
                let v = (vp0 + vn0 + v0n + v0p) / 4.0




                //let weightAvg = weightSampler.SampleLevel(tc, 0.0).X
                //let v = weightTimesSrcSampler.SampleLevel(tc, 0.0)

                let value = 
                    if weightAvg < 0.00001 then 
                        V4d.Zero
                    else 
                        //let v = interpolate4 frac (v00 * w00) (v01 * w01) (v10 * w10) (v11 * w11)
                        v / weightAvg

                dst.[id] <- value
                dstWeight.[id] <- V4d.IIII * weightAvg
        }

    [<LocalSize(X = 8, Y = 8)>]
    let interpolate<'fmt when 'fmt :> Formats.IFloatingFormat> (dst : Image2d<'fmt>) =
        compute {
            let id = getGlobalId().XY
            let dstSize = dst.Size
            let srcSize = srcSampler.Size

            if id.X < dstSize.X && id.Y < dstSize.Y then
                let tc = (V2d(id) + V2d.Half) / V2d(dstSize)
                let v = srcSampler.SampleLevel(tc, 0.0)
                dst.[id] <- v 
        }
    [<LocalSize(X = 8, Y = 8)>]

    let divergence<'fmt when 'fmt :> Formats.IFloatingFormat> (dst : Image2d<'fmt>) =
        compute {
            let id = getGlobalId().XY
            let dstSize = dst.Size
            let srcSize = srcSampler.Size

            if id.X < dstSize.X && id.Y < dstSize.Y then
                let tc = (V2d(id) + V2d.Half) / V2d(dstSize)

                let d = 1.0 / V2d srcSize

                let v00  = srcSampler.SampleLevel(tc,0.0)
                let vp0  = srcSampler.SampleLevel(tc + V2d( d.X, 0.0 ),0.0)
                let vn0  = srcSampler.SampleLevel(tc + V2d(-d.X, 0.0 ),0.0)
                let v0p  = srcSampler.SampleLevel(tc + V2d( 0.0, d.Y ),0.0)
                let v0n  = srcSampler.SampleLevel(tc + V2d( 0.0,-d.Y ),0.0)

                let div = 4.0 * v00 - vp0 - vn0 - v0p - v0n
                
                dst.[id] <- div 
        }

type ConjugateGradientSolver2d<'f, 'v when 'v : unmanaged and 'f :> FShade.Formats.IFloatingFormat> (runtime : IRuntime, residual' : Term.TermParameter2d -> Term<V2i> -> Term<V2i>) =
    static let ceilDiv (a : int) (b : int) =
        if a % b = 0 then a / b
        else 1 + a / b
    
    static let ceilDiv2 (a : V2i) (b : V2i) =
        V2i(
            ceilDiv a.X b.X,
            ceilDiv a.Y b.Y
        )

    static let getFormat =
        LookupTable.lookupTable [
            typeof<FShade.Formats.r32f>, TextureFormat.R32f
            typeof<FShade.Formats.rg32f>, TextureFormat.Rg32f
            typeof<FShade.Formats.rgba32f>, TextureFormat.Rgba32f
        ]

    static let format = getFormat typeof<'f>

    static let doWhile (cond : unit -> bool) (action : unit -> unit) =
        action()
        while cond() do action()

    let real = RealInstances.instance<'v>
    let rreal = ReflectedReal.instance<'v>
    
    let tools = TensorTools<'v>.Get(runtime)
    
    let poly'  = residual' (Term.TermParameter2d "x") (Term.Uniform "h")

    //let epoly = Term.toReflectedCall Term.Read.image residual
    //let upoly = Term.parameters residual |> HMap.toList |> List.map fst
    
    let epoly' = Term.toReflectedCall Term.Read.image poly'
    let upoly' = Term.parameters poly' |> HMap.toList |> List.map fst

    let poly'' =
        let d = Term.TermParameter2d("d")
        
        let mutable sum = Term<V2i>.Zero
        let all = Term.allDerivatives "x" poly'
        for (c, p) in HMap.toSeq all do
            sum <- sum + p * d.[c.X, c.Y]
        sum

    let epoly'' = Term.toReflectedCall Term.Read.image poly''
    let upoly'' = Term.parameters poly'' |> HMap.toList |> List.map fst


    let negativeV4 = 
        let toV4 = rreal.toV4
        let neg = rreal.neg
        <@ fun v -> (%toV4) ((%neg) v) @>
        
    let negativeDerivative = runtime.CreateComputeShader (ConjugateGradientShaders.polynomial2d<'f, 'v> epoly' negativeV4)
    let derivative = runtime.CreateComputeShader (ConjugateGradientShaders.polynomial2d<'f, 'v> epoly' rreal.toV4)
    let secondMulD = runtime.CreateComputeShader (ConjugateGradientShaders.polynomial2d<'f, 'v> epoly'' rreal.toV4)
    
    let createTexture (img : PixImage) =
        let t = runtime.CreateTexture(img.Size, TextureFormat.ofPixFormat img.PixFormat TextureParams.empty, 1, 1)
        runtime.Upload(t, 0, 0, img)
        t

    member x.Tools = tools

    member x.NegativeDerivative(h : float, inputs : Map<string, ITextureSubResource>, dst : ITextureSubResource) =
        use input = runtime.NewInputBinding negativeDerivative
        input.["res"] <- dst
        input.["h"] <- real.fromFloat h

        for used in upoly' do
            let r = inputs.[used]
            input.[used] <- r
            input.[sprintf "%sLevel" used] <- 0
            input.[sprintf "%sSize" used] <- r.Size.XY
            
        input.Flush()

        runtime.Run [
            ComputeCommand.Bind negativeDerivative
            ComputeCommand.SetInput input
            ComputeCommand.Dispatch(ceilDiv2 dst.Size.XY negativeDerivative.LocalSize.XY)
        ]

    member x.Derivative(h : float, inputs : Map<string, ITextureSubResource>, dst : ITextureSubResource) =
        use input = runtime.NewInputBinding derivative
        input.["res"] <- dst
        input.["h"] <- real.fromFloat h

        for used in upoly' do
            let r = inputs.[used]
            input.[used] <- r
            input.[sprintf "%sLevel" used] <- 0
            input.[sprintf "%sSize" used] <- r.Size.XY
            
        input.Flush()

        runtime.Run [
            ComputeCommand.Bind derivative
            ComputeCommand.SetInput input
            ComputeCommand.Dispatch(ceilDiv2 dst.Size.XY negativeDerivative.LocalSize.XY)
        ]

    member x.SecondMulD(h : float, inputs : Map<string, ITextureSubResource>, d : ITextureSubResource, dst : ITextureSubResource) =
        use input = runtime.NewInputBinding secondMulD
        input.["res"] <- dst
        input.["h"] <- real.fromFloat h

        input.["d"] <- d
        input.["dLevel"] <- 0
        input.["dSize"] <- d.Size.XY

        for used in upoly' do
            let r = inputs.[used]
            input.[used] <- r
            input.[sprintf "%sLevel" used] <- 0
            input.[sprintf "%sSize" used] <- r.Size.XY
            
        input.Flush()

        runtime.Run [
            ComputeCommand.Bind secondMulD
            ComputeCommand.SetInput input
            ComputeCommand.Dispatch(ceilDiv2 dst.Size.XY negativeDerivative.LocalSize.XY)
        ]


    member internal this.SolveInternal(h : float, inputs : Map<string, ITextureSubResource>, x : ITextureSubResource, eps : float, imax : int, jmax : int) =
        let size = x.Size.XY
        let n = size.X * size.Y
        
        use __ = runtime.NewInputBinding secondMulD

        let mutable i = 0
        let mutable j = 0
        let mutable k = 0

        
        let inputs = Map.add "x" x inputs 
        let r = inputs.["__r"]
        let d = inputs.["__d"]
        let temp = inputs.["__temp"]
        //let r = runtime.CreateTexture(size, format, 1, 1)
        //let d = runtime.CreateTexture(size, format, 1, 1)
        //let temp = runtime.CreateTexture(size, format, 1, 1)


        // r <- -f'(x)
        this.NegativeDerivative(h, inputs, r)

        // d <- r
        runtime.Copy(r, d)
        
        
        let mutable deltaOld = real.zero
        let mutable deltaNew = tools.LengthSquared r
        let delta0 = deltaNew

        let mutable deltaD = deltaNew
        let mutable alpha = real.zero

        let eps2Delta0 = real.mul (real.fromFloat (eps * eps)) delta0
        let eps2 = real.fromFloat (eps * eps)

        while i < imax && real.isGreater deltaNew eps2Delta0 do
            j <- 0

            // deltaD <- <d|d>
            deltaD <- tools.LengthSquared d

            //this.Residual(inputs, temp.[TextureAspect.Color, 0, 0])
            //let sumSq = tools.Sum(temp.[TextureAspect.Color, 0, 0])
            //printfn "res^2: %A (%A)" sumSq deltaD


            let alphaTest() =
                let alpha2DeltaD = real.mul (real.pow alpha 2) deltaD
                real.isGreater alpha2DeltaD eps2

            doWhile (fun () -> j < jmax && alphaTest())  (fun () -> 
                // a <- <f'(x) | d >
                this.Derivative(h, inputs, temp)
                let a = tools.Dot(temp, d)
                
                // b <- <d | f''(x) * d >
                this.SecondMulD(h, inputs, d, temp)
                let b = tools.Dot(temp, d)

                // alpha <- -a / b
                alpha <- real.neg (real.div a b) //(dot d (f'' x d))

                // x <- x + alpha*d
                tools.MultiplyAdd(d, alpha, x, real.one)
                
                j <- j + 1
            )
            
            // r <- -f'(x)
            this.NegativeDerivative(h, inputs, r)
            deltaOld <- deltaNew
            deltaNew <- tools.LengthSquared r
            let beta = real.div deltaNew deltaOld

            // d <- r + beta * d
            tools.MultiplyAdd(r, real.one, d, beta)
            
            k <- k + 1
            if k = n then //|| real.isNegative (tools.Dot(r, d)) then
                runtime.Copy(r, d)
                k <- 0

            i <- i + 1


        printfn "iter %dx%d: %d" size.X size.Y i




    member this.Solve(inputs : Map<string, ITextureSubResource>, x : ITextureSubResource, eps : float, imax : int, jmax : int) =
        let r = runtime.CreateTexture(x.Size.XY, format, 1, 1)
        let d = runtime.CreateTexture(x.Size.XY, format, 1, 1)
        let temp = runtime.CreateTexture(x.Size.XY, format, 1, 1)

        try
            let inputs = 
                inputs
                |> Map.add "__r" r.[TextureAspect.Color, 0, 0]
                |> Map.add "__d" d.[TextureAspect.Color, 0, 0]
                |> Map.add "__temp" temp.[TextureAspect.Color, 0, 0]
            this.SolveInternal(1.0, inputs, x, eps, imax, jmax)
        finally 
            runtime.DeleteTexture r
            runtime.DeleteTexture d
            runtime.DeleteTexture temp



    member this.Solve(inputs : Map<string, PixImage>, x : PixImage, eps : float, imax : int, jmax : int) =
        let inputs = inputs |> Map.map (fun _ img -> (createTexture img).[TextureAspect.Color, 0, 0])
        let x = createTexture x

        this.Solve(inputs, x.[TextureAspect.Color, 0, 0], eps, imax, jmax)

        let res = runtime.Download(x, 0, 0)
        runtime.DeleteTexture x
        inputs |> Map.iter (fun _ t -> runtime.DeleteTexture t.Texture)
        res


type MultigridSolver2d<'f, 'v when 'v : unmanaged and 'f :> FShade.Formats.IFloatingFormat> (runtime : IRuntime, residual : Term.TermParameter2d -> Term<V2i> -> Term<V2i>) = 
    static let ceilDiv (a : int) (b : int) =
        if a % b = 0 then a / b
        else 1 + a / b
    
    static let ceilDiv2 (a : V2i) (b : V2i) =
        V2i(
            ceilDiv a.X b.X,
            ceilDiv a.Y b.Y
        )

    static let getFormat =
        LookupTable.lookupTable [
            typeof<FShade.Formats.r32f>, TextureFormat.R32f
            typeof<FShade.Formats.rg32f>, TextureFormat.Rg32f
            typeof<FShade.Formats.rgba32f>, TextureFormat.Rgba32f
        ]

    static let format = getFormat typeof<'f>
    static let real = RealInstances.instance<'v>
    
    let mul = runtime.CreateComputeShader (ConjugateGradientShaders.mul2d<'f>)
    let restrictWeight = runtime.CreateComputeShader (ConjugateGradientShaders.restrictWeight<'f>)
    let restrict = runtime.CreateComputeShader (ConjugateGradientShaders.restrict<'f>)
    let interpolate = runtime.CreateComputeShader (ConjugateGradientShaders.interpolate<'f>)
    let divergence = runtime.CreateComputeShader (ConjugateGradientShaders.divergence<'f>)
    
    let constantPart, cg = 
        let mutable constantPart = Term<V2i>.Zero
        let bb = Term.TermParameter2d("b")
        let res (x : Term.TermParameter2d) (h : Term<V2i>) =
            let r = residual x h
            let r = Term.derivative "x" V2i.Zero r
            let (a, b) = Term.factorize "x" r
            constantPart <- b //bb.[0,0] * (h - 1.0) //Term<V2i>.Zero
            a + bb.[0,0]

        
        let cg = ConjugateGradientSolver2d<'f, 'v>(runtime, res)
        constantPart, cg

    let uconstantPart = Term.parameters constantPart |> HMap.toSeq |> Seq.map fst |> Set.ofSeq

    let computeB = 
        let rreal = ReflectedReal.instance<'v>
        runtime.CreateComputeShader(ConjugateGradientShaders.polynomial2d<'f, 'v> (Term.toReflectedCall Term.Read.image constantPart) rreal.toV4)

    let createTexture (img : PixImage) =
        let t = runtime.CreateTexture(img.Size, TextureFormat.ofPixFormat img.PixFormat TextureParams.empty, 1, 1)
        runtime.Upload(t, 0, 0, img)
        t

   
    member x.ComputeB(h : float, inputs : Map<string, ITextureSubResource>, dst : ITextureSubResource) =
        use input = runtime.NewInputBinding computeB
        input.["res"] <- dst
        input.["h"] <- real.fromFloat h

        for used in uconstantPart do
            let r = inputs.[used]
            input.[used] <- r
            input.[sprintf "%sLevel" used] <- 0
            input.[sprintf "%sSize" used] <- r.Size.XY
            
        input.Flush()

        runtime.Run [
            ComputeCommand.Bind computeB
            ComputeCommand.SetInput input
            ComputeCommand.Dispatch(ceilDiv2 dst.Size.XY computeB.LocalSize.XY)
        ]


    member x.Restrict(src : ITextureSubResource, srcWeight : ITextureSubResource, dst : ITextureSubResource, dstWeight : ITextureSubResource, temp : ITextureSubResource) =
        use input = runtime.NewInputBinding mul
        input.["l"] <- src
        input.["r"] <- srcWeight
        input.["dst"] <- temp
        input.Flush()
        runtime.Run [
            ComputeCommand.Bind mul
            ComputeCommand.SetInput input
            ComputeCommand.Dispatch (ceilDiv2 src.Size.XY mul.LocalSize.XY)
        ]
        

        use input = runtime.NewInputBinding restrictWeight
        input.["src"] <- src
        input.["weight"] <- srcWeight
        input.["weightTimesSrc"] <- temp
        input.["dst"] <- dst
        input.["dstWeight"] <- dstWeight
        input.Flush()
        runtime.Run [
            ComputeCommand.Bind restrictWeight
            ComputeCommand.SetInput input
            ComputeCommand.Dispatch (ceilDiv2 dst.Size.XY restrictWeight.LocalSize.XY)
        ]

    member x.Restrict(src : ITextureSubResource, dst : ITextureSubResource) =
        use input = runtime.NewInputBinding restrict
        input.["src"] <- src
        input.["dst"] <- dst
        input.Flush()

        runtime.Run [
            ComputeCommand.Bind restrict
            ComputeCommand.SetInput input
            ComputeCommand.Dispatch (ceilDiv2 dst.Size.XY restrict.LocalSize.XY)
        ]

    member x.Interpolate(src : ITextureSubResource, dst : ITextureSubResource) =
        use input = runtime.NewInputBinding interpolate
        input.["src"] <- src
        input.["dst"] <- dst
        input.Flush()

        runtime.Run [
            ComputeCommand.Bind interpolate
            ComputeCommand.SetInput input
            ComputeCommand.Dispatch (ceilDiv2 dst.Size.XY interpolate.LocalSize.XY)
        ]

    member x.Divergence(src : ITextureSubResource, dst : ITextureSubResource) =
        use input = runtime.NewInputBinding divergence
        input.["src"] <- src
        input.["dst"] <- dst
        input.Flush()

        runtime.Run [
            ComputeCommand.Bind divergence
            ComputeCommand.SetInput input
            ComputeCommand.Dispatch (ceilDiv2 dst.Size.XY divergence.LocalSize.XY)
        ]

    member x.Restrict (m : Map<string, ITextureSubResource>) =
        m |> Map.map (fun name img ->
            let next = img.Texture.[img.Aspect, img.Level + 1, img.Slice]
            if not (name.StartsWith "__") then
                if name.StartsWith "w_" then
                    if not (Map.containsKey (name.Substring(2)) m) then
                        x.Restrict(img, next)
                        
                else
                    let wname = "w_" + name
                    match Map.tryFind wname m with
                        | None ->
                            x.Restrict(img, next)
                        | Some wimg ->
                            let wnext = wimg.Texture.[wimg.Aspect, wimg.Level + 1, wimg.Slice]
                            let temp = m.["__temp"]
                            x.Restrict(img, wimg, next, wnext, temp)
            next
        )

    member private x.Cycle(inputs : Map<string, ITextureSubResource>, level : int, size : V2i, ibefore : int, iafter : int, ileaf : int, eps : float) =
        let h = 1 <<< level |> float

        let debug = false
        //cg.Tools.MultiplyAdd(inputs.["__r"], real.zero, inputs.["x"], real.zero)
        
        if debug then
            let r = inputs.["b"]
            runtime.Download(r.Texture, r.Level, r.Slice).SaveAsImage (sprintf @"C:\temp\a\b%dx%d.jpg" r.Size.X r.Size.Y)
            let r = inputs.["v"]
            runtime.Download(r.Texture, r.Level, r.Slice).SaveAsImage (sprintf @"C:\temp\a\v%dx%d.jpg" r.Size.X r.Size.Y)
            let r = inputs.["w_v"]
            runtime.Download(r.Texture, r.Level, r.Slice).SaveAsImage (sprintf @"C:\temp\a\w%dx%d.jpg" r.Size.X r.Size.Y)

        if size.AllGreater 8 then
            if ibefore > 0 then
                cg.SolveInternal(h, inputs, inputs.["x"], eps, ibefore, 1)

            if debug then
                let r = inputs.["x"]
                runtime.Download(r.Texture, r.Level, r.Slice).SaveAsImage (sprintf @"C:\temp\a\x%dx%d.jpg" r.Size.X r.Size.Y)

            //let res = inputs.["__r"]
            //let temp = inputs.["__temp"]
            
            //cg.Derivative(h, inputs, res) // res = Ax + b // res = b


            // Ax = -b
            
            // Ax0 + b = res
            


            // Ax + b = res


            // Ax1 - b + res = 0
            // Ax1 = -(res - b)

            //cg.Tools.MultiplyAdd(inputs.["b"], real.neg real.one, res, real.one)

            //runtime.Copy(inputs.["x"], V3i.Zero, temp, V3i.Zero, temp.Size)
            
            let down = x.Restrict inputs
            //cg.Tools.MultiplyAdd(inputs.["b"], real.zero, res, real.neg real.one)
            //x.Restrict(res, down.["b"])
            cg.Tools.MultiplyAdd(down.["x"], real.zero, down.["b"], real.fromFloat (1.0 / 4.0))


            let half = V2i(max 1 (size.X / 2), max 1 (size.Y / 2))
            
            x.Cycle(down, level + 1, half, ibefore, iafter, ileaf, eps)

            x.Interpolate(down.["x"], inputs.["x"])
            //cg.Tools.MultiplyAdd(temp, real.one, inputs.["x"], real.fromFloat (1.0 / 4.0))
            
            if debug then
                let r = inputs.["x"]
                runtime.Download(r.Texture, r.Level, r.Slice).SaveAsImage (sprintf @"C:\temp\a\u%dx%d.jpg" r.Size.X r.Size.Y)

            if iafter > 0 then
                cg.SolveInternal(h, inputs, inputs.["x"], eps, iafter, 1)
            
           
        else
            if ileaf > 0 then
                cg.SolveInternal(h, inputs, inputs.["x"], eps, ileaf, 1)

        if debug then
            let r = inputs.["x"]
            runtime.Download(r.Texture, r.Level, r.Slice).SaveAsImage (sprintf @"C:\temp\a\f%dx%d.jpg" r.Size.X r.Size.Y)
 
    member this.CreateTexture(img : PixImage) =
        let res = runtime.CreateTexture(img.Size, TextureFormat.ofPixFormat img.PixFormat TextureParams.empty, 1, 1)
        runtime.Upload(res, 0, 0, img)
        res
        

    member this.CreateTempTexture(img : PixImage) =
        let levels = 1 + int(Fun.Floor(Fun.Log2 (max img.Size.X img.Size.Y)))
        let res = runtime.CreateTexture(img.Size, TextureFormat.ofPixFormat img.PixFormat TextureParams.empty, levels, 1)
        runtime.Upload(res, 0, 0, img)
        res
    member x.Tools = cg.Tools

    member this.Solve(inputs : Map<string, ITextureSubResource>, x : ITextureSubResource, n : int, eps : float) =
        use __ = runtime.NewInputBinding restrict

        let size = x.Size
        let levels = 1 + int(Fun.Floor(Fun.Log2 (max x.Size.X x.Size.Y)))
        
        let v = runtime.CreateTexture(size.XY, format, levels, 1)
        runtime.Copy(x, V3i.Zero, v.[TextureAspect.Color, 0, 0], V3i.Zero, size)
  

        let ip =
            inputs |> Map.map (fun name i ->
                if i.Texture.MipMapLevels >= levels then
                    i
                else
                    let res = runtime.CreateTexture(size.XY, format, levels, 1)
                    runtime.Copy(i, V3i.Zero, res.[TextureAspect.Color, 0, 0], V3i.Zero, size)
                    res.[TextureAspect.Color, 0, 0]
            )
            
        let b = runtime.CreateTexture(size.XY, format, levels, 1)
        let b0 = runtime.CreateTexture(size.XY, format, 1, 1)
        let r0 = runtime.CreateTexture(size.XY, format, levels, 1)

        let r,deleteR = 
            match Map.tryFind "__r" inputs with
                | Some r -> r.Texture, false
                | None ->
                    let r = runtime.CreateTexture(size.XY, format, levels, 1)
                    r, true

        let d,deleteD = 
            match Map.tryFind "__d" inputs with
                | Some d -> d.Texture, false
                | None ->
                    let d = runtime.CreateTexture(size.XY, format, levels, 1)
                    d, true

        let temp,deleteTemp = 
            match Map.tryFind "__temp" inputs with
                | Some temp -> temp.Texture, false
                | None ->
                    let temp = runtime.CreateTexture(size.XY, format, levels, 1)
                    temp, true

        let ip =
            ip
            |> Map.add "x" v.[TextureAspect.Color, 0, 0]
            |> Map.add "__r" r.[TextureAspect.Color, 0, 0]
            |> Map.add "__d" d.[TextureAspect.Color, 0, 0]
            |> Map.add "__temp" temp.[TextureAspect.Color, 0, 0]
            |> Map.add "b" b.[TextureAspect.Color, 0, 0]


        this.ComputeB(1.0, ip, b0.[TextureAspect.Color, 0, 0])
        
        for i in 1 .. 4 do
            if i = 1 then runtime.Copy(b0.[TextureAspect.Color, 0, 0], V3i.Zero, b.[TextureAspect.Color, 0, 0], V3i.Zero, size)
            else cg.Derivative(1.0, Map.add "x" x (Map.add "b" b0.[TextureAspect.Color, 0, 0] ip), b.[TextureAspect.Color, 0, 0])
            // Ax = -b

            // Ax + b = r
            
            
            // Ax + b = r

            // Ax1 = +b - r
            // Ax1 = -(r - b)


            this.Cycle(ip, 0, size.XY, 0, 20, 20, eps)

            cg.Tools.MultiplyAdd(v.[TextureAspect.Color, 0, 0], real.one, x, real.one)

            
        let avg = cg.Tools.Average(x)

        //runtime.Copy(sum.[TextureAspect.Color, 0, 0], V3i.Zero, x, V3i.Zero, size)

        runtime.DeleteTexture b
        runtime.DeleteTexture v
        if deleteR then runtime.DeleteTexture r
        if deleteD then runtime.DeleteTexture d
        if deleteTemp then runtime.DeleteTexture temp
        
        for (k,t) in Map.toSeq ip do
            match Map.tryFind k inputs with
                | Some it -> if t.Texture <> it.Texture then runtime.DeleteTexture t.Texture
                | _ -> ()

        avg
    

    member this.Solve(inputs : Map<string, PixImage>, x : PixImage, n : int, eps : float) =
        let inputs = inputs |> Map.map (fun _ img -> (createTexture img).[TextureAspect.Color, 0, 0])
        let x = createTexture x

        let avg = this.Solve(inputs, x.[TextureAspect.Color, 0, 0], n, eps)

        let res = runtime.Download(x, 0, 0)
        runtime.DeleteTexture x
        inputs |> Map.iter (fun _ t -> runtime.DeleteTexture t.Texture)
        res









// This example illustrates how to render a simple triangle using aardvark.
let inline printMat (name : string) (m : IMatrix< ^a >) =
    let table = 
        [
            for y in 0L .. m.Dim.Y - 1L do
                yield [
                    for x in 0L .. m.Dim.X - 1L do
                        let str = sprintf "%.3f" m.[V2l(x,y)]
                        if str.StartsWith "-" then yield str
                        else yield " " + str
                ]
        ]

    let maxLength = table |> Seq.collect (fun row -> row |> Seq.map (fun s -> s.Length)) |> Seq.max
    let pad (str : string) =
        if str.Length < maxLength then 
            let m = maxLength - str.Length
            let b = m // m / 2
            let a = 0 // m - b

            System.String(' ', b) + str + System.String(' ', a)
        else    
            str

    let rows = 
        table |> List.map (fun row ->
            row |> List.map pad |> String.concat "  "
        )
    printfn "%s (%dx%d)" name m.Dim.Y m.Dim.X
    for r in rows do
        printfn "   %s" r





[<EntryPoint>]
let main argv = 
    ////let test = 0.25 * ((-x<float>.[-1] + 2.0 * x<float>.[0] - x<float>.[1] - 50.0) ** 2)
    
    ////let a = test.WithoutConstant("x")
    ////let b = -test.ConstantPart("x")
    
    ////printfn "%A = 0" test
    ////printfn ""
    ////printfn "%A = %A" a b
    ////printfn "%A = %A" (a.Derivative("x", 0)) (b.Derivative("x", 0))
    ////printfn "%A = %A" (a.Derivative("x", 1)) (b.Derivative("x", 1))




    //// f(x) = (A(x) - b)^2


    //// A(x) - b 



    ////let A = test.WithoutConstant("x")
    ////let b = test.ConstantPart("x")

    ////let final = 
    ////    //let test = test.Derivative("x", 0)
    ////    let coords = A.FreeParameters.["x"]
    ////    let s = coords |> Seq.sumBy (fun c -> A.Rename(fun i -> i - c))
    ////    let s = s + b
    ////    s.Derivative("x", 0)

    ////printfn "%A" final
    
    ////Environment.Exit 0


    use app = new HeadlessVulkanApplication()
    //app.Runtime.ShaderCachePath <- None
    let runtime = app.Runtime :> IRuntime


    let div = Term.TermParameter2d("div")
    let v = Term.TermParameter2d("v")
    let w_v = Term.TermParameter2d("w_v")


    // 1*x^2 + 0*y^2


    // (x - a)^2 + (x - b)^2 + 2(x-a)(x-b)
    // ((x-a) + (x-b))^2

    // f(x) = (A1x - b1)^2 + (A2x - b2)^2 + ....


    // df/dx = 2A1T(A1x - b1) + 2A2T(A2x - b2) + ....

    // A1T*A1x + A2T*A2x - (A1T*b1 + A2T*b2) = 0
    // (A1T*A1 + A2T*A2)x - (A1T*b1 + A2T*b2) = f'(x) = 0


    
    let polya (x : Term.TermParameter2d) (h : Term<V2i>) = 
        let term = 
            let cdiv = (4.0 * x.[0,0] - x.[-1,0] - x.[1,0] - x.[0,-1] - x.[0,1]) / (h ** 2)
            0.25 * (cdiv - div.[0,0]) ** 2 //+ 
            //1.0   * w_v.[0,0] * (x.[0,0] - v.[0,0]) ** 2

        //let f' = Term.derivative "x" V2i.Zero term
        //let (d,c) = Term.factorize "x" f'
        //Log.warn "f  = %s" (Term.toString f')
        //Log.warn "Ax = %s" (Term.toString d)
        //Log.warn "b  = %s" (Term.toString c)

        //let test = d + c |> Term.simplify
        //Log.warn "t  = %s" (Term.toString test)
        
        //let f'' = Term.allDerivatives "x" f'
        //let d' = Term.allDerivatives "x" d 

        //for (c,a) in HMap.toList f'' do
        //    Log.warn "df'/d%A = %s" c (Term.toString a)
            
        //for (c,a) in HMap.toList d' do
        //    Log.warn "dd/d%A  = %s" c (Term.toString a)

        term

    //let poly = 0.125f * (4.0f * x<float32>.[0,0] - x.[-1,0] - x.[1,0] - x.[0,-1] - x.[0,1]) ** 2 + w_v.[0,0] * (x<float32>.[0,0] - v.[0,0]) ** 2

    //let test = poly.Rename(fun v -> 2 * v)

    let fmt = Col.Format.Gray
    let solver = MultigridSolver2d<FShade.Formats.r32f, float32>(runtime, polya)
    
    let clooneyPix =
        @"C:\temp\a\bla.png"
            |> PixImage.Create
            
    let acc =
        TensorAccessors(
            Getter = System.Func<float32[], int64, C4f>(fun arr i -> C4f(arr.[int i], arr.[int i], arr.[int i], 1.0f)),
            Setter = System.Action<float32[], int64, C4f>(fun arr i v -> arr.[int i ] <- v.ToGrayFloat())
        )

    let getMat(img : PixImage<float32>) =
        if fmt = Col.Format.RGBA then
            img.GetMatrix<C4f>()
        else 
            let mutable mat = img.Volume.SubXYMatrixWindow<C4f>(0L)
            mat.Accessors <- acc
            mat

    let clooneyPix = clooneyPix.ToPixImage<byte>(Col.Format.RGBA)
    let res = PixImage<float32>(fmt, clooneyPix.Size)
    getMat(res).SetMap(clooneyPix.GetMatrix<C4b>(), fun c -> c.ToC4f()) |> ignore
    res.ToPixImage<byte>().SaveAsImage @"C:\temp\a\input.png"

    let clooneyPix = res
    //clooneyPix.ToPixImage<byte>(Col.Format.Gray).SaveAsImage @"C:\temp\a\input.png"
    
    let clooney = solver.CreateTempTexture(clooneyPix)

    let div = 
        let tex = runtime.CreateTexture(clooney.Size.XY,TextureFormat.R32f, 1, 1)
        solver.Divergence(clooney.[TextureAspect.Color,0,0],tex.[TextureAspect.Color,0,0])
        tex

    

    let size = clooney.Size.XY

    let edgeSize = 1L
    let isXEdge (v : V2l) = v.X < edgeSize || v.X >= int64 size.X - edgeSize
    let isYEdge (v : V2l) = v.Y < edgeSize  || v.Y >= int64 size.Y - edgeSize
    let isCorner (v : V2l) = isXEdge v && isYEdge v
    let isEdge (v : V2l) = isXEdge v || isYEdge v


    let c = V2l(size) / 2L
    let isCenter (v : V2l) =
        v = c || v = c - V2l.IO || v = c - V2l.OI || v = c - V2l.II
        

    

    let x = PixImage<float32>(fmt, size)
    getMat(x).SetByCoord (fun (c : V2l) -> C4f(0.0f)) |> ignore
    
    //let div = PixImage<float32>(Col.Format.Gray, size)
    //div.GetChannel(Col.Channel.Gray).SetByCoord (fun (c : V2l) -> 
    //    0.0f
    //) |> ignore
    
    let cache = Dict<V2l, C4f>()
    let rand = RandomSystem()
    let getColor (c : V2l) =
        //let c = c / 2L
        //if (c.X + c.Y) % 2L = 0L then C4f.White
        //else C4f.Black
        cache.GetOrCreate(c, fun _ -> rand.UniformC3f().ToC4f())


    //let clooneyMat = clooneyPix.GetMatrix<C4b>()
    //let v = PixImage<float32>(fmt, size)
    //getMat(v).SetByCoord (fun (c : V2l) -> 
    //    if isEdge c then clooneyMat.[c].ToC4f()
    //    else C4f(0.0f, 0.0f, 0.0f, 0.0f)
    //    //let c = c / 64L
    //    //if (c.X + c.Y) % 2L = 0L then getColor c
    //    //else C4f(0.0f)
    //) |> ignore
    
    //let w_v = PixImage<float32>(fmt, size)
    //getMat(w_v).SetByCoord (fun (c : V2l) -> 
    //    if isEdge c then C4f(100.0f, 100.0f, 100.0f, 100.0f)
    //    else C4f(0.0f, 0.0f, 0.0f, 0.0f)
    //) |> ignore

    let divPix = PixImage<float32>(Col.Format.RGBA, size)
    runtime.Download(div, 0, 0, divPix)

    let inputs =
        Map.ofList [
            //"div", div :> PixImage
            //"v", v :> PixImage
            //"w_v", w_v :> PixImage
            "div", divPix :> PixImage
        ]

    let textures =
        inputs |> Map.map (fun name img ->
            solver.CreateTempTexture img
        )
    let textures =
        textures
        |> Map.add "__r" (solver.CreateTempTexture x)
        |> Map.add "__d" (solver.CreateTempTexture x)
        |> Map.add "__temp" (solver.CreateTempTexture x)

    let views = textures |> Map.map (fun _ t -> t.[TextureAspect.Color, 0, 0])
    let res = solver.CreateTempTexture x
    


    Log.startTimed "solve"
    let avg = solver.Solve(views, res.[TextureAspect.Color, 0, 0], 1, 1E-10)
    Log.stop()
    
    //runtime.DeleteTexture(res)
    //let res = solver.CreateTexture x
    //Log.startTimed "solve again"
    //solver.Solve(views, res.[TextureAspect.Color, 0, 0], 1, 1E-8)
    //Log.stop()

    runtime.Download(res, 0, 0, x)
    runtime.DeleteTexture res
    textures |> Map.iter (fun _ t -> runtime.DeleteTexture t)

    let tavg = solver.Tools.Average(clooney.[TextureAspect.Color, 0, 0])
    x.GetChannel(0L).SetMap(x.GetChannel(0L), fun v -> v + (tavg - avg)) |> ignore

    //let c = x.ToPixImage<float32>().GetChannel(Col.Channel.Gray)
    //printMat "x" c

    x.SaveAsImage @"C:\temp\a\z_result.jpg"

    //for i in 0 .. 10 do
    //    x <- solver.Solve(inputs, x, 1E-3, 5, 1)
        
    //    let c = x.ToPixImage<float32>().GetChannel(Col.Channel.Gray)
    //    printMat "x" c
        
    //x <- solver.Solve(inputs, x, 1E-3, 20, 1)
    //let c = x.ToPixImage<float32>().GetChannel(Col.Channel.Gray)
    //printMat "x" c
        
        
    //x <- solver.Solve(inputs, x, 1E-3, 20, 1)
    //let c = x.ToPixImage<float32>().GetChannel(Col.Channel.Gray)
    //printMat "x" c

    //let term : Term<V2i> =
    //    let a (x : int) (y : int) = Term.parameter "x" (V2i(x,y))
    //    let h = Term.uniform "h"

    //    0.125 * ((4.0 * a 0 0 - a -1 0 - a 1 0 - a 0 -1 - a 0 1) / (h ** 2)) ** 2
    //    |> Term.derivative "x" V2i.Zero
    //    //|> Term.derivative "x" V2i.Zero

    //let code = Term.toCCode Term.Read.image term
    //printfn "%s" code



    0
