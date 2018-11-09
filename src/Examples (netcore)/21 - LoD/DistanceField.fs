module DistanceField

open Aardvark.Base

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