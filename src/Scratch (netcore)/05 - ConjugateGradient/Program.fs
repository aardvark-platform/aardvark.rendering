open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open Microsoft.FSharp.Quotations
open ConjugateGradient
open Aardvark.Rendering.Vulkan
open Aardvark.Application.Slim
open System
open System.Collections.Concurrent

[<ReflectedDefinition>]
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
            filter Filter.MinMagMipLinear
        }

    let weightSampler =
        sampler2d {
            texture uniform?weight
            addressU WrapMode.Border
            addressV WrapMode.Border
            borderColor (C4f(0.0f, 0.0f, 0.0f, 0.0f))
            filter Filter.MinMagMipLinear
        }

    let weightTimesSrcSampler =
        sampler2d {
            texture uniform?weightTimesSrc
            addressU WrapMode.Border
            addressV WrapMode.Border
            borderColor (C4f(0.0f, 0.0f, 0.0f, 0.0f))
            filter Filter.MinMagMipLinear
        }


    let cubic (v : float) =
        let n = V4d(1.0, 2.0, 3.0, 4.0) - V4d(v,v,v,v)
        let s = n * n * n
        let x = s.X
        let y = s.Y - 4.0 * s.X
        let z = s.Z - 4.0 * s.Y + 6.0 * s.X
        let w = 6.0 - x - y - z
        V4d(x, y, z, w) * (1.0/6.0)


    let w0 (a : float) = (1.0/6.0)*(a*(a*(-a + 3.0) - 3.0) + 1.0)
    let w1 (a : float) = (1.0/6.0)*(a*a*(3.0*a - 6.0) + 4.0)
    let w2 (a : float) = (1.0/6.0)*(a*(a*(-3.0*a + 3.0) + 3.0) + 1.0)
    let w3 (a : float) = (1.0/6.0)*(a*a*a)
    let g0 (a : float) = w0(a) + w1(a)
    let g1 (a : float) = w2(a) + w3(a)

    [<GLSLIntrinsic("fract({0})")>]
    let fract (v : V2d) : V2d = onlyInShaderCode "fract"

    let V4(a : V2d, b : V2d) = V4d(a.X, a.Y, b.X, b.Y)

    
    let sampleLinear (sam : Sampler2d) (tc : V2d) : V4d =
        srcSampler.SampleLevel(tc, 0.0)

    let sampleGauss3 (sam : Sampler2d) (tc : V2d) : V4d =
        let size = sam.Size
        let d = 0.5 / V2d size
        let v00 = srcSampler.SampleLevel(tc, 0.0)
        let vp0 = srcSampler.SampleLevel(tc + V2d( d.X,  0.0), 0.0)
        let vn0 = srcSampler.SampleLevel(tc + V2d(-d.X,  0.0), 0.0)
        let v0p = srcSampler.SampleLevel(tc + V2d( 0.0,  d.Y), 0.0)
        let v0n = srcSampler.SampleLevel(tc + V2d( 0.0, -d.Y), 0.0)

        (vp0 + vn0 + v0n + v0p) / 4.0

        
    let sampleGauss5 (sam : Sampler2d) (tc : V2d) : V4d =
        let size = sam.Size
        let d = 0.5 / V2d size

        let sum = 
            sampleGauss3 sam (tc + V2d( d.X,  0.0)) + 
            sampleGauss3 sam (tc + V2d(-d.X,  0.0)) + 
            sampleGauss3 sam (tc + V2d( 0.0,  d.Y)) + 
            sampleGauss3 sam (tc + V2d( 0.0, -d.Y)) + 
            sampleGauss3 sam (tc + V2d( d.X,  d.Y)) + 
            sampleGauss3 sam (tc + V2d( d.X, -d.Y)) + 
            sampleGauss3 sam (tc + V2d(-d.X,  d.Y)) + 
            sampleGauss3 sam (tc + V2d(-d.X, -d.Y))
        sum / 8.0

    let sampleCubic (sam : Sampler2d) (uv : V2d) : V4d =
        let texSize = V2d sam.Size

        // half_f is a sort of sub-pixelquad fraction, -1 <= half_f < 1.
        let half_f = 2.0 * fract(0.5 * uv * texSize - V2d(0.25, 0.25)) - 1.0

        // f is the regular sub-pixel fraction, 0 <= f < 1. This is equivalent to
        // fract(uv * texSize - 0.5), but based on half_f to prevent rounding issues.
        let f = fract(half_f)
        
 
        let s1         = ( 0.5 * f - 0.5) * f           // = w1 / (1 - f)
        let s12        = (-2.0 * f + 1.5) * f + 1.0     // = (w2 - w1) / (1 - f)
        let s34        = ( 2.0 * f - 2.5) * f - 0.5     // = (w4 - w3) / f

        let p0 = (-f * s12 + s1) / (texSize * s12) + uv
        let p1 = (-f * s34 + s1 + s34) / (texSize * s34) + uv
        let positions = V4d(p0.X, p0.Y, p1.X, p1.Y)

        let sign_flip = if half_f.X * half_f.Y > 0.0 then 1.0 else -1.0

        let w          = V4(-f * s12 + s12, s34 * f) // = (w2 - w1, w4 - w3)
        let weights    = V4(w.XZ * (w.Y * sign_flip), w.XZ * (w.W * sign_flip))


        sam.SampleLevel(positions.XY, 0.0) * weights.X +
        sam.SampleLevel(positions.ZY, 0.0) * weights.Y +
        sam.SampleLevel(positions.XW, 0.0) * weights.Z +
        sam.SampleLevel(positions.ZW, 0.0) * weights.W
        


    [<LocalSize(X = 8, Y = 8)>]
    let restrict<'fmt when 'fmt :> Formats.IFloatingFormat> (dst : Image2d<'fmt>) (factor : float) =
        compute {
            let id = getGlobalId().XY
            let dstSize = dst.Size
            let srcSize = srcSampler.Size

            if id.X < dstSize.X && id.Y < dstSize.Y then
                let tc = (V2d(id) + V2d.Half) / V2d(dstSize)


                let d = 0.25 / V2d dstSize
                let vp0 = srcSampler.SampleLevel(tc + V2d( d.X,  0.0), 0.0)
                let vn0 = srcSampler.SampleLevel(tc + V2d(-d.X,  0.0), 0.0)
                let v0p = srcSampler.SampleLevel(tc + V2d( 0.0,  d.Y), 0.0)
                let v0n = srcSampler.SampleLevel(tc + V2d( 0.0, -d.Y), 0.0)
                let value = (vp0 + vn0 + v0n + v0p) / 4.0

                //let value = srcSampler.SampleLevel(tc, 0.0) //sampleLinear srcSampler tc 

                //let value = srcSampler.SampleLevel(tc, 0.0)

                dst.[id] <- factor * value
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

                let d = 0.25 / V2d dstSize
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
                
                //let weightAvg = weightSampler.SampleLevel(tc, 0.0).X //(sampleLinear weightSampler tc).X 
                //let v = weightTimesSrcSampler.SampleLevel(tc, 0.0) //sampleLinear weightTimesSrcSampler tc 

                //let weightAvg = (sampleCubic weightSampler tc).X 
                //let v = sampleCubic weightTimesSrcSampler tc 
                
                //let weightAvg = (sampleLinear weightSampler tc).X 
                //let v = sampleLinear weightTimesSrcSampler tc 
                

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
    let interpolate<'fmt when 'fmt :> Formats.IFloatingFormat> (factor : V4d) (dst : Image2d<'fmt>) =
        compute {
            let id = getGlobalId().XY
            let dstSize = dst.Size
            let srcSize = srcSampler.Size

            if id.X < dstSize.X && id.Y < dstSize.Y then
                let tc = (V2d(id) + V2d.Half) / V2d(dstSize)
                let v = factor * srcSampler.SampleLevel(tc, 0.0)
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


type ConjugateGradientConfig =
    {
        gradientTolerance       : float
        stepTolerance           : float
        maxIterations           : int
        maxLineSearchIterations : int
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
        LookupTable.lookup [
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
    //let upoly = Term.parameters residual |> HashMap.toList |> List.map fst
    
    let epoly' = Term.toReflectedCall Term.Read.image poly'
    let upoly' = Term.parameters poly' |> HashMap.toList |> List.map fst

    let poly'' =
        let d = Term.TermParameter2d("d")
        
        let mutable sum = Term<V2i>.Zero
        let all = Term.allDerivatives "x" poly'
        for (c, p) in HashMap.toSeq all do
            sum <- sum + p * d.[c.X, c.Y]
        sum

    let epoly'' = Term.toReflectedCall Term.Read.image poly''
    let upoly'' = Term.parameters poly'' |> HashMap.toList |> List.map fst


    let negativeV4 = 
        let toV4 = rreal.toV4
        let neg = rreal.neg
        <@ fun v -> (%toV4) ((%neg) v) @>
        
    let negativeDerivative = runtime.CreateComputeShader (ConjugateGradientShaders.polynomial2d<'f, 'v> epoly' negativeV4)
    let derivative = runtime.CreateComputeShader (ConjugateGradientShaders.polynomial2d<'f, 'v> epoly' rreal.toV4)
    let secondMulD = runtime.CreateComputeShader (ConjugateGradientShaders.polynomial2d<'f, 'v> epoly'' rreal.toV4)
    
    let createTexture (img : PixImage) =
        let t = runtime.CreateTexture2D(img.Size, TextureFormat.ofPixFormat img.PixFormat TextureParams.empty, 1, 1)
        t.Upload(img)
        t

    member x.Tools = tools

    member x.Compile(t : Term<V2i>) =
        let used = Term.names t
        let call = Term.toReflectedCall Term.Read.image t
        let shader = runtime.CreateComputeShader (ConjugateGradientShaders.polynomial2d<'f, 'v> call rreal.toV4)

        fun (inputs : Map<string, ITextureSubResource>) (dst : ITextureSubResource) ->
            use input = runtime.CreateInputBinding shader

            input.["res"] <- dst

            for u in used do
                let r = inputs.[u]
                input.[u] <- r
                input.[sprintf "%sLevel" u] <- 0
                input.[sprintf "%sSize" u] <- r.Size.XY


            input.Flush()

            runtime.Run [
                ComputeCommand.Bind shader
                ComputeCommand.SetInput input
                ComputeCommand.Dispatch(ceilDiv2 dst.Size.XY shader.LocalSize.XY)
            ]



    member x.NegativeDerivative(h : float, inputs : Map<string, ITextureSubResource>, dst : ITextureSubResource) =
        use input = runtime.CreateInputBinding negativeDerivative
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
        use input = runtime.CreateInputBinding derivative
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
        use input = runtime.CreateInputBinding secondMulD
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


    member internal this.SolveInternal(h : float, inputs : Map<string, ITextureSubResource>, x : ITextureSubResource, cfg : ConjugateGradientConfig) =
        let size = x.Size.XY
        let n = size.X * size.Y
        
        use __ = runtime.CreateInputBinding secondMulD

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

        let eps = cfg.stepTolerance
        let eps2Delta0 = real.mul (real.fromFloat (eps * eps)) delta0
        let eps2 = real.fromFloat (eps * eps)

        let absEps = cfg.gradientTolerance * cfg.gradientTolerance |> real.fromFloat

        while i < cfg.maxIterations && real.isGreater deltaNew eps2Delta0 && real.isGreater deltaNew absEps do
            j <- 0

            // deltaD <- <d|d>
            deltaD <- tools.LengthSquared d

            //this.Residual(inputs, temp.[TextureAspect.Color, 0, 0])
            //let sumSq = tools.Sum(temp.[TextureAspect.Color, 0, 0])
            //printfn "res^2: %A (%A)" sumSq deltaD


            let alphaTest() =
                let alpha2DeltaD = real.mul (real.pow alpha 2) deltaD
                real.isGreater alpha2DeltaD eps2

            doWhile (fun () -> j < cfg.maxLineSearchIterations && alphaTest())  (fun () -> 
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




    member this.Solve(inputs : Map<string, ITextureSubResource>, x : ITextureSubResource, cfg : ConjugateGradientConfig) =
        let r = runtime.CreateTexture2D(x.Size.XY, format, 1, 1)
        let d = runtime.CreateTexture2D(x.Size.XY, format, 1, 1)
        let temp = runtime.CreateTexture2D(x.Size.XY, format, 1, 1)

        try
            let inputs = 
                inputs
                |> Map.add "__r" r.[TextureAspect.Color, 0, 0]
                |> Map.add "__d" d.[TextureAspect.Color, 0, 0]
                |> Map.add "__temp" temp.[TextureAspect.Color, 0, 0]
            this.SolveInternal(1.0, inputs, x, cfg)
        finally 
            runtime.DeleteTexture r
            runtime.DeleteTexture d
            runtime.DeleteTexture temp



    member this.Solve(inputs : Map<string, PixImage>, x : PixImage, cfg : ConjugateGradientConfig) =
        let inputs = inputs |> Map.map (fun _ img -> (createTexture img).[TextureAspect.Color, 0, 0])
        let x = createTexture x

        this.Solve(inputs, x.[TextureAspect.Color, 0, 0], cfg)

        let res = runtime.Download(x, 0, 0)
        runtime.DeleteTexture x
        inputs |> Map.iter (fun _ t -> runtime.DeleteTexture t.Texture)
        res

type MultigridConfig =
    {
        gradientTolerance       : float
        stepTolerance           : float
        cycles                  : int
        smoothIterations        : int
        solveIterations         : int
        correctIterations       : int
        useGuess                : bool
        maxSolveSize            : V2i
        debugPath               : Option<string>
    }

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
        LookupTable.lookup [
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
    
    let parts, cg = 
        let mutable parts = Map.empty
        
        let res (x : Term.TermParameter2d) (h : Term<V2i>) =
            let res = residual x h
            let r = Term.derivative "x" V2i.Zero res
            
            let rec allIsolations (b : Term<_>) =
                match b with
                    | Zero ->
                        []
                    | _ -> 
                        let (f, bf) = Term.isolate "h" b
                        
                        match f with
                            | One -> 
                                let (d, c) = Term.factorize "h" b
                                let hasX = Set.contains "x" (Term.names d) && Set.contains "x" (Term.names c)
                                if hasX then
                                    match c, d with
                                        | Zero, _ | _, Zero -> 
                                            [(Power_(Uniform "h", Value 0.0), b)]
                                        | _ -> 
                                            (Power_(Uniform "h", Value 0.0), c) :: allIsolations d
                                else
                                    [(Power_(Uniform "h", Value 0.0), b)]
                            | f -> 
                                allIsolations bf |> List.map (fun (fi,b) ->
                                    Term.simplify (f * fi), b
                                )

            let residuals = 
                allIsolations r |> List.map (fun (f, ex) ->
                    match f with
                        | Power_(Uniform "h", Value e) when Fun.IsTiny (Fun.Frac e) ->
                            -int e, ex

                        | _ ->
                            failwith "sadasda"
                )

            let overall =
                residuals |> List.sumBy (fun (e, ex) ->
                    let (d, c) = Term.factorize "x" ex

                    let name = sprintf "b_%d" (int e)
                    parts <- Map.add name (e, d, c) parts

                    let parameter = Term.TermParameter2d(name)
                    
                    (d + parameter.[0,0]) * (Uniform "h") ** -e
                )

            let f = Term.simplify overall
            Log.warn "0 = %s" (Term.toString f)

            f

        let cg = ConjugateGradientSolver2d<'f, 'v>(runtime, res)


        let final = 
            parts |> Map.map (fun name (e, d,c) ->
                Log.warn "b_%d[0,0]  = %s" e (Term.toString c)
                Log.warn "Ax_%d[0,0] = %s" e (Term.toString d)
                (e,d,c)
            )
            
        final, cg
        
    let uconstantPart =
        parts 
        |> Map.toSeq
        |> Seq.map (fun (name,(e,d,c)) -> Term.parameterNames c)
        |> Set.unionMany
        
    let udependentPart =
        parts 
        |> Map.toSeq
        |> Seq.map (fun (name,(e,d,c)) -> Set.union (Term.parameterNames d) (Term.parameterNames c))
        |> Set.unionMany
        


    let computeResiduals0 = 
        let rreal = ReflectedReal.instance<'v>
        parts |> Map.map (fun name (_, _, c) ->
            runtime.CreateComputeShader(ConjugateGradientShaders.polynomial2d<'f, 'v> (Term.toReflectedCall Term.Read.image c) rreal.toV4)
        )
        
    let computeResiduals = 
        let rreal = ReflectedReal.instance<'v>
        parts |> Map.map (fun name (e, d, c) ->
            let t = d + c |> Term.simplify
            runtime.CreateComputeShader(ConjugateGradientShaders.polynomial2d<'f, 'v> (Term.toReflectedCall Term.Read.image t) rreal.toV4)
        )
        
    let computeResidualsForB = 
        let rreal = ReflectedReal.instance<'v>
        parts |> Map.map (fun name (e, d, c) ->
            let c = Parameter(name, V2i.Zero) //* (Uniform "h" ** -e)
            let t = d + c |> Term.simplify
            runtime.CreateComputeShader(ConjugateGradientShaders.polynomial2d<'f, 'v> (Term.toReflectedCall Term.Read.image t) rreal.toV4)
        )

    let createTexture (img : PixImage) =
        let t = runtime.CreateTexture2D(img.Size, TextureFormat.ofPixFormat img.PixFormat TextureParams.empty, 1, 1)
        t.Upload(img)
        t

    member x.Compile(term : Term<V2i>) =
        cg.Compile(term)

    member x.ComputeResidualsZero(h : float, inputs : Map<string, ITextureSubResource>, dst : Map<string, ITextureSubResource>) =
        computeResiduals0 |> Map.iter (fun e computeResidual0 ->
            match Map.tryFind e dst with
                | Some dst ->
                    use input = runtime.CreateInputBinding computeResidual0
                    input.["res"] <- dst
                    input.["h"] <- real.fromFloat h

                    for used in uconstantPart do
                        let r = inputs.[used]
                        input.[used] <- r
                        input.[sprintf "%sLevel" used] <- 0
                        input.[sprintf "%sSize" used] <- r.Size.XY
            
                    input.Flush()

                    runtime.Run [
                        ComputeCommand.Bind computeResidual0
                        ComputeCommand.SetInput input
                        ComputeCommand.Dispatch(ceilDiv2 dst.Size.XY computeResidual0.LocalSize.XY)
                    ]
                | None ->
                    ()
        )

    member x.ComputeResiduals(h : float, inputs : Map<string, ITextureSubResource>, dst : Map<string, ITextureSubResource>) =
        computeResiduals |> Map.iter (fun e computeResidual ->
            match Map.tryFind e dst with
                | Some dst ->
                    use input = runtime.CreateInputBinding computeResidual
                    input.["res"] <- dst
                    input.["h"] <- real.fromFloat h
                    

                    for used in udependentPart do
                        let r = inputs.[used]
                        input.[used] <- r
                        input.[sprintf "%sLevel" used] <- 0
                        input.[sprintf "%sSize" used] <- r.Size.XY
            
                    input.Flush()

                    runtime.Run [
                        ComputeCommand.Bind computeResidual
                        ComputeCommand.SetInput input
                        ComputeCommand.Dispatch(ceilDiv2 dst.Size.XY computeResidual.LocalSize.XY)
                    ]
                | None ->
                    ()
        )

    member x.ComputeResidualsForB(h : float, inputs : Map<string, ITextureSubResource>, dst : Map<string, ITextureSubResource>) =
        computeResidualsForB |> Map.iter (fun e computeResidual ->
            match Map.tryFind e dst with
                | Some dst ->
                    use input = runtime.CreateInputBinding computeResidual
                    input.["res"] <- dst
                    input.["h"] <- real.fromFloat h
                    

                    for used in udependentPart do
                        let r = inputs.[used]
                        input.[used] <- r
                        input.[sprintf "%sLevel" used] <- 0
                        input.[sprintf "%sSize" used] <- r.Size.XY
            
                    for (used,_) in Map.toSeq parts do
                        let r = inputs.[used]
                        input.[used] <- r
                        input.[sprintf "%sLevel" used] <- 0
                        input.[sprintf "%sSize" used] <- r.Size.XY

                    input.Flush()

                    runtime.Run [
                        ComputeCommand.Bind computeResidual
                        ComputeCommand.SetInput input
                        ComputeCommand.Dispatch(ceilDiv2 dst.Size.XY computeResidual.LocalSize.XY)
                    ]
                | None ->
                    ()
        )


    member x.Restrict(src : ITextureSubResource, srcWeight : ITextureSubResource, dst : ITextureSubResource, dstWeight : ITextureSubResource, temp : ITextureSubResource) =
        use mulInput = runtime.CreateInputBinding mul
        mulInput.["l"] <- src
        mulInput.["r"] <- srcWeight
        mulInput.["dst"] <- temp
        mulInput.Flush()

        use input = runtime.CreateInputBinding restrictWeight
        input.["src"] <- src
        input.["weight"] <- srcWeight
        input.["weightTimesSrc"] <- temp
        input.["dst"] <- dst
        input.["dstWeight"] <- dstWeight
        input.Flush()
        runtime.Run [
            
            ComputeCommand.Bind mul
            ComputeCommand.SetInput mulInput
            ComputeCommand.Dispatch (ceilDiv2 src.Size.XY mul.LocalSize.XY)
            ComputeCommand.Sync temp.Texture

            ComputeCommand.Bind restrictWeight
            ComputeCommand.SetInput input
            ComputeCommand.Dispatch (ceilDiv2 dst.Size.XY restrictWeight.LocalSize.XY)
        ]

    member x.Restrict(src : ITextureSubResource, dst : ITextureSubResource, factor : float) =
        use input = runtime.CreateInputBinding restrict
        input.["src"] <- src
        input.["dst"] <- dst
        input.["factor"] <- factor
        input.Flush()

        runtime.Run [
            ComputeCommand.Bind restrict
            ComputeCommand.SetInput input
            ComputeCommand.Dispatch (ceilDiv2 dst.Size.XY restrict.LocalSize.XY)
        ]

    member x.Interpolate(src : ITextureSubResource, dst : ITextureSubResource, factor : float) =
        use input = runtime.CreateInputBinding interpolate
        input.["src"] <- src
        input.["dst"] <- dst
        input.["factor"] <- V4d.IIII * factor
        input.Flush()

        runtime.Run [
            ComputeCommand.Bind interpolate
            ComputeCommand.SetInput input
            ComputeCommand.Dispatch (ceilDiv2 dst.Size.XY interpolate.LocalSize.XY)
        ]

    member x.Divergence(src : ITextureSubResource, dst : ITextureSubResource) =
        use input = runtime.CreateInputBinding divergence
        input.["src"] <- src
        input.["dst"] <- dst
        input.Flush()

        runtime.Run [
            ComputeCommand.Bind divergence
            ComputeCommand.SetInput input
            ComputeCommand.Dispatch (ceilDiv2 dst.Size.XY divergence.LocalSize.XY)
        ]

    member x.NextLevel(m : Map<string, ITextureSubResource>) =
        m |> Map.map (fun name img ->
            img.Texture.[img.Aspect, img.Level + 1, img.Slice]
        )
        
    member x.GetLevel(m : Map<string, ITextureSubResource>, l : int) =
        m |> Map.map (fun name img ->
            img.Texture.[img.Aspect, l, img.Slice]
        )
        

    member x.Restrict (m : Map<string, ITextureSubResource>) =
        m |> Map.map (fun name img ->
            let next = img.Texture.[img.Aspect, img.Level + 1, img.Slice]
            if not (name.StartsWith "__") then
                //if name.StartsWith "b_" then
                //    let e = name.Substring(2) |> int
                //    x.Restrict(img, next, 1.0 / float (1 <<< e))

                if name.StartsWith "w_" then
                    if not (Map.containsKey (name.Substring(2)) m) then
                        x.Restrict(img, next, 1.0)
                        
                else
                    let wname = "w_" + name
                    match Map.tryFind wname m with
                        | None ->
                            x.Restrict(img, next, 1.0)
                        | Some wimg ->
                            let wnext = wimg.Texture.[wimg.Aspect, wimg.Level + 1, wimg.Slice]
                            let temp = m.["__r"]
                            x.Restrict(img, wimg, next, wnext, temp)
            next
        )

    member x.Download(t : ITextureSubResource) =
        let dst = PixImage.Create(TextureFormat.toDownloadFormat t.Texture.Format, int64 t.Size.X, int64 t.Size.Y)
        t.Texture.Download(dst, t.Level, t.Slice)
        dst

    member private x.VCycle(inputs : Map<string, ITextureSubResource>, bPing : Map<string, ITextureSubResource>, bPong : Map<string, ITextureSubResource> , iter : int, level : int, size : V2i, inputSize : V2i, cfg : MultigridConfig) =
        let hv = V2d inputSize / V2d size
        let h = 0.5 * (hv.X + hv.Y)
        
               
        let sol = inputs.["x"]
        cg.Tools.Set(sol, V4d.Zero)

        match cfg.debugPath with
            | Some path -> 
                let needed = Set.remove "x" udependentPart
                inputs |> Map.iter (fun name t ->
                    if not (name.StartsWith "__") && Set.contains name needed then
                        let dst = PixImage<float32>(Col.Format.RGBA, t.Size.XY)
                        t.Texture.Download(dst, t.Level, t.Slice)
                        if name.StartsWith "b_" then
                            dst.GetMatrix<C4f>().SetMap(dst.GetMatrix<C4f>(), fun v -> ((v.ToV4f() + V4f.IIII) * 0.5f).ToC4f()) |> ignore
                        
                        let name = sprintf @"%d_%s_input_%dx%d.jpg" iter name size.X size.Y
                        dst.Save (Path.combine [path; name])
                )

                bPing |> Map.iter (fun name t ->
                    let dst = PixImage<float32>(Col.Format.RGBA, t.Size.XY)
                    t.Texture.Download(dst, t.Level, t.Slice)
                    dst.GetMatrix<C4f>().SetMap(dst.GetMatrix<C4f>(), fun v -> ((v.ToV4f() + V4f.IIII) * 0.5f).ToC4f()) |> ignore
                    let name = sprintf @"%d_%s_ping_%dx%d.jpg" iter name size.X size.Y
                    dst.Save (Path.combine [path; name]) 
                )
            | None -> 
                ()
         
        let cgConfig =
            {
                maxIterations = 0
                maxLineSearchIterations = 1
                gradientTolerance = cfg.gradientTolerance
                stepTolerance = cfg.stepTolerance
            }


        if size.AnyGreater cfg.maxSolveSize then
            let temp = inputs.["__temp"]
            
            let bPingHalf, bPongHalf = 
                if cfg.smoothIterations > 0 then
                    cg.SolveInternal(h, Map.union inputs bPing, sol, { cgConfig with maxIterations = cfg.smoothIterations })
                
                    // evacuate the current solution to temp
                    runtime.Copy(sol, V3i.Zero, temp, V3i.Zero, sol.Size)
                
                    // recompute residuals to bPong
                    x.ComputeResidualsForB(h, Map.union inputs bPing, bPong)

                    //match cfg.debugPath with
                    //    | Some path -> 
                    //        bPong |> Map.iter (fun name t ->
                    //            let dst = PixImage<float32>(Col.Format.RGBA, t.Size.XY)
                    //            runtime.Download(t.Texture, t.Level, t.Slice, dst)
                    //            dst.GetMatrix<C4f>().SetMap(dst.GetMatrix<C4f>(), fun v -> ((v.ToV4f() + V4f.IIII) * 0.5f).ToC4f()) |> ignore
                    //            let name = sprintf @"%d_%s_pong_%dx%d.jpg" iter name size.X size.Y
                    //            dst.SaveAsImage (Path.combine [path; name]) 
                    //        )
                    //    | _ ->
                    //        ()

                    // swap bPing and bPong
                    x.Restrict(bPong), x.NextLevel(bPing)
                else
                    x.Restrict(bPing), x.NextLevel(bPong)

    
            let inputsHalf = x.NextLevel(inputs)
            
            let half = V2i(max 1 (size.X / 2), max 1 (size.Y / 2))
            x.VCycle(inputsHalf, bPingHalf, bPongHalf, iter, level + 1, half, inputSize, cfg)

            x.Interpolate(inputsHalf.["x"], sol, 1.0)
           
            if cfg.smoothIterations > 0 then
                cg.Tools.MultiplyAdd(temp, real.one, sol, real.one)

            if cfg.correctIterations > 0 then
                cg.SolveInternal(h, Map.union inputs bPing, sol, { cgConfig with maxIterations = cfg.correctIterations })
            
           
        else
            if cfg.solveIterations > 0 then
                cg.SolveInternal(h, Map.union inputs bPing, inputs.["x"], { cgConfig with maxIterations = cfg.solveIterations })

        match cfg.debugPath with
            | Some path -> 
                let name = "x"
                let t = inputs.[name]
                let dst = PixImage<float32>(Col.Format.RGBA, t.Size.XY)
                t.Texture.Download(dst, t.Level, t.Slice)

                dst.GetMatrix<C4f>().SetMap(dst.GetMatrix<C4f>(), fun v -> ((v.ToV4f() + V4f.IIII) * 0.5f).ToC4f()) |> ignore

                let name = sprintf @"%d_%s_output_%dx%d.jpg" iter name size.X size.Y
                dst.Save (Path.combine [path; name])
            | None ->
                ()
            

    member this.CreateTexture(size : V2i) =
        let res = runtime.CreateTexture2D(size, format, 1, 1)
        res
          

    member this.CreateTempTexture(size : V2i) =
        let levels = 1 + int(Fun.Floor(Fun.Log2 (max size.X size.Y)))
        let res = runtime.CreateTexture2D(size, format, levels, 1)
        res

    member this.CreateTexture(img : PixImage) =
        let res = runtime.CreateTexture2D(img.Size, TextureFormat.ofPixFormat img.PixFormat TextureParams.empty, 1, 1)
        res.Upload(img)
        res
        

    member this.CreateTempTexture(img : PixImage) =
        let levels = 1 + int(Fun.Floor(Fun.Log2 (max img.Size.X img.Size.Y)))
        let res = runtime.CreateTexture2D(img.Size, TextureFormat.ofPixFormat img.PixFormat TextureParams.empty, levels, 1)
        res.Upload(img)
        res

    member x.Tools = cg.Tools

    member this.Solve(inputs : Map<string, ITextureSubResource>, sum : ITextureSubResource, cfg : MultigridConfig) =
        use __ = runtime.CreateInputBinding restrict

        let size = sum.Size
        let levels = 1 + int(Fun.Floor(Fun.Log2 (max sum.Size.X sum.Size.Y)))
        
        let ip =
            inputs |> Map.map (fun name i ->
                if i.Texture.MipMapLevels >= levels then
                    i
                else
                    let res = runtime.CreateTexture2D(size.XY, format, levels, 1)
                    runtime.Copy(i, V3i.Zero, res.[TextureAspect.Color, 0, 0], V3i.Zero, size)
                    res.[TextureAspect.Color, 0, 0]
            )

        let bPing = 
            parts |> Map.map (fun name (e,_,_) ->
                let t = runtime.CreateTexture2D(size.XY, format, levels, 1)
                t.[TextureAspect.Color, 0, 0]
            )

        let bPong =
            if cfg.smoothIterations > 0 then
                parts |> Map.map (fun name (e,_,_) ->
                    let t = runtime.CreateTexture2D(size.XY, format, levels, 1)
                    t.[TextureAspect.Color, 0, 0]
                )
            else
                Map.empty
            
        let r,deleteR = 
            match Map.tryFind "__r" inputs with
                | Some r -> r.Texture, false
                | None ->
                    let r = runtime.CreateTexture2D(size.XY, format, levels, 1)
                    r, true

        let d,deleteD = 
            match Map.tryFind "__d" inputs with
                | Some d -> d.Texture, false
                | None ->
                    let d = runtime.CreateTexture2D(size.XY, format, levels, 1)
                    d, true

        let temp,deleteTemp = 
            match Map.tryFind "__temp" inputs with
                | Some temp -> temp.Texture, false
                | None ->
                    let temp = runtime.CreateTexture2D(size.XY, format, levels, 1)
                    temp, true

        let x = runtime.CreateTexture2D(size.XY, format, levels, 1)
        let xs = runtime.CreateTexture2D(size.XY, format, levels, 1)

        let ip =
            ip
            |> Map.add "x" x.[TextureAspect.Color, 0, 0]
            |> Map.add "__r" r.[TextureAspect.Color, 0, 0]
            |> Map.add "__d" d.[TextureAspect.Color, 0, 0]
            |> Map.add "__temp" temp.[TextureAspect.Color, 0, 0]

        //if not cfg.useGuess then 
        //    cg.Tools.Set(sum, V4d.Zero)
        //    this.ComputeResidualsZero(1.0, Map.union ip bPing, bPing)

        do // restrict all inputs
            let mutable v = ip
            for l in 1 .. levels - 1 do 
                v <- this.Restrict(v)
                cg.Tools.Set(xs.[TextureAspect.Color, l, 0], V4d.Zero)

            
        let solveLevels = 
            let lx = Fun.Log2 (float sum.Size.X / float cfg.maxSolveSize.X)
            let ly = Fun.Log2 (float sum.Size.Y / float cfg.maxSolveSize.Y)
            1 + int(Fun.Floor(max lx ly))
            
        let mutable iter = 0
        for height in 0 .. solveLevels - 1 do
            let startLevel = solveLevels - 1 - height
            let xsl = xs.[TextureAspect.Color, startLevel, 0]
            
            let i = this.GetLevel(ip, startLevel)
            let b0 = this.GetLevel(bPing, startLevel)
            let b1 = this.GetLevel(bPong, startLevel)
            let s = size / (1 <<< startLevel)
            let hv = V2d size / V2d s
            let h = 0.5 * (hv.X + hv.Y)

            match cfg.debugPath with
                | Some path -> 
                    let name = sprintf "%d_xsl.jpg" iter
                    let dst = PixImage<float32>(Col.Format.RGBA, xsl.Size.XY)
                    xsl.Texture.Download(dst, xsl.Level, xsl.Slice)
                    dst.Save(Path.combine [path; name])
                | None ->
                    ()

            this.ComputeResiduals(h, Map.add "x" xsl i, b0)

            this.VCycle(i, b0, b1, iter, startLevel, s.XY, size.XY, cfg)
            
            let x = i.["x"]
            cg.Tools.MultiplyAdd(x, real.one, xsl, real.one)

            if startLevel > 0 then
                let r = this.GetLevel(ip, startLevel - 1)
                this.Interpolate(xsl, xsl.Texture.[TextureAspect.Color, startLevel - 1, 0], 1.0)
            iter <- iter + 1

        let xsl = xs.[TextureAspect.Color, 0, 0]
        for i in 0 .. cfg.cycles - 2 do
            this.ComputeResiduals(1.0, Map.add "x" xsl ip, bPing)
            this.VCycle(ip, bPing, bPong, iter, 0, size.XY, size.XY, cfg)
            cg.Tools.MultiplyAdd(ip.["x"], real.one, xsl, real.one)
            iter <- iter + 1

        runtime.Copy(xsl, V3i.Zero, sum, V3i.Zero, sum.Size)
        //for i in 0 .. cfg.cycles - 1 do
        //    if i <> 0 || cfg.useGuess then 
        //        this.ComputeResiduals(1.0, Map.add "x" sum ip, bPing)
                
        //    this.VCycle(ip, bPing, bPong, i, 0, size.XY, size.XY, cfg)

        //    cg.Tools.MultiplyAdd(x.[TextureAspect.Color, 0, 0], real.one, sum, real.one)

            
        runtime.DeleteTexture x
        runtime.DeleteTexture xs
        if deleteR then runtime.DeleteTexture r
        if deleteD then runtime.DeleteTexture d
        if deleteTemp then runtime.DeleteTexture temp
        bPing |> Map.iter (fun _ t -> runtime.DeleteTexture t.Texture)
        bPong |> Map.iter (fun _ t -> runtime.DeleteTexture t.Texture)
        for (k,t) in Map.toSeq ip do
            match Map.tryFind k inputs with
                | Some it -> if t.Texture <> it.Texture then runtime.DeleteTexture t.Texture
                | _ -> ()
                
    

    member this.Solve(inputs : Map<string, PixImage>, x : PixImage, cfg : MultigridConfig) =
        let inputs = inputs |> Map.map (fun _ img -> (createTexture img).[TextureAspect.Color, 0, 0])
        let x = createTexture x

        this.Solve(inputs, x.[TextureAspect.Color, 0, 0], cfg)

        let res = runtime.Download(x, 0, 0)
        runtime.DeleteTexture x
        inputs |> Map.iter (fun _ t -> runtime.DeleteTexture t.Texture)
        res








[<AutoOpen>]
module PixImageExtensionsNew =
    
    let acc : PixFormat -> int64 -> obj =
        let div255 (b : byte) = float b / 255.0
        let v255 (r : byte) (g : byte) (b : byte) (a : byte) = V4d(div255 r, div255 g, div255 b, div255 a)
        let vf (r : float32) (g : float32) (b : float32) (a : float32) = V4d(r, g, b, a)

        let b255 (v : float) = clamp 0.0 1.0 v * 255.0 |> byte

        LookupTable.lookup [
            PixFormat.ByteBGR, fun dz -> 
                TensorAccessors<byte, V4d>(
                    Getter = (fun a i -> v255 (a.[int (i + 2L * dz)]) (a.[int (i + 1L * dz)]) (a.[int (i + 0L * dz)]) 255uy),
                    Setter = (fun a i v -> a.[int (i + 2L * dz)] <- b255 v.Z; a.[int (i + 1L * dz)] <- b255 v.Y; a.[int (i + 0L * dz)] <- b255 v.X)
                ) :> obj

            PixFormat.ByteBGRA, fun dz -> 
                TensorAccessors<byte, V4d>(
                    Getter = (fun a i -> v255 (a.[int (i + 2L * dz)]) (a.[int (i + 1L * dz)]) (a.[int (i + 0L * dz)]) (a.[int (i + 3L * dz)])),
                    Setter = (fun a i v -> a.[int (i + 2L * dz)] <- b255 v.Z; a.[int (i + 1L * dz)] <- b255 v.Y; a.[int (i + 0L * dz)] <- b255 v.X; a.[int (i + 3L * dz)] <- b255 v.W)
                ) :> obj

            PixFormat.ByteGray, fun dz -> 
                TensorAccessors<byte, V4d>(
                    Getter = (fun a i -> v255 (a.[int i]) (a.[int i]) (a.[int i]) 255uy),
                    Setter = (fun a i v -> a.[int i] <- v.ToC4f().ToC4b().ToGrayByte())
                ) :> obj
                
            PixFormat.ByteRGB, fun dz -> 
                TensorAccessors<byte, V4d>(
                    Getter = (fun a i -> v255 (a.[int (i + 0L * dz)]) (a.[int (i + 1L * dz)]) (a.[int (i + 2L * dz)]) 255uy),
                    Setter = (fun a i v -> a.[int (i + 0L * dz)] <- b255 v.Z; a.[int (i + 1L * dz)] <- b255 v.Y; a.[int (i + 2L * dz)] <- b255 v.X)
                ) :> obj

            PixFormat.ByteRGBA, fun dz -> 
                TensorAccessors<byte, V4d>(
                    Getter = (fun a i -> v255 (a.[int (i + 0L * dz)]) (a.[int (i + 1L * dz)]) (a.[int (i + 2L * dz)]) (a.[int (i + 3L * dz)])),
                    Setter = (fun a i v -> a.[int (i + 0L * dz)] <- b255 v.Z; a.[int (i + 1L * dz)] <- b255 v.Y; a.[int (i + 2L * dz)] <- b255 v.X; a.[int (i + 3L * dz)] <- b255 v.W)
                ) :> obj

                
            PixFormat.FloatBGR, fun dz -> 
                TensorAccessors<float32, V4d>(
                    Getter = (fun a i -> vf (a.[int (i + 2L * dz)]) (a.[int (i + 1L * dz)]) (a.[int (i + 0L * dz)]) 1.0f),
                    Setter = (fun a i v -> a.[int (i + 2L * dz)] <- float32 v.Z; a.[int (i + 1L * dz)] <- float32 v.Y; a.[int (i + 0L * dz)] <- float32 v.X)
                ) :> obj

            PixFormat.FloatBGRA, fun dz -> 
                TensorAccessors<float32, V4d>(
                    Getter = (fun a i -> vf (a.[int (i + 2L * dz)]) (a.[int (i + 1L * dz)]) (a.[int (i + 0L * dz)]) (a.[int (i + 3L * dz)])),
                    Setter = (fun a i v -> a.[int (i + 2L * dz)] <- float32 v.Z; a.[int (i + 1L * dz)] <- float32 v.Y; a.[int (i + 0L * dz)] <- float32 v.X; a.[int (i + 3L * dz)] <- float32 v.W)
                ) :> obj

            PixFormat.FloatGray, fun dz -> 
                TensorAccessors<float32, V4d>(
                    Getter = (fun a i -> vf (a.[int i]) (a.[int i]) (a.[int i]) 1.0f),
                    Setter = (fun a i v -> a.[int i] <- v.ToC4f().ToGrayFloat())
                ) :> obj
                
            PixFormat.FloatRGB, fun dz -> 
                TensorAccessors<float32, V4d>(
                    Getter = (fun a i -> vf (a.[int (i + 0L * dz)]) (a.[int (i + 1L * dz)]) (a.[int (i + 2L * dz)]) 1.0f),
                    Setter = (fun a i v -> a.[int (i + 0L * dz)] <- float32 v.Z; a.[int (i + 1L * dz)] <- float32 v.Y; a.[int (i + 2L * dz)] <- float32 v.X)
                ) :> obj

            PixFormat.FloatRGBA, fun dz -> 
                TensorAccessors<float32, V4d>(
                    Getter = (fun a i -> vf (a.[int (i + 0L * dz)]) (a.[int (i + 1L * dz)]) (a.[int (i + 2L * dz)]) (a.[int (i + 3L * dz)])),
                    Setter = (fun a i v -> a.[int (i + 0L * dz)] <- float32 v.Z; a.[int (i + 1L * dz)] <- float32 v.Y; a.[int (i + 2L * dz)] <- float32 v.X; a.[int (i + 3L * dz)] <- float32 v.W)
                ) :> obj
        ]

    module PixImage =
        let toMatrix (img : PixImage<'a>) =
            let acc = acc img.PixFormat img.Volume.DZ |> unbox<TensorAccessors<'a, V4d>>
            let mutable mat = img.Volume.SubXYMatrixWindow<V4d>(0L)
            mat.Accessors <- acc
            mat

        let map (mapping : V4d -> V4d) (img : PixImage) =
            img.Visit {
                new IPixImageVisitor<PixImage> with
                    member x.Visit(img : PixImage<'a>) =
                        let res = PixImage<'a>(img.Format, img.Size)
                        let sm = toMatrix img
                        let dm = toMatrix res
                        dm.SetMap(sm, mapping) |> ignore
                        res :> PixImage
            }

        let range (img : PixImage) =
            let merge (ll : V4d, lh : V4d) (rl : V4d, rh : V4d) =
                let low = 
                    V4d(
                        min ll.X rl.X,
                        min ll.Y rl.Y,
                        min ll.Z rl.Z,
                        min ll.W rl.W
                    )
                let high = 
                    V4d(
                        max lh.X rh.X,
                        max lh.Y rh.Y,
                        max lh.Z rh.Z,
                        max lh.W rh.W
                    )
                low, high

            img.Visit {
                new IPixImageVisitor<V4d * V4d> with
                    member x.Visit(img : PixImage<'a>) =
                        let sm = toMatrix img
                        sm.InnerProduct(sm, (fun l _ -> (l,l)), (V4d.PositiveInfinity, V4d.NegativeInfinity), merge)
            }
    
        let toRGBAByteImage (img : PixImage) =
            img.Visit {
                new IPixImageVisitor<PixImage<byte>> with
                    member x.Visit(img : PixImage<'a>) =
                        if typeof<'a> = typeof<byte> && img.Format = Col.Format.RGBA then 
                            unbox<PixImage<byte>> img
                        else
                            let res = PixImage<byte>(Col.Format.RGBA, img.Size)
                            let sm = toMatrix img
                            let dm = toMatrix res
                            dm.SetMap(sm, id) |> ignore
                            res

            }
            
        let toGrayImage<'a> (img : PixImage) =
            img.Visit {
                new IPixImageVisitor<PixImage<'a>> with
                    member x.Visit(img : PixImage<'b>) =
                        if typeof<'a> = typeof<'b> && img.Format = Col.Format.Gray then 
                            unbox<PixImage<'a>> img
                        else
                            let res = PixImage<'a>(Col.Format.Gray, img.Size)
                            let sm = toMatrix img
                            let dm = toMatrix res
                            dm.SetMap(sm, id) |> ignore
                            res

            }
            
        let toRGBAImage<'a> (img : PixImage) =
            img.Visit {
                new IPixImageVisitor<PixImage<'a>> with
                    member x.Visit(img : PixImage<'b>) =
                        if typeof<'a> = typeof<'b> && img.Format = Col.Format.RGBA then 
                            unbox<PixImage<'a>> img
                        else
                            let res = PixImage<'a>(Col.Format.RGBA, img.Size)
                            let sm = toMatrix img
                            let dm = toMatrix res
                            dm.SetMap(sm, id) |> ignore
                            res

            }
            
        let normalize (img : PixImage) =
            let min, max = range img
            let mutable size = max - min
            if size.X < 1.0 then size.X <- 1.0
            if size.Y < 1.0 then size.Y <- 1.0
            if size.Z < 1.0 then size.Z <- 1.0
            if size.W < 1.0 then size.W <- 1.0

            let mapping (v : V4d) = (v - min) / size
            map mapping img

        let save (path : string) (img : PixImage) =
            img.Save path

type DepthMapSolver(runtime : IRuntime, lambda : float, sigma : float) =

    static let w_cx = Term.TermParameter2d("w_cx")
    static let w_cy = Term.TermParameter2d("w_cy")
    static let w_v = Term.TermParameter2d("w_v")
    static let v = Term.TermParameter2d("v")

    let residual (x : Term.TermParameter2d) (h : Term<V2i>) =
        let cx = (2.0 * x.[0,0] - x.[-1,0] - x.[1,0]) / h**2
        let cy = (2.0 * x.[0,0] - x.[0,-1] - x.[0,1]) / h**2
        
        Term.Value lambda * (w_cx.[0,0] * cx ** 2 + w_cy.[0,0] * cy ** 2) +
        w_v.[0,0] * (x.[0,0] - v.[0,0]) ** 2
        
    let solver = new MultigridSolver2d<FShade.Formats.r32f, float32>(runtime, residual)
    
    let computeWCX = 
        solver.Compile (
            let cx = 2.0 * v.[0,0] - v.[-1,0] - v.[1,0]
            exp (-abs cx / sigma)
        )
    let computeWCY = 
        solver.Compile (
            let cy = 2.0 * v.[0,0] - v.[0,-1] - v.[0,-1]
            exp (-abs cy / sigma)
        )

    member x.Solve(v : ITextureSubResource, w_v : ITextureSubResource, gray : ITextureSubResource, res : ITextureSubResource, cfg : MultigridConfig) =
        let size = v.Size.XY
        let w_cx = solver.CreateTempTexture size
        let w_cy = solver.CreateTempTexture size

        try
            let inputs = Map.ofList ["v", gray]
            computeWCX inputs w_cx.[TextureAspect.Color, 0, 0]
            computeWCY inputs w_cy.[TextureAspect.Color, 0, 0]
            
            match cfg.debugPath with
                | Some path ->
                    let wcx = Path.combine [path; "w_cx.png"]
                    let wcy = Path.combine [path; "w_cy.png"]
                    
                    solver.Download(w_cx.[TextureAspect.Color, 0, 0]) |> PixImage.normalize |> PixImage.toRGBAByteImage |> PixImage.save wcx
                    solver.Download(w_cy.[TextureAspect.Color, 0, 0]) |> PixImage.normalize |> PixImage.toRGBAByteImage |> PixImage.save wcy

                | None ->
                    ()


            let textures =
                Map.ofList [
                    "v", v
                    "w_v", w_v
                    "w_cx", w_cx.[TextureAspect.Color, 0, 0]
                    "w_cy", w_cy.[TextureAspect.Color, 0, 0]
                ]

            solver.Solve(textures, res, cfg)
        finally
            runtime.DeleteTexture w_cx
            runtime.DeleteTexture w_cy

    member x.Solve(v : PixImage, w_v : PixImage, image : PixImage, cfg : MultigridConfig) =
        let v = PixImage.toGrayImage<float32> v
        let w_v = PixImage.toGrayImage<float32> w_v
        let image = PixImage.toGrayImage<float32> image

        let tv = solver.CreateTempTexture v
        let tw_v = solver.CreateTempTexture w_v
        let timage = solver.CreateTempTexture image
        let res = solver.CreateTexture v.Size

        try
            x.Solve(tv.[TextureAspect.Color, 0, 0], tw_v.[TextureAspect.Color, 0, 0], timage.[TextureAspect.Color, 0, 0], res.[TextureAspect.Color, 0, 0], cfg)
            let img = PixImage<float32>(Col.Format.Gray, v.Size)
            res.Download(img)
            img
        finally
            runtime.DeleteTexture tv
            runtime.DeleteTexture tw_v
            runtime.DeleteTexture timage
            runtime.DeleteTexture res

type ImageResonstructionSolver(runtime : IRuntime) =

    static let div = Term.TermParameter2d("div")
    static let w_v = Term.TermParameter2d("w_v")
    static let v = Term.TermParameter2d("v")

    let residual (x : Term.TermParameter2d) (h : Term<V2i>) =
        let cx = (2.0 * x.[0,0] - x.[-1,0] - x.[1,0]) / h**2
        let cy = (2.0 * x.[0,0] - x.[0,-1] - x.[0,1]) / h**2
        
        (cx + cy - div.[0,0]) ** 2 +
        w_v.[0,0] * (x.[0,0] - v.[0,0]) ** 2
        
    let solver = new MultigridSolver2d<FShade.Formats.rgba32f, V4f>(runtime, residual)
    
    let computeDiv = 
        solver.Compile (
            4.0 * v.[0,0] - v.[-1,0] - v.[1,0] - v.[0,-1] - v.[0,1]
        )

    member x.Solve(input : ITextureSubResource, weights : ITextureSubResource, res : ITextureSubResource, cfg : MultigridConfig) =
        let size = input.Size.XY
        let div = solver.CreateTempTexture size
        try
            computeDiv (Map.ofList ["v", input]) div.[TextureAspect.Color, 0, 0]
            
            let textures =
                Map.ofList [
                    "v", input
                    "w_v", weights
                    "div", div.[TextureAspect.Color, 0, 0]
                ]

            solver.Solve(textures, res, cfg)

        finally
            runtime.DeleteTexture div

    member x.Solve(input : PixImage, weights : PixImage, cfg : MultigridConfig) =
        let input = PixImage.toRGBAImage<float32> input
        let weights = PixImage.toRGBAImage<float32> weights

        let tinput = solver.CreateTempTexture input
        let tweight = solver.CreateTempTexture weights
        let res = solver.CreateTexture input.Size

        try
            x.Solve(tinput.[TextureAspect.Color, 0, 0], tweight.[TextureAspect.Color, 0, 0], res.[TextureAspect.Color, 0, 0], cfg)
            let dst = PixImage<float32>(Col.Format.RGBA, input.Size)
            res.Download(dst)
            PixImage.toRGBAByteImage dst
        finally
            runtime.DeleteTexture tinput
            runtime.DeleteTexture tweight
            runtime.DeleteTexture res


let depth (runtime : IRuntime) =
    let color = PixImage.Load @"C:\temp\b\P9094511.png"
    let depthValues = PixImage.Load @"C:\temp\b\P9094511.exr"
    let depthWeight = depthValues |> PixImage.map (fun v -> if v.X > 0.0 then V4d.IIII else V4d.Zero)
    let solver = DepthMapSolver(runtime, 10.0, 0.1)


    let config =
        {
            cycles = 5
            maxSolveSize = V2i(4, 4)
            stepTolerance = 1E-5
            gradientTolerance = 1E-10
            smoothIterations = 6
            solveIterations = 32
            correctIterations = 6
            useGuess = false
            debugPath = Some @"C:\temp\b\debug"
        }

        

    let res = solver.Solve(depthValues, depthWeight, color, config)
    res.Save @"C:\temp\b\depth.exr"

    let n = PixImage.normalize res |> PixImage.toRGBAByteImage
    n.Save @"C:\temp\b\depth.png"

let reconstruct (runtime : IRuntime) =

    let color = PixImage.Load @"C:\temp\a\bla.png"
    let size = color.Size
    let edgeSize = 2L
    let isXEdge (v : V2l) = v.X < edgeSize || v.X >= int64 size.X - edgeSize
    let isYEdge (v : V2l) = v.Y < edgeSize  || v.Y >= int64 size.Y - edgeSize
    let isCorner (v : V2l) = isXEdge v && isYEdge v
    let isEdge (v : V2l) = isXEdge v || isYEdge v

    let weight = PixImage<float32>(Col.Format.RGBA, size)
    weight.GetMatrix<C4f>().SetByCoord(fun c -> if isEdge c then C4f(1.0,1.0,1.0,1.0) else C4f(0.0,0.0,0.0,0.0)) |> ignore

    let solver = ImageResonstructionSolver(runtime)


    let config =
        {
            cycles = 1
            maxSolveSize = V2i(4, 4)
            stepTolerance = 1E-5
            gradientTolerance = 1E-10
            smoothIterations = 6
            solveIterations = 32
            correctIterations = 6
            useGuess = false
            debugPath = Some @"C:\temp\a\debug"
        }

        

    let res = solver.Solve(color, weight, config)
    res.Save @"C:\temp\a\result.png"

[<EntryPoint>]
let main argv = 
    use app = new HeadlessVulkanApplication()
    //app.Runtime.ShaderCachePath <- None
    let runtime = app.Runtime :> IRuntime
    
    reconstruct runtime

    0
