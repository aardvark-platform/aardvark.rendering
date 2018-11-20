namespace ConjugateGradient

open Aardvark.Base

[<StructuredFormatDisplay("{AsString}")>]
type Term<'c when 'c : equality> =
    | Parameter of name : string * coord : 'c
    | Uniform of name : string
    | Value of float
    | Negate of Term<'c>

    | Sum of list<Term<'c>>
    | Product of list<Term<'c>>
    | Power of Term<'c> * Term<'c>

    | Sine of Term<'c>
    | Cosine of Term<'c>
    | Tangent of Term<'c>
    | Logarithm of Term<'c>
    | Absolute of Term<'c>

    static member Log(a : Term<'c>) = Logarithm a

    static member Sin(a : Term<'c>) = Sine a
    static member Cos(a : Term<'c>) = Cosine a
    static member Tan(a : Term<'c>) = Tangent a
    static member Exp(a : Term<'c>) = Power(Value System.Math.E, a)
    static member Abs(a : Term<'c>) = Absolute(a)

    static member (~-) (v : Term<'c>) = Negate v
    static member (+) (l : Term<'c>, r : Term<'c>) = Sum [l;r]
    static member (+) (l : Term<'c>, r : float) = Sum [l; Value r]
    static member (+) (l : float, r : Term<'c>) = Sum [Value l; r]
    static member (+) (l : Term<'c>, r : int) = Sum [l; Value (float r)]
    static member (+) (l : int, r : Term<'c>) = Sum [Value (float l); r]


    static member (-) (l : Term<'c>, r : Term<'c>) = Sum [l; Negate r]
    static member (-) (l : Term<'c>, r : float) = Sum [l; Value -r]
    static member (-) (l : float, r : Term<'c>) = Sum [Value l; Negate r]
    static member (-) (l : Term<'c>, r : int) = Sum [l; Value -(float r)]
    static member (-) (l : int, r : Term<'c>) = Sum [Value (float l); Negate r]
    
    static member (*) (l : Term<'c>, r : Term<'c>) = Product [l;r]
    static member (*) (l : Term<'c>, r : float) = Product [l;Value r]
    static member (*) (l : float, r : Term<'c>) = Product [Value l;r]
    static member (*) (l : Term<'c>, r : int) = Product [l;Value (float r)]
    static member (*) (l : int, r : Term<'c>) = Product [Value(float l);r]

    static member (/) (l : Term<'c>, r : Term<'c>) = Product [l;Power(r, Value -1.0)]
    static member (/) (l : Term<'c>, r : float) = Product [l;Value(1.0/r)]
    static member (/) (l : float, r : Term<'c>) = Product [Value l;Power(r, Value -1.0)]
    static member (/) (l : Term<'c>, r : int) = Product [l;Value(1.0/float r)]
    static member (/) (l : int, r : Term<'c>) = Product [Value (float l);Power(r, Value -1.0)]

    static member Pow (l : Term<'c>, r : Term<'c>) = Power(l,r)
    static member Pow (l : Term<'c>, r : float) = Power(l,Value r)
    static member Pow (l : Term<'c>, r : int) = Power(l,Value (float r))

    static member Sqrt(l : Term<'c>) = Power(l, Value 0.5)
    static member Cbrt(l : Term<'c>) = Power(l, Value (1.0 / 3.0))
    

    static member Zero : Term<'c> = Value 0.0
    static member One : Term<'c> = Value 1.0
    static member E : Term<'c> = Value Constant.E
    static member Pi : Term<'c> = Value Constant.Pi

    member private x.AsString = x.ToString()

    override e.ToString() =

        let rec cmp (l : Term<'a>) (r : Term<'a>) =
            match l, r with
                | Value l, Value r -> compare l r
                | Value _, _ -> -1
                | _, Value _ -> 1

                | Uniform l, Uniform r -> compare l r
                | Uniform _, _ -> -1
                | _, Uniform _ -> 1

                | Parameter(l,_), Parameter(r,_) -> compare l r
                | Parameter _, _ -> -1
                | _, Parameter _ -> 1

                | Negate l, Negate r 
                | Power(l,_), Power(r,_)
                | Sine l, Sine r
                | Cosine l, Cosine r
                | Tangent l, Tangent r
                | Logarithm l, Logarithm r ->
                    cmp l r

                | Product(ls), Product(rs)
                | Sum (ls), Sum(rs) ->
                    let ls = List.sortWith cmp ls
                    let rs = List.sortWith cmp rs

                    match ls, rs with
                        | [], [] -> 0
                        | [], _ -> -1
                        | _, [] -> 1
                        | (l::_), (r::_) -> cmp l r


                | _ -> 0

        let inline toString (e : Term<'c>) = e.ToString()
        match e with
            | Value v -> sprintf "%A" v
            | Parameter(name, i) -> sprintf "%s%A" name i
            | Uniform name -> name

            | Absolute a -> sprintf "abs(%s)" (toString a)
            | Sine a -> sprintf "sin(%s)" (toString a)
            | Cosine a -> sprintf "cos(%s)" (toString a)
            | Tangent a -> sprintf "tan(%s)" (toString a)

            | Sum(vs) -> 
                let neg, pos = vs |> List.partition (function Negate a -> true | _ -> false)

                let pos = pos |> List.sortWith cmp |> List.map toString
                let neg = neg |> List.sortWith cmp |> List.map (function (Negate a) -> toString a | _ -> failwith "") 

                let s = 
                    match pos, neg with
                        | [], [] -> "0.0"
                        | pos, [] -> String.concat " + " pos
                        | [], neg -> String.concat " - " neg |> sprintf "-%s"
                        | pos, neg ->
                            String.concat " + " pos + " - " + String.concat " - " neg

                sprintf "(%s)" s

            | Product(vs) ->
        
                let neg, pos = vs |> List.partition (function Power(a, Value e) when e < 0.0 -> true | _ -> false)

                let pos = pos |> List.sortWith cmp |> List.map toString
                let neg = neg |> List.sortWith cmp |> List.map (function (Power(a,Value e)) -> toString (Power(a, Value -e)) | _ -> failwith "")

                let conc (sep : string) (ls : list<string>) =
                    match ls with
                        | [] -> ""
                        | [a] -> a
                        | ls -> String.concat sep ls |> sprintf "(%s)"
                    

                match pos, neg with
                    | [], [] -> "1.0"
                    | pos, [] -> conc " * " pos 
                    | [], neg -> conc " * " neg |> sprintf "1.0 / %s"
                    | pos, neg ->
                        let pos = conc " * " pos 
                        let neg = conc " * " neg
                        sprintf "%s / %s" pos neg
                        

            | Negate(l) -> sprintf "-%s" (toString l)
            | Power(l,Value 0.5) -> sprintf "sqrt(%s)" (toString l)
            | Power(l,Value 1.0) -> (toString l)
            | Power(l,Value -1.0) -> sprintf "1.0 / %s" (toString l)
            | Power(l,r) -> sprintf "%s**%s" (toString l) (toString r)
            | Logarithm a -> sprintf "log(%s)" (toString a)
       

[<AutoOpen>]
module TermPatterns = 

    let (|Combination|_|) (e : Term<'a>) =
        match e with
            | Sum es        -> Some(Sum, es)
            | Product es    -> Some(Product, es)
            | Negate e      -> Some(List.head >> Negate, [e])
            | Power(a,e)    -> Some((function [a;e] -> Power(a,e) | _ -> failwith "invalid argument count"), [a;e])
            | Sine e        -> Some(List.head >> Sine, [e])
            | Cosine e      -> Some(List.head >> Cosine, [e])
            | Tangent e     -> Some(List.head >> Tangent, [e])
            | Logarithm e   -> Some(List.head >> Logarithm, [e])
            | Absolute e    -> Some(List.head >> Absolute, [e])
            | _             -> None


    let private tryFoldConstants (seed : float) (combine : float -> float -> float) (l : list<Term<'c>>) =

        let rec foldConstantsAux (cnt : int) (seed : float) (combine : float -> float -> float) (l : list<Term<'c>>) =
            match l with
                | Value v :: rest ->
                    foldConstantsAux (cnt + 1) (combine seed v) combine rest
                | e :: rest ->
                    match foldConstantsAux cnt seed combine rest with
                        | Some rest ->
                            Some (e :: rest)
                        | None ->
                            None
                | [] ->
                    if cnt > 1 then Some [Value seed]
                    else None

        foldConstantsAux 0 seed combine l

    let (|ConstantSum|_|) (e : Term<'a>) =
        match e with
            | Sum es -> 
                tryFoldConstants 0.0 (+) es |> Option.map Sum
            | _ ->
                None

    let (|ConstantProduct|_|) (e : Term<'a>) =
        match e with
            | Product es -> 
                tryFoldConstants 1.0 (*) es |> Option.map Product
            | _ ->
                None

    let (|AnyTwo|_|) (pattern : 'c -> Option<'r>) (l : list<'c>) =
        let rec find (n : int) (l : list<'c>) =
            if n <= 0 then
                Some ([], l)
            else
                match l with
                    | [] -> 
                        None
                    | h :: t ->
                        match pattern h with
                            | Some r ->
                                match find (n - 1) t with
                                    | Some (rs, ls) ->
                                        Some (r :: rs, ls)
                                    | None ->
                                        None
                            | None ->
                                match find n t with
                                    | Some (rs, ls) ->
                                        Some (rs, h :: ls)
                                    | None ->
                                        None
        match find 2 l with
            | Some ([a;b], rest) ->
                Some (a,b,rest)
            | _ ->
                None

    let (|AnyPair|_|) (pattern : 'c -> 'c -> Option<'r>) (l : list<'c>) =
        let rec findMatching (a : 'c) (l : list<'c>) =
            match l with
                | [] -> None
                | h :: t ->
                    match pattern a h with
                        | Some r -> Some (r, t)
                        | None ->
                            match findMatching a t with
                                | Some(r,t) ->
                                    Some(r, h :: t)
                                | None ->
                                    None
    
        let rec find (l : list<'c>) =
            match l with
                | [] -> 
                    None
                | h :: t ->
                    match findMatching h t with
                    | Some (r, t) -> Some (r,t)
                    | None ->
                        match find t with
                        | Some (rs, ls) ->
                            Some (rs, h :: ls)
                        | None ->
                            None
        find l

    let (|Any|_|) (pattern : 'c -> Option<'r>) (l : list<'c>) =
        let rec find (n : int) (l : list<'c>) =
            if n <= 0 then
                Some ([], l)
            else
                match l with
                    | [] -> 
                        None
                    | h :: t ->
                        match pattern h with
                            | Some r ->
                                match find (n - 1) t with
                                    | Some (rs, ls) ->
                                        Some (r :: rs, ls)
                                    | None ->
                                        None
                            | None ->
                                match find n t with
                                    | Some (rs, ls) ->
                                        Some (rs, h :: ls)
                                    | None ->
                                        None
        match find 1 l with
            | Some ([a], rest) ->
                Some (a,rest)
            | _ ->
                None

    let (|AnyEq|_|) (v : 'c) (l : list<'c>) =
        let rec find (l : list<'c>) =
            match l with
                | [] -> None
                | h :: t ->
                    if h = v then Some t
                    else 
                        match find t with
                            | Some (t) -> Some (h :: t)
                            | None -> None
        find l

    let (|Pinf|_|) (e : Term<'c>) =
        match e with
            | Value v when System.Double.IsPositiveInfinity v -> Some ()
            | _ -> None

    let (|Ninf|_|) (e : Term<'c>) =
        match e with
            | Value v when System.Double.IsNegativeInfinity v -> Some ()
            | _ -> None

    let (|Zero|_|) (e : Term<'c>) =
        match e with
            | Value v when Fun.IsTiny v -> Some ()
            | Sum [] -> Some ()
            | _ -> None

    let (|Half|_|) (e : Term<'c>) =
        match e with
            | Value v when Fun.IsTiny(v - 0.5) -> Some ()
            | _ -> None

    let (|One|_|) (e : Term<'c>) =
        match e with
            | Value v when Fun.IsTiny(v - 1.0) -> Some ()
            | Product [] -> Some ()
            | _ -> None

    let (|MinusOne|_|) (e : Term<'c>) =
        match e with
            | Value v when Fun.IsTiny(v + 1.0) -> Some ()
            | _ -> None

    let (|E|_|) (e : Term<'c>) =
        match e with
            | Value v when Fun.IsTiny(v - Constant.E) -> Some ()
            | _ -> None


    let internal findCommon (l : list<list<Term<'c>>>) =
        let terms = l |> List.collect id |> HSet.ofList

        let rec tryRemove (e : Term<'c>) (l : list<Term<'c>>) =
            match l with
                | [] -> None
                | h :: r ->
                    if h = e then 
                        Some r
                    else
                        match h, e with
                            | Power(a,Value ae), Power(b,Value be) when be > 0.0 && a = b && ae >= be ->
                                Some(Power(a, Value (ae - be)) :: r)
                            
                            | Power(a,Value ae), Power(b,Value be) when be < 0.0 && a = b && ae <= be ->
                                Some(Power(a, Value (ae - be)) :: r)
                            
                            | Power(a,Value ae), b when a = b && ae >= 1.0 ->
                                Some(Power(a, Value (ae - 1.0)) :: r)
                                

                            | _ ->
                                match tryRemove e r with
                                    | Some rest -> Some (h :: rest)
                                    | None -> None

        let mutable l = l
        let mutable common = HSet.empty
        for t in terms do
            let mutable failed = false
            let nl =
                l |> List.choose (fun s -> 
                    match tryRemove t s with
                        | Some r -> Some r
                        | None -> failed <- true; None
                )

            if not failed then
                common <- HSet.add t common
                l <- nl

        if HSet.isEmpty common then
            None
        else
            Some (HSet.toList common, l)


    let rec (|Factorize|_|) (e : Term<'c>) =
        //printfn "%s" (toString e)
        //System.Console.ReadLine() |> ignore
        match e with
            | Sum [] -> 
                None

            | Sum [a] -> 
                None

            | Sum l ->
                let lists = l |> List.map (function Product p -> p | e -> [e])
                match findCommon lists with
                    | Some(common, rest) ->
                        let rest = rest |> List.map Product |> Sum
                        let common = Product common
                        Some (common * rest)
                    | _ ->
                        let rec drop (n : int) (acc : list<'a>) (l : list<'a>) =
                            if n <= 0 then
                                [ List.rev acc, l ]
                            else
                                match l with
                                    | h :: t ->
                                        (drop (n-1) (h :: acc) t) @
                                        (List.map (fun (acc,l) -> acc, h::l) (drop n acc t))
                                    | [] ->
                                        []


                        let rec drop1 (l : list<'a>) =
                            drop 1 [] l |> List.map (fun (dropped, rest) -> List.head dropped, rest)
 
                        drop1 lists
                        |> List.tryPick (fun (dropped, rest) ->
                            let dropped = Product dropped
                            let rest = rest |> List.map Product |> Sum

                            match rest with
                                | Factorize(repl) ->
                                    Some (dropped + repl)
                                | _ ->
                                    None
                        )
            | _ ->
                None

    let internal isSum (e : Term<'c>) =
        match e with
            | Sum a -> Some a
            | _ -> None

        
    let internal isProduct (e : Term<'c>) =
        match e with
            | Product a -> Some a
            | _ -> None
            
    let internal isValue (e : Term<'c>) =
        match e with
            | Value a -> Some a
            | _ -> None
            
    let internal isLog (e : Term<'c>) =
        match e with
            | Logarithm a -> Some a
            | _ -> None

module Term = 

    let private baseSimplifyRules<'c when 'c : equality> : list<Term<'c> -> Option<Term<'c>>> =

        let sinDivCos a b =
            match a, b with
                | Sine(a), Power(Cosine(b), Value -1.0) 
                | Power(Cosine(b), Value -1.0), Sine(a) when a = b ->
                    Some (Tangent(a))
                    
                | Cosine(a), Power(Sine(b), Value -1.0) 
                | Power(Sine(b), Value -1.0), Cosine(a) when a = b ->
                    Some (1.0 / Tangent(a))
                | _ ->
                    None

        let sinAddCosSquared a b =
            match a, b with
                | Power(Sine(a), Value 2.0), Power(Cosine(b), Value 2.0) 
                | Power(Cosine(b), Value 2.0), Power(Sine(a), Value 2.0) when a = b ->
                    Some (Value 1.0)

                | _ ->
                    None

        let neg = fun a b -> if a = Negate b || b = Negate a then Some () else None

        [
            
            function Sum [] -> Some (Value 0.0) | _ -> None
            function Product [] -> Some (Value 1.0) | _ -> None
            function Sum [a] -> Some a | _ -> None
            function Product [a] -> Some a | _ -> None
        
            function Sum(Any isSum (s, rest)) -> Some (Sum (s @ rest)) | _ -> None
            function Product(Any isProduct (s, rest)) -> Some (Product (s @ rest)) | _ -> None
            function Negate(Sum ls) -> Some (Sum (List.map Negate ls)) | _ -> None
            function Power(Power(a,e0), e1) -> Some (Power(a, e0 * e1)) | _ -> None
            function Sine(Negate a) -> Some (Negate (Sine a)) | _ -> None
            function Tangent(Negate a) -> Some (Negate (Tangent a)) | _ -> None
            function Cosine(Negate a) -> Some (Cosine a) | _ -> None
            function Logarithm(Value v) -> Some (Value (log v)) | _ -> None
            function Logarithm(Power(a,e)) -> Some (log a * e) | _ -> None
            function Negate(Value a) -> Some (Value -a) | _ -> None
            function Power(Value a, Value e) -> Some (Value (a ** e)) | _ -> None
            function Power(Product ps, e) -> Some (Product (List.map (fun p -> Power(p,e)) ps)) | _ -> None
            function Negate(Product (Any isValue (v,rest))) -> Some (Product (Value -v :: rest)) | _ -> None
            function Power(a, One) -> Some a | _ -> None
            function Power(a, Zero) -> Some (Value 1.0) | _ -> None
            function Absolute(Value a) -> Some (Value (abs a)) | _ -> None
            function Absolute(Negate a) -> Some (Absolute a) | _ -> None
            function Absolute(Power(a, Value e)) when e % 2.0 = 0.0 -> Some (Power(a, Value e)) | _ -> None
            function Power(Absolute a, Value e) when e % 2.0 = 0.0 -> Some (Power(a, Value e)) | _ -> None

            function Sine(Value a) -> Some (Value (sin a)) | _ -> None
            function Cosine(Value a) -> Some (Value (cos a)) | _ -> None
            function Tangent(Value a) -> Some (Value (tan a)) | _ -> None
            function Product(AnyPair sinDivCos (t,rest)) -> Some (Product(t :: rest)) | _ -> None
            function Sum(AnyPair sinAddCosSquared (t,rest)) -> Some (Sum(t :: rest)) | _ -> None
            function Sum(Any (|Zero|_|) (_, rest)) -> Some (Sum rest) | _ -> None
            function Product(Any (|Zero|_|) _) -> Some (Value 0.0) | _ -> None
            function Product(Any (|One|_|) (_, rest)) -> Some (Product rest) | _ -> None
            function Product(Any (|MinusOne|_|) (_, rest)) -> Some (Negate (Product rest)) | _ -> None
            function Negate(Negate a) -> Some a | _ -> None
            function ConstantSum e -> Some e | _ -> None
            function ConstantProduct e -> Some e | _ -> None
            function Sum(AnyPair neg (_, rest)) -> Some (Sum rest) | _ -> None
            
        ]

    let private simplifyRules<'c when 'c : equality> : list<Term<'c> -> Option<Term<'c>>> =
        let eqa a b = 
            if a = b then 
                Some(Value 2.0, a) 
            else 
                match a, b with
                    | a, Product (AnyEq a rest) 
                    | Product (AnyEq a rest), a -> 
                        Some(Sum [Product rest; Value 1.0], a)

                    | a, Negate(Product (AnyEq a rest)) 
                    | Negate(Product (AnyEq a rest)), a -> 
                        Some (Sum [Value 1.0; Negate (Product rest)], a)
                    
                    | _ -> 
                        None

        let eqm a b = 
            if a = b then 
                Some(a, Value 2.0) 
            else 
                match a, b with
                    | a, Power(b,f) 
                    | Power(b,f), a  when a = b -> 
                        Some(a, Sum [f; Value 1.0])

                    | Power(a,e0), Power(b,e1) when a = b ->
                        Some(a, Sum [e0; e1])
                    
                    | _ -> None

    
        
        baseSimplifyRules @ [
            function Sum(AnyTwo isLog (a,b,rest)) -> Some (Sum (log (a * b) :: rest)) | _ -> None
            function Product(AnyPair eqm ((a,e), rest)) -> Some (Product (Power(a,e) :: rest)) | _ -> None
            function Sum(AnyPair eqa ((f,a), rest)) -> Some (Sum (Product [f; a] :: rest)) | _ -> None
            function (Factorize(e)) -> Some e | _ -> None
        ]
    
    let rec private applyRules (depth : int) (rules : list<Term<'c> -> Option<Term<'c>>>) (e : Term<'c>) =
        match rules |> List.tryPick (fun r -> r e) with
            | Some n ->
                if depth > 100 then Log.warn "deep"
                n
            | None ->
                match e with
                    | Combination(rebuild, args) ->
                        let args = args |> List.map (applyRules (depth + 1) rules)
                        rebuild args
                    | _ ->
                        e

    let applyFix (rules : list<Term<'c> -> Option<Term<'c>>>) (e : Term<'c>) =
        let mutable o = e
        let mutable n = applyRules 0 rules o
        while o <> n do
            o <- n
            n <- applyRules 0 rules o
        n

    [<GeneralizableValue>]
    let zero<'c when 'c : equality> = Term<'c>.Zero

    [<GeneralizableValue>]
    let one<'c when 'c : equality> = Term<'c>.One
    
    let inline constant (value : float) = Value value
    let inline parameter (name : string) (i : 'c) = Parameter(name, i)
    let inline uniform (name : string) = Uniform(name)

    let uniforms (e : Term<'c>) =
        let rec usedUniforms (acc : Set<string>) (e : Term<'c>) =
            match e with
                | Uniform(name) ->
                    acc |> Set.add name
                | Combination(_, args) ->
                    args |> List.fold usedUniforms acc
                | _ ->
                    acc

        usedUniforms Set.empty e

    let parameters (e : Term<'c>) =
        let rec usedParameters (acc : hmap<string, hset<'c>>) (e : Term<'c>) =
            match e with
                | Parameter(name, i) ->
                    acc |> HMap.alter name (Option.defaultValue HSet.empty >> HSet.add i >> Some)
                | Combination(_, args) ->
                    args |> List.fold usedParameters acc
                | _ ->
                    acc
        usedParameters HMap.empty e
        
    let parameterNames (e : Term<'c>) =
        let rec usedParameterNames (acc : Set<string>) (e : Term<'c>) =
            match e with
                | Parameter(name, i) ->
                    acc |> Set.add name
                | Combination(_, args) ->
                    args |> List.fold usedParameterNames acc
                | _ ->
                    acc
        usedParameterNames Set.empty e

    let names (e : Term<'c>) =
        let rec names (acc : Set<string>) (e : Term<'c>) =
            match e with
                | Parameter(name, _)
                | Uniform(name) ->
                    acc |> Set.add name

                | Combination(_, args) ->
                    args |> List.fold names acc
                | _ ->
                    acc
        names Set.empty e
        
    let simplify (e : Term<'c>) =
        applyFix simplifyRules e

    let derivative (name : string) (i : 'c) (e : Term<'c>) =
        let rec derivative (name : string) (i : 'c) (e : Term<'c>) =
            match e with
                | Absolute _ -> 
                    failwith "no derivative for abs"
                | Value _ | Uniform _ -> 
                    Value 0.0
                
                | Parameter(n, ii) ->
                    if name = n && i = ii then Value 1.0
                    else Value 0.0

                | Negate a -> 
                    Negate (derivative name i a)

                | Sum es -> 
                    Sum (es |> List.map (derivative name i))

                | Sine a ->
                    Cosine a * derivative name i a
                
                | Cosine a ->
                    Negate(Sine a) * derivative name i a

                | Tangent a ->
                    (1.0 + Tangent(a) ** 2) * derivative name i a

                | Power(E, x) ->
                    Power(Value Constant.E, x) * derivative name i x

                | Power(a, Value e) ->
                    e * Power(a, Value (e - 1.0)) * derivative name i a

                | Power(Value a, e) ->
                    Power(Value a, e) * log a * derivative  name i e

                | Power(f,g) ->
                    Power(f, g - 1.0) * (g * derivative name i f + f * log f * derivative name i g)

                | Logarithm a ->
                    (1.0 / a) * derivative name i a

                | Product es ->
                    let rec derive (acc : list<Term<'c>>) (r : list<Term<'c>>) =
                        match r with
                            | [] -> Value 0.0
                            | h :: t ->
                                derivative name i h * (Product (acc @ t)) +
                                derive (h :: acc) t
                    derive [] es

        derivative name i e |> simplify

    let allDerivatives (name : string) (e : Term<'c>) =
        let used = parameters e
        match HMap.tryFind name used with
            | None ->
                HMap.empty
            | Some cs ->    
                cs 
                |> Seq.map (fun i -> i, derivative name i e)
                |> HMap.ofSeq
                
    let toString (e : Term<'a>) = e.ToString()
       
       
    let private factorizeRules<'c when 'c : equality> : list<Term<'c> -> Option<Term<'c>>> =

        let rec power (l : list<Term<'c>>) (e : int) =
            if e <= 0 then Value 1.0
            elif e = 1 then Sum l
            else
                let l2 = List.allPairs l l |> List.map (fun (a, b) -> a * b) |> List.sum
                l2 * power l (e - 2)

                
        baseSimplifyRules @ [

            function Product(Any isSum (s, rest)) -> s |> List.map (fun s -> Product (s :: rest)) |> Sum |> Some | _ -> None
            function Power(Product xs, e) -> Some (Product (xs |> List.map (fun x -> Power(x, e)))) | _ -> None
            function Power(Sum xs, Value e) when Fun.IsTiny(Fun.Frac e) && e > 0.0 -> Some (power xs (int e)) | _ -> None
            function Power(Negate a, Value e) when Fun.IsTiny(Fun.Frac e) && int (abs e) % 2 = 0 -> Some (Power(a, Value e)) | _ -> None
            function Power(Negate a, Value e) when Fun.IsTiny(Fun.Frac e) && int (abs e) % 2 = 1 -> Some (Negate (Power(a, Value e))) | _ -> None
            

            function Logarithm(Power(a,e)) -> Some (e * log a) | _ -> None
            function Logarithm(Product a) -> Some (List.sumBy log a) | _ -> None
            function Logarithm(One) -> Some Term.Zero | _ -> None
            
            function Sine(Sum (a :: b)) -> Some (sin a * cos (Sum b) + cos a * sin (Sum b)) | _ -> None
            function Cosine(Sum (a :: b)) -> Some (cos a * cos (Sum b) - sin a * sin (Sum b)) | _ -> None
            
            function Power(s, Value e) when float (int e) = e && e > 0.0 -> Some (Product [s; Power(s, Value (e - 1.0))]) | _ -> None
        ]

    let factorize (name : string) (t : Term<'a>) =
        let rec run (depth : int) (t : Term<'a>) =
            if depth > 100 then Log.warn "deep"
            match t with
                | Negate a -> 
                    let (d,c) = run (depth + 1) a
                    (Negate d, Negate c)

                | Sum es ->
                    let (ds, cs) = es |> List.map (run (depth + 1)) |> List.unzip
                    (Sum ds, Sum cs)

                | t -> 
                    let ns = names t
                    if Set.contains name ns then
                        (t, Value 0.0)
                    else
                        (Value 0.0, t)
                  

        let (d,c) = t |> applyFix factorizeRules |> run 0
        simplify d, simplify c
        
    let isolate (name : string) (t : Term<'a>) =
        let rec isolate (name : string) (t : Term<'a>) =
            match t with
                | Uniform n ->
                    if n = name then t, Value 1.0
                    else Value 1.0, t

                | Negate(t) ->
                    let f, t = isolate name t
                    f, Negate t
                | Product es ->
                    let fs, ts = es |> List.map (isolate name) |> List.unzip
                    Product fs, Product ts

                | Factorize (Product es) ->
                    isolate name (Product es)

                | Power(a, e) ->
                    let (f,a) = isolate name a
                    Power(f,e), Power(a, e)

                | Parameter _ | Value _
                | Sine _ | Cosine _ | Tangent _ | Sum _ | Logarithm _ | Absolute _ ->
                    Value 1.0, t
        let f, t = isolate name t
        simplify f, simplify t

    let substituteUniform (name : string) (s : Term<'a>) (t : Term<'a>) =
        let rec substituteUniform (name : string) (s : Term<'a>) (t : Term<'a>) =
            match t with
                | Uniform n ->
                    if name = n then s
                    else t
                | Combination(rebuild, args) ->
                    args |> List.map (substituteUniform name s) |> rebuild
                | _ ->
                    t

        substituteUniform name s t |> simplify


    type TermParameter(name : string) =
        
        member x.Name = name

        member x.Item
            with get(i : int) = Term<int>.Parameter(name, i)
            
        member x.Item
            with get(i : V2i) = Term<V2i>.Parameter(name, i)

        member x.Item
            with get(i : V3i) = Term<V3i>.Parameter(name, i)

        member x.Item
            with get(i : int, j : int) = Term<V2i>.Parameter(name, V2i(i,j))
            
        member x.Item
            with get(i : int, j : int, k : int) = Term<V3i>.Parameter(name, V3i(i,j,k))
             
    type TermParameter2d(name : string) =
        member x.Name = name

        member x.Item
            with get(i : V2i) = Term<V2i>.Parameter(name, i)
            
        member x.Item
            with get(i : int, j : int) = Term<V2i>.Parameter(name, V2i(i,j))
            
    open Microsoft.FSharp.Quotations
    
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
            

        let private readUniform (name : string) : Expr<'c> =
            getUniform uniform name

        let buffer (id : Expr<int>) (name : string) (p : Option<int>) : Expr<'v> =
            match p with
                | None -> readUniform name
                | Some p -> 
                    let buffer : Expr<'v[]> =  getUniform uniform?StorageBuffer name
                    let cnt : Expr<int> = getUniform uniform?Arguments (sprintf "%sCount" name)
                    if p = 0 then
                        <@ (%buffer).[clamp 0 ((%cnt) - 1) ((%id))] @>
                    else
                        <@ (%buffer).[clamp 0 ((%cnt) - 1) ((%id) + p)] @>

        let image (id : Expr<V2i>) (name : string) (p : Option<V2i>) : Expr<'c> =
            match p with
                | None -> readUniform name
                | Some p ->
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

    [<AutoOpen>]
    module private ExprExtensions =
        open Aardvark.Base.ReflectionHelpers
        open FShade.ReflectionPatterns
        open Aardvark.Base.TypeInfo.Patterns

        let private vecNames = [| "X"; "Y"; "Z"; "W" |]
        let private neg = getMethodInfo <@ (~-) : int -> int @>
        let private add = getMethodInfo <@ (+) : int -> int -> int @>
        let private sub = getMethodInfo <@ (-) : int -> int -> int @>
        let private mul = getMethodInfo <@ (*) : int -> int -> int @>
        let private div = getMethodInfo <@ (/) : int -> int -> int @>
        let private pow = getMethodInfo <@ ( ** ) : float -> float -> float @>
        
        type Expr with
            static member Negate(l : Expr) =
                let neg = neg.MakeGenericMethod [| l.Type |]
                Expr.Call(neg, [l])

            static member Add(l : Expr, r : Expr) =
                let add = 
                    match l.Type, r.Type with
                        | (VectorOf(d0,t0) as vt), _ 
                        | _, (VectorOf(d0,t0) as vt) ->
                            add.MakeGenericMethod [| l.Type; r.Type; vt |]
                        | _ -> 
                            add.MakeGenericMethod [| l.Type; r.Type; r.Type |]

                Expr.Call(add, [l; r])
                
            static member Sub(l : Expr, r : Expr) =
                let sub = 
                    match l.Type, r.Type with
                        | (VectorOf(d0,t0) as vt), _ 
                        | _, (VectorOf(d0,t0) as vt) ->
                            sub.MakeGenericMethod [| l.Type; r.Type; vt |]
                        | _ -> 
                            sub.MakeGenericMethod [| l.Type; r.Type; r.Type |]

                Expr.Call(sub, [l; r])

            static member Mul(l : Expr, r : Expr) =
                let mul = 
                    match l.Type, r.Type with
                        | (VectorOf(d0,t0) as vt), _ 
                        | _, (VectorOf(d0,t0) as vt) ->
                            mul.MakeGenericMethod [| l.Type; r.Type; vt |]
                        | _ -> 
                            mul.MakeGenericMethod [| l.Type; r.Type; r.Type |]

                Expr.Call(mul, [l; r])
                
            static member Div(l : Expr, r : Expr) =
                let div = 
                    match l.Type, r.Type with
                        | (VectorOf(d0,t0) as vt), _ 
                        | _, (VectorOf(d0,t0) as vt) ->
                            div.MakeGenericMethod [| l.Type; r.Type; vt |]
                        | _ -> 
                            div.MakeGenericMethod [| l.Type; r.Type; r.Type |]

                Expr.Call(div, [l; r])

            static member Pow(l : Expr, r : Expr) =
                match l.Type, r.Type with
                    | VectorOf(d0,t0), VectorOf(d1, t1) ->
                        let lFields = List.init d0 (fun i -> Expr.FieldGet(l, l.Type.GetField(vecNames.[i])))
                        let rFields = List.init d1 (fun i -> Expr.FieldGet(r, r.Type.GetField(vecNames.[i])))

                        let ctor = l.Type.GetConstructor(Array.create d0 t0)
                        let args = List.map2 (fun l r -> Expr.Pow(l,r)) lFields rFields
                        Expr.NewObject(ctor, args)

                    | VectorOf(d0,t0), rt ->
                        let lFields = List.init d0 (fun i -> Expr.FieldGet(l, l.Type.GetField(vecNames.[i])))
                        let ctor = l.Type.GetConstructor(Array.create d0 t0)
                        let args = List.map (fun l-> Expr.Pow(l,r)) lFields
                        Expr.NewObject(ctor, args)

                    | lt, VectorOf(d0,t0) ->
                        let rFields = List.init d0 (fun i -> Expr.FieldGet(r, r.Type.GetField(vecNames.[i])))

                        let ctor = l.Type.GetConstructor(Array.create d0 t0)
                        let args = List.map (fun r -> Expr.Pow(l,r)) rFields
                        Expr.NewObject(ctor, args)
                    | _ -> 
                        let m = pow.MakeGenericMethod [| l.Type; r.Type |]
                        Expr.Call(pow, [l; r])

    let toExpr (fetch : string -> Option<'c> -> Expr<'v>) (p : Term<'c>) =
        let real = RealInstances.instance<'v>
        let rreal = ReflectedReal.instance<'v>


        let bindings = Dict<string * Option<'c> * int, Var * Expr>()

        let mul = rreal.mul
        let rec get (name : string) (i : Option<'c>) (e : int) : Expr<'v> =
            if e = 0 then
                rreal.one
            else
                match bindings.TryGetValue((name,i,e)) with
                    | (true, (v,_)) -> 
                        Expr.Var(v) |> Expr.Cast
                    | _ ->
                        match i with
                            | None ->
                                if e = 1 then
                                    fetch name None
                                else
                                    let l = e / 2
                                    let r = e - l
                                    let l = get name None l
                                    let r = get name None r
                                    let ex = <@ (%mul) (%l) (%r) @>
                                    let vname = sprintf "%s_%d" name e
                                    let v = Var(vname, typeof<'v>)
                                    bindings.[(name,None,e)] <- (v, ex :> Expr)
                                    Expr.Var(v) |> Expr.Cast

                            | Some i -> 
                                let vname = varNames typeof<'c> name i
                                if e = 1 then
                                    let ex = fetch name (Some i)
                                    let v = Var(vname, typeof<'v>)
                                    bindings.[(name,Some i,e)] <- (v, ex :> Expr)
                                    Expr.Var(v) |> Expr.Cast
                                else
                                    let l = e / 2
                                    let r = e - l
                                    let l = get name (Some i) l
                                    let r = get name (Some i) r
                                    let ex = <@ (%mul) (%l) (%r) @>
                                    let vname = sprintf "%s_%d" vname e
                                    let v = Var(vname, typeof<'v>)
                                    bindings.[(name,Some i,e)] <- (v, ex :> Expr)
                                    Expr.Var(v) |> Expr.Cast

        let mul (l : Expr) (r : Expr) =
            let lf = l.Type = typeof<float>
            let rf = r.Type = typeof<float>
            match lf, rf with
                | true, true -> <@@ (%%l : float) * (%%r : float) @@>
                | false, true -> <@@ (%rreal.muls) (%%l) (%%r) @@>
                | true, false -> <@@ (%rreal.muls) (%%r) (%%l) @@>
                | false, false -> <@@ (%rreal.mul) (%%l) (%%r) @@>

        let div (l : Expr) (r : Expr) =
            let lf = l.Type = typeof<float>
            let rf = r.Type = typeof<float>
            match lf, rf with
                | true, true -> <@@ (%%l : float) / (%%r : float) @@>
                | false, true -> <@@ (%rreal.divs) (%%l) (%%r) @@>
                | true, false -> <@@ (%rreal.div) ((%rreal.fromFloat) %%l) (%%r) @@>
                | false, false -> <@@ (%rreal.div) (%%l) (%%r) @@>

        let pow (l : Expr) (r : Expr) =
            let lf = l.Type = typeof<float>
            let rf = r.Type = typeof<float>
            match lf, rf with
                | true, true -> <@@ (%%l : float) ** (%%r : float) @@>
                | false, true -> <@@ (%rreal.pows) (%%l) (%%r) @@>
                | true, false -> <@@ (%rreal.pow) ((%rreal.fromFloat) (%%l)) (%%r) @@>
                | false, false -> <@@ (%rreal.pow) (%%l) (%%r) @@>

        let negate (l : Expr) =
            if l.Type = typeof<float> then 
                <@@ -(%%l : float) @@>
            else
                <@@ (%rreal.neg) (%%l) @@>


        let add (l : Expr) (r : Expr) =
            let l =
                if l.Type = typeof<float> then <@@ (%rreal.fromFloat) (%%l : float) @@>
                else l

            let r =
                if r.Type = typeof<float> then <@@ (%rreal.fromFloat) (%%r : float) @@>
                else r

            <@@ (%rreal.add) (%%l) (%%r) @@>

        let sub (l : Expr) (r : Expr) =
            let l =
                if l.Type = typeof<float> then <@@ (%rreal.fromFloat) (%%l : float) @@>
                else l

            let r =
                if r.Type = typeof<float> then <@@ (%rreal.fromFloat) (%%r : float) @@>
                else r

            <@@ (%rreal.sub) (%%l) (%%r) @@>

        let rec toExpr (term : Term<'c>) : Expr =
            match term with

                | Value v ->
                    Expr.Value(v)

                | Uniform name ->
                    get name None 1 :> Expr

                | Parameter(name, i) -> 
                    get name (Some i) 1 :> Expr

                | Power(Uniform name, Value e) when float (int e) = e ->
                    if e > 0.0 then
                        get name None (int e) :> Expr
                    else
                        let v = get name None (int -e)
                        div <@@ 1.0 @@> v 
                        
                
                | Power(Parameter(name, i), Value e) when float (int e) = e ->
                    get name (Some i) (int e) :> Expr

                | Power(E, value) ->
                    let v = toExpr value
                    <@@ (%rreal.exp) (%%v) @@>

                | Sine t ->
                    let e = toExpr t
                    <@@ (%rreal.sin) (%%e) @@>
        
                | Cosine t ->
                    let e = toExpr t
                    <@@ (%rreal.cos) (%%e) @@>
                    
                | Tangent t ->
                    let e = toExpr t
                    <@@ (%rreal.tan) (%%e) @@>
        
                | Logarithm t ->
                    let e = toExpr t
                    <@@ (%rreal.log) (%%e) @@>
                    
                | Absolute t ->
                    let e = toExpr t
                    <@@ (%rreal.abs) (%%e) @@>
                    
                | Negate(a) ->
                    let e = toExpr a
                    negate e
                
                | Power(a, MinusOne) ->
                    let a = toExpr a
                    let one = real.one
                    div <@@ one @@> a
                    
                | Power(a, b) ->
                    let a = toExpr a
                    let b = toExpr b
                    pow a b

                | Sum vs ->
                    let neg, pos = vs |> List.partition (function Negate a -> true | _ -> false)
                    let pos = pos |> List.map toExpr
                    let neg = neg |> List.map (function (Negate a) -> toExpr a | _ -> failwith "")
                    
                    match pos, neg with
                        | [], [] -> 
                            Expr.Value(real.zero)

                        | (p :: pos), [] ->
                            pos |> List.fold add p

                        | [], (n :: neg) -> 
                            neg |> List.fold sub (negate n)

                        | (p :: pos), neg ->
                            let pos = pos |> List.fold add p
                            neg |> List.fold sub pos
                                

               
                | Product vs ->
                    let neg, pos = vs |> List.partition (function Power(a, Value e) when e < 0.0 -> true | _ -> false)
                    let pos = pos |> List.map toExpr
                    let neg = neg |> List.map (function (Power(a,Value e)) -> toExpr (Power(a, Value -e)) | _ -> failwith "")

                    match pos, neg with
                    | [], [] -> 
                        Expr.Value(real.one)

                    | (p :: pos), [] ->
                        pos |> List.fold mul p

                    | [], (n :: neg) -> 
                        let neg = neg |> List.fold mul n
                        let one = real.one
                        div <@@ one @@> neg

                    | (p :: pos), (n :: neg) ->
                        let pos = pos |> List.fold mul p
                        let neg = neg |> List.fold mul n
                        div pos neg

                    //let es = vs |> List.map toExpr
                    //match es with
                    //    | [] -> 
                    //        Expr.Value(real.zero) |> Expr.Cast
                    //    | h :: t ->
                    //        t |> List.fold (fun s e -> <@ (%rreal.mul) (%s) (%e) @>) h

        let sum = toExpr (simplify p)
        let sum = 
            if sum.Type = typeof<float> then <@@ (%rreal.fromFloat) (%%sum) @@>
            else sum
        let bindings = bindings.Values |> Seq.toList |> List.sortBy (fun (v,_) -> v.Name)

        let rec wrap (bindings : list<Var * Expr>) (b : Expr) =
            match bindings with
                | [] -> b
                | (v,e) :: rest -> Expr.Let(v,e, wrap rest b)

        wrap bindings sum

    open FShade
    open FShade.Imperative
    open System

    let toReflectedCall (fetch : Expr<'c> -> string -> Option<'c> -> Expr<'v>)  (p : Term<'c>) =
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

    let toCCode (fetch : Expr<'c> -> string -> Option<'c> -> Expr<float>)  (p : Term<'c>) =
        let call = toReflectedCall fetch p

        let coord : Expr<'c> =
            if typeof<'c> = typeof<int> then <@ getGlobalId().X @> |> Expr.Cast
            elif typeof<'c> = typeof<V2i> then <@ getGlobalId().XY @> |> Expr.Cast
            elif typeof<'c> = typeof<V3i> then <@ getGlobalId() @> |> Expr.Cast
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
