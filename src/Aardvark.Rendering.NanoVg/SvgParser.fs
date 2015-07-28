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
                    | _ ->
                        None


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
            type PathState = { text : Text; pos : V2d; lastC2 : Option<V2d> }

            let floatRx = Regex @"^[-+]?[0-9]*\.?[0-9]+([eE][-+]?[0-9]+)?"
            let isWhiteSpace (c : char) =
                c = ' ' || c = '\t' || c = '\r' || c = '\n' || c = ','

            let readChar = { runState = fun s -> s.text.[0], { s with text = s.text.SubText(1).TrimmedAtStart(fun c -> isWhiteSpace c) }}
            let readCommand = { runState = fun s -> (string s.text.[0]), { s with text = s.text.SubText(1).TrimmedAtStart(fun c -> isWhiteSpace c) }}
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
                let state = { text = Text(p).TrimmedAtStart(fun c -> isWhiteSpace c); pos = V2d.Zero; lastC2 = None }
                let (path, s) = parsePath().runState state
                path

        let rec parseNode (state : ParseState) (n : XElement) : INvg =
            let ownFill = n |> tryReadAttribute "fill"
            let ownStroke = n |> tryReadAttribute "stroke"

            let fill = ownFill |> withDefault state.fillColor
            let stroke = ownStroke |> withDefault state.strokeColor

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
                    |> maybeApp Nvg.fillColor ownFill
                    |> maybeApp Nvg.strokeColor ownStroke

            match n.Name.LocalName with
                | "a" | "g" ->
                    let state = match ownFill with | Some f -> { state with fillColor = f } | _ -> state
                    let state = match ownStroke with | Some s -> { state with strokeColor = s } | _ -> state

                    n.Elements()
                        |> Seq.toList
                        |> List.map (parseNode state)
                        |> Nvg.ofList
                        |> maybeApp Nvg.fillColor ownFill
                        |> maybeApp Nvg.strokeColor ownStroke

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

                | "path" ->
                    
                    let segments = n |> tryReadAttribute "d" |> withDefault ""
                    let width = n |> tryReadAttribute "stroke-width"

  
                    segments 
                        |> PathParser.parse
                        |> Primitive.Path
                        |> renderPrimitive
                        |> maybeApp Nvg.strokeWidth width

                | "polygon" ->
                    let points = n |> tryReadAttribute "points" |> withDefault ""
                    let width = n |> tryReadAttribute "stroke-width"

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
                            |> maybeApp Nvg.strokeWidth width
                    else
                        Nvg.empty


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
                |> Nvg.trafo (Mod.constant (M33d.Scale(5.0)))


    let ofString (str : string) =
        str |> XDocument.Parse |> Parser.parseDocument

    let ofFile (path : string) =
        path |> XDocument.Load |> Parser.parseDocument

    let ofStream (stream : Stream) =
        stream |> XDocument.Load |> Parser.parseDocument