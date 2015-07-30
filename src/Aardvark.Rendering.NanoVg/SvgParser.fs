#if COMPILED
namespace Aardvark.Rendering.NanoVg
#else
#r "System.Xml.dll"
#r "System.Xml.Linq.dll"
#endif
open System
open System.IO
open System.Xml
open System.Xml.Linq
open Aardvark.Base
open Aardvark.Base.Incremental

module Svg =
    
    module private Parser =
        let ns = "http://www.w3.org/2000/svg"

        let private xname str =
            XName.Get(str, ns)
    
        let private attname str =
            XName.Get(str)
    

        let private tryGetAttribute (name : string) (e : XElement) =
            let att = e.Attribute(attname name)
            match att with
                | null -> None
                | a -> Some a.Value

        let private getAttribute (name : string) (e : XElement) =
            let att = e.Attribute(attname name)
            att.Value

        let private parseColor (str : string) : Option<C4f> =
            if str.StartsWith "#" then
                let str = str.Substring 1
                let l = str.Length
                let componentLength = l / 3

                let readhex (str : string) = 
                    let mutable res = 255
                    if System.Int32.TryParse(str, System.Globalization.NumberStyles.HexNumber, null, &res) then
                        if l = 3 then
                            res * 16
                        else
                            res
                    else
                        255


                let r = str.Substring(0 * componentLength, componentLength) |> readhex
                let g = str.Substring(1 * componentLength, componentLength) |> readhex
                let b = str.Substring(2 * componentLength, componentLength) |> readhex

                C4b(byte r, byte g, byte b).ToC4f() |> Some

            else
                match str with
                    | "black" -> Some C4f.Black
                    | "white" -> Some C4f.White
                    | "red" -> Some C4f.Red
                    | "green" -> Some C4f.Green
                    | "blue" -> Some C4f.Blue
                    | "none" -> Some (C4f(0.0, 0.0, 0.0, 0.0))
                    | _ ->
                        None


        let private whiteSpaceRx = System.Text.RegularExpressions.Regex @"[ \t\r\n]+"
        let private functionRx = System.Text.RegularExpressions.Regex @"(?<name>[a-zA-Z_]+)\((?<args>.*)\)"
        let private parseTrafo (str : string) : Option<M33d> =
            let str = whiteSpaceRx.Replace(str, "")
            let mutable m = functionRx.Match str
            let mutable trafo = M33d.Identity
            while m.Success do
                let f = m.Groups.["name"].Value
                let args = m.Groups.["args"].Value.Split(',') |> Array.map float

                let mat = 
                    match f.ToLower() with
                        | "matrix" -> 
                            if args.Length >= 6 then
                                M33d(args.[0], args.[1], args.[2], args.[3], args.[4], args.[5], 0.0, 0.0, 1.0)
                            else
                                Log.warn "invalid matrix command: %A" m.Value
                                M33d.Identity
                        | "translate" ->
                            match args with
                                | [|x; y|] -> M33d.Translation(x,y)
                                | [|x|] -> M33d.Translation(x,0.0)
                                | _ ->
                                    Log.warn "invalid translate command: %A" m.Value
                                    M33d.Identity
                        | "scale" ->
                            match args with
                                | [|x; y|] -> M33d.Scale(x,y)
                                | [|x|] -> M33d.Scale(x)
                                | _ ->
                                    Log.warn "invalid scale command: %A" m.Value
                                    M33d.Identity

                        | "rotate" ->
                            match args with
                                | [| angle |] -> 
                                    M33d.Rotation(-Constant.RadiansPerDegree * angle)
                                | [| angle; cx; cy|] ->
                                    let rot = M33d.Rotation(-Constant.RadiansPerDegree * angle)
                                    M33d.Translation(cx, cy) * rot * M33d.Translation(-cx, -cy)
                                | _ -> 
                                    Log.warn "invalid rotate command: %A" m.Value
                                    M33d.Identity

                        | "skewx" ->
                            if args.Length > 0 then
                                M33d(1.0, tan (Constant.RadiansPerDegree * args.[0]), 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0)
                            else
                                Log.warn "invalid skewX command: %A" m.Value
                                M33d.Identity

                        | "skewy" ->
                            if args.Length > 0 then
                                M33d(1.0, 0.0, 0.0, tan (Constant.RadiansPerDegree * args.[0]), 1.0, 0.0, 0.0, 0.0, 1.0)
                            else
                                Log.warn "invalid skewX command: %A" m.Value
                                M33d.Identity
                        | _ ->
                            Log.warn "unknown command: %A" m.Value
                            M33d.Identity

                trafo <- mat * trafo
                m <- m.NextMatch()
            

            if trafo.IsIdentity(Constant.PositiveTinyValue) then
                None
            else
                Some trafo


        let private parseAlign (str : string) : Option<TextAlign> =
            match str.ToLower() with
                | "start" -> Some TextAlign.Left
                | "middle" -> Some TextAlign.Center
                | "end" -> Some TextAlign.Right
                | _ -> Some TextAlign.Left


        let private readers =

            let wrap (f : string -> bool * 'a) (str : string) =
                let res = ref Unchecked.defaultof<'a>
                match f str with
                    | (true, v) -> Some (v :> obj)
                    | _ -> None

            let cast (f : string -> Option<'a>) (str : string) =
                match f str with
                    | Some v -> Some (v :> obj)
                    | None -> None
                                     
            Dictionary.ofList [
                typeof<string>, cast Some
                typeof<int>, wrap System.Int32.TryParse
                typeof<float>, wrap System.Double.TryParse
                typeof<C4f>, cast parseColor
                typeof<M33d>, cast parseTrafo
                typeof<TextAlign>, cast parseAlign
            ]


        let private tryReadAttribute<'a> (name : string) (e : XElement) =
            let att = e.Attribute (attname name)
            match att with
                | null -> None
                | att ->
                    match readers.TryGetValue typeof<'a> with
                        | (true, r) -> 
                            match r att.Value with
                                | Some (:? 'a as r) -> Some r
                                | _ -> None
                        | _ -> None

        let private withDefault (d : 'a) (o : Option<'a>) =
            match o with
                | Some v -> v
                | None -> d


        type ParseState =
            {
                fillColor : C4f
                strokeColor : C4f
            }

        let maybeApp (f : IMod<'b> -> 'a -> 'a) (o : Option<'b>) =
            match o with
                | Some b -> fun a -> f (Mod.constant b) a
                | _ -> id


        

        module PathParser = 
            open System.Text.RegularExpressions
            open Aardvark.Base.Monads.State
            type PathState = { text : Text; pos : V2d; lastC2 : Option<V2d>; lastCommand : Option<string> }

            let charRx = Regex @"^[a-zA-Z]"
            let floatRx = Regex @"^[-+]?[0-9]*\.?[0-9]+([eE][-+]?[0-9]+)?"
            let isWhiteSpace (c : char) =
                c = ' ' || c = '\t' || c = '\r' || c = '\n' || c = ','

            let readChar = { runState = fun s -> s.text.[0], { s with text = s.text.SubText(1).TrimmedAtStart(fun c -> isWhiteSpace c) }}
            let readCommand = 
                { runState = 
                    fun s -> 
                        let cmd = string s.text.[0]
                        if not (charRx.IsMatch cmd) then
                            s.lastCommand.Value, s
                        else
                            cmd, 
                            { s with text = s.text.SubText(1).TrimmedAtStart(fun c -> isWhiteSpace c); lastCommand = Some cmd }
                }

            let isEmpty = { runState = fun s -> s.text.Count = 0, s}

            let readDouble = 
                { runState = fun s ->
                    let m = floatRx.Match(s.text.String, s.text.Start, s.text.Count)
                    if m.Success then
                        float m.Value, { s with text = s.text.SubText(m.Length).TrimmedAtStart(fun c -> isWhiteSpace c)}
                    else
                        failwithf "expected a double at: %A" s.text
                }

            let readV2d =
                state {
                    let! x = readDouble
                    let! y = readDouble
                    return V2d(x,y)
                }

            let setControlPoints (c0 : V2d) (c1 : V2d) =
                { runState = fun s -> (), { s with lastC2 = Some c1 }}

            let lastC1 = 
                { runState = fun s -> (match s.lastC2 with | Some c -> c | _ -> s.pos), s}

            let currentPos = { runState = fun s -> s.pos, s}
            let setPos p = { runState = fun s -> (), { s with pos = p } }

            let readPos (relative : bool) =
                state {
                    if relative then
                        let! current = currentPos
                        let! v = readV2d
                        return v + current
                    else
                        return! readV2d
                }

            let rec parsePath() =
                state {
                    let! empty = isEmpty
                    if empty then 
                        return []
                    else
                        let! cmd = readCommand
                        
                        match cmd with
                            | "m" | "M" ->
                                let! p = readPos (cmd = "m")
                                let cmd = MoveTo p
                                
                                do! setPos p

                                let! rest = parsePath()
                                return cmd::rest

                            | "c" | "C" ->
                                let rel = cmd = "c"
                                let! c0 = readPos rel
                                let! c1 = readPos rel
                                let! p1 = readPos rel

                                let cmd = BezierTo(c0, c1, p1)

                                do! setControlPoints c0 c1
                                do! setPos p1

                                let! rest = parsePath()
                                return cmd::rest

                            | "s" | "S" ->
                                let rel = cmd = "s"
                                let! current = currentPos
                                let! c1 = readPos rel
                                let! p = readPos rel


                                let! lastC1 = lastC1


                                let c0 = current - (lastC1 - current)

//                                let dir = (p - current).Normalized
//                                let n = V2d(-dir.X, dir.Y)
//                                let rc2 = c2 - current
//                                let c1 = current + (rc2 - 2.0 * n * n.Dot(rc2))


                                do! setControlPoints c0 c1
                                let cmd = BezierTo(c0, c1, p)

                                do! setPos p

                                let! rest = parsePath()
                                return cmd::rest


                            | "q" | "Q" ->
                                let rel = cmd = "q"
                                let! c = readPos rel
                                let! p = readPos rel

                                let cmd = QuadraticTo(c,p)

                                do! setPos p

                                let! rest = parsePath()
                                return cmd::rest

                            | "l" | "L" ->
                                let! p = readPos (cmd = "l")
                                let cmd = LineTo p
                                do! setPos p
                                
                                let! rest = parsePath()
                                return cmd::rest

                            | "h" | "H" ->
                                let! x = readDouble
                                let! p = currentPos

                                let pos =
                                    if cmd = "h" then V2d(p.X + x, p.Y)
                                    else V2d(x, p.Y)

                                let cmd = LineTo pos
                                do! setPos pos
                                
                                let! rest = parsePath()
                                return cmd::rest

                            | "v" | "V" ->
                                let! y = readDouble
                                let! p = currentPos

                                let pos =
                                    if cmd = "v" then V2d(p.X, p.Y + y)
                                    else V2d(p.X, y)

                                let cmd = LineTo pos
                                do! setPos pos
                                
                                let! rest = parsePath()
                                return cmd::rest

                            | "z" | "Z" ->
                                return [ClosePath]


                            | _ ->
                                return failwith "cannot parse path"
                }
        
            let parse (p : string) =
                let state = { text = Text(p).TrimmedAtStart(fun c -> isWhiteSpace c); pos = V2d.Zero; lastC2 = None; lastCommand = None }
                let (path, s) = parsePath().runState state
                path

        let rec parseNode (state : ParseState) (n : XElement) : INvg =
            let ownFill = n |> tryReadAttribute "fill"
            let ownStroke = n |> tryReadAttribute "stroke"

            let fill = ownFill |> withDefault state.fillColor
            let stroke = ownStroke |> withDefault state.strokeColor
            let strokeWidth = n |> tryReadAttribute "stroke-width"
            let transform = n |> tryReadAttribute "transform"
            let fontSize = n |> tryReadAttribute "font-size"
            let fontFamily = n |> tryReadAttribute "font-family" |> Option.map (fun n -> SystemFont(n, FontStyle.Regular))
            let anchor = n |> tryReadAttribute "text-anchor"

            let ret sg =
                sg |> maybeApp Nvg.fillColor ownFill
                   |> maybeApp Nvg.strokeColor ownStroke
                   |> maybeApp Nvg.strokeWidth strokeWidth
                   |> maybeApp Nvg.trafo transform
                   |> maybeApp Nvg.fontSize fontSize
                   |> maybeApp Nvg.font fontFamily
                   |> maybeApp Nvg.align anchor

            let renderPrimitive (p : Primitive) =
                let leafs = 
                    if stroke.A > 0.0f && fill.A > 0.0f then
                        let rect = Mod.constant p
                        [Nvg.fill rect; Nvg.stroke rect]
                    elif stroke.A > 0.0f then
                        [p |> Mod.constant |> Nvg.stroke]
                    elif fill.A > 0.0f then
                        [p |> Mod.constant |> Nvg.fill]
                    else
                        []

                leafs
                    |> Nvg.ofList
                    |> ret


            match n.Name.LocalName with
                | "a" | "g" ->
                    let state = match ownFill with | Some f -> { state with fillColor = f } | _ -> state
                    let state = match ownStroke with | Some s -> { state with strokeColor = s } | _ -> state

                    n.Elements()
                        |> Seq.toList
                        |> List.map (parseNode state)
                        |> Nvg.ofList
                        |> ret

                | "rect" ->
                    let x = n |> tryReadAttribute "x" |> withDefault 0.0
                    let y = n |> tryReadAttribute "y" |> withDefault 0.0
                    let w = n |> tryReadAttribute "width" |> withDefault 1.0
                    let h = n |> tryReadAttribute "height" |> withDefault 1.0

                    let rx = n |> tryReadAttribute "rx" |> withDefault 0.0
                    let ry = n |> tryReadAttribute "ry" |> withDefault 0.0

                    
                    let box = Box2d.FromMinAndSize(x, y, w, h)

                    if rx = 0.0 && ry = 0.0 then
                        Rectangle(box) |> renderPrimitive
                    else
                        RoundedRectangle(box, float rx) |> renderPrimitive


                | "circle" ->

                    let cx = n |> tryReadAttribute "cx" |> withDefault 0.0
                    let cy = n |> tryReadAttribute "cy" |> withDefault 0.0
                    let r = n |> tryReadAttribute "r" |> withDefault 1.0

                    Circle(V2d(cx,cy), r) |> renderPrimitive

                | "ellipse" ->

                    let cx = n |> tryReadAttribute "cx" |> withDefault 0.0
                    let cy = n |> tryReadAttribute "cy" |> withDefault 0.0
                    let rx = n |> tryReadAttribute "rx" |> withDefault 1.0
                    let ry = n |> tryReadAttribute "ry" |> withDefault 1.0

                    Ellipse(V2d(cx,cy), V2d(rx,ry)) |> renderPrimitive


                | "path" ->
                    
                    let segments = n |> tryReadAttribute "d" |> withDefault ""

  
                    segments 
                        |> PathParser.parse
                        |> Primitive.Path
                        |> renderPrimitive


                | "polyline" ->
                    let points = n |> tryReadAttribute "points" |> withDefault ""

                    let readPoint (str : string) =
                        let arr = str.Split ','
                        V2d(float (arr.[0].Trim()), float (arr.[1].Trim()))

                    let arr = points.SplitOnWhitespace() |> Array.map readPoint

                    if arr.Length > 0 then
                        let commands =
                            [
                                yield MoveTo(arr.[0])
                                for i in 1..arr.Length-1 do
                                    yield LineTo(arr.[i])
                            ]


                        commands 
                            |> Primitive.Path
                            |> renderPrimitive
                    else
                        Nvg.empty

                | "polygon" ->
                    let points = n |> tryReadAttribute "points" |> withDefault ""

                    let readPoint (str : string) =
                        let arr = str.Split ','
                        V2d(float (arr.[0].Trim()), float (arr.[1].Trim()))

                    let arr = points.SplitOnWhitespace() |> Array.map readPoint

                    if arr.Length > 0 then
                        let commands =
                            [
                                yield MoveTo(arr.[0])

                                for i in 1..arr.Length-1 do
                                    yield LineTo(arr.[i])

                                yield ClosePath
                            ]


                        commands 
                            |> Primitive.Path
                            |> renderPrimitive
                    else
                        Nvg.empty

                | "text" ->
                    let x = n |> tryReadAttribute "x" |> withDefault 0.0
                    let y = n |> tryReadAttribute "y" |> withDefault 0.0
                    let content = n.Value
                    Nvg.text (Mod.constant content)
                        |> Nvg.trafo (Mod.constant (M33d.Translation(V2d(x,y))))
                        |> ret

                | "line" ->
                    let x0 = n |> tryReadAttribute "x1" |> withDefault 0.0
                    let y0 = n |> tryReadAttribute "y1" |> withDefault 0.0
                    let x1 = n |> tryReadAttribute "x2" |> withDefault 0.0
                    let y1 = n |> tryReadAttribute "y2" |> withDefault 0.0

                    [MoveTo(V2d(x0, y0)); LineTo(V2d(x1, y1))]
                        |> Primitive.Path
                        |> renderPrimitive

                | _ -> 
                    Log.warn "unknown element-type: %A" n.Name.LocalName
                    Nvg.empty


        let parseDocument (d : XDocument) : INvg =
            let initialState = { fillColor = C4f.Black; strokeColor = C4f(0.0,0.0,0.0,0.0) }
            d.Root.Elements()
                |> Seq.toList
                |> List.map (parseNode initialState)
                |> Nvg.ofList
                |> Nvg.fillColor (Mod.constant C4f.Black)
                |> Nvg.align (Mod.constant (TextAlign.Left ||| TextAlign.BaseLine))
                //|> Nvg.trafo (Mod.constant (M33d.Translation(V2d(400.0, 300.0)) * M33d.Scale(2.0)))

    let ofString (str : string) =
        str |> XDocument.Parse |> Parser.parseDocument

    let ofFile (path : string) =
        path |> XDocument.Load |> Parser.parseDocument

    let ofStream (stream : Stream) =
        stream |> XDocument.Load |> Parser.parseDocument