namespace Aardvark.Rendering.NanoVg

open System
open System.IO
open System.Collections.Generic
open NanoVgSharp
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Rendering.GL

module Context =
    let mutable private context = 0n
    let styles = [FontStyle.Regular; FontStyle.Bold; FontStyle.Italic]

    let private toSysStyle (s : FontStyle) =
        match s with
            | FontStyle.Regular -> System.Drawing.FontStyle.Regular
            | FontStyle.Bold -> System.Drawing.FontStyle.Bold
            | FontStyle.Italic -> System.Drawing.FontStyle.Italic
            | _ -> failwithf "unknown font style: %A" s

    let fontPathTable =
        let folder = Environment.GetFolderPath(Environment.SpecialFolder.Fonts)

        let res = Dictionary()
        for f in Directory.EnumerateFiles(folder, "*.ttf") do
            use coll = new System.Drawing.Text.PrivateFontCollection()
            coll.AddFontFile(f)
            let fam = coll.Families.[0]

            let s = styles |> List.filter (toSysStyle >> fam.IsStyleAvailable)
            match s with
                | style::_ -> res.[(fam.Name, style)] <- f
                | _ -> ()
            ()
        res


    let private fontNames = Dictionary<string, string>()

    let init(ctx : Context) =
        if context = 0n then
            NanoVgGl.glewInit()
            using ctx.ResourceLock (fun _ ->
                context <- NanoVgGl.nvgCreateGL3 NvgCreateFlags.Antialias
            )

    let current() =
        if context <> 0n then 
            context
        else
            match ContextHandle.Current with
                | Some h -> 
                    NanoVgGl.glewInit()
                    context <- NanoVgGl.nvgCreateGL3 NvgCreateFlags.Antialias
                    context
                | None ->
                    failwith "cannot initialize NanoVg without a GL context"

    let rec getFontName (font : Aardvark.Rendering.NanoVg.Font) =
        match font with
            | FileFont path ->
                match fontNames.TryGetValue path with
                    | (true, n) -> n
                    | _ ->
                        let n = Guid.NewGuid().ToString()
                        let ctx = current()
                        NanoVg.nvgCreateFont(ctx, n, path) |> ignore
                        fontNames.[path] <- n
                        n
            | SystemFont(name, style) ->
                match fontPathTable.TryGetValue((name, style)) with
                    | (true, path) -> getFontName (FileFont path)
                    | _ -> failwithf "cannot locate system font: %s with style %A" name style
