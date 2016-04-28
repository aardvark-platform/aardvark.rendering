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
            scale   : V2d
            strong  : bool
            emph    : bool
            code    : bool
        }

        static member empty = { scale = V2d.II; strong = false; emph = false; code = false }

    type LayoutState = 
        {
            x           : float
            y           : float
            indent      : float
            textState   : TextState
            color       : C4b

            shapes      : list<Shape>
            offsets     : list<V2d>
            scales      : list<V2d>
            colors      : list<C4b>

        }

        static member empty = { x = 0.0; y = 0.0; indent = 0.0; textState = TextState.empty; color = C4b.White; shapes = []; offsets = []; scales = []; colors = [] }


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
                    |> List.collect (getAll TextState.empty) 
                    |> Some
            else
                None

        /// A heading element
        let (|Heading|_|) (b : Block) =
            if b.Tag = BlockTag.AtxHeading || b.Tag = BlockTag.SetextHeading then
                let content = 
                    b.InlineContent 
                        |> startFrom 
                        |> List.collect (getAll TextState.empty) 
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
                    |> List.collect (getAll TextState.empty) 
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
                b.StringContent.ToString() |> Some
            else 
                None

        /// A code block element that was formatted by indenting the lines with at least 4 spaces.
        let (|IndentedCode|_|) (b : Block) =
            if b.Tag = BlockTag.IndentedCode then
                b.StringContent.ToString() |> Some
            else 
                None

    module Layouter = 
        open Patterns


        [<AutoOpen>]
        module StateHelpers = 
            let moveX (v : float) =
                modifyState (fun s -> { s with x = s.x + v * s.textState.scale.X })

            let moveY (v : float) =
                modifyState (fun s -> { s with y = s.y - v * s.textState.scale.Y })

            let indent (v : float) =
                modifyState (fun s -> 
                    let nx = s.indent + v * s.textState.scale.X
                    { s with indent = nx; x = nx }
                )

            let pushColor (c : C4b) =
                state {
                    let! s = getState
                    do! putState { s with color = c}
                    return s.color
                }
      
            let lineBreak =
                state {
                    let! s = getState
                    do! putState { s with x = s.indent; y = s.y - s.textState.scale.Y}
                }

            let withTextState (ts : TextState) (f : unit -> State<LayoutState, 'a>) =
                state {
                    let! s = getState
                    let old = s.textState

                    let nts = 
                        { 
                            strong      = old.strong || ts.strong
                            emph        = old.emph || ts.emph
                            code        = old.code || ts.code
                            scale       = old.scale * ts.scale
                        }

                    do! putState { s with textState = nts }
                    let! res = f()
                    do! modifyState (fun s -> { s with textState = old })
                    return res
                }

            

            let pos = 
                state {
                    let! s = getState
                    return V2d(s.x, s.y)
                }

            let emit (g : Shape) =
                modifyState (fun (s : LayoutState) ->
                    { s with
                        shapes = g::s.shapes
                        offsets = V2d(s.x, s.y)::s.offsets
                        scales = s.textState.scale::s.scales
                        colors = s.color::s.colors
                    }
                )


            module Paragraph = 
                let fontName        = "Times New Roman"
                let regular         = new Font(fontName, FontStyle.Regular)
                let bold            = new Font(fontName, FontStyle.Bold)
                let italic          = new Font(fontName, FontStyle.Italic)
                let boldItalic      = new Font(fontName, FontStyle.Italic ||| FontStyle.Bold)

            module Code = 
                let fontName        = "Consolas"
                let regular         = new Font(fontName, FontStyle.Regular)
                let bold            = new Font(fontName, FontStyle.Bold)
                let italic          = new Font(fontName, FontStyle.Italic)
                let boldItalic      = new Font(fontName, FontStyle.Italic ||| FontStyle.Bold)


            let getFont =
                state {
                    let! s = getState
                    let ts = s.textState
                    match ts.strong, ts.emph, ts.code with
                        | false,    false,      false   -> return Paragraph.regular
                        | false,    false,      true    -> return Code.regular
                        | false,    true,       false   -> return Paragraph.italic
                        | false,    true,       true    -> return Code.italic
                        | true,     false,      false   -> return Paragraph.bold
                        | true,     false,      true    -> return Code.bold
                        | true,     true,       false   -> return Paragraph.boldItalic
                        | true,     true,       true    -> return Code.boldItalic

                }

        let layout (str : string) =
            let ast = CommonMarkConverter.Parse(str)

            let layoutParts (parts : list<TextState * string>) =
                state {
                    for (props, text) in parts do
                        if not (isNull text) then
                            do! withTextState props (fun () ->
                                state {
                                    let! font = getFont
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

                                }
                            )

                        else
                            Log.warn "bad text: %A" text
                }

            let headingStyles =
                Map.ofList [
                    1, { scale = V2d.II * 3.0; emph = false; strong = true; code = false }
                    2, { scale = V2d.II * 2.5; emph = false; strong = true; code = false }
                    3, { scale = V2d.II * 2.0; emph = false; strong = true; code = false }
                    4, { scale = V2d.II * 1.5; emph = false; strong = true; code = false }
                    5, { scale = V2d.II * 1.2; emph = false; strong = true; code = false }
                    6, { scale = V2d.II * 1.1; emph = false; strong = true; code = false }
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
                            let style = headingStyles.[level]
                            do! withTextState style (fun () ->
                                state {
                                    do! layoutParts parts
                                    do! lineBreak
                                }
                            )

                        | IndentedCode content | FencedCode content ->
                            
                            do! moveY 1.0
                            do! indent 1.0
                            do! layoutParts [{ TextState.empty with code = true }, content]
                            do! indent -1.0
                            do! lineBreak
                            
                        | HorizontalRuler ->
                            let h = 0.05
                            let w = 20.0
                            do! moveY -(0.4 + h/2.0)
                            do! withTextState { TextState.empty with scale = V2d(w, h) } (fun () ->
                                state {
                                    do! emit Shape.Quad
                                }
                            )
                            do! moveY (0.4 + h/2.0)
                            do! lineBreak
                        
                        | List(kind, items) ->
                            do! moveY 0.4

                            let! font = getFont
                            let mutable index = 1
                            for b in items do
                                do! moveX 0.5
                                let prefix =
                                    match kind with
                                        | ListType.Bullet -> "•"
                                        | _ -> sprintf "%d." index

                                do! layoutParts [TextState.empty, prefix]
                                //do! moveX 0.6
                                let! p = pos
                                let nextMul4 = ceil (p.X / 1.5) * 1.5

                                do! indent nextMul4
                                for inner in b do do! layout inner
                                do! indent -nextMul4
                                index <- index + 1

                            do! moveY 0.4

                        | _ ->
                            return failwithf "unknown block: %A" b.Tag
            
                }

            let run = layout ast

            let ((), s) = 
                run.runState {
                    LayoutState.empty with
                        color       = C4b.White   
                }

            {
                ShapeList.shapes   = List.toArray s.shapes
                ShapeList.offsets  = List.toArray s.offsets
                ShapeList.scales   = List.toArray s.scales
                ShapeList.colors   = List.toArray s.colors
            }


    let layout (code : string) =
        Layouter.layout code

[<AutoOpen>]
module ``Markdown Sg Extensions`` =
    module Sg =
        let markdown (code : IMod<string>) =
            code
                |> Mod.map Markdown.layout
                |> Sg.shape