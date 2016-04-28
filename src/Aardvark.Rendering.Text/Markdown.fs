namespace Aardvark.Rendering.Text


open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open CommonMark
open CommonMark.Syntax
    

module Markdown =
    open Aardvark.Base.Monads
    open Aardvark.Base.Monads.State

    type TextState =
        {
            strong : bool
            emph : bool
            code : bool
        }

    module Patterns = 
        let inline private startFrom< ^a when ^a : (member NextSibling : ^a) and ^a : null > (b : ^a) =
            [
                let mutable c = b
                while not (isNull c) do
                    yield c
                    c <- (^a : (member NextSibling : ^a) (c))
            ]

        let rec private getAll (state : TextState) (i : Inline) =
            [
                if isNull i.FirstChild then 
                    if i.Tag = InlineTag.LineBreak || i.Tag = InlineTag.SoftBreak then
                        yield (state, "\n")
                    else
                        yield (state, i.LiteralContent)
                else 
                    let state =
                        match i.Tag with
                            | InlineTag.Strong -> { state with strong = true }
                            | InlineTag.Emphasis -> { state with emph = true }
                            | InlineTag.Code -> { state with code = true }
                            | _ -> state

                    
                    for c in startFrom i.FirstChild do
                        yield! getAll state  c

            ]

        let private emptyState = { strong = false; emph = false; code = false }

        /// The root element that represents the document itself. There should only be one in the tree.
        let (|Document|_|) (b : Block) =
            if b.Tag = BlockTag.Document then
                Some(startFrom b.FirstChild)
            else
                None

        /// A paragraph block element.
        let (|Paragraph|_|) (b : Block) =
            if b.Tag = BlockTag.Paragraph then
                b.InlineContent 
                    |> startFrom 
                    |> List.collect (getAll emptyState) 
                    |> Some
            else
                None

        /// A heading element
        let (|Heading|_|) (b : Block) =
            if b.Tag = BlockTag.AtxHeading || b.Tag = BlockTag.SetextHeading then
                let content = 
                    b.InlineContent 
                        |> startFrom 
                        |> List.collect (getAll emptyState) 
                Some(int b.Heading.Level, content)
            else
                None

        /// A list element. Will contain nested blocks with type of ListItem
        let (|List|_|) (b : Block) =
            if b.Tag = BlockTag.List then
                let listType = b.ListData.ListType

                let children = 
                    b.FirstChild |> startFrom |> List.map (fun c -> c.FirstChild |> startFrom)
    
                Some(listType, children)
            else
                None

        /// A block-quote element.
        let (|BlockQuote|_|) (b : Block) =
            if b.Tag = BlockTag.BlockQuote then
                b.InlineContent 
                    |> startFrom 
                    |> List.collect (getAll emptyState) 
                    |> Some
            else
                None

        /// A thematic break element.
        let (|HorizontalRuler|_|) (b : Block) =
            if b.Tag = BlockTag.ThematicBreak then Some ()
            else None

        /// A code block element that was formatted with fences (for example, <c>~~~\nfoo\n~~~</c>).
        let (|FencedCode|_|) (b : Block) =
            if b.Tag = BlockTag.FencedCode then
                b.InlineContent 
                    |> startFrom 
                    |> List.collect (getAll { emptyState with code = true }) 
                    |> Some
            else 
                None

        /// A code block element that was formatted by indenting the lines with at least 4 spaces.
        let (|IndentedCode|_|) (b : Block) =
            if b.Tag = BlockTag.IndentedCode then
                b.InlineContent 
                    |> startFrom 
                    |> List.collect (getAll { emptyState with code = true }) 
                    |> Some
            else 
                None

    module Layouter = 
        open Patterns


        type LayoutState = 
            {
                x           : float
                y           : float
                scale       : float
                color       : C4b

                glyphs      : list<Glyph>
                offsets     : list<V2d>
                scales      : list<V2d>
                colors      : list<C4b>

            }

        let moveX (v : float) =
            modifyState (fun s -> { s with x = s.x + v * s.scale })

        let moveY (v : float) =
            modifyState (fun s -> { s with x = s.y + v * s.scale })
    
        let lineBreak =
            state {
                let! s = getState
                do! putState { s with x = 0.0; y = s.y - s.scale}
            }

        let setX (x : float) =
            modifyState (fun s -> { s with x = x })

        let setScale (scale : float) =
            state {
                let! s = getState
                do! putState { s with scale = scale }
                return s.scale
            }


        let pos = 
            state {
                let! s = getState
                return V2d(s.x, s.y)
            }

        let emit (g : Glyph) =
            modifyState (fun (s : LayoutState) ->
                { s with
                    glyphs = g::s.glyphs
                    offsets = V2d(s.x, s.y)::s.offsets
                    scales = V2d(s.scale, s.scale)::s.scales
                    colors = s.color::s.colors
                }
            )


        let private regular         = new Font("Times New Roman", FontStyle.Regular)
        let private bold            = new Font("Times New Roman", FontStyle.Bold)
        let private italic          = new Font("Times New Roman", FontStyle.Italic)
        let private boldItalic      = new Font("Times New Roman", FontStyle.Italic ||| FontStyle.Bold)

        let private getFont (s : TextState) =
            match s.strong, s.emph with
                | false, false  -> regular
                | true, false   -> bold
                | false, true   -> italic
                | true, true    -> boldItalic

        let layout (str : string) =
            let ast = CommonMarkConverter.Parse(str)

            let layoutParts (parts : list<TextState * string>) =
                state {
                    for (props, text) in parts do
                        if not (isNull text) then
                            let font = getFont props

                            let mutable last = '\n'
                            for c in text do
                                match c with
                                    | ' ' -> do! moveX 0.5
                                    | '\t' -> do! moveX 2.0
                                    | '\n' -> do! lineBreak
                                    | '\r' -> ()
                                    | _ ->
                                        let! s = getState
                                        let g = font.GetGlyph c
                                        let kerning = font.GetKerning(last, c)
                                        let before = g.Before + kerning
                                        let after = g.Advance - before

                                        do! moveX before
                                        do! emit g
                                        do! moveX after
                                last <- c

                        else
                            Log.warn "bad text: %A" text
                }

            let scales =
                Map.ofList [
                    1, 3.0
                    2, 2.0
                    3, 1.5
                    4, 1.2
                ]

            let rec layout (b : Block) =
                state {
                    match b with
                        | Document children ->
                            for c in children do
                                do! layout c

                        | Paragraph parts ->
                            do! layoutParts parts
                            do! lineBreak

                        | Heading(level, parts) ->  
                            let scale = scales.[level]
                            let! old = setScale scale

                            do! layoutParts parts
                            do! lineBreak

                            let! _ = setScale old
                            ()

                        | _ ->
                            return failwithf "unknown block: %A" b.Tag
            
                }

            let run = layout ast

            let ((), s) = 
                run.runState {
                    x           = 0.0
                    y           = 0.0
                    scale       = 1.0
                    color       = C4b.White
                         
                    glyphs      = []
                    offsets     = []
                    scales      = []
                    colors      = []
                }

            {
                RenderText.glyphs   = List.toArray s.glyphs
                RenderText.offsets  = List.toArray s.offsets
                RenderText.scales   = List.toArray s.scales
                RenderText.colors   = List.toArray s.colors
            }


    let layout (code : string) =
        Layouter.layout code

[<AutoOpen>]
module ``Markdown Sg Extensions`` =
    module Sg =
        let markdown (code : IMod<string>) =
            code
                |> Mod.map Markdown.layout
                |> Sg.text