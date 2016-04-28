namespace Aardvark.Rendering.Text


open System
open System.Linq
open System.Collections.Generic
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Base.Rendering


[<ReferenceEquality; NoComparison>]
[<StructuredFormatDisplay("{AsString}")>]
type RenderText =
    {
        offsets     : V2d[]
        scales      : V2d[]
        colors      : C4b[]
        glyphs      : Glyph[]
    }

    override x.ToString() =
        x.glyphs |> Array.map Glyph.char |> String
        
    member private x.AsString = x.ToString()


type TextAlignment =
    | Left = 0
    | Right = 1
    | Center = 2

[<AbstractClass; Sealed; Extension>]
type Text private() =

    static let lineBreak = System.Text.RegularExpressions.Regex @"\r?\n|\r"

    [<Extension>]
    static member Layout(font : Font, color : C4b, align : TextAlignment, bounds : Box2d, content : string) =
        let chars = List<float * Glyph>()
        let offsets = List<V2d>()
        let scales = List<V2d>()
        let colors = List<C4b>()
        let glyphs = List<Glyph>()

        let mutable cy = 0.0
        let allLines = lineBreak.Split content

        for l in allLines do
            let mutable cx = 0.0
            let mutable last = '\n'
            chars.Clear()

            for c in l do
                let kerning = font.GetKerning(last, c)
               
                match c with
                    | ' ' -> cx <- cx + font.Spacing
                    | '\t' -> cx <- cx + 4.0 + font.Spacing
                    | c ->
                        let g = font |> Font.glyph c
                        chars.Add(cx + g.Before + kerning, g)
                        cx <- cx + g.Advance

                last <- c

            let shift = 
                match align with
                    | TextAlignment.Left -> 0.0
                    | TextAlignment.Right -> bounds.SizeX - cx
                    | TextAlignment.Center -> (bounds.SizeX - cx) / 2.0
                    | _ -> failwithf "[Text] bad align: %A" align

            let y = cy

            for (x,g) in chars do
                let pos = bounds.Min + V2d(shift + x,y)
                offsets.Add(pos)
                scales.Add(V2d(1.0, 1.0))
                colors.Add(color)
                glyphs.Add(g)

            cy <- cy - font.LineHeight

        {
            offsets     = offsets.ToArray()
            scales      = scales.ToArray()
            colors      = colors.ToArray()
            glyphs      = glyphs.ToArray()
        }

    [<Extension>]
    static member Layout(font : Font, align : TextAlignment, bounds : Box2d, content : string) =
        Text.Layout(font, C4b.Black, align, bounds, content)

    [<Extension>]
    static member Layout(font : Font, content : string) =
        Text.Layout(font, C4b.Black, TextAlignment.Left, Box2d(0.0, 0.0, Double.MaxValue, Double.MaxValue), content)

    [<Extension>]
    static member Layout(font : Font, color : C4b, content : string) =
        Text.Layout(font, color, TextAlignment.Left, Box2d(0.0, 0.0, Double.MaxValue, Double.MaxValue), content)



