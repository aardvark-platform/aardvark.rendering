namespace Aardvark.Rendering.Text



open CommonMark
open CommonMark.Syntax
    

module Markdown =

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

        let (|Document|_|) (b : Block) =
            if b.Tag = BlockTag.Document then
                Some(startFrom b.FirstChild)
            else
                None

        let (|Paragraph|_|) (b : Block) =
            if b.Tag = BlockTag.Paragraph then
                b.InlineContent 
                    |> startFrom 
                    |> List.collect (getAll emptyState) 
                    |> Some
            else
                None

        let (|Heading|_|) (b : Block) =
            if b.Tag = BlockTag.AtxHeading || b.Tag = BlockTag.SetextHeading then
                b.InlineContent 
                    |> startFrom 
                    |> List.collect (getAll emptyState) 
                    |> Some
            else
                None

        let (|List|_|) (b : Block) =
            if b.Tag = BlockTag.List then
                let listType = b.ListData.ListType

                let children = 
                    b.FirstChild |> startFrom
    
                Some(listType, children)
            else
                None

        let (|BlockQuote|_|) (b : Block) =
            if b.Tag = BlockTag.BlockQuote then
                b.InlineContent 
                    |> startFrom 
                    |> List.collect (getAll emptyState) 
                    |> Some
            else
                None

        let (|HorizontalRuler|_|) (b : Block) =
            if b.Tag = BlockTag.ThematicBreak then Some ()
            else None

        let (|FencedCode|_|) (b : Block) =
            if b.Tag = BlockTag.FencedCode then
                b.InlineContent 
                    |> startFrom 
                    |> List.collect (getAll { emptyState with code = true }) 
                    |> Some
            else 
                None

        let (|IndentedCode|_|) (b : Block) =
            if b.Tag = BlockTag.IndentedCode then
                b.InlineContent 
                    |> startFrom 
                    |> List.collect (getAll { emptyState with code = true }) 
                    |> Some
            else 
                None

    open Patterns
    let parse (str : string) =
        let block = CommonMarkConverter.Parse(str)
       
        match block with
            | Document content -> 
                for c in content do
                    match c with
                        | Paragraph p -> printfn "par %A" p
                        | Heading p -> printfn "heading %A" p
                        | List p -> printfn "list %A" p
                        | BlockQuote p -> printfn "quote %A" p
                        | _ -> printfn "unknown %A" c.Tag
            | _ -> ()



