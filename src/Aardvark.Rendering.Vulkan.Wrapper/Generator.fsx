#r "System.Xml.dll"
#r "System.Xml.dll"
#r "System.Xml.Linq.dll"

open System.Xml.Linq
open System
open System.IO
open System.Text.RegularExpressions

[<AutoOpen>]
module StringUtilities =

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

        member x.ToInt32 =
            match x with
            | Int32 v -> Some v
            | Int64 v -> Some <| int32 v
            | UInt32 v -> Some <| int32 v
            | UInt64 v -> Some <| int32 v
            | Float32 _ -> None

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

    let rec toDefine (t : string) (v : string) : Numeric =
        let v =
            if v.StartsWith "(" && v.EndsWith ")" then
                v.Substring(1, v.Length-2) |> toDefine t
            elif v.Contains("-") then
                let values = v.Split('-') |> Array.map (toNumeric 10)
                values |> Array.reduce (-)
            else
                v |> toNumeric 10

        match v, t with
        | Int32 _, "int32_t"
        | Int64 _, "int64_t"
        | UInt32 _, "uint32_t"
        | UInt64 _, "uint64_t"
        | Float32 _, "float" -> v

        | Int32 x, "uint32_t" -> UInt32 <| uint32 x
        | Int64 x, "uint64_t" -> UInt64 <| uint64 x
        | UInt32 x, "int32_t" -> Int32 <| int32 x
        | UInt64 x, "int64_t" -> Int64 <| int64 x

        | _ ->
            printfn $"WARNING: Unknown data type for define: {t}"
            v

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
                let deprecated =
                    match attrib e "deprecated" with
                    | Some "true" | Some "ignored" | Some "aliased" -> true
                    | _ -> false

                if deprecated then
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

    let private isVulkanApi e =
        match attrib e "api" with
        | Some api -> api.Split(',') |> Array.contains "vulkan"
        | _ -> true

    type XContainer with
        member x.Elements(name: string) = x.Elements(xname name) |> Seq.filter isVulkanApi
        member x.Descendants(name: string) = x.Descendants(xname name) |> Seq.filter isVulkanApi

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

    let private (|Suffix|_|) (suffix: string) (str: string) =
        if str.EndsWith suffix then Some ()
        else None

    let isEnum = function
        | Literal (Suffix "Flags")
        | Literal (Suffix "FlagsKHR")
        | Literal (Suffix "FlagsEXT")
        | Literal (Suffix "FlagsNV") -> true
        | _ -> false

    let literalSuffix = function
        | Literal "int8_t"   -> "y"
        | Literal "uint8_t"  -> "uy"
        | Literal "int16_t"  -> "s"
        | Literal "uint16_t" -> "us"
        | Literal "int32_t"  -> ""
        | Literal "uint32_t" -> "u"
        | Literal "int"      -> ""
        | Literal "float"    -> "f"
        | Literal "double"   -> ""
        | Literal "int64_t"  -> "L"
        | Literal "uint64_t" -> "UL"
        | Literal "size_t"   -> "UL"
        | t -> failwith $"Unknown literal suffix for {t}"

    let rec sizeInBits = function
        | Literal "int8_t"            -> 8
        | Literal "uint8_t"           -> 8
        | Literal "char"              -> 8
        | Literal "int16_t"           -> 16
        | Literal "uint16_t"          -> 16
        | Literal "uint32_t"          -> 32
        | Literal "int32_t"           -> 32
        | Literal "int"               -> 32
        | Literal "float"             -> 32
        | Literal (Suffix "Flags")    -> 32
        | Literal (Suffix "FlagsKHR") -> 32
        | Literal (Suffix "FlagsEXT") -> 32
        | Literal (Suffix "FlagsNV")  -> 32
        | Literal "double"            -> 64
        | Literal "int64_t"           -> 64
        | Literal "uint64_t"          -> 64
        | Literal "size_t"            -> 64
        | t ->
            failwith $"Cannot determine bit size of type {t}"

    let private tryMatch (regex : Regex) (str : string) =
        let ret = regex.Match str
        if ret.Success then Some ret else None

    let arraySize (defines : Map<string, Numeric>) (s : string) =
        match System.Int32.TryParse s with
        | (true, s) -> s
        | _ ->
            match Map.tryFind s defines with
            | Some v ->
                match v.ToInt32 with
                | Some v -> v
                | _ -> failwith "non-integer array-size"
            | _ -> failwithf "non-literal array-size: %A" s

    let arraySize2d (defines : Map<string, Numeric>) (width : string) (height : string) =
        let w = arraySize defines width
        let h = arraySize defines height
        w, h

    let private cleanName (name : string) =
        name.Trim().Replace("FlagBits", "Flags")

    let parseTypeAndName (defined : Map<string, Numeric>) (strangeType : string) (strangeName : string) =
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

    let readXmlTypeAndName (defined : Map<string, Numeric>) (e : XElement) =
        let strangeName = e.Element(xname "name").Value
        e.Elements(xname "comment").Remove()
        let strangeType =
            let v = e.Value.Replace("typedef", "").Trim()
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
                e.Descendants("enum")
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

type Field = {
    typ : Type
    name : string
    values : string option
}

type BitFieldBits =
    {
        typ : Type
        name : string
        offset : int
        count : int
    }

type BitField =
    {
        typ : Type
        name : string
        bits : BitFieldBits list
    }

[<RequireQualifiedAccess>]
type StructField =
    | Field of Field
    | BitField of BitField

    member x.Type =
        match x with
        | Field f -> f.typ
        | BitField f -> f.typ

    member x.Name =
        match x with
        | Field f -> f.name
        | BitField f -> f.name

