#r "System.Xml.dll"
#r "System.Xml.dll"
#r "System.Xml.Linq.dll"

open System.Xml.Linq
open System
open System.IO
open System.Text.RegularExpressions

[<AutoOpen>]
module XmlStuff =
    let xname s = XName.op_Implicit s
    let attrib (e : XElement) s =
        let e = e.Attribute (xname s)
        if e = null then None
        else Some e.Value

    let attrib' s e = attrib e s

    let child (e : XElement) n =
        match e.Element(xname n) with
            | null -> None
            | e -> Some e.Value

    let numericValue = Regex @"^(?<value>-?[0-9a-fA-F]*(\.[0-9]*)?)(f|F|U|ULL)?$"

    type Numeric =
        | Int32 of int32
        | Int64 of int64
        | UInt32 of uint32
        | UInt64 of uint64
        | Float32 of float32

        member x.Negated =
            match x with
            | Int32 v -> Int32 ~~~v
            | Int64 v -> Int64 ~~~v
            | UInt32 v -> UInt32 ~~~v
            | UInt64 v -> UInt64 ~~~v
            | Float32 _ -> failwithf "Cannot negate float value"

        override x.ToString() =
            match x with
            | Int32 v -> string v
            | Int64 v -> string v + "L"
            | UInt32 v -> string v + "u"
            | UInt64 v -> string v + "UL"
            | Float32 v -> sprintf "%.8ff" v

        static member (-) (l : Numeric, r : Numeric) =
            match l, r with
            | Int32 l, Int32 r -> Int32 (l - r)
            | Int64 l, Int32 r -> Int64 (l - int64 r)
            | UInt32 l, Int32 r -> UInt32 (l - uint32 r)
            | UInt64 l, Int32 r -> UInt64 (l - uint64 r)
            | Float32 l, Int32 r -> Float32 (l - float32 r)

            | Int64 l, Int64 r -> Int64 (l - r)
            | UInt32 l, UInt32 r -> UInt32 (l - r)
            | UInt64 l, UInt64 r -> UInt64 (l - r)
            | Float32 l, Float32 r -> Float32 (l - r)

            | _ -> failwithf "Cannot subtract %A and %A" l r

    let rec toNumeric (radix : int) (v : string) : Numeric =
        let v = v.Trim()

        if v.StartsWith "(" && v.EndsWith ")" then
            v.Substring(1, v.Length-2) |> toNumeric radix
        elif v.StartsWith "~" then
            (toNumeric radix (v.Substring(1))).Negated
        elif v.StartsWith "0x" then
            v.Substring(2) |> toNumeric 16

        else
            let m = numericValue.Match v

            if m.Success then
                let value = m.Groups.["value"].Value

                if v.EndsWith "f" || v.EndsWith "F" then
                    try System.Convert.ToSingle(value, System.Globalization.CultureInfo.InvariantCulture.NumberFormat) |> Float32
                    with e -> 
                        
                        try System.Convert.ToUInt32(value, 16) |> UInt32
                        with e -> failwithf "not a uint32: %A" v
                elif v.EndsWith "U" then
                    try System.Convert.ToUInt32(value, radix) |> UInt32
                    with e -> failwithf "not a uint32: %A" v
                elif v.EndsWith "ULL" then
                    try System.Convert.ToUInt64(value, radix) |> UInt64
                    with e -> failwithf "not a uint64: %A" v
                else
                    System.Convert.ToInt32(value, radix) |> Int32
            else
                failwithf "not a number: %A" v

    let rec toDefine (v : string) : string =
        if v.StartsWith "(" && v.EndsWith ")" then
            v.Substring(1, v.Length-2) |> toDefine
        elif v.Contains("-") then
            let values = v.Split('-') |> Array.map (toNumeric 10)
            values |> Array.reduce (-) |> string
        else
            v |> toNumeric 10 |> string

    let toInt32 (radix : int) (v : string) =
        match v |> toNumeric radix with
        | Int32 v -> v
        | Int64 v -> int32 v
        | UInt32 v -> int32 v
        | UInt64 v -> int32 v
        | Float32 v -> int32 v

    let extensionEnumValue (dir : Option<string>) (e : int) (offset : int) =
        match dir with
            | Some "-" ->
                -(1000000000 + (e - 1) * 1000 + offset)
            | _ ->
                1000000000 + (e - 1) * 1000 + offset


    let (|Enum|BitMask|Ext|Failure|) (e : XElement) =
        let rec f e =
            match attrib e "value", attrib e "bitpos", attrib e "alias" with
            | Some v, _, _ ->
                Enum (v |> toInt32 10)

            | _, Some bp, _ ->
                BitMask (System.Int32.Parse bp)

            | _, _, Some a ->
                let comment = attrib e "comment" |> Option.defaultValue ""
                if comment.Contains("Backwards-compatible") || comment.Contains("backwards compatibility") then
                    Failure
                else
                    // Search the whole document for the alias. This is a bit crazy but perhaps the simplest
                    // way resolve all the scattered aliases including aliases of aliases (e.g. VK_PIPELINE_CREATE_DISPATCH_BASE_KHR).
                    let ref =
                        e.Ancestors(xname "registry").Descendants(xname "enum")
                        |> Seq.filter (fun e -> attrib e "name" = Some a)
                        |> Seq.tryHead

                    match ref with
                    | Some ref ->
                        // Find the reference element and set its relevant values to the current alias element, delete
                        // the alias attribute and simply repeat the match
                        let attributes = ["value"; "bitpos"; "extnumber"; "offset"]

                        attributes
                        |> List.map (fun name -> name, attrib ref name)
                        |> List.iter (fun (name, value) ->
                            value |> Option.iter (fun v -> e.SetAttributeValue(xname name, v))
                        )

                        e.SetAttributeValue(xname "alias", null)
                        f e
                    | _ ->
                        printfn "WARNING: Could not find alias %s" a
                        Failure

            | _ ->
                let extnumber =
                    let ext =
                        e.Ancestors(xname "extension") |> Seq.tryHead

                    attrib e "extnumber"
                    |> Option.orElse (
                        ext |> Option.bind (attrib' "number")
                    )

                match extnumber, attrib e "offset" with
                | Some en, Some on ->
                    let en = en |> toInt32 10
                    let on = on |> toInt32 10
                    Ext (extensionEnumValue (attrib e "dir") en on)

                | _ ->
                    Failure

        f e


type Type =
    | Literal of string
    | Ptr of Type
    | FixedArray of Type * int
    | FixedArray2d of Type * int * int
    | BitField of Type * int

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Type =
    let private cleanRx = Regex @"([ \t\r\n]+|const)"
    let private typeRx = Regex @"(?<name>[a-zA-Z_0-9]+)(\[(?<width>[a-zA-Z_0-9]+)\])?(\[(?<height>[a-zA-Z_0-9]+)\])?(?<ptr>[\*]*)(:(?<bits>[0-9]+))?"

    let rec baseTypeName (t : Type) =
        match t with
        | Literal n -> n
        | Ptr t -> baseTypeName t
        | FixedArray(t,_) -> baseTypeName t
        | FixedArray2d(t,_,_) -> baseTypeName t
        | BitField(t,_) -> baseTypeName t

    let private tryMatch (regex : Regex) (str : string) =
        let ret = regex.Match str
        if ret.Success then Some ret else None

    let arraySize (defines : Map<string, string>) (s : string) =
        match System.Int32.TryParse s with
        | (true, s) -> s
        | _ ->
            match Map.tryFind s defines with
            | Some v ->
                match System.Int32.TryParse v with
                | (true, v) -> v
                | _ -> failwith "non-integer array-size"
            | _ -> failwithf "non-literal array-size: %A" s

    let arraySize2d (defines : Map<string, string>) (width : string) (height : string) =
        let w = arraySize defines width
        let h = arraySize defines height
        w, h

    let private cleanName (name : string) =
        name.Trim().Replace("FlagBits", "Flags")

    let parseTypeAndName (defined : Map<string, string>) (strangeType : string) (strangeName : string) =
        let cleaned = cleanRx.Replace(strangeType, "")

        match cleaned |> tryMatch typeRx with
        | Some m ->
            let id = m.Groups.["name"].Value
            let ptr = m.Groups.["ptr"].Length

            let mutable t = Literal (cleanName id)
            for i in 1..ptr do
                t <- Ptr(t)

            // Array dimensions
            let width = m.Groups.["width"].Value
            let height = m.Groups.["height"].Value

            t <-
                match width, height with
                | "", "" -> t
                | w, "" -> FixedArray(t, arraySize defined w)
                | w, h ->
                    let w, h = arraySize2d defined w h
                    FixedArray2d(t, w, h)

            // Bit field
            let bits = m.Groups.["bits"].Value

            if bits <> "" then
                match System.Int32.TryParse(bits) with
                | (true, size) -> BitField(t, size), cleanName strangeName
                | _ -> failwith "non integer bit field size"
            else
                t, cleanName strangeName

        | _ ->
            failwithf "failed to parse type %s" cleaned

    let rec baseType (t : Type) =
        match t with
        | Literal t -> t
        | Ptr t
        | FixedArray (t, _)
        | FixedArray2d (t, _, _)
        | BitField (t, _) ->
            baseType t

    let readXmlTypeAndName (defined : Map<string, string>) (e : XElement) =
        let strangeName = e.Element(xname "name").Value
        e.Elements(xname "comment").Remove()
        let strangeType =
            let v = e.Value.Trim()
            let id = v.LastIndexOf(strangeName)
            if id < 0 then v
            else v.Substring(0, id).Trim() + v.Substring(id + strangeName.Length)

        parseTypeAndName defined strangeType strangeName

module Comment =
    let tryRead (e : XElement) =
        match attrib e "comment" with
        | Some c ->
            let clean = c.TrimStart(' ', '/').TrimEnd()
            if clean.ToLower() = "optional" then
                None
            else
                Some clean
        | _ -> None

type EnumValue =
    | EnumValue of int
    | EnumBit of int

type EnumCase =
    { name : string; value : EnumValue; comment : string option }

type Enum =
    { name : string; bitmask : bool; cases : EnumCase list }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Enum =

    //let baseName (suffices : string list) (e : string) =
    //    let rec remove (str : string) =
    //        let suffix = suffices |> List.filter str.EndsWith
    //        match suffix with
    //        | x :: _ -> str.Substring(0, str.Length - x.Length)
    //        | [] -> str

    //    let name = remove e
    //    let suffix = "Flags"

    //    if name.EndsWith suffix then
    //        name.Substring(0, name.Length - suffix.Length)
    //    else
    //        name

    let cleanName (name : string) =
        name.Trim().Replace("FlagBits", "Flags")

    let valueToStr (v : EnumValue) =
        match v with
        | EnumValue v -> sprintf "%d" v
        | EnumBit b -> sprintf "0x%08X" (1 <<< b)

    let tryGetValue (e : XElement) =
        match e with
        | Enum value -> Some (EnumValue value)
        | BitMask bit -> Some (EnumBit bit)
        | Ext value -> Some (EnumValue value)
        | _ ->
            printfn "Ignoring: %A" e
            None

    let tryRead (e : XElement) =
        let bitmask =
            attrib e "type" = Some "bitmask"

        match attrib e "name" with
        | Some name ->
            let cases =
                e.Descendants(xname "enum")
                    |> Seq.choose (fun kv ->
                        let name = attrib kv "name"
                        let comment = Comment.tryRead kv
                        let value = tryGetValue kv
                        value |> Option.map (fun v -> { name = name.Value; value = v; comment = comment })
                    )
                    |> Seq.toList

            match cases with
            | [] when bitmask ->
                let none = { name = "NONE"; value = EnumValue 0; comment = None }
                Some { name = name; bitmask = bitmask; cases = [none] }
            | [] ->
                None
            | _ ->
                Some { name = name; bitmask = bitmask; cases = cases }

        | None -> None

type StructField = {
    typ : Type
    name : string
    values : string option
}

type Struct = {
    name : string
    fields : list<StructField>
    isUnion : bool
    alias : Option<string>
    comment : Option<string>
}

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Struct =
    let tryRead (defines : Map<string, string>) (e : XElement) =
        let isUnion = attrib e "category" = Some "union"
        let comment = Comment.tryRead e
        match attrib e "name" with
        | Some name ->
            match attrib e "alias" with
            | Some alias ->
                Some { name = name; fields = []; isUnion = isUnion; alias = Some alias; comment = comment }
            | None ->
                let fields =
                    e.Descendants (xname "member")
                        |> Seq.map (fun m ->
                            m.Elements(xname "comment").Remove()

                            let name = m.Element(xname "name").Value
                            let t =
                                let v = m.Value.Trim()
                                let id = v.LastIndexOf(name)
                                if id < 0 then v
                                else v.Substring(0, id).Trim() + v.Substring(id + name.Length)

                            let values = attrib m "values"
                            let typ, name = Type.parseTypeAndName defines t name

                            { typ = typ; name = name; values = values }
                        )
                        |> Seq.toList

                Some { name = name; fields = fields; isUnion = isUnion; alias = None; comment = comment }

        | None -> None

    let private dfs (graph : Map<'a, list<'a>>) visited start_node =
      let rec explore path visited node =
        if List.exists (fun n -> n = node) path    then printfn "WARNING: Dependency cycle detected"; visited else
        if List.exists (fun n -> n = node) visited then visited else
          let new_path = node :: path in
          let edges    = Map.find node graph in
          let visited  = List.fold (explore new_path) visited edges in
          node :: visited
      in explore [] visited start_node

    let toposort graph =
      List.fold (fun visited (node,_) -> dfs graph visited node) [] (Map.toList graph)

    let topologicalSort (s : list<Struct>) : list<Struct> =

        let typeMap = s |> List.map (fun s -> s.name, s) |> Map.ofList

        let graph =
            s |> List.map (fun s ->
                    let usedTypes =
                        let fields =
                            s.fields
                            |> List.map (fun f -> f.typ)
                            |> List.map Type.baseType
                            |> List.choose (fun m -> Map.tryFind m typeMap)

                        let alias =
                            s.alias
                            |> Option.bind (fun a -> Map.tryFind a typeMap)
                            |> Option.toList

                        fields @ alias

                    s, usedTypes

                 )
               |> Map.ofList

        toposort graph |> List.rev

type Typedef = { name : string; baseType : Type }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Typedef =

    let tryRead (defines : Map<string, string>) (e : XElement) =
        match child e "name", child e "type" with
        | Some n, Some t ->
            let (t, n) = Type.parseTypeAndName defines t n

            let emit = 
                match t with
                | Type.Literal t -> Enum.cleanName t <> Enum.cleanName n
                | _ -> true
            if emit then
                Some { name = Enum.cleanName n; baseType = t}
            else
                None
        | _ -> None


type Alias =
    {
        name : string
        baseSym : string
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Alias =

    let tryRead (e : XElement) =
        match attrib e "name", attrib e "alias" with
        | Some n, Some a ->
            let (n, a) = Enum.cleanName n, Enum.cleanName a
            if n <> a then
                Some { name = n; baseSym = a }
            else
                None
        | _ ->
            None

type Command = { returnType : Type; name : string; parameters : list<Type * string> }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Command =
    let tryRead (defines : Map<string, string>) (e : XElement) : Option<Choice<Command, Alias>> =
        try
            let proto = e.Element(xname "proto")
            let (returnType,name) = Type.readXmlTypeAndName defines proto

            let parameters =
                e.Elements(xname "param")
                    |> Seq.map (Type.readXmlTypeAndName defines)
                    |> Seq.toList


            Some <| Choice1Of2 { returnType = returnType; name = name; parameters = parameters }
        with _ ->
            match Alias.tryRead e with
            | Some a -> Some <| Choice2Of2 a
            | None -> printfn "WARNING: Invalid command '%A'" e; None

    let tryResolveAlias (commands : Map<string, Command>) (alias : Alias) =
        match commands |> Map.tryFind alias.baseSym with
        | Some cmd -> Some { cmd with name = alias.name }
        | _ ->
            printfn "WARNING: Command alias %s not found" alias.baseSym
            None

type Handle =
    {
        name : string
        nonDispatchable : bool
    }

type FuncPointer =
    {
        name : string
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module FuncPointer =
    let tryRead (e : XElement) =
        match child e "name" with
        | Some n -> Some { name = n }
        | None -> None

type ArrayStruct =
    {
        baseType : string
        baseTypeSize : int
        count : int
    }


type Definitions =
    {
        enums : Map<string, Enum>
        structs : Map<string, Struct>
        aliases : Map<string, Alias>
        commands : Map<string, Command>
        typedefs : Map<string, Typedef>
        handles : Map<string, Handle>
        funcpointers : Map<string, FuncPointer>
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Definitions =

    let tryFindCommand (name : string) (definitions : Definitions) =
        definitions.commands |> Map.tryFind name

    let tryFindType (name : string) (definitions : Definitions) =
        let e = definitions.enums |> Map.tryFind name
        let s = definitions.structs |> Map.tryFind name
        let a = definitions.aliases |> Map.tryFind name
        let f = definitions.funcpointers |> Map.tryFind name
        let t = definitions.typedefs |> Map.tryFind name
        let h = definitions.handles |> Map.tryFind name

        match e, s, a, f, t, h with
        | Some e, _, _, _, _, _ -> Some <| Choice1Of6 e
        | _, Some s, _, _, _, _ -> Some <| Choice2Of6 s
        | _, _, Some a, _, _, _ -> Some <| Choice3Of6 a
        | _, _, _, Some f, _, _ -> Some <| Choice4Of6 f
        | _, _, _, _, Some t, _ -> Some <| Choice5Of6 t
        | _, _, _, _, _, Some h -> Some <| Choice6Of6 h
        | _ -> None


type VkVersion =
    | VkVersion10
    | VkVersion11
    | VkVersion12
    | VkVersion13

    static member All =
        [VkVersion10; VkVersion11; VkVersion12; VkVersion13]

    static member Parse(str) =
        match str with
        | "VK_VERSION_1_0" -> VkVersion10
        | "VK_VERSION_1_1" -> VkVersion11
        | "VK_VERSION_1_2" -> VkVersion12
        | "VK_VERSION_1_3" -> VkVersion13
        | _ -> failwithf "failed to parse version %s" str

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module VkVersion =

    let autoOpen = function
        | VkVersion10 -> false
        | VkVersion11 -> false
        | VkVersion12 -> false
        | VkVersion13 -> false

    let toModuleName = function
        | VkVersion10 -> None
        | VkVersion11 -> Some "Vulkan11"
        | VkVersion12 -> Some "Vulkan12"
        | VkVersion13 -> Some "Vulkan13"

    let getPriorModules (version : VkVersion) =
        let index = VkVersion.All |> List.findIndex ((=) version)
        VkVersion.All |> List.splitAt index |> fst |> List.choose toModuleName

    let allModules =
        VkVersion.All |> List.choose toModuleName

[<RequireQualifiedAccess>]
type RequiredBy =
    | Core of VkVersion
    | Extension of string

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RequiredBy =

    let autoOpen = function
        | RequiredBy.Core v -> VkVersion.autoOpen v
        | _ -> false

    let isCore (r : RequiredBy) =
        match r with
        | RequiredBy.Core _ -> true
        | _ -> false


type Require =
    {
        enumExtensions  : Map<string, EnumCase list>
        enums           : list<Enum>
        structs         : list<Struct>
        aliases         : list<Alias>
        commands        : list<Command>
        funcpointers    : list<FuncPointer>
        typedefs        : list<Typedef>
        handles         : list<Handle>
        comment         : string option
        requiredBy      : RequiredBy
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Require =
    let isEmpty (r : Require) =
        Map.isEmpty r.enumExtensions && List.isEmpty r.enums && List.isEmpty r.structs && List.isEmpty r.commands

    let union (x : Require) (y : Require) =

        let mapOfListUnion (x : Map<string, 'a list>) (y : Map<string, 'a list>) =
            (x, y) ||> Map.fold (fun map key values ->
                match Map.tryFind key map with
                | None -> map |> Map.add key values
                | Some x -> map |> Map.add key (x @ values)
            )

        if x.requiredBy <> y.requiredBy then
            failwith "cannot union interfaces required by different APIs"
        else
            {
                enumExtensions = mapOfListUnion x.enumExtensions y.enumExtensions
                enums = x.enums @ y.enums
                structs = x.structs @ y.structs
                aliases = x.aliases  @ y.aliases |> List.distinct
                commands = x.commands @ y.commands
                funcpointers = x.funcpointers @ y.funcpointers
                typedefs = x.typedefs @ y.typedefs |> List.distinct
                handles = x.handles @ y.handles
                comment = None
                requiredBy = x.requiredBy
            }

    let unionMany (r : seq<Require>) =
        r |> Seq.reduce union


type Feature =
    {
        version : VkVersion
        requires : List<Require>
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Feature =
    let isEmpty (f : Feature) =
        f.requires |> List.forall Require.isEmpty


type Extension =
    {
        name            : string
        number          : int
        dependencies    : Set<string>
        references      : Set<string>
        requires        : List<Require>
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Extension =
    let private dummyRx = System.Text.RegularExpressions.Regex @"VK_[A-Za-z]+_extension_([0-9]+)"

    let isEmpty (e : Extension) =
        dummyRx.IsMatch e.name && e.requires |> List.forall Require.isEmpty

module XmlReader =
    let vendorTags (registry : XElement) =
        registry.Elements(xname "tags")
            |> Seq.collect (fun e ->
                e.Elements(xname "tag")
                |> Seq.choose (fun c ->
                    match attrib c "name" with
                    | Some name -> Some name
                    | _ -> None
                )
            )
            |> List.ofSeq

    let defines (registry : XElement) =
        registry.Elements(xname "enums")
            |> Seq.filter (fun e -> attrib e "name" = Some "API Constants")
            |> Seq.collect (fun e ->
                  let choices = e.Elements(xname "enum")
                  choices |> Seq.choose (fun c ->
                    match attrib c "name", attrib c "value" with
                        | Some name, Some value -> Some(name, toDefine value)
                        | _ -> None
                  )
              )
            |> Map.ofSeq

    let readRequire (definitions : Definitions) (require : XElement) =
        let enumExtensions =
            require.Elements(xname "enum")
            |> List.ofSeq
            |> List.choose (fun e ->
                match attrib e "extends", attrib e "name" with
                | Some baseType, Some name ->
                    let name = Enum.cleanName name
                    let baseType = Enum.cleanName baseType
                    match Enum.tryGetValue e with
                    | Some value -> Some (baseType, { name = name; value = value; comment = Comment.tryRead e })
                    | None -> None
                | _ ->
                    None
            )

        let types =
            require.Descendants(xname "type")
                |> Seq.toList
                |> List.choose (fun t ->
                    match attrib t "name" with
                    | Some name -> Some (Enum.cleanName name)
                    | None -> None
                )
                |> List.distinct
                |> List.choose (fun name ->
                    match definitions |> Definitions.tryFindType name with
                    | Some t -> Some t
                    | None ->
                        printfn "WARNING: Could not find type definition: %A" name
                        None
                )

        let commands =
            require.Descendants(xname "command")
                |> Seq.toList
                |> List.choose (fun c ->
                    match attrib c "name" with
                    | Some name -> definitions |> Definitions.tryFindCommand name
                    | None -> None
                )

        let enums        = types |> List.choose (function Choice1Of6 e -> Some e | _ -> None)
        let structs      = types |> List.choose (function Choice2Of6 e -> Some e | _ -> None)
        let aliases      = types |> List.choose (function Choice3Of6 e -> Some e | _ -> None)
        let funcpointers = types |> List.choose (function Choice4Of6 e -> Some e | _ -> None)
        let typedefs     = types |> List.choose (function Choice5Of6 e -> Some e | _ -> None) |> List.distinct
        let handles      = types |> List.choose (function Choice6Of6 e -> Some e | _ -> None)

        let groups =
            enumExtensions
            |> List.groupBy (fun (b, _) -> b) |> List.map (fun (g,l) -> g, l |> List.map (fun (_, c) -> c)) |> Map.ofList
            |> Map.filter (fun name _ -> name <> "VkStructureType")

        let requiredBy =
            match attrib require "feature", attrib require "extension" with
            | Some f, _ -> RequiredBy.Core <| VkVersion.Parse(f)
            | _, Some e -> RequiredBy.Extension e
            | _ -> RequiredBy.Core VkVersion10

        {
            enumExtensions  = groups
            enums           = enums
            structs         = structs
            aliases         = []
            commands        = commands
            funcpointers    = funcpointers
            typedefs        = typedefs
            handles         = handles
            comment         = attrib require "comment"
            requiredBy      = requiredBy
        }

    let readFeature (definitions : Definitions) (feature : XElement) =
        match attrib feature "name" with
        | Some name ->
            let requires =
                feature.Elements(xname "require")
                |> List.ofSeq
                |> List.choose (fun r ->
                    let r = r |> readRequire definitions

                    if Require.isEmpty r then
                        None
                    else
                        Some r
                )

            {
                version = VkVersion.Parse(name)
                requires = requires
            }

        | _ ->
            failwith "Feature tag without name!"

    let features (definitions : Definitions) (registry : XElement) =
        registry.Elements(xname "feature")
            |> List.ofSeq
            |> List.choose (fun f ->
                let f = f |> readFeature definitions

                if Feature.isEmpty f then
                    None
                else
                    Some f
            )

    let readExtension (definitions : Definitions) (extension : XElement) =
        match attrib extension "name", attrib extension "number" with
        | Some name, Some number ->
            let number = Int32.Parse(number)

            let dependencies =
                match attrib extension "requires" with
                | Some v -> v.Split(',') |> Set.ofArray
                | None -> Set.empty

            let requires =
                extension.Elements(xname "require")
                    |> List.ofSeq
                    |> List.choose (fun r ->
                        let r = r |> readRequire definitions

                        if Require.isEmpty r then
                            None
                        else
                            Some r
                    )

            {
                name         = name
                number       = number
                dependencies = dependencies
                references   = dependencies
                requires     = requires
            }

        | _ ->
            failwith "Extension missing name or number"

    let extensions (definitions : Definitions) (registry : XElement) =
        registry.Element(xname "extensions").Elements(xname "extension")
            |> List.ofSeq
            |> List.filter (fun e -> attrib e "supported" <> Some "disabled")
            |> List.choose (fun e ->
                let e = e |> readExtension definitions

                if Extension.isEmpty e then
                    None
                else
                    Some e
            )

    let emptyBitfields (registry : XElement) =
        registry.Element(xname "types").Elements(xname "type")
            |> List.ofSeq
            |> List.filter (fun e -> attrib e "category" = Some "bitmask" && attrib e "requires" = None)
            |> List.choose (fun e -> child e "name")

    let enums (registry : XElement) =
        registry.Elements(xname "enums")
            |> Seq.filter (fun e -> attrib e "name" <> Some "API Constants")
            |> Seq.choose Enum.tryRead
            |> Seq.map (fun e -> Enum.cleanName e.name, e)
            |> Map.ofSeq

    let structureTypes (registry : XElement) =
        let name = "VkStructureType"

        let baseCases =
            registry.Descendants(xname "enums")
                |> Seq.filter (fun e -> attrib e "name" = Some name)
                |> Seq.collect (fun e -> e.Elements(xname "enum"))

        let extensionCases =
            registry.Descendants(xname "enum")
                |> Seq.filter (fun e -> attrib e "extends" = Some name)

        seq { baseCases; extensionCases}
            |> Seq.concat
            |> Seq.choose (fun e ->
                let name = attrib e "name"
                e |> Enum.tryGetValue |> Option.map (fun v -> name.Value, Enum.valueToStr v)
            )
            |> Map.ofSeq

    let funcpointers (registry : XElement) =
        registry.Elements(xname "types")
            |> Seq.collect (fun tc -> tc.Elements (xname "type"))
            |> Seq.filter (fun t -> attrib t "category" = Some "funcpointer")
            |> Seq.choose (FuncPointer.tryRead)
            |> Seq.toList
            |> Seq.map (fun s -> s.name, s)
            |> Map.ofSeq

    let structs (defines : Map<string, string>) (registry : XElement) =
        registry.Elements(xname "types")
            |> Seq.collect (fun tc -> tc.Elements (xname "type"))
            |> Seq.filter (fun t -> attrib t "category" = Some "struct" || attrib t "category" = Some "union")
            |> Seq.choose (Struct.tryRead defines)
            |> Seq.toList
            |> Seq.map (fun s -> s.name, s)
            |> Map.ofSeq

    let typedefs (defines : Map<string, string>) (registry : XElement) =
        registry.Elements(xname "types")
            |> Seq.collect (fun tc -> tc.Elements (xname "type"))
            |> Seq.filter (fun t ->  attrib t "category" = Some "basetype")
            |> Seq.choose (Typedef.tryRead defines)
            |> Seq.toList
            |> Seq.map (fun t -> t.name, t)
            |> Map.ofSeq

    let aliases (registry : XElement) =
        registry.Elements(xname "types")
            |> Seq.collect (fun tc -> tc.Elements (xname "type"))
            |> Seq.filter (fun t ->  attrib t "alias" |> Option.isSome)
            |> Seq.choose Alias.tryRead
            |> Seq.map (fun t -> t.name, t)
            |> Map.ofSeq

    let handles (registry : XElement) =
        registry.Elements(xname "types")
            |> Seq.collect (fun tc -> tc.Elements (xname "type"))
            |> Seq.filter (fun t -> attrib t "category" = Some "handle")
            |> Seq.choose (fun e ->
                match child e "name" with
                | Some name -> Some (name, { name = name; nonDispatchable = e.Value.Contains "NON_DISPATCHABLE" })
                | _ -> None
            )
            |> Map.ofSeq

    let commands (defines : Map<string, string>) (registry : XElement) =
        let elems =
            registry.Element(xname "commands").Elements(xname "command")

        let cmdsAndAliases =
            elems
            |> Seq.choose (Command.tryRead defines)
            |> Seq.toList

        let cmds =
            cmdsAndAliases
            |> List.choose (function Choice1Of2 cmd -> Some cmd | _ -> None)
            |> List.map (fun cmd -> cmd.name, cmd)
            |> Map.ofList

        let aliases =
            cmdsAndAliases
            |> List.choose (function Choice2Of2 alias -> Command.tryResolveAlias cmds alias | _ -> None)
            |> List.map (fun cmd -> cmd.name, cmd)
            |> Map.ofList

        (cmds, aliases) ||> Map.fold (fun m k v -> m |> Map.add k v)

module FSharpWriter =

    open System.Text.RegularExpressions
    open System.Xml.Linq

    type Location =
        | Global of VkVersion
        | Extension of name: string * requiredBy: RequiredBy

        member x.RelativePath(relativeTo : Location) =
            let sprintVersion =
                VkVersion.toModuleName >> Option.map (sprintf "%s.") >> Option.defaultValue ""

            let sprintRequiredBy = function
                | RequiredBy.Core v -> sprintVersion v
                | RequiredBy.Extension e -> sprintf "%s." e

            match x, relativeTo with
            | Global v, _ -> sprintVersion v
            | Extension (n1, r1), Extension(n2, _) when n1 = n2 -> sprintRequiredBy r1
            | Extension (n1, r1), _ -> sprintf "%s.%s" n1 <| sprintRequiredBy r1

    let definitionLocations = Collections.Generic.Dictionary<string, Location>()

    let tryGetTypeAlias (location : Location) (name : string) =
        let name = Enum.cleanName name
        match definitionLocations.TryGetValue(name) with
        | true, alias ->
            let path = alias.RelativePath(location)
            Some <| sprintf "%s%s" path name
        | _ ->
            definitionLocations.Add(name, location)
            None

    let tryGetCommandAlias (location : Location) (name : string) =
        match definitionLocations.TryGetValue(name) with
        | true, alias ->
            let path = alias.RelativePath(location)
            Some <| sprintf "%sVkRaw.%s" path name
        | _ ->
            definitionLocations.Add(name, location)
            None

    let private uppercase = Regex @"[A-Z0-9]+"
    let private startsWithNumber = Regex @"^[0-9]+"

    let private removePrefix (p : string) (str : string) =
        if str.StartsWith p then str.Substring(p.Length)
        else str

    let private removePrefixes (p : string list) (str : string) =
        let p = p |> List.sortByDescending String.length
        (str, p) ||> List.fold (fun str p -> removePrefix p str)

    let private removeSuffix (s : string) (str : string) =
        if str.EndsWith s then str.Substring(0, str.Length - s.Length)
        else str

    let private avoidStartWithNumber (str : string) =
        if startsWithNumber.IsMatch str then "D" + str
        else str

    let capsToCamelCase (prefixes : string list) (suffix : string) (str : string) =
        let matchCollection = uppercase.Matches str
        let matches = seq { for m in matchCollection do yield m.Value }
        matches
            |> Seq.map (fun m -> m.Substring(0, 1) + m.Substring(1).ToLower())
            |> String.concat ""
            |> removePrefixes prefixes
            |> removeSuffix suffix
            |> avoidStartWithNumber

    let addNoneCase (cases : list<EnumCase>) =
        let hasNoneCase =
            cases |> List.exists (fun c ->
                match c.name, c.value with
                | "None", _ -> true
                | _, EnumValue 0 -> true
                | _, EnumBit -1 -> true
                | _ -> false
            )

        if not hasNoneCase then
            { name = "None"; value = EnumValue 0; comment = None} :: cases
        else
            cases

    let addAllCase (cases : list<EnumCase>) =
        let value =
            cases |> List.fold (fun x c ->
                match c.value with
                | EnumBit b -> x ||| (1 <<< b)
                | _ -> x
            ) 0

        let hasAllCase =
            cases |> List.exists (fun c ->
                match c.name, c.value with
                | "All", _ -> true
                | _, EnumValue v when cases.Length > 1 -> v = value
                | _ -> false
            )

        if not hasAllCase then
            { name = "All"; value = EnumValue value; comment = None } :: cases
        else
            cases

    let builder = System.Text.StringBuilder()

    let printfn fmt =
        Printf.kprintf (fun str -> builder.AppendLine(str) |> ignore) fmt

    let header() =
        printfn "namespace Aardvark.Rendering.Vulkan"
        printfn ""
        printfn "#nowarn \"1337\""
        printfn "#nowarn \"49\""
        printfn ""
        printfn "open System"
        printfn "open System.Runtime.InteropServices"
        printfn "open System.Runtime.CompilerServices"
        printfn "open Microsoft.FSharp.NativeInterop"
        printfn "open System.Security"
        printfn "open Aardvark.Base"
        printfn "open Aardvark.Rendering.Vulkan"
        printfn ""
        printfn "#nowarn \"9\""
        printfn "#nowarn \"51\""

    let missingExtensionReferences =
        [
            "VK_KHR_device_group", ["VK_KHR_swapchain"]
            "VK_KHR_sampler_ycbcr_conversion", ["VK_EXT_debug_report"]
            "VK_KHR_acceleration_structure", ["VK_EXT_debug_report"]
            "VK_NV_ray_tracing", ["VK_EXT_debug_report"; "VK_KHR_acceleration_structure"; "VK_KHR_ray_tracing_pipeline"]
            "VK_KHR_ray_tracing_pipeline", ["VK_KHR_pipeline_library"]
            "VK_KHR_descriptor_update_template", ["VK_EXT_debug_report"]
        ]
        |> List.map (fun (n, d) -> n, Set.ofList d)
        |> Map.ofList

    let emptyBitfields (enums : List<string>) =
        enums |> List.iter (fun e ->
            printfn ""
            printfn "[<Flags>]"
            printfn "type %s = | None = 0" e
        )

    let inlineArray (indent : string) (location : Location) (baseType : string) (baseTypeSize : int) (size : int) =
        let printfn fmt =
            Printf.kprintf (fun str ->
                if str = "" then printfn "" else printfn "%s%s" indent str
            ) fmt

        let name = sprintf "%s_%d" baseType size

        match tryGetTypeAlias location name with
        | None ->
            let totalSize = size * baseTypeSize
            printfn "[<StructLayout(LayoutKind.Explicit, Size = %d)>]" totalSize
            printfn "type %s =" name
            printfn "    struct"
            printfn "        [<FieldOffset(0)>]"
            printfn "        val mutable public First : %s" baseType
            printfn ""
            printfn "        member x.Item"
            printfn "            with get (i : int) : %s =" baseType
            printfn "                if i < 0 || i > %d then raise <| IndexOutOfRangeException()" (size - 1)
            printfn "                let ptr = &&x |> NativePtr.toNativeInt |> NativePtr.ofNativeInt"
            printfn "                NativePtr.get ptr i"
            printfn "            and set (i : int) (value : %s) =" baseType
            printfn "                if i < 0 || i > %d then raise <| IndexOutOfRangeException()" (size - 1)
            printfn "                let ptr = &&x |> NativePtr.toNativeInt |> NativePtr.ofNativeInt"
            printfn "                NativePtr.set ptr i value"
            printfn ""
            printfn "        member x.Length = %d" size
            printfn ""
            printfn "        interface System.Collections.IEnumerable with"
            printfn "            member x.GetEnumerator() = let x = x in (Seq.init %d (fun i -> x.[i])).GetEnumerator() :> System.Collections.IEnumerator" size
            printfn "        interface System.Collections.Generic.IEnumerable<%s> with" baseType
            printfn "            member x.GetEnumerator() = let x = x in (Seq.init %d (fun i -> x.[i])).GetEnumerator()" size
            printfn "    end"
        | Some alias ->
            if name <> alias then
                printfn "type %s = %s" name alias

//    let extendedEnums() =
//        printfn "[<AutoOpen>]"
//        printfn "module WSIEnums = "
//        printfn "    type VkStructureType with"
//        printfn "        static member XLibSurfaceCreateInfo = unbox<VkStructureType> 1000004000"
//        printfn "        static member XcbSurfaceCreateInfo = unbox<VkStructureType> 1000005000"
//        printfn "        static member WaylandSurfaceCreateInfo = unbox<VkStructureType> 1000006000"
//        printfn "        static member MirSurfaceCreateInfo = unbox<VkStructureType> 1000007000"
//        printfn "        static member AndroidSurfaceCreateInfo = unbox<VkStructureType> 1000008000"
//        printfn "        static member Win32SurfaceCreateInfo = unbox<VkStructureType> 1000009000"
////        VK_STRUCTURE_TYPE_XLIB_SURFACE_CREATE_INFO_KHR = 1000004000,
////        VK_STRUCTURE_TYPE_XCB_SURFACE_CREATE_INFO_KHR = 1000005000,
////        VK_STRUCTURE_TYPE_WAYLAND_SURFACE_CREATE_INFO_KHR = 1000006000,
////        VK_STRUCTURE_TYPE_MIR_SURFACE_CREATE_INFO_KHR = 1000007000,
////        VK_STRUCTURE_TYPE_ANDROID_SURFACE_CREATE_INFO_KHR = 1000008000,
////        VK_STRUCTURE_TYPE_WIN32_SURFACE_CREATE_INFO_KHR = 1000009000,
    let apiConstants (map : Map<string, string>) =
        printfn ""
        printfn "[<AutoOpen>]"
        printfn "module Constants = "
        for (n,v) in Map.toSeq map do
            printfn ""
            printfn "    [<Literal>]"
            let n = n |> capsToCamelCase [] ""
            printfn "    let %s = %s" n v
        printfn ""

    let cleanEnumName (e : string) =
        if e.Contains "FlagBits" then e.Replace("FlagBits", "Flags")
        else e

    let findEnumVendorSuffix (vendorTags : string list) (e : string) =
        vendorTags
        |> List.filter (fun tag -> e <> tag && e.EndsWith tag)
        |> List.tryHead

    let baseEnumName (vendorTags : string list) (e : string) =
        let rec remove (str : string) =
            let suffix = str |> findEnumVendorSuffix vendorTags
            match suffix with
            | Some x -> str.Substring(0, str.Length - x.Length) |> remove
            | None -> str

        let name = remove e
        let suffix = "Flags"

        if name.EndsWith suffix then
            name.Substring(0, name.Length - suffix.Length)
        else
            name

    let knownTypes =
        Map.ofList [
            "VkStructureType", "uint32"
            "int8_t", "int8"
            "uint8_t", "byte"
            "char", "byte"
            "int16_t", "int16"
            "uint16_t", "uint16"
            "uint32_t", "uint32"
            "int32_t", "int"
            "int", "int"
            "float", "float32"
            "double", "float"
            "int64_t", "int64"
            "int64_t", "int64"
            "uint64_t", "uint64"
            "uint64_t", "uint64"
            "size_t", "uint64"

            // for extern stuff only
            "void", "void"

            "HANDLE", "nativeint"
            "HINSTANCE", "nativeint"
            "HWND", "nativeint"
            "Display", "nativeint"
            "Window", "nativeint"
            "VisualID", "nativeint"
            "ANativeWindow", "nativeint"
            "xcb_connection_t", "nativeint"
            "xcb_window_t", "nativeint"
            "xcb_visualid_t", "nativeint"
            "SECURITY_ATTRIBUTES", "nativeint"
            "xcb_connection_t", "nativeint"
            "RROutput", "nativeint"

            "DWORD", "uint32"
            "LPCWSTR", "cstr"
        ]

    let reservedKeywords = Set.ofList ["module"; "type"; "object"; "SFRRectCount"; "function"]

    let vulkanTypeArrays =
        [
            { baseType = "VkPhysicalDevice"; baseTypeSize = 8; count = 32 }
            { baseType = "VkDeviceSize"; baseTypeSize = 8; count = 16 }
            { baseType = "VkFragmentShadingRateCombinerOpKHR"; baseTypeSize = 4; count = 2 }
            { baseType = "VkMemoryHeap"; baseTypeSize = 16; count = 16 }
            { baseType = "VkMemoryType"; baseTypeSize = 8; count = 32 }
            { baseType = "VkOffset3D"; baseTypeSize = 12; count = 2 }
        ]
        |> List.map (fun s -> s.baseType, s)
        |> Map.ofList

    let fsharpName (n : string) =
        if Set.contains n reservedKeywords then sprintf "_%s" n
        else n

    let rec typeName (n : Type) =
        match n with
            | FixedArray(Literal "int32_t", 2) -> "V2i"
            | FixedArray(Literal "int32_t", 3) -> "V3i"
            | FixedArray(Literal "int32_t", 4) -> "V4i"
            | FixedArray(Literal "uint32_t", 2) -> "V2ui"
            | FixedArray(Literal "uint32_t", 3) -> "V3ui"
            | FixedArray(Literal "uint32_t", 4) -> "V4ui"
            | FixedArray(Literal "float", 2) -> "V2f"
            | FixedArray(Literal "float", 3) -> "V3f"
            | FixedArray(Literal "float", 4) -> "V4f"
            | FixedArray(Literal "uint8_t", 16) -> "Guid"

            | FixedArray2d(Literal "int32_t", 2, 2) -> "M22i"
            | FixedArray2d(Literal "int32_t", 3, 3) -> "M33i"
            | FixedArray2d(Literal "int32_t", 4, 4) -> "M44i"
            | FixedArray2d(Literal "int32_t", 2, 3) -> "M23i"
            | FixedArray2d(Literal "int32_t", 3, 4) -> "M34i"
            | FixedArray2d(Literal "float", 2, 2) -> "M22f"
            | FixedArray2d(Literal "float", 3, 3) -> "M33f"
            | FixedArray2d(Literal "float", 4, 4) -> "M44f"
            | FixedArray2d(Literal "float", 2, 3) -> "M23f"
            | FixedArray2d(Literal "float", 3, 4) -> "M34f"

            | BitField(l, s) ->
                match typeName l, s with
                | "int32", 8 -> "int8"
                | "uint32", 8 -> "uint8"
                | "uint32", 24 -> "uint24"
                | t, 8 -> System.Console.WriteLine("WARNING: Replacing {0}:8 with uint8", t); "uint8"
                | t, 24 -> System.Console.WriteLine("WARNING: Replacing {0}:24 with uint24", t); "uint24"
                | _ -> failwithf "unsupported bit field type %A:%A" l s

            | Ptr(Literal "char") -> "cstr"
            | FixedArray(Literal "char", s) -> sprintf "String%d" s
            | Ptr(Literal "void") -> "nativeint"
            | Literal n ->
                if n.Contains "FlagBits" then
                    n.Replace("FlagBits", "Flags") //.Substring(0, n.Length - 8) + "Flags"
                else
                    match Map.tryFind n knownTypes with
                        | Some n -> n
                        | None ->
                            if n.StartsWith "Vk" || n.StartsWith "PFN" then n
                            elif n.StartsWith "structVk" then n.Substring("struct".Length)
                            elif n.StartsWith "Mir" || n.StartsWith "struct" then "nativeint"
                            else "nativeint" //failwithf "strange type: %A" n
            | Ptr t ->
                sprintf "nativeptr<%s>" (typeName t)
            | FixedArray(t, s) ->
                let t = typeName t
                sprintf "%s_%d" t s
            | FixedArray2d(t, w, h) ->
                let t = typeName t
                sprintf "%s_%d" t (w * h)

    let enumExtensions (indent : string) (vendorTags : list<string>) (exts : Map<string, list<EnumCase>>) =

        let printfn fmt =
            Printf.kprintf (fun str ->
                if str = "" then printfn "" else printfn "%s%s" indent str
            ) fmt

        let vendorTagsCamel =
            vendorTags |> List.map (capsToCamelCase [] "")

        if exts.Count > 0 then
            printfn "[<AutoOpen>]"
            printfn "module EnumExtensions ="

            for (name, values) in Map.toSeq exts do

                let name = cleanEnumName name
                let baseName = baseEnumName vendorTags name

                let exts = values |> List.map (fun c ->
                    let camelCase = capsToCamelCase ["Vk"; baseName] "" c.name

                    // Not sure if we should remove all the extension suffixes from enum extension values...
                    //let withoutExt = baseEnumName vendorTagsCamel camelCase

                    // For now just remove the last one, if it matches the extension of the enum type itself
                    // E.g. VkDebugReportObjectTypeEXT : AccelerationStructureKhrExt
                    // becomes VkDebugReportObjectTypeEXT : AccelerationStructureKhr
                    let enumSuff =
                        name
                        |> findEnumVendorSuffix vendorTags
                        |> Option.toList
                        |> List.map (capsToCamelCase [] "")

                    { c with name = baseEnumName enumSuff camelCase }
                )

                printfn "     type %s with" name
                for c in exts do
                    match c.comment with
                    | Some comment -> printfn "          /// %s" comment
                    | _ -> ()
                    printfn "          static member inline %s = unbox<%s> %s" c.name name (Enum.valueToStr c.value)

            printfn ""

    let enums (indent : string) (vendorTags : list<string>) (location : Location) (enums : list<Enum>) =

        let printfn fmt =
            Printf.kprintf (fun str ->
                if str = "" then printfn "" else printfn "%s%s" indent str
            ) fmt

        let vendorTagsCamel =
            vendorTags |> List.map (capsToCamelCase [] "")

        for e in enums do
            let name = cleanEnumName e.name

            if name <> "VkStructureType" then
                match tryGetTypeAlias location name with
                | None ->
                    let baseName = baseEnumName vendorTags name

                    let cases = e.cases |> List.map (fun c ->
                        let camelCase = capsToCamelCase ["Vk"; baseName] "" c.name
                        let withoutExt = baseEnumName vendorTagsCamel camelCase

                        { c with name = withoutExt }
                    )

                    let cases =
                        if e.bitmask then cases |> addNoneCase |> addAllCase
                        else cases

                    let cases =
                        let byName = cases |> List.groupBy (fun c -> c.name)
                        for (n, c) in byName do

                            let allEqual =
                                match c with
                                | [] -> true
                                | x::_ -> c |> List.forall (fun c -> c.value = x.value)

                            if not allEqual then
                                failwithf "Enum %s has multiple cases with name %s but different values!" name n

                        cases |> List.distinctBy (fun c -> c.name)

                    if e.bitmask then
                        printfn "[<Flags>]"
                    printfn "type %s =" name

                    for c in cases do
                        match c.comment with
                        | Some comment -> printfn "    /// %s" comment
                        | _ -> ()
                        printfn  "    | %s = %s"  c.name (Enum.valueToStr c.value)
                    printfn ""

                    if name = "VkQueueGlobalPriorityKHR" then
                        printfn ""
                        inlineArray "    " (Extension("VK_KHR_global_priority", RequiredBy.Core VkVersion13)) "VkQueueGlobalPriorityKHR" 4 16



                | Some alias -> 
                    if name <> alias then
                        printfn "type %s = %s" name alias

            match vulkanTypeArrays |> Map.tryFind name with
            | Some arr -> inlineArray indent location name arr.baseTypeSize arr.count
            | None -> ()

        if enums.Length > 0 then
            printfn ""

    let funcpointers (indent : string) (ptrs : list<FuncPointer>) =
        for p in ptrs do
            printfn "%stype %s = nativeint" indent p.name

        if List.length ptrs > 0 then
            printfn ""

    let handles (indent : string) (location : Location) (l : list<Handle>) =
        let printfn fmt =
            Printf.kprintf (fun str ->
                if str = "" then printfn "" else printfn "%s%s" indent str
            ) fmt

        for h in l do
            if h.nonDispatchable then
                match tryGetTypeAlias location h.name with
                | None ->
                    printfn ""
                    printfn "[<StructLayout(LayoutKind.Sequential)>]"
                    printfn "type %s =" h.name
                    printfn "    struct"
                    printfn "        val mutable public Handle : uint64"
                    printfn "        new(h) = { Handle = h }"
                    printfn "        static member Null = %s(0UL)" h.name
                    printfn "        member x.IsNull = x.Handle = 0UL"
                    printfn "        member x.IsValid = x.Handle <> 0UL"
                    printfn "    end"
                | Some alias ->
                    if h.name <> alias then
                        printfn "type %s = %s" h.name alias
            else
                printfn "type %s = nativeint" h.name

        for h in l do
            match vulkanTypeArrays |> Map.tryFind h.name with
            | Some arr ->
                printfn ""
                inlineArray indent location arr.baseType arr.baseTypeSize arr.count
            | _ -> ()

        if List.length l > 0 then
            printfn ""

    let typedefs (indent : string) (location : Location) (l : list<Typedef>) =
        for x in l do
            if x.name <> typeName x.baseType then
                printfn "%stype %s = %s" indent x.name (typeName x.baseType)

        for t in l do
            match vulkanTypeArrays |> Map.tryFind t.name with
            | Some arr ->
                printfn ""
                inlineArray indent location t.name arr.baseTypeSize arr.count
            | None -> ()

        if List.length l > 0 then
            printfn ""

    let aliases (indent : string) (location : Location) (aliases : list<Alias>) =
        for a in aliases do
            if a.name <> a.baseSym then
                printfn "%stype %s = %s" indent a.name a.baseSym

        for a in aliases do
            match vulkanTypeArrays |> Map.tryFind a.name with
            | Some arr ->
                printfn ""
                inlineArray indent location a.name arr.baseTypeSize arr.count
            | None -> ()

        if List.length aliases > 0 then
            printfn ""

    let structs (indent : string) (structureTypes : Map<string, string>) (location : Location) (structs : list<Struct>) =

        let printfn fmt =
            Printf.kprintf (fun str ->
                if str = "" then printfn "" else printfn "%s%s" indent str
            ) fmt

        let printfn' (tabs : int) fmt =
            let indent = String.replicate tabs "    "

            Printf.kprintf (fun str ->
                if str = "" then printfn "" else printfn "%s%s" indent str
            ) fmt

        let toInlineFunction (tabs : int) (separator : string) (opening : string) (closing : string) (values : string list) =
            let sb = System.Text.StringBuilder()
            sb.Append(opening) |> ignore

            values |> List.iteri(fun i v ->
                sb.Append v |> ignore

                if i < values.Length - 1 then
                    sb.Append separator |> ignore
            )

            sb.Append(closing) |> ignore
            printfn' tabs "%A" sb

        let toFunction (tabs : int) (separator : string) (opening : string) (closing : string) (values : string list) =
            match values with
            | f::fs ->
                printfn' tabs "%s" opening
                printfn' (tabs + 1) "%s" f

                for f in fs do
                    printfn' (tabs + 1) "%s%s" separator f

                printfn' tabs "%s" closing

            | [] ->
                printfn' tabs "%s%s" opening closing

        let toFunctionCall (indent : int) (functionName : string) (values : string list) =
            values |> toInlineFunction indent ", " (sprintf "%s(" functionName) ")"

        let toFunctionDecl (indent : int) (functionName : string) (fields : StructField list) =
            fields |> List.map (fun f ->
                sprintf "%s : %s" (fsharpName f.name) (typeName f.typ)
            )
            |> toInlineFunction indent ", " (sprintf "%s(" functionName) ") ="

        let toConstructorBody (indent : int) (assignments : List<string * string>) =
            assignments |> List.map (fun (n, v) ->
                sprintf "%s = %s" n v
            )
            |> toFunction indent "" "{" "}"

        for s in structs do
            match s.comment with
            | Some comment -> printfn "/// %s" comment
            | _ -> ()

            match s.alias, tryGetTypeAlias location s.name with
            | _, Some alias ->
                if s.name <> alias then
                    printfn "type %s = %s" s.name alias
                    printfn ""
            | Some alias, _ ->
                if s.name <> alias then
                    printfn "type %s = %s" s.name alias
                    printfn ""
            | None, None ->

                if s.isUnion then printfn "[<StructLayout(LayoutKind.Explicit)>]"
                else printfn "[<StructLayout(LayoutKind.Sequential)>]"


                printfn "type %s =" s.name
                printfn' 1 "struct"
                for f in s.fields do
                    let n = fsharpName f.name

                    if s.isUnion then
                        printfn' 2 "[<FieldOffset(0)>]"

                    printfn' 2 "val mutable public %s : %s" n (typeName f.typ)
                    ()

                // Set the sType field automatically
                let fields =
                    match s.name with
                    | "VkBaseInStructure"
                    | "VkBaseOutStructure" -> s.fields
                    | _ -> s.fields |> List.filter (fun f -> f.name <> "sType")

                let hasTypeField =
                    s.fields.Length <> fields.Length

                let sType =
                    let value =
                        s.fields
                        |> List.tryFind (fun f -> f.name = "sType")
                        |> Option.bind (fun f ->
                            match f.values with
                            | Some v -> structureTypes |> Map.tryFind v
                            | None -> None
                        )

                    match value with
                    | Some v -> sprintf "%su" v
                    | None -> @"failwith ""Reserved for future use or possibly a bug in the generator"""

                let isNextPtr (f : StructField) =
                    f.name = "pNext" && f.typ = Ptr (Literal "void")

                let nextPtrIndex = fields |> List.tryFindIndex isNextPtr

                // Proper default constructors are not allowed for structs...
                let defaultConstructor() =
                    printfn ""

                    let values =
                        fields |> List.map(fun f ->
                            sprintf "Unchecked.defaultof<%s>" (typeName f.typ)
                        )

                    printfn' 2 "static member Empty ="
                    values |> toFunctionCall 3 s.name

                let isEmptyMember() =
                    printfn ""

                    let checks =
                        fields |> List.map (fun f ->
                            sprintf "x.%s = Unchecked.defaultof<%s>" (fsharpName f.name) (typeName f.typ)
                        )

                    printfn' 2 "member x.IsEmpty ="
                    printfn' 3 "%s" (checks |> String.concat " && ")

                // Constructor with all fields
                let constructorWithAllFields (isPrivate : bool) =
                    printfn ""

                    let name = if isPrivate then "private new" else "new"
                    fields |> toFunctionDecl 2 name

                    let assignments = [
                        if hasTypeField then
                            yield "sType", sType

                        yield! fields |> List.map (fun f -> (fsharpName f.name), (fsharpName f.name))
                    ]

                    assignments |> toConstructorBody 3

                // Convenience constructor without pNext parameter
                let constructorWithoutNextPtr () =
                    match nextPtrIndex with
                    | Some index when fields.Length > 1 ->
                        printfn ""

                        fields
                        |> List.filter (isNextPtr >> not)
                        |> toFunctionDecl 2 "new"

                        fields
                        |> List.mapi (fun i f ->
                            if i = index then
                                sprintf "Unchecked.defaultof<%s>" (typeName f.typ)
                            else
                                fsharpName f.name
                        )
                        |> toFunctionCall 3 s.name

                    | _ ->
                        ()

                if s.isUnion then
                    // Static member constructors for each union case
                    for f in fields do
                        printfn ""

                        let arg = { f with name = "value" }
                        let name = sprintf "static member %s" (f.name.Substring(0, 1).ToUpper() + f.name.Substring(1))
                        [arg] |> toFunctionDecl 2 name
                        printfn' 3 "let mutable result = Unchecked.defaultof<%s>" s.name
                        printfn' 3 "result.%s <- value" (fsharpName f.name)
                        printfn' 3 "result"


                else
                    // Constructor with all fields
                    constructorWithAllFields false

                    // Constructor without pNext
                    constructorWithoutNextPtr()

                    // IsEmpty member
                    isEmptyMember()

                    // Empty default "constructor"
                    defaultConstructor()

                    if s.name = "VkExtent3D" then
                        printfn ""
                        printfn' 2 "new(w : int, h : int, d : int) = VkExtent3D(uint32 w, uint32 h, uint32 d)"
                    elif s.name = "VkExtent2D" then
                        printfn ""
                        printfn' 2 "new(w : int, h : int) = VkExtent2D(uint32 w, uint32 h)"


                let fieldSplice = s.fields |> List.map (fun f -> sprintf "%s = %%A" (fsharpName f.name))
                let fieldAccess = s.fields |> List.map (fun f -> sprintf "x.%s" (fsharpName f.name))

                printfn ""
                printfn' 2 "override x.ToString() ="
                printfn' 3 "String.concat \"; \" ["

                for (s,a) in List.zip fieldSplice fieldAccess do
                    printfn' 4 "sprintf \"%s\" %s" s a

                printfn' 3 "] |> sprintf \"%s { %%s }\"" s.name

                printfn' 1 "end"
                printfn ""

                match vulkanTypeArrays |> Map.tryFind s.name with
                | Some arr -> inlineArray indent location s.name arr.baseTypeSize arr.count
                | _ -> ()

        if structs.Length > 0 then
            printfn ""

    let primitiveArrays() =
        printfn ""
        inlineArray "" (Global VkVersion10) "uint32" 4 32

        printfn ""
        inlineArray "" (Global VkVersion10) "byte" 1 32

        printfn ""
        inlineArray "" (Global VkVersion10) "byte" 1 8

    let rec externTypeName (n : Type) =
        match n with
        | FixedArray(Literal "int32_t", 2) -> "V2i"
        | FixedArray(Literal "int32_t", 3) -> "V3i"
        | FixedArray(Literal "int32_t", 4) -> "V4i"
        | FixedArray(Literal "uint32_t", 2) -> "V2ui"
        | FixedArray(Literal "uint32_t", 3) -> "V3ui"
        | FixedArray(Literal "uint32_t", 4) -> "V4ui"
        | FixedArray(Literal "float", 2) -> "V2f"
        | FixedArray(Literal "float", 3) -> "V3f"
        | FixedArray(Literal "float", 4) -> "V4f"
        | FixedArray(Literal "uint8_t", 16) -> "Guid"

        | BitField(l, s) ->
            match typeName l, s with
            | "int32", 8 -> "int8"
            | "uint32", 8 -> "uint8"
            | "uint32", 24 -> "uint24"
            | t, 8 -> System.Console.WriteLine("WARNING: Replacing {0}:8 with uint8", t); "uint8"
            | t, 24 -> System.Console.WriteLine("WARNING: Replacing {0}:24 with uint24", t); "uint24"
            | _ -> failwithf "unsupported bit field type %A:%A" l s

        | Ptr(Literal "char") -> "string"
        | FixedArray(Literal "char", s) -> sprintf "String%d" s
        | Ptr(Literal "void") -> "nativeint"

        | Literal n ->
            if n.EndsWith "FlagBits" then
                n.Substring(0, n.Length - 8) + "Flags"
            else
                match Map.tryFind n knownTypes with
                | Some n -> n
                | None ->
                    if n.StartsWith "Vk" || n.StartsWith "PFN" then n
                    elif n.StartsWith "Mir" || n.StartsWith "struct" then "nativeint"
                    else "nativeint" //failwithf "strange type: %A" n
        | Ptr (Ptr t) -> "nativeint*"
        | Ptr t ->
            sprintf "%s*" (externTypeName t)
        | FixedArray(t, s) ->
            let t = externTypeName t
            sprintf "%s_%d" t s
        | FixedArray2d(t, w, h) ->
            let t = externTypeName t
            sprintf "%s_%d" t (w * h)

    let coreCommands (indent : string) (location : Location) (l : list<Command>) =
        let printfn fmt =
            Printf.kprintf (fun str ->
                if str = "" then printfn "" else printfn "%s%s" indent str
            ) fmt

        printfn "module VkRaw ="

        let isVersion10 =
            (location = Global VkVersion10)

        if isVersion10 then
            printfn "    [<CompilerMessage(\"activeInstance is for internal use only\", 1337, IsError=false, IsHidden=true)>]"
            printfn "    let mutable internal activeInstance : VkInstance = 0n"
            printfn ""
            printfn "    [<Literal>]"
            printfn "    let lib = \"vulkan-1\""
        else
            printfn "    open VkRaw"
        printfn ""

        for c in l do
            if c.name = "vkCreateInstance" then
                let args = c.parameters |> List.map (fun (t,n) -> sprintf "%s %s" (externTypeName t) (fsharpName n)) |> String.concat ", "
                printfn "    [<DllImport(lib, EntryPoint=\"%s\"); SuppressUnmanagedCodeSecurity>]" c.name
                printfn "    extern %s private _%s(%s)" (externTypeName c.returnType) c.name args



                let argDef = c.parameters |> List.map (fun (t,n) -> sprintf "%s : %s" (fsharpName n) (typeName t)) |> String.concat ", "
                let argUse = c.parameters |> List.map (fun (t,n) -> fsharpName n) |> String.concat ", "
                let instanceArgName = c.parameters |> List.pick (fun (t,n) -> match t with | Ptr(Literal "VkInstance") -> Some n | _ -> None)

                printfn "    let vkCreateInstance(%s) =" argDef
                printfn "        let res = _vkCreateInstance(%s)" argUse
                printfn "        if res = VkResult.Success then"
                printfn "            activeInstance <- NativePtr.read %s" instanceArgName
                printfn "        res"
                printfn ""
            else
                match tryGetCommandAlias location c.name with
                | None ->
                    let args = c.parameters |> List.map (fun (t,n) -> sprintf "%s %s" (externTypeName t) (fsharpName n)) |> String.concat ", "
                    printfn "    [<DllImport(lib); SuppressUnmanagedCodeSecurity>]"
                    printfn "    extern %s %s(%s)" (externTypeName c.returnType) c.name args
                | Some alias ->
                    printfn "    let %s = %s" c.name alias

                printfn ""

        if isVersion10 then
            printfn "    [<CompilerMessage(\"vkImportInstanceDelegate is for internal use only\", 1337, IsError=false, IsHidden=true)>]"
            printfn "    let vkImportInstanceDelegate<'a>(name : string) = "
            printfn "        let ptr = vkGetInstanceProcAddr(activeInstance, name)"
            printfn "        if ptr = 0n then"
            printfn "            Log.warn \"could not load function: %%s\" name"
            printfn "            Unchecked.defaultof<'a>"
            printfn "        else"
            printfn "            Report.Line(3, sprintf \"loaded function %%s (0x%%08X)\" name ptr)"
            printfn "            Marshal.GetDelegateForFunctionPointer(ptr, typeof<'a>) |> unbox<'a>"

    let extensionCommands (indent : string) (location : Location) (l : list<Command>) =
        let printfn fmt =
            Printf.kprintf (fun str ->
                if str = "" then printfn "" else printfn "%s%s" indent str
            ) fmt

        let extension =
            match location with
            | Extension(name, RequiredBy.Core _) -> name
            | Extension(name, RequiredBy.Extension ext) -> sprintf "%s -> %s" name ext
            | _ -> ""

        let exists name = definitionLocations.ContainsKey(name)
        let existAll = l |> List.map (fun c -> exists c.name) |> List.forall id

        printfn "module VkRaw ="
        for c in l do
            if not (exists c.name) then
                let delegateName = c.name.Substring(0, 1).ToUpper() + c.name.Substring(1) + "Del"
                let targs = c.parameters |> List.map (fun (t,n) -> (typeName t)) |> String.concat " * "
                let ret =
                    match typeName c.returnType with
                    | "void" -> "unit"
                    | n -> n

                let tDel = sprintf "%s -> %s" targs ret
                printfn "    [<SuppressUnmanagedCodeSecurity>]"
                printfn "    type %s = delegate of %s" delegateName tDel

        if not existAll then
            printfn ""
            printfn "    [<AbstractClass; Sealed>]"
            printfn "    type private Loader<'d> private() ="
            printfn "        static do Report.Begin(3, \"[Vulkan] loading %s\")" extension

            for c in l do
                if not (exists c.name) then
                    let delegateName = c.name.Substring(0, 1).ToUpper() + c.name.Substring(1) + "Del"
                    printfn "        static let s_%sDel = VkRaw.vkImportInstanceDelegate<%s> \"%s\"" c.name delegateName c.name

            printfn "        static do Report.End(3) |> ignore"

            for c in l do
                if not (exists c.name) then
                    printfn "        static member %s = s_%sDel" c.name c.name

        for c in l do
            match tryGetCommandAlias location c.name with
            | None ->
                let argDef = c.parameters |> List.map (fun (t,n) -> sprintf "%s : %s" (fsharpName n) (typeName t)) |> String.concat ", "
                let argUse = c.parameters |> List.map (fun (_,n) -> (fsharpName n)) |> String.concat ", "
                printfn "    let %s(%s) = Loader<unit>.%s.Invoke(%s)" c.name argDef c.name argUse
            | Some alias ->
                printfn "    let %s = %s" c.name alias

    let camelCase (str : string) =
        let parts = System.Collections.Generic.List<string>()

        let mutable str = str
        let mutable fi = str.IndexOf '_'
        while fi >= 0 do
            parts.Add (str.Substring(0, fi))
            str <- str.Substring(fi + 1)
            fi <- str.IndexOf '_'

        if str.Length > 0 then
            parts.Add str
        let parts = Seq.toList parts

        parts |> List.map (fun p -> p.Substring(0,1).ToUpper() + p.Substring(1).ToLower()) |> String.concat ""

    let extCamelCase (str : string) =
        let regex = Text.RegularExpressions.Regex @"^VK_(?<kind>[A-Z]+)_(?<name>.*)$"
        let m = regex.Match str
        let kind = m.Groups.["kind"].Value
        let name = m.Groups.["name"].Value
        sprintf "%s%s" kind (camelCase name)

    let require (indent : int) (vendorTags : list<string>) (structureTypes : Map<string, string>) (parent : RequiredBy) (require : Require) =

        let name =
            match require.requiredBy with
            | RequiredBy.Core v -> VkVersion.toModuleName v
            | RequiredBy.Extension ext -> Some (extCamelCase ext)

        let location =
            match parent, require.requiredBy with
            | RequiredBy.Core v, _ -> Global v
            | RequiredBy.Extension name, RequiredBy.Core v -> Extension(extCamelCase name, RequiredBy.Core v)
            | RequiredBy.Extension name, RequiredBy.Extension ext -> Extension(extCamelCase name, RequiredBy.Extension <| extCamelCase ext)

        let subindent n = String.replicate (if name.IsSome then indent + n + 1 else indent + n) "    "
        let indent = String.replicate indent "    "

        let printfn fmt =
            Printf.kprintf (fun str ->
                if str = "" then printfn "" else printfn "%s%s" indent str
            ) fmt

        match name with
        | Some name ->
            if RequiredBy.autoOpen require.requiredBy then
                printfn "[<AutoOpen>]"
            printfn "module %s =" name
        | _ ->
            ()

        funcpointers (subindent 0) require.funcpointers
        handles (subindent 0) location require.handles
        typedefs (subindent 0) location require.typedefs
        aliases (subindent 0) location require.aliases
        enums (subindent 0) vendorTags location require.enums
        structs (subindent 0) structureTypes location (Struct.topologicalSort require.structs)
        enumExtensions (subindent 0) vendorTags require.enumExtensions

        if not require.commands.IsEmpty then
            if RequiredBy.isCore parent then
                coreCommands (subindent 0) location require.commands
            else
                extensionCommands (subindent 0) location require.commands

        printfn ""

    let feature (vendorTags : list<string>) (structureTypes : Map<string, string>) (feature : Feature) =

        let name = VkVersion.toModuleName feature.version
        let indent = if name.IsSome then 1 else 0

        match name with
        | Some name ->
            if VkVersion.autoOpen feature.version then
                printfn "[<AutoOpen>]"

            printfn "module %s =" name

            for v in VkVersion.getPriorModules feature.version do
                printfn "    open %s" v

            printfn ""
        | _ ->
            ()

        feature.requires |> Require.unionMany |> require indent vendorTags structureTypes (RequiredBy.Core feature.version)

    let features (vendorTags : list<string>) (structureTypes : Map<string, string>) (features : Feature list) =
        for f in features do
            f |> feature vendorTags structureTypes
            printfn ""

    let extension (vendorTags : list<string>) (structureTypes : Map<string, string>) (e : Extension) =

        let name = extCamelCase e.name

        if String.IsNullOrEmpty(name) then
            Console.WriteLine("WARNING: Ignoring extension '{0}'", e.name)
        else
            printfn "module %s =" name
            
            // Extensions make use of types defined by extended core versions
            // but do not declare it for some reason. Therefore we have to open all core
            // modules.
            for v in VkVersion.allModules do
                printfn "    open %s" v

            for r in e.references do
                printfn "    open %s" <| extCamelCase r

            printfn "    let Name = \"%s\"" e.name
            printfn "    let Number = %d" e.number
            printfn ""

            if not (Set.isEmpty e.dependencies) then
                let exts = e.dependencies |> Set.map (extCamelCase >> sprintf "%s.Name") |> String.concat "; " |> sprintf "[ %s ]"
                printfn "    let Required = %s" exts
                printfn ""

            printfn ""

            // Group requires by requiredBy property
            // Not sure if this is necessary (i.e. if there can be multiple
            // requires with the same requiredBy, for feature tags it's possible at least)
            let requires =
                (([], []), e.requires)
                    ||> List.fold (fun (result, current) r ->
                        match current with
                        | [] -> result, [r]
                        | x::_ ->
                            if x.requiredBy = r.requiredBy then
                                result, current @ [r]
                            else
                                result @ [Require.unionMany current], [r]
                    )
                    ||> List.append

            for r in requires do
                r |> require 1 vendorTags structureTypes (RequiredBy.Extension e.name)

    let topoExtensions (extensions : list<Extension>) : list<Extension> =
        let typeMap = extensions |> List.map (fun s -> s.name, s) |> Map.ofList
        let graph =
            extensions |> List.map (fun s ->
                    let dependencies =
                        s.dependencies |> Set.toList |> List.choose (fun m -> Map.tryFind m typeMap)

                    let requires =
                        s.requires |> List.choose (fun r ->
                            match r.requiredBy with
                            | RequiredBy.Core _ -> None
                            | RequiredBy.Extension ext -> Map.tryFind ext typeMap
                        )

                    let usedTypes =
                        List.concat [dependencies; requires]
                        |> List.distinctBy (fun e -> e.name)

                    s, usedTypes

                    )
                |> Map.ofList

        Struct.toposort graph |> List.rev

    let extensions (vendorTags : list<string>) (structureTypes : Map<string, string>) (exts : Extension list) =

        // Transitive references
        let directRefs =
            let explicit = exts |> List.map (fun e -> e.name, e.dependencies) |> Map.ofList

            (explicit, missingExtensionReferences) ||> Map.fold (fun map name deps ->
                let cur = map |> Map.tryFind name |> Option.defaultValue Set.empty
                map |> Map.add name (Set.union cur deps)
            )

        let rec traverse (name : string) =
            match Map.tryFind name directRefs with
            | Some others ->
                let children = others |> Seq.map traverse |> Seq.concat |> Set.ofSeq
                others |> Set.union children
            | None ->
                Set.ofList [ name ]

        let exts =
            let refs = directRefs |> Map.map (fun name _ -> traverse name |> Set.remove name)

            exts |> List.map (fun e ->
                { e with references = refs |> Map.find e.name }
            )

        for e in topoExtensions exts do
            extension vendorTags structureTypes e

let run () =
    let path = Path.Combine(__SOURCE_DIRECTORY__, "vk.xml")
    let vk = XElement.Load(path)
    if vk.Name <> xname "registry" then
        Console.WriteLine("WARNING: Root element is not 'registry'")

    let vendorTags = XmlReader.vendorTags vk
    let defines = XmlReader.defines vk
    let structureTypes = XmlReader.structureTypes vk
    let enums = XmlReader.enums vk
    let emptyBitfields = XmlReader.emptyBitfields vk |> List.filter (fun n -> not (Map.containsKey (Enum.cleanName n) enums))

    let aliases = XmlReader.aliases vk
    let mutable defs = XmlReader.typedefs defines vk

    for KeyValue(k,a) in aliases do
        match Map.tryFind k defs with
        | None ->   
            let def = { name = a.name; baseType = Literal a.baseSym }
            defs <- Map.add k def defs
        | _ ->
            ()

    let definitions =
        {
            enums = enums
            aliases = Map.empty
            structs = XmlReader.structs defines vk
            commands = XmlReader.commands defines vk
            funcpointers = XmlReader.funcpointers vk
            handles = XmlReader.handles vk
            typedefs = defs
        }


    let features = XmlReader.features definitions vk
    let exts = XmlReader.extensions definitions vk

    FSharpWriter.header()
    FSharpWriter.apiConstants defines
    FSharpWriter.emptyBitfields emptyBitfields
    FSharpWriter.primitiveArrays()
    FSharpWriter.features vendorTags structureTypes features
    FSharpWriter.extensions vendorTags structureTypes exts

    let str = FSharpWriter.builder.ToString()
    FSharpWriter.builder.Clear() |> ignore

    let file = Path.Combine(__SOURCE_DIRECTORY__, "Vulkan.fs")

    File.WriteAllText(file, str)
    printfn "Generated 'Vulkan.fs' successfully!"

module PCI =
    open System
    open System.IO
    let builder = System.Text.StringBuilder()

    let printfn fmt =
        Printf.kprintf (fun str -> builder.AppendLine(str) |> ignore) fmt


    let writeVendorAndDeviceEnum() =

        let rx = System.Text.RegularExpressions.Regex "\"0x(?<vendor>[0-9A-Fa-f]+)\",\"0x(?<device>[0-9A-Fa-f]+)\",\"(?<vendorName>[^\"]+)\",\"(?<deviceName>[^\"]+)\""

        let req = System.Net.HttpWebRequest.Create("http://pcidatabase.com/reports.php?type=csv")
        let response = req.GetResponse()
        let reader = new System.IO.StreamReader(response.GetResponseStream())

        let vendors = System.Collections.Generic.Dictionary<int64, string>()
        let devices = System.Collections.Generic.Dictionary<int64, string>()

        let mutable line = reader.ReadLine()

        while not (isNull line) do
            let m = rx.Match line

            if m.Success then
                let vid = System.Int64.Parse(m.Groups.["vendor"].Value, System.Globalization.NumberStyles.HexNumber)
                let did = System.Int64.Parse(m.Groups.["device"].Value, System.Globalization.NumberStyles.HexNumber)
                let vname = m.Groups.["vendorName"].Value
                let dname = m.Groups.["deviceName"].Value

                vendors.[vid] <- vname.Replace("\\", "\\\\")
                devices.[did] <- dname.Replace("\\", "\\\\")

            line <- reader.ReadLine()

        printfn "namespace Aardvark.Rendering.Vulkan"
        printfn "open System.Collections.Generic"
        printfn "open Aardvark.Base"


        printfn "module PCI = "
        printfn "    let vendors ="
        printfn "        Dictionary.ofArray [|"
        for (KeyValue(k,v)) in vendors do
            if k <= int64 Int32.MaxValue then
                printfn "            0x%08X, \"%s\"" k v
        printfn "        |]"

//        printfn "    let devices ="
//        printfn "        Dictionary.ofArray [|"
//        for (KeyValue(k,v)) in devices do
//            if k <= int64 Int32.MaxValue then
//                printfn "            0x%08X, \"%s\"" k v
//        printfn "        |]"


        printfn "    let vendorName (id : int) ="
        printfn "        match vendors.TryGetValue id with"
        printfn "            | (true, name) -> name"
        printfn "            | _ -> \"Unknown\""

//        printfn "    let deviceName (id : int) ="
//        printfn "        match devices.TryGetValue id with"
//        printfn "            | (true, name) -> name"
//        printfn "            | _ -> \"Unknown\""

    let run() =
        builder.Clear() |> ignore
        writeVendorAndDeviceEnum()
        let str = builder.ToString()
        builder.Clear() |> ignore
        let file = Path.Combine(__SOURCE_DIRECTORY__, "PCI.fs")
        File.WriteAllText(file, str)

do run()