namespace Opc

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Rendering
open Aardvark.Rendering.Text
open Aardvark.Application
open Aardvark.Application.Utilities

type OverlayPosition =
    | None      = 0x00
    | Top       = 0x01
    | Bottom    = 0x02
    | Left      = 0x10
    | Right     = 0x20

type OverlayConfig =
    {
        pos : OverlayPosition
    }

type TableEntry =
    | Text of value : aval<string>
    | Time of value : aval<MicroTime>
    | Number of value : aval<float> * unit : string * digits : int
    | Progress of value : aval<float>
    | ColSpan of width : int * child : TableEntry
    | Concat of content : list<TableEntry>
    
type Table =
    {
        prefix      : string
        suffix      : string
        separator   : string
        entries     : list<list<TableEntry>>
    }

module HeatMap =
    let private heatMapColors =
        let fromInt (i : int) =
            C4b(
                byte ((i >>> 16) &&& 0xFF),
                byte ((i >>> 8) &&& 0xFF),
                byte (i &&& 0xFF),
                255uy
            )

        Array.map fromInt [|
            0x1639fa
            0x2050fa
            0x3275fb
            0x459afa
            0x55bdfb
            0x67e1fc
            0x72f9f4
            0x72f8d3
            0x72f7ad
            0x71f787
            0x71f55f
            0x70f538
            0x74f530
            0x86f631
            0x9ff633
            0xbbf735
            0xd9f938
            0xf7fa3b
            0xfae238
            0xf4be31
            0xf29c2d
            0xee7627
            0xec5223
            0xeb3b22
        |]

    let color (tc : float) =
        let tc = clamp 0.0 1.0 tc
        let fid = tc * float heatMapColors.Length - 0.5

        let id = int (floor fid)
        if id < 0 then 
            heatMapColors.[0]
        elif id >= heatMapColors.Length - 1 then
            heatMapColors.[heatMapColors.Length - 1]
        else
            let c0 = heatMapColors.[id]
            let c1 = heatMapColors.[id + 1]
            let t = fid - float id
            (c0 * (1.0 - t) + c1 * t)



module Table =
    open System.Text

    module private Seq = 
        let range (l : seq<int>) =
            let mutable r = Range1i.Invalid
            for e in l do
                r.ExtendBy e
            r


    let format (suffix : string) (d : int) (v : float) (u : string) =  
        let fmt = 
            if d > 1 then "0." + System.String('0', 1) + System.String('#', d - 1)
            elif d > 0 then "0." + System.String('0', 1)
            else "0"

        v.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture) + suffix + u

    let private numberString (v : float) (u : string) (d : int) =
        let a = abs v
        if v = 0.0 then "0"
        elif a >= 1000000000000000000000000.0 then format "Y" d (v / 1000000000000000000000000.0) u
        elif a >= 1000000000000000000000.0 then format "Z" d (v / 1000000000000000000000.0) u
        elif a >= 1000000000000000000.0 then format "E" d (v / 1000000000000000000.0) u
        elif a >= 1000000000000000.0 then format "P" d (v / 1000000000000000.0) u
        elif a >= 1000000000000.0 then format "T" d (v / 1000000000000.0) u
        elif a >= 1000000000.0 then format "G" d (v / 1000000000.0) u
        elif a >= 1000000.0 then format "M" d (v / 1000000.0) u
        elif a >= 1000.0 then format "k" d (v / 1000.0) u
        elif a >= 1.0 then format "" d v u
        elif a >= 0.0010 then format "m" d (v * 1000.0) u
        elif a >= 0.0000010 then format "µ" d (v * 1000000.0) u
        elif a >= 0.0000000010 then format "n" d (v * 1000000000.0) u
        elif a >= 0.0000000000010 then format "p" d (v * 1000000000000.0) u
        elif a >= 0.0000000000000010 then format "f" d (v * 1000000000000000.0) u
        elif a >= 0.0000000000000000010 then format "a" d (v * 1000000000000000000.0) u
        elif a >= 0.0000000000000000000010 then format "z" d (v * 1000000000000000000000.0) u
        elif a >= 0.0000000000000000000000010 then format "y" d (v * 1000000000000000000000000.0) u
        else
            let e = int (Fun.Log10 a)
            let r = v / pown 10.0 e
            format (sprintf "E%d" e) d r u

    let rec private toStringColSpan (t : AdaptiveToken) (id : ref<int>) (progressTable : ref<Map<string, float>>) (e : TableEntry) =
        match e with
        | Text s -> 
            1, s.GetValue t
        | Time v -> 
            1, v.GetValue t |> string

        | Number(v,u,d) ->
            let v = v.GetValue t
            let str = numberString v u d
            1, str
        | Progress v -> 
            let v = v.GetValue t
            let i = !id
            let splice = "{" + i.ToString("X6") + "}"
            id := i + 1
            progressTable := Map.add splice v !progressTable
            1, splice

        | Concat es ->
            let rs = es |> Seq.map (toStringColSpan t id progressTable >> snd) |> String.concat ""
            1, rs
        | ColSpan(w, e) ->
            let (_, v) = toStringColSpan t id progressTable e
            w, v
  
    let rec private toShapeColSpan (cfg : TextConfig) (t : AdaptiveToken) (id : ref<int>) (progressTable : ref<Map<string, float>>) (e : TableEntry) =
        match e with
        | Text v -> 
            let v = v.GetValue t
            let shape = cfg.Layout v
            1, shape
            
        | Time v -> 
            let v = v.GetValue t
            let shape = cfg.Layout (string v)
            1, shape

        | Number(v,u,d) ->
            let v = v.GetValue t
            let str = numberString v u d
            let shape = cfg.Layout str
            1, shape
            
        | Concat es ->
            let rs = es |> List.map (toShapeColSpan cfg t id progressTable >> snd)
            
            let res = 
                match rs with
                | [] -> failwith ""
                | h :: t ->
                    t |> List.fold (ShapeList.appendHorizontal 0.0) h

            1, res
            
        | Progress v -> 
            1, ShapeList.empty
            
        | ColSpan(w, e) ->
            let (_, v) = toShapeColSpan cfg t id progressTable e
            w, v

  
    let private progressBar (v : float) (box : Box2d) =
        let q = clamp 0.03 1.0 v
        let w = 0.1
        let offset = box.Min
        let size =  V2d(box.Size.X, min 0.8 box.Size.Y)
        let bgBounds = Box2d.FromMinAndSize(offset, size).ShrunkBy(w / 2.0)
        let curBounds = Box2d.FromMinAndSize(bgBounds.Min, V2d(bgBounds.SizeX * q, bgBounds.SizeY)).ShrunkBy(w / 2.0)
        let mutable c = HeatMap.color(1.0 - q)
        
        if q < 1.0 then c.A <- 200uy
        else c.A <- 150uy

        [
            yield ConcreteShape.roundedRectangle (C4b(255uy,255uy,255uy,255uy)) w 0.8 bgBounds
            yield ConcreteShape.fillRoundedRectangle c 0.8 curBounds
        ]
              
              
    let layoutNew (cfg : TextConfig) (table : Table) =
        AVal.custom (fun t ->
            let id = ref 0
            let progress = ref Map.empty
            let entries = table.entries |> List.map (List.map (toShapeColSpan cfg t id progress))
            
            let columns =
                entries |> Seq.map (List.sumBy fst) |> Seq.max
            

            let colLengths =
                let lengths = Array.zeroCreate columns
                entries |> List.iter (fun row ->
                    let mutable c = 0
                    row |> List.iter (fun (span,v) ->
                        if span = 1 then
                            lengths.[c] <- max v.bounds.SizeX lengths.[c]
                        c <- c + span
                    )
                )
                lengths 

            entries |> List.iter (fun row ->
                let mutable c = 0
                row |> List.iter (fun (span,v) ->
                    if span > 1 then
                        let sum = Seq.init span (fun i -> colLengths.[c + i]) |> Seq.sum
                        let sum = sum 
                        if sum < v.bounds.SizeX then
                            let missing = v.bounds.SizeX - sum
                            let realSum = Seq.init span (fun i -> colLengths.[c + i]) |> Seq.sum
                            for i in 0 .. span - 1 do
                                let rel = colLengths.[c + i] / realSum
                                let inc = rel * missing
                                colLengths.[c + i] <- colLengths.[c + i] + inc
   
                    c <- c + span
                )
            ) 
            
            for i in 0 .. columns - 2 do colLengths.[i] <- colLengths.[i] + 2.0
            
            let negativeV = 0.3
            let rows =
                entries |> List.map (fun row ->
                    let mutable res = ShapeList.empty

                    let mutable offset = 0.0
                    let mutable c = 0
                    for (span, e) in row do
                        let l = colLengths.[c]

                        let e = ShapeList.translate (V2d(offset, 0.0)) e
                        res <- ShapeList.append res e

                        offset <- offset + l
                        c <- c + span
                        

                    let mutable offset = 0.0
                    let mutable c = 0
                    for (span, _) in row do
                        if c > 0 then                           
                            let width = 0.02
                            let vLine = ConcreteShape.fillRectangle C4b.White (Box2d(-width/2.0, res.bounds.Min.Y + negativeV, width / 2.0, res.bounds.Max.Y))
                            let vLine = [{ vLine with z = 100 }] |> ShapeList.ofList
                            res <- ShapeList.append res (ShapeList.translate (V2d(offset - 1.0, 0.0)) vLine)
                        offset <- offset + colLengths.[c]
                        c <- c + span
                        

                    res
                )

                
            let mutable offset = 0.0
            let mutable res = ShapeList.empty
            for r in rows do
                res <- ShapeList.append res (ShapeList.translate (V2d(0.0, offset)) r)
                offset <- offset - r.bounds.SizeY + negativeV

            
            //let width = 0.02
            //let vLine = ConcreteShape.fillRectangle C4b.White (Box2d(-width/2.0, res.bounds.Min.Y, width / 2.0, res.bounds.Max.Y))
            //let vLine = [{ vLine with z = 100 }] |> ShapeList.ofList

            //let mutable offset = colLengths.[0] - 1.0
            //for i in 1 .. columns - 1 do
            //    res <- ShapeList.union res (ShapeList.translated (V2d(offset, 0.0)) vLine)
            //    offset <- offset + colLengths.[i]


            res
        )

    let layout (cfg : TextConfig) (table : Table) =
        AVal.custom (fun t ->
            let id = ref 0
            let progress = ref Map.empty
            let entries = table.entries |> List.map (List.map (toStringColSpan t id progress))
            let progress = !progress

            let columns =
                entries |> Seq.map (List.sumBy fst) |> Seq.max
            
            let colLengths =
                let lengths = Array.zeroCreate columns
                entries |> List.iter (fun row ->
                    let mutable c = 0
                    row |> List.iter (fun (span,v) ->
                        //let strLen = v.Length + table.separator.Length
                        //let v = ()
                        
                        if span = 1 then
                            lengths.[c] <- max v.Length lengths.[c]
                        //let sum = Seq.init span (fun i -> lengths.[c + i]) |> Seq.sum
                        //if sum < strLen then
                        //    let mutable missing = strLen - sum
                        //    let inc = int (float missing / float span)
                        //    for i in 0 .. span - 1 do
                        //        lengths.[c + i] <- lengths.[c + i] + inc
                        //        missing <- missing - inc
                        //    if missing > 0 then
                        //        lengths.[c + span - 1] <- lengths.[c + span - 1] + missing

                        c <- c + span
                    )
                )
                lengths 
                
            entries |> List.iter (fun row ->
                let mutable c = 0
                row |> List.iter (fun (span,v) ->
                    if span > 1 then
                        let sum = Seq.init span (fun i -> colLengths.[c + i] + table.separator.Length) |> Seq.sum
                        let sum = sum - table.separator.Length
                        if sum < v.Length then
                            let missing = v.Length - sum
                            if missing <= span then
                                for i in 0 .. missing - 1 do colLengths.[c + i] <- colLengths.[c + i] + 1
                            else
                                let mutable rem = missing
                                let realSum = Seq.init span (fun i -> colLengths.[c + i]) |> Seq.sum
                                for i in 0 .. span - 1 do
                                    let rel = float colLengths.[c + i] / float realSum
                                    let inc = int (rel * float missing)
                                    colLengths.[c + i] <- colLengths.[c + i] + inc
                                    rem <- rem - inc

                                if rem > 0 then
                                    colLengths.[c + span - 1] <- colLengths.[c + span - 1] + rem
                                    

                    c <- c + span
                )
            )

            let content =
                let b = StringBuilder()
                entries |> List.iter (fun row ->
                    let mutable c = 0

                    b.Append(table.prefix) |> ignore

                    for (span, e) in row do
                        let len = 
                            let l = Seq.init span (fun i -> colLengths.[c + i] + table.separator.Length) |> Seq.sum
                            l - table.separator.Length
                        b.Append(e) |> ignore

                        if len > e.Length then
                            b.Append(' ', len - e.Length) |> ignore
                            
                        c <- c + span
                        if c < columns then b.Append(table.separator) |> ignore
                        else b.Append(table.suffix) |> ignore

                    b.AppendLine() |> ignore
                )
                b.ToString()
                
            let mutable shapes = cfg.Layout content

            for (splice, value) in Map.toSeq progress do
                shapes <- ShapeList.replaceString splice (progressBar value) shapes

            shapes
        )

module Overlay =
    open System
    open System.Reflection
    open Aardvark.Base.IL
    open Microsoft.FSharp.Reflection

    type private Format private() =
        static member Format(t : MicroTime) = t.ToString()
        static member Format(t : Mem) = t.ToString()

    module private String =
        let ws = System.String(' ', 512)

        let padRight (len : int) (str : string) =
            if len > str.Length then str + ws.Substring(0, len - str.Length)
            else str
            
        let padLeft (len : int) (str : string) =
            if len > str.Length then ws.Substring(0, len - str.Length) + str
            else str
            
        let padCenter (len : int) (str : string) =
            if len > str.Length then 
                let m = len - str.Length
                let l = m / 2
                let r = m - l
                ws.Substring(0, l) + str + ws.Substring(0, r)
            else 
                str

    module private Reflection =
        open System.Reflection

        let getFields (t : Type) =
            if FSharpType.IsTuple t then 
                let args = FSharpType.GetTupleElements t
                Array.init args.Length (fun i ->
                    let (p,_) = FSharpValue.PreComputeTuplePropertyInfo(t, i)
                    p
                )
            elif FSharpType.IsRecord(t, true) then 
                FSharpType.GetRecordFields(t, true)
            else
                failwith "unexpected type"

        let private formatters = System.Collections.Concurrent.ConcurrentDictionary<Type, MethodInfo>()

        let private getFormatMethod (t : Type) =
            formatters.GetOrAdd(t, fun t ->
                let m = typeof<Format>.GetMethod("Format", BindingFlags.NonPublic ||| BindingFlags.Public ||| BindingFlags.Static, Type.DefaultBinder, [| t |], null)
                if isNull m || m.ReturnType <> typeof<string> then
                    t.GetMethod("ToString", BindingFlags.NonPublic ||| BindingFlags.Public ||| BindingFlags.Instance, Type.DefaultBinder, [||], null)
                else
                    m
            )

        let getFormatter (p : PropertyInfo) : obj -> string =
            let m = getFormatMethod p.PropertyType
            cil {
                do! IL.ldarg 0
                do! IL.call p.GetMethod
                do! IL.call m
                do! IL.ret
            }


    let table (cfg : aval<OverlayConfig>) (viewport : aval<V2i>) (table : aval<Table>) =
        let fgAlpha = byte (255.0 * 1.0)
        let bgAlpha = byte (255.0 * 0.6)
        let config = 
            { TextConfig.Default with 
                font = Aardvark.Base.Fonts.Font "Blackadder ITC"
                color = C4b(255uy, 255uy, 255uy, fgAlpha) 
                align = TextAlignment.Left
                flipViewDependent = false
            }
        
        let content =
            table |> AVal.bind (Table.layoutNew config)

        let shapes =


            content |> AVal.map (fun shapes ->
                let realBounds = shapes.bounds
                let bounds = realBounds.EnlargedBy(0.5, 0.5, 0.2, 0.5)
                let rect = ConcreteShape.fillRoundedRectangle (C4b(0uy,0uy,0uy,bgAlpha)) 0.8 bounds
                ShapeList.prepend rect shapes
            )


        let trafo =
            AVal.custom (fun t ->
                let s = viewport.GetValue t
                let shapes = shapes.GetValue t
                let cfg = cfg.GetValue t
                let bounds = shapes.bounds
                let fontSize = 18.0
                let padding = 5.0

                let verticalShift =
                    if cfg.pos.HasFlag(OverlayPosition.Top) then V3d(0.0, -bounds.Max.Y - padding / fontSize, 0.0)
                    elif cfg.pos.HasFlag(OverlayPosition.Bottom) then V3d(0.0, -bounds.Min.Y + padding / fontSize, 0.0)
                    else V3d(0.0, -bounds.Center.Y, 0.0)
                    
                let horizontalShift =
                    if cfg.pos.HasFlag(OverlayPosition.Left) then V3d(-bounds.Min.X + padding / fontSize, 0.0, 0.0)
                    elif cfg.pos.HasFlag(OverlayPosition.Right) then V3d(-bounds.Max.X - padding / fontSize, 0.0, 0.0)
                    else V3d(-bounds.Center.X, 0.0, 0.0)

                let finalPos =
                    let v = 
                        if cfg.pos.HasFlag(OverlayPosition.Top) then V3d(0,1,0)
                        elif cfg.pos.HasFlag(OverlayPosition.Bottom) then V3d(0,-1,0)
                        else V3d.Zero
                    
                    let h = 
                        if cfg.pos.HasFlag(OverlayPosition.Left) then V3d(-1, 0, 0)
                        elif cfg.pos.HasFlag(OverlayPosition.Right) then V3d(1, 0, 0)
                        else V3d.Zero
                    v + h

                Trafo3d.Translation(verticalShift + horizontalShift) * 
                Trafo3d.Scale(40.0) *

                Trafo3d.Scale(1.0 / float s.X, 1.0 / float s.Y, 1.0) *
                Trafo3d.Scale(2.0, 2.0, 2.0) *
                Trafo3d.Translation(finalPos)
            )

        Sg.shape shapes
        |> Sg.trafo trafo
        |> Sg.blendMode (AVal.constant BlendMode.Blend)
        |> Sg.viewTrafo (AVal.constant Trafo3d.Identity)
        |> Sg.projTrafo (AVal.constant Trafo3d.Identity)

[<StructuredFormatDisplay("{AsString}"); Struct>]
type private Numeric(value : int64) =
    member x.Value = value
    member private x.AsString = x.ToString()

    override x.ToString() =
        let a = abs value
        if a >= 1000000000000L then sprintf "%.3fT" (float value / 1000000000000.0)
        elif a >= 1000000000L then sprintf "%.3fG" (float value / 1000000000.0)
        elif a >= 1000000L then sprintf "%.3fM" (float value / 1000000.0)
        elif a >= 1000L then sprintf "%.1fk" (float value / 1000.0)
        else sprintf "%d" value
        
module LodRendererStats =

    let toSg (win : ISimpleRenderWindow) (stats : aval<LodRendererStats>) =

    
        let pi = "($qual$)"
        let pm = "($memo$)"
        let pt = "($pts$$)"
        
        let totalMem = stats |> AVal.map (fun s -> sprintf "%A (%A)" s.usedMemory s.allocatedMemory)
        let counts = stats |> AVal.map (fun s -> sprintf "%An / %Ap" (Numeric(int64 s.totalNodes)) (Numeric s.totalPrimitives))
        
        let prim =
            AVal.custom (fun t ->
                let v = stats.GetValue(t).totalPrimitives
                sprintf "%Atris" (Numeric v)
            )

        let renderTime = stats |> AVal.map (fun s -> string s.renderTime)

        let lines =
            [
                //"Scale",            "O", "P", cfg.pointSize |> AVal.map (sprintf "%.2f")
                //"Overlay",          "-", "+", cfg.overlayAlpha |> AVal.map (sprintf "%.1f")
                //"Boxes",            "B", "B", cfg.renderBounds |> AVal.map (function true -> "on" | false -> "off")
                //"Budget",           "X", "C/Y", cfg.budget |> AVal.map (fun v -> if v < 0L then "off" else string (Numeric v))
                //"Light",            "L", "L", cfg.lighting |> AVal.map (function true -> "on" | false -> "off")
                //"Color",            "V", "V", cfg.colors |> AVal.map (function true -> "on" | false -> "off")
                //"PlaneFit" ,        "1", "1", cfg.planeFit |> AVal.map (function true -> "on" | false -> "off")
                //"SSAO",             "2", "2", cfg.ssao |> AVal.map (function true -> "on" | false -> "off")
                //"Gamma",            "U", "I", cfg.gamma |> AVal.map (sprintf "%.2f")
                "Memory",           " ", " ", totalMem
                "Quality",          " ", " ", AVal.constant pi
                "Points",           " ", " ", prim
            ]

        let maxNameLength = 
            lines |> List.map (fun (n,l,h,_) -> n.Length) |> List.max

        let pad (len : int) (i : string) (str : string) =
            let padding = if str.Length < len then System.String(' ', len - str.Length) else ""
            sprintf "%s%s%s" str i padding

        let text =
            let lines = lines |> List.map (fun (n,l,h,v) -> pad maxNameLength ": " n, l, h, v)

            AVal.custom (fun t ->
                let lines =
                    lines |> List.map (fun (n,l,h,v) -> 
                    let v = v.GetValue t
                    (n,l,h,v)
                )

                let maxValueLength = 
                    lines |> List.map (fun (_,l,h,v) -> if System.String.IsNullOrWhiteSpace l && System.String.IsNullOrWhiteSpace h then 0 else v.Length) |> List.max
                lines 
                |> List.map (fun (n,l,h,v) -> 
                    let v = pad maxValueLength "" v
                    
                    if l <> h then
                        sprintf "%s%s (%s/%s)" n v l h
                    else    
                        if System.String.IsNullOrWhiteSpace l then
                            sprintf "%s%s" n v
                        else
                            sprintf "%s%s (%s)" n v l
                )
                |> String.concat "\r\n"
            )

        let trafo =
            win.Sizes |> AVal.map (fun s ->
                Trafo3d.Translation(1.0, -1.5, 0.0) * 
                Trafo3d.Scale(18.0) *

                Trafo3d.Scale(1.0 / float s.X, 1.0 / float s.Y, 1.0) *
                Trafo3d.Scale(2.0, 2.0, 2.0) *
                Trafo3d.Translation(-1.0, 1.0, -1.0)
            )
            
        let config = { TextConfig.Default with align = TextAlignment.Left }

        let active = AVal.init false

        let layoutWithBackground (alpha : float) (text : string) =
            let fgAlpha = byte (255.0 * (clamp 0.0 1.0 alpha))
            let bgAlpha = byte (255.0 * (clamp 0.0 1.0 alpha) * 0.6)
            let config = { config with color = C4b(255uy, 255uy, 255uy, fgAlpha) }
            let shapes = config.Layout text
            let realBounds = shapes.bounds
            let bounds = realBounds.EnlargedBy(3.0, 0.5, 0.2, 3.0)
            let rect = ConcreteShape.fillRoundedRectangle (C4b(0uy,0uy,0uy,bgAlpha)) 0.8 bounds
            ShapeList.prepend rect shapes

        let progress (v : float) (mv : float) (box : Box2d) =
            let q = clamp 0.03 1.0 v
            let mq = clamp 0.03 1.0 mv
            let w = 0.1
            let offset = box.Min
            let size =  V2d(box.Size.X, min 0.8 box.Size.Y)
            let bgBounds = Box2d.FromMinAndSize(offset, size).ShrunkBy(w / 2.0)
            let maxBounds = Box2d.FromMinAndSize(bgBounds.Min, V2d(bgBounds.SizeX * mq, bgBounds.SizeY)).ShrunkBy(w / 2.0)
            let curBounds = Box2d.FromMinAndSize(bgBounds.Min, V2d(bgBounds.SizeX * q, bgBounds.SizeY)).ShrunkBy(w / 2.0)
            let mutable c = HeatMap.color(1.0 - q)

            let max = if mv > 0.0 then mv else 1.0

            if q < max then c.A <- 200uy
            else c.A <- 150uy

            [
                yield ConcreteShape.roundedRectangle (C4b(255uy,255uy,255uy,255uy)) w 0.8 bgBounds
                if mv > 0.0 then yield ConcreteShape.fillRoundedRectangle (C4b(255uy,255uy,255uy,150uy)) 0.8 maxBounds
                yield ConcreteShape.fillRoundedRectangle c 0.8 curBounds
            ]
            
        let inline progress' (v : ^a) (max : ^a) =
            progress (v / max) -1.0

        let shapes =
            adaptive {
                let! a = active
                if a then
                    let! text = text
                    let! stats = stats
                    return 
                        text
                        |> layoutWithBackground 1.0
                        |> ShapeList.replaceString pi (progress stats.quality stats.maxQuality)
                        |> ShapeList.replaceString pm (progress' stats.usedMemory stats.allocatedMemory)
                        |> ShapeList.replaceString pt (progress' (float stats.totalPrimitives) (float -1))
                else
                    return layoutWithBackground 0.5 "press 'H' for help"
            }

        win.Keyboard.DownWithRepeats.Values.Add (fun k ->
            if k = Keys.H then
                transact (fun () -> active.Value <- not active.Value)
            else
                ()
        )

        Sg.shape shapes
            |> Sg.trafo trafo
            |> Sg.blendMode (AVal.constant BlendMode.Blend)
            |> Sg.viewTrafo (AVal.constant Trafo3d.Identity)
            |> Sg.projTrafo (AVal.constant Trafo3d.Identity)
