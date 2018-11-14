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
            filter Filter.MinMagPoint
        }
    let weightSampler =
        sampler2d {
            texture uniform?weight
            addressU WrapMode.Border
            addressV WrapMode.Border
            borderColor (C4f(0.0f, 0.0f, 0.0f, 0.0f))
            filter Filter.MinMagPoint
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
            let s = dst.Size
            let srcSize = srcSampler.Size

            if id.X < s.X && id.Y < s.Y then
                let tc = (V2d(id) + V2d.Half) / V2d(s)
                let d = V2d.Half / V2d srcSize

                let v = 
                    srcSampler.SampleLevel(tc + V2d( d.X, 0.0 ), 0.0) +
                    srcSampler.SampleLevel(tc + V2d(-d.X, 0.0 ), 0.0) +
                    srcSampler.SampleLevel(tc + V2d( 0.0, d.Y), 0.0) +
                    srcSampler.SampleLevel(tc + V2d( 0.0,-d.Y), 0.0)

                dst.[id] <- V4d.Zero
        }

    [<LocalSize(X = 8, Y = 8)>]
    let mul2d<'fmt when 'fmt :> Formats.IFloatingFormat> (l : Image2d<'fmt>) (r : Image2d<'fmt>) (dst : Image2d<'fmt>) (dstWeight : Image2d<'fmt>) =
        compute {
            let id = getGlobalId().XY
            let dstSize = dst.Size
            if id.X < dstSize.X && id.Y < dstSize.Y then
                dst.[id] <- l.[id] * r.[id]
        }

    [<LocalSize(X = 8, Y = 8)>]
    let restrictWeight<'fmt when 'fmt :> Formats.IFloatingFormat> (dst : Image2d<'fmt>) (dstWeight : Image2d<'fmt>) =
        compute {
            let id = getGlobalId().XY
            let dstSize = dst.Size
            let srcSize = srcSampler.Size

            if id.X < dstSize.X && id.Y < dstSize.Y then
                let tc = (V2d(id) + V2d.Half) / V2d(dstSize)
                let d = V2d.II / V2d srcSize
                

                let fid = tc * V2d srcSize - V2d.Half
                let srcId = V2i(floor fid.X, floor fid.Y)
                let frac = fid - V2d srcId

                let w00 = weightSampler.SampleLevel((V2d srcId + V2d(0.5, 0.5)) / V2d srcSize, 0.0).X
                let w01 = weightSampler.SampleLevel((V2d srcId + V2d(0.5, 1.5)) / V2d srcSize, 0.0).X
                let w10 = weightSampler.SampleLevel((V2d srcId + V2d(1.5, 0.5)) / V2d srcSize, 0.0).X
                let w11 = weightSampler.SampleLevel((V2d srcId + V2d(1.5, 1.5)) / V2d srcSize, 0.0).X
                
                let v00 = srcSampler.SampleLevel((V2d srcId + V2d(0.5, 0.5)) / V2d srcSize, 0.0)
                let v01 = srcSampler.SampleLevel((V2d srcId + V2d(0.5, 1.5)) / V2d srcSize, 0.0)
                let v10 = srcSampler.SampleLevel((V2d srcId + V2d(1.5, 0.5)) / V2d srcSize, 0.0)
                let v11 = srcSampler.SampleLevel((V2d srcId + V2d(1.5, 1.5)) / V2d srcSize, 0.0)
             

                let weightSum = w00 + w01 + w10 + w11

                let value = 
                    if weightSum < 0.00001 then V4d.Zero
                    else (v00 * w00 + v01 * w01 + v10 * w10 + v11 * w11) / weightSum

                let avgWeight = weightSum / 4.0

                //let wnn = weightSampler.SampleLevel(tc + V2d(-d.X, -d.Y), 0.0).X
                //let w0n = weightSampler.SampleLevel(tc + V2d( 0.0, -d.Y), 0.0).X
                //let wpn = weightSampler.SampleLevel(tc + V2d( d.X, -d.Y), 0.0).X
                //let wn0 = weightSampler.SampleLevel(tc + V2d(-d.X,  0.0), 0.0).X
                //let w00 = weightSampler.SampleLevel(tc, 0.0).X
                //let wp0 = weightSampler.SampleLevel(tc + V2d( d.X,  0.0), 0.0).X
                //let wnp = weightSampler.SampleLevel(tc + V2d(-d.X,  d.Y), 0.0).X
                //let w0p = weightSampler.SampleLevel(tc + V2d( 0.0,  d.Y), 0.0).X
                //let wpp = weightSampler.SampleLevel(tc + V2d( d.X,  d.Y), 0.0).X
                
                //let vnn = weightTimesSrcSampler.SampleLevel(tc + V2d(-d.X, -d.Y), 0.0)
                //let v0n = weightTimesSrcSampler.SampleLevel(tc + V2d( 0.0, -d.Y), 0.0)
                //let vpn = weightTimesSrcSampler.SampleLevel(tc + V2d( d.X, -d.Y), 0.0)
                //let vn0 = weightTimesSrcSampler.SampleLevel(tc + V2d(-d.X,  0.0), 0.0)
                //let v00 = weightTimesSrcSampler.SampleLevel(tc, 0.0)
                //let vp0 = weightTimesSrcSampler.SampleLevel(tc + V2d( d.X,  0.0), 0.0)
                //let vnp = weightTimesSrcSampler.SampleLevel(tc + V2d(-d.X,  d.Y), 0.0)
                //let v0p = weightTimesSrcSampler.SampleLevel(tc + V2d( 0.0,  d.Y), 0.0)
                //let vpp = weightTimesSrcSampler.SampleLevel(tc + V2d( d.X,  d.Y), 0.0)

                //let weight = 
                //    0.25 * wnn + 0.50 * w0n + 0.25 * wpn + 
                //    0.50 * wn0 + 1.00 * w00 + 0.50 * wp0 + 
                //    0.25 * wnp + 0.50 * w0p + 0.25 * wpp

                //let value =
                //    0.25 * vnn + 0.50 * v0n + 0.25 * vpn + 
                //    0.50 * vn0 + 1.00 * v00 + 0.50 * vp0 + 
                //    0.25 * vnp + 0.50 * v0p + 0.25 * vpp

                //let weight = weightSampler.SampleLevel(tc, 0.0).X
                //let value = weightTimesSrcSampler.SampleLevel(tc, 0.0)

                //let value = 
                //    if weight < 0.001 then V4d.Zero
                //    else value / weight
                    
                dst.[id] <- value
                dstWeight.[id] <- V4d.IIII * avgWeight
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

type ConjugateGradientSolver2d<'f, 'v when 'v : unmanaged and 'f :> FShade.Formats.IFloatingFormat> (runtime : IRuntime, residual : Polynomial<V2i, 'v>) =
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
    
    let epoly = Polynomial.toReflectedCall Polynomial.Read.image residual
    let upoly = Polynomial.parameters residual |> HMap.toList |> List.map fst

    let poly' = residual.Derivative("x", V2i.Zero)
    let epoly' = Polynomial.toReflectedCall Polynomial.Read.image poly'
    let upoly' = Polynomial.parameters poly' |> HMap.toList |> List.map fst

    let poly'' =
        let d = PolynomialParam<'v>("d")
        
        let mutable sum = Polynomial<V2i, 'v>.Zero
        let all = poly'.AllDerivatives("x")
        for (c, p) in HMap.toSeq all do
            sum <- sum + p * d.[c.X, c.Y]
        sum

    let epoly'' = Polynomial.toReflectedCall Polynomial.Read.image poly''
    let upoly'' = Polynomial.parameters poly'' |> HMap.toList |> List.map fst


    let negativeV4 = 
        let toV4 = rreal.toV4
        let neg = rreal.neg
        <@ fun v -> (%toV4) ((%neg) v) @>
        
    let residual = runtime.CreateComputeShader (ConjugateGradientShaders.polynomial2d<'f, 'v> epoly rreal.toV4)
    let negativeDerivative = runtime.CreateComputeShader (ConjugateGradientShaders.polynomial2d<'f, 'v> epoly' negativeV4)
    let derivative = runtime.CreateComputeShader (ConjugateGradientShaders.polynomial2d<'f, 'v> epoly' rreal.toV4)
    let secondMulD = runtime.CreateComputeShader (ConjugateGradientShaders.polynomial2d<'f, 'v> epoly'' rreal.toV4)
    
    let createTexture (img : PixImage) =
        let t = runtime.CreateTexture(img.Size, TextureFormat.ofPixFormat img.PixFormat TextureParams.empty, 1, 1)
        runtime.Upload(t, 0, 0, img)
        t

   
    member x.Residual(inputs : Map<string, ITextureSubResource>, dst : ITextureSubResource) =
        use input = runtime.NewInputBinding residual
        input.["res"] <- dst

        for used in upoly do
            let r = inputs.[used]
            input.[used] <- r.Texture
            input.[sprintf "%sLevel" used] <- r.Level
            input.[sprintf "%sSize" used] <- r.Size.XY
            
        input.Flush()

        runtime.Run [
            ComputeCommand.Bind residual
            ComputeCommand.SetInput input
            ComputeCommand.Dispatch(ceilDiv2 dst.Size.XY residual.LocalSize.XY)
        ]

    member x.NegativeDerivative(inputs : Map<string, ITextureSubResource>, dst : ITextureSubResource) =
        use input = runtime.NewInputBinding negativeDerivative
        input.["res"] <- dst

        for used in upoly' do
            let r = inputs.[used]
            input.[used] <- r.Texture
            input.[sprintf "%sLevel" used] <- r.Level
            input.[sprintf "%sSize" used] <- r.Size.XY
            
        input.Flush()

        runtime.Run [
            ComputeCommand.Bind negativeDerivative
            ComputeCommand.SetInput input
            ComputeCommand.Dispatch(ceilDiv2 dst.Size.XY negativeDerivative.LocalSize.XY)
        ]

    member x.Derivative(inputs : Map<string, ITextureSubResource>, dst : ITextureSubResource) =
        use input = runtime.NewInputBinding derivative
        input.["res"] <- dst

        for used in upoly' do
            let r = inputs.[used]
            input.[used] <- r.Texture
            input.[sprintf "%sLevel" used] <- r.Level
            input.[sprintf "%sSize" used] <- r.Size.XY
            
        input.Flush()

        runtime.Run [
            ComputeCommand.Bind derivative
            ComputeCommand.SetInput input
            ComputeCommand.Dispatch(ceilDiv2 dst.Size.XY negativeDerivative.LocalSize.XY)
        ]

    member x.SecondMulD(inputs : Map<string, ITextureSubResource>, d : ITextureSubResource, dst : ITextureSubResource) =
        use input = runtime.NewInputBinding secondMulD
        input.["res"] <- dst

        input.["d"] <- d.Texture
        input.["dLevel"] <- d.Level
        input.["dSize"] <- d.Size.XY

        for used in upoly' do
            let r = inputs.[used]
            input.[used] <- r.Texture
            input.[sprintf "%sLevel" used] <- r.Level
            input.[sprintf "%sSize" used] <- r.Size.XY
            
        input.Flush()

        runtime.Run [
            ComputeCommand.Bind secondMulD
            ComputeCommand.SetInput input
            ComputeCommand.Dispatch(ceilDiv2 dst.Size.XY negativeDerivative.LocalSize.XY)
        ]


    member internal this.SolveInternal(inputs : Map<string, ITextureSubResource>, x : ITextureSubResource, eps : float, imax : int, jmax : int) =
        let size = x.Size.XY
        let n = size.X * size.Y
        
        use __ = runtime.NewInputBinding residual

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
        this.NegativeDerivative(inputs, r)

        // d <- r
        runtime.Copy(r, d)
        
        
        let mutable deltaOld = real.zero
        let mutable deltaNew = tools.LengthSquared r
        let delta0 = deltaNew

        let mutable deltaD = deltaNew
        let mutable alpha = real.zero

        let eps2Delta0 = real.mul (real.fromFloat (eps * eps)) delta0
        let eps2 = real.fromFloat (eps * eps)

        while i < imax && real.isPositive (real.sub deltaNew eps2Delta0) do
            j <- 0

            // deltaD <- <d|d>
            deltaD <- tools.LengthSquared d

            //this.Residual(inputs, temp.[TextureAspect.Color, 0, 0])
            //let sumSq = tools.Sum(temp.[TextureAspect.Color, 0, 0])
            //printfn "res^2: %A (%A)" sumSq deltaD


            let alphaTest() =
                let alpha2DeltaD = real.mul (real.pow alpha 2) deltaD
                real.sub alpha2DeltaD eps2 |> real.isPositive

            doWhile (fun () -> j < jmax && alphaTest())  (fun () -> 
                

                // a <- <f'(x) | d >
                this.Derivative(inputs, temp)
                let a = tools.Dot(temp, d)
                
                // b <- <d | f''(x) * d >
                this.SecondMulD(inputs, d, temp)
                let b = tools.Dot(temp, d)

                // alpha <- -a / b
                let alpha = real.neg (real.div a b) //(dot d (f'' x d))

                // x <- x + alpha*d
                tools.MultiplyAdd(d, alpha, x, real.one)
                
                j <- j + 1
            )
            
            // r <- -f'(x)
            this.NegativeDerivative(inputs, r)
            deltaOld <- deltaNew
            deltaNew <- tools.LengthSquared r
            let beta = real.div deltaNew deltaOld

            // d <- r + beta * d
            tools.MultiplyAdd(r, real.one, d, beta)

            let rd = tools.Dot(r, d)
            k <- k + 1
            if k = n || real.neg rd |> real.isPositive then
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
            this.SolveInternal(inputs, x, eps, imax, jmax)
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


type MultigridSolver2d<'f, 'v when 'v : unmanaged and 'f :> FShade.Formats.IFloatingFormat> (runtime : IRuntime, residual : 'v -> Polynomial<V2i, 'v>) = 
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
    let cgs = 
        Array.init 20 (fun l ->
            let s = 2.0 ** float l
            lazy (ConjugateGradientSolver2d<'f, 'v>(runtime, residual (real.fromFloat s)))
        )
    let createTexture (img : PixImage) =
        let t = runtime.CreateTexture(img.Size, TextureFormat.ofPixFormat img.PixFormat TextureParams.empty, 1, 1)
        runtime.Upload(t, 0, 0, img)
        t

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
        let cg = cgs.[level].Value
        if size.AnyGreater 4 then
            if ibefore > 0 then
                cg.SolveInternal(inputs, inputs.["x"], eps, ibefore, 1)

            let down = x.Restrict inputs
            let r = down.["v"]
            runtime.Download(r.Texture, r.Level, r.Slice).SaveAsImage (sprintf @"C:\temp\a\v%dx%d.jpg" r.Size.X r.Size.Y)
            let r = down.["w_v"]
            runtime.Download(r.Texture, r.Level, r.Slice).SaveAsImage (sprintf @"C:\temp\a\w%dx%d.jpg" r.Size.X r.Size.Y)
            
            let half = V2i(max 1 (size.X / 2), max 1 (size.Y / 2))
            
            x.Cycle(down, level + 1, half, ibefore, iafter, ileaf, eps)

            x.Interpolate(down.["x"], inputs.["x"])
            
            let r = inputs.["x"]
            runtime.Download(r.Texture, r.Level, r.Slice).SaveAsImage (sprintf @"C:\temp\a\u%dx%d.jpg" r.Size.X r.Size.Y)

            if iafter > 0 then
                cg.SolveInternal(inputs, inputs.["x"], eps, iafter, 1)
            
           
        else
            if ileaf > 0 then
                cg.SolveInternal(inputs, inputs.["x"], eps, ileaf, 1)

        let r = inputs.["x"]
        runtime.Download(r.Texture, r.Level, r.Slice).SaveAsImage (sprintf @"C:\temp\a\f%dx%d.jpg" r.Size.X r.Size.Y)
 
    member this.Solve(inputs : Map<string, ITextureSubResource>, x : ITextureSubResource, n : int, eps : float) =
        use __ = runtime.NewInputBinding restrict

        let size = x.Size
        let levels = 1 + int(Fun.Floor(Fun.Log2 (max x.Size.X x.Size.Y)))
        
        let v = runtime.CreateTexture(size.XY, format, levels, 1)
        runtime.Copy(x, V3i.Zero, v.[TextureAspect.Color, 0, 0], V3i.Zero, size)

        let inputs =
            inputs |> Map.map (fun name i ->
                let res = runtime.CreateTexture(size.XY, format, levels, 1)
                runtime.Copy(i, V3i.Zero, res.[TextureAspect.Color, 0, 0], V3i.Zero, size)
                res.[TextureAspect.Color, 0, 0]
            )
            
        let r = runtime.CreateTexture(size.XY, format, levels, 1)
        let d = runtime.CreateTexture(size.XY, format, levels, 1)
        let temp = runtime.CreateTexture(size.XY, format, levels, 1)
        

        let inputs =
            inputs
            |> Map.add "x" v.[TextureAspect.Color, 0, 0]
            |> Map.add "__r" r.[TextureAspect.Color, 0, 0]
            |> Map.add "__d" d.[TextureAspect.Color, 0, 0]
            |> Map.add "__temp" temp.[TextureAspect.Color, 0, 0]

        for i in 1 .. n do
            this.Cycle(inputs, 0, size.XY, 8, 8, 30, eps)

        runtime.Copy(v.[TextureAspect.Color, 0, 0], V3i.Zero, x, V3i.Zero, size)

        inputs |> Map.iter (fun _ t -> runtime.DeleteTexture t.Texture)
        
    member this.Solve(inputs : Map<string, PixImage>, x : PixImage, n : int, eps : float) =
        let inputs = inputs |> Map.map (fun _ img -> (createTexture img).[TextureAspect.Color, 0, 0])
        let x = createTexture x

        this.Solve(inputs, x.[TextureAspect.Color, 0, 0], n, eps)

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


    //use app = new HeadlessVulkanApplication()
    ////app.Runtime.ShaderCachePath <- None
    //let runtime = app.Runtime :> IRuntime


    //let div = PolynomialParam<V4f>("div")
    //let v = PolynomialParam<V4f>("v")
    //let w_v = PolynomialParam<V4f>("w_v")


    //// 1*x^2 + 0*y^2


    //// (x - a)^2 + (x - b)^2 + 2(x-a)(x-b)
    //// ((x-a) + (x-b))^2

    //// f(x) = (A1x - b1)^2 + (A2x - b2)^2 + ....


    //// df/dx = 2A1T(A1x - b1) + 2A2T(A2x - b2) + ....

    //// A1T*A1x + A2T*A2x - (A1T*b1 + A2T*b2) = 0
    //// (A1T*A1 + A2T*A2)x - (A1T*b1 + A2T*b2) = f'(x) = 0


    
    //let polya (h : V4f) = 
    //    V4f(50.0f) * (((V4f.IIII * 4.0f) * x<V4f>.[0,0] - x.[-1,0] - x.[1,0] - x.[0,-1] - x.[0,1]) * (1.0f / (h*h))) ** 2 + 
    //    w_v.[0,0] * (x<V4f>.[0,0] - v.[0,0]) ** 2

    ////let poly = 0.125f * (4.0f * x<float32>.[0,0] - x.[-1,0] - x.[1,0] - x.[0,-1] - x.[0,1]) ** 2 + w_v.[0,0] * (x<float32>.[0,0] - v.[0,0]) ** 2

    ////let test = poly.Rename(fun v -> 2 * v)

    //let solver = MultigridSolver2d<FShade.Formats.rgba32f, V4f>(runtime, polya)

    //let size = V2i(4096,4096)
    //let edgeSize = 1L
    //let isXEdge (v : V2l) = v.X < edgeSize || v.X >= int64 size.X - edgeSize
    //let isYEdge (v : V2l) = v.Y < edgeSize  || v.Y >= int64 size.Y - edgeSize
    //let isCorner (v : V2l) = isXEdge v && isYEdge v
    //let isEdge (v : V2l) = isXEdge v || isYEdge v


    //let c = V2l(size) / 2L
    //let isCenter (v : V2l) =
    //    v = c || v = c - V2l.IO || v = c - V2l.OI || v = c - V2l.II


    //let x = PixImage<float32>(Col.Format.RGBA, size)
    //x.GetMatrix<C4f>().SetByCoord (fun (c : V2l) -> C4f(0.0f)) |> ignore
    
    ////let div = PixImage<float32>(Col.Format.Gray, size)
    ////div.GetChannel(Col.Channel.Gray).SetByCoord (fun (c : V2l) -> 
    ////    0.0f
    ////) |> ignore
    
    //let cache = Dict<int64, C4f>()
    //let rand = RandomSystem()
    //let getColor (c : V2l) =
    //    cache.GetOrCreate(c.X, fun _ -> rand.UniformC3f().ToC4f())

    //let v = PixImage<float32>(Col.Format.RGBA, size)
    //v.GetMatrix<C4f>().SetByCoord (fun (c : V2l) -> 
    //    let c = c / 128L
    //    if (c.X) % 4L = 0L then getColor c
    //    else C4f(0.0f)
    //) |> ignore
    
    //let w_v = PixImage<float32>(Col.Format.RGBA, size)
    //w_v.GetMatrix<C4f>().SetByCoord (fun (c : V2l) -> 
    //    let c = c / 128L
    //    if (c.X) % 4L = 0L then C4f(1.0f)
    //    else C4f(0.0f,0.0f,0.0f,0.0f)
    //) |> ignore

    //let inputs =
    //    Map.ofList [
    //        //"div", div :> PixImage
    //        "v", v :> PixImage
    //        "w_v", w_v :> PixImage
            
    //    ]

    //let mutable x = x :> PixImage
    //x <- solver.Solve(inputs, x, 1, 1E-3)
    ////let c = x.ToPixImage<float32>().GetChannel(Col.Channel.Gray)
    ////printMat "x" c

    //x.SaveAsImage @"C:\temp\a\z_result.jpg"

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

    let term : Term<V2i> =
        let a (x : int) (y : int) = Term.parameter "x" (V2i(x,y))
        let h = Term.uniform "h"

        0.125 * ((4.0 * a 0 0 - a -1 0 - a 1 0 - a 0 -1 - a 0 1) / (h ** 2)) ** 2
        |> Term.derivative "x" V2i.Zero
        //|> Term.derivative "x" V2i.Zero

    let code = Term.toCCode Term.Read.image term
    printfn "%s" code



    0
