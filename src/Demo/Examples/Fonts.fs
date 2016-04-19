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

             
type PathComponent =
    | Line of V2d * V2d
    | Poly of Box2d * Polynomial2d

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module PathComponent =
    let minT = -0.0000
    let maxT =  1.0000

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

    let inside (point : V2d) (path : list<PathComponent>) =
        let mutable left = 0
        let mutable right = 0
        let mutable up = 0
        let mutable down = 0

        for c in path do
            
            let poly= 
                match c with
                    | Line(p0,p1) -> Polynomial2d [|p0; p1 - p0|]
                    | Poly(_,p) -> p


            let p = poly - point
            let px = p.X
            let py = p.Y

            let xRoots = px.ComputeRealRoots(0.0, 1.0, 1.0E-8)
            for r in xRoots do
                let pp = py.Evaluate r
                if pp > 0.0 then up <- up + 1
                elif pp < 0.0 then down <- down + 1

            let yRoots = py.ComputeRealRoots(0.0, 1.0, 1.0E-8)
            for r in yRoots do
                let pp = px.Evaluate r
                if pp > 0.0 then right <- right + 1
                elif pp < 0.0 then left <- left + 1

        left % 2 <> 0 || right % 2 <> 0 || up % 2 <> 0 || down % 2 <> 0

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
                if not (V2d.ApproxEqual(p, start)) then
                    components.Add(PathComponent.line p start)

                currentPoints.Clear()
                start <- V2d.NaN
                        


            printfn "%A: %A" t p

            ()


        

        { path = components |> CSharpList.toList; bounds = Box2d(V2d.Zero, V2d.II) }

    type Segment =
        | Straight of V2d * V2d
        | Bezier2 of V2d * V2d * V2d
        | Bezier3 of V2d * V2d * V2d * V2d





    let geometry (f : Font) (c : char) : IndexedGeometry =
        use path = new GraphicsPath()
        let size = 1.0f

        path.AddString(String(c, 1), f.FontFamily, int f.Style, size, PointF(0.0f, 0.0f), StringFormat.GenericDefault)
        
        

        let types = path.PathTypes
        let points = path.PathPoints |> Array.map (fun p -> V2d(p.X, p.Y))

        let otherTriangles = List<bool * Triangle2d>()
        let otherTexCoords = List<V3d>()

        let mutable start = V2d.NaN
        let currentPoints = List<V2d>()
        let innerPoints = List<List<V2d>>()
        let lines = List<Line2d>()

        let last() = innerPoints.[innerPoints.Count-1]
        
        // http://www.msr-waypoint.net/en-us/um/people/cloop/LoopBlinn05.pdf
        let texCoords(p0 : V2d, p1 : V2d, p2 : V2d, p3 : V2d) =
            let p0 = V3d(p0, 1.0)
            let p1 = V3d(p1, 1.0)
            let p2 = V3d(p2, 1.0)
            let p3 = V3d(p3, 1.0)

            let M3 =
                M44d(
                     +1.0,   0.0,  0.0,  0.0,
                     -3.0,   3.0,  0.0,  0.0,
                     +3.0,  -6.0,  3.0,  0.0,
                     -1.0,   3.0, -3.0,  1.0
                )

            let M3Inverse = M3.Inverse
//                M44d(
//                     1.0,   0.0,        0.0,        0.0,
//                     1.0,   1.0/3.0,    0.0,        0.0,
//                     1.0,   2.0/3.0,    1.0/3.0,    0.0,
//                     1.0,   1.0,        1.0,        1.0
//                )

            let v0 =  1.0*p0
            let v1 = -3.0*p0 + 3.0*p1 
            let v2 =  3.0*p0 - 6.0*p1 + 3.0*p2
            let v3 = -1.0*p0 + 3.0*p1 - 3.0*p2 + 1.0*p3

            let det (r0 : V3d) (r1 : V3d) (r2 : V3d) =
                M33d.FromRows(r0, r1, r2).Det

            let d0 =  (det v3 v2 v1)
            let d1 = -(det v3 v2 v0)
            let d2 =  (det v3 v1 v0)
            let d3 = -(det v2 v1 v0)


            let O(a : V3d,b : V3d,c : V3d,d : V3d) =
                V3d(-a.X, -a.Y, a.Z),
                V3d(-b.X, -b.Y, b.Z),
                V3d(-c.X, -c.Y, c.Z),
                V3d(-d.X, -d.Y, d.Z)



            let zero v = Fun.IsTiny(v, 1.0E-5)
            let nonzero v = Fun.IsTiny(v, 1.0E-5) |> not

            let v = 3.0 * d2 * d2 - 4.0*d1*d3
            let d1z = zero d1
            let d1nz = d1z |> not


            // 1. The Serpentine
            // 3a. Cusp with inflection at infinity
            if d1nz && v >= 0.0 then
                // serpentine
                // Cusp with inflection at infinity

                let r = sqrt((3.0*d2*d2 - 4.0*d1*d3) / 3.0)
                let tl = d2 + r
                let sl = 2.0 * d1
                let tm = d2 - r
                let sm = sl


                let F =
                    M44d(
                         tl * tm,            tl * tl * tl,           tm * tm * tm,       1.0,
                        -sm*tl - sl*tm,     -3.0*sl*tl*tl,          -3.0*sm*tm*tm,       0.0,
                         sl*sm,              3.0*sl*sl*tl,           3.0*sm*sm*tm,       0.0,
                         0.0,               -sl*sl*sl,              -sm*sm*sm,           0.0
                    )

                let weights = M3Inverse * F

                let w0 = weights.R0.XYZ
                let w1 = weights.R1.XYZ
                let w2 = weights.R2.XYZ
                let w3 = weights.R3.XYZ

                let res = w0, w1, w2, w3
                if d1 < 0.0 then O res
                else res

            // 2. The Loop
            elif d1nz && v < 0.0 then
                // loop

                let r = sqrt(4.0 * d1 * d3 - 3.0*d2*d2)

                let td = d2 + r
                let sd = 2.0*d1

                let te = d2 - r
                let se = sd


                let F =
                    M44d(
                         td*te,               td*td*te,                   td*te*te,                       1.0,
                        -se*td - sd*te,      -se*td*td - 2.0*sd*te*td,   -sd*te*te - 2.0*se*td*te,        0.0,
                         sd * se,             te*sd*sd + 2.0*se*td*sd,    td*se*se + 2.0*sd*te*se,        0.0,
                         0.0,                -sd*sd*se,                  -sd*se*se,                       0.0
                    )

                let weights = M3Inverse * F

                let w0 = weights.R0.XYZ
                let w1 = weights.R1.XYZ
                let w2 = weights.R2.XYZ
                let w3 = weights.R3.XYZ

                let res = w0, w1, w2, w3
                if d1 < 0.0 then O res
                else res

            // 4. Quadratic
            elif zero d1 && zero d2 && nonzero d3 then
                let w0 = V3d(0.0,0.0,0.0)
                let w1 = V3d(1.0/3.0,0.0,1.0/3.0)
                let w2 = V3d(2.0/3.0,1.0/3.0,2.0/3.0)
                let w3 = V3d(1.0,1.0,1.0)


                let res = w0,w1,w2,w3
                if d3 < 0.0 then O res
                else res

            // 3b. Cusp with cusp at infinity
            elif d1z && zero v then
                let tl = d3
                let sl = 3.0*d2
                let tm = 1.0
                let sm = 0.0


                let F =
                    M44d(
                         tl,     tl*tl*tl,       1.0, 1.0,
                        -sl,    -3.0*sl*tl*tl,   0.0, 0.0,
                        0.0,     3.0*sl*sl*tl,   0.0, 0.0,
                        0.0,    -sl*sl*sl,       0.0, 0.0
                    )

                let weights = M3Inverse * F
                let w0 = weights.R0.XYZ
                let w1 = weights.R1.XYZ
                let w2 = weights.R2.XYZ
                let w3 = weights.R3.XYZ

                w0, w1, w2, w3


            elif Fun.IsTiny d1 && Fun.IsTiny d2 && Fun.IsTiny d3 then
                failwith "line or point"


            else
                failwith "not possible"


        


        for (p, t) in Array.zip points types do
            let t = t |> unbox<PathPointType>

            let close = t &&& PathPointType.CloseSubpath <> PathPointType.Start



            match t &&& PathPointType.PathTypeMask with
                | PathPointType.Line ->
                    if currentPoints.Count > 0 then
                        let last = currentPoints.[currentPoints.Count - 1]
                        currentPoints.Clear()
                        lines.Add(Line2d(last, p))

                    currentPoints.Add p
                    last().Add(p)



                | PathPointType.Bezier ->
                    currentPoints.Add p
                    if currentPoints.Count >= 4 then
                        let p0 = currentPoints.[0]
                        let p1 = currentPoints.[1]
                        let p2 = currentPoints.[2]
                        let p3 = currentPoints.[3]


                        let w0,w1,w2,w3 = texCoords(p0, p1, p2, p3)

                        last().Add(p0)
                        let p1Inside = p1.PosLeftOfLineValue(p0, p3) > 0.0
                        let p2Inside = p2.PosLeftOfLineValue(p0, p3) > 0.0

                        if p1Inside && p2Inside then 
                            last().Add(p1); 
                            last().Add(p2)

                            lines.Add(Line2d(p0, p1))
                            lines.Add(Line2d(p1, p2))
                            lines.Add(Line2d(p2, p3))
                        else
                            lines.Add(Line2d(p0, p3))

                        otherTriangles.Add(p1Inside, Triangle2d(p0, p1, p2))
                        otherTexCoords.AddRange [w0; w1; w2]

                        otherTriangles.Add(p2Inside, Triangle2d(p0, p2, p3))
                        otherTexCoords.AddRange [w0; w2; w3]

                        last().Add(p3)

                        currentPoints.Clear()
                        currentPoints.Add p3

                | PathPointType.Start | _ ->
                    currentPoints.Add p
                    innerPoints.Add(List())
                    last().Add p
                    start <- p
                    ()

            if close then
                if start <> p then
                    lines.Add(Line2d(p, start))

                last().Add(start)
                currentPoints.Clear()
                start <- V2d.NaN
                        


            printfn "%A: %A" t p

            ()





        let innerPoints = innerPoints |> CSharpList.map CSharpList.toArray



//        let a = [|V2d(0.0, 0.0); V2d(1.0,0.0); V2d(1.0,1.0); V2d(0.0,1.0); V2d(0.0,0.0)|]
//        let b = [|V2d(0.9,0.9); V2d(0.9,0.1); V2d(0.1,0.1); V2d(0.1,0.9); V2d(0.9,0.9)|]
//        
        let mergePolys(a : V2d[]) (b : V2d[]) =

            let distances =
                seq {
                    for i in 0..a.Length-1 do
                        for j in 0..b.Length-1 do
                            let a = a.[i]
                            let b = b.[j]
                            yield V2d.Distance(a,b), (i,j)
                }

            let (i,j) = distances |> Seq.minBy fst |> snd

            let bperm =
                if j = 0 then b
                else Array.append (Array.skip j b) (Array.sub b 1 j)



            match i with
                | 0 -> 
                    // a0 ... an,a0, b0 ... bn,b0, a0
                    Array.concat [ a; bperm; [|a.[0]|] ]
                | i -> 
                    // a0 ... ai, b0 ... bn, b0, ai ... an, a0
                    Array.concat [ Array.take (i+1) a; bperm; Array.skip i a; [|a.[0]|] ]
//
//            let pt = a.[a.Length-1]
//            let closest = b |> Seq.mapi (fun i v -> i,V2d.Distance(v,pt)) |> Seq.minBy snd |> fst
//            let b = 
//                if closest = 0 then
//                    Array.append b [|a.[0]|]
//                else
//                    let start = Array.skip closest b
//                    let rest = Array.sub b 1 closest
//                    Array.concat [ [|rest.[rest.Length-1]|]; start; rest; [|a.[0]|] ]
//
//            Array.append a b

        let polygons = List<V2d[]>()
        for points in innerPoints do
            let p = Polygon2d points
            let w = p.ComputeWindingNumber()

            if polygons.Count > 0 && p.IsFullyContainedInside(Polygon2d polygons.[polygons.Count-1]) then
                let last = polygons.[polygons.Count-1]
                polygons.[polygons.Count-1] <- mergePolys last points
            else
                polygons.Add(points)


        let insidePoints =
            polygons 
            |> CSharpList.toArray
            |> Array.collect (fun p ->
                let poly = Polygon3d(p |> Array.map (fun v -> V3d(v, 0.0)))
                let poly = poly.WithoutMultiplePoints()
                let index = poly.ComputeTriangulationOfConcavePolygon(1.0E-7)
                

                index |> Array.map (fun i -> 0, poly.[i])
            )

        let t = otherTriangles |> Seq.collect (fun (i,t) -> [ i,t.P0; i,t.P1; i,t.P2 ]) |> Seq.map (fun (i,v) -> 1, V3d(v.X, v.Y, 0.0)) |> Seq.toArray
        let tc = otherTexCoords |> Seq.toArray

        let pos = Array.append insidePoints t
        let tex = Array.append (Array.create insidePoints.Length V3d.Zero) tc


        let positions = pos |> Array.map snd |> Array.map V3f.op_Explicit
        let texCoords = tex |> Array.map V3f.op_Explicit
        let colors = pos |> Array.map (fst >> float32)
        
        IndexedGeometry(
            Mode = IndexedGeometryMode.TriangleList,
            IndexedAttributes =
                SymDict.ofList [
                    DefaultSemantic.Positions, positions :> Array
                    Symbol.Create "KLMN", texCoords :> Array
                    Symbol.Create "TriangleKind", colors :> Array
                ]
            )
            
        



module PathComponentTest =
    open Aardvark.Application
    open Aardvark.Application.WinForms
    open Aardvark.Base.Incremental
    open Aardvark.SceneGraph
    open FShade


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


    let drawDistance (size : V2i) (path : list<PathComponent>) =

        let floatImage = PixImage<float32>(Col.Format.RGB, size)
        let mutable mat = floatImage.GetMatrix<C3f>()

        mat.ForeachIndex(fun (x : int64) (y : int64) (i : int64) ->
            let c = V2l(x,y)
            let coord = (V2d(c) + V2d.Half) / V2d(mat.Size)

            let mutable cnt = 0

            
            let crease = 10.0
            let dirEquals (a : V2d) (b : V2d) = 
                let angle = a.Dot(b)  |> acos
                if angle * Constant.DegreesPerRadian < crease then
                    true
                else
                    false

            let rec assignColors (lastNormal : V2d) (currentColor : int) (path : list<PathComponent>) =
                match path with
                    | [] -> []
                    | h::rest ->
                        let start = PathComponent.getNormal 0.0 h
                        let color = 
                            if dirEquals start lastNormal then currentColor
                            else (currentColor + 1) % 3

                        (color, h) :: assignColors (PathComponent.getNormal 1.0 h) color rest


            let mutable used = Set.empty
            let mutable result = V3d.Zero
            let mutable distance = 1.0

            let inside = PathComponent.inside coord path
            let mutable cnt = 0

            let mutable closest = Map.empty //System.Collections.Generic.SortedList<float, bool * int * PathComponent>()

            for (color, c) in assignColors V2d.Zero 0 path do
                let (d,t) = c |> PathComponent.getDistanceAndT coord
                let d = d / 0.08
                let d = clamp 0.0 1.0 d

                let n = c |> PathComponent.getNormal t
                let p = c |> PathComponent.getPoint t

                let left = Vec.dot n (coord - p) > 0.0

                closest <- 
                    match Map.tryFind d closest with
                        | Some v -> Map.add d ((left, color, c) :: v) closest
                        | None -> Map.add d [(left, color, c)] closest


            
            let mutable used = Set.empty
            let mutable result = V3d.Zero


            let rec fill (result : V3d) (entries : list<float * (bool * int * PathComponent)>) (used : Set<int>) =
                match entries with
                    | [] -> 
                        result

                    | (d, (left, color, c))::rest ->
                        if Set.contains color used || (left && not inside) then
                            fill result rest used
                        else
                            let mutable r = result
                            r.[color] <- if left then 0.5 + 0.5*d else 0.5 - 0.5*d
                            fill r rest (Set.add color used)

            let result = fill V3d.III (closest |> Seq.collect (fun (KeyValue(k,v)) -> v |> Seq.map (fun v -> k,v)) |> Seq.toList) Set.empty

            mat.[i] <- C3f result

        )



        floatImage.ToPixImage<byte>(Col.Format.RGBA)

    let drawGlyph (size : V2i) (c : char) =
        use font = new Font("Consolas", 1.0f)
        let g = Glyph.ofChar font c

        drawDistance size g.path



    let testRoots() =
        let rand = Random()

        let iter() =
            let roots = List.init 5 (fun _ -> rand.NextDouble()) |> List.sort
            let poly = roots |> List.map (fun v -> (Polynomial([|0.0; 1.0|]) - v)) |> List.fold (*) Polynomial.One

            let eps = 1.0E-7
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

    module Shader = 
        type Vertex =
            {
                [<Position>] p : V4d
                [<Semantic("KLMN")>] tc : V3d
                [<Semantic("TriangleKind")>] kind : float
            }

        let sdf (v : Vertex) =
            fragment {
                let s : bool = uniform?Filled

                if v.kind = 0.0 then
                    return V4d.IIII
                else
                    let k = v.tc.X
                    let l = v.tc.Y
                    let m = v.tc.Z
                    if s then
                        if pow k 3.0 > l*m then
                            return V4d.OOOO
                        else
                            return V4d.IIII
                    else
                        return V4d.IOOI


            }

    let test() =
        Ag.initialize()
        Aardvark.Init()

        use app = new OpenGlApplication()
        let win = app.CreateSimpleRenderWindow(1)
        win.Text <- "Aardvark rocks \\o/"

        let view = CameraView.LookAt(V3d(2.0,2.0,2.0), V3d.Zero, V3d.OOI)
        let perspective = 
            win.Sizes 
              |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 50.0 (float s.X / float s.Y))


        let viewTrafo = DefaultCameraController.control win.Mouse win.Keyboard win.Time view

        let quadSg =
            let quad =
                IndexedGeometry(
                    Mode = IndexedGeometryMode.TriangleList,
                    IndexArray = ([|0;1;2; 0;2;3|] :> Array),
                    IndexedAttributes =
                        SymDict.ofList [
                            DefaultSemantic.Positions,                  [| V3f(-1,-1,0); V3f(1,-1,0); V3f(1,1,0); V3f(-1,1,0) |] :> Array
                            DefaultSemantic.Normals,                    [| V3f.OOI; V3f.OOI; V3f.OOI; V3f.OOI |] :> Array
                            DefaultSemantic.DiffuseColorCoordinates,    [| V2f.OO; V2f.IO; V2f.II; V2f.OI |] :> Array
                        ]
                )
                
            quad |> Sg.ofIndexedGeometry


        use font = new Font("Times New Roman", 1.0f)



        let filled = Mod.init true


        let sg =
            Glyph.geometry font '§' 
                |> Sg.ofIndexedGeometry
                |> Sg.effect [
                    DefaultSurfaces.trafo |> toEffect
                    Shader.sdf |> toEffect
                  ]
               //|> Sg.diffuseTexture' (PixTexture2d(PixImageMipMap [|image :> PixImage|], true))
               |> Sg.viewTrafo (viewTrafo   |> Mod.map CameraView.viewTrafo )
               |> Sg.projTrafo (perspective |> Mod.map Frustum.projTrafo    )
               |> Sg.fillMode (filled |> Mod.map (fun v -> if v then Aardvark.Base.Rendering.FillMode.Fill else Aardvark.Base.Rendering.FillMode.Line))
               |> Sg.uniform "Filled" filled
               |> Sg.trafo (Trafo3d.Scale(-1.0, 1.0, 1.0) |> Mod.constant)
        win.Keyboard.KeyDown(Keys.R).Values.Add(fun _ ->
            transact (fun () ->
                filled.Value <- not filled.Value
            )
        )


        let task = app.Runtime.CompileRender(win.FramebufferSignature, sg)
        win.RenderTask <- task 
        win.Run()















