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

    [<LocalSize(X = 8, Y = 8)>]
    let mad2d<'c, 'f, 'fmt when 'fmt :> Formats.IFloatingFormat> (mul : Expr<V4d -> 'f -> 'c>) (add : Expr<'c -> 'c -> V4d>) (srcFactor : 'f) (dstFactor : 'f) (dst : Image2d<'fmt>) =
        compute {
            let id = getGlobalId().XY
            let s = dst.Size

            if id.X < s.X && id.Y < s.Y then
                dst.[id] <- (%add) ((%mul) dst.[id] dstFactor) ((%mul) srcSampler.[id, uniform?srcLevel] srcFactor)
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
    
    let tools = new TensorTools<'v>(runtime)
    
    let epoly = Polynomial.toReflectedCall Polynomial.Read.image residual
    let upoly = Polynomial.parameters residual |> HMap.toList |> List.map fst

    let poly' = 
        residual.Derivative("x", V2i.Zero)
        //let coords = residual.FreeParameters.["x"]
        //let polys' = 
        //    coords |> Seq.map (fun c -> 
        //        let newPoly = residual.Rename(fun i -> i - c).Derivative("x", V2i.Zero)
            
        //        if c = V2i.Zero then newPoly
        //        else newPoly.WithoutConstant("x")
        //    ) |> Seq.sum

        //printfn "%A" polys'
        //polys'

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
    let mad = 
        let ofV4 = rreal.fromV4
        let toV4 = rreal.toV4
        let mul = rreal.mul
        let add = rreal.add

        let mulV4 = <@ fun (l : V4d) (r : 'v) -> (%mul) ((%ofV4) l) r @>
        let addV4 = <@ fun (l : 'v) (r : 'v) -> (%toV4) ((%add) l r) @>
        runtime.CreateComputeShader (ConjugateGradientShaders.mad2d<'v, 'v, 'f> mulV4 addV4)

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

    member x.MultiplyAdd(src : ITextureSubResource, srcFactor : 'v, dst : ITextureSubResource, dstFactor : 'v) =
        use input = runtime.NewInputBinding mad
        input.["src"] <- src.Texture
        input.["srcLevel"] <- src.Level
        input.["dst"] <- dst
        input.["srcFactor"] <- srcFactor
        input.["dstFactor"] <- dstFactor
        input.Flush()

        runtime.Run [
            ComputeCommand.Bind mad
            ComputeCommand.SetInput input
            ComputeCommand.Dispatch (ceilDiv2 dst.Size.XY mad.LocalSize.XY)
        ]

    member this.Solve(inputs : Map<string, ITextureSubResource>, x : ITextureSubResource, eps : float, imax : int, jmax : int, n : int) =
        let size = x.Size.XY
        
        use __ = runtime.NewInputBinding mad

        let mutable i = 0
        let mutable j = 0
        let mutable k = 0

        
        let inputs = Map.add "x" x inputs 
        let r = runtime.CreateTexture(size, format, 1, 1)
        let d = runtime.CreateTexture(size, format, 1, 1)
        let temp = runtime.CreateTexture(size, format, 1, 1)


        // r <- -f'(x)
        this.NegativeDerivative(inputs, r.[TextureAspect.Color, 0, 0])

        // d <- r
        runtime.Copy(r.[TextureAspect.Color, 0, 0], d.[TextureAspect.Color, 0, 0])
        
        
        let mutable deltaOld = real.zero
        let mutable deltaNew = tools.LengthSquared r.[TextureAspect.Color, 0, 0]
        let delta0 = deltaNew

        let mutable deltaD = deltaNew
        let mutable alpha = real.zero

        let eps2Delta0 = real.mul (real.fromFloat (eps * eps)) delta0
        let eps2 = real.fromFloat (eps * eps)

        while i < imax && real.isPositive (real.sub deltaNew eps2Delta0) do
            j <- 0

            // deltaD <- <d|d>
            deltaD <- tools.LengthSquared d.[TextureAspect.Color, 0, 0]

            //this.Residual(inputs, temp.[TextureAspect.Color, 0, 0])
            //let sumSq = tools.Sum(temp.[TextureAspect.Color, 0, 0])
            //printfn "res^2: %A (%A)" sumSq deltaD


            let alphaTest() =
                let alpha2DeltaD = real.mul (real.pow alpha 2) deltaD
                real.sub alpha2DeltaD eps2 |> real.isPositive

            doWhile (fun () -> j < jmax && alphaTest())  (fun () -> 
                

                // a <- <f'(x) | d >
                this.Derivative(inputs, temp.[TextureAspect.Color, 0, 0])
                let a = tools.Dot(temp.[TextureAspect.Color, 0, 0], d.[TextureAspect.Color, 0, 0])
                
                // b <- <d | f''(x) * d >
                this.SecondMulD(inputs, d.[TextureAspect.Color, 0, 0], temp.[TextureAspect.Color, 0, 0])
                let b = tools.Dot(temp.[TextureAspect.Color, 0, 0], d.[TextureAspect.Color, 0, 0])

                // alpha <- -a / b
                let alpha = real.neg (real.div a b) //(dot d (f'' x d))

                // x <- x + alpha*d
                this.MultiplyAdd(d.[TextureAspect.Color, 0, 0], alpha, x, real.one)
                
                j <- j + 1
            )
            
            // r <- -f'(x)
            this.NegativeDerivative(inputs, r.[TextureAspect.Color, 0, 0])
            deltaOld <- deltaNew
            deltaNew <- tools.LengthSquared r.[TextureAspect.Color, 0, 0]
            let beta = real.div deltaNew deltaOld

            // d <- r + beta * d
            this.MultiplyAdd(r.[TextureAspect.Color, 0, 0], real.one, d.[TextureAspect.Color, 0, 0], beta)

            let rd = tools.Dot(r.[TextureAspect.Color, 0, 0], d.[TextureAspect.Color, 0, 0])
            k <- k + 1
            if k = n || real.neg rd |> real.isPositive then
                runtime.Copy(r.[TextureAspect.Color, 0, 0], d.[TextureAspect.Color, 0, 0])
                k <- 0

            i <- i + 1


        printfn "iter: %d" i
        runtime.DeleteTexture r
        runtime.DeleteTexture d
        runtime.DeleteTexture temp
   
    member this.Solve(inputs : Map<string, PixImage>, x : PixImage, eps : float, imax : int, jmax : int, n : int) =
        let inputs = inputs |> Map.map (fun _ img -> (createTexture img).[TextureAspect.Color, 0, 0])
        let x = createTexture x

        this.Solve(inputs, x.[TextureAspect.Color, 0, 0], eps, imax, jmax, n)

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
    //let test = 0.25 * ((-x<float>.[-1] + 2.0 * x<float>.[0] - x<float>.[1] - 20.0) ** 2)
    



    //let a = test.WithoutConstant("x")
    //let b = -test.ConstantPart("x")
    
    //printfn "%A = 0" test
    //printfn "%A = %A" a b
    //printfn "%A = %A" (a.Derivative("x", 0)) (b.Derivative("x", 0))
    //printfn "%A = %A" (a.Derivative("x", 1)) (b.Derivative("x", 1))

    //let final = 
    //    //let test = test.Derivative("x", 0)
    //    let coords = test.FreeParameters.["x"]
    //    let s = coords |> Seq.sumBy (fun c -> test.Rename(fun i -> i - c))
    //    s.Derivative("x", 0)
    //printfn "%A" final
    
    //Environment.Exit 0


    use app = new HeadlessVulkanApplication()
    //app.Runtime.ShaderCachePath <- None
    let runtime = app.Runtime :> IRuntime


    let div = PolynomialParam<float32>("div")
    let value = PolynomialParam<float32>("value")
    let weight = PolynomialParam<float32>("weight")

    let poly = 0.125f * (4.0f * x<float32>.[0,0] - x.[-1,0] - x.[1,0] - x.[0,-1] - x.[0,1] - div.[0,0]) ** 2 + weight.[0,0] * (x<float32>.[0,0] - value.[0,0]) ** 2
    let solver = ConjugateGradientSolver2d<FShade.Formats.r32f, float32>(runtime, poly)

    let size = V2i(7,7)
    let edgeSize = 1L
    let isXEdge (v : V2l) = v.X < edgeSize || v.X >= int64 size.X - edgeSize
    let isYEdge (v : V2l) = v.Y < edgeSize  || v.Y >= int64 size.Y - edgeSize
    let isCorner (v : V2l) = isXEdge v && isYEdge v
    let isEdge (v : V2l) = isXEdge v || isYEdge v

    let x = PixImage<float32>(Col.Format.Gray, size)
    x.GetChannel(Col.Channel.Gray).SetByCoord (fun (c : V2l) -> 0.0f) |> ignore
    
    let value = PixImage<float32>(Col.Format.Gray, size)
    value.GetChannel(Col.Channel.Gray).SetByCoord (fun (c : V2l) -> 0.0f) |> ignore
    
    let div = PixImage<float32>(Col.Format.Gray, size)
    div.GetChannel(Col.Channel.Gray).SetByCoord (fun (c : V2l) -> 
        if isEdge c then 0.0f 
        else 4.0f
    ) |> ignore
    
    let weight = PixImage<float32>(Col.Format.Gray, size)
    weight.GetChannel(0L).SetByCoord (fun (c : V2l) -> 
        if isCorner c then 1000.0f 
        elif isEdge c then 50.0f 
        else 0.0f
    ) |> ignore

    let inputs =
        Map.ofList [
            "div", div :> PixImage
            "value", value :> PixImage
            "weight", weight :> PixImage
            
        ]

    let mutable x = x :> PixImage
    for i in 0 .. 10 do
        x <- solver.Solve(inputs, x, 1E-3, 5, 1, 10000000)
        
        let c = x.ToPixImage<float32>().GetChannel(Col.Channel.Gray)
        printMat "x" c
        
    x <- solver.Solve(inputs, x, 1E-3, 20, 1, 10000000)
    let c = x.ToPixImage<float32>().GetChannel(Col.Channel.Gray)
    printMat "x" c
        
        
    x <- solver.Solve(inputs, x, 1E-3, 20, 1, 10000000)
    let c = x.ToPixImage<float32>().GetChannel(Col.Channel.Gray)
    printMat "x" c

    //let poly : Polynomial<_, float> =
    //    0.125 * (4.0 * x<float>.[0,0] - x.[-1,0] - x.[1,0] - x.[0,-1] - x.[0,1] - w<float>.[0,0]) ** 2
    //    |> Polynomial.derivative "x" V2i.Zero

    //let code = Polynomial.toCCode Polynomial.Read.image poly

    //printfn "%s" code



    0
