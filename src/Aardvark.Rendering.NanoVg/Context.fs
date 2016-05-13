namespace Aardvark.Rendering.NanoVg

open System
open System.IO
open System.Collections.Generic
open NanoVgSharp
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Rendering.GL
open System.Threading

module Context =
    
    let private styles = [FontStyle.Regular; FontStyle.Bold; FontStyle.Italic]


    let private toSysStyle (s : FontStyle) =
        match s with
            | FontStyle.Regular -> System.Drawing.FontStyle.Regular
            | FontStyle.Bold -> System.Drawing.FontStyle.Bold
            | FontStyle.Italic -> System.Drawing.FontStyle.Italic
            | _ -> failwithf "unknown font style: %A" s

    module private FontResolver =

        module Windows =
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

        module Linux =
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
                lazy (
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
                )

            let getSystemFontFileName (name : string) (style : FontStyle) =
                match allNames.Value.TryGetValue ((name, style)) with
                    | (true, p) -> Some p
                    | _ -> 
                        match allNames.Value.TryGetValue(("Ubuntu Mono", FontStyle.Regular)) with
                            | (true, p) -> Some p
                            | _ -> None

        module MacOS =
            let getSystemFontFileName (name : string) (style : FontStyle) =
                Log.warn "font retrieval not implemented on MacOS X"
                None

        let private fontPaths = Dictionary<Font, string>()

        let private getSystemFontFileName (name : string) (style : FontStyle) =
            match Environment.OSVersion.Platform with
                | PlatformID.Unix -> Linux.getSystemFontFileName name style
                | PlatformID.MacOSX -> MacOS.getSystemFontFileName name style
                | _ -> Windows.getSystemFontFileName name style

        let rec getFontNameAndPath (font : Font) =
            match fontPaths.TryGetValue font with
                | (true, path) -> 
                    path
                | _ ->
                    let path = 
                        match font with
                            | FileFont path ->
                                path
                            | SystemFont(name, style) ->
                                match getSystemFontFileName name style with
                                    | Some path -> 
                                        getFontNameAndPath (FileFont path)
                                    | _ -> failwithf "cannot locate system font: %s with style %A" name style

                    fontPaths.[font] <- path
                    path

    [<AllowNullLiteral>]
    type NanoVgContextHandle(handle : nativeint) =
        let loadedFonts = Dictionary<string, int>()


        member x.Handle = handle

        member x.GetFontId(font : Font) =
            let path = FontResolver.getFontNameAndPath(font)
            match loadedFonts.TryGetValue path with
                | (true, id) -> id
                | _ ->
                    let name = Guid.NewGuid().ToString()
                    let id = NanoVg.nvgCreateFont(handle, name, path)
                    loadedFonts.[path] <- id
                    id

    let private contextMap = ConcurrentDict<obj, NanoVgContextHandle>(Dict())
    let mutable private glew = false
//
//    let current() =
//        match ContextHandle.Current with
//            | Some h -> 
//                contextMap.GetOrCreate(h.Handle, fun h ->
//                    if not glew then
//                        glew <- true
//                        NanoVgGl.glewInit()
//                    let handle = NanoVgGl.nvgCreateGL3 NvgCreateFlags.Antialias
//
//                    NanoVgContextHandle(handle)
//                            
//                )
//            | None ->
//                failwith "cannot initialize NanoVg without a GL context"

    type NanoVgContext(context : Aardvark.Rendering.GL.Context) =
        static let nopDisposable = { new IDisposable with member x.Dispose() = () }
        let contextTable = ConcurrentDict<ContextHandle, NanoVgContextHandle>(Dict())
        
        let getOrCreate (gl : ContextHandle) =
            contextTable.GetOrCreate(gl, fun gl ->
                if not glew then
                    glew <- true
                    NanoVgGl.glewInit()
                let handle = NanoVgGl.nvgCreateGL3 NvgCreateFlags.Antialias
                NanoVgContextHandle(handle)
            )

        let mutable current = new ThreadLocal<Option<NanoVgContextHandle>>(fun () -> None)


        member x.Current = current.Value

        member x.ResourceLock =
            match current.Value with
                | Some h -> nopDisposable
                | None ->
                    match ContextHandle.Current with
                        | None ->
                            let glLock = context.ResourceLock
                            let glContext = ContextHandle.Current.Value
                            current.Value <- Some (getOrCreate glContext)
                            { new IDisposable with
                                member x.Dispose() = 
                                    current.Value <- None
                                    glLock.Dispose()
                            }
                        | Some glContext ->
                            current.Value <- Some (getOrCreate glContext)
                            { new IDisposable with
                                member x.Dispose() = 
                                    current.Value <- None
                            }

        member x.Use (f : NanoVgContextHandle -> 'a) =
            using x.ResourceLock (fun _ ->
                let current = current.Value.Value
                f current
            )
        
