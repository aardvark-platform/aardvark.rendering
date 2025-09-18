open System
open System.Collections.Generic
open System.IO
open System.Reflection
open System.Text
open System.Text.RegularExpressions

// Generates a simplified and Aardvark-friendly API for Hexa.NET.ImGui

[<AutoOpen>]
module Constants =

   let ImGuiAssemblyName = "Hexa.NET.ImGui.dll"
   let ImGuiNamespace    = "Hexa.NET.ImGui"
   let HexaGenNamespace  = "HexaGen.Runtime"
   let ImGuiTypeName     = "ImGui"

[<AutoOpen>]
module Utilities =

    module String =

        let safeName (str: string) =
            match str with
            | "val" -> "value"
            | "type" -> "typ"
            | _ -> str

        let toUpperStart (str: string) =
            if String.IsNullOrEmpty str then str
            else string (Char.ToUpper str.[0]) + str.Substring(1)

    let private rxGenericName = Regex @"(?<name>.+)`[0-9]+$"

    type Type with
        member this.NonGenericName =
            let m = rxGenericName.Match this.Name
            if m.Success then m.Groups.["name"].Value
            else this.Name

    type MethodInfo with
        member this.BaseName =
            if this.Name.Length > 1 && this.Name.EndsWith 'S' && this.ReturnType = typeof<string> then
                this.Name.Substring(0, this.Name.Length - 1)
            else
                this.Name

    [<AutoOpen>]
    module TypePatterns =

        [<return: Struct>]
        let (|UInt8|_|) (typ: Type) =
            if typ = typeof<uint8> then ValueSome ()
            else ValueNone

        [<return: Struct>]
        let (|Int32|_|) (typ: Type) =
            if typ = typeof<int32> then ValueSome ()
            else ValueNone

        [<return: Struct>]
        let (|Float|_|) (typ: Type) =
            if typ = typeof<float32> then ValueSome ()
            else ValueNone

        [<return: Struct>]
        let (|String|_|) (typ: Type) =
            if typ = typeof<string> then ValueSome ()
            else ValueNone

        [<return: Struct>]
        let (|Pointer|_|) (typ: Type) =
            if typ.IsPointer then ValueSome <| typ.GetElementType()
            else ValueNone

        [<return: Struct>]
        let (|FunctionPointer|_|) (typ: Type) =
            if typ.IsFunctionPointer then ValueSome (typ.GetFunctionPointerParameterTypes(), typ.GetFunctionPointerReturnType())
            else ValueNone

        [<return: Struct>]
        let (|ByRef|_|) (typ: Type) =
            if typ.IsByRef then ValueSome <| typ.GetElementType()
            else ValueNone

        [<return: Struct>]
        let (|Array|_|) (typ: Type) =
            if typ.IsArray then ValueSome <| typ.GetElementType()
            else ValueNone

        [<return: Struct>]
        let (|Span|_|) (typ: Type) =
            match typ.Name with
            | "Span´1" | "ReadOnlySpan`1" when typ.Namespace = "System" -> ValueSome typ.GenericTypeArguments.[0]
            | _ -> ValueNone

        [<return: Struct>]
        let (|Void|_|) (typ: Type) =
            if typ = typeof<Void> then ValueSome ()
            else ValueNone

        [<return: Struct>]
        let (|Vector|_|) (typ: Type) =
            if typ = typeof<System.Numerics.Vector2> then ValueSome (typeof<float32>, 2)
            elif typ = typeof<System.Numerics.Vector3> then ValueSome (typeof<float32>, 3)
            elif typ = typeof<System.Numerics.Vector4> then ValueSome (typeof<float32>, 4)
            else ValueNone

        [<return: Struct>]
        let (|ImTypePtr|_|) (typ: Type) =
            if typ.Name.StartsWith "Im" && typ.Namespace = ImGuiNamespace then
                match typ.Assembly.GetType($"{typ.Namespace}.{typ.Name}Ptr") with
                | null -> ValueNone
                | tptr -> ValueSome tptr
            else
                ValueNone

        [<return: Struct>]
        let (|Delegate|_|) (typ: Type) =
            if typeof<Delegate>.IsAssignableFrom typ then
                let invoke = typ.GetMethod("Invoke", BindingFlags.Public ||| BindingFlags.Instance)
                ValueSome (invoke.GetParameters(), invoke.ReturnType)
            else
                ValueNone

    module Type =

        let private knownTypes =
            let d = Dictionary<Type, string>()
            d.[typeof<string>]     <- "string"
            d.[typeof<Void>]       <- "unit"
            d.[typeof<bool>]       <- "bool"
            d.[typeof<int8>]       <- "int8"
            d.[typeof<uint8>]      <- "uint8"
            d.[typeof<int16>]      <- "int16"
            d.[typeof<uint16>]     <- "uint16"
            d.[typeof<int32>]      <- "int32"
            d.[typeof<uint32>]     <- "uint32"
            d.[typeof<int64>]      <- "int64"
            d.[typeof<uint64>]     <- "uint64"
            d.[typeof<float32>]    <- "float32"
            d.[typeof<float>]      <- "float"
            d.[typeof<unativeint>] <- "unativeint"
            d

        let private vectorSuffices =
            let d = Dictionary<Type, string>()
            d.[typeof<uint8>]      <- "b"
            d.[typeof<int16>]      <- "s"
            d.[typeof<uint16>]     <- "us"
            d.[typeof<int32>]      <- "i"
            d.[typeof<uint32>]     <- "ui"
            d.[typeof<int64>]      <- "l"
            d.[typeof<float32>]    <- "f"
            d.[typeof<float>]      <- "d"
            d

        let colorElementTypes =
            [
                typeof<uint8>
                typeof<uint16>
                typeof<uint32>
                typeof<float32>
                typeof<float>
            ]

        let rec toString (typ: Type) =
            match knownTypes.TryGetValue typ with
            | true, str -> str
            | _ ->
                if typ.IsGenericType then
                    let gargs = typ.GetGenericArguments() |> Array.map toString |> String.concat ", "
                    $"{typ.NonGenericName}<{gargs}>"
                elif typ.IsArray then
                    let et = typ.GetElementType()
                    $"{toString et}[]"
                else
                    typ.Name

        let getSuffix (typ: Type) =
            match vectorSuffices.TryGetValue typ with
            | true, s -> s
            | _ -> failwith $"Invalid vector element type: {typ}"

    type IDictionary<'K, 'V> with
        member this.GetOrCreate(key: 'K, create: 'K -> 'V) =
            match this.TryGetValue key with
            | true, value -> value
            | _ ->
                let value = create key
                this.[key] <- value
                value

module ApiParser =

    module ApiOverrides =

        let preferRef (_apiName: string) (paramName: string) =
            paramName <> "buf"

        let allowInOut =
            let methodNameRx =
                [
                    "^Add[A-Z]"
                    "^Calc[A-Z]"
                    "^BuildRanges$"
                    "^ColorConvertU32ToFloat4$"
                    "^ColorConvertHSVtoRGB$"
                    "^ColorConvertRGBtoHSV$"
                    "^DataTypeFormatString$"
                    "^Destroy"
                    "^Get[A-Z]"
                    "^Im[A-Z]"
                    "^InputText"
                    "^IsMousePosValid$"
                    "^RenderChar$"
                    "^TempInputText$"
                    "^PlotHistogram$"
                    "^PlotLines$"
                ]
                |> List.map (fun p -> $"({p})")
                |> String.concat "|"
                |> Regex

            fun (methodName: string) (paramName: string) ->
                not (methodNameRx.IsMatch methodName || (methodName = "ColorPicker4" && paramName = "refCol"))

        let vectorizedApis =
            let d = Dictionary<string, Type * int>()
            d.["ColorEdit3"]   <- typeof<float32>, 3
            d.["ColorEdit4"]   <- typeof<float32>, 4
            d.["ColorPicker3"] <- typeof<float32>, 3
            d.["ColorPicker4"] <- typeof<float32>, 4
            d.["DragFloat2"]   <- typeof<float32>, 2
            d.["DragFloat3"]   <- typeof<float32>, 3
            d.["DragFloat4"]   <- typeof<float32>, 4
            d.["DragInt2"]     <- typeof<int32>, 2
            d.["DragInt3"]     <- typeof<int32>, 3
            d.["DragInt4"]     <- typeof<int32>, 4
            d.["InputFloat2"]  <- typeof<float32>, 2
            d.["InputFloat3"]  <- typeof<float32>, 3
            d.["InputFloat4"]  <- typeof<float32>, 4
            d.["InputInt2"]    <- typeof<int32>, 2
            d.["InputInt3"]    <- typeof<int32>, 3
            d.["InputInt4"]    <- typeof<int32>, 4
            d.["SliderFloat2"] <- typeof<float32>, 2
            d.["SliderFloat3"] <- typeof<float32>, 3
            d.["SliderFloat4"] <- typeof<float32>, 4
            d.["SliderInt2"]   <- typeof<int32>, 2
            d.["SliderInt3"]   <- typeof<int32>, 3
            d.["SliderInt4"]   <- typeof<int32>, 4
            d

        let rangeApis =
            let d = Dictionary<string, string>()
            d.["DragFloatRange2"] <- "vCurrent"
            d.["DragIntRange2"]   <- "vCurrent"
            d

        let colorApis =
            let d = Dictionary<string, string list>()
            d.["ColorButton"]    <- [ "col" ]
            d.["ColorEdit3"]     <- [ "col" ]
            d.["ColorEdit4"]     <- [ "col" ]
            d.["ColorPicker3"]   <- [ "col" ]
            d.["ColorPicker4"]   <- [ "col"; "refCol" ]
            d.["PushStyleColor"] <- [ "col" ]
            d.["TextColored"]    <- [ "col" ]
            d.["TextColoredV"]   <- [ "col" ]
            d

        type InputTextApi =
            {
                Buf         : string
                BufSize     : string
                BufSizeType : Type
                Text        : string
                Last        : string  // last regular parameter, after which flags and callback follow
                Flags       : string
                Callback    : string
            }

            static member Null =
                { Buf         = null
                  BufSize     = null
                  BufSizeType = null
                  Text        = null
                  Last        = null
                  Flags       = null
                  Callback    = null }

            static member Default =
                { Buf         = "buf"
                  BufSize     = "bufSize"
                  BufSizeType = typeof<unativeint>
                  Text        = "text"
                  Last        = "text"
                  Flags       = "flags"
                  Callback    = "callback" }

        let inputTextApis =
            let d = Dictionary<string, InputTextApi>()
            d.["InputText"]          <- InputTextApi.Default
            d.["InputTextEx"]        <- { InputTextApi.Default with Last = "sizeArg"; BufSizeType = typeof<int> }
            d.["InputTextMultiline"] <- { InputTextApi.Default with Last = "size" }
            d.["InputTextWithHint"]  <- InputTextApi.Default
            d

    [<CustomEquality; NoComparison>]
    type ApiType =
        | Prim   of Type
        | Vec    of Type * int
        | Col    of Type * int
        | Range  of Type
        | Arr    of ApiType
        | Ptr    of ApiType
        | FnPtr  of ApiType[] * ApiType
        | Ref    of ApiType
        | CVal   of ApiType

        static member Bool   = Prim typeof<bool>
        static member String = Prim typeof<string>
        static member Void   = Prim typeof<Void>

        member this.IsByRef = match this with Ref _ -> true | _ -> false
        member this.IsArray = match this with Arr _ -> true | _ -> false

        member this.Equals(other: ApiType) =
            match this, other with
            | Prim t, Prim o
            | Range t, Range o -> t = o
            | Vec (tt, td), Vec (ot, od)
            | Col (tt, td), Col (ot, od) -> tt = ot && td = od
            | Ptr _, Ptr _ -> true          // https://github.com/dotnet/fsharp/issues/7428
            | FnPtr (tp, tq), FnPtr (op, oq) -> tp = op && tq = oq
            | Arr t, Arr o
            | Ref t, Ref o
            | CVal t, CVal o -> t.Equals o
            | _ -> false

        override this.Equals(obj: obj) =
            match obj with
            | :? ApiType as other -> this.Equals other
            | _ -> false

        override this.GetHashCode() =
            match this with
            | Prim t -> t.GetHashCode()
            | Vec (t, d) -> HashCode.Combine(1, t.GetHashCode(), d)
            | Col (t, d) -> HashCode.Combine(2, t.GetHashCode(), d)
            | Range t -> HashCode.Combine(3, t.GetHashCode())
            | Arr t -> HashCode.Combine(4, t.GetHashCode())
            | Ptr _ -> 5
            | FnPtr (p, r) ->
                let p = (6, p) ||> Array.fold (fun c p -> HashCode.Combine(c, p.GetHashCode()))
                HashCode.Combine(p, r.GetHashCode())
            | Ref t -> HashCode.Combine(7, t.GetHashCode())
            | CVal t -> HashCode.Combine(8, t.GetHashCode())

        override this.ToString() =
            match this with
            | Prim t -> Type.toString t
            | Arr t -> $"{t.ToString()}[]"
            | Vec (t, d) -> $"V{d}{Type.getSuffix t}"
            | Col (t, d) -> $"C{d}{Type.getSuffix t}"
            | Range t -> $"Range1{Type.getSuffix t}"
            | Ptr (Prim Void) -> "voidptr"
            | Ptr t -> $"nativeptr<{t.ToString()}>"
            | FnPtr _ -> "nativeint"
            | Ref t -> $"byref<{t.ToString()}>"
            | CVal t -> $"cval<{t.ToString()}>"

    module ApiType =

        let rec ofType = function
            | String -> Ptr <| Prim typeof<uint8>
            | Array e -> Ptr <| ofType e
            | ByRef (ImTypePtr t) -> Prim t                     // Lots of overloads with byref<ImType> and ImTypePtr -> prefer latter, ignore byref ones
            | Pointer e | ByRef e | Span e -> Ptr <| ofType e   // Treat byref and spans like pointers initially, so we group overloads with essentially the same signature
            | FunctionPointer (p, r) -> FnPtr (p |> Array.map ofType, ofType r)
            | Vector (t, d) -> Vec (t, d)
            | t -> Prim t

        let rec getContainedTypes = function
            | Prim (Delegate (p, r) as t) ->
                let p = p |> Array.collect (_.ParameterType >> ofType >> getContainedTypes)
                let r = r |> ofType |> getContainedTypes
                (Array.append p r) |> Array.append [|t|]
            | Prim t | Vec (t, _) | Col (t, _) | Range t -> [|t|]
            | Arr t | Ref t | Ptr t | CVal t -> getContainedTypes t
            | FnPtr (p, r) -> (p, [|r|]) ||> Array.append |> Array.collect getContainedTypes

        let ptrToRef = function
            | Ptr t -> Ref t
            | t -> t

        let ptrToStr = function
            | Ptr (Prim UInt8)       -> ApiType.String
            | Ptr (Ptr (Prim UInt8)) -> Arr ApiType.String
            | t -> t

        let ptrToCVal = function
            | Ptr t -> CVal t
            | t -> t

        let refToArr = function
            | Ref t -> Arr t
            | t -> t

        let rec vecToCol (elementType: Type) = function
            | Vec (_, d) -> Col (elementType, d)
            | Arr t -> Arr <| vecToCol elementType t
            | Ref t -> Ref <| vecToCol elementType t
            | Ptr t -> Ptr <| vecToCol elementType t
            | CVal t -> CVal <| vecToCol elementType t
            | t -> t

    type ApiParameter =
        {
            Name : string
            Type : ApiType
        }

        override this.ToString() =
            $"{this.Name}: {this.Type}"

    module ApiParameter =

        let ofParameterInfo (pi: ParameterInfo) =
            {
                Name = String.safeName pi.Name
                Type = ApiType.ofType pi.ParameterType
            }

    type ApiSignature =
        {
            ReturnType : ApiType
            Parameters : ApiParameter[]
        }

        override this.ToString() =
            let parameters = this.Parameters |> Array.map _.ToString() |> String.concat ", "
            $"({parameters}) : {this.ReturnType}"

    module ApiSignature =

        let ofMethodInfo (mi: MethodInfo) =
            {
                ReturnType = ApiType.ofType mi.ReturnType
                Parameters = mi.GetParameters() |> Array.map ApiParameter.ofParameterInfo
            }

    [<RequireQualifiedAccess>]
    type ApiParameterAttribute =
        | Default
        | String
        | ByRef
        | InOut

    type ApiOverload(apiName: string, signature: ApiSignature, methods: seq<MethodInfo>) =
        let methods = List methods

        new (apiName: string, signature: ApiSignature) =
            ApiOverload(apiName, signature, List())

        member _.Signature       = signature
        member _.Methods         = methods.AsReadOnly()
        member _.Vectorized      = ApiOverrides.vectorizedApis.GetValueOrDefault(apiName, (null, 1))
        member this.IsVectorized = snd this.Vectorized > 1
        member _.RangeParameter  = ApiOverrides.rangeApis.GetValueOrDefault(apiName, null)
        member _.InputText       = ApiOverrides.inputTextApis.GetValueOrDefault(apiName, ApiOverrides.InputTextApi.Null)
        member this.IsInputText  = this.InputText <> ApiOverrides.InputTextApi.Null
        member _.ReturnString    = methods.Exists (_.ReturnType >> (=) typeof<string>)

        member this.IsColorParameter(paramName: string) =
            ApiOverrides.colorApis.GetValueOrDefault(apiName, [])
            |> List.contains paramName

        member _.GetParameterAttribute(paramName: string) =
            let mutable isString = false
            let mutable isByRef = false

            for mi in methods do
                for pi in mi.GetParameters() do
                    if pi.Name = paramName then
                        match pi.ParameterType with
                        | String | Array String -> isString <- true
                        | ByRef _               -> isByRef  <- true
                        | _ -> ()

            match isString, isByRef with
            | true, _                                                -> ApiParameterAttribute.String
            | _, true when ApiOverrides.allowInOut apiName paramName -> ApiParameterAttribute.InOut
            | _, true when ApiOverrides.preferRef apiName paramName  -> ApiParameterAttribute.ByRef
            | _                                                      -> ApiParameterAttribute.Default

        member _.Add(mi: MethodInfo) =
            methods.Add mi

    type Api(name: string) =
        let overloads = Dictionary<ApiSignature, ApiOverload>()

        member _.Name = name
        member _.Overloads = overloads.AsReadOnly()

        member this.Add(mi: MethodInfo) =
            let signature = ApiSignature.ofMethodInfo mi
            let overload = overloads.GetOrCreate(signature, fun signature -> ApiOverload(name, signature))
            overload.Add mi

        member this.Set(signature: ApiSignature, methods: seq<MethodInfo>) =
            overloads.[signature] <- ApiOverload(name, signature, methods)

    type Result =
        {
            Apis  : Dictionary<string, Api>
            Types : HashSet<Type>
        }

    let parse (assembly: Assembly) =
        let imguiType = assembly.GetType($"{ImGuiNamespace}.{ImGuiTypeName}", true)

        let imguiMethods =
            imguiType.GetMethods(BindingFlags.Static ||| BindingFlags.Public)

        let basicApis = Dictionary<string, Api>()
        let finalApis = Dictionary<string, Api>()

        for mi in imguiMethods do
            if Char.IsUpper mi.Name.[0] then
                let api = basicApis.GetOrCreate(mi.BaseName, Api)
                api.Add mi

        // At this point the overloads have basic signatures with pointers instead of strings and arrays, and without cval parameters.
        // Due to ApiType.ofType a ton of methods in the Hexa.NET wrapper are grouped (e.g. ReadOnlySpan, nativeptr, and byref are considered the same).
        for basicApi in basicApis.Values do
            let finalApi = finalApis.GetOrCreate(basicApi.Name, Api)

            for KeyValue(basicSignature, basicOverload) in basicApi.Overloads do
                let replaceColors (colorType: Type) (parameters: ApiParameter[]) =
                    if colorType <> null then
                        parameters |> Array.map (fun param ->
                            if basicOverload.IsColorParameter param.Name then
                                { param with Type = param.Type |> ApiType.vecToCol colorType }
                            else
                                param
                        )
                    else
                        parameters

                let replacePointers (adaptiveInOut: bool) (parameters: ApiParameter[]) =
                    parameters |> Array.map (fun param ->
                        match basicOverload.GetParameterAttribute param.Name with
                        | ApiParameterAttribute.String  -> { param with Type = param.Type |> ApiType.ptrToStr }
                        | ApiParameterAttribute.ByRef   -> { param with Type = param.Type |> ApiType.ptrToRef }
                        | ApiParameterAttribute.InOut   -> { param with Type = param.Type |> if adaptiveInOut then ApiType.ptrToCVal else ApiType.ptrToRef }
                        | ApiParameterAttribute.Default -> param
                    )

                let vectorize (parameters: ApiParameter[]) =
                    match basicOverload.Vectorized with
                    | t, d when d > 1 ->
                        parameters |> Array.map (fun param ->
                            let vt =
                                match param.Type with
                                | CVal (Prim pt) when pt = t -> CVal <| Vec (t, d)
                                | Ref  (Prim pt) when pt = t -> Ref  <| Vec (t, d)
                                | t -> t

                            { param with Type = vt }
                        )
                    | _ -> parameters

                let replaceRanges (parameters: ApiParameter[]) =
                    match basicOverload.RangeParameter with
                    | null -> parameters
                    | rangeParam ->
                        let result = ResizeArray<ApiParameter>()

                        for p in parameters do
                            if p.Name = $"{rangeParam}Min" then
                                match p.Type with
                                | CVal (Prim t) -> result.Add { Type = CVal <| Range t; Name = rangeParam }
                                | Ref  (Prim t) -> result.Add { Type = Ref  <| Range t; Name = rangeParam }
                                | _ -> failwith $"Cannot replace range in {basicApi.Name}"
                            elif p.Name <> $"{rangeParam}Max" then
                                result.Add p

                        result.ToArray()

                let replaceArrays (parameters: ApiParameter[]) =
                    let result = ResizeArray<ApiParameter>()

                    let mutable i = 0
                    while i < parameters.Length do
                        let j = min (i + 1) (parameters.Length - 1)
                        let pi = parameters.[i]
                        let pj = parameters.[j]

                        if (pi.Type.IsByRef || pi.Type.IsArray) && (pj.Name = $"{pi.Name}Count" || pj.Name = $"num{String.toUpperStart pi.Name}") then
                            result.Add { pi with Type = pi.Type |> ApiType.refToArr }
                            i <- i + 2
                        else
                            result.Add pi
                            i <- i + 1

                    result.ToArray()

                let replaceInputText (adaptiveInOut: bool) (parameters: ApiParameter[]) =
                    if basicOverload.IsInputText then
                        let result = ResizeArray<ApiParameter>()
                        let inputText = basicOverload.InputText

                        let mutable i = 0
                        while i < parameters.Length do
                            let j = min (i + 1) (parameters.Length - 1)
                            let pi = parameters.[i]
                            let pj = parameters.[j]

                            if pi.Name = inputText.Buf && pj.Name = inputText.BufSize then
                                result.Add { pi with Name = inputText.Text; Type = if adaptiveInOut then CVal ApiType.String else Ref ApiType.String }
                                i <- i + 2
                            else
                                result.Add pi
                                i <- i + 1

                        result.ToArray()
                    else
                        parameters

                let getFinalSignature (colorType: Type) (adaptiveInOut: bool) =
                    let returnType =
                        if basicOverload.ReturnString then ApiType.String
                        else basicSignature.ReturnType

                    let parameters =
                        basicSignature.Parameters
                        |> replacePointers adaptiveInOut
                        |> vectorize
                        |> replaceColors colorType
                        |> replaceRanges
                        |> replaceArrays
                        |> replaceInputText adaptiveInOut

                    { ReturnType = returnType; Parameters = parameters }

                for colorType in null :: Type.colorElementTypes do
                    for adaptiveInOut in [ false; true ] do
                        let signature = getFinalSignature colorType adaptiveInOut
                        finalApi.Set(signature, basicOverload.Methods)

        // Find all referenced Hexa.NET types so we can define typedefs.
        // We don't want to force the user to open the Hexa.NET namespace.
        let types = HashSet<Type>()

        for api in finalApis.Values do
            for signature in api.Overloads.Keys do
                for t in ApiType.getContainedTypes signature.ReturnType do
                    types.Add t |> ignore

                for typ in signature.Parameters do
                    for t in ApiType.getContainedTypes typ.Type do
                        types.Add t |> ignore

        types.RemoveWhere (fun t ->
            (t.Namespace <> ImGuiNamespace && t.Namespace <> HexaGenNamespace) || t.IsGenericType
        ) |> ignore

        { Apis  = finalApis
          Types = types }

module Generator =
    open ApiParser

    let private toString (result: ApiParser.Result) =
        let builder = StringBuilder()
        let mutable indent = 0

        let ln() =
            builder.AppendLine() |> ignore

        let fn fmt =
            let indent = String.replicate indent "    "
            Printf.kprintf (fun str ->
                let str = indent + str
                builder.AppendLine(str) |> ignore
            ) fmt

        let blk fmt (f: unit -> unit) =
            fn fmt
            indent <- indent + 1
            f()
            indent <- indent - 1

        fn "namespace Aardvark.Rendering.ImGui"
        ln()
        fn "open Aardvark.Base"
        fn "open FSharp.Data.Adaptive"
        fn "open FSharp.NativeInterop"
        ln()
        fn "#nowarn \"9\""
        fn "#nowarn \"51\""
        ln()

        for t in result.Types |> Seq.sortBy _.Name do
            if not t.IsGenericType && not t.IsGenericTypeDefinition then
                fn $"type {t.Name} = {t.Namespace}.{t.Name}"

        fn "type ImVector<'T when 'T : unmanaged and 'T : (new: unit -> 'T) and 'T : struct and 'T :> System.ValueType> = Hexa.NET.ImGui.ImVector<'T>"

        ln()
        fn "[<AutoOpen>]"
        blk "module internal ConversionExtensions =" (fun _ ->
            fn "open System.Numerics"
            ln()

            let fields = [| "X"; "Y"; "Z"; "W" |]

            for d = 2 to 4 do
                let args = fields |> Array.take d |> Array.map (fun f -> $"v.{f}") |> String.concat ", "

                blk $"type Vector{d} with" (fun _ ->
                    fn $"static member FromV{d}f(v: V{d}f) = Vector{d}({args})"
                    fn $"member v.ToV{d}f() = V{d}f({args})"

                    if d > 2 then
                        for ct in Type.colorElementTypes do
                            let t = Col(ct, d)
                            let toVdf, toCol =
                                if ct = typeof<float32> then
                                    $"ToV{d}f()", $"To{t}()"
                                else
                                    $"ToC{d}f().ToV{d}f()", $"ToC{d}f().To{t}()"

                            ln()
                            fn $"static member From{t}(v: {t}) = Vector{d}.FromV{d}f(v.{toVdf})"
                            fn $"member v.To{t}() = v.ToV{d}f().{toCol}"
                )
                ln()
        )

        fn "[<AbstractClass; Sealed>]"
        blk "type ImGui =" (fun _ ->
            for api in result.Apis.Values |> Seq.sortBy _.Name do
                ln()
                for KeyValue(signature, overload) in api.Overloads do
                    let parameters = signature.Parameters

                    let fixedArrays  = ResizeArray<string>()
                    let arguments    = ResizeArray<string>()
                    let localState   = ResizeArray<{| Name: string; Setter: string; ToState: string; FromState: string |}>()
                    let mutable isAdaptive = false

                    let getState (typ: ApiType) (name: string) =
                        match typ with
                        | Col (typ, dim) when typ <> typeof<float32> ->
                            let state = Some {| Name = name; Setter = ""; ToState = $".ToC{dim}f()"; FromState = $".ToC{dim}{Type.getSuffix typ}()" |}
                            Col (typeof<float32>, dim), state
                        | _ ->
                            let state = None
                            typ, state

                    let addAdaptiveState (typ: ApiType) (name: string) =
                        isAdaptive <- true
                        let typ, state = getState typ name
                        let state = state |> Option.defaultValue {| Name = name; Setter = ""; ToState = ""; FromState = "" |}
                        localState.Add {| state with Setter = ".Value"; ToState = $".Value{state.ToState}" |}
                        typ

                    let addLocalState (typ: ApiType) (name: string) =
                        let typ, state = getState typ name
                        state |> Option.iter localState.Add
                        typ

                    let getPtrCast = function
                        | Col (Float, d)
                        | Vec (Float, d) when not overload.IsVectorized -> $"NativePtr.cast<_, System.Numerics.Vector{d}> "
                        | Range t | Vec (t, _) | Col (t, _) -> $"NativePtr.cast<_, {Type.toString t}> "
                        | _ -> ""

                    let hasParam name =
                        parameters |> Array.exists (_.Name >> (=) name)

                    let addFlagsAndCallback() =
                        if not <| hasParam overload.InputText.Flags then
                            arguments.Add "ImGuiInputTextFlags.CallbackResize"
                            if not <| hasParam overload.InputText.Callback then arguments.Add "TextBuffer.Shared.InputTextResizeCallback"

                    for p in parameters do
                        let n = p.Name

                        match p.Type with
                        | Vec (Float, d) -> arguments.Add $"System.Numerics.Vector{d}.FromV{d}f({n})"
                        | Col (_, d)     -> arguments.Add $"System.Numerics.Vector{d}.From{p.Type}({n})"

                        | Arr t when t = ApiType.String ->
                            arguments.Add $"{n}"
                            arguments.Add $"{n}.Length"

                        | Arr t ->
                            fixedArrays.Add n
                            arguments.Add $"{getPtrCast t}{n}Pinned"
                            arguments.Add $"{n}.Length"

                        | Prim _ when overload.IsInputText && n = overload.InputText.Flags ->
                            arguments.Add $"{n} ||| ImGuiInputTextFlags.CallbackResize"
                            if not <| hasParam overload.InputText.Callback then arguments.Add "TextBuffer.Shared.InputTextResizeCallback"

                        | Ref (Prim String) | CVal (Prim String) when overload.IsInputText ->
                            let toSize = match overload.InputText.BufSizeType with Int32 -> "" | t -> $"{Type.toString t} "
                            isAdaptive <- not p.Type.IsByRef
                            arguments.Add "TextBuffer.Shared.Handle"
                            arguments.Add $"{toSize}TextBuffer.Shared.Size"
                            if n = overload.InputText.Last then addFlagsAndCallback()

                        | Prim _ when overload.IsInputText && n = overload.InputText.Last ->
                            arguments.Add n
                            addFlagsAndCallback()

                        | Ref (Range _ as t) ->
                            arguments.Add $"{getPtrCast t}&&{n}"
                            arguments.Add $"NativePtr.step 1 ({getPtrCast t}&&{n})"

                        | Ref t ->
                            let t = addLocalState t n
                            arguments.Add $"{getPtrCast t}&&{n}"

                        | CVal (Range _ as t) ->
                            let t = addAdaptiveState t n
                            arguments.Add $"{getPtrCast t}&&{n}State"
                            arguments.Add $"NativePtr.step 1 ({getPtrCast t}&&{n}State)"

                        | CVal t ->
                            let t = addAdaptiveState t n
                            arguments.Add $"{getPtrCast t}&&{n}State"

                        | _ ->
                            arguments.Add n

                    let ignoreResult, outerSignature =
                        if isAdaptive && signature.ReturnType = ApiType.Bool then
                            true, { signature with ReturnType = ApiType.Void } // We probably don't care about the bool return when we use avals anyway
                        else
                            false, signature

                    ln()
                    blk $"static member {api.Name}{outerSignature} =" (fun _ ->
                        for n in fixedArrays do
                            fn $"use {n}Pinned = fixed {n}"

                        for s in localState do
                            fn $"let mutable {s.Name}State = {s.Name}{s.ToState}"

                        if overload.IsInputText then
                            if hasParam overload.InputText.Callback then
                                blk $"let {overload.InputText.Callback} data =" (fun _ ->
                                    fn "TextBuffer.Shared.InputTextResizeCallback data |> ignore"
                                    fn $"{overload.InputText.Callback}.Invoke data"
                                )

                            let getter = if isAdaptive then ".Value" else ""
                            fn $"TextBuffer.Shared.Text <- {overload.InputText.Text}{getter}"

                        let invoke =
                            let arglist = arguments.ToArray() |> String.concat ", "

                            let apiName =
                                if signature.ReturnType = ApiType.String && overload.Methods |> Seq.exists (_.Name >> (=) $"{api.Name}S") then
                                    $"{api.Name}S"
                                else
                                    api.Name

                            $"{ImGuiNamespace}.{ImGuiTypeName}.{apiName}({arglist})"

                        let rest() =
                            for s in localState do
                                fn $"{s.Name}{s.Setter} <- {s.Name}State{s.FromState}"

                            if overload.IsInputText then
                                let setter = if isAdaptive then ".Value" else ""
                                fn $"{overload.InputText.Text}{setter} <- TextBuffer.Shared.Text"

                        let convertResult =
                            match signature.ReturnType with
                            | Vec (Float, (2 | 3 | 4 as d)) -> $"result.ToV{d}f()"
                            | Ptr (Vec _) -> "NativePtr.cast result"
                            | _ -> null

                        if signature.ReturnType <> ApiType.Void && (localState.Count > 0 || convertResult <> null || overload.IsInputText) then
                            fn $"let result = {invoke}"

                            if signature.ReturnType = ApiType.Bool then
                                blk "if result then" rest
                            else
                                rest()

                            if convertResult = null then
                                if not ignoreResult then
                                    fn "result"
                            else
                                fn $"{convertResult}"
                        else
                            fn $"{invoke}"
                            rest()
                    )
        )

        builder.ToString()

    let run (outputPath: string) =
        let binFolder = Path.Combine(__SOURCE_DIRECTORY__, "..", "..", "bin")

        if not <| Directory.Exists binFolder then
            printfn "Cannot find bin folder"
            Environment.Exit -1

        let assemblyPath =
            Directory.EnumerateFiles(binFolder, ImGuiAssemblyName, SearchOption.AllDirectories)
            |> Seq.tryHead
            |> Option.defaultWith (fun _ ->
                printfn "Cannot find %A" ImGuiAssemblyName
                Environment.Exit -1
                null
            )

        let assembly = Assembly.LoadFrom assemblyPath
        let parsed = parse assembly
        let output = toString parsed

        File.WriteAllText(outputPath, output)
        printfn $"Generated '{Path.GetFileName outputPath}'"

fsi.ShowDeclarationValues <- false
Generator.run <| Path.Combine(__SOURCE_DIRECTORY__, "API.fs")