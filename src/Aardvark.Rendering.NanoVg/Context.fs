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

    let private fontNames = Dictionary<Font, string>()

    module private FontResolver =

        module private Windows =
            open System.IO
            open Microsoft.Win32
            let fontFolder = Environment.GetFolderPath Environment.SpecialFolder.Fonts

            let getSystemFontFileName (name : string) (style : FontStyle) =
                use font = new System.Drawing.Font(name, 10.0f, toSysStyle style)
                let fontname = font.Name + " (TrueType)"
                let fonts = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\Fonts", false)
                match fonts with
                    | null -> failwith "can't find font registry database."
                    | _ ->
                        let o = fonts.GetValue(fontname)  
                        match o with
                            | :? string as path -> Some (Path.Combine(fontFolder, path))
                            | _ -> None

        module private Linux =
            open System.Diagnostics
            open System.Text.RegularExpressions

            let lineRx = Regex @"(?<path>.*?)[ \t]*:[ \t]*(?<name>.*)[ \t]*:[ \t]*style=(?<style>[a-zA-Z_]*)"
            let parseLine (str : string) =
                let m = lineRx.Match str 
                if m.Success then
                    let path = m.Groups.["path"].Value
                    let name = m.Groups.["name"].Value
                    let style = m.Groups.["style"].Value.ToLower()
                    Some(name, style, path)
                else
                    None
                ///usr/share/fonts/type1/gsfonts/n019003l.pfb: Nimbus Sans L:style=Regular

            let allNames =
                let si = ProcessStartInfo("/usr/bin/fc-list")
                si.RedirectStandardOutput <- true
                si.UseShellExecute <- false
                let proc = Process.Start(si)
                proc.WaitForExit()

                let lines = proc.StandardOutput.ReadToEnd().Split('\n')
                let all = 
                    lines 
                        |> Array.choose parseLine
                        |> Array.choose (fun (n,s,p) ->
                            if p.EndsWith ".ttf" then
                                match s with
                                    | "regular"     -> Some ((n,FontStyle.Regular), p)
                                    | "bold"        -> Some ((n,FontStyle.Bold), p)
                                    | "italic"      -> Some ((n,FontStyle.Italic), p)
                                    | _ -> None
                            else
                                None
                           )
                let res = Dict()
                for (k,v) in all do
                    res.[k] <- v
                res

            let getSystemFontFileName (name : string) (style : FontStyle) =
                match allNames.TryGetValue ((name, style)) with
                    | (true, p) -> Some p
                    | _ -> None

        module private MacOS =
            let getSystemFontFileName (name : string) (style : FontStyle) =
                Log.warn "font retrieval not implemented on MacOS X"
                None

        let getSystemFontFileName (name : string) (style : FontStyle) =
            match Environment.OSVersion.Platform with
                | PlatformID.Unix -> Linux.getSystemFontFileName name style
                | PlatformID.MacOSX -> MacOS.getSystemFontFileName name style
                | _ -> Windows.getSystemFontFileName name style



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

    let rec getFontName (font : Font) =
        match fontNames.TryGetValue font with
            | (true, name) -> name
            | _ ->
                let name = 
                    match font with
                        | FileFont path ->
                            let n = Guid.NewGuid().ToString()
                            let ctx = current()
                            let fontId = NanoVg.nvgCreateFont(ctx, n, path)
                            n
                        | SystemFont(name, style) ->
                            match FontResolver.getSystemFontFileName name style with
                                | Some path -> getFontName (FileFont path)
                                | _ -> failwithf "cannot locate system font: %s with style %A" name style

                fontNames.[font] <- name
                name
