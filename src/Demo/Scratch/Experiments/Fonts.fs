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
open Aardvark.Base.Rendering

#nowarn "9"
#nowarn "51"

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
                    let r = -d / k
                    if valid r then [r]
                    else []

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
                            if depth > 1000 then
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
                                    recurse (depth + 1) v better (0.9 * damping)

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
//
//                    // brent's method
//                    if Double.IsNaN t then
//                        match Double.IsInfinity min, Double.IsInfinity max with
//                            | (false, false) -> 
//                                t <- brent x.Evaluate -0.1 1.1
//                            | _ ->
//                                ()
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


//                    // bisect
//                    if Double.IsNaN t then
//                        t <- bisect x.Evaluate


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
            let eps = 1.0E-6
            let derivativeRoots = 
                derivative.ComputeRealRoots(eps)
                    |> List.filter (fun t -> t >= min-eps && t <= max+eps)
   
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














