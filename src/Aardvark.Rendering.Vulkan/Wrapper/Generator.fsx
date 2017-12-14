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
        e.Elements(xname "comment").Remove()
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
                match alternatives with
                    | [] -> 
                        None
                    | _ -> 
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
                            m.Elements(xname "comment").Remove()

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

    let toposort graph = 
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

type Extension =
    {
        name            : string
        extName         : string
        number          : int
        requires        : list<string>
        enumExtensions  : Map<string, list<string * int>>
        enums           : list<Enum>
        structs         : list<Struct>
        commands        : list<Command>
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Extension =
    let private dummyRx = System.Text.RegularExpressions.Regex @"VK_[A-Za-z]+_extension_([0-9]+)"
    
    let isEmpty (e : Extension) =
        dummyRx.IsMatch e.name && Map.isEmpty e.enumExtensions && List.isEmpty e.enums && List.isEmpty e.structs && List.isEmpty e.commands

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

    let extensions (vk : XElement) (commands : list<Command>) (enums : list<Enum>) (structs : list<Struct>) =
        let mutable commands = commands
        let mutable enums = enums
        let mutable structs = structs

        let removeCmd (name : string) =
            let mutable result = None

            commands <-
                commands |> List.filter(fun cmd ->
                    if cmd.name = name then
                        result <- Some cmd
                        false
                    else
                        true
                )

            result

        let removeStruct (name : string) =
            let mutable result = None

            structs <-
                structs |> List.filter(fun cmd ->
                    if cmd.name = name then
                        result <- Some cmd
                        false
                    else
                        true
                )

            result

        let removeEnum (name : string) =
            let mutable result = None

            enums <-
                enums |> List.filter(fun cmd ->
                    if cmd.name = name then
                        result <- Some cmd
                        false
                    else
                        true
                )

            result

        let extensionEnumValue (dir : Option<string>) (e : int) (offset : int) =
            match dir with
                | Some "-" -> 
                    -(1000000000 + (e - 1) * 1000 + offset)
                | _ ->
                    1000000000 + (e - 1) * 1000 + offset

        let extensions =
            [
                for e in vk.Descendants(xname "extensions").Descendants(xname "extension") do
                    let require = e.Descendants(xname "require") |> Seq.tryHead
                    let name = attrib e "name"
                    let number = attrib e "number"
                    match require, name, number with
                        | Some r, Some name, Some number ->
                            let number = System.Int32.Parse number

                            let requires = 
                                match attrib e "requires" with
                                    | Some v -> v.Split(',') |> Array.toList
                                    | None -> []



                            let enumExtensions =
                                r.Descendants(xname "enum") |> Seq.toList |> List.choose (fun e -> 
                                    match attrib e "extends", attrib e "offset", attrib e "name" with
                                        | Some baseType, Some offset, Some name ->
                                            let value = extensionEnumValue (attrib e "dir") number (System.Int32.Parse offset)
                                            Some (baseType, name, value)    
                                        | _ ->
                                            None
                                )


                            let types = 
                                r.Descendants(xname "type") 
                                    |> Seq.toList
                                    |> List.choose (fun t ->
                                        match attrib t "name" with  
                                            | Some name -> 
                                                match removeEnum name with
                                                    | Some e -> Some (Choice1Of2 e)
                                                    | None ->
                                                        match removeStruct name with
                                                            | Some s -> Some (Choice2Of2 s)
                                                            | None -> None
                                            | None ->
                                                None
                                    )

                            let commands =
                                r.Descendants(xname "command")
                                    |> Seq.toList
                                    |> List.choose (fun c ->
                                        match attrib c "name" with 
                                            | Some name -> removeCmd name
                                            | None -> None
                                    )

                            let enums = types |> List.choose (function Choice1Of2 e -> Some e | _ -> None)
                            let structs = types |> List.choose (function Choice2Of2 e -> Some e | _ -> None)



                    
                            let groups = 
                                enumExtensions |> List.groupBy (fun (b,_,_) -> b) |> List.map (fun (g,l) -> g, l |> List.map (fun (_,a,b) -> a, b)) |> Map.ofList


                            yield {
                                name            = name
                                extName         = name
                                requires        = requires
                                number          = number
                                enumExtensions  = groups
                                enums           = enums
                                structs         = structs
                                commands        = commands
                            }

                        | _ ->
                            ()
                ]
            
        

        extensions, commands, enums, structs
        
        


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
        printfn "#nowarn \"1337\""
        printfn ""
        printfn "open System"
        printfn "open System.Runtime.InteropServices"
        printfn "open System.Runtime.CompilerServices"
        printfn "open Microsoft.FSharp.NativeInterop"
        printfn "open System.Security"
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
        printfn "type VkExternalMemoryHandleTypeFlagsNV = uint32"
        printfn "type VkExternalMemoryFeatureFlagsNV = uint32"
        printfn "type VkIndirectCommandsLayoutUsageFlagsNVX = uint32"
        printfn "type VkObjectEntryUsageFlagsNVX = uint32"
        printfn ""
        printfn "type VkDescriptorUpdateTemplateCreateFlagsKHR = uint32"
        printfn "type VkDeviceGroupPresentModeFlagsKHX = uint32"
        printfn "type VkExternalFenceHandleTypeFlagsKHR = uint32"
        printfn "type VkExternalMemoryHandleTypeFlagsKHR = uint32"
        printfn "type VkExternalSemaphoreHandleTypeFlagsKHR = uint32"
        printfn "type VkExternalMemoryFeatureFlagsKHR = uint32"
        printfn "type VkExternalFenceFeatureFlagsKHR = uint32"
        printfn "type VkExternalSemaphoreFeatureFlagsKHR = uint32"
        printfn "type VkIOSSurfaceCreateFlagsMVK = uint32"
        printfn "type VkFenceImportFlagsKHR = uint32"
        printfn "type VkSemaphoreImportFlagsKHR = uint32"
        printfn "type VkMacOSSurfaceCreateFlagsMVK = uint32"
        printfn "type VkMemoryAllocateFlagsKHX = uint32"
        printfn "type VkPipelineCoverageModulationStateCreateFlagsNV = uint32"
        printfn "type VkPipelineCoverageToColorStateCreateFlagsNV = uint32"
        printfn "type VkPipelineDiscardRectangleStateCreateFlagsEXT = uint32"
        printfn "type VkPipelineViewportSwizzleStateCreateFlagsNV = uint32"
        printfn "type VkSurfaceCounterFlagsEXT = uint32"
        printfn "type VkValidationCacheCreateFlagsEXT = uint32"
        printfn "type VkViSurfaceCreateFlagsNN = uint32"
        printfn "type VkPeerMemoryFeatureFlagsKHX = uint32"
        printfn "type VkCommandPoolTrimFlagsKHR = uint32"

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
    let defines (map : Map<string, string>) =
        printfn "module Defines = "
        for (n,v) in Map.toSeq map do
            printfn "    [<Literal>]"
            let v = v.Replace("~", "~~~").Replace("U", "u").Replace("uLL", "UL").Replace("uL", "UL")
            printfn "    let %s = %s" n v
        printfn ""

    let cleanEnumName (e : string) =
        if e.EndsWith "FlagBits" then e.Substring(0, e.Length - 8) + "Flags"
        else e

    let enumExtensions (name : string) (exts : list<string * int>) =
        let name = cleanEnumName name
        
        let prefix =
            if name.EndsWith "Flags" then name.Substring(0,name.Length-5)
            else name

        let exts = exts |> List.map (fun (n,v) -> (capsToCamelCase prefix n, v))

        printfn "    type %s with" name
        for (n,v) in exts do
            printfn "         static member inline %s = unbox<%s> %d" n name v

    let enums (indent : string) (enums : list<Enum>) =
        for e in enums do
            let name = cleanEnumName e.name

            let prefix =
                if name.EndsWith "Flags" then name.Substring(0,name.Length-5)
                else name

            let alternatives = e.alternatives |> List.map (fun (n,v) -> (capsToCamelCase prefix n, v))

            let isFlag = isFlags alternatives
            let alternatives = 
                if isFlag then addNoneCase alternatives
                else alternatives

            if isFlag then
                printfn "%s[<Flags>]" indent
            printfn "%stype %s = " indent name


            for (n,v) in alternatives do
                match v with
                    | EnumValue v   -> printfn "%s    | %s = %d" indent n v 
                    | EnumBit b     -> printfn "%s    | %s = 0x%08X" indent n (1 <<< b)
            printfn "%s" indent

    let knownTypes =
        Map.ofList [
            "uint32_t", "uint32"
            "int32_t", "int"
            "int", "int"
            "float", "float32"
            "uint64_t", "uint64"
            "uint64_t", "uint64"
            "size_t", "uint64"
            "char", "byte"
            "uint8_t", "byte"

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

    let reservedKeywords = Set.ofList ["module"; "type"; "object"; "SFRRectCount"]

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
                            elif n.StartsWith "Mir" || n.StartsWith "struct" then "nativeint"
                            else "nativeint" //failwithf "strange type: %A" n
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
        printfn "        [<FieldOffset(0)>]"
        printfn "        val mutable public First : %s" baseType
        printfn "        "
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

    let structs (indent : string) (structs : list<Struct>) =

        let printfn fmt =
            Printf.kprintf (fun str -> 
                printfn "%s%s" indent str
            ) fmt

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

    let globalStructs (s : list<Struct>) =
        inlineArray "uint32" 4 32
        inlineArray "byte" 1 8
        inlineArray "VkPhysicalDevice" 8 32
        structs "" s

    let aliases (l : list<Alias>) =
        for a in l do
            printfn "type %s = %s" a.name (typeName a.baseType)


    let handles (l : list<bool * string>) =
        for (nodisp, n) in l do
            if nodisp then
                printfn "[<StructLayout(LayoutKind.Sequential)>]"
                printfn "type %s = " n
                printfn "    struct"
                printfn "        val mutable public Handle : int64"
                printfn "        new(h) = { Handle = h }"
                printfn "        static member Null = %s(0L)" n
                printfn "        member x.IsNull = x.Handle = 0L"
                printfn "        member x.IsValid = x.Handle <> 0L"
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
                            elif n.StartsWith "Mir" || n.StartsWith "struct" then "nativeint"
                            else "nativeint" //failwithf "strange type: %A" n
            | Ptr (Ptr t) -> "nativeint*"
            | Ptr t ->
                sprintf "%s*" (externTypeName t)
            | FixedArray(t, s) ->
                let t = externTypeName t
                sprintf "%s_%d" t s

    let commands (l : list<Command>) =
        printfn "module VkRaw = "

        printfn "    [<CompilerMessage(\"activeInstance is for internal use only\", 1337, IsError=false, IsHidden=true)>]"
        printfn "    let mutable internal activeInstance : VkInstance = 0n"


        printfn "    [<Literal>]"
        printfn "    let lib = \"vulkan-1.dll\""
        printfn ""
        for c in l do
            if c.name = "vkCreateInstance" then
                let args = c.parameters |> List.map (fun (t,n) -> sprintf "%s %s" (externTypeName t) (fsharpName n)) |> String.concat ", "                
                printfn "    [<DllImport(lib, EntryPoint=\"%s\");SuppressUnmanagedCodeSecurity>]" c.name
                printfn "    extern %s private _%s(%s)" (externTypeName c.returnType) c.name args
                


                let argDef = c.parameters |> List.map (fun (t,n) -> sprintf "%s : %s" (fsharpName n) (typeName t)) |> String.concat ", "
                let argUse = c.parameters |> List.map (fun (t,n) -> fsharpName n) |> String.concat ", "
                let instanceArgName = c.parameters |> List.pick (fun (t,n) -> match t with | Ptr(Literal "VkInstance") -> Some n | _ -> None)

                printfn "    let vkCreateInstance(%s) = " argDef
                printfn "        let res = _vkCreateInstance(%s)" argUse 
                printfn "        if res = VkResult.VkSuccess then"
                printfn "            activeInstance <- NativePtr.read %s" instanceArgName
                printfn "        res"
                printfn "    "
            else
                let args = c.parameters |> List.map (fun (t,n) -> sprintf "%s %s" (externTypeName t) (fsharpName n)) |> String.concat ", "
                printfn "    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]"
                printfn "    extern %s %s(%s)" (externTypeName c.returnType) c.name args

                
        printfn "    "
        printfn "    [<CompilerMessage(\"vkImportInstanceDelegate is for internal use only\", 1337, IsError=false, IsHidden=true)>]"
        printfn "    let vkImportInstanceDelegate<'a>(name : string) = "
        printfn "        let ptr = vkGetInstanceProcAddr(activeInstance, name)"
        printfn "        if ptr = 0n then"
        printfn "            Log.warn \"could not load function: %%s\" name"
        printfn "            Unchecked.defaultof<'a>"
        printfn "        else"
        printfn "            Report.Line(3, sprintf \"loaded function %%s (0x%%08X)\" name ptr)"
        printfn "            Marshal.GetDelegateForFunctionPointer(ptr, typeof<'a>) |> unbox<'a>"




    let topoExtensions (s : list<Extension>) : list<Extension> =
        let typeMap = s |> List.map (fun s -> s.name, s) |> Map.ofList
        let graph =
            s |> List.map (fun s -> 
                    let usedTypes = 
                        (s.requires |> List.choose (fun m -> Map.tryFind m typeMap))

                    let usedTypes = 
                        if s.name <> "EXTDebugReport" then
                            Map.find "EXTDebugReport" typeMap :: usedTypes
                        else 
                            usedTypes

                    s, usedTypes

                    )
                |> Map.ofList

        Struct.toposort graph |> List.rev

        


    let extRx = System.Text.RegularExpressions.Regex @"^VK_(?<kind>[A-Z]+)_(?<name>.*)$"

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


    let extensions (extensions : list<Extension>) =

        let extensions = extensions |> List.filter (not << Extension.isEmpty)

        let kindAndName (e : string) =
            let m = extRx.Match e
            let kind = m.Groups.["kind"].Value
            let name = m.Groups.["name"].Value
            kind, camelCase name

        let fullName (e : string) =
            let k,n = kindAndName e
            let res = sprintf "%s%s" k n
            res

        let extensions = 
            extensions |> List.map (fun e ->
                { e with name = fullName e.name; requires = e.requires |> List.map fullName }
            )
        




        let mapping = extensions |> List.map (fun e -> e.name, e.requires) |> Map.ofList
        let mapping = mapping |> Map.map (fun _ req -> List.filter (fun e -> Map.containsKey e mapping) req)


        let rec requires (name : string) =
            match Map.tryFind name mapping with
                | Some r -> 
                    r @ (r |> List.collect requires)
                | None ->
                    []

        for e in topoExtensions extensions do
            printfn ""
            printfn "module %s =" e.name

            printfn "    let Name = \"%s\"" e.extName
            printfn "    let Number = %d" e.number
            printfn "    "

            let required = requires e.name |> Set.ofList

            if not (Set.isEmpty required) then
                let exts = required |> Seq.map (sprintf "%s.Name") |> String.concat "; " |> sprintf "[ %s ]"
                printfn "    let Required = %s" exts
                for r in required do
                    printfn "    open %s" r
                
            if e.name <> "EXTDebugReport" then
                printfn "    open EXTDebugReport"
                printfn "    "

            enums "    " e.enums
            printfn "    "

            structs "    " (Struct.topologicalSort e.structs)
            printfn "    "
            for (name, values) in Map.toSeq e.enumExtensions do
                enumExtensions name values
            printfn "    "


            match e.commands with
                | [] -> ()
                | _ -> 
                    printfn "    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]"
                    printfn "    module VkRaw ="
                    for c in e.commands do
                        let delegateName = c.name.Substring(0,1).ToUpper() + c.name.Substring(1) + "Del"
                        let targs = c.parameters |> List.map (fun (t,n) -> (typeName t)) |> String.concat " * "
                        let ret = 
                            match typeName c.returnType with
                                | "void" -> "unit"
                                | n -> n

                        let tDel = sprintf "%s -> %s" targs ret
                        printfn "        [<SuppressUnmanagedCodeSecurity>]"
                        printfn "        type %s = delegate of %s" delegateName tDel
                        
                    printfn "        "
                    printfn "        [<AbstractClass; Sealed>]"
                    printfn "        type private Loader<'d> private() ="
                    printfn "            static do Report.Begin(3, \"[Vulkan] loading %s\")" e.extName
                    for c in e.commands do
                        let delegateName = c.name.Substring(0,1).ToUpper() + c.name.Substring(1) + "Del"
                        printfn "            static let s_%sDel = VkRaw.vkImportInstanceDelegate<%s> \"%s\"" c.name delegateName c.name
                    printfn "            static do Report.End(3) |> ignore"
                    for c in e.commands do
                        printfn "            static member %s = s_%sDel" c.name c.name
                     

//
//                    for c in e.commands do
//                        let delegateName = c.name.Substring(0,1).ToUpper() + c.name.Substring(1) + "Del"
//                        printfn "        let private %sDel = lazy (Marshal.GetDelegateForFunctionPointer(VkRaw.vkGetInstanceProcAddr(VkRaw.activeInstance, \"%s\"), typeof<%s>) |> unbox<%s>)" c.name c.name delegateName delegateName
//                        
                    for c in e.commands do
                        let argDef = c.parameters |> List.map (fun (t,n) -> sprintf "%s : %s" (fsharpName n) (typeName t)) |> String.concat ", "
                        let argUse = c.parameters |> List.map (fun (_,n) -> (fsharpName n)) |> String.concat ", "

                        printfn "        let %s(%s) = Loader<unit>.%s.Invoke(%s)" c.name argDef c.name argUse



//
//                    let args = c.parameters |> List.map (fun (t,n) -> sprintf "%s %s" (externTypeName t) (fsharpName n)) |> String.concat ", "
//                    printfn "    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]"
//                    printfn "    extern %s %s(%s)" (externTypeName c.returnType) c.name args

                



let run () = 
//    
//    let dir = Path.GetTempPath()
//    let file = Path.Combine(dir, "vk.xml")
//    
//    let request = System.Net.HttpWebRequest.Create("https://raw.githubusercontent.com/KhronosGroup/Vulkan-Docs/1.0/src/spec/vk.xml")
//    let response = request.GetResponse()
//    let s = response.GetResponseStream()

    let vk = XElement.Load(@"C:\VulkanSDK\1.0.65.1\vk.xml")
    let defines = XmlReader.defines vk
    let aliases = XmlReader.aliases defines vk
    let handles = XmlReader.handles vk
    let enums = XmlReader.enums vk
    let structs = XmlReader.structs defines vk
    let commands = XmlReader.commands defines vk
    let exts, commands, enums, structs = XmlReader.extensions vk commands enums structs





    FSharpWriter.header()
    FSharpWriter.missing()
    FSharpWriter.handles handles
    FSharpWriter.aliases aliases
    FSharpWriter.enums "" enums
    FSharpWriter.globalStructs (Struct.topologicalSort structs)
    FSharpWriter.commands commands
    FSharpWriter.extensions exts

    




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
