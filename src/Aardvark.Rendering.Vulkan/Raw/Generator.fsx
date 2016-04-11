#r "System.Xml.dll"
#r "System.Xml.Linq.dll"

open System.Xml.Linq
open System.IO
open System.Text.RegularExpressions

//type float32_2 = struct end
//type float32_4 = struct end
//type int_4 = struct end
//type uint32_4 = struct end
//type uint32_3 = struct end
//type uint32_2 = struct end
//type byte_16 = struct end
//type String256 = struct end
//type VkMemoryType_32 = struct end
//type VkMemoryHeap_16 = struct end

//type VkCmdBufferCreateFlags = int
//type VkEventCreateFlags = int
//type VkSemaphoreCreateFlags = int
//type VkShaderCreateFlags = int
//type VkShaderModuleCreateFlags = int


[<AutoOpen>]
module XmlStuff = 
    let xname s = XName.op_Implicit s
    let attrib (e : XElement) s = 
        let e = e.Attribute (xname s) 
        if e = null then None
        else Some e.Value

    let child (e : XElement) n =
        match e.Element(xname n) with
            | null -> None
            | e -> Some e.Value

    let private numericValue = Regex @"^(?<value>.*?)(f|U|ULL)?$"

    let rec toInt32 (v : string) (b : int) =
        if v.StartsWith "(" && v.EndsWith ")" then
            toInt32 (v.Substring(1, v.Length-2)) b
        elif v.StartsWith "~" then
            ~~~(toInt32 (v.Substring(1)) b)
        elif v.StartsWith "0x" then
            toInt32 (v.Substring(2)) 16
        
        else
            let m = numericValue.Match v
        
            if m.Success then
                let v = m.Groups.["value"].Value
                if v.Contains "." then
                    
                    System.Convert.ToSingle(v, System.Globalization.CultureInfo.InvariantCulture.NumberFormat) |> int
                else
                    System.Convert.ToInt32(v, b)
            else
                failwithf "not a number: %A" v


    let (|Enum|BitMask|Failure|) (e : XElement) = 
        match attrib e "value", attrib e "bitpos" with
              | Some v,_ ->
                    let v =  toInt32 v 10
//                            if v.Contains "x" then System.Convert.ToInt32(v, 16)
//                            elif v.EndsWith "f" then System.Convert.ToSingle(v.Substring(0, v.Length-1)) |> int
//                            elif v.[0] = '(' && v.[v.Length-1] = ')' then
//                                let v = v.Substring(1, v.Length-2)
//                                if v.StartsWith "~" then 
//                                    ~~~System.Convert.ToInt32(v.Substring(1), 16)
//                                else
//                                    System.Convert.ToInt32(v.Substring(1), 16)
//                                    
//                            else System.Convert.ToInt32(v, 10)
                    Enum v
              | _, Some bp -> 
                    BitMask (System.Int32.Parse bp)
              | _ -> Failure


type Type =
    | Literal of string
    | Ptr of Type
    | FixedArray of Type * int

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Type =
    let private cleanRx = Regex @"([ \t\r\n]+|const)"
    let private typeRx = Regex @"(?<name>[a-zA-Z_0-9]+)(\[(?<arr>.*)\])?(?<ptr>[\*]*)"
    let private arrRx = Regex @"(?<name>[^\[]*)(\[(?<size>.*)\])?"

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

    let parseTypeAndName (defined : Map<string, string>) (strangeType : string) (strangeName : string) =
        let cleaned = cleanRx.Replace(strangeType, "")
        let m = typeRx.Match cleaned



        if m.Success then
            let id = m.Groups.["name"].Value
            let ptr = m.Groups.["ptr"].Length

            let mutable t = Literal id
            for i in 1..ptr do
                t <- Ptr(t)

            let arrMatch = arrRx.Match strangeName
            if arrMatch.Success then
                let n = arrMatch.Groups.["name"].Value
                let s = arrMatch.Groups.["size"].Value

                let arrType = 
                    match s with
                        | "" -> t
                        | _ -> FixedArray(t, arraySize defined s)

                if m.Groups.["arr"].Success then
                    FixedArray(arrType, arraySize defined m.Groups.["arr"].Value), n
                else
                    arrType, n
            else
                failwith "invalid name"

        else
            failwith "bad"

    let rec baseType (t : Type) =
        match t with
            | Literal t -> t
            | Ptr(t) -> baseType t
            | FixedArray(t,_) -> baseType t

    let rec isPointer (t : Type) =
        match t with
            | Ptr(_) -> true
            | _ -> false

    let readXmlTypeAndName (defined : Map<string, string>) (e : XElement) =
        let strangeName = e.Element(xname "name").Value
        let strangeType = 
            let v = e.Value.Trim()
            let id = v.LastIndexOf(strangeName)
            if id < 0 then v
            else v.Substring(0, id).Trim() + v.Substring(id + strangeName.Length)

        parseTypeAndName defined strangeType strangeName

type EnumValue = 
    | EnumValue of int
    | EnumBit of int

type Enum = { name : string; alternatives : list<string * EnumValue> }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Enum =
    let tryRead (e : XElement) =
        match attrib e "name" with
            | Some name ->
                let alternatives = 
                    e.Descendants(xname "enum")
                        |> Seq.map (fun kv ->
                            let name = (attrib kv "name").Value
                            match kv with
                                | Enum value -> name, EnumValue value
                                | BitMask bit -> name, EnumBit bit
                                | _ -> failwithf "invalid enum-value: %A" kv         
                           )
                        |> Seq.toList

                Some { name = name; alternatives = alternatives }
            | None -> None


type Struct = { name : string; fields : list<Type * string>; isUnion : bool }
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Struct =
    let tryRead (defines : Map<string, string>) (e : XElement) =
        let isUnion = attrib e "category" = Some "union"
        match attrib e "name" with
            | Some name ->
                
                let fields = 
                    e.Descendants (xname "member")
                        |> Seq.map (fun m ->
                            let name = m.Element(xname "name").Value
                            let t = 
                                let v = m.Value.Trim()
                                let id = v.LastIndexOf(name)
                                if id < 0 then v
                                else v.Substring(0, id).Trim() + v.Substring(id + name.Length)

                            Type.parseTypeAndName defines t name
                           )
                        |> Seq.toList

                Some { name = name; fields = fields; isUnion = isUnion }

            | None -> None

    let private dfs (graph : Map<'a, list<'a>>) visited start_node = 
      let rec explore path visited node = 
        if List.exists (fun n -> n = node) path    then failwith "cycle" else
        if List.exists (fun n -> n = node) visited then visited else     
          let new_path = node :: path in 
          let edges    = Map.find node graph in
          let visited  = List.fold (explore new_path) visited edges in
          node :: visited
      in explore [] visited start_node

    let private toposort graph = 
      List.fold (fun visited (node,_) -> dfs graph visited node) [] (Map.toList graph)

    let topologicalSort (s : list<Struct>) : list<Struct> =

        let typeMap = s |> List.map (fun s -> s.name, s) |> Map.ofList

        let graph =
            s |> List.map (fun s -> 
                    let usedTypes = 
                        s.fields 
                          |> List.map fst 
                          |> List.map Type.baseType 
                          |> List.choose (fun m -> Map.tryFind m typeMap)

                    s, usedTypes

                 )
               |> Map.ofList

        toposort graph |> List.rev

type Alias = { baseType : Type; name : string }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Alias =
    let tryRead (defines : Map<string, string>) (e : XElement) =
        match child e "name", child e "type" with
            | Some name, Some t ->
                let (t, n) = Type.parseTypeAndName defines t name
                Some { baseType = t; name = n }
            | _ -> None


type Command = { returnType : Type; name : string; parameters : list<Type * string> }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Command =
    let tryRead (defines : Map<string, string>) (e : XElement) : Option<Command> =
        let proto = e.Element(xname "proto")
        let (returnType,name) = Type.readXmlTypeAndName defines proto

        let parameters =
            e.Elements(xname "param")
                |> Seq.map (Type.readXmlTypeAndName defines)
                |> Seq.toList


        Some { returnType = returnType; name = name; parameters = parameters }

module XmlReader =
    let defines (d : XElement) =
        d.Descendants(xname "enums") 
            |> Seq.filter (fun e -> attrib e "name" = Some "API Constants") 
            |> Seq.collect (fun e ->
                  let choices = e.Descendants(xname "enum")
                  choices |> Seq.choose (fun c ->
                    match attrib c "name", attrib c "value" with
                        | Some name, Some value -> Some(name, value)
                        | _ -> None
                  )
              )
            |> Map.ofSeq

    let enums (vk : XElement) =
        vk.Descendants(xname "enums")
            |> Seq.filter (fun e -> attrib e "name" <> Some "API Constants") 
            |> Seq.choose Enum.tryRead
            |> Seq.toList

    let structs (defines : Map<string, string>) (vk : XElement) =
        vk.Descendants(xname "types")
            |> Seq.collect (fun tc -> tc.Descendants (xname "type"))
            |> Seq.filter (fun t -> attrib t "category" = Some "struct" || attrib t "category" = Some "union")
            |> Seq.choose (Struct.tryRead defines)
            |> Seq.toList

    let aliases (defines : Map<string, string>) (vk : XElement) =
        vk.Descendants(xname "types")
            |> Seq.collect (fun tc -> tc.Descendants (xname "type"))
            |> Seq.filter (fun t -> attrib t "category" = Some "basetype")
            |> Seq.choose (Alias.tryRead defines)
            |> Seq.toList

    let handles (vk : XElement) =
        vk.Descendants(xname "types")
            |> Seq.collect (fun tc -> tc.Descendants (xname "type"))
            |> Seq.filter (fun t -> attrib t "category" = Some "handle")
            |> Seq.choose (fun e -> match child e "name" with | Some name -> Some((e.Value.Contains "NON_DISPATCHABLE"),name) | _ -> None)
            |> Seq.toList

    let commands (defines : Map<string, string>) (vk : XElement) =
        vk.Descendants(xname "commands")
            |> Seq.collect (fun tc -> tc.Descendants (xname "command"))
            |> Seq.choose (Command.tryRead defines)
            |> Seq.toList

module FSharpWriter =

    open System.Text.RegularExpressions

    let private uppercase = Regex @"[A-Z0-9]+"
    let private startsWithNumber = Regex @"^[0-9]+"
    let private removePrefix (p : string) (str : string) =
        if str.StartsWith p then str.Substring(p.Length)
        else str

    let private avoidStartWithNumer (str : string) =
        if startsWithNumber.IsMatch str then "D" + str
        else str

    let capsToCamelCase (prefix : string) (str : string) =
        let matchCollection = uppercase.Matches str
        let matches = seq { for m in matchCollection do yield m.Value }
        matches
            |> Seq.map (fun m -> m.Substring(0, 1) + m.Substring(1).ToLower())
            |> String.concat ""
            |> removePrefix prefix
            |> avoidStartWithNumer

    let addNoneCase (cases : list<string * EnumValue>) =
        let hasNoneCase =
            cases |> List.exists (fun (_,v) -> 
                match v with
                    | EnumValue 0 -> true
                    | EnumBit -1 -> true
                    | _ -> false
            )

        if not hasNoneCase then
            ("None", EnumValue 0)::cases
        else
            cases

    let isFlags (cases : list<string * EnumValue>) =
        cases |> List.exists (fun (_,v) ->
            match v with
                | EnumBit _ -> true
                | _ -> false
        )

    let builder = System.Text.StringBuilder()

    let printfn fmt =
        Printf.kprintf (fun str -> System.Console.WriteLine(str); builder.AppendLine(str) |> ignore) fmt

    let header() =
        printfn "namespace Aardvark.Rendering.Vulkan"
        printfn ""
        printfn "open System"
        printfn "open System.Runtime.InteropServices"
        printfn "open System.Runtime.CompilerServices"
        printfn "open Microsoft.FSharp.NativeInterop"
        printfn "open Aardvark.Base"
        printfn ""
        printfn "#nowarn \"9\""
        printfn "#nowarn \"51\""

        printfn "type PFN_vkAllocationFunction = nativeint"
        printfn "type PFN_vkReallocationFunction = nativeint"
        printfn "type PFN_vkInternalAllocationNotification = nativeint"
        printfn "type PFN_vkInternalFreeNotification = nativeint"
        printfn "type PFN_vkFreeFunction = nativeint "
        printfn "type PFN_vkVoidFunction = nativeint"
        printfn ""


    let missing() =
        printfn "// missing in vk.xml"
        printfn "type VkCmdBufferCreateFlags = uint32"
        printfn "type VkEventCreateFlags = uint32"
        printfn "type VkSemaphoreCreateFlags = uint32"
        printfn "type VkShaderCreateFlags = uint32"
        printfn "type VkShaderModuleCreateFlags = uint32"
        printfn "type VkMemoryMapFlags = uint32"

        // missing since new version
        printfn "type VkDisplayPlaneAlphaFlagsKHR = uint32"
        printfn "type VkDisplaySurfaceCreateFlagsKHR = uint32"
        printfn "type VkSwapchainCreateFlagsKHR = uint32"
        printfn "type VkSurfaceTransformFlagsKHR = uint32"
        printfn "type VkCompositeAlphaFlagsKHR = uint32"

        printfn "type VkPipelineLayoutCreateFlags = uint32"
        printfn "type VkBufferViewCreateFlags = uint32"
        printfn "type VkPipelineShaderStageCreateFlags = uint32"
        printfn "type VkDescriptorSetLayoutCreateFlags = uint32"
        printfn "type VkDeviceQueueCreateFlags = uint32"
        printfn "type VkInstanceCreateFlags = uint32"
        printfn "type VkImageViewCreateFlags = uint32"
        printfn "type VkDeviceCreateFlags = uint32"
        printfn "type VkFramebufferCreateFlags = uint32"
        printfn "type VkDescriptorPoolResetFlags = uint32"
        printfn "type VkPipelineVertexInputStateCreateFlags = uint32"
        printfn "type VkPipelineInputAssemblyStateCreateFlags = uint32"
        printfn "type VkPipelineTesselationStateCreateFlags = uint32"
        printfn "type VkPipelineViewportStateCreateFlags = uint32"
        printfn "type VkPipelineRasterizationStateCreateFlags = uint32"
        printfn "type VkPipelineMultisampleStateCreateFlags = uint32"
        printfn "type VkPipelineDepthStencilStateCreateFlags = uint32"
        printfn "type VkPipelineColorBlendStateCreateFlags = uint32"
        printfn "type VkPipelineDynamicStateCreateFlags = uint32"
        printfn "type VkPipelineCacheCreateFlags = uint32"
        printfn "type VkQueryPoolCreateFlags = uint32"
        printfn "type VkSubpassDescriptionFlags = uint32"
        printfn "type VkRenderPassCreateFlags = uint32"
        printfn "type VkSamplerCreateFlags = uint32"
        
        printfn ""
        printfn "type VkAndroidSurfaceCreateFlagsKHR = uint32"
        printfn "type VkDisplayModeCreateFlagsKHR = uint32"
        printfn "type VkPipelineTessellationStateCreateFlags = uint32"
        printfn "type VkXcbSurfaceCreateFlagsKHR = uint32"
        printfn "type VkXlibSurfaceCreateFlagsKHR = uint32"
        printfn "type VkWin32SurfaceCreateFlagsKHR = uint32"
        printfn "type VkWaylandSurfaceCreateFlagsKHR = uint32"
        printfn "type VkMirSurfaceCreateFlagsKHR = uint32"

        printfn "type VkDebugReportFlagsEXT = uint32"
        printfn "type PFN_vkDebugReportCallbackEXT = nativeint"
        printfn ""

    let extendedEnums() =
        printfn "[<AutoOpen>]"
        printfn "module WSIEnums = "
        printfn "    type VkStructureType with"
        printfn "        static member XLibSurfaceCreateInfo = unbox<VkStructureType> 1000004000"
        printfn "        static member XcbSurfaceCreateInfo = unbox<VkStructureType> 1000005000"
        printfn "        static member WaylandSurfaceCreateInfo = unbox<VkStructureType> 1000006000"
        printfn "        static member MirSurfaceCreateInfo = unbox<VkStructureType> 1000007000"
        printfn "        static member AndroidSurfaceCreateInfo = unbox<VkStructureType> 1000008000"
        printfn "        static member Win32SurfaceCreateInfo = unbox<VkStructureType> 1000009000"
//        VK_STRUCTURE_TYPE_XLIB_SURFACE_CREATE_INFO_KHR = 1000004000,
//        VK_STRUCTURE_TYPE_XCB_SURFACE_CREATE_INFO_KHR = 1000005000,
//        VK_STRUCTURE_TYPE_WAYLAND_SURFACE_CREATE_INFO_KHR = 1000006000,
//        VK_STRUCTURE_TYPE_MIR_SURFACE_CREATE_INFO_KHR = 1000007000,
//        VK_STRUCTURE_TYPE_ANDROID_SURFACE_CREATE_INFO_KHR = 1000008000,
//        VK_STRUCTURE_TYPE_WIN32_SURFACE_CREATE_INFO_KHR = 1000009000,
    let defines (map : Map<string, string>) =
        printfn "module Defines = "
        for (n,v) in Map.toSeq map do
            printfn "    [<Literal>]"
            let v = v.Replace("~", "~~~").Replace("U", "u").Replace("uLL", "UL").Replace("uL", "UL")
            printfn "    let %s = %s" n v
        printfn ""

    let enums (enums : list<Enum>) =
        for e in enums do
            let name =
                if e.name.EndsWith "FlagBits" then e.name.Substring(0, e.name.Length - 8) + "Flags"
                else e.name

            let prefix =
                if name.EndsWith "Flags" then name.Substring(0,name.Length-5)
                else name

            let alternatives = e.alternatives |> List.map (fun (n,v) -> (capsToCamelCase prefix n, v))

            let isFlag = isFlags alternatives
            let alternatives = 
                if isFlag then addNoneCase alternatives
                else alternatives

            if isFlag then
                printfn "[<Flags>]"
            printfn "type %s = " name


            for (n,v) in alternatives do
                match v with
                    | EnumValue v   -> printfn "    | %s = %d" n v
                    | EnumBit b     -> printfn "    | %s = 0x%08X" n (1 <<< b)
            printfn ""

    let knownTypes =
        Map.ofList [
            "uint32_t", "uint32"
            "int32_t", "int"
            "float", "float32"
            "uint64_t", "uint64"
            "uint64_t", "uint64"
            "size_t", "uint64"
            "char", "byte"
            "uint8_t", "byte"

            // for extern stuff only
            "void", "void"

        ]

    let reservedKeywords = Set.ofList ["module"; "type"; "object"]

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

            | Ptr(Literal "char") -> "cstr"
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
                            elif n = "ANativeWindow" || n = "HINSTANCE" || n = "HWND" || n = "Display" || n = "Window" || n = "VisualID" || n = "xcb_connection_t"  || n = "xcb_window_t"  || n = "xcb_visualid_t"then "nativeint"
                            elif n.StartsWith "Mir" || n.StartsWith "struct" then "nativeint"
                            else failwithf "strange type: %A" n
            | Ptr t ->
                sprintf "nativeptr<%s>" (typeName t)
            | FixedArray(t, s) ->
                let t = typeName t
                sprintf "%s_%d" t s

    let inlineArray (baseType : string) (baseTypeSize : int) (size : int) =
        let totalSize = size * baseTypeSize
        printfn "[<StructLayout(LayoutKind.Explicit, Size = %d)>]" totalSize
        printfn "type %s_%d =" baseType size
        printfn "    struct"
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
        printfn ""

    let structs (structs : list<Struct>) =
        for s in structs do
            if s.isUnion then printfn "[<StructLayout(LayoutKind.Explicit)>]"
            else printfn "[<StructLayout(LayoutKind.Sequential)>]"

            printfn "type %s = " s.name
            printfn "    struct"
            for (t, n) in s.fields do
                let n = fsharpName n

                if s.isUnion then
                    printfn "        [<FieldOffset(0)>]"

                printfn "        val mutable public %s : %s" n (typeName t)
                ()

            let fieldDefs = s.fields |> List.map (fun (t,n) -> sprintf "%s : %s" (fsharpName n) (typeName t)) |> String.concat ", "
            let fieldAss = s.fields |> List.map (fun (_,n) -> sprintf "%s = %s" (fsharpName n) (fsharpName n)) |> String.concat "; "

            if not s.isUnion then
                printfn ""
                printfn "        new(%s) = { %s }" fieldDefs fieldAss

                if s.name = "VkExtent3D" then
                    printfn "        new(w : int, h : int, d : int) = VkExtent3D(uint32 w,uint32 h,uint32 d)" 
                elif s.name = "VkExtent2D" then
                    printfn "        new(w : int, h : int) = VkExtent2D(uint32 w,uint32 h)" 


            let fieldSplice = s.fields |> List.map (fun (t,n) -> sprintf "%s = %%A" (fsharpName n)) |> String.concat "; "
            let fieldAccess = s.fields |> List.map (fun (_,n) -> sprintf "x.%s" (fsharpName n)) |> String.concat " "


            printfn "        override x.ToString() ="
            printfn "            sprintf \"%s { %s }\" %s" s.name fieldSplice fieldAccess


            printfn "    end"
            printfn ""

            if s.name = "VkMemoryHeap" then
                inlineArray "VkMemoryHeap" 16 16
            elif s.name = "VkMemoryType" then
                inlineArray "VkMemoryType" 8 32
            elif s.name = "VkOffset3D" then
                inlineArray "VkOffset3D" 12 2

    let aliases (l : list<Alias>) =
        for a in l do
            printfn "type %s = %s" a.name (typeName a.baseType)


    let handles (l : list<bool * string>) =
        for (nodisp, n) in l do
            if nodisp then
                printfn "[<StructLayout(LayoutKind.Sequential)>]"
                printfn "type %s = " n
                printfn "    struct"
                printfn "        val mutable public Handle : uint64"
                printfn "        new(h) = { Handle = h }"
                printfn "        static member Null = %s(0UL)" n
                printfn "        member x.IsNull = x.Handle = 0UL"
                printfn "        member x.IsValid = x.Handle <> 0UL"
                printfn "    end"
                printfn ""
            else
                printfn "type %s = nativeint" n
        printfn ""

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
                            elif n = "ANativeWindow" || n = "HINSTANCE" || n = "HWND" || n = "Display" || n = "Window" || n = "VisualID" || n = "xcb_connection_t" || n = "xcb_window_t" || n = "xcb_visualid_t" then "nativeint"
                            elif n.StartsWith "Mir" || n.StartsWith "struct" then "nativeint"
                            else failwithf "strange type: %A" n
            | Ptr t ->
                sprintf "%s*" (externTypeName t)
            | FixedArray(t, s) ->
                let t = externTypeName t
                sprintf "%s_%d" t s

    let commands (l : list<Command>) =
        printfn "module VkRaw = "
        printfn "    [<Literal>]"
        printfn "    let lib = \"vulkan-1-1-0-8-0.dll\""
        printfn ""
        for c in l do
            let args = c.parameters |> List.map (fun (t,n) -> sprintf "%s %s" (externTypeName t) (fsharpName n)) |> String.concat ", "
            printfn "    [<DllImport(lib)>]"
            printfn "    extern %s %s(%s)" (externTypeName c.returnType) c.name args



let run () = 
    let vk = XElement.Load(@"C:\VulkanSDK\1.0.8.0\vk.xml")
    
    let defines = XmlReader.defines vk
    let enums = XmlReader.enums vk
    let structs = XmlReader.structs defines vk
    let aliases = XmlReader.aliases defines vk
    let handles = XmlReader.handles vk
    let commands = XmlReader.commands defines vk

    FSharpWriter.header()
    FSharpWriter.missing()
    FSharpWriter.handles handles
    FSharpWriter.aliases aliases
    FSharpWriter.enums enums
    FSharpWriter.structs (Struct.topologicalSort structs)
    FSharpWriter.extendedEnums()
    FSharpWriter.commands commands
    let str = FSharpWriter.builder.ToString()


    let file = Path.Combine(__SOURCE_DIRECTORY__, "Vulkan.fs")

    File.WriteAllText(file, str)


module PCI =
    open System
    open System.IO
    let builder = System.Text.StringBuilder()

    let printfn fmt =
        Printf.kprintf (fun str -> System.Console.WriteLine(str); builder.AppendLine(str) |> ignore) fmt


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
        let file = Path.Combine(__SOURCE_DIRECTORY__, "PCI.fs")
        File.WriteAllText(file, str)

do run()