type Struct = {
    name : string
    fields : list<StructField>
    isUnion : bool
    alias : Option<string>
    comment : Option<string>
}

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Struct =
    let tryRead (defines : Map<string, Numeric>) (e : XElement) =
        let isUnion = attrib e "category" = Some "union"
        let comment = Comment.tryRead e
        match attrib e "name" with
        | Some name ->
            match attrib e "alias" with
            | Some alias ->
                Some { name = name; fields = []; isUnion = isUnion; alias = Some alias; comment = comment }
            | None ->
                let fields =
                    e.Descendants ("member")
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

                let combined, bitfield, _ =
                    (([], None, 0), fields) ||> Seq.fold (fun (result, bitfield, index) f ->
                        match f.typ with
                        | BitField (baseType, bitCount) ->
                            match bitfield with
                            | Some bf ->
                                let totalSize = Type.sizeInBits baseType

                                if Type.sizeInBits bf.typ <> totalSize then
                                    failwith $"Mismatching bitfield type {baseType} for {name}::{f.name} (Expected {bf.typ})"
                                else
                                    let currentSize =
                                        match bf.bits with
                                        | f::_ -> f.offset + f.count
                                        | _ -> 0

                                    let newSize = currentSize + bitCount
                                    let bits = { typ = baseType; name = f.name; offset = currentSize; count = bitCount} :: bf.bits

                                    if newSize < totalSize then
                                        let bf = { bf with bits = bits }
                                        result, Some bf, index

                                    elif newSize = totalSize then
                                        let bf = { bf with bits = List.rev bits }
                                        (StructField.BitField bf)::result, None, index

                                    else
                                        failwith $"Bitfield members exceed size ({totalSize} bits) of base type {baseType} in {name}"
                            | _ ->
                                let bits = { typ = baseType; name = f.name; offset = 0; count = bitCount}
                                let bf = { typ = baseType; name = $"__bitfield{index}"; bits = [bits] }
                                result, Some bf, index + 1
                        | _ ->
                            match bitfield with
                            | Some bf -> failwith $"Incomplete bitfield {bf.bits} in {name}"
                            | _ -> ()

                            (StructField.Field f)::result, None, index
                    )

                match bitfield with
                | Some bf -> failwith $"Incomplete bitfield {bf.bits} in {name}"
                | _ -> ()

                Some { name = name; fields = List.rev combined; isUnion = isUnion; alias = None; comment = comment }

        | None -> None

    let dfs (getName : 'a -> string) (graph : Map<'a, list<'a>>) visited start_node =

        let rec explore path visited node =
            if List.contains node path then
                let cycle = node :: path |> List.rev |> List.map getName |> String.concat " -> "
                printfn "WARNING: Dependency cycle detected %s" cycle
                visited
            else
                if List.contains node visited then
                    visited
                else
                    let new_path = node :: path
                    let edges    = Map.find node graph
                    let visited  = List.fold (explore new_path) visited edges
                    node :: visited

        explore [] visited start_node

    let inline toposort< ^T when ^T : comparison and (^T) : (member name : string)> (graph: Map< ^T, ^T list>) =
        let getName (x: ^T) = x.name
        List.fold (fun visited (node,_) -> dfs getName graph visited node) [] (Map.toList graph)

    let topologicalSort (s : list<Struct>) : list<Struct> =

        let typeMap = s |> List.map (fun s -> s.name, s) |> Map.ofList

        let graph =
            s |> List.map (fun s ->
                    let usedTypes =
                        let fields =
                            s.fields
                            |> List.map _.Type
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

    let tryRead (defines : Map<string, Numeric>) (e : XElement) =
        let (t, n) = Type.readXmlTypeAndName defines e

        let emit =
            match t with
            | Type.Literal t -> Enum.cleanName t <> Enum.cleanName n
            | _ -> true
        if emit then
            Some { name = Enum.cleanName n; baseType = t}
        else
            None

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
    let tryRead (defines : Map<string, Numeric>) (e : XElement) : Option<Choice<Command, Alias>> =
        try
            let proto = e.Element(xname "proto")
            let (returnType,name) = Type.readXmlTypeAndName defines proto

            let parameters =
                e.Elements("param")
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

    member x.Name =
        $"{x.baseType}_{x.count}"

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
    | VkVersion14

    override x.ToString() =
        match x with
        | VkVersion10 -> "Vulkan 1.0"
        | VkVersion11 -> "Vulkan 1.1"
        | VkVersion12 -> "Vulkan 1.2"
        | VkVersion13 -> "Vulkan 1.3"
        | VkVersion14 -> "Vulkan 1.4"

    static member TryParse(str) =
        match str with
        | "VK_VERSION_1_0" -> Some VkVersion10
        | "VK_VERSION_1_1" -> Some VkVersion11
        | "VK_VERSION_1_2" -> Some VkVersion12
        | "VK_VERSION_1_3" -> Some VkVersion13
        | "VK_VERSION_1_4" -> Some VkVersion14
        | _ -> None

let (|VkVersion|_|) (str: string) =
    VkVersion.TryParse str

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module VkVersion =
    let toModuleName = function
        | VkVersion10 -> None
        | VkVersion11 -> Some "Vulkan11"
        | VkVersion12 -> Some "Vulkan12"
        | VkVersion13 -> Some "Vulkan13"
        | VkVersion14 -> Some "Vulkan14"

[<RequireQualifiedAccess>]
type Module =
    | Core of VkVersion
    | Extension of string

let (|ModuleName|_|) = function
    | VkVersion v -> Some <| Module.Core v
    | str when not <| String.IsNullOrEmpty str -> Some <| Module.Extension str
    | _ -> None

[<RequireQualifiedAccess>]
type Dependency =
    | Empty
    | Expr of string

    member x.References =
        match x with
        | Empty -> Set.empty
        | Expr expr ->
            expr.Replace("(", "").Replace(")", "").Replace(",", "+").Split('+')
            |> Set.ofArray
            |> Set.map (function
                | ModuleName name -> name
                | _ -> failwithf "Invalid depdendency expression '%s'" expr
            )

    member x.Extensions =
        x.References
        |> Set.toList
        |> List.choose (function
            | Module.Extension ext -> Some ext
            | _ -> None
        )
        |> Set.ofList

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
        depends         : Dependency
        parent          : Module
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

        if x.depends <> y.depends then
            failwith "cannot union interfaces required by different APIs"
        elif x.parent <> y.parent then
            failwith "cannot union interfaces from different locations"
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
                depends = x.depends
                parent = x.parent
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

type Attribute =
    | Disabled
    | Obsolete of Module option
    | Promoted of Module
    | Deprecated of Module option

type ExtensionType =
    | Device
    | Instance

type Extension =
    {
        typ             : ExtensionType
        name            : string
        number          : int
        depends         : Dependency
        requires        : Require list
        attributes      : Attribute list
    }

    member x.Disabled =
        x.attributes |> List.contains Attribute.Disabled

    member x.Promoted =
        x.attributes
        |> List.tryPick (function
            | Attribute.Promoted ref -> Some ref
            | _ -> None
        )

    member x.Obsolete =
        x.attributes
        |> List.tryPick (function
            | Attribute.Obsolete ref -> Some ref
            | _ -> None
        )

    member x.Deprecated =
        x.attributes
        |> List.tryPick (function
            | Attribute.Deprecated ref -> Some ref
            | _ -> None
        )

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Extension =
    let private dummyRx = System.Text.RegularExpressions.Regex @"VK_[A-Za-z]+_extension_([0-9]+)"

    let isEmpty (e : Extension) =
        dummyRx.IsMatch e.name && e.requires |> List.forall Require.isEmpty

    let private regex = Regex @"^VK_(?<kind>[A-Z]+)_(?<name>.*)$"

    let toModuleName (str: string) =
        let m = regex.Match str
        let kind = m.Groups.["kind"].Value
        let name = m.Groups.["name"].Value
        sprintf "%s%s" kind (camelCase name)

    let getVendor (str: string) =
        let m = regex.Match str
        m.Groups.["kind"].Value

module Module =

    let isCore = function
        | Module.Core _ -> true
        | _ -> false

    let toModuleName = function
        | Module.Core v -> VkVersion.toModuleName v
        | Module.Extension e -> Some <| Extension.toModuleName e

module Dependency =
    let ofOption = function
        | Some expr -> Dependency.Expr expr
        | _ -> Dependency.Empty

    let private regexFeatureName = Regex "[a-zA-Z0-9_]+"

    let toString = function
        | Dependency.Empty -> None
        | Dependency.Expr expr ->
            Some <| regexFeatureName.Replace(expr, fun m ->
                match m.Value with
                | VkVersion v -> v |> VkVersion.toModuleName |> Option.get
                | ext -> Extension.toModuleName ext

            ).Replace(",", " | ").Replace("+", ", ")

module XmlReader =
    let vendorTags (registry : XElement) =
        registry.Elements("tags")
            |> Seq.collect (fun e ->
                e.Elements("tag")
                |> Seq.choose (fun c ->
                    match attrib c "name" with
                    | Some name -> Some name
                    | _ -> None
                )
            )
            |> List.ofSeq

    let defines (registry : XElement) =
        registry.Elements("enums")
            |> Seq.filter (fun e -> attrib e "name" = Some "API Constants")
            |> Seq.collect (fun e ->
                  let choices = e.Elements("enum")
                  choices |> Seq.choose (fun c ->
                    match attrib c "name", attrib c "value", attrib c "type" with
                        | Some name, Some value, Some typ -> Some(name, toDefine typ value)
                        | _ -> None
                  )
              )
            |> Map.ofSeq

    let readRequire (definitions : Definitions) (parent : Module) (require : XElement) =
        let enumExtensions =
            require.Elements("enum")
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
            require.Descendants("type")
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
            require.Descendants("command")
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
            depends         = attrib require "depends" |> Dependency.ofOption
            parent          = parent
        }

    let tryReadFeature (definitions : Definitions) (feature : XElement) =
        match attrib feature "name" with
        | Some (VkVersion v) ->
            let requires =
                feature.Elements("require")
                |> List.ofSeq
                |> List.choose (fun r ->
                    let r = r |> readRequire definitions (Module.Core v)

                    if Require.isEmpty r then
                        None
                    else
                        Some r
                )

            if requires |> List.forall Require.isEmpty then
                printfn "WARNING: Empty feature: %A" v
                None
            else
                Some {
                    version = v
                    requires = requires
                }

        | _ ->
            None

    let features (definitions : Definitions) (registry : XElement) =
        registry.Elements("feature")
            |> List.ofSeq
            |> List.choose (tryReadFeature definitions)

    let readExtension (definitions : Definitions) (extension : XElement) =
        match attrib extension "name", attrib extension "number" with
        | Some name, Some number ->
            let number = Int32.Parse(number)

            let depends =
                attrib extension "depends"
                |> Dependency.ofOption

            let requires =
                extension.Elements("require")
                    |> List.ofSeq
                    |> List.choose (fun r ->
                        let r = r |> readRequire definitions (Module.Extension name)

                        if Require.isEmpty r then
                            None
                        else
                            Some r
                    )

            let attributes =
                [
                    match attrib extension "supported" with
                    | Some "disabled" ->
                        yield Attribute.Disabled

                    | Some api when api.Split(",") |> Array.contains "vulkan" |> not ->
                        yield Attribute.Disabled

                    | _ -> ()

                    match attrib extension "promotedto" with
                    | Some (ModuleName name) -> yield Attribute.Promoted name
                    | _ -> ()

                    match attrib extension "deprecatedby" with
                    | Some (ModuleName name) -> yield Attribute.Deprecated (Some name)
                    | Some _ -> yield Attribute.Deprecated None
                    | _ -> ()

                    match attrib extension "obsoletedby" with
                    | Some (ModuleName name) -> yield Attribute.Obsolete (Some name)
                    | Some _ -> yield Attribute.Obsolete None
                    | _ -> ()
                ]

            let typ =
                match attrib extension "type" with
                | Some "device" -> Device
                | Some "instance" -> Instance
                | Some t -> failwithf "Extension %s has unknown type '%s'" name t
                | None ->
                    if attributes |> List.contains Attribute.Disabled |> not then
                        failwithf "Extension %s does not specify a type" name
                    Device

            {
                typ          = typ
                name         = name
                number       = number
                depends      = depends
                requires     = requires
                attributes   = attributes
            }

        | _ ->
            failwith "Extension missing name or number"

    let extensions (definitions : Definitions) (registry : XElement) =
        registry.Element(xname "extensions").Elements("extension")
            |> List.ofSeq
            |> List.filter (fun e -> attrib e "supported" <> Some "disabled")
            |> List.choose (fun e ->
                let e = e |> readExtension definitions

                if Extension.isEmpty e || e.Disabled then
                    None
                else
                    Some e
            )

    let emptyBitfields (registry : XElement) =
        registry.Element(xname "types").Elements("type")
            |> List.ofSeq
            |> List.filter (fun e -> attrib e "category" = Some "bitmask" && attrib e "requires" = None)
            |> List.choose (fun e -> child e "name")

    let enums (registry : XElement) =
        registry.Elements("enums")
            |> Seq.filter (fun e -> attrib e "name" <> Some "API Constants")
            |> Seq.choose Enum.tryRead
            |> Seq.map (fun e -> Enum.cleanName e.name, e)
            |> Map.ofSeq

    let structureTypes (registry : XElement) =
        let name = "VkStructureType"

        let baseCases =
            registry.Descendants("enums")
                |> Seq.filter (fun e -> attrib e "name" = Some name)
                |> Seq.collect (fun e -> e.Elements("enum"))

        let extensionCases =
            registry.Descendants("enum")
                |> Seq.filter (fun e -> attrib e "extends" = Some name)

        seq { baseCases; extensionCases}
            |> Seq.concat
            |> Seq.choose (fun e ->
                let name = attrib e "name"
                e |> Enum.tryGetValue |> Option.map (fun v -> name.Value, Enum.valueToStr v)
            )
            |> Map.ofSeq

    let funcpointers (registry : XElement) =
        registry.Elements("types")
            |> Seq.collect (fun tc -> tc.Elements ("type"))
            |> Seq.filter (fun t -> attrib t "category" = Some "funcpointer")
            |> Seq.choose (FuncPointer.tryRead)
            |> Seq.toList
            |> Seq.map (fun s -> s.name, s)
            |> Map.ofSeq

    let structs (defines : Map<string, Numeric>) (registry : XElement) =
        registry.Elements("types")
            |> Seq.collect (fun tc -> tc.Elements ("type"))
            |> Seq.filter (fun t -> attrib t "category" = Some "struct" || attrib t "category" = Some "union")
            |> Seq.choose (Struct.tryRead defines)
            |> Seq.toList
            |> Seq.map (fun s -> s.name, s)
            |> Map.ofSeq

    let typedefs (defines : Map<string, Numeric>) (registry : XElement) =
        registry.Elements("types")
            |> Seq.collect (fun tc -> tc.Elements ("type"))
            |> Seq.filter (fun t ->  attrib t "category" = Some "basetype")
            |> Seq.choose (Typedef.tryRead defines)
            |> Seq.toList
            |> Seq.map (fun t -> t.name, t)
            |> Map.ofSeq

    let aliases (registry : XElement) =
        registry.Elements("types")
            |> Seq.collect (fun tc -> tc.Elements ("type"))
            |> Seq.filter (fun t ->  attrib t "alias" |> Option.isSome)
            |> Seq.choose Alias.tryRead
            |> Seq.map (fun t -> t.name, t)
            |> Map.ofSeq

    let handles (registry : XElement) =
        registry.Elements("types")
            |> Seq.collect (fun tc -> tc.Elements ("type"))
            |> Seq.filter (fun t -> attrib t "category" = Some "handle")
            |> Seq.choose (fun e ->
                match child e "name" with
                | Some name -> Some (name, { name = name; nonDispatchable = e.Value.Contains "NON_DISPATCHABLE" })
                | _ -> None
            )
            |> Map.ofSeq

    let commands (defines : Map<string, Numeric>) (registry : XElement) =
        let elems =
            registry.Element(xname "commands").Elements("command")

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
        | Extension of name: string * requires: Dependency

        member x.RelativePath(relativeTo: Location) =
            let sprintVersion =
                VkVersion.toModuleName >> Option.map (sprintf "%s.") >> Option.defaultValue ""

            match x, relativeTo with
            | Global v1, Global v2 when v1 = v2 -> ""
            | Extension (n1, _), Extension (n2, _) when n1 = n2 -> ""
            | Global v, _ -> sprintVersion v
            | Extension(n, e), _ ->
                let n = Extension.toModuleName n
                match Dependency.toString e with
                | Some e -> $"{n}.``{e}``."
                | None -> $"{n}."

        member x.Vendor =
            match x with
            | Global _ -> None
            | Extension (name, _) -> Some <| Extension.getVendor name

        override x.ToString() =
            match x with
            | Global v -> string v
            | Extension (name, Dependency.Empty) -> name
            | Extension (name, Dependency.Expr dep) -> $"{name} ({dep})"

    type Require with
        member x.Location =
            match x.parent with
            | Module.Core v -> Global v
            | Module.Extension name -> Extension(name, x.depends)

    let definitionLocations = Collections.Generic.Dictionary<string, Location>()

    let tryGetTypeAlias (location : Location) (name : string) =
        match definitionLocations.TryGetValue name with
        | true, aliasLocation when aliasLocation <> location ->
            let path = aliasLocation.RelativePath(location)
            Some $"{path}{name}"

        | true, _ ->
            None

        | _ ->
            failwith $"Location for definition {name} is unknown."

    let getFullyQualifiedTypeName (location: Location) (name : string) =
        tryGetTypeAlias location name |> Option.defaultValue name

    let tryGetCommandAlias (location : Location) (name : string) =
        match definitionLocations.TryGetValue name with
        | true, aliasLocation when aliasLocation <> location ->
            let path = aliasLocation.RelativePath(location)
            Some $"{path}VkRaw.{name}"

        | true, _ ->
            None

        | _ ->
            failwith $"Location for definition {name} is unknown."

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

    let emptyBitfields (enums : List<string>) =
        enums |> List.iter (fun e ->
            printfn ""
            printfn "[<Flags>]"
            printfn "type %s = | None = 0" (Enum.cleanName e)
        )

    let inlineArray (indent : string) (location : Location) (typ: ArrayStruct) =
        let printfn fmt =
            Printf.kprintf (fun str ->
                if str = "" then printfn "" else printfn "%s%s" indent str
            ) fmt

        match tryGetTypeAlias location typ.Name with
        | None ->
            let totalSize = typ.count * typ.baseTypeSize
            printfn "/// Array of %d %s values." typ.count typ.baseType
            printfn "[<StructLayout(LayoutKind.Explicit, Size = %d)>]" totalSize
            printfn "type %s =" typ.Name
            printfn "    struct"
            printfn "        [<FieldOffset(0)>]"
            printfn "        val mutable public First : %s" typ.baseType
            printfn ""
            printfn "        member x.Item"
            printfn "            with get (i : int) : %s =" typ.baseType
            printfn "                if i < 0 || i > %d then raise <| IndexOutOfRangeException()" (typ.count - 1)
            printfn "                let ptr = &&x |> NativePtr.toNativeInt |> NativePtr.ofNativeInt"
            printfn "                NativePtr.get ptr i"
            printfn "            and set (i : int) (value : %s) =" typ.baseType
            printfn "                if i < 0 || i > %d then raise <| IndexOutOfRangeException()" (typ.count - 1)
            printfn "                let ptr = &&x |> NativePtr.toNativeInt |> NativePtr.ofNativeInt"
            printfn "                NativePtr.set ptr i value"
            printfn ""
            printfn "        member x.Length = %d" typ.count
            printfn ""
            printfn "        interface System.Collections.IEnumerable with"
            printfn "            member x.GetEnumerator() = let x = x in (Seq.init %d (fun i -> x.[i])).GetEnumerator() :> System.Collections.IEnumerator" typ.count
            printfn "        interface System.Collections.Generic.IEnumerable<%s> with" typ.baseType
            printfn "            member x.GetEnumerator() = let x = x in (Seq.init %d (fun i -> x.[i])).GetEnumerator()" typ.count
            printfn "    end"
        | Some alias ->
            if typ.Name <> alias then
                printfn "type %s = %s" typ.Name alias

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
    let apiConstants (map : Map<string, Numeric>) =
        printfn ""
        printfn "[<AutoOpen>]"
        printfn "module Constants ="
        for (n, v) in Map.toSeq map do
            printfn ""
            printfn "    [<Literal>]"
            let n = n |> capsToCamelCase [] ""
            printfn "    let %s = %s" n (string v)
        printfn ""

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
            "int32_t", "int32"
            "int", "int32"
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

    let primitiveTypeArrays =
        [
            { baseType = "uint32"; baseTypeSize = 4; count = 32 }
            { baseType = "int32"; baseTypeSize = 4; count = 7 }
            { baseType = "byte"; baseTypeSize = 1; count = 32 }
            { baseType = "byte"; baseTypeSize = 1; count = 8 }
            { baseType = "float32"; baseTypeSize = 4; count = 6 }
        ]

    let vulkanTypeArrays =
        [
            { baseType = "VkPhysicalDevice"; baseTypeSize = 8; count = 32 }
            { baseType = "VkDeviceSize"; baseTypeSize = 8; count = 16 }
            { baseType = "VkFragmentShadingRateCombinerOpKHR"; baseTypeSize = 4; count = 2 }
            { baseType = "VkMemoryHeap"; baseTypeSize = 16; count = 16 }
            { baseType = "VkMemoryType"; baseTypeSize = 8; count = 32 }
            { baseType = "VkOffset3D"; baseTypeSize = 12; count = 2 }
            { baseType = "VkQueueGlobalPriority"; baseTypeSize = 4; count = 16 }
        ]
        |> List.map (fun s -> s.baseType, s)
        |> Map.ofList

    type Enum with
        member x.ArrayType =
            vulkanTypeArrays |> Map.tryFind (Enum.cleanName x.name)

    type Handle with
        member x.ArrayType =
            vulkanTypeArrays |> Map.tryFind (Enum.cleanName x.name)

    type Typedef with
        member x.ArrayType =
            vulkanTypeArrays |> Map.tryFind (Enum.cleanName x.name)

    type Alias with
        member x.ArrayType =
            vulkanTypeArrays |> Map.tryFind (Enum.cleanName x.name)

    type Struct with
        member x.ArrayType =
            vulkanTypeArrays |> Map.tryFind (Enum.cleanName x.name)

    let fsharpName (n : string) =
        if Set.contains n reservedKeywords then sprintf "_%s" n
        else n

    let rec typeName (location: Location) (n: Type) =
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
            match typeName location l, s with
            | "int32", 8 -> "int8"
            | "uint32", 8 -> "uint8"
            | "uint32", 24 -> "uint24"
            | t, 8 -> System.Console.WriteLine("WARNING: Replacing {0}:8 with uint8", t); "uint8"
            | t, 24 -> System.Console.WriteLine("WARNING: Replacing {0}:24 with uint24", t); "uint24"
            | t, s -> failwith $"unsupported bit field type {t}:{s}"

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
                    if n.StartsWith "Vk" || n.StartsWith "PFN" then getFullyQualifiedTypeName location n
                    elif n.StartsWith "structVk" then n.Substring("struct".Length)
                    elif n.StartsWith "Mir" || n.StartsWith "struct" then "nativeint"
                    else "nativeint" //failwithf "strange type: %A" n
        | Ptr t ->
            sprintf "nativeptr<%s>" (typeName location t)
        | FixedArray(t, s) ->
            let t = typeName location t
            sprintf "%s_%d" t s
        | FixedArray2d(t, w, h) ->
            let t = typeName location t
            sprintf "%s_%d" t (w * h)

    let enumExtensions (indent : string) (vendorTags : list<string>) (location : Location) (exts : Map<string, list<EnumCase>>) =

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

                let name = Enum.cleanName name
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

                let name = getFullyQualifiedTypeName location name
                printfn "     type %s with" name
                for c in exts do
                    match c.comment with
                    | Some comment -> printfn "          /// %s" comment
                    | _ -> ()
                    printfn "          static member inline %s = enum<%s> %s" c.name name (Enum.valueToStr c.value)

            printfn ""

    let enums (indent : string) (vendorTags : list<string>) (location : Location) (enums : list<Enum>) =

        let printfn fmt =
            Printf.kprintf (fun str ->
                if str = "" then printfn "" else printfn "%s%s" indent str
            ) fmt

        let vendorTagsCamel =
            vendorTags |> List.map (capsToCamelCase [] "")

        for e in enums do
            let name = Enum.cleanName e.name

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

                | Some alias ->
                    if name <> alias then
                        printfn "type %s = %s" name alias

            e.ArrayType |> Option.iter (inlineArray indent location)

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
            h.ArrayType |> Option.iter (fun arr ->
                printfn ""
                inlineArray indent location arr
            )

        if List.length l > 0 then
            printfn ""

    let typedefs (indent : string) (location : Location) (l : list<Typedef>) =
        for x in l do
            let name = getFullyQualifiedTypeName location x.name
            let alias = typeName location x.baseType
            if name <> alias then
                printfn "%stype %s = %s" indent name alias

        for t in l do
            t.ArrayType |> Option.iter (fun arr ->
                printfn ""
                inlineArray indent location arr
            )

        if List.length l > 0 then
            printfn ""

    let aliases (indent : string) (location : Location) (aliases : list<Alias>) =
        for a in aliases do
            if a.name <> a.baseSym then
                printfn "%stype %s = %s" indent a.name a.baseSym

        for a in aliases do
            a.ArrayType |> Option.iter (fun arr ->
                printfn ""
                inlineArray indent location arr
            )

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

        let toFunctionDecl (indent : int) (functionName : string) (fields : Field list) =
            fields |> List.map (fun f ->
                sprintf "%s: %s" (fsharpName f.name) (typeName location f.typ)
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

            let alias =
                s.alias
                |> Option.map (getFullyQualifiedTypeName location)
                |> Option.orElseWith (fun _ ->
                    tryGetTypeAlias location s.name
                )

            match alias with
            | Some alias ->
                if s.name <> alias then
                    printfn "type %s = %s" s.name alias
                    printfn ""
            | None ->
                if s.isUnion then printfn "[<StructLayout(LayoutKind.Explicit)>]"
                else printfn "[<StructLayout(LayoutKind.Sequential)>]"

                printfn "type %s =" s.name
                printfn' 1 "struct"
                for f in s.fields do
                    let name = fsharpName f.Name
                    let typeName = typeName location f.Type
                    let access = match f with StructField.Field _ -> "public" | _ -> "private"

                    if s.isUnion then
                        printfn' 2 "[<FieldOffset(0)>]"

                    printfn' 2 $"val mutable {access} {name} : {typeName}"

                // Properties for bit field members
                let getMask (bf: BitField) (f: BitFieldBits) =
                    let v = (1UL <<< f.count) - 1UL
                    let s = v.ToString("X")
                    $"0x{s}{Type.literalSuffix bf.typ}"

                let getCast (forward: bool) (bf: BitField) (f: BitFieldBits) =
                    if bf.typ <> f.typ then
                        if forward then
                            typeName location bf.typ
                        else
                            let t = typeName location f.typ
                            if Type.isEnum f.typ then $"enum<{t}> <| int32" else t
                    else
                        ""

                for f in s.fields do
                    match f with
                    | StructField.BitField bf ->
                        for f in bf.bits do
                            let n = fsharpName f.name
                            let bn = fsharpName bf.name
                            let castfw = getCast true bf f
                            let castbw = getCast false bf f
                            let t = typeName location f.typ
                            let mask = getMask bf f
                            printfn ""
                            printfn' 2 $"member x.{n}"
                            printfn' 3 $"with get() : {t} = {castbw} ((x.{bn} >>> {f.offset}) &&& {mask})"
                            printfn' 3 $"and set (value: {t}) = x.{bn} <- (x.{bn} &&& ~~~({mask} <<< {f.offset})) ||| ((({castfw} value) &&& {mask}) <<< {f.offset})"

                    | _ -> ()

                // Set the sType field automatically
                let fields =
                    match s.name with
                    | "VkBaseInStructure"
                    | "VkBaseOutStructure" -> s.fields
                    | _ -> s.fields |> List.filter (fun f -> f.Name <> "sType")

                let expandedFields =
                    fields |> List.collect (function
                        | StructField.Field f -> [f]
                        | StructField.BitField bf -> bf.bits |> List.map (fun b -> { typ = b.typ; name = b.name; values = None })
                    )

                let hasTypeField =
                    s.fields.Length <> fields.Length

                let sType =
                    let value =
                        s.fields
                        |> List.tryPick (function
                            | StructField.Field f when f.name = "sType" -> Some f
                            | _ -> None
                        )
                        |> Option.bind (fun f ->
                            match f.values with
                            | Some v -> structureTypes |> Map.tryFind v
                            | None -> None
                        )

                    match value with
                    | Some v -> sprintf "%su" v
                    | None -> @"failwith ""Reserved for future use or possibly a bug in the generator"""

                let isNextPtr (f : Field) =
                    f.name = "pNext" && f.typ = Ptr (Literal "void")

                let nextPtrIndex = expandedFields |> List.tryFindIndex isNextPtr

                // Proper default constructors are not allowed for structs...
                let defaultConstructor() =
                    printfn ""

                    let values =
                        expandedFields |> List.map(fun f ->
                            sprintf "Unchecked.defaultof<%s>" (typeName location f.typ)
                        )

                    printfn' 2 "static member Empty ="
                    values |> toFunctionCall 3 s.name

                let isEmptyMember() =
                    printfn ""

                    let checks =
                        fields |> List.map (fun f ->
                            sprintf "x.%s = Unchecked.defaultof<%s>" (fsharpName f.Name) (typeName location f.Type)
                        )

                    printfn' 2 "member x.IsEmpty ="
                    printfn' 3 "%s" (checks |> String.concat " && ")

                // Constructor with all fields
                let constructorWithAllFields (isPrivate : bool) =
                    printfn ""

                    let name = if isPrivate then "private new" else "new"
                    expandedFields |> toFunctionDecl 2 name

                    let assignments = [
                        if hasTypeField then
                            yield "sType", sType

                        yield! fields |> List.map (function
                            | StructField.Field f ->
                                    (fsharpName f.name), (fsharpName f.name)

                            | StructField.BitField bf ->
                                let value =
                                    bf.bits |> List.map (fun f ->
                                        let mask = getMask bf f
                                        let cast = getCast true bf f
                                        $"(({cast} {fsharpName f.name} &&& {mask}) <<< {f.offset})"
                                    )
                                    |> String.concat " ||| "

                                fsharpName bf.name, value
                        )
                    ]

                    assignments |> toConstructorBody 3

                // Convenience constructor without pNext parameter
                let constructorWithoutNextPtr () =
                    match nextPtrIndex with
                    | Some index when fields.Length > 1 ->
                        printfn ""

                        expandedFields
                        |> List.filter (isNextPtr >> not)
                        |> toFunctionDecl 2 "new"

                        expandedFields
                        |> List.mapi (fun i f ->
                            if i = index then
                                sprintf "Unchecked.defaultof<%s>" (typeName location f.typ)
                            else
                                fsharpName f.name
                        )
                        |> toFunctionCall 3 s.name

                    | _ ->
                        ()

                if s.isUnion then
                    // Static member constructors for each union case
                    for f in expandedFields do
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


                let fieldSplice = expandedFields |> List.map (fun f -> sprintf "%s = %%A" (fsharpName f.name))
                let fieldAccess = expandedFields |> List.map (fun f -> sprintf "x.%s" (fsharpName f.name))

                printfn ""
                printfn' 2 "override x.ToString() ="
                printfn' 3 "String.concat \"; \" ["

                for (s,a) in List.zip fieldSplice fieldAccess do
                    printfn' 4 "sprintf \"%s\" %s" s a

                printfn' 3 "] |> sprintf \"%s { %%s }\"" s.name

                printfn' 1 "end"
                printfn ""

                s.ArrayType |> Option.iter (inlineArray indent location)

        if structs.Length > 0 then
            printfn ""

    let primitiveArrays() =
        for t in primitiveTypeArrays do
            printfn ""
            inlineArray "" (Global VkVersion10) t
        printfn ""

    let rec externTypeName (location: Location) (n: Type) =
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

        | BitField(l, s) -> failwith "Bit fields should be handled as a whole"

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
                    if n.StartsWith "Vk" || n.StartsWith "PFN" then getFullyQualifiedTypeName location n
                    elif n.StartsWith "Mir" || n.StartsWith "struct" then "nativeint"
                    else "nativeint" //failwithf "strange type: %A" n
        | Ptr (Ptr t) -> "nativeint*"
        | Ptr t ->
            sprintf "%s*" (externTypeName location t)
        | FixedArray(t, s) ->
            let t = externTypeName location t
            sprintf "%s_%d" t s
        | FixedArray2d(t, w, h) ->
            let t = externTypeName location t
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
                let args = c.parameters |> List.map (fun (t,n) -> sprintf "%s %s" (externTypeName location t) (fsharpName n)) |> String.concat ", "
                printfn "    [<DllImport(lib, EntryPoint=\"%s\"); SuppressUnmanagedCodeSecurity>]" c.name
                printfn "    extern %s private _%s(%s)" (externTypeName location c.returnType) c.name args



                let argDef = c.parameters |> List.map (fun (t,n) -> sprintf "%s : %s" (fsharpName n) (typeName location t)) |> String.concat ", "
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
                    let args = c.parameters |> List.map (fun (t,n) -> sprintf "%s %s" (externTypeName location t) (fsharpName n)) |> String.concat ", "
                    printfn "    [<DllImport(lib); SuppressUnmanagedCodeSecurity>]"
                    printfn "    extern %s %s(%s)" (externTypeName location c.returnType) c.name args
                | Some alias ->
                    printfn "    let %s = %s" c.name alias

                printfn ""

        if isVersion10 then
            printfn "    [<CompilerMessage(\"vkImportInstanceDelegate is for internal use only\", 1337, IsError=false, IsHidden=true)>]"
            printfn "    let vkImportInstanceDelegate<'T>(name : string) ="
            printfn "        let ptr = vkGetInstanceProcAddr(activeInstance, name)"
            printfn "        if ptr = 0n then"
            printfn "            Log.warn \"could not load function: %%s\" name"
            printfn "            Unchecked.defaultof<'T>"
            printfn "        else"
            printfn "            Report.Line(3, sprintf \"loaded function %%s (0x%%08X)\" name ptr)"
            printfn "            Marshal.GetDelegateForFunctionPointer(ptr, typeof<'T>) |> unbox<'T>"

    let extensionCommands (indent : string) (location : Location) (l : list<Command>) =
        let printfn fmt =
            Printf.kprintf (fun str ->
                if str = "" then printfn "" else printfn "%s%s" indent str
            ) fmt

        let extension =
            match location with
            | Extension(name, dep) ->
                let name = Extension.toModuleName name
                match Dependency.toString dep with
                | Some sub -> sprintf "%s -> %s" name sub
                | _ -> name

            | _ ->
                failwithf "Cannot invoke extensionCommands for location %A" location

        let exists name = tryGetCommandAlias location name |> Option.isSome
        let existAll = l |> List.map (fun c -> exists c.name) |> List.forall id

        printfn "module VkRaw ="
        for c in l do
            if not (exists c.name) then
                let delegateName = c.name.Substring(0, 1).ToUpper() + c.name.Substring(1) + "Del"
                let targs = c.parameters |> List.map (fst >> typeName location) |> String.concat " * "
                let ret =
                    match typeName location c.returnType with
                    | "void" -> "unit"
                    | n -> n

                let tDel = sprintf "%s -> %s" targs ret
                printfn "    [<SuppressUnmanagedCodeSecurity>]"
                printfn "    type %s = delegate of %s" delegateName tDel

        if not existAll then
            printfn ""
            printfn "    [<AbstractClass; Sealed>]"
            printfn "    type private Loader<'T> private() ="
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
                let argDef = c.parameters |> List.map (fun (t,n) -> sprintf "%s : %s" (fsharpName n) (typeName location t)) |> String.concat ", "
                let argUse = c.parameters |> List.map (fun (_,n) -> (fsharpName n)) |> String.concat ", "
                printfn "    let %s(%s) = Loader<unit>.%s.Invoke(%s)" c.name argDef c.name argUse
            | Some alias ->
                printfn "    let %s = %s" c.name alias

    let require (indent : int) (vendorTags : list<string>) (structureTypes : Map<string, string>) (require : Require) =
        let name = require.depends |> Dependency.toString
        let location = require.Location

        let subindent n = String.replicate (if name.IsSome then indent + n + 1 else indent + n) "    "
        let indent = String.replicate indent "    "

        let printfn fmt =
            Printf.kprintf (fun str ->
                if str = "" then printfn "" else printfn "%s%s" indent str
            ) fmt

        match name with
        | Some name ->
            printfn "[<AutoOpen>]"
            printfn "module ``%s`` =" name
        | _ ->
            ()

        funcpointers (subindent 0) require.funcpointers
        handles (subindent 0) location require.handles
        typedefs (subindent 0) location require.typedefs
        aliases (subindent 0) location require.aliases
        enums (subindent 0) vendorTags location require.enums
        structs (subindent 0) structureTypes location (Struct.topologicalSort require.structs)
        enumExtensions (subindent 0) vendorTags location require.enumExtensions

        if not require.commands.IsEmpty then
            if Module.isCore require.parent then
                coreCommands (subindent 0) location require.commands
            else
                extensionCommands (subindent 0) location require.commands

        printfn ""

    let feature (vendorTags : list<string>) (structureTypes : Map<string, string>) (feature : Feature) =
        let name = VkVersion.toModuleName feature.version
        let indent = if name.IsSome then 1 else 0

        match name with
        | Some name ->
            printfn "module %s =" name
        | _ ->
            ()

        feature.requires |> Require.unionMany |> require indent vendorTags structureTypes

    let features (vendorTags : list<string>) (structureTypes : Map<string, string>) (features : Feature list) =
        for f in features do
            f |> feature vendorTags structureTypes
            printfn ""

    let extension (vendorTags : list<string>) (structureTypes : Map<string, string>) (e : Extension) =
        let printfn fmt =
            Printf.kprintf (fun str ->
                if str = "" then printfn "" else printfn "    %s" str
            ) fmt

        let name = Extension.toModuleName e.name

        if String.IsNullOrEmpty(name) then
            Console.WriteLine("WARNING: Ignoring extension '{0}'", e.name)
        else
            match Dependency.toString e.depends with
            | Some expr -> printfn "/// Requires %s." expr
            | _ -> ()

            e.Promoted
            |> Option.iter (fun name ->
                match Module.toModuleName name with
                | Some name -> printfn "/// Promoted to %s." name
                | _ -> ()
            )

            e.Obsolete
            |> Option.map (Option.bind Module.toModuleName)
            |> Option.iter (function
                | Some name -> printfn "/// Incompatible with %s." name
                | _ -> printfn "/// Obsolete."
            )

            e.Deprecated
            |> Option.map (Option.bind Module.toModuleName)
            |> Option.iter (function
                | Some name -> printfn "/// Deprecated by %s." name
                | _ -> printfn "/// Deprecated."
            )

            printfn "module %s =" name
            printfn "    let Type = ExtensionType.%A" e.typ
            printfn "    let Name = \"%s\"" e.name
            printfn "    let Number = %d" e.number
            printfn ""

            let requires =
                e.requires
                |> List.groupBy (fun r -> r.depends)
                |> List.map (snd >> Require.unionMany)

            for r in requires do
                r |> require 2 vendorTags structureTypes

    let extensions (vendorTags : list<string>) (structureTypes : Map<string, string>) (exts : Extension list) =
        let sorted = exts |> List.sortBy _.number

        printfn "[<AutoOpen>]"
        printfn "module rec Extensions ="
        printfn ""

        for e in sorted do
            extension vendorTags structureTypes e

    // Finds and stores the location of all type definitions.
    // If there are multiple possible locations, the best one is selected based on some simple rules.
    let preprocess (vendorTags: string list) (emptyBitfields: string list) (features: Feature list) (extensions: Extension list) =
        let locations = System.Collections.Generic.Dictionary<string, Location list>()

        let addDefinitionLocation (location: Location) (name: string) =
            let name = Enum.cleanName name

            let value =
                match locations.TryGetValue name with
                | true, existing -> existing @ [location]
                | _ -> [location]

            locations.[name] <- value

        let addType (location: Location) (arrayType: ArrayStruct option) (name: string) =
            addDefinitionLocation location name
            arrayType |> Option.iter (fun arr -> addDefinitionLocation location arr.Name)

        for e in emptyBitfields do
            addDefinitionLocation (Location.Global VkVersion10) e

        let addDefinitionsFromRequire (r: Require) =
            for f in r.funcpointers do
                addDefinitionLocation r.Location f.name

            for h in r.handles do
                addType r.Location h.ArrayType h.name

            for t in r.typedefs do
                addType r.Location t.ArrayType t.name

            for a in r.aliases do
                addType r.Location a.ArrayType a.name

            for e in r.enums do
                addType r.Location e.ArrayType e.name

            for s in r.structs do
                addType r.Location s.ArrayType s.name

            for c in r.commands do
                addDefinitionLocation r.Location c.name

        for p in primitiveTypeArrays do
            addDefinitionLocation (Location.Global VkVersion10) p.Name

        for f in features do
            for r in f.requires do
                addDefinitionsFromRequire r

        for e in extensions do
            for r in e.requires do
                addDefinitionsFromRequire r

        for KeyValue(name, locations) in locations do
            match locations with
            | [single] ->
                definitionLocations.[name] <- single

            | _ ->
                let typeVendor = findEnumVendorSuffix vendorTags name

                let getScore (location: Location) =
                    match typeVendor, location.Vendor with
                    | Some vt, Some vl when vt = vl -> 1
                    | Some vt, Some vl when vt <> vl -> -1
                    | _ -> 0

                // Sort the locations:
                // (1) Prefer core versions over later core versions and extensions
                // (2) Prefer KHR over EXT over all other vendors
                // (3) Prefer locations where vendor matches type vendor
                // (4) Avoid locations where vendor does NOT match type vendor
                let sorted =
                    locations
                    |> List.sortWith (fun l r ->
                        match l, r with
                        | Global l, Global r -> compare l r
                        | Global _, Extension _ -> -1
                        | Extension _, Global _ -> 1
                        | Extension (nl, _), Extension (nr, _) ->
                            match Extension.getVendor nl, Extension.getVendor nr with
                            | "KHR", _ -> -1
                            | _, "KHR" -> 1
                            | "EXT", _ -> -1
                            | _, "EXT" -> 1
                            | _ -> compare (getScore r) (getScore l)
                    )

                FSharp.Core.Printf.printfn $"WARNING: Multiple definition locations for {name}: {sorted}"
                definitionLocations.[name] <- List.head sorted

open FSharpWriter

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
    let extensions = XmlReader.extensions definitions vk

    FSharpWriter.preprocess vendorTags emptyBitfields features extensions

    FSharpWriter.header()
    FSharpWriter.apiConstants defines
    FSharpWriter.emptyBitfields emptyBitfields
    FSharpWriter.primitiveArrays()
    FSharpWriter.features vendorTags structureTypes features
    FSharpWriter.extensions vendorTags structureTypes extensions

    let str = FSharpWriter.builder.ToString()
    FSharpWriter.builder.Clear() |> ignore

    let file = Path.Combine(__SOURCE_DIRECTORY__, "Vulkan.fs")

    File.WriteAllText(file, str)
    printfn "Generated 'Vulkan.fs' successfully!"

//module PCI =
//    open System
//    open System.IO
//    let builder = System.Text.StringBuilder()

//    let printfn fmt =
//        Printf.kprintf (fun str -> builder.AppendLine(str) |> ignore) fmt


//    let writeVendorAndDeviceEnum() =

//        let rx = System.Text.RegularExpressions.Regex "\"0x(?<vendor>[0-9A-Fa-f]+)\",\"0x(?<device>[0-9A-Fa-f]+)\",\"(?<vendorName>[^\"]+)\",\"(?<deviceName>[^\"]+)\""

//        let req = System.Net.HttpWebRequest.Create("http://pcidatabase.com/reports.php?type=csv")
//        let response = req.GetResponse()
//        let reader = new System.IO.StreamReader(response.GetResponseStream())

//        let vendors = System.Collections.Generic.Dictionary<int64, string>()
//        let devices = System.Collections.Generic.Dictionary<int64, string>()

//        let mutable line = reader.ReadLine()

//        while not (isNull line) do
//            let m = rx.Match line

//            if m.Success then
//                let vid = System.Int64.Parse(m.Groups.["vendor"].Value, System.Globalization.NumberStyles.HexNumber)
//                let did = System.Int64.Parse(m.Groups.["device"].Value, System.Globalization.NumberStyles.HexNumber)
//                let vname = m.Groups.["vendorName"].Value
//                let dname = m.Groups.["deviceName"].Value

//                vendors.[vid] <- vname.Replace("\\", "\\\\")
//                devices.[did] <- dname.Replace("\\", "\\\\")

//            line <- reader.ReadLine()

//        printfn "namespace Aardvark.Rendering.Vulkan"
//        printfn "open System.Collections.Generic"
//        printfn "open Aardvark.Base"


//        printfn "module PCI = "
//        printfn "    let vendors ="
//        printfn "        Dictionary.ofArray [|"
//        for (KeyValue(k,v)) in vendors do
//            if k <= int64 Int32.MaxValue then
//                printfn "            0x%08X, \"%s\"" k v
//        printfn "        |]"

////        printfn "    let devices ="
////        printfn "        Dictionary.ofArray [|"
////        for (KeyValue(k,v)) in devices do
////            if k <= int64 Int32.MaxValue then
////                printfn "            0x%08X, \"%s\"" k v
////        printfn "        |]"


//        printfn "    let vendorName (id : int) ="
//        printfn "        match vendors.TryGetValue id with"
//        printfn "            | (true, name) -> name"
//        printfn "            | _ -> \"Unknown\""

////        printfn "    let deviceName (id : int) ="
////        printfn "        match devices.TryGetValue id with"
////        printfn "            | (true, name) -> name"
////        printfn "            | _ -> \"Unknown\""

//    let run() =
//        builder.Clear() |> ignore
//        writeVendorAndDeviceEnum()
//        let str = builder.ToString()
//        builder.Clear() |> ignore
//        let file = Path.Combine(__SOURCE_DIRECTORY__, "PCI.fs")
//        File.WriteAllText(file, str)

do run()