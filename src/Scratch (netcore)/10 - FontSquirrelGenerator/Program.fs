open Aardvark.Base


module FontSquirrelGenerator =

    open System
    open System.IO
    open System.Net
    open System.Net.Http
    open System.Text.Json
    open System.IO.Compression

    let private client = new WebClient()
    do client.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/79.0.3945.130 Safari/537.36")

    let private md5 = System.Security.Cryptography.MD5.Create()

    let private cachePath = 
        let p = Path.GetTempPath()
        let d = Path.Combine(p, "fontsquirrel")
        if not (Directory.Exists d) then Directory.CreateDirectory d |> ignore
        d
        
    let private getCachedBytes (url : string) =
        let cacheName = md5.ComputeHash(System.Text.Encoding.Unicode.GetBytes url) |> Guid |> string
        let p = Path.Combine(cachePath, cacheName)
        if File.Exists p then 
            File.readAllBytes p
        else
            let c = client.DownloadData(url)
            File.WriteAllBytes(p, c)
            c

    let private getCached (url : string) =
        let cacheName = md5.ComputeHash(System.Text.Encoding.Unicode.GetBytes url) |> Guid |> string
        let p = Path.Combine(cachePath, cacheName)
        if File.Exists p then 
            File.ReadAllText p
        else
            let c = client.DownloadString(url)
            File.WriteAllText(p, c)
            c


    let get url = url |> Printf.kprintf getCached
    let getBytes url = url |> Printf.kprintf getCachedBytes

    type FontStyle =
        | Regular
        | Bold
        | Italic
        | Thin
        | Light
        | Medium
        | Black
        | Condensed
        | Extrabold
        | Semibold
        | Semiitalic
        | Extralight
        | Plain
        | Other of string

    module FontStyle =
        let private rx = System.Text.RegularExpressions.Regex @"(semi|extra)[ \t]+([^ \t]+)"
        let parse (str : string) =
            let str = rx.Replace(str.ToLower().Trim(), fun m -> m.Groups.[1].Value + m.Groups.[2].Value)
            
            let parts = str.Split([|' '|], StringSplitOptions.RemoveEmptyEntries)

            let style =
                parts |> Seq.collect (fun p ->
                    match p with
                    | "bold" | "grueso" | "gordita" | "heavy" -> Seq.singleton FontStyle.Bold
                    | "italic" | "oblique" | "inclinado" | "slanted" -> Seq.singleton FontStyle.Italic
                    | "thin" | "fino" | "fina" | "hairline" | "semilight" -> Seq.singleton FontStyle.Thin
                    | "light" | "lite" -> Seq.singleton FontStyle.Light
                    | "regular" | "normal" | "roman" -> Seq.singleton FontStyle.Regular
                    | "medium" -> Seq.singleton FontStyle.Medium
                    | "black" -> Seq.singleton FontStyle.Black
                    | "condensed" -> Seq.singleton FontStyle.Condensed
                    | "extrabold" -> Seq.singleton FontStyle.Extrabold
                    | "demibold" | "semibold" -> Seq.singleton FontStyle.Semibold
                    | "extralight" -> Seq.singleton FontStyle.Extralight
                    | "semiitalic" -> Seq.singleton FontStyle.Semiitalic
                    | "bolditalic" | "boldoblique" | "heavyitalic" -> [FontStyle.Bold; FontStyle.Italic] :> seq<_>
                    | "boldcondensed" -> [FontStyle.Bold; FontStyle.Condensed] :> seq<_>
                    | "regularitalic" -> [FontStyle.Regular; FontStyle.Italic] :> seq<_>
                    | "plain" -> Seq.singleton FontStyle.Plain
                    | o -> Seq.singleton (FontStyle.Other o)
                )
                |> Set.ofSeq

            style


    type Font =
        {
            id : string
            family : string
            urlName : string
            fileName : string
            style : Set<FontStyle>
            monospace : bool
            serif : option<bool>
            monocase : bool
        }

    let tryGetFont (f : Font) =
        let fileName = Path.Combine(cachePath, f.fileName)
        if File.Exists fileName then 
            Some fileName
        else
            let data = getBytes "http://www.fontsquirrel.com/fontfacekit/%s" f.urlName
            use s = new MemoryStream(data)
            use a = new ZipArchive(s)

            let real = f.fileName.ToLower()
            let ttf = Path.ChangeExtension(real, ".ttf")

            let entry = 
                a.Entries |> Seq.tryFind (fun e -> 
                    let name = e.Name.ToLower().Replace("-webfont", "")
                    name = real || name = ttf
                )

            match entry with
            | Some e -> 
                e.ExtractToFile(fileName, true)
                Some fileName
            | None ->
                None
                


    let run(outFile : string) =
        let json = 
            JsonDocument.Parse(
                get "https://www.fontsquirrel.com/api/fontlist/all"
            )

        let all = System.Collections.Generic.Dictionary<string, Map<Set<FontStyle>, Font>>()
        for entry in json.RootElement.EnumerateArray() do   
            try
                let urlName = entry.GetProperty("family_urlname").GetString()
                let id = entry.GetProperty("id").GetString()
                let infos = get "https://www.fontsquirrel.com/api/familyinfo/%s" urlName |> JsonDocument.Parse
                for info in infos.RootElement.EnumerateArray() do
                    let fileName = info.GetProperty("filename").GetString()
                    let style = info.GetProperty("style_name").GetString() |> FontStyle.parse
                    let fam = info.GetProperty("family_name").GetString()
                    let clazz = info.GetProperty("classification").GetString().ToLower()


                    let monospaced = clazz.Contains "monospaced" || clazz.Contains "programming"
                    let sans = clazz.Contains "sans serif"
                    let serif = not sans && clazz.Contains "serif"

                    let monocase = info.GetProperty("is_monocase").GetString().ToLower().Trim() = "y"
                    let font =
                        {
                            id = id
                            family = fam
                            urlName = urlName
                            fileName = fileName
                            style = style
                            monocase = monocase
                            monospace = monospaced
                            serif = if sans then Some false elif serif then Some true else None
                        }
                    match all.TryGetValue fam with
                    | (true, m) -> all.[fam] <- Map.add style font all.[fam]
                    | _ -> all.[fam] <- Map.ofList [style, font]
            with _ ->
                ()
            ()

        let b = System.Text.StringBuilder()
        let printfn fmt = fmt |> Printf.kprintf (fun s  -> Console.WriteLine("{0}", s); b.AppendLine s |> ignore)

        printfn "namespace Aardvark.Rendering.Text"
        printfn ""
        printfn "module FontSquirrel = "
        printfn "    open System.IO"
        printfn "    open System.IO.Compression"
        printfn "    open System.Net"
        printfn ""
        printfn "    let private client = lazy (new WebClient())"
        printfn "    let private getBytes fmt = fmt |> Printf.kprintf (fun str -> client.Value.DownloadData(str))"
        printfn ""
        printfn "    let private cachePath = "
        printfn "        let p = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData)"
        printfn "        let d = Path.Combine(p, \"fontsquirrel\")"
        printfn "        if not (Directory.Exists d) then Directory.CreateDirectory d |> ignore"
        printfn "        d"
        printfn ""
        printfn "    let private load (urlName : string) (fileName: string) : Font ="
        printfn "        let path = Path.Combine(cachePath, fileName)"
        printfn "        if File.Exists path then"
        printfn "            Font.Load(path)"
        printfn "        else"
        printfn "            let data = getBytes \"https://www.fontsquirrel.com/fonts/download/%%s\" urlName"
        printfn "            use s = new MemoryStream(data)"
        printfn "            use a = new ZipArchive(s)"
        printfn "        "
        printfn "            let real = fileName.ToLower()"
        printfn "            let ttf = Path.ChangeExtension(real, \".ttf\")"
        printfn "        "
        printfn "            let entry = "
        printfn "                a.Entries |> Seq.tryFind (fun e -> "
        printfn "                    let name = e.Name.ToLower().Replace(\"-webfont\", \"\")"
        printfn "                    e.Length > 0L && (name = real || name = ttf)"
        printfn "                )"
        printfn "        "
        printfn "            match entry with"
        printfn "            | Some e -> "
        printfn "                e.ExtractToFile(path, true)"
        printfn "                Font.Load(path)"
        printfn "            | None ->"
        printfn "                failwithf \"could not resolve %%s %%s\" urlName fileName"
        printfn ""

        let named =
            all |> Dictionary.toMap |> Map.map (fun family map ->
                map |> Map.map (fun _ f ->
                    let knownName = 
                        f.style |> Seq.choose (fun s ->
                            match s with
                            | Other o -> None
                            | s -> Some (string s)
                        ) |> String.concat ""
                
                    let rest =
                        f.style |> Seq.choose (fun s ->
                            match s with
                            | Other o -> Some o
                            | _ -> None
                        ) |> String.concat ""

                    let name = knownName + rest
                    name, f
                )
            )


        for (KeyValue(family, named)) in named do
            let fam = family.Replace(' ', '_').Replace(".", "").Replace("+", "Plus").Replace("&", "And").Replace("[", "").Replace("]", "")


            printfn "    [<AbstractClass; Sealed>]"
            printfn "    type ``%s`` private() =" fam

            
            for (_,(name, f)) in Map.toSeq named do
                printfn "        static let ``_%s`` = lazy (load \"%s\" \"%s\")" name f.urlName f.fileName

            for (_,(name, f)) in Map.toSeq named do
                printfn "        static member internal ``%sLazy`` = ``_%s``" name name
                printfn "        static member ``%s`` = ``_%s``.Value" name name

        printfn "    module Monospace ="
        for (KeyValue(family, named)) in named do
            let fam = family.Replace(' ', '_').Replace(".", "").Replace("+", "Plus").Replace("&", "And").Replace("[", "").Replace("]", "")
            let map = named |> Map.filter (fun _ (_,f) -> f.monospace)
            if not (Map.isEmpty map) then
                printfn "        [<AbstractClass; Sealed>]"
                printfn "        type ``%s`` private() =" fam
                for (_,(name, f)) in Map.toSeq map do
                    printfn "            static member ``%s`` = ``%s``.``%sLazy``.Value" name fam name
                ()

        printfn "    module Serif ="
        for (KeyValue(family, named)) in named do
            let fam = family.Replace(' ', '_').Replace(".", "").Replace("+", "Plus").Replace("&", "And").Replace("[", "").Replace("]", "")
            let map = named |> Map.filter (fun _ (_,f) -> match f.serif with Some s -> s | _ -> false)
            if not (Map.isEmpty map) then
                printfn "        [<AbstractClass; Sealed>]"
                printfn "        type ``%s`` private() =" fam
                for (_,(name, f)) in Map.toSeq map do
                    printfn "            static member ``%s`` = ``%s``.``%sLazy``.Value" name fam name
                ()

        printfn "    module SansSerif ="
        for (KeyValue(family, named)) in named do
            let fam = family.Replace(' ', '_').Replace(".", "").Replace("+", "Plus").Replace("&", "And").Replace("[", "").Replace("]", "")
            let map = named |> Map.filter (fun _ (_,f) -> match f.serif with Some s -> not s | _ -> false)
            if not (Map.isEmpty map) then
                printfn "        [<AbstractClass; Sealed>]"
                printfn "        type ``%s`` private() =" fam
                for (_,(name, f)) in Map.toSeq map do
                    printfn "            static member ``%s`` = ``%s``.``%sLazy``.Value" name fam name
                ()



        File.WriteAllText(outFile, b.ToString())

open System.IO

[<EntryPoint>]
let main argv = 
    FontSquirrelGenerator.run (Path.Combine(__SOURCE_DIRECTORY__, "..", "..", "Aardvark.Rendering.Text", "FontSquirrel.fs"))
    0
