namespace ConjugateGradient

open Microsoft.FSharp.Quotations
open Aardvark.Base
open Aardvark.Base.Rendering
open System.Runtime.InteropServices
open System.Runtime.CompilerServices

module TensorToolShaders =
    open FShade

    [<Literal>]
    let foldSize = 128
    
    [<Literal>]
    let halfFoldSize = 64

    let lSampler =
        sampler2d {
            texture uniform?l
            addressU WrapMode.Border
            addressV WrapMode.Border
            filter Filter.MinMagLinear
        }

    let rSampler =
        sampler2d {
            texture uniform?r
            addressU WrapMode.Border
            addressV WrapMode.Border
            filter Filter.MinMagLinear
        }


    [<LocalSize(X = halfFoldSize)>]
    let fold1d (zero : Expr<'b>) (add : Expr<'b -> 'b -> 'b>) (cnt : int) (arr : 'b[]) (result : 'b[]) =
        compute {
            let mem = allocateShared<'b> foldSize
            let tid = getLocalId().X
            let gid = getWorkGroupId().X
            
            // index calculations
            let lai = 2 * tid
            let lbi = lai + 1
            let ai  = foldSize * gid + lai
            let bi  = ai + 1 
            
            // load existing values into local memory
            mem.[lai] <- if ai < cnt then arr.[ai] else %zero
            mem.[lbi] <- if bi < cnt then arr.[bi] else %zero
            barrier()

            // sum the local values from right to left
            let mutable s = halfFoldSize
            while s > 0 do
                if tid < s then
                    mem.[tid] <- (%add) mem.[tid] mem.[tid + s]
                s <- s >>> 1
                barrier()

            // store the overall sum in the result-buffer
            if tid = 0 then
                result.[gid] <- mem.[0]

        }

    [<LocalSize(X = halfFoldSize)>]
    let dot1d (zero : Expr<'b>) (mul : Expr<'a -> 'a -> 'b>) (add : Expr<'b -> 'b -> 'b>) (cnt : int) (l : 'a[]) (r : 'a[]) (result : 'b[]) =
        compute {
            let mem = allocateShared<'b> foldSize
            let tid = getLocalId().X
            let gid = getWorkGroupId().X
            
            // index calculations
            let lai = 2 * tid
            let lbi = lai + 1
            let ai  = foldSize * gid + lai
            let bi  = ai + 1 
            
            // load existing values into local memory
            mem.[lai] <- if ai < cnt then (%mul) l.[ai] r.[ai] else %zero
            mem.[lbi] <- if bi < cnt then (%mul) l.[bi] r.[bi] else %zero
            barrier()
            
            // sum the local values from right to left
            let mutable s = halfFoldSize
            while s > 0 do
                if tid < s then
                    mem.[tid] <- (%add) mem.[tid] mem.[tid + s]

                s <- s >>> 1
                barrier()
                
            // store the overall sum in the result-buffer
            if tid = 0 then
                result.[gid] <- mem.[0]

        }

    [<LocalSize(X = 8, Y = 8)>]
    let fold2d (zero : Expr<'b>) (addV4 : Expr<V4d -> V4d -> 'b>) (add : Expr<'b -> 'b -> 'b>) (lLevel : int) (result : 'b[]) =
        compute {
            let mem = allocateShared<'b> 128

            let size = lSampler.GetSize(lLevel)
            let rcpSize = 1.0 / V2d size

            let lid = getLocalId().XY
            let gid = getWorkGroupId().XY
            let groups = getWorkGroupCount().XY

            // index calculations
            let id = gid * 16 + lid * 2
            let tc00 = (V2d id + V2d.Half) * rcpSize
            let tc01 = tc00 + V2d(rcpSize.X, 0.0)
            let tc10 = tc00 + V2d(0.0, rcpSize.Y)
            let tc11 = tc00 + rcpSize
            let tid = lid.X + 8 * lid.Y

            // load existing values into local memory
            let v0 =
                (%addV4) (lSampler.SampleLevel(tc00, float lLevel)) (lSampler.SampleLevel(tc01, float lLevel))
                
            let v1 =
                (%addV4) (lSampler.SampleLevel(tc10, float lLevel)) (lSampler.SampleLevel(tc11, float lLevel)) 

            mem.[tid * 2 + 0] <- v0
            mem.[tid * 2 + 1] <- v1
            barrier()
            
            // sum the local values from right to left
            let mutable s = 64
            while s > 0 do
                if tid < s then
                    mem.[tid] <- (%add) mem.[tid] mem.[tid + s]

                s <- s >>> 1
                barrier()
                
            // store the overall sum in the result-buffer
            if tid = 0 then
                result.[gid.X + groups.X * gid.Y] <- mem.[0]

        }
       
    [<LocalSize(X = 8, Y = 8)>]
    let dot2d (zero : Expr<'b>) (mul : Expr<V4d -> V4d -> 'b>) (add : Expr<'b -> 'b -> 'b>) (lLevel : int) (rLevel : int) (result : 'b[]) =
        compute {
            let mem = allocateShared<'b> 128

            let size = lSampler.GetSize(lLevel)
            let rcpSize = 1.0 / V2d size

            let lid = getLocalId().XY
            let gid = getWorkGroupId().XY
            let groups = getWorkGroupCount().XY

            // index calculations
            let id = gid * 16 + lid * 2
            let tc00 = (V2d id + V2d.Half) * rcpSize
            let tc01 = tc00 + V2d(rcpSize.X, 0.0)
            let tc10 = tc00 + V2d(0.0, rcpSize.Y)
            let tc11 = tc00 + rcpSize
            let tid = lid.X + 8 * lid.Y

            // load existing values into local memory
            let v0 =
                (%add) 
                    ((%mul) (lSampler.SampleLevel(tc00, float lLevel)) (rSampler.SampleLevel(tc00, float rLevel)))
                    ((%mul) (lSampler.SampleLevel(tc01, float lLevel)) (rSampler.SampleLevel(tc01, float rLevel)))
                
            let v1 =
                (%add) 
                    ((%mul) (lSampler.SampleLevel(tc10, float lLevel)) (rSampler.SampleLevel(tc10, float rLevel)))
                    ((%mul) (lSampler.SampleLevel(tc11, float lLevel)) (rSampler.SampleLevel(tc11, float rLevel)))

            mem.[tid * 2 + 0] <- v0
            mem.[tid * 2 + 1] <- v1
            barrier()
            
            // sum the local values from right to left
            let mutable s = 64
            while s > 0 do
                if tid < s then
                    mem.[tid] <- (%add) mem.[tid] mem.[tid + s]

                s <- s >>> 1
                barrier()
                
            // store the overall sum in the result-buffer
            if tid = 0 then
                result.[gid.X + groups.X * gid.Y] <- mem.[0]

        }
              

    [<LocalSize(X = 64)>]
    let mad1d (mul : Expr<'a -> 'b -> 'c>) (add : Expr<'d -> 'c -> 'd>) (cnt : int) (src : 'a[]) (factor : 'b) (dst : 'd[]) =
        compute {
            let id = getGlobalId().X
            if id < cnt then
                dst.[id] <- (%add) dst.[id] ((%mul) src.[id] factor)
        }




type TensorTools<'a when 'a : unmanaged>(runtime : IRuntime) =
    static let num = RealInstances.instance<'a>
    static let rnum = ReflectedReal.instance<'a>
    

    let conv = rnum.fromV4
    let v4Mul = <@ fun a b -> (%rnum.mul) ((%conv) a) ((%conv) b) @>
    let v4Add = <@ fun a b -> (%rnum.add) ((%conv) a) ((%conv) b) @>
    let v4Max = <@ fun a b -> (%rnum.max) ((%conv) a) ((%conv) b) @>
    let v4Min = <@ fun a b -> (%rnum.min) ((%conv) a) ((%conv) b) @>

    static let ceilDiv (a : int) (b : int) =
        if a % b = 0 then a / b
        else 1 + a / b
    
    static let ceilDiv2 (a : V2i) (b : V2i) =
        V2i(
            ceilDiv a.X b.X,
            ceilDiv a.Y b.Y
        )

    let withImage (img : PixImage) (action : IBackendTexture -> 'r) =
        let tex = runtime.CreateTexture (img.Size, TextureFormat.ofPixFormat img.PixFormat TextureParams.empty, 1, 1)
        runtime.Upload(tex, 0, 0, img)
        try action tex
        finally runtime.DeleteTexture tex

    let withBuffer (data : 'a[]) (action : IBuffer<'a> -> 'r) =
        use b = runtime.CreateBuffer data
        action b
        
    let withBuffer2d (data : 'a[,]) (action : IBuffer<'a> -> 'r) =
        let cnt = data.GetLength(0) * data.GetLength(1)
        use buffer = runtime.CreateBuffer<'a>(cnt)
        let gc = GCHandle.Alloc(data, GCHandleType.Pinned) 
        try buffer.Upload(gc.AddrOfPinnedObject(), nativeint sizeof<'a> * nativeint cnt)
        finally gc.Free()
        action buffer

    let dot1d = runtime.CreateComputeShader (TensorToolShaders.dot1d rnum.zero rnum.mul rnum.add)
    let dot2d = runtime.CreateComputeShader (TensorToolShaders.dot2d rnum.zero v4Mul rnum.add)
    let sum1d = runtime.CreateComputeShader (TensorToolShaders.fold1d rnum.zero rnum.add)
    let sum2d = runtime.CreateComputeShader (TensorToolShaders.fold2d rnum.zero v4Add rnum.add)

    let mul1d = lazy ( runtime.CreateComputeShader (TensorToolShaders.fold1d rnum.one rnum.mul) )
    let max1d = lazy ( runtime.CreateComputeShader (TensorToolShaders.fold1d rnum.ninf rnum.max) )
    let min1d = lazy ( runtime.CreateComputeShader (TensorToolShaders.fold1d rnum.pinf rnum.min) )
    
    let mul2d = lazy ( runtime.CreateComputeShader (TensorToolShaders.fold2d rnum.one v4Mul rnum.mul) )
    let max2d = lazy ( runtime.CreateComputeShader (TensorToolShaders.fold2d rnum.ninf v4Max rnum.max) )
    let min2d = lazy ( runtime.CreateComputeShader (TensorToolShaders.fold2d rnum.pinf v4Min rnum.min) )
    
    let mad1d = runtime.CreateComputeShader (TensorToolShaders.mad1d rnum.mul rnum.add)

    let rec fold1d (zero : 'a) (shader : IComputeShader) (v : IBuffer<'a>) =
        if v.Count <= 0 then
            zero

        elif v.Count = 1 then
            let arr = Array.zeroCreate 1
            v.Download(arr)
            arr.[0]

        else
            let resCnt = ceilDiv v.Count TensorToolShaders.foldSize
            use res = runtime.CreateBuffer<'a>(resCnt)
            use input = runtime.NewInputBinding shader
            input.["arr"] <- v
            input.["cnt"] <- v.Count
            input.["result"] <- res
            input.Flush()
            
            runtime.Run [
                ComputeCommand.Bind shader
                ComputeCommand.SetInput input
                ComputeCommand.Dispatch resCnt
                ComputeCommand.Sync(res.Buffer, ResourceAccess.ShaderWrite, ResourceAccess.ShaderRead)
            ]

            fold1d zero shader res

    let rec fold2d (zero : 'a) (shader : IComputeShader) (shader1d : IComputeShader) (v : ITextureSubResource) =
        let size = v.Size.XY
        
        if size.AnySmallerOrEqual 0 then
            zero
            
        else
            let resCnt = ceilDiv2 size (V2i(16,16))
            
            use res = runtime.CreateBuffer<'a>(resCnt.X * resCnt.Y)
            use input = runtime.NewInputBinding shader
            input.["l"] <- v.Texture
            input.["lLevel"] <- v.Level
            input.["result"] <- res
            input.Flush()

            runtime.Run [
                ComputeCommand.Bind shader
                ComputeCommand.SetInput input
                ComputeCommand.Dispatch resCnt
                ComputeCommand.Sync(res.Buffer, ResourceAccess.ShaderWrite, ResourceAccess.ShaderRead)
            ]
            
            fold1d zero shader1d res
            
    member x.Sum(v : IBuffer<'a>) = fold1d num.zero sum1d v
    member x.Product(v : IBuffer<'a>) = fold1d num.one mul1d.Value v
    member x.Min(v : IBuffer<'a>) = fold1d (num.div num.one num.zero) min1d.Value v
    member x.Max(v : IBuffer<'a>) = fold1d (num.div (num.neg num.one) num.zero) max1d.Value v
    member x.Dot(l : IBuffer<'a>, r : IBuffer<'a>) =
        if l.Count <> r.Count then failwith "buffers have mismatching size"
        let cnt = l.Count
        
        if cnt <= 0 then
            num.zero

        elif cnt = 1 then
            let la = Array.zeroCreate 1
            let ra = Array.zeroCreate 1
            l.Download(la)
            r.Download(ra)
            num.mul la.[0] ra.[0]

        else
            let resCnt = ceilDiv cnt TensorToolShaders.foldSize
            
            use res = runtime.CreateBuffer<'a>(resCnt)
            use input = runtime.NewInputBinding dot1d
            input.["l"] <- l
            input.["r"] <- r
            input.["cnt"] <- l.Count
            input.["result"] <- res
            input.Flush()

            runtime.Run [
                ComputeCommand.Bind dot1d
                ComputeCommand.SetInput input
                ComputeCommand.Dispatch resCnt
                ComputeCommand.Sync(res.Buffer, ResourceAccess.ShaderWrite, ResourceAccess.ShaderRead)
            ]

            x.Sum(res)

    member x.Length(l : IBuffer<'a>) =
        let r = x.Dot(l,l)
        num.sqrt r
        
    member x.LengthSquared(l : IBuffer<'a>) =
        x.Dot(l,l)

    member x.Average(v : IBuffer<'a>) =
        num.div (x.Sum v) (num.fromInt v.Count)
        
    member x.Variance(v : IBuffer<'a>) =
        let e0 = num.div (x.LengthSquared v) (num.fromInt v.Count)
        let e1 = num.pow (x.Average v) 2
        num.sub e0 e1

    member x.MultiplyAdd(src : IBuffer<'a>, f : 'a, dst : IBuffer<'a>) =
        let cnt = min src.Count dst.Count
        if cnt > 0 then
            use input = runtime.NewInputBinding mad1d
            input.["src"] <- src
            input.["cnt"] <- cnt
            input.["dst"] <- dst
            input.["factor"] <- f
            input.Flush()

            runtime.Run [
                ComputeCommand.Bind mad1d
                ComputeCommand.SetInput input
                ComputeCommand.Dispatch (ceilDiv cnt mad1d.LocalSize.X)
            ]


    member x.Sum(v : ITextureSubResource) = fold2d num.zero sum2d sum1d v
    member x.Product(v : ITextureSubResource) = fold2d num.one mul2d.Value mul1d.Value v
    member x.Min(v : ITextureSubResource) = fold2d (num.div num.one num.zero) min2d.Value min1d.Value v
    member x.Max(v : ITextureSubResource) = fold2d (num.div (num.neg num.one) num.zero) max2d.Value max1d.Value v
        
    member x.Dot(l : ITextureSubResource, r : ITextureSubResource) =  
        if l.Size.XY <> r.Size.XY then failwith "buffers have mismatching size"
        let size = l.Size.XY
        
        if size.AnySmallerOrEqual 0 then
            num.zero
            
        else
            let resCnt = ceilDiv2 size (V2i(16,16))
            
            use res = runtime.CreateBuffer<'a>(resCnt.X * resCnt.Y)
            use input = runtime.NewInputBinding dot2d
            input.["l"] <- l.Texture
            input.["r"] <- r.Texture
            input.["lLevel"] <- l.Level
            input.["rLevel"] <- r.Level
            input.["result"] <- res
            input.Flush()

            runtime.Run [
                ComputeCommand.Bind dot2d
                ComputeCommand.SetInput input
                ComputeCommand.Dispatch resCnt
                ComputeCommand.Sync(res.Buffer, ResourceAccess.ShaderWrite, ResourceAccess.ShaderRead)
            ]

            x.Sum(res)

    member x.Length(l : ITextureSubResource) =
        let r = x.Dot(l,l)
        num.sqrt r
        
    member x.LengthSquared(l : ITextureSubResource) =
        x.Dot(l,l)
        
    member x.Average(v : ITextureSubResource) =
        num.div (x.Sum v) (num.fromInt (v.Size.X * v.Size.Y))
        
    member x.Variance(v : ITextureSubResource) =
        let e0 = num.div (x.LengthSquared v) (num.fromInt (v.Size.X * v.Size.Y))
        let e1 = num.pow (x.Average v) 2
        num.sub e0 e1


    member x.Sum(v : 'a[]) = withBuffer v x.Sum
    member x.Product(v : 'a[]) = withBuffer v x.Product
    member x.Min(v : 'a[]) = withBuffer v x.Min
    member x.Max(v : 'a[]) = withBuffer v x.Max
    member x.Dot(l : 'a[], r : 'a[]) = withBuffer l (fun lb -> withBuffer r (fun rb -> x.Dot(lb, rb)))
    member x.Length(v : 'a[]) = withBuffer v x.Length
    member x.LengthSquared(v : 'a[]) = withBuffer v x.LengthSquared
    member x.Average(v : 'a[]) = withBuffer v x.Average
    member x.Variance(v : 'a[]) = withBuffer v x.Variance
    member x.MultiplyAdd(src : 'a[], f : 'a, dst : 'a[]) = withBuffer src (fun src -> withBuffer dst (fun dst -> x.MultiplyAdd(src, f, dst); dst.Download()))


    member x.Sum(v : 'a[,]) = withBuffer2d v x.Sum
    member x.Product(v : 'a[,]) = withBuffer2d v x.Product
    member x.Min(v : 'a[,]) = withBuffer2d v x.Min
    member x.Max(v : 'a[,]) = withBuffer2d v x.Max
    member x.Dot(l : 'a[,], r : 'a[,]) = withBuffer2d l (fun lb -> withBuffer2d r (fun rb -> x.Dot(lb, rb)))
    member x.Length(v : 'a[,]) = withBuffer2d v x.Length
    member x.LengthSquared(v : 'a[,]) = withBuffer2d v x.LengthSquared
    member x.Average(v : 'a[,]) = withBuffer2d v x.Average
    member x.Variance(v : 'a[,]) = withBuffer2d v x.Variance
    member x.MultiplyAdd(src : 'a[,], f : 'a, dst : 'a[,]) = 
        withBuffer2d src (fun bsrc -> 
            withBuffer2d dst (fun bdst -> 
                x.MultiplyAdd(bsrc, f, bdst)
                let cnt = (src.GetLength 0) * (src.GetLength 1)
                let res : 'a[,] = Array2D.zeroCreate (src.GetLength 0) (src.GetLength 1)
                let gc = GCHandle.Alloc(res, GCHandleType.Pinned)
                try
                    bdst.Buffer.Download(0n, gc.AddrOfPinnedObject(), nativeint sizeof<'a> * nativeint cnt)
                    res
                finally
                    gc.Free()
            )
        )



    member x.Sum(v : PixImage) = withImage v (fun t -> x.Sum(t.[TextureAspect.Color, 0, 0]))
    member x.Product(v : PixImage) = withImage v (fun t -> x.Product(t.[TextureAspect.Color, 0, 0]))
    member x.Min(v : PixImage) = withImage v (fun t -> x.Min(t.[TextureAspect.Color, 0, 0]))
    member x.Max(v : PixImage) = withImage v (fun t -> x.Max(t.[TextureAspect.Color, 0, 0]))
    member x.Dot(l : PixImage, r : PixImage) = withImage l (fun tl -> withImage r (fun tr -> x.Dot(tl.[TextureAspect.Color, 0, 0], tr.[TextureAspect.Color, 0, 0])))
    member x.Length(v : PixImage) = withImage v (fun t -> x.Length(t.[TextureAspect.Color, 0, 0]))
    member x.LengthSquared(v : PixImage) = withImage v (fun t -> x.LengthSquared(t.[TextureAspect.Color, 0, 0]))
    member x.Average(v : PixImage) = withImage v (fun t -> x.Average(t.[TextureAspect.Color, 0, 0]))
    member x.Variance(v : PixImage) = withImage v (fun t -> x.Variance(t.[TextureAspect.Color, 0, 0]))
        