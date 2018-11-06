namespace ConjugateGradient

open Microsoft.FSharp.Quotations
open Aardvark.Base

[<AutoOpen>]
module private PolynomialHelpers =

    let arr2d (arr : float[][]) =
        let r = arr.Length
        if r = 0 then 
            Array2D.zeroCreate 0 0
        else
            let c = arr.[0].Length
            Array2D.init r c (fun r c -> arr.[r].[c])

    let inline doWhile (cond : unit -> bool) (action : unit -> unit) =
        action()
        while cond() do action()


    let cross<'p, 'c> (num : Real<'c>) (l : hmap<hmap<string * 'p, int>, 'c>) (r : hmap<hmap<string * 'p, int>, 'c>) : hmap<hmap<string * 'p, int>, 'c> =
        let l = l |> HMap.toList
        let r = r |> HMap.toList

        let inline union (_) (l : int) (r : int) =
            l + r

        let mutable res = HMap.empty
        for ((lk, lv), (rk, rv)) in List.allPairs l r do
            let k = HMap.unionWith union lk rk
            let v = num.mul lv rv
            if not (num.isTiny v) then
                res <- HMap.alter k (Option.defaultValue num.zero >> num.add v >> Some) res
        res

    let inline toOption (v : float) =
        if Fun.IsTiny v then None
        else Some v

    module List =
        let inline mulBy (f : 'a -> 'b) (l : list<'a>) =
            let mutable res = LanguagePrimitives.GenericOne
            for e in l do
                res <- res * f e
            res

            
    module Seq =
        let inline mulBy (f : 'a -> 'b) (l : seq<'a>) =
            let mutable res = LanguagePrimitives.GenericOne
            for e in l do
                res <- res * f e
            res

[<StructuredFormatDisplay("{AsString}")>]
type Polynomial<'p, 'c> (coeff : hmap<hmap<string * 'p, int>, 'c>) =

    let names =  
        lazy ( coeff |> HMap.toSeq |> Seq.collect (fst >> HMap.toSeq >> Seq.map (fst >> fst)) |> Set.ofSeq )

    let degrees =
        lazy (
            if HMap.isEmpty coeff then
                HMap.empty
            else
                names.Value |> Seq.map (fun name ->
                    name, lazy (coeff |> HMap.toSeq |> Seq.map (fun (k,_) -> k |> HMap.toSeq |> Seq.filter (fun ((n,_),_) -> n = name) |> Seq.sumBy snd) |> Seq.max)
                )
                |> HMap.ofSeq
        )

    let degree (name : string) = 
        match HMap.tryFind name degrees.Value with
            | Some v -> v.Value
            | None -> 0

    static let num = RealInstances.instance<'c>

    static let (<+>) (l : 'c) (r : 'c) = num.add l r
    static let (<*>) l r = num.mul l r

    static let toOption v = if num.isTiny v then None else Some v

    member x.Degree(name : string) = degree name
    member x.coefficients = coeff

    static member private map (f : 'c -> 'c) (l : Polynomial<'p, 'c>) =
        let merge _ (l : 'c) = 
            let r = f l
            if num.isTiny r then None
            else Some r

        Polynomial(HMap.choose merge l.coefficients)

    static member private map2 (f : 'c -> 'c -> 'c) (l : Polynomial<'p, 'c>) (r : Polynomial<'p, 'c>)=
        let merge _ (l : Option<'c>) (r : Option<'c>) =
            match l with
                | Some l ->
                    match r with
                        | Some r -> f l r |> toOption
                        | None -> f l num.zero |> toOption
                | None ->
                    match r with
                        | Some r -> f num.zero r |> toOption
                        | None -> f num.zero num.zero |> toOption
        Polynomial(HMap.choose2 merge l.coefficients r.coefficients)
        

    static member Parameter(name : string, p : 'p) = Polynomial<'p, 'c> (HMap.ofList [ HMap.ofList [(name,p), 1], num.one ])
    static member Constant v = Polynomial<'p, 'c> (HMap.ofList [ HMap.empty, v ])
    static member Zero = Polynomial<'p, 'c> (HMap.empty)
    static member One = Polynomial<'p, 'c> (HMap.ofList [ HMap.empty, num.one ])
    
    static member (~-) (l : Polynomial<'p, 'c>) = Polynomial<'p, 'c>.map num.neg l
    
    static member (+) (l : Polynomial<'p, 'c>, r : Polynomial<'p, 'c>) = Polynomial<'p, 'c>.map2 num.add l r

    static member (+) (l : 'c, r : Polynomial<'p, 'c>) = 
        if num.isTiny l then
            r
        else
            Polynomial(HMap.alter HMap.empty (fun o -> match o with | None -> Some l | Some r -> toOption (l <+> r)) r.coefficients )

    static member (+) (l : Polynomial<'p, 'c>, r : 'c) = 
        if num.isTiny r then
            l
        else
            Polynomial(HMap.alter HMap.empty (fun o -> match o with | None -> Some r | Some l -> toOption (l <+> r)) l.coefficients )
       

    static member (-) (l : Polynomial<'p, 'c>, r : Polynomial<'p, 'c>) = Polynomial<'p, 'c>.map2 num.sub l r

    static member (-) (l : 'c, r : Polynomial<'p, 'c>) = Polynomial<'p, 'c>.Constant(l) - r
    static member (-) (l : Polynomial<'p, 'c>, r : 'c) = l + Polynomial<'p, 'c>.Constant (num.neg r)

    static member (*) (l : Polynomial<'p, 'c>, r : Polynomial<'p, 'c>) = Polynomial<'p, 'c>(cross num l.coefficients r.coefficients)
      
    static member (*) (l : Polynomial<'p, 'c>, r : 'c) = l * Polynomial<'p, 'c>.Constant r
    static member (*) (l : 'c, r : Polynomial<'p, 'c>) = Polynomial<'p, 'c>.Constant l * r

    static member Pow(l : Polynomial<'p, 'c>, r : int) =
        if r < 0 then failwith "negative exponent"
        elif r = 0 then Polynomial<'p,'c>.Zero
        elif r = 1 then l
        else l * Polynomial<'p,'c>.Pow(l, r - 1)

    override x.ToString() =
        if HMap.isEmpty x.coefficients then
            "0"
        else
            let paramStr (p : hmap<string * 'p, int>) =
                p |> HMap.toSeq |> Seq.map (fun ((name,p),e) -> 
                    let p = 
                        if typeof<'p> = typeof<int> then sprintf "(%A)" p
                        else sprintf "%A" p
                    if e = 1 then
                        sprintf "%s%s" name p
                    else
                        sprintf "%s%s^%d" name p e
                )
                |> String.concat "*"
        
            x.coefficients 
            |> HMap.toSeq 
            |> Seq.sortByDescending (fun (p,_) -> p |> HMap.toSeq |> Seq.sumBy snd)
            |> Seq.mapi (fun i (p,f) ->
                let op, f = 
                    if i = 0 then
                        "", f
                    elif num.isPositive f then
                        " + ", f
                    else
                        " - ", num.neg f
                    
                if HMap.isEmpty p then
                    sprintf "%s%A" op f
                else
                    let isOne = num.isTiny (num.sub f num.one)
                    let isMinusOne = num.isTiny (num.add f num.one)
                    
                    let p = paramStr p
                    if isOne then
                        sprintf "%s%s" op p

                    elif isMinusOne then
                        sprintf "%s-%s" op p

                    else
                        sprintf "%s%A*%s" op f p
            )
            |> String.concat ""

    member private x.AsString = x.ToString()

    member x.Evaluate(v : hmap<string * 'p, 'c>) =
        x.coefficients 
        |> HMap.toSeq
        |> Seq.fold (fun s (k,f) ->
            let factor = 
                k |> HMap.toSeq |> Seq.fold (fun r (p,e) -> 
                    match HMap.tryFind p v with
                        | Some v -> num.mul r (num.pow v e)
                        | _ -> r
                ) f

            s <+> factor
        ) num.zero

    member x.Derivative(name : string, p : 'p) =
        let p = (name,p)
        let mutable coeff = HMap.empty
        for (c, v) in HMap.toSeq x.coefficients do
             match HMap.tryFind p c with
                    | Some e ->
                        if e = 1 then
                            let c' = HMap.remove p c
                            let v' = v <*> (num.fromInt e)
                            coeff <- HMap.alter c' (function Some o -> toOption (v' <+> o) | None -> toOption v') coeff
                        else
                            let c' = HMap.add p (e-1) c
                            let v' = v <*> (num.fromInt e)
                            coeff <- HMap.alter c' (function Some o -> toOption (v' <+> o) | None -> toOption v') coeff

                            
                    | None ->
                        ()

        Polynomial(coeff)

    member x.RenameMonotonic(action : 'p -> 'p) =
        let mutable res = HMap.empty

        for (m,f) in coeff do
            let mutable m1 = HMap.empty
            for ((name,idx),e) in HMap.toSeq m do
                let k = (name, action idx)
                m1 <- HMap.add k e m1

            res <- HMap.add m1 f res
        Polynomial res
    member x.WithoutConstant(name : string) =
        let res = 
            coeff |> HMap.filter (fun m f ->
                m |> Seq.exists (fun ((n,_),_) -> n = name)
            )
        Polynomial res

    member x.FreeParameters =
        let mutable res = HMap.empty
        let all = x.coefficients |> HMap.toSeq |> Seq.collect (fun (k,_) -> k |> HMap.toSeq |> Seq.map (fun (k,_) -> k)) 
        for (name, pi) in all do
            res <- HMap.alter name (fun s -> s |> Option.defaultValue HSet.empty |> HSet.add pi |> Some) res

        res

    member x.AllDerivatives(name : string) =
        match HMap.tryFind name x.FreeParameters with
            | Some parameters -> 
                parameters |> Seq.map (fun p ->
                    p, x.Derivative(name, p)
                )
                |> HMap.ofSeq
            | None ->
                HMap.empty
        
    member x.AllSecondDerivatives(name : string) =
        match HMap.tryFind name x.FreeParameters with
            | Some free -> 
                Seq.allPairs free free 
                |> Seq.choose (fun (p0, p1) ->
                    let d = x.Derivative(name, p0).Derivative(name, p1)
                    if HMap.isEmpty d.coefficients then
                        None
                    else
                        Some ((p0, p1), d)
                )
                |> HMap.ofSeq
            | None ->
                HMap.empty

[<AutoOpen>]
module PolynomialExtensions =

    [<Struct>]
    type PolynomialParam<'a> (name : string)=
        member x.Item
            with get (i : int) = 
                Polynomial<int, 'a>.Parameter(name, i)

        member x.Item
            with get (i : int, j : int) = 
                Polynomial<V2i, 'a>.Parameter(name, V2i(i,j))
        
        member x.Item
            with get (i : int, j : int, k : int) = 
                Polynomial<V3i, 'a>.Parameter(name, V3i(i,j,k))
        

    [<GeneralizableValue>]
    let x<'a> = PolynomialParam<'a> "x"
    
    [<GeneralizableValue>]
    let y<'a> = PolynomialParam<'a> "y"

    [<GeneralizableValue>]
    let z<'a> = PolynomialParam<'a> "z"

    [<GeneralizableValue>]
    let w<'a> = PolynomialParam<'a> "w"
    
    [<AllowNullLiteral>]
    type Param private() = class end
    let param : Param = null
    let inline (?) (v : Param) (name : string) : 'p -> Polynomial<'p, 'c> =
        let num = RealInstances.instance<'c>
        fun i -> Polynomial<'p, 'c> ( HMap.ofList [ HMap.ofList [(name, i), 1], num.one ] )

module Polynomial =

    [<GeneralizableValue>]
    let zero<'p, 'c> = Polynomial<'p, 'c>.Zero

    [<GeneralizableValue>]
    let one<'p, 'c> = Polynomial<'p, 'c>.One
    
    let inline constant<'p, 'c> (value : 'c) = Polynomial<'p, 'c>.Constant value
    let inline degree (name : string) (p : Polynomial<'p, 'c>) = p.Degree name
    let inline parameter<'p, 'c> (name : string) (p : 'p) = Polynomial<'p, 'c>.Parameter(name, p)
    let inline derivative (name : string) (i : 'p) (p : Polynomial<'p, 'c>) = p.Derivative(name, i)

    let inline parameters (p : Polynomial<'p, 'c>) = p.FreeParameters
    let inline evaluate (values : hmap<string * 'p, 'c>) (p : Polynomial<'p, 'c>) = p.Evaluate(values)



    let private varNames =
        LookupTable.lookupTable [
            typeof<int>, (fun (name : string) (i : obj) ->
                let i = unbox<int> i
                if i = 0 then sprintf "%s_0" name
                elif i < 0 then sprintf "%s_n%d" name -i
                else sprintf "%s_p%d" name i
            )
            
            typeof<V2i>, (fun (name : string) (i : obj) ->
                let i = unbox<V2i> i
                let ni = 
                    if i.X = 0 then "0"
                    elif i.X < 0 then sprintf "n%d" -i.X
                    else sprintf "p%d" i.X
                let nj = 
                    if i.Y = 0 then "0"
                    elif i.Y < 0 then sprintf "n%d" -i.Y
                    else sprintf "p%d" i.Y

                sprintf "%s_%s_%s" name ni nj
            )
            typeof<int * int>, (fun (name : string) (i : obj) ->
                let i,j = unbox<int * int> i
                let ni = 
                    if i = 0 then "0"
                    elif i < 0 then sprintf "n%d" -i
                    else sprintf "p%d" i
                let nj = 
                    if j = 0 then "0"
                    elif j < 0 then sprintf "n%d" -j
                    else sprintf "p%d" j

                sprintf "%s_%s_%s" name ni nj
            )
        ]

    module Read =
        open FShade
        open FShade.Imperative
        let private q = getMethodInfo <@ (?) @>
        let private qScope = q.MakeGenericMethod [| typeof<UniformScope> |]

        let rec private rebuildScope (s : UniformScope) =
            match s.Parent with
                | None -> <@@ uniform @@>
                | Some p -> Expr.Call(qScope, [ rebuildScope p; Expr.Value s.Name ])
                  
        let private getUniform<'v> (scope : UniformScope) (name : string) : Expr<'v> =
            let q = q.MakeGenericMethod [| typeof<'v> |]
            let scope = rebuildScope scope
            Expr.Call(q, [ scope; Expr.Value(name)]) |> Expr.Cast
            

        let buffer (id : Expr<int>) (name : string) (p : int) : Expr<'c> =
            let buffer : Expr<'c[]> =  getUniform uniform?StorageBuffer name
            let cnt : Expr<int> = getUniform uniform?Arguments (sprintf "%sCount" name)
            if p = 0 then
                <@ (%buffer).[clamp 0 ((%cnt) - 1) ((%id))] @>
            else
                <@ (%buffer).[clamp 0 ((%cnt) - 1) ((%id) + p)] @>

        let image (id : Expr<V2i>) (name : string) (p : V2i) : Expr<'c> =
            let r = ReflectedReal.instance<'c>
            let fromV4 = r.fromV4
            let sam                 = Expr.ReadInput<Sampler2d>(ParameterKind.Uniform, name)
            let level : Expr<int>   = getUniform uniform?Arguments (sprintf "%sLevel" name)
            let size : Expr<V2i>    = getUniform uniform?Arguments (sprintf "%sSize" name)
            
            if p = V2i.Zero then
                <@
                    (%fromV4) ((%sam).SampleLevel((V2d (%id) + V2d.Half) / V2d (%size), float (%level)))
                @>
            else 
                let ox = float p.X + 0.5
                let oy = float p.Y + 0.5
                <@
                    (%fromV4) ((%sam).SampleLevel((V2d (%id) + V2d(ox, oy)) / V2d (%size), float (%level)))
                @>

    let toExpr (fetch : string -> 'p -> Expr<'c>) (p : Polynomial<'p, 'c>) =
        let real = RealInstances.instance<'c>
        let rreal = ReflectedReal.instance<'c>
        let mul = rreal.mul
        let add = rreal.add

        let bindings = Dict<string * 'p * int, Var * Expr>()
        
        let rec get (name : string) (i : 'p) (e : int) : Expr<'c> =
            match bindings.TryGetValue((name,i,e)) with
                | (true, (v,_)) -> 
                    Expr.Var(v) |> Expr.Cast
                | _ ->
                    let vname = varNames typeof<'p> name i
                    if e = 1 then
                        let ex = fetch name i
                        let v = Var(vname, typeof<'c>)
                        bindings.[(name,i,e)] <- (v, ex :> Expr)
                        Expr.Var(v) |> Expr.Cast
                    else
                        let l = e / 2
                        let r = e - l
                        let l = get name i l
                        let r = get name i r
                        let ex = <@ (%mul) (%l) (%r) @>
                        let vname = sprintf "%s_%d" vname e
                        let v = Var(vname, typeof<'c>)
                        bindings.[(name,i,e)] <- (v, ex :> Expr)
                        Expr.Var(v) |> Expr.Cast

        let factor (m : hmap<string * 'p, int>) =
            let factors =
                m |> HMap.toList |> List.map (fun ((name,i),e) -> get name i e)

            let acc (s : Option<Expr<'c>>) (e : Expr<'c>) =
                match s with
                    | None -> Some e
                    | Some s -> Some <@ (%mul) (%s) (%e) @>

            factors |> List.fold acc None

        let sum =
            let summands = 
                p.coefficients |> HMap.toList |> List.map (fun (c,f) -> 
                    match factor c with
                        | Some c -> 
                            if real.isTiny (real.sub f real.one) then c
                            elif real.isTiny (real.add f real.one) then let n = rreal.neg in <@ (%n) (%c) @>
                            else <@ (%mul) (%c) f @> 
                        | None -> <@ f @>
                )

            let acc (s : Option<Expr<'c>>) (e : Expr<'c>) =
                match s with
                    | None -> Some e
                    | Some s -> Some <@ (%add) (%s) (%e) @>

            match summands |> List.fold acc None with
                | Some s -> s
                | None -> rreal.zero
                
        let bindings = bindings.Values |> Seq.toList |> List.sortBy (fun (v,_) -> v.Name)

        let rec wrap (bindings : list<Var * Expr>) (b : Expr) =
            match bindings with
                | [] -> b
                | (v,e) :: rest -> Expr.Let(v,e, wrap rest b)

        wrap bindings sum

    open FShade
    open FShade.Imperative
    open System

    let toReflectedCall (fetch : Expr<'p> -> string -> 'p -> Expr<'v>)  (p : Polynomial<'p, 'v>) =
        let vid = Var("id", typeof<'p>)
        let body : Expr<'v> = toExpr (fetch (Expr.Cast (Expr.Var vid))) p |> Expr.Cast
        let f : UtilityFunction =
            {
                functionId = Expr.ComputeHash body
                functionName = "fetch"
                functionArguments = [vid]
                functionBody = body
                functionMethod = None
                functionTag = null
                functionIsInline = false
            }

        let call : Expr<'p -> 'v> =
            let a = Var("id", typeof<'p>)
            Expr.Lambda(a, Expr.CallFunction(f, [Expr.Var a])) |> Expr.Cast

        call

    let toCCode (fetch : Expr<'p> -> string -> 'p -> Expr<float>)  (p : Polynomial<'p, float>) =
        let call = toReflectedCall fetch p

        let coord : Expr<'p> =
            if typeof<'p> = typeof<int> then <@ getGlobalId().X @> |> Expr.Cast
            elif typeof<'p> = typeof<V2i> then <@ getGlobalId().XY @> |> Expr.Cast
            elif typeof<'p> = typeof<V3i> then <@ getGlobalId() @> |> Expr.Cast
            else failwith "invalid parameter"



        let shader (dst : Image2d<Formats.r32f>) =
            compute {
                let id = (%coord)
                let value = (%call) id
                dst.[getGlobalId().XY] <- V4d.IIII * value
            }

        let glsl = 
            ComputeShader.ofFunction V3i.III shader
                |> ComputeShader.toModule
                |> ModuleCompiler.compileGLSL430

        glsl.code





