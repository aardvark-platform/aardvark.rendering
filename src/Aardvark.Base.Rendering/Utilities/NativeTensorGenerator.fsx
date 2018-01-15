﻿



module Generator =

    type Blubber<'a> = class end

    type Bla<'a>() =

    
        member x.GetSlice(fixedX : int, maxX : Option<int>, maxY : Option<int>) : Blubber<'a> =
            failwith ""
        member x.GetSlice(minX : Option<int>, minY : Option<int>, fixedY) : Blubber<'a> =
            failwith ""

        member x.GetSlice(minX : Option<int>, minY : Option<int>, maxX : Option<int>, maxY : Option<int>) : Bla<'a> =
            failwith ""
        member x.SetSlice(minX : Option<int>, minY : Option<int>, maxX : Option<int>, maxY : Option<int>, value : 'a) : Bla<'a> =
            failwith ""

    let test (bla : Bla<int>) =
        let a = bla.[*, 1]
        ()

    
    let tensorNames =
        Map.ofList [
            1, "Vector"
            2, "Matrix"
            3, "Volume"
        ]

    let componentNames = [| "X"; "Y"; "Z"; "W" |]

    let vectorNames =
        Map.ofList [
            2, ("V2l", "V2i")
            3, ("V3l", "V3i")
            4, ("V4l", "V4i")
        ]

    let builder = System.Text.StringBuilder()

    let write (str : string) = builder.Append(str) |> ignore
    
    let mutable indent = ""
    let line fmt = Printf.kprintf (fun str -> write indent; write str; write "\r\n") fmt
    let start fmt = Printf.kprintf (fun str -> write indent; write str; write "\r\n"; indent <- indent + "    ") fmt
    let stop() = indent <- indent.Substring(4)

    let rec insert (v : 'a) (l : list<'a>) =
        match l with
            | [] -> [[v]]
            | h :: rest ->
                (v :: h :: rest) ::
                (insert v rest |> List.map (fun l -> h::l))

    let rec allPermutations (l : list<'a>) =
        match l with
            | [] -> [[]]
            | h :: rest ->
                allPermutations rest |> List.collect (insert h)
             
    let rec take2 (l : list<'a>) =
        match l with
            | [] -> Set.empty
            | [e] -> Set.empty
            | [a;b] -> Set.ofList [(a,b)]
            | a :: rest ->
                take2 rest |> Seq.collect (fun (a',b') ->       
                    [ (a, a'); (a, b'); (a', b')]
                ) |> Set.ofSeq

    let rec allComparisons (acc : Map<string * string, string>) (seen : list<string>) (perm : list<string>) =
        match perm with
            | [] -> acc
            | a :: rest -> 
                let mutable res = seen |> List.fold (fun m s -> m |> Map.add (a,s) "<= 0" |> Map.add (s, a) ">= 0 ") acc
                allComparisons res (a :: seen) rest


    let rec private allSubsetsInternal (s : list<'a>) =
        match s with
            | [] -> [Set.empty]
            | h :: rest ->
                let r = allSubsetsInternal rest
                (r |> List.map (Set.add h)) @ r

    let allSubsets (s : list<'a>) =
        allSubsetsInternal s


    let setter (components : string[]) =
        let suffix = components |> String.concat ""
        start "member inline private x.Set%s(value : 'a) = " suffix
        line "let sa = nativeint (sizeof<'a>)"
        line "let mutable ptr = ptr |> NativePtr.toNativeInt"
        line "ptr <- ptr + nativeint info.Origin * sa"

        for d in 0 .. components.Length-2 do
            let mine = components.[d]
            let next = components.[d + 1]
            line "let s%s = nativeint (info.S%s * info.D%s) * sa" mine mine mine
            line "let j%s = nativeint (info.D%s - info.S%s * info.D%s) * sa" mine mine next next


        let mine = components.[components.Length-1]
        line "let s%s = nativeint (info.S%s * info.D%s) * sa" mine mine mine
        line "let j%s = nativeint (info.D%s) * sa" mine mine

        let rec buildLoop (index : int) =
            if index >= components.Length then
                line "NativePtr.write (NativePtr.ofNativeInt<'a> ptr) value"
            else
                let mine = components.[index]
                line "let e%s = ptr + s%s" mine mine
                start "while ptr <> e%s do" mine 
                buildLoop (index + 1)
                line "ptr <- ptr + j%s" mine
                stop()

        buildLoop 0
        stop()

    let copyToInternal (otherType : string) (otherGenArg : string) (args : list<string * string>) (op : string -> string -> string) (components : string[]) =
        let suffix = components |> String.concat ""
        let argDef = (("y", sprintf "%s<%s>" otherType otherGenArg) :: args) |> Seq.map (fun (n,t) -> sprintf "%s : %s" n t) |> String.concat ", "
        
        let sb = 
            if otherGenArg <> "'a" then 
                start "member inline private x.CopyTo%s<%s when %s : unmanaged>(%s) = " suffix otherGenArg otherGenArg argDef
                line "let sb = nativeint (sizeof<%s>)" otherGenArg
                "sb"
            else
                start "member inline private x.CopyTo%s(%s) = " suffix argDef
                "sa"

        line "let sa = nativeint (sizeof<'a>)"
        
        line "let mutable xptr = ptr |> NativePtr.toNativeInt"
        line "xptr <- xptr + nativeint info.Origin * sa"
        line "let mutable yptr = y.Pointer |> NativePtr.toNativeInt"
        line "yptr <- yptr + nativeint y.Info.Origin * %s" sb

        for d in 0 .. components.Length-2 do
            let mine = components.[d]
            let next = components.[d + 1]
            line "let s%s = nativeint (info.S%s * info.D%s) * sa" mine mine mine
            line "let xj%s = nativeint (info.D%s - info.S%s * info.D%s) * sa" mine mine next next
            line "let yj%s = nativeint (y.D%s - y.S%s * y.D%s) * %s" mine mine next next sb


        let mine = components.[components.Length-1]
        line "let s%s = nativeint (info.S%s * info.D%s) * sa" mine mine mine
        line "let xj%s = nativeint (info.D%s) * sa" mine mine
        line "let yj%s = nativeint (y.D%s) * %s" mine mine sb

        let rec buildLoop (index : int) =
            if index >= components.Length then
                let str = op "xptr" "yptr"
                line "%s" str
            else
                let mine = components.[index]
                line "let e%s = xptr + s%s" mine mine
                start "while xptr <> e%s do" mine 
                buildLoop (index + 1)
                line "xptr <- xptr + xj%s" mine
                line "yptr <- yptr + yj%s" mine
                stop()

        buildLoop 0
        stop()


    let dispatcher (check : unit -> unit) (componentNames : list<string>) (name : string) (args : list<string * string>) =
        let argDef = (args |> Seq.map (fun (n,t) -> sprintf "%s : %s" n t) |> String.concat ", ")
        let argRef = args |> Seq.map fst |> String.concat ", "
        start "member x.%s(%s) = " name argDef
        check()
        let comparisons = take2 componentNames
        for (a,b) in comparisons do
            line "let c%s%s = compare (abs info.D%s) (abs info.D%s)" a b a b
        let rec printDispatcher (first : bool) (perms : list<list<string>>) =
            match perms with
                | [] -> ()
                | [perm] ->
                    let suffix = perm |> String.concat ""
                    if first then
                        line "x.%s%s(%s)" name suffix argRef
                    else
                        line "else x.%s%s(%s)" name suffix argRef
                | perm :: rest ->
                    let suffix = perm |> String.concat ""
                    let cond = if first then "if" else "elif"
                    let all = allComparisons Map.empty [] perm
                    let condition = all |> Map.filter (fun k _ -> Set.contains k comparisons) |> Map.toSeq |> Seq.map (fun ((a,b),c) -> sprintf "c%s%s %s" a b c) |> String.concat " && "
                    line "%s %s then x.%s%s(%s)" cond condition name suffix argRef
                    printDispatcher false rest
                    
                    
        printDispatcher true (allPermutations componentNames)
        stop()

    let copyTo (componentNames : list<string>) (otherName : string) (otherGenArg : string) (args : list<string * string>) (op : string -> string -> string) =
        for perm in allPermutations componentNames do copyToInternal otherName otherGenArg args op (List.toArray perm)
        
        let check () =
            start "if info.Size <> y.Size then"
            line "failwithf \"%s size mismatch: { src = %%A; dst = %%A }\" info.Size y.Size" otherName 
            stop()
        
        dispatcher check componentNames "CopyTo" (("y", sprintf "%s<%s>" otherName otherGenArg) :: args)

    let getManagedName (dim : int) =
        match Map.tryFind dim tensorNames with
            | Some n -> n
            | None -> sprintf "Tensor%d" dim

    let getNativeName (dim : int) =
        match Map.tryFind dim tensorNames with
            | Some n -> "Native" + n
            | None -> sprintf "NativeTensor%d" dim


    let sliced (name : string) (args : int -> list<string * string>) (op : int -> string -> string) (pinned : Set<string>) (componentNames : list<string>) =
        
        let free = Set.difference (Set.ofList componentNames) pinned
        let viewDim = Set.count free
        if viewDim > 0 then
            
            let lastManaged = getManagedName viewDim
            let lastName = getNativeName viewDim
            let otherComponents = componentNames |> List.filter (fun c -> not (Set.contains c pinned))

            for long in [false; true] do
                let indexType = if long then "int64" else "int"
                let optionType = if long then "Option<int64>" else "Option<int>"
                let zero = if long then "0L" else "0"
                let one = if long then "1L" else "1"
                let cast = if long then "" else " |> int64"
                let intCast = if long then "" else "int "

                let minMaxArgs = 
                    componentNames |> List.collect (fun c -> 
                        if Set.contains c pinned then [sprintf "min%s" c, indexType ]
                        else [sprintf "min%s" c, optionType; sprintf "max%s" c, optionType]
                    )
                let args = args viewDim
                let argDef = (minMaxArgs @ args) |> List.map (fun (n, t) -> sprintf "%s : %s" n t) |> String.concat ", "
                start "member x.%s(%s) = " name argDef
                for c in componentNames do
                    if Set.contains c pinned then
                        line "let begin%s = min%s%s" c c cast
                    else
                        line "let begin%s = defaultArg min%s %s%s" c c zero cast
                        line "let max%s = defaultArg max%s (%sinfo.S%s - %s)%s" c c intCast c one cast
                        line "let size%s = 1L + max%s - begin%s" c c c 
                

                let offsetRef = componentNames |> List.map (sprintf "begin%s") |> String.concat ", "
                let sizeRef = otherComponents |> List.map (sprintf "size%s") |> String.concat ", "
                let deltaRef = otherComponents |> List.map (sprintf "info.D%s") |> String.concat ", "
                
                match Map.tryFind viewDim vectorNames with
                    | Some (longName, _) ->
                        line "let info = %sInfo(info.Index(%s), %s(%s), %s(%s))" lastManaged offsetRef longName sizeRef longName deltaRef
                    | None -> 
                        line "let info = %sInfo(info.Index(%s), %s, %s)" lastManaged offsetRef sizeRef deltaRef
                line "let res = %s<'a>(ptr, info)" lastName
                line "%s" (op viewDim "res")
                stop()


    let tensor (dim : int) =
        let managedName = getManagedName dim
        let infoName = managedName + "Info"
        let name = getNativeName dim
        let componentNames = Array.take dim componentNames |> Array.toList

        line "[<Sealed>]"
        start "type %s<'a when 'a : unmanaged>(ptr : nativeptr<'a>, info : %s) = " name infoName
        line "member x.Pointer = ptr"
        line "member x.Info = info"
        line "member x.Size = info.Size"
        line "member x.Delta = info.Delta"
        line "member x.Origin = info.Origin"

        for c in componentNames do
            line "member x.S%s = info.S%s" c c
        for c in componentNames do
            line "member x.D%s = info.D%s" c c
        

        // Set (value)
        for perm in allPermutations componentNames do setter (List.toArray perm)
        dispatcher id componentNames "Set" ["value", "'a"]

        // CopyTo(other)
        copyTo componentNames name "'a" [] (fun l r -> sprintf "NativePtr.write (NativePtr.ofNativeInt<'a> %s) (NativePtr.read (NativePtr.ofNativeInt<'a> %s))" r l)
        
        // CopyTo(other, transform)
        copyTo componentNames name "'b" ["f", "'a -> 'b"] (fun l r -> sprintf "NativePtr.write (NativePtr.ofNativeInt<'b> %s) (f (NativePtr.read (NativePtr.ofNativeInt<'a> %s)))" r l)

        let offsets = componentNames |> List.map (sprintf "begin%s")
        let sizes = componentNames |> List.map (sprintf "size%s")
        let deltas = componentNames |> List.map (sprintf "delta%s")

        let offsetDef = offsets |> List.map (sprintf "%s : int64") |> String.concat ", "
        let offsetRef = offsets |> String.concat ", "
        let sizeDef = sizes |> List.map (sprintf "%s : int64") |> String.concat ", "
        let sizeRef = sizes |> String.concat ", "
        let deltaDef = deltas |> List.map (sprintf "%s : int64") |> String.concat ", "
        let deltaRef = deltas |> String.concat ", "


        start "static member Using<'b> (m : %s<'a>, f : %s<'a> -> 'b) = " managedName name 
        line "let gc = GCHandle.Alloc(m.Data, GCHandleType.Pinned)"
        line "try f (%s<'a>(NativePtr.ofNativeInt (gc.AddrOfPinnedObject()), m.Info))" name
        line "finally gc.Free()"
        stop()

        
        line "member x.Sub%s(%s, %s, %s) = %s<'a>(ptr, info.Sub%s(%s, %s, %s))" managedName offsetDef sizeDef deltaDef name managedName offsetRef sizeRef deltaRef
        line "member x.Sub%s(%s, %s) = %s<'a>(ptr, info.Sub%s(%s, %s))" managedName offsetDef sizeDef name managedName offsetRef sizeRef
        
        match Map.tryFind dim vectorNames with
            | Some (longName, intName) ->
                line "member x.Sub%s(offset : %s, size : %s) = %s<'a>(ptr, info.Sub%s(offset, size))" managedName longName longName name managedName
                line "member x.Sub%s(offset : %s, size : %s, delta : %s) = %s<'a>(ptr, info.Sub%s(offset, size, delta))" managedName longName longName longName name managedName
                line "member x.Sub%s(offset : %s, size : %s) = %s<'a>(ptr, info.Sub%s(offset, size))" managedName intName intName name managedName
                line "member x.Sub%s(offset : %s, size : %s, delta : %s) = %s<'a>(ptr, info.Sub%s(offset, size, delta))" managedName intName intName intName name managedName
            | _ ->
                ()

        for pinned in allSubsets componentNames do
            // GetSlice()
            sliced "GetSlice" (fun _ -> []) (fun _ v -> v) pinned componentNames

            // SetSlice(value)
            sliced "SetSlice" (fun _ -> ["value", "'a"]) (fun _ -> sprintf "%s.Set(value)") pinned componentNames 

            //SetSlice(NativeTensor)
            sliced "SetSlice" (fun view -> ["src", sprintf "%s<'a>" (getNativeName view)]) (fun _ -> sprintf "src.CopyTo(%s)") pinned componentNames 

            //SetSlice(ManagedTensor)
            sliced "SetSlice" (fun view -> ["src", sprintf "%s<'a>" (getManagedName view)]) (fun dim res -> sprintf "%s<'a>.Using(src, fun src -> src.CopyTo(%s))" (getNativeName dim) res) pinned componentNames 


        stop()
        line ""
        
        line "/// The %s module providers convenient F#-style functions for accessing %ss" name name
        start "module %s =" name
        line "/// sets the entire %s to the given value" managedName
        line "let inline set (value : 'a) (dst : %s<'a>) = dst.Set(value)" name 
        line ""
        line "/// copies the content of 'src' to 'dst'"
        line "let inline copy (src : %s<'a>) (dst : %s<'a>) = src.CopyTo(dst)" name name 
        line ""
        line "/// copies the content of 'src' to 'dst' by applying the given function"
        line "let inline copyWith (f : 'a -> 'b) (src : %s<'a>) (dst : %s<'b>) = src.CopyTo(dst, f)" name name 
        line ""
        line "/// temporarily pins a %s making it available as %s" managedName name
        start "let using (m : %s<'a>) (f : %s<'a> -> 'b) = %s<'a>.Using(m, f)" managedName name  name
        stop()
        stop()

        line ""
        line ""


        ()

    let run() =
        line "namespace Aardvark.Base"
        line ""
        line "open Microsoft.FSharp.NativeInterop"
        line "open System.Runtime.InteropServices"
        line ""
        line "#nowarn \"9\""
        line ""
        for d in 1 .. 4 do
            tensor d


        let str = builder.ToString()
        System.IO.File.WriteAllText(System.IO.Path.Combine(__SOURCE_DIRECTORY__, @"NativeTensorGenerated.fs"), str)

do Generator.run()