namespace ConjugateGradient

open Microsoft.FSharp.Quotations
open Aardvark.Base
open Aardvark.Rendering
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Collections.Concurrent

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
    let fold2d (zero : Expr<'b>) (addV4 : Expr<V4f -> V4f -> 'b>) (add : Expr<'b -> 'b -> 'b>) (result : 'b[]) =
        compute {
            let mem = allocateShared<'b> 128

            let size = lSampler.Size
            let rcpSize = 1.0f / V2f size

            let lid = getLocalId().XY
            let gid = getWorkGroupId().XY
            let groups = getWorkGroupCount().XY

            // index calculations
            let id = gid * 16 + lid * 2
            let tc00 = (V2f id + V2f.Half) * rcpSize
            let tc01 = tc00 + V2f(rcpSize.X, 0.0f)
            let tc10 = tc00 + V2f(0.0f, rcpSize.Y)
            let tc11 = tc00 + rcpSize
            let tid = lid.X + 8 * lid.Y

            // load existing values into local memory
            let v0 =
                (%addV4) (lSampler.SampleLevel(tc00, 0.0f)) (lSampler.SampleLevel(tc01, 0.0f))
                
            let v1 =
                (%addV4) (lSampler.SampleLevel(tc10, 0.0f)) (lSampler.SampleLevel(tc11, 0.0f))

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
    let dot2d (zero : Expr<'b>) (mul : Expr<V4f -> V4f -> 'b>) (add : Expr<'b -> 'b -> 'b>) (result : 'b[]) =
        compute {
            let mem = allocateShared<'b> 128

            let size = lSampler.Size
            let rcpSize = 1.0f / V2f size

            let lid = getLocalId().XY
            let gid = getWorkGroupId().XY
            let groups = getWorkGroupCount().XY

            // index calculations
            let id = gid * 16 + lid * 2
            let tc00 = (V2f id + V2f.Half) * rcpSize
            let tc01 = tc00 + V2f(rcpSize.X, 0.0f)
            let tc10 = tc00 + V2f(0.0f, rcpSize.Y)
            let tc11 = tc00 + rcpSize
            let tid = lid.X + 8 * lid.Y

            // load existing values into local memory
            let v0 =
                (%add) 
                    ((%mul) (lSampler.SampleLevel(tc00, 0.0f)) (rSampler.SampleLevel(tc00, 0.0f)))
                    ((%mul) (lSampler.SampleLevel(tc01, 0.0f)) (rSampler.SampleLevel(tc01, 0.0f)))
                
            let v1 =
                (%add) 
                    ((%mul) (lSampler.SampleLevel(tc10, 0.0f)) (rSampler.SampleLevel(tc10, 0.0f)))
                    ((%mul) (lSampler.SampleLevel(tc11, 0.0f)) (rSampler.SampleLevel(tc11, 0.0f)))

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
    let mad1d (mul : Expr<'a -> 'b -> 'c>) (add : Expr<'c -> 'c -> 'a>) (cnt : int) (src : 'a[]) (srcFactor : 'b) (dst : 'a[]) (dstFactor : 'b) =
        compute {
            let id = getGlobalId().X
            if id < cnt then
                dst.[id] <- (%add) ((%mul) dst.[id] dstFactor) ((%mul) src.[id] srcFactor)
        }
        
    [<LocalSize(X = 8, Y = 8)>]
    let mad2d<'c, 'f, 'fmt when 'fmt :> Formats.IFloatingFormat> (mul : Expr<V4f -> 'f -> 'c>) (add : Expr<'c -> 'c -> V4f>) (srcFactor : 'f) (dstFactor : 'f) (src : Image2d<'fmt>) (dst : Image2d<'fmt>) =
        compute {
            let id = getGlobalId().XY
            let s = dst.Size

            if id.X < s.X && id.Y < s.Y then
                dst.[id] <- (%add) ((%mul) dst.[id] dstFactor) ((%mul) src.[id] srcFactor)
        }

    [<LocalSize(X = 8, Y = 8)>]
    let set2d<'fmt when 'fmt :> Formats.IFloatingFormat>  (value : V4f) (dst : Image2d<'fmt>) =
        compute {
            let id = getGlobalId().XY
            let s = dst.Size

            if id.X < s.X && id.Y < s.Y then
                dst.[id] <- value
        }

[<AutoOpen>]
module private FormatHacks = 
    open System
    open System.Reflection

    type FormatVisitor<'r> =
        abstract member Visit<'f when 'f :> FShade.Formats.IFloatingFormat> : unit -> 'r

    type FormatVisitor private() =
        static let table =
            Dictionary.ofList [
                TextureFormat.R11fG11fB10f, typeof<FShade.Formats.r11g11b10f>
                TextureFormat.R16, typeof<FShade.Formats.r16>
                TextureFormat.R16f, typeof<FShade.Formats.r16f>
                TextureFormat.R16Snorm, typeof<FShade.Formats.r16_snorm>
                TextureFormat.R32f, typeof<FShade.Formats.r32f>
                TextureFormat.R8, typeof<FShade.Formats.r8>
                TextureFormat.R8Snorm, typeof<FShade.Formats.r8_snorm>
                TextureFormat.Rg16, typeof<FShade.Formats.rg16>
                TextureFormat.Rg16f, typeof<FShade.Formats.rg16f>
                TextureFormat.Rg16Snorm, typeof<FShade.Formats.rg16_snorm>
                TextureFormat.Rg32f, typeof<FShade.Formats.rg32f>
                TextureFormat.Rg8, typeof<FShade.Formats.rg8>
                TextureFormat.Rg8Snorm, typeof<FShade.Formats.rg8_snorm>
                TextureFormat.Rgb10A2, typeof<FShade.Formats.rgb10a2>
                TextureFormat.Rgba16, typeof<FShade.Formats.rgba16>
                TextureFormat.Rgba16f, typeof<FShade.Formats.rgba16f>
                TextureFormat.Rgba16Snorm, typeof<FShade.Formats.rgba16_snorm>
                TextureFormat.Rgba32f, typeof<FShade.Formats.rgba32f>
                TextureFormat.Rgba8, typeof<FShade.Formats.rgba8>
                TextureFormat.Rgba8Snorm, typeof<FShade.Formats.rgba8_snorm>
            ]

        static let compile (formatType : Type) (resultType : Type) =
            let tv = typedefof<FormatVisitor<_>>.MakeGenericType [| resultType |]
            let m = tv.GetMethod("Visit", BindingFlags.NonPublic ||| BindingFlags.Public ||| BindingFlags.Instance)
            let m = m.MakeGenericMethod [| formatType |]
            
            IL.Assembler.assembleDelegate {
                IL.ArgumentTypes = [| tv |]
                IL.ReturnType = resultType
                IL.Body = 
                    [
                        IL.Ldarg 0
                        IL.Call m
                        IL.Ret
                    ]
            }

        static let cache = System.Collections.Concurrent.ConcurrentDictionary<Type * Type, Delegate>()

        static let get (formatType : Type) (resultType : Type) =
            cache.GetOrAdd((formatType, resultType), fun (formatType, resultType) ->
                compile formatType resultType
            )
            
        static member Visit<'r>(fmt : TextureFormat, v : FormatVisitor<'r>) =
            let func = get table.[fmt] typeof<'r> |> unbox<Func<FormatVisitor<'r>, 'r>>
            func.Invoke(v)


    let formatCache (v : FormatVisitor<'r>) =
        let dict = System.Collections.Concurrent.ConcurrentDictionary<TextureFormat, 'r>()

        fun (fmt : TextureFormat) ->
            dict.GetOrAdd(fmt, fun fmt ->
                FormatVisitor.Visit(fmt, v)
            )


type TensorTools<'a when 'a : unmanaged> private(runtime : IRuntime) =
    static let num = RealInstances.instance<'a>
    static let rnum = ReflectedReal.instance<'a>
    static let cache = ConcurrentDictionary<IRuntime, TensorTools<'a>>()

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
        let tex = runtime.CreateTexture2D (img.Size, TextureFormat.ofPixFormat img.PixFormat TextureParams.empty, 1, 1)
        tex.Upload(img)
        try action tex
        finally runtime.DeleteTexture tex

    let withBuffer (data : 'a[]) (action : IBuffer<'a> -> 'r) =
        use b = runtime.CreateBuffer data
        action b
        
    let withBuffer2d (data : 'a[,]) (action : IBuffer<'a> -> 'r) =
        let cnt = data.GetLength(0) * data.GetLength(1)
        use buffer = runtime.CreateBuffer<'a>(cnt)
        let gc = GCHandle.Alloc(data, GCHandleType.Pinned) 
        try buffer.Upload(gc.AddrOfPinnedObject(), uint64 sizeof<'a> * uint64 cnt)
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
    let mad2d = 
        let v4Mul = <@ fun a b -> (%rnum.mul) ((%conv) a) b @>
        let v4Add = <@ fun a b -> (%rnum.toV4) ((%rnum.add) a b) @>
        formatCache { 
            new FormatVisitor<_> with
                member x.Visit<'f when 'f :> FShade.Formats.IFloatingFormat>() =
                    runtime.CreateComputeShader (TensorToolShaders.mad2d<'a, 'a, 'f> v4Mul v4Add)
        }
    let set2d = 
        formatCache { 
            new FormatVisitor<_> with
                member x.Visit<'f when 'f :> FShade.Formats.IFloatingFormat>() =
                    runtime.CreateComputeShader (TensorToolShaders.set2d<'f>)
        }      

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
            use input = runtime.CreateInputBinding shader
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
            use input = runtime.CreateInputBinding shader
            input.["l"] <- v
            input.["result"] <- res
            input.Flush()

            runtime.Run [
                ComputeCommand.Bind shader
                ComputeCommand.SetInput input
                ComputeCommand.Dispatch resCnt
                ComputeCommand.Sync(res.Buffer, ResourceAccess.ShaderWrite, ResourceAccess.ShaderRead)
            ]
            
            fold1d zero shader1d res
          
    static member Get(r : IRuntime) =
        cache.GetOrAdd(r, fun r ->
            let t = new TensorTools<'a>(r)
            r.OnDispose.Add (fun () -> () (* t.Dispose() *))
            t
        )

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
            use input = runtime.CreateInputBinding dot1d
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

    member x.MultiplyAdd(src : IBuffer<'a>, srcFactor : 'a, dst : IBuffer<'a>, dstFactor : 'a) =
        let cnt = min src.Count dst.Count
        if cnt > 0 then
            use input = runtime.CreateInputBinding mad1d
            input.["src"] <- src
            input.["cnt"] <- cnt
            input.["dst"] <- dst
            input.["srcFactor"] <- srcFactor
            input.["dstFactor"] <- dstFactor
            input.Flush()

            runtime.Run [
                ComputeCommand.Bind mad1d
                ComputeCommand.SetInput input
                ComputeCommand.Dispatch (ceilDiv cnt mad1d.LocalSize.X)
            ]

    member x.MultiplyAdd(src : ITextureSubResource, srcFactor : 'a, dst : ITextureSubResource, dstFactor : 'a) =
        let size = V2i(min src.Size.X dst.Size.X, min src.Size.Y dst.Size.Y)
        if size.AllGreaterOrEqual 1 then
            let mad2d = mad2d dst.Texture.Format
            use input = runtime.CreateInputBinding mad2d
            input.["src"] <- src
            input.["srcFactor"] <- srcFactor
            input.["dst"] <- dst
            input.["dstFactor"] <- dstFactor
            input.Flush()

            runtime.Run [
                ComputeCommand.Bind mad2d
                ComputeCommand.SetInput input
                ComputeCommand.Dispatch (ceilDiv2 size mad2d.LocalSize.XY)
            ]

    member x.Set(dst : ITextureSubResource, value : V4d) =
        let size = dst.Size.XY
        if size.AllGreaterOrEqual 1 then
            let set2d = set2d dst.Texture.Format
            use input = runtime.CreateInputBinding set2d
            input.["dst"] <- dst
            input.["value"] <- value
            input.Flush()

            runtime.Run [
                ComputeCommand.Bind set2d
                ComputeCommand.SetInput input
                ComputeCommand.Dispatch (ceilDiv2 size set2d.LocalSize.XY)
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
            use input = runtime.CreateInputBinding dot2d
            input.["l"] <- l
            input.["r"] <- r
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
    member x.MultiplyAdd(src : 'a[], srcFactor : 'a, dst : 'a[], dstFactor : 'a) = withBuffer src (fun src -> withBuffer dst (fun dst -> x.MultiplyAdd(src, srcFactor, dst, dstFactor); dst.Download()))


    member x.Sum(v : 'a[,]) = withBuffer2d v x.Sum
    member x.Product(v : 'a[,]) = withBuffer2d v x.Product
    member x.Min(v : 'a[,]) = withBuffer2d v x.Min
    member x.Max(v : 'a[,]) = withBuffer2d v x.Max
    member x.Dot(l : 'a[,], r : 'a[,]) = withBuffer2d l (fun lb -> withBuffer2d r (fun rb -> x.Dot(lb, rb)))
    member x.Length(v : 'a[,]) = withBuffer2d v x.Length
    member x.LengthSquared(v : 'a[,]) = withBuffer2d v x.LengthSquared
    member x.Average(v : 'a[,]) = withBuffer2d v x.Average
    member x.Variance(v : 'a[,]) = withBuffer2d v x.Variance
    member x.MultiplyAdd(src : 'a[,], srcFactor : 'a, dst : 'a[,], dstFactor : 'a) = 
        withBuffer2d src (fun bsrc -> 
            withBuffer2d dst (fun bdst -> 
                x.MultiplyAdd(bsrc, srcFactor, bdst, dstFactor)
                let cnt = (src.GetLength 0) * (src.GetLength 1)
                let res : 'a[,] = Array2D.zeroCreate (src.GetLength 0) (src.GetLength 1)
                let gc = GCHandle.Alloc(res, GCHandleType.Pinned)
                try
                    bdst.Buffer.Download(0UL, gc.AddrOfPinnedObject(), uint64 sizeof<'a> * uint64 cnt)
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
        