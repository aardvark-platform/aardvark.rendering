module DistanceField

open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Rendering.Vulkan
open FSharp.Quotations

// ported from here: https://github.com/parmanoir/Meijster-distance/blob/master/index.html

let computeDistance (p : PixImage<byte>) =
    let infinity = p.Size.X + p.Size.Y 
    let b = p.GetChannel(0L)
    let r = PixImage<int>()
    let one = 255uy

    let g = Array2D.zeroCreate p.Size.X p.Size.Y

    Log.startTimed "one"
    for x in 0 .. p.Size.X - 1 do 
        if b.[x,0] = one then   
            g.[x,0] <- 0
        else
            g.[x,0] <- infinity
        for y in 1 .. p.Size.Y - 1 do
            if b.[x,y] = one then
                g.[x,y] <- 0
            else 
                g.[x,y] <- 1 + g.[x,y-1]
        for y = p.Size.Y-2 downto  0 do
            if g.[x,y+1] < g.[x,y] then
                g.[x,y] <- 1 + g.[x,y+1]

    Log.stop()
    let edt_f (x,i,g_i) = (x-i)*(x-i) + g_i*g_i
    let edt_sep (i:int,u:int,g_i:int,g_u:int) : int= 
        (u*u - i*i + g_u*g_u - g_i*g_i) / (2*(u-i))

    let f = edt_f
    let sep = edt_sep
    let result = PixImage<float32>(Col.Format.RGB, p.Size)
    let mutable dt = result.GetChannel(Col.Channel.Red)
    let s = Array.zeroCreate p.Size.X
    let t = Array.zeroCreate p.Size.X
    let mutable w = 0

    Log.startTimed "go"
    for y in 0 .. p.Size.Y - 1 do
        let mutable q = 0
        s.[0] <- 0
        t.[0] <- 0
        for u in 1 .. p.Size.X - 1 do
            while q >= 0 && f(t.[q], s.[q], g.[s.[q],y]) > f(t.[q], u, g.[u,y]) do
                q <- q - 1
            if q < 0 then
                q <- 0
                s.[0] <- u
            else
                w <- 1 + sep(s.[q],u,g.[s.[q],y], g.[u,y])
                if w < p.Size.X then
                    q <- q + 1
                    s.[q] <- u
                    t.[q] <- w
        for u = p.Size.X - 1 downto  0 do
            let d = f(u,s.[q],g.[s.[q],y])
            let d = d |> float |> sqrt |> floor |> float32
            dt.[u,y] <- if d < 50.0f then 1.0f else 0.0f// d
            if u = t.[q] then 
                q <- q - 1
    Log.stop ()
    result

[<ReflectedDefinition>]
module Shaders = 

    (** super naive stupid,super naive stupid ,super naive stupid ,super naive stupid  **)

    open FShade

    let f (x,i,g_i) = (x-i)*(x-i) + g_i*g_i
    let sep (i:int,u:int,g_i:int,g_u:int) : int= 
        (u*u - i*i + g_u*g_u - g_i*g_i) / (2*(u-i))

    [<LocalSize(X = 64)>]
    let phase1 (b : IntImage2d<Formats.r32i>) (g : IntImage2d<Formats.r32i>) =
        compute {
            let one = 1
            let infinity = b.Size.X + b.Size.Y
            let id = getGlobalId()
            let x = id.X
            if x < b.Size.X then
                if b.[V2i(x,0)].X = one then   
                    g.[V2i(x,0)] <- V4i.OOOO
                else
                    g.[V2i(x,0)] <- V4i(infinity)
                for y in 1 .. b.Size.Y - 1 do
                    if b.[V2i(x,y)].X = one then
                        g.[V2i(x,y)] <- V4i 0
                    else 
                        g.[V2i(x,y)] <- V4i(1 + g.[V2i(x,y-1)].X)
                for y = b.Size.Y-2 downto 0 do
                    if g.[V2i(x,y+1)].X < g.[V2i(x,y)].X then
                        g.[V2i(x,y)] <- V4i(1 + g.[V2i(x,y+1)].X)
        }

    [<LocalSize(X = 64)>]
    let phase2 (g : IntImage2d<Formats.r32i>) (s : IntImage2d<Formats.r32i>) 
               (t : IntImage2d<Formats.r32i>) (dt : Image2d<Formats.r32f>) = 
        compute {
            let id = getGlobalId()
            let y = id.X
            if y < g.Size.Y then
                let mutable w = 0
                let mutable q = 0
                s.[V2i(0,y)] <- V4i 0
                t.[V2i(0,y)] <- V4i 0
                for u in 1 .. g.Size.X - 1 do
                    while q >= 0 && f(t.[V2i(q,y)].X, s.[V2i(q,y)].X, g.[V2i(s.[V2i(q,y)].X,y)].X) > f(t.[V2i(q,y)].X, u, g.[V2i(u,y)].X) do
                        q <- q - 1
                    if q < 0 then
                        q <- 0
                        s.[V2i(0,y)] <- V4i u
                    else
                        w <- 1 + sep(s.[V2i(q,y)].X,u,g.[V2i(s.[V2i(q,y)].X,y)].X, g.[V2i(u,y)].X)
                        if w < g.Size.X then
                            q <- q + 1
                            s.[V2i(q,y)] <- V4i u
                            t.[V2i(q,y)] <- V4i w
                for u = g.Size.X - 1 downto  0 do
                    let d = f(u,s.[V2i(q,y)].X,g.[V2i(s.[V2i(q,y)].X,y)].X)
                    let d = d |> float |> sqrt |> floor |> float32
                    let v = if d < 50.0f then 1.0f else 0.0f
                    dt.[V2i(u,y)] <- V4d v // d
                    if u = t.[V2i(q,y)].X then 
                        q <- q - 1
            
        }

    [<LocalSize(X = 8,Y=8)>]
    let toBinary (i : Image2d<Formats.rgba8>) (b : IntImage2d<Formats.r32i>) =
        compute {
            let id = getGlobalId()
            if id.X < i.Size.X && id.Y < i.Size.Y then
                b.[id.XY] <- if i.[id.XY].X = 1.0 then V4i 1 else V4i 0
        }

let ceilDiv (a : int) (b : int) =
    if a % b = 0 then a / b
    else 1 + a / b
    
let ceilDiv2 (a : V2i) (b : V2i) =
    V2i(
        ceilDiv a.X b.X,
        ceilDiv a.Y b.Y
    )

let computeDistanceGPU (pi : PixImage<byte>) =
    use app = new HeadlessVulkanApplication(false)
    let runtime = app.Runtime :> IComputeRuntime
    // and an instance of our parallel primitives
    //let primitives = new ParallelPrimitives(app.Runtime)

    let input = 
        runtime.PrepareTexture(PixTexture2d(PixImageMipMap [| pi :> PixImage |], true))

    let b = runtime.CreateTexture(pi.Size,TextureFormat.R32i,1,1)
    let g = runtime.CreateTexture(pi.Size,TextureFormat.R32i,1,1)
    let s = runtime.CreateTexture(pi.Size,TextureFormat.R32i,1,1)
    let t = runtime.CreateTexture(pi.Size,TextureFormat.R32i,1,1)
    let dt = runtime.CreateTexture(pi.Size,TextureFormat.Rgba32f,1,1)

    let toBinary = runtime.CreateComputeShader(Shaders.toBinary)
    use toBinaryInput = runtime.NewInputBinding(toBinary)
    toBinaryInput.["i"] <- input
    toBinaryInput.["b"] <- b
    toBinaryInput.Flush()

    let phase1 = runtime.CreateComputeShader(Shaders.phase1)
    let phase1Input = runtime.NewInputBinding(phase1)
    phase1Input.["b"] <- b
    phase1Input.["g"] <- g
    phase1Input.Flush()

    let phase2 = runtime.CreateComputeShader(Shaders.phase2)
    let phase2Input = runtime.NewInputBinding(phase2)
    phase2Input.["g"] <- g
    phase2Input.["s"] <- s
    phase2Input.["t"] <- t
    phase2Input.["dt"] <- dt
    phase2Input.Flush()

    runtime.Run [
        ComputeCommand.Bind toBinary
        ComputeCommand.SetInput toBinaryInput
        ComputeCommand.Dispatch(ceilDiv2 pi.Size.XY (V2i(8,8)))
        ComputeCommand.Sync(b)
    ]

    let p = 
        runtime.Compile [
                ComputeCommand.Bind phase1
                ComputeCommand.SetInput phase1Input
                ComputeCommand.Dispatch(ceilDiv pi.Size.X 64)
                ComputeCommand.Sync(g)

                ComputeCommand.Bind phase2
                ComputeCommand.SetInput phase2Input
                ComputeCommand.Dispatch(ceilDiv pi.Size.X 64)
                ComputeCommand.Sync(dt)
            ]

    for i in 0 .. 10 do 
        Log.startTimed "do it: %d" i
        p.Run()
        Log.stop()

    let pi = app.Runtime.Download(dt, 0, 0)
    pi

    