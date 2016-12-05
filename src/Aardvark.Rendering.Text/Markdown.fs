namespace Aardvark.Rendering.Text


open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open CommonMark
open CommonMark.Syntax
    
type TextStyle =
    {
        scale   : V2d
        strong  : bool
        emph    : bool
        code    : bool
    }

type MarkdownConfig =
    {

        color               : C4b
        lineSpacing         : float
        characterSpacing    : float
        headingStyles       : Map<int, TextStyle>
        
        paragraphFont       : string
        codeFont            : string
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module TextStyle =
    
    let empty = { scale = V2d.II; strong = false; emph = false; code = false }


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module MarkdownConfig =
    
    let light =
        {
            color                   = C4b(51uy, 51uy, 51uy, 255uy)
            lineSpacing             = 1.5
            characterSpacing        = 1.0
            paragraphFont           = "Arial"
            codeFont                = "Consolas"

            headingStyles =
                Map.ofList [
                    1, { TextStyle.empty with scale = V2d.II * 3.0; strong = true }
                    2, { TextStyle.empty with scale = V2d.II * 2.5; strong = true }
                    3, { TextStyle.empty with scale = V2d.II * 2.0; strong = true }
                    4, { TextStyle.empty with scale = V2d.II * 1.5; strong = true }
                    5, { TextStyle.empty with scale = V2d.II * 1.2; strong = true }
                    6, { TextStyle.empty with scale = V2d.II * 1.1; strong = true }
                ]  

        }

    let dark =
        { light with
            color = C4b(204uy, 204uy, 204uy, 255uy)
        }


module Markdown =
    open Aardvark.Base.Monads
    open Aardvark.Base.Monads.StateOld

    type FontCollection =
        {
            regular     : Font
            bold        : Font
            italic      : Font
            boldItalic  : Font
        }

    let private fontCollectionCache = System.Collections.Concurrent.ConcurrentDictionary<string, FontCollection>()

    let getFontCollection(name : string) =
        fontCollectionCache.GetOrAdd(name, fun name ->
            {
                regular     = Font.create name FontStyle.Regular
                bold        = Font.create name FontStyle.Bold
                italic      = Font.create name FontStyle.Italic
                boldItalic  = Font.create name FontStyle.BoldItalic
            }
        )


    type LayoutState = 
        {
            paragraph   : FontCollection
            code        : FontCollection

            x           : float
            y           : float
            indent      : float
            textState   : TextStyle
            config      : MarkdownConfig
            color       : C4b
            max         : V2d
            min         : V2d

            shapes      : list<Shape>
            offsets     : list<V2d>
            scales      : list<float -> V2d>
            colors      : list<C4b>

        }

        static member empty = 
            { 
                paragraph = getFontCollection "Arial"
                code      = getFontCollection "Consolas"

                x = 0.0
                y = 0.0
                indent = 0.0
                textState = TextStyle.empty
                config = MarkdownConfig.light
                color = C4b.White
                min = V2d(0.0, 0.0)
                max = V2d(0.0, 0.0)

                shapes = []
                offsets = []
                scales = []
                colors = [] 
            }

    module Patterns = 
        let inline private startFrom< ^a when ^a : (member NextSibling : ^a) and ^a : null > (b : ^a) =
            [
                let mutable c = b
                while not (isNull c) do
                    yield c
                    c <- (^a : (member NextSibling : ^a) (c))
            ]

        let rec private getAll (state : TextStyle) (i : Inline) =
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
                    |> List.collect (getAll TextStyle.empty) 
                    |> Some
            else
                None

        /// A heading element
        let (|Heading|_|) (b : Block) =
            if b.Tag = BlockTag.AtxHeading || b.Tag = BlockTag.SetextHeading then
                let content = 
                    b.InlineContent 
                        |> startFrom 
                        |> List.collect (getAll TextStyle.empty) 
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
                    |> List.collect (getAll TextStyle.empty) 
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
                modifyState (fun s -> 
                    let nx = s.x + v * s.textState.scale.X * s.config.characterSpacing
                    { s with x = nx; max = V2d(max nx s.max.X, s.max.Y); }
                )

            let moveY (v : float) =
                modifyState (fun s -> 
                    let ny = s.y - v * s.textState.scale.Y * s.config.lineSpacing
                    { s with y = ny; min = V2d(s.min.X, min ny s.min.Y) })

            let indent (v : float) =
                modifyState (fun s -> 
                    let nx = s.indent + v * s.textState.scale.X * s.config.characterSpacing
                    { s with indent = nx; x = nx; max = V2d(max nx s.max.X, s.max.Y) }
                )

            let pushColor (c : C4b) =
                state {
                    let! s = getState
                    do! putState { s with LayoutState.color = c}
                    return s.color
                }
      
            let lineBreak =
                state {
                    let! s = getState
                    let ny = s.y - s.textState.scale.Y * s.config.lineSpacing
                    do! putState { s with x = s.indent; y = ny; min = V2d(s.min.X, min ny s.min.Y) }
                }

            let withTextState (ts : TextStyle) (f : unit -> State<LayoutState, 'a>) =
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
                        max = V2d(s.max.X, max (s.y + s.textState.scale.Y) s.max.Y)
                        shapes = g::s.shapes
                        offsets = V2d(s.x, s.y)::s.offsets
                        scales = (fun _ -> s.textState.scale)::s.scales
                        colors = s.color::s.colors
                    }
                )

            let emitFullWidth (g : Shape) =
                modifyState (fun (s : LayoutState) ->
                    { s with
                        max = V2d(s.max.X, max (s.y + s.textState.scale.Y) s.max.Y)
                        shapes = g::s.shapes
                        offsets = V2d(s.x, s.y)::s.offsets
                        scales = (fun w -> V2d(w, s.textState.scale.Y))::s.scales
                        colors = s.color::s.colors
                    }
                )

            let getFont =
                state {
                    let! s = getState
                    let ts = s.textState
                    match ts.strong, ts.emph, ts.code with
                        | false,    false,      false   -> return s.paragraph.bold
                        | false,    false,      true    -> return s.code.regular
                        | false,    true,       false   -> return s.paragraph.italic
                        | false,    true,       true    -> return s.code.italic
                        | true,     false,      false   -> return s.paragraph.bold
                        | true,     false,      true    -> return s.code.bold
                        | true,     true,       false   -> return s.paragraph.boldItalic
                        | true,     true,       true    -> return s.code.boldItalic

                }

        let layout (config : MarkdownConfig) (str : string) =
            let ast = CommonMarkConverter.Parse(str)

            let layoutParts (parts : list<TextStyle * string>) =
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


            let rec layout (b : Block) =
                state {
                    let! s = getState
                    let config = s.config

                    match b with
                        | Document children ->
                            for c in children do
                                do! layout c

                        | Paragraph parts ->
                            do! layoutParts parts
                            do! lineBreak

                        | Heading(level, parts) ->  
                            let style = config.headingStyles.[level]
                            do! withTextState style (fun () ->
                                state {
                                    do! layoutParts parts
                                    do! lineBreak
                                }
                            )

                        | IndentedCode content | FencedCode content ->
                            
                            do! moveY 1.0
                            do! indent 1.0
                            do! layoutParts [{ TextStyle.empty with code = true }, content]
                            do! indent -1.0
                            do! lineBreak
                            
                        | HorizontalRuler ->
                            let h = 0.05
                            do! moveY -(0.4 + h/2.0)
                            do! withTextState { TextStyle.empty with scale = V2d(1.0, h) } (fun () ->
                                state {
                                    do! emitFullWidth Shape.Quad
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

                                do! layoutParts [TextStyle.empty, prefix]
                                //do! moveX 0.6
                                let! p = pos
                                let nextMul4 = ceil (p.X / 1.7) * 1.7

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
                        paragraph   = getFontCollection config.paragraphFont
                        code        = getFontCollection config.codeFont
                        color       = config.color 
                        config      = config
                }

            let bounds = Box2d.FromPoints(s.min, s.max)

            {
                ShapeList.bounds   = bounds
                ShapeList.shapes   = s.shapes
                ShapeList.offsets  = s.offsets
                ShapeList.scales   = s.scales |> List.map (fun f -> f bounds.SizeX)
                ShapeList.colors   = s.colors
            }


    let layout (config : MarkdownConfig) (code : string) =
        Layouter.layout config code

[<AutoOpen>]
module ``Markdown Sg Extensions`` =
    module Sg =
        let markdown (config : MarkdownConfig) (code : IMod<string>) =
            code
                |> Mod.map (Markdown.layout config)
                |> Sg.shapeWithBackground (C4b(255uy, 255uy, 255uy, 200uy))