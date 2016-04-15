#if COMPILED
namespace Fonts
#else
#I @"..\..\..\bin\Debug"
#r "Aardvark.Base.dll"
#r "Aardvark.Base.TypeProviders.dll"
#r "Aardvark.Base.FSharp.dll"
#endif
open System
open System.Drawing
open System.Drawing.Drawing2D
open Aardvark.Base

[<AutoOpen>]
module ``Move To Base`` =
    let inline private until (cond : unit -> bool) (body : unit -> unit) =
        body()
        while not (cond()) do
            body()

    [<Struct; CustomEquality; NoComparison; StructuredFormatDisplay("{AsString}")>]
    type Polynomial private (u : unit, c : float[]) =
        static let rand = Random()

        static let pruneTrailingZeros (c : float[]) =
            let l = c.Length
            let mutable len = l
            while len > 0 && Fun.IsTiny(c.[len-1], 1.0E-5) do
                len <- len - 1

            if len <> l then
                Array.take len c
            else
                c

        static let zero = Polynomial [|0.0|]
        static let one = Polynomial [|1.0|]


        member x.Coefficients = c
        member x.Degree = c.Length

        member x.Evaluate(t : float) =
            let l1 = c.Length - 1
            match l1 with
                | -1 -> 0.0
                | 0 -> c.[0]
                | 1 -> c.[0] + t * c.[1]
                | 2 -> c.[0] + t * c.[1] + t * t * c.[2]
                | _ ->
                    let mutable res = c.[0]
                    let mutable tx = t
                    for i in 1..l1-1 do
                        res <- res + tx * c.[i]
                        tx <- tx * t
                    res <- res + tx * c.[l1]

                    res

        member x.Derivative =
            let degree = c.Length
            if degree <= 1 then zero
            else
                let res = Array.zeroCreate (degree - 1)
                for i in 0..degree-2 do
                    res.[i] <- c.[i + 1] * float (i + 1)
                res |> Polynomial

        member x.ComputeRealRoots(min : float, max : float, eps : float) =
            let x = x
            let valid v = 
                not (Double.IsNaN v) &&
                v >= min && v <= max &&
                Fun.IsTiny(x.Evaluate v, eps)


            match Array.toList x.Coefficients with
                | [] | [_] -> 
                    []

                | [d;k] -> 
                    [-d / k]

                | [c;b;a] -> 
                    let tup = Polynomial.RealRootsOf(a,b,c)
                    [tup.E0; tup.E1] |> List.filter valid |> List.sort

                | [d;c;b;a] ->
                    let tup = Polynomial.RealRootsOf(a,b,c,d)
                    [tup.E0; tup.E1; tup.E2] |> List.filter valid |> List.sort

                | [e;d;c;b;a] ->
                    let tup = Polynomial.RealRootsOf(a,b,c,d,e)
                    [tup.E0; tup.E1; tup.E2; tup.E3] |> List.filter valid |> List.sort

                | long ->
          
                    let bisect (f : float -> float) =
                        match Double.IsInfinity min, Double.IsInfinity max with
                            | (false, false) ->
                                let vmin = f min
                                let vmax = f max
                                if sign vmin <> sign vmax then

                                    let rec recurse (min : float) (vmin : float) (smin : int) (max : float) (vmax : float) (smax : int) =
                                        if Fun.IsTiny(vmin, eps)  then
                                            min
                                        elif Fun.IsTiny(vmax, eps)  then
                                            max
                                        else
                                            let mid = 0.5 * (max + min)
                                            let vmid = f mid
                                            let smid = sign vmid
                                            if smid = 0 then
                                                mid
                                            elif smin <> smid then
                                                recurse min vmin smin mid vmid smid
                                            elif smax <> smin then
                                                recurse mid vmid smid max vmax smax
                                            else
                                                nan
                                        

                                    recurse min vmin (sign vmin) max vmax (sign vmax)

                                else
                                    nan

                            | _ -> nan


                    let newton (f : float -> float) (f' : float -> float)  =
                        let rec recurse (depth : int) (lastValue : float) (t : float) (damping : float) =
                            if depth > 500 then
                                if Fun.IsTiny(lastValue, eps) then t
                                else nan
                            else
                                let v = f t
                                let v' = f' t
                                let step = damping * v / v'
                                let better = t - step

                                if Fun.IsTiny(v, eps) then
                                    t
                                else
                                    recurse (depth + 1) v better damping

                        let guess = 
                            match Double.IsInfinity min , Double.IsInfinity max with
                                | (true, true) -> 0.0
                                | (true, false) -> max
                                | (false, true) -> min
                                | (false, false) -> min + rand.NextDouble() * (max - min)

                        recurse 0 infinity guess 1.0


                    let ( == ) a b = Fun.ApproximateEquals(a, b, eps)
                    let ( != ) a b = Fun.ApproximateEquals(a, b, eps) |> not
                    let zero v = Fun.IsTiny(v, eps)

                    // found: https://github.com/duncanmcnae/cpp_brent/blob/master/cpp_brent.h
                    let brent (f : float -> float) (min : float) (max : float) =
                        let mutable a = min
                        let mutable b = max
                        let mutable fa = f a
                        let mutable fb = f b
                        let mutable fs = 0.0

                        if fa * fb > 0.0 then
                            nan
                        else
                            if abs fa < abs fb then
                                Fun.Swap(&a, &b)
                                Fun.Swap(&fa, &fb)

                            let mutable c = a
                            let mutable fc = fa
                            let mutable mflag = true
                            let mutable it = 0
                            let mutable s = 0.0
                            let mutable d = 0.0

                            while abs (b - a) > eps  do
                                if fa <> fc && fb <> fc then //not (Fun.ApproximateEquals(fa, fc, eps)) && not (Fun.ApproximateEquals(fb, fc, eps)) then
                                    let s1 = (a * fb * fc) / ((fa - fb) * (fa - fc))
                                    let s2 = (b * fa * fc) / ((fb - fa) * (fb - fc))
                                    let s3 = (c * fa * fb) / ((fc - fa) * (fc - fb))
                                    s <- s1 + s2 + s3
                                else
                                    s <- b - fb * (b - a) / (fb - fa)



                                if ((s < 0.25 * (3.0 * a + b)) || (s > b)) ||
                                   (mflag && abs(s-b) >= 0.5 * abs(b-c)) ||
                                   (not mflag && abs(s-b) >= 0.5 * abs(c-d)) ||
                                   (mflag && abs(b-c) < eps) ||
                                   (not mflag && abs(c-d) < eps)
                                then
                                    s <- 0.5 * (a + b)
                                    mflag <- true
                                else
                                    mflag <- false

                                fs <- f s
                                d <- c
                                c <- b
                                fc <- fb

                                if fa*fb < 0.0 then
                                    b <- s
                                    fb <- fs
                                else
                                    a <- s
                                    fa <- fs

                                if abs fa < abs fb then
                                    Fun.Swap(&a, &b)
                                    Fun.Swap(&fa, &fb)

                                inc &it

                            if zero (fb) then b
                            elif zero fs then s
                            else nan


                    let mutable t = nan

//                    // brent's method
//                    if Double.IsNaN t then
//                        match Double.IsInfinity min, Double.IsInfinity max with
//                            | (false, false) -> 
//                                t <- brent x.Evaluate -0.1 1.1
//                            | _ ->
//                                ()

//                    // bisect
//                    if Double.IsNaN t then
//                        t <- bisect x.Evaluate

                    // newton
                    if Double.IsNaN t then
                        let x' = x.Derivative
                        let mutable tn = newton x.Evaluate x'.Evaluate
                        let mutable tries = 1
                        while Double.IsNaN tn && tries < 50 do
                            //printfn "retry"
                            tn <-  newton x.Evaluate x'.Evaluate
                            tries <- tries + 1
                    
                        
                        t <- tn


                    if Double.IsNaN t then 
                        []
                    else
                        let rem = Polynomial [|-t; 1.0|]
                        let rest, rem = Polynomial.DivideWithRemainder(x, rem) 
                        // rem has to be tiny

                        let rec insert (v : float) (l : list<float>) =
                            match l with
                                | [] -> [v]
                                | h::rest ->
                                    if h < v then h :: insert v rest
                                    else v :: h :: rest

                        if t >= min && t <= max then
                            rest.ComputeRealRoots(min, max, eps) |> insert t
                        else
                            rest.ComputeRealRoots(min, max, eps)
                
        member x.ComputeRealRoots(eps) =
            x.ComputeRealRoots(-infinity, infinity, eps)

        member private x.ComputeExtremaCandidates(min : float, max : float) =
            let derivative = x.Derivative
            let derivativeRoots = derivative.ComputeRealRoots(min, max, 1.0E-5)
            match Double.IsInfinity min, Double.IsInfinity max with
                | true,  true  -> derivativeRoots
                | false, true  -> min::derivativeRoots
                | true,  false -> max::derivativeRoots
                | false, false -> min::max::derivativeRoots

        member x.ComputeMinimum(min : float, max : float) =
            let x = x
            let candidates = x.ComputeExtremaCandidates(min, max)

            match candidates with
                | [] -> nan
                | _ ->
                    let (bestT, bestValue) = 
                        candidates 
                            |> List.map (fun v -> v, x.Evaluate v)
                            |> List.minBy snd

                    bestT

        member x.ComputeMaximum(min : float, max : float) =
            let x = x
            let candidates = x.ComputeExtremaCandidates(min, max)

            let (bestT, bestValue) = 
                candidates 
                    |> List.map (fun v -> v, x.Evaluate v)
                    |> List.maxBy snd

            bestT

    

        static member Zero = zero
        static member One = one

        static member IsTiny(p : Polynomial, eps : float) =
            if p.Degree = 0 then 
                true
            elif p.Degree = 1 then
                Fun.IsTiny(p.Coefficients.[0], eps)
            else
                false
        
        static member IsTiny(p : Polynomial) =
            if p.Degree = 0 then 
                true
            elif p.Degree = 1 then
                Fun.IsTiny(p.Coefficients.[0])
            else
                false 

        static member Exp(x : int) =
            let arr = Array.zeroCreate (x + 1)
            arr.[x] <- 1.0
            arr |> Polynomial

        static member inline (~-) (r : Polynomial) =
            r.Coefficients |> Array.map (~-) |> Polynomial


        static member inline (+) (l : Polynomial, r : Polynomial) =
            let l = l.Coefficients
            let r = r.Coefficients
            let res = Array.zeroCreate (max l.Length r.Length)
            
            for i in 0..l.Length-1 do
                res.[i] <- l.[i]
            for i in 0..r.Length-1 do
                res.[i] <- res.[i] + r.[i]

            res |> Polynomial

        static member inline (+) (l : Polynomial, r : float) =
            if l.Degree > 0 then
                let res = l.Coefficients |> Array.copy
                res.[0] <- res.[0] + r
                res |> Polynomial
            else
                [|r|] |> Polynomial

        static member inline (+) (l : float, r : Polynomial) =
            r + l


        static member inline (-) (l : Polynomial, r : Polynomial) =
            let l = l.Coefficients
            let r = r.Coefficients
            let res = Array.zeroCreate (max l.Length r.Length)
            
            for i in 0..l.Length-1 do
                res.[i] <- l.[i]
            for i in 0..r.Length-1 do
                res.[i] <- res.[i] - r.[i]

            res |> Polynomial

        static member inline (-) (l : Polynomial, r : float) =
            if l.Degree > 0 then
                let res = l.Coefficients |> Array.copy
                res.[0] <- res.[0] - r
                res |> Polynomial
            else
                [|-r|] |> Polynomial

        static member inline (-) (l : float, r : Polynomial) =
            l + (-r)


        static member inline (*) (l : Polynomial, r : float) =
            l.Coefficients |> Array.map (fun c -> c * r) |> Polynomial

        static member inline (*) (l : float, r : Polynomial) =
            r.Coefficients |> Array.map (fun c -> c * l) |> Polynomial

        static member inline (*) (l : Polynomial, r : Polynomial) =
            let l = l.Coefficients
            let r = r.Coefficients

            let res = Array.zeroCreate (l.Length + r.Length)

            for i in 0..l.Length-1 do
                for j in 0..r.Length-1 do
                    let ij = i + j
                    res.[ij] <- res.[ij] + l.[i] * r.[j]

            res |> Polynomial


        static member inline (/) (l : Polynomial, r : float) =
            l.Coefficients |> Array.map (fun c -> c / r) |> Polynomial

        static member DivideWithRemainder (l : Polynomial, r : Polynomial) : Polynomial * Polynomial =
            if r.Degree > l.Degree then
                failwithf "[Polynomial] cannot divide polynomials of degree %d and %d" l.Degree r.Degree
        
            let get (i : int) (p : float[]) = p.[i]
            let highest (p : Polynomial) = p.Coefficients.[p.Coefficients.Length-1]
             
            let mutable current = l 
            let mutable result = zero
          
            while current.Degree >= r.Degree do
                let factor = 
                    let l = current.Degree - r.Degree
                    let arr = Array.zeroCreate (1 + l)
                    arr.[l] <- highest current / highest r
                    Polynomial arr

                current <- current - factor * r
                result <- result + factor

            result, current

        static member inline (/) (l : Polynomial, r : Polynomial) =
            let (res, rem) = Polynomial.DivideWithRemainder(l, r)

            if rem.Degree <> 0 then
                failwithf "[Polynomial] division had remainder: %A" rem

            res

        static member inline Pow(l : Polynomial, f : int) =
            let mutable res = Polynomial.One
            for i in 1..f do
                res <- res * l
            res        


        member private x.AsString =
            let strs = 
                c |> Seq.mapi (fun i a ->
                    if a = 0.0 then
                        None
                    else
                        if i = 0 then sprintf "%g" a |> Some
                        elif i = 1 then 
                            if a = 1.0 then "t" |> Some
                            elif a = -1.0 then "-t" |> Some
                            else sprintf "%gt" a |> Some
                        else 
                            if a = 1.0 then sprintf "t^%d" i |> Some
                            elif a = -1.0 then sprintf "-t^%d" i |> Some
                            else sprintf "%gt^%d" a i |> Some
                    )
                |> Seq.choose id
                |> Seq.toList
                |> List.rev

            let mutable str = ""
            for s in strs do
                if str.Length > 0 then
                    if s.StartsWith "-" then
                        str <- str + " - " + s.Substring(1)
                    else
                        str <- str + " + " + s
                else
                    str <- s
            str

        override x.ToString() = x.AsString

        override x.GetHashCode() =
            let l = c.Length
            if l = 0 then 0
            elif l = 1 then c.[0].GetHashCode()
            elif l = 2 then HashCode.Combine(c.[0].GetHashCode(), c.[1].GetHashCode())
            else  
                let hashes = c |> Seq.skip 2 |> Seq.map (fun v -> v.GetHashCode()) |> Seq.toArray
                HashCode.Combine(c.[0].GetHashCode(), c.[1].GetHashCode(), hashes)

        override x.Equals o =
            match o with
                | :? Polynomial as o -> c = o.Coefficients
                | _ -> false

        new(c) = Polynomial((), pruneTrailingZeros c)

    [<Struct; CustomEquality; NoComparison; StructuredFormatDisplay("{AsString}")>]
    type Polynomial2d private(u : unit, c : V2d[]) =
        static let pruneTrailingZeros (c : V2d[]) =
            let l = c.Length
            let mutable len = l
            while len > 0 && c.[len-1] = V2d.Zero do
                len <- len - 1

            if len <> l then
                Array.take len c
            else
                c

        static let zero = Polynomial2d [|V2d.Zero|]

        member x.Coefficients = c
        member x.Degree = c.Length

        member x.Evaluate(t : float) =
            let mutable tx = 1.0
            let mutable res = V2d.Zero
            for ci in c do
                res <- res + ci * tx
                tx <- tx * t

            res

        member x.Derivative =
            let degree = c.Length
            if degree <= 1 then zero
            else
                let res = Array.zeroCreate (degree - 1)
                for i in 0..degree-2 do
                    res.[i] <- c.[i + 1] * float (i + 1)
                res |> Polynomial2d

        member x.X =
            c |> Array.map (fun v -> v.X) |> Polynomial

        member x.Y = 
            c |> Array.map (fun v -> v.Y) |> Polynomial

        member x.Dot(other : Polynomial2d) =
            x.X * other.X + x.Y * other.Y



        static member Zero = zero


        static member Bezier2(p0 : V2d, p1 : V2d, p2 : V2d) =
            
            Polynomial2d [|
                p0
                -2.0*p0 + 2.0*p1
                p0 - 2.0*p1 + p2
            |]

        static member Bezier3(p0 : V2d, p1 : V2d, p2 : V2d, p3 : V2d) =
            if p1.ApproxEqual(p2, 1.0E-5) then
                Polynomial2d.Bezier2(p0, p1, p3)
            else
                Polynomial2d [|
                    p0
                    3.0 * (p1 - p0)
                    3.0 * (p0 - 2.0 * p1 + p2)
                    p3 + 3.0 * (p1 - p2) - p0
                |]

        static member inline (~-) (r : Polynomial2d) =
            r.Coefficients |> Array.map (~-) |> Polynomial2d


        static member inline (+) (l : Polynomial2d, r : Polynomial2d) =
            let l = l.Coefficients
            let r = r.Coefficients
            let res = Array.zeroCreate (max l.Length r.Length)
            
            for i in 0..l.Length-1 do
                res.[i] <- l.[i]
            for i in 0..r.Length-1 do
                res.[i] <- res.[i] + r.[i]

            res |> Polynomial2d

        static member inline (+) (l : Polynomial2d, r : V2d) =
            if l.Degree > 0 then
                let res = l.Coefficients |> Array.copy
                res.[0] <- res.[0] + r
                res |> Polynomial2d
            else
                [|r|] |> Polynomial2d

        static member inline (+) (l : V2d, r : Polynomial2d) =
            r + l


        static member inline (-) (l : Polynomial2d, r : Polynomial2d) =
            let l = l.Coefficients
            let r = r.Coefficients
            let res = Array.zeroCreate (max l.Length r.Length)
            
            for i in 0..l.Length-1 do
                res.[i] <- l.[i]
            for i in 0..r.Length-1 do
                res.[i] <- res.[i] - r.[i]

            res |> Polynomial2d

        static member inline (-) (l : Polynomial2d, r : V2d) =
            if l.Degree > 0 then
                let res = l.Coefficients |> Array.copy
                res.[0] <- res.[0] - r
                res |> Polynomial2d
            else
                [|-r|] |> Polynomial2d

        static member inline (-) (l : V2d, r : Polynomial2d) =
            l + (-r)


        static member inline (*) (l : Polynomial2d, r : float) =
            l.Coefficients |> Array.map (fun c -> c * r) |> Polynomial2d

        static member inline (*) (l : float, r : Polynomial2d) =
            r.Coefficients |> Array.map (fun c -> c * l) |> Polynomial2d

        static member inline (*) (l : Polynomial2d, r : Polynomial) =
            let l = l.Coefficients
            let r = r.Coefficients

            let res = Array.zeroCreate (l.Length + r.Length)

            for i in 0..l.Length-1 do
                for j in 0..r.Length-1 do
                    let ij = i + j
                    res.[ij] <- res.[ij] + l.[i] * r.[j]

            res |> Polynomial2d

        static member inline (*) (l : Polynomial, r : Polynomial2d) =
            r * l

        static member inline Dot(l : Polynomial2d, r : Polynomial2d) =
            l.Dot(r)


        static member inline (/) (l : Polynomial2d, r : float) =
            l.Coefficients |> Array.map (fun c -> c / r) |> Polynomial2d


        member private x.AsString =
            c |> Seq.mapi (fun i a ->
                if a = V2d.Zero then
                    None
                else
                    if i = 0 then sprintf "%A" a |> Some
                    elif i = 1 then 
                        sprintf "%A*t" a |> Some
                    else 
                        sprintf "%A*t^%d" a i |> Some
                )
            |> Seq.choose id
            |> Seq.toList
            |> List.rev
            |> String.concat " + "
        

        override x.ToString() = x.AsString

        override x.GetHashCode() =
            let l = c.Length
            if l = 0 then 0
            elif l = 1 then c.[0].GetHashCode()
            elif l = 2 then HashCode.Combine(c.[0].GetHashCode(), c.[1].GetHashCode())
            else  
                let hashes = c |> Seq.skip 2 |> Seq.map (fun v -> v.GetHashCode()) |> Seq.toArray
                HashCode.Combine(c.[0].GetHashCode(), c.[1].GetHashCode(), hashes)

        override x.Equals o =
            match o with
                | :? Polynomial2d as o -> c = o.Coefficients
                | _ -> false
    
        new(c) = Polynomial2d((), pruneTrailingZeros c)

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Polynomial = 

        let inline ofList (l : list<float>) = l |> List.toArray |> Polynomial
        let inline evaluate (t : float) (p : Polynomial) = p.Evaluate t
        let inline derivative (p : Polynomial) = p.Derivative
        let inline realRoots (p : Polynomial) = p.ComputeRealRoots(1.0E-5)
        let inline realRootsInRange (min : float) (max : float) (p : Polynomial) = p.ComputeRealRoots(min, max, 1.0E-5)
        let inline minimum (min : float) (max : float) (p : Polynomial) = p.ComputeMinimum(min, max)
        let inline maximum (min : float) (max : float) (p : Polynomial) = p.ComputeMaximum(min, max)

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Polynomial2d = 

        let inline ofList (l : list<V2d>) = l |> List.toArray |> Polynomial2d
        let inline evaluate (t : float) (p : Polynomial2d) = p.Evaluate t
        let inline derivative (p : Polynomial2d) = p.Derivative

             
type PathComponent =
    | Line of V2d * V2d
    | Poly of Box2d * Polynomial2d

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module PathComponent =
    let getDistanceAndT (point : V2d) (comp : PathComponent) =
        match comp with
            | Line(p0,p1) ->
                let mutable t = 0.0
                let p = Line2d(p0, p1).GetClosestPointOn(point, &t)
                let d = V2d.Distance(point, p)
                d, t

            
            | Poly(_,p) ->
                let d2 =
                    let d = p - point
                    d.Dot(d)

                let t = d2.ComputeMinimum(0.0, 1.0)
                if Double.IsNaN t then
                    infinity, 0.0
                else
                    let d = p.Evaluate(t) - point |> Vec.length
                    d, t

    let getPoint (t : float) (comp : PathComponent) =
        match comp with
            | Line(p0, p1) -> 
                (1.0 - t) * p0 + t * p1

            | Poly(_,p) -> 
                p.Evaluate(t)

    let getTangent (t : float) (comp : PathComponent) =
        match comp with
            | Line(p0, p1) -> 
                p1 - p0 |> Vec.normalize

            | Poly(_,p) ->
                p.Derivative.Evaluate(t) |> Vec.normalize
               
    let getNormal (t : float) (comp : PathComponent) =
        let t = getTangent t comp
        V2d(-t.Y, t.X)


    let line (p0 : V2d) (p1 : V2d) =
        Line(p0, p1)

    let bezier2 (p0 : V2d) (p1 : V2d) (p2 : V2d) =
        let u = p1 - p0 |> Vec.normalize
        let v = p2 - p1 |> Vec.normalize

        if Vec.dot u v > 0.999 then
            Line(p0, p2)
        else
            let bounds = Box2d(p0, p1, p2)
            Poly(bounds, Polynomial2d.Bezier2(p0, p1, p2))

    let bezier3 (p0 : V2d) (p1 : V2d) (p2 : V2d) (p3 : V2d) =
        let bounds = Box2d(p0, p1, p2, p3)
        Poly(bounds, Polynomial2d.Bezier3(p0, p1, p2, p3))


type Glyph = { path : list<PathComponent>; bounds : Box2d }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Glyph =
    open System.Drawing
    open System.Drawing.Drawing2D
    open System.Collections.Generic

    [<Flags>]
    type PathPointType =
        | Start           = 0uy
        | Line            = 1uy
        | Bezier          = 3uy
        | PathTypeMask    = 0x07uy
        | DashMode        = 0x10uy
        | PathMarker      = 0x20uy
        | CloseSubpath    = 0x80uy

    let ofChar (f : Font) (c : char) : Glyph =
        use p = new GraphicsPath()
        let size = 1000.0f

        p.AddString(String(c, 1), f.FontFamily, int f.Style, size, PointF(0.0f, 0.0f), StringFormat.GenericDefault)

        let types = p.PathTypes
        let points = p.PathPoints |> Array.map (fun p -> 0.8 * (V2d(p.X, p.Y) / (float size)) + 0.1)

        
        let mutable start = V2d.NaN
        let currentPoints = List<V2d>()
        let components = List<PathComponent>()

        let bounds = Box2d(points)

        for (p, t) in Array.zip points types do
            let t = t |> unbox<PathPointType>

            let close = t &&& PathPointType.CloseSubpath <> PathPointType.Start



            match t &&& PathPointType.PathTypeMask with
                | PathPointType.Line ->
                    if currentPoints.Count > 0 then
                        let last = currentPoints.[currentPoints.Count - 1]
                        components.Add(PathComponent.line last p)
                        currentPoints.Clear()

                    currentPoints.Add p



                | PathPointType.Bezier ->
                    currentPoints.Add p
                    if currentPoints.Count >= 4 then
                        let p0 = currentPoints.[0]
                        let p1 = currentPoints.[1]
                        let p2 = currentPoints.[2]
                        let p3 = currentPoints.[3]
                        components.Add(PathComponent.bezier3 p0 p1 p2 p3)
                        currentPoints.Clear()
                        currentPoints.Add p3

                | PathPointType.Start | _ ->
                    currentPoints.Add p
                    start <- p
                    ()

            if close then
                components.Add(PathComponent.line p start)

                currentPoints.Clear()
                start <- V2d.NaN
                        


            printfn "%A: %A" t p

            ()


        

        { path = components |> CSharpList.toList; bounds = Box2d(V2d.Zero, V2d.II) }



module PathComponentTest =
    let draw (path : list<PathComponent>) =
        let image = PixImage<float32>(Col.Format.RGB, V2i.II * 1024)
        let mat = image.GetMatrix<C3f>()

        Log.startTimed "rasterizer"
        Log.line "size: %A" image.Size
        mat.SetByCoordParallelX(fun (x : int64) (y : int64) ->
            let c = V2l(x,y)
            let coord = (V2d(c) + V2d.Half) / V2d(image.Size)
            let (value, _) = 
                path |> List.map (fun v ->
                            let (d,t) = PathComponent.getDistanceAndT coord v
                            d, (v, t)
                        )
                     |> List.minBy fst

            
            let value = pow value (1.0 / 3.0) |> clamp 0.0 1.0
            C3f(value, value, value)
        ) |> ignore

        Log.stop()


        for comp in path do
            let s = V2d(mat.Size)
            let mutable last = s * PathComponent.getPoint 0.0 comp
            for i in 1..50 do
                let t = float i / 50.0

                let p1 = s * PathComponent.getPoint t comp
                mat.SetLine(last, p1, C3f.Red)
                last <- p1

        image.ToPixImage<byte>(Col.Format.RGBA).SaveAsImage @"C:\Users\schorsch\Desktop\test.png"

    let drawGlyph (c : char) =
        use font = new Font("Times New Roman", 1.0f)
        let g = Glyph.ofChar font c

        let image = PixImage<float32>(Col.Format.RGB, V2i.II * 256)
        let mat = image.GetMatrix<C3f>()

        Log.startTimed "rasterizer"
        Log.line "size: %A" image.Size
        mat.SetByCoordParallelX(fun (x : int64) (y : int64) ->
            let c = V2l(x,y)
            let coord = (V2d(c) + V2d.Half) / V2d(image.Size)

            let colors = [| C3f.Red; C3f.Green; C3f.Blue |]

            let (value, (c, t)) = 
                g.path |> List.mapi (fun i  v ->
                            let (d,t) = PathComponent.getDistanceAndT coord v
                            d, (v, t)
                        )
                     |> List.minBy fst

            let n = PathComponent.getNormal t c
            let p = PathComponent.getPoint t c
            let s = Vec.dot (coord - p) n

            let value = 
                let value = value / 0.05
                let v = clamp 0.0 1.0 value
                if s > 0.0 then 
                    0.5 * v + 0.5
                else
                    0.5 - 0.5 * v


            //let value = pow value (1.0 / 3.0)
            
            
            
            C3f.White * value
        ) |> ignore

        Log.stop()

//        let rand = Random()
//        let randomColor() = C3f(rand.NextDouble(), rand.NextDouble(), rand.NextDouble())
//        for comp in g.path do
//            let s = V2d(mat.Size)
//            let mutable last = s * PathComponent.getPoint 0.0 comp
//            let color = randomColor()
//            for i in 1..100 do
//                let t = float i / 100.0
//
//                let p1 = s * PathComponent.getPoint t comp
//                mat.SetLine(last, p1, color)
//                last <- p1

        image.ToPixImage<byte>(Col.Format.RGBA).SaveAsImage @"C:\Users\schorsch\Desktop\glyph.png"


    let testRoots() =
        let rand = Random()

        let iter() =
            let roots = List.init 5 (fun _ -> rand.NextDouble()) |> List.sort
            let poly = roots |> List.map (fun v -> (Polynomial([|0.0; 1.0|]) - v)) |> List.fold (*) Polynomial.One

            let eps = 1.0E-5
            let found = poly.ComputeRealRoots(0.0, 1.0, eps)

            let rec check (real : list<float>) (found : list<float>) =
                match real, found with
                    | [], [] -> ()
                    | r::real, f::found -> 
                        if Fun.ApproximateEquals(r, f, 1.0E-2) then
                            check real found
                        else 
                            Log.line "bad: %g vs. %g" r f
                            check real found
                    | r, [] -> Log.line "did not find roots: %A" r
                    | [], f -> Log.line "found extra roots: %A" f

            Log.start "%A" poly
            check roots found
            Log.stop()

        for i in 1..1000 do
            iter()

    let test() =

        drawGlyph '&'
//
//        let path =
//            let min = 0.2
//            let max = 0.8
//            [
//                PathComponent.line      (V2d(min, min))     (V2d(max, min))
//                PathComponent.line      (V2d(max, min))     (V2d(max, max))
//                PathComponent.bezier3   (V2d(max, max))     (V2d(min, max))     (V2d(max, min))     (V2d(min,min))
//            ]
//
//        draw path














