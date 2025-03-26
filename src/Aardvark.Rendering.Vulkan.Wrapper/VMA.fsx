#r @"..\..\bin\Debug\netstandard2.0\Aardvark.Rendering.Vulkan.Wrapper.dll"

open System
open System.Collections.Generic
open System.IO
open System.Text
open System.Text.RegularExpressions
open Aardvark.Rendering.Vulkan

// Generator for VMA API parsing vk_mem_alloc.h

[<AutoOpen>]
module Utilities =

    module String =
        let toLower (str: string)=
            if String.IsNullOrEmpty str then str
            else str.ToLowerInvariant()

        let toLowerStart (str: string)=
            if String.IsNullOrEmpty str then str
            else $"{Char.ToLowerInvariant str.[0]}{str.Substring 1}"

        let remove (pattern: string) (str: string) =
            str.Replace(pattern, "")

        let strip (str: string) =
            str.Replace(" ", "")

        let removeRegex (pattern: Regex) (str: string) =
            pattern.Replace(str, "")

        let split (separator: char) (str: string) =
            str.Split(separator, StringSplitOptions.TrimEntries ||| StringSplitOptions.RemoveEmptyEntries)

        let splitStr (separator: string) (str: string) =
            str.Split(separator, StringSplitOptions.TrimEntries ||| StringSplitOptions.RemoveEmptyEntries)

        let removePrefix (prefix: string) (str: string) =
            if String.IsNullOrEmpty str then str
            else
                if str.StartsWith prefix then str.Substring(prefix.Length)
                else str

        let removeSuffix (suffix: string) (str: string) =
            if String.IsNullOrEmpty str then str
            else
                if str.EndsWith suffix then str.Substring(0, str.Length - suffix.Length)
                else str

        let toCamelCase (str: string) =
            str.Split('_', StringSplitOptions.TrimEntries ||| StringSplitOptions.RemoveEmptyEntries)
            |> Array.map (fun str -> $"{Char.ToUpperInvariant str.[0]}{str.ToLowerInvariant().Substring 1}")
            |> String.Concat

    module Regex =
        let VmaMacro = Regex("VMA_[A-Z0-9_]+\\(\"?([A-Za-z0-9]+(\\:\\:)?)+?\"?\\)", RegexOptions.Compiled)
        let VmaDefine = Regex(@"VMA_[A-Z0-9_]+", RegexOptions.Compiled)

type Type =
    | Ptr of Type
    | Literal of string

module Type =
    let private rxType = Regex(@"^(?<name>[a-zA-Z_0-9]+)(?<ptr>[\*]*)$")

    let private knownTypes =
        Map.ofList [
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

            "HANDLE", "nativeint"
            "HINSTANCE", "nativeint"
            "HWND", "nativeint"

            "DWORD", "uint32"

            "VkExternalMemoryHandleTypeFlagsKHR", "VkExternalMemoryHandleTypeFlags"
        ]

    let private (|StartsWith|_|) (prefix: string) (str: string) =
        if str.StartsWith prefix then Some ()
        else None

    let getSizeInBytes = function
        | Literal "VmaDetailedStatistics" -> 64
        | t -> failwith $"Cannot determine size of type {t}"

    let rec externName = function
        | Ptr(Literal "char")            -> "cstr"
        | Ptr(Literal "void")            -> "nativeint"
        | Ptr(Ptr t)                     -> $"{externName t}* *"
        | Ptr t                          -> $"{externName t}*"
        | Literal (StartsWith "PFN_vk")
        | Literal (StartsWith "PFN_vma") -> "nativeint"
        | Literal n                      -> knownTypes |> Map.tryFind n |> Option.defaultValue n

    let fsharpName (count: int) (typ: Type) =
        let rec baseName = function
            | Ptr(Literal "char")            -> "cstr"
            | Ptr(Literal "void")            -> "nativeint"
            | Ptr t                          -> $"nativeptr<{baseName t}>"
            | Literal (StartsWith "PFN_vk")
            | Literal (StartsWith "PFN_vma") -> "nativeint"
            | Literal n                      -> knownTypes |> Map.tryFind n |> Option.defaultValue n

        let name = baseName typ
        if count > 1 then $"{name}_{count}"
        else name

    let parse (str: string) =
        let m = str |> String.strip |> rxType.Match
        if not m.Success then failwith $"Cannot parse type: {str}"
        let id = m.Groups.["name"].Value
        let ptr = m.Groups.["ptr"].Length

        let mutable t = Literal id
        for i in 1 .. ptr do
            t <- Ptr t
        t

type EnumCase =
    { name  : string
      value : string }

module EnumCase =
    let private rxCase = Regex(@"(?<name>VMA_[A-Z0-9_]+)\s*=\s*((?<value>-?[0-9A-Fx]+)|(?<alias>VMA_[A-Z0-9_]+(?:\s*\|\s*VMA_[A-Z0-9_]+)*))", RegexOptions.Compiled)
    let private rxZero = Regex(@"^0+x?0*$")
    let knownValues = Dictionary<string, string>()

    let parseAll (isFlags: bool) (baseName: string) (str: string) =
        let cases = ResizeArray<EnumCase>()

        for m in rxCase.Matches str do
            let nameRaw = m.Groups.["name"].Value

            let name =
                nameRaw
                |> String.toCamelCase
                |> if isFlags then String.removePrefix $"{baseName}FlagBits" else id
                |> String.removePrefix baseName

            let value =
                if m.Groups.["value"].Success then
                    let value = m.Groups.["value"].Value
                    knownValues.[nameRaw] <- value
                    value
                else
                    let alias = m.Groups.["alias"].Value |> String.split '|'
                    let value =
                        let combined = alias |> Array.map (fun alias -> knownValues.[alias]) |> String.concat " ||| "
                        if alias.Length > 1 then $"({combined})" else combined

                    knownValues.[nameRaw] <- value
                    value

            cases.Add({
              name  = name
              value = value
            })

        if isFlags then
            let hasNone = cases |> Seq.exists (_.name >> String.toLower >> (=) "none")
            let hasZero = cases |> Seq.exists (_.value >> rxZero.IsMatch)

            if not hasNone && not hasZero then
                cases.Insert(0, { name = "None"; value = "0" })

        cases.ToArray()

type Enum =
    { name    : string
      isFlags : bool
      cases   : EnumCase[] }

module Enum =
    let private rxEnum = Regex(@"typedef enum (?<name>[a-zA-Z0-9_]+)[\r\n]+{(?<cases>(?:.|\n)*?)}", RegexOptions.Compiled)

    let parseAll (str: string) =
        let enums = ResizeArray<Enum>()

        for m in rxEnum.Matches str do
            let name = m.Groups.["name"].Value
            let baseName = name |> String.removeSuffix "FlagBits"
            let isFlags = name <> baseName
            let cases = m.Groups.["cases"].Value |> EnumCase.parseAll isFlags baseName

            enums.Add({
                name = if isFlags then $"{baseName}Flags" else baseName
                isFlags = isFlags
                cases = cases
            })

        enums.ToArray()

type Parameter =
    { typ  : Type
      name : string }

module Parameter =
    let private rxParam = Regex(@"^(const )?(?<type>[A-Za-z0-9_]+[\*\s]*)(?<name>[A-Za-z0-9_]+)$")

    let parse (str: string) =
        let m = str.Trim() |> rxParam.Match
        if not m.Success then failwith $"Cannot parse parameter: {str}"
        let typ = Type.parse <| m.Groups.["type"].Value
        let name = m.Groups.["name"].Value
        { typ  = typ
          name = name }

type Handle =
    { name         : string
      dispatchable : bool }

module Handle =
    let private rxHandle = Regex(@"VK_DEFINE(?<nondisp>_NON_DISPATCHABLE)?_HANDLE\((?<name>[A-Za-z0-9]+)\)", RegexOptions.Compiled)

    let parseAll (str: string) =
        let handles = ResizeArray()

        for m in rxHandle.Matches str do
            let name = m.Groups.["name"].Value
            let nonDispatchable = m.Groups.["nondisp"].Success

            handles.Add({
                name = name
                dispatchable = not nonDispatchable
            })

        handles.ToArray()

type Field =
    { typ   : Type
      name  : string
      count : int }

module Field =
    let private rxField = Regex(@"^(const\s*)?(?<type>[A-Za-z0-9_]+[\*\s]*)(?<name>[A-Za-z0-9_]+)\s*(?:\[(?<count>[A-Z_]+)\])?\s*;$", RegexOptions.Compiled)

    let private getConstant = function
        | "VK_MAX_MEMORY_TYPES" -> int VkMaxMemoryTypes
        | "VK_MAX_MEMORY_HEAPS" -> int VkMaxMemoryHeaps
        | str ->
            match Int32.TryParse str with
            | true, value -> value
            | _ -> failwith $"Unknown constant {str}"

    let parseAll (str: string) =
        let fields = ResizeArray<Field>()

        let lines =
            str
            |> String.removeRegex Regex.VmaDefine
            |> String.splitStr Environment.NewLine

        for l in lines do
            let m = rxField.Match l
            if m.Success then
                let name = m.Groups.["name"].Value
                let count =
                    if m.Groups.["count"].Success then
                        getConstant m.Groups.["count"].Value
                    else
                        1

                if not <| fields.Exists (_.name >> (=) name) then
                    fields.Add({
                        typ  = m.Groups.["type"].Value |> Type.parse
                        name = name
                        count = count
                    })

        fields.ToArray()

type Struct =
    | Struct of name: string * fields: Field[]
    | Array of typ: Type * count: int

module Struct =
    let private rxStruct = Regex(@"typedef struct (?<name>Vma[A-Za-z0-9]+)\s*{(?<fields>(.|\n)*?)}", RegexOptions.Compiled)

    let parseAll (str: string) =
        let structs = ResizeArray<Struct>()
        let arrayStructs = HashSet<Type * int>()

        for m in rxStruct.Matches str do
            let name = m.Groups.["name"].Value
            let fields = m.Groups.["fields"].Value |> Field.parseAll

            for f in fields do
                if f.count > 1 && arrayStructs.Add(f.typ, f.count) then
                    structs.Add <| Array (f.typ, f.count)

            structs.Add <| Struct (name, fields)

        structs.ToArray()

type Function =
    { name       : string
      entryPoint : string
      returnType : Type
      parameters : Parameter[] }

module Function =
    let private rxFunc = Regex(@"VMA_CALL_PRE (?<returnType>[A-Za-z0-9]+) VMA_CALL_POST vma(?<name>[A-Za-z0-9]+)\((?<params>(.|\n)*?)\)", RegexOptions.Compiled)

    let parseAll (str: string) =
        let functions = ResizeArray()

        for m in rxFunc.Matches str do
            let name = m.Groups.["name"].Value
            let returnType = m.Groups.["returnType"].Value |> Type.parse

            let parameters =
                m.Groups.["params"].Value
                |> String.remove Environment.NewLine
                |> String.removeRegex Regex.VmaDefine
                |> String.split ','

            functions.Add({
                name = String.toLowerStart name
                entryPoint = $"vma{name}"
                returnType = returnType
                parameters = parameters |> Array.map Parameter.parse
            })

        functions.ToArray()

type VmaHeader =
    { enums     : Enum[]
      handles   : Handle[]
      structs   : Struct[]
      functions : Function[] }

module VmaHeader =
    let private vulkanSdkEnv =
        [ "VULKAN_SDK"; "VK_SDK_PATH" ]

    let private getVulkanSdk() =
        vulkanSdkEnv |> List.pick (fun v ->
            let p = Environment.GetEnvironmentVariable v
            if String.IsNullOrWhiteSpace p then None else Some p
        )

    let read() : VmaHeader =
        let sdk = getVulkanSdk()
        let path = Path.Combine(sdk, "Include", "vma", "vk_mem_alloc.h")

        let getSection =
            let data = File.ReadAllLines path

            fun (removeMacros: bool) (section: string) ->
                let s = data |> Array.findIndex (fun s -> s.Trim() = $"#ifndef {section}")
                let e = data |> Array.findIndex (fun s -> s.Trim() = $"#endif // {section}")

                data.[s .. e]
                |> String.concat Environment.NewLine
                |> if removeMacros then String.removeRegex Regex.VmaMacro else id

        let enums = Enum.parseAll <| getSection true "_VMA_ENUM_DECLARATIONS"
        let handles = Handle.parseAll <| getSection false "_VMA_DATA_TYPES_DECLARATIONS"
        let structs = Struct.parseAll <| getSection true "_VMA_DATA_TYPES_DECLARATIONS"
        let functions = Function.parseAll <| getSection true "_VMA_FUNCTION_HEADERS"

        { enums     = enums
          handles   = handles
          structs   = structs
          functions = functions }

module Writer =
    let private builder = StringBuilder()
    let mutable private indent = 0

    let private ln() =
        builder.AppendLine() |> ignore

    let private fn fmt =
        let indent = String.replicate indent "    "
        Printf.kprintf (fun str ->
            let str = indent + str
            builder.AppendLine(str) |> ignore
        ) fmt

    let private blk fmt (f: unit -> unit) =
        fn fmt
        indent <- indent + 1
        f()
        indent <- indent - 1

    let header (header : VmaHeader) =
        let arrayStructs = ResizeArray<Type * int>()

        fn "namespace Aardvark.Rendering.Vulkan.Memory"
        ln()
        fn "open Aardvark.Base"
        fn "open Aardvark.Rendering.Vulkan"
        fn "open FSharp.NativeInterop"
        fn "open System"
        fn "open System.Diagnostics"
        fn "open System.Security"
        fn "open System.Runtime.InteropServices"
        fn "open Vulkan11"
        ln()
        fn "#nowarn \"9\""
        fn "#nowarn \"51\""

        for e in header.enums do
            ln()
            if e.isFlags then fn "[<Flags>]"
            blk $"type {e.name} =" (fun _ ->
                for c in e.cases do
                    fn $"| {c.name} = {c.value}"
            )

        for h in header.handles do
            ln()
            if h.dispatchable then
                fn $"type {h.name} = nativeint"
            else
                fn "[<StructLayout(LayoutKind.Sequential)>]"
                blk $"type {h.name} =" (fun _ ->
                    blk "struct" (fun _ ->
                        fn "val mutable public Handle : uint64"
                        fn "new(h) = { Handle = h }"
                        fn $"static member Null = {h.name}(0UL)"
                        fn "member x.IsNull = x.Handle = 0UL"
                        fn "member x.IsValid = x.Handle <> 0UL"
                    )
                    fn "end"
                )

        for s in header.structs do
            ln()
            match s with
            | Struct (name, fields) ->
                fn "[<Struct; StructLayout(LayoutKind.Sequential)>]"
                blk $"type {name} =" (fun _ ->
                    blk "{" (fun _ ->
                        for f in fields do
                            fn $"mutable {f.name} : {Type.fsharpName f.count f.typ}"
                    )
                    fn "}"
                    ln()
                    blk $"static member Empty : {name} =" (fun _ ->
                        blk "{" (fun _ ->
                            for f in fields do
                                fn $"{f.name} = Unchecked.defaultof<{Type.fsharpName f.count f.typ}>"
                        )
                        fn "}"
                    )
                )

            | Array (typ, count) ->
                arrayStructs.Add(typ, count)
                let baseName = Type.fsharpName 1 typ

                fn $"[<StructLayout(LayoutKind.Explicit, Size = {count} * {Type.getSizeInBytes typ})>]"
                blk $"type {Type.fsharpName count typ} =" (fun _ ->
                    blk "struct" (fun _ ->
                        blk "member x.Item" (fun _ ->
                            blk $"with get (index: int) : {baseName} =" (fun _ ->
                                fn $"if index < 0 || index > {count - 1} then raise <| IndexOutOfRangeException()"
                                fn $"let ptr = NativePtr.cast &&x"
                                fn $"ptr.[index]"
                            )
                            blk $"and set (index: int) (value: {baseName}) =" (fun _ ->
                                fn $"if index < 0 || index > {count - 1} then raise <| IndexOutOfRangeException()"
                                fn $"let ptr = NativePtr.cast &&x"
                                fn $"ptr.[index] <- value"
                            )
                        )

                        ln()
                        fn $"member x.Length = {count}"

                        ln()
                        blk "interface System.Collections.IEnumerable with" (fun _ ->
                            fn $"member x.GetEnumerator() = let x = x in (Seq.init {count} (fun i -> x.[i])).GetEnumerator() :> System.Collections.IEnumerator"
                        )

                        ln()
                        blk $"interface System.Collections.Generic.IEnumerable<{baseName}> with" (fun _ ->
                            fn $"member x.GetEnumerator() = let x = x in (Seq.init {count} (fun i -> x.[i])).GetEnumerator()"
                        )
                    )
                    fn "end"
                )

        ln()
        fn "[<SuppressUnmanagedCodeSecurity>]"
        blk "module Vma =" (fun _ ->
            fn "[<Literal>]"
            fn "let private lib = \"vkvm\""

            if arrayStructs.Count > 0 then
                ln()
                fn "#if DEBUG"
                blk "do" (fun _ ->
                    for t, n in arrayStructs do
                       let name = Type.fsharpName n t
                       let baseName = Type.fsharpName 1 t
                       fn $"Debug.Assert(sizeof<{name}> = sizeof<{baseName}> * {n}, $\"Unexpected size for {name}, expected {{sizeof<{baseName}> * {n}}} but got {{sizeof<{name}>}}.\")"
                )
                fn "#endif"

            for f in header.functions do
                ln()
                let p = f.parameters |> Array.map (fun p -> $"{Type.externName p.typ} {p.name}") |> String.concat ", "
                fn $"[<DllImport(lib, EntryPoint = \"{f.entryPoint}\")>]"
                fn $"extern {Type.externName f.returnType} {f.name}({p})"
        )
        builder.ToString()

let run (outputPath: string) =
    let vma = VmaHeader.read()
    let output = Writer.header vma

    File.WriteAllText(outputPath, output)
    printfn $"Generated '{Path.GetFileName outputPath}'"

fsi.ShowDeclarationValues <- false
run <| Path.Combine(__SOURCE_DIRECTORY__, "VMA.fs")