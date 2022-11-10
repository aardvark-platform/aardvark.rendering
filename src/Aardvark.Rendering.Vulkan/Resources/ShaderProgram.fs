namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop
open FSharp.Data.Adaptive

#nowarn "9"
// #nowarn "51"
#nowarn "8989"

module private FShadeAdapter =
    open FShade
    open FShade.GLSL
    open Aardvark.Base.MultimethodTest


    let private toGeometryFlags (d : GLSLShaderDecoration) =
        match d with
            | GLSLInputTopology t ->
                match t with
                    | InputTopology.Point -> GeometryFlags.InputPoints
                    | InputTopology.Line -> GeometryFlags.InputLines
                    | InputTopology.LineAdjacency -> GeometryFlags.InputLinesAdjacency
                    | InputTopology.Triangle -> GeometryFlags.InputTriangles
                    | InputTopology.TriangleAdjacency -> GeometryFlags.InputTrianglesAdjacency
                    | InputTopology.Patch 1 -> GeometryFlags.InputPoints
                    | InputTopology.Patch 2 -> GeometryFlags.InputLines
                    | InputTopology.Patch 3 -> GeometryFlags.InputTriangles
                    | InputTopology.Patch 4 -> GeometryFlags.InputLinesAdjacency
                    | InputTopology.Patch 6 -> GeometryFlags.InputTrianglesAdjacency
                    | InputTopology.Patch _ -> GeometryFlags.None

            | GLSLOutputTopology t ->
                match t with
                    | OutputTopology.Points -> GeometryFlags.OutputPoints
                    | OutputTopology.LineStrip -> GeometryFlags.OutputLineStrip
                    | OutputTopology.TriangleStrip -> GeometryFlags.OutputTriangleStrip

            | _ ->
                GeometryFlags.None
    
    let private toTessellationFlags (d : GLSLShaderDecoration) =
        match d with
            | GLSLOutputTopology t ->
                match t with
                    | OutputTopology.Points -> TessellationFlags.OutputPoints
                    | OutputTopology.LineStrip -> TessellationFlags.OutputIsolines
                    | OutputTopology.TriangleStrip -> TessellationFlags.OutputTriangles

            | GLSLSpacing s ->
                match s with
                    | GLSLSpacing.Equal -> TessellationFlags.SpacingEqual
                    | GLSLSpacing.FractionalEven -> TessellationFlags.SpacingFractionalEven
                    | GLSLSpacing.FractionalOdd -> TessellationFlags.SpacingFractionalOdd
                    
            | GLSLWinding w ->
                match w with
                    | GLSLWinding.CCW -> TessellationFlags.VertexOrderCcw
                    | GLSLWinding.CW -> TessellationFlags.VertexOrderCw

            | _ ->
                TessellationFlags.None
    
    let geometryInfo (iface : GLSLShaderInterface) =
        {
            outputVertices =
                iface.shaderDecorations 
                |> List.tryPick (function GLSLMaxVertices v -> Some v | _ -> None)
                |> Option.defaultValue 1
        
            invocations =
                iface.shaderDecorations 
                |> List.tryPick (function GLSLInvocations v -> Some v | _ -> None)
                |> Option.defaultValue 1

            flags =
                iface.shaderDecorations
                |> List.fold (fun f d -> f ||| toGeometryFlags d) GeometryFlags.None
                
        }

    let tessControlInfo (ctrl : GLSLShaderInterface) (eval : GLSLShaderInterface) =
        {
            
            inputPatchSize =
                ctrl.shaderDecorations 
                |> List.tryPick (function GLSLInputTopology (InputTopology.Patch v) -> Some v | _ -> None)
                |> Option.defaultValue 1
        
            flags =
                eval.shaderDecorations
                |> List.fold (fun f d -> f ||| toTessellationFlags d) TessellationFlags.None
                
        }

    let fragmentInfo (iface : GLSLShaderInterface) =
        let writesDepth = 
            MapExt.containsKey "gl_Depth" iface.shaderBuiltInOutputs

        let sampleShading =
            MapExt.containsKey "gl_SampleLocation" iface.shaderBuiltInInputs ||
            MapExt.containsKey "gl_SampleID" iface.shaderBuiltInInputs || 
            MapExt.containsKey "gl_SampleIndex" iface.shaderBuiltInInputs

        {
            flags = (if writesDepth then FragmentFlags.DepthReplacing else FragmentFlags.DepthUnchanged)
            discard = GLSLShaderInterface.usesDiscard iface
            sampleShading = sampleShading
        }

type ShaderProgram(device : Device, shaders : array<ShaderModule>, layout : PipelineLayout, original : string, iface : FShade.GLSL.GLSLProgramInterface) =
    inherit CachedResource(device)

    static let allTopologies = Enum.GetValues(typeof<IndexedGeometryMode>) |> unbox<IndexedGeometryMode[]> |> Set.ofArray
    
    //static let stagesRev =
    //    [|
    //        FShade.ShaderStage.Compute
    //        FShade.ShaderStage.Fragment
    //        FShade.ShaderStage.Geometry
    //        FShade.ShaderStage.TessEval
    //        FShade.ShaderStage.TessControl
    //        FShade.ShaderStage.Vertex
    //    |]
    //static let stages =
    //    [|
    //        FShade.ShaderStage.Vertex
    //        FShade.ShaderStage.TessControl
    //        FShade.ShaderStage.TessEval
    //        FShade.ShaderStage.Geometry
    //        FShade.ShaderStage.Fragment
    //        FShade.ShaderStage.Compute
    //    |]

    // get in-/outputs
    let inputs  = iface.inputs //stages |> Array.tryPick (fun s -> MapExt.tryFind s iface.shaders) |> Option.get |> FShade.GLSL.GLSLShaderInterface.inputs
    let outputs = iface.outputs //stagesRev |> Array.tryPick (fun s -> MapExt.tryFind s iface.shaders) |> Option.get |> FShade.GLSL.GLSLShaderInterface.outputs

    let mutable geometryInfo = None
    let mutable tessInfo = None
    let mutable fragInfo = None

    do for shader in shaders do
        let iface = shader.Interface
        match iface.shaderStage with
            | FShade.ShaderStage.Geometry -> geometryInfo <- Some <| FShadeAdapter.geometryInfo iface
            | FShade.ShaderStage.TessControl -> 
                match shaders |> Array.tryFind (fun (s : ShaderModule) -> s.Interface.shaderStage = FShade.ShaderStage.TessEval) with
                    | Some eval -> 
                        tessInfo <- Some <| FShadeAdapter.tessControlInfo iface eval.Interface
                    | None ->
                        ()
            | FShade.ShaderStage.Fragment-> fragInfo <- Some <| FShadeAdapter.fragmentInfo iface
            | _ -> ()

    let acceptedTopologies =
        match tessInfo, geometryInfo with
            | Some i, _ ->
                let flags = i.flags
                match i.inputPatchSize with
                    | 1 -> Set.singleton IndexedGeometryMode.PointList
                    | 2 -> Set.ofList [ IndexedGeometryMode.LineList; IndexedGeometryMode.LineStrip ]
                    | 3 -> Set.ofList [ IndexedGeometryMode.TriangleList; IndexedGeometryMode.TriangleStrip ]
                    | 4 -> Set.ofList [ IndexedGeometryMode.LineAdjacencyList; IndexedGeometryMode.QuadList ]
                    | 6 -> Set.ofList [ IndexedGeometryMode.TriangleAdjacencyList ]
                    | _ -> failf "bad tess-control vertex-count: %A" i.inputPatchSize

            | None, Some i ->
                let flags = i.flags
                if flags.HasFlag GeometryFlags.InputPoints then Set.ofList [ IndexedGeometryMode.PointList ]
                elif flags.HasFlag GeometryFlags.InputLines then Set.ofList [ IndexedGeometryMode.LineList; IndexedGeometryMode.LineStrip ]
                elif flags.HasFlag GeometryFlags.InputLinesAdjacency then Set.ofList [ IndexedGeometryMode.LineAdjacencyList ]
                elif flags.HasFlag GeometryFlags.InputTriangles then Set.ofList [ IndexedGeometryMode.TriangleList; IndexedGeometryMode.TriangleStrip ]
                elif flags.HasFlag GeometryFlags.InputTrianglesAdjacency then Set.ofList [ IndexedGeometryMode.TriangleAdjacencyList ]
                else Set.empty

            | None, None ->
                allTopologies

    let fragInfo =
        match fragInfo with
            | Some i -> i
            | None -> { flags = FragmentFlags.DepthUnchanged; discard = false; sampleShading = false }



    let createInfos =
        shaders |> Array.map (fun shader ->
            VkPipelineShaderStageCreateInfo(
                VkPipelineShaderStageCreateFlags.None,
                VkShaderStageFlags.ofShaderStage shader.Stage,
                shader.Handle,
                CStr.malloc shader.Interface.shaderEntry,
                NativePtr.zero
            )
        )

    member x.Interface = iface

    member x.Device = device
    member x.Shaders = shaders
    member x.PipelineLayout = layout

    member x.GLSL = original
    member x.Inputs = inputs
    member x.Outputs = outputs

    member x.Surface = original

    member x.HasTessellation = Option.isSome tessInfo
    member x.HasDiscard = fragInfo.discard
    member x.FragmentFlags = fragInfo.flags
    member x.SampleShading = fragInfo.sampleShading
    member x.ShaderCreateInfos = createInfos
    member x.TessellationPatchSize = 
        match tessInfo with
            | Some i -> i.inputPatchSize
            | None -> 0

    override x.Destroy() =
        for s in shaders do s.Dispose()
        layout.Dispose()

        for i in 0 .. createInfos.Length-1 do
            let ptr = createInfos.[i].pName
            if not (NativePtr.isNull ptr) then
                NativePtr.free ptr
                createInfos.[i] <- Unchecked.defaultof<_>

    interface IBackendSurface with
        member x.Handle = x :> obj

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ShaderProgram =
    open System.IO
    open FShade.GLSL

    let private versionRx = System.Text.RegularExpressions.Regex @"\#version.*$"
    let private layoutRx = System.Text.RegularExpressions.Regex @"layout[ \t]*\([ \t]*set[ \t]*\=[ \t]*(?<set>[0-9]+),[ \t]*binding[ \t]*\=[ \t]*(?<binding>[0-9]+)[ \t]*\)[ \t\r\n]*uniform[ \t]+(?<name>[_a-zA-Z0-9]+)[ \t\r\n]*\{"
    
    let private ofGLSLInteral (layout : Option<PipelineLayout>) (iface : FShade.GLSL.GLSLProgramInterface) (code : string) (layers : int) (perLayer : Set<string>) (device : Device) =
        let code =
            layoutRx.Replace(code, fun m ->
                let set = m.Groups.["set"].Value
                let binding = m.Groups.["binding"].Value
                let name = m.Groups.["name"].Value

                sprintf "layout(set = %s, binding = %s, std140)\r\nuniform %s\r\n{" set binding name
            )

        let code = code.Replace("gl_InstanceID", "gl_InstanceIndex").Replace("gl_VertexID", "gl_VertexIndex")

        let code =
            versionRx.Replace(code, "#version 450 core")

        if device.DebugConfig.PrintShaderCode then
            ShaderCodeReporting.logLines code

        let logs = System.Collections.Generic.Dictionary<FShade.ShaderSlot, string>()

        let shaders =
            match iface.shaders with 
            | GLSLProgramShaders.Graphics { stages = s } -> s
            | _ -> MapExt.empty

        let binaries =
            iface.shaders.Slots
            |> MapExt.toArray
            |> Array.map (fun (slot, shader) ->
                let entry = shader.shaderEntry
                let define = slot.Conditional
                let stage = ShaderModule.glslangStage slot

                match GLSLang.GLSLang.tryCompile stage entry [define] code with
                | Some binary, log ->
                    let binary = GLSLang.GLSLang.optimizeDefault binary
                    logs.[slot] <- log
                    slot, binary, iface
                | None, err ->
                    Log.error "[Vulkan] %A shader compilation failed: %A" slot err
                    failf "%A shader compilation failed: %A" slot err
            )

        let shaders =
            binaries
            |> Array.map (fun (slot, binary, iface) ->
                device.CreateShaderModule(slot, binary, iface)
            )

        let layout =
            match layout with
            | Some l -> l.AddReference(); l
            | None -> device.CreatePipelineLayout(shaders, layers, perLayer)

        new ShaderProgram(device, shaders, layout, code, iface)


    type private EffectCacheKey =
        {
            effect : FShade.Effect
            layout : FramebufferLayout
            topology : IndexedGeometryMode
            deviceCount : int
        }

    let private effectCache = Symbol.Create "effectCache"
    let private moduleCache = Symbol.Create "moduleCache"

    let ofGLSL (code : FShade.GLSL.GLSLShader) (device : Device) =
        ofGLSLInteral None code.iface code.code 1 Set.empty device

    module private PicklerExtensions = 
        open MBrace.FsPickler
        open System.Reflection
        open System.Reflection.Emit


        let tryUnifyTypes (decl : Type) (real : Type) =
            let assignment = System.Collections.Generic.Dictionary<Type, Type>()

            let rec recurse (decl : Type) (real : Type) =
                if decl = real then
                    true

                elif decl.IsGenericParameter then
                    match assignment.TryGetValue decl with
                        | (true, old) ->
                            if old.IsAssignableFrom real then 
                                true

                            elif real.IsAssignableFrom old then
                                assignment.[decl] <- real
                                true

                            else 
                                false
                        | _ ->
                            assignment.[decl] <- real
                            true
            
                elif decl.IsArray then
                    if real.IsArray then
                        let de = decl.GetElementType()
                        let re = real.GetElementType()
                        recurse de re
                    else
                        false

                elif decl.ContainsGenericParameters then
                    let dgen = decl.GetGenericTypeDefinition()
                    let rgen = 
                        if real.IsGenericType then real.GetGenericTypeDefinition()
                        else real

                    if dgen = rgen then
                        let dargs = decl.GetGenericArguments()
                        let rargs = real.GetGenericArguments()
                        Array.forall2 recurse dargs rargs

                    elif dgen.IsInterface then
                        let rface = real.GetInterface(dgen.FullName)
                        if isNull rface then
                            false
                        else
                            recurse decl rface

                    elif not (isNull real.BaseType) then
                        recurse decl real.BaseType

                    else
                        false

                elif decl.IsAssignableFrom real then
                    true

                else
                    false


            if recurse decl real then
                Some (assignment |> Dictionary.toSeq |> HashMap.ofSeq)
            else
                None

        type PicklerRegistry(types : list<Type>) =

            let picklerGen = typedefof<Pickler<_>>
            let allMeths = types |> List.collect (fun t -> t.GetMethods(BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic) |> Array.toList) //Introspection.GetAllMethodsWithAttribute<MyCrazyAttribute>() |> Seq.map (fun m -> m.E0) |> Seq.toArray

            let upcastToPicker (mi : MethodInfo) =
                let meth = 
                    DynamicMethod(
                        sprintf "upcasted.%s" mi.Name,
                        MethodAttributes.Public ||| MethodAttributes.Static,
                        CallingConventions.Standard,
                        typeof<Pickler>,
                        [| typeof<IPicklerResolver> |],
                        typeof<obj>,
                        true
                    )
                let il = meth.GetILGenerator()

                il.Emit(OpCodes.Ldarg_0)
                il.Emit(OpCodes.Tailcall)
                il.EmitCall(OpCodes.Call, mi, null)
                il.Emit(OpCodes.Ret)
                let func = 
                    meth.CreateDelegate(typeof<Func<IPicklerResolver, Pickler>>) 
                        |> unbox<Func<IPicklerResolver, Pickler>>        
                fun (r : IPicklerResolver) -> func.Invoke(r)

            let genericThings = 
                allMeths
                    |> List.filter (fun mi -> mi.GetGenericArguments().Length > 0)
                    |> List.choose (fun mi ->
                        let ret = mi.ReturnType
                        if ret.IsGenericType && ret.GetGenericTypeDefinition() = picklerGen && mi.GetParameters().Length = 1 then
                            let pickledType = ret.GetGenericArguments().[0]

                            let tryInstantiate (t : Type) =
                                match tryUnifyTypes pickledType t with
                                    | Some ass ->
                                        let targs = mi.GetGenericArguments() |> Array.map (fun a -> ass.[a])
                                        let mi = mi.MakeGenericMethod targs
                                        Some (upcastToPicker mi)
                                            
                                    | None ->
                                        None
                                        

                            Some tryInstantiate
                        else
                            None
                    )

            let nonGenericThings = 
                allMeths
                    |> List.filter (fun mi -> mi.GetGenericArguments().Length = 0)
                    |> List.choose (fun mi ->
                        let ret = mi.ReturnType
                        if ret.IsGenericType && ret.GetGenericTypeDefinition() = picklerGen && mi.GetParameters().Length = 1 then
                            let pickledType = ret.GetGenericArguments().[0]

                            let create = upcastToPicker mi
                            Some (pickledType, create)

                        else
                            None
                    )
                    |> Dictionary.ofList



                    
            member x.GetRegistration(t : Type) : CustomPicklerRegistration =
                if t.IsGenericType then
                    match genericThings |> List.tryPick (fun a -> a t) with
                        | Some r -> 
                            CustomPicklerRegistration.CustomPickler r
                        | None ->
                            match nonGenericThings.TryGetValue t with   
                                | (true, r) -> CustomPicklerRegistration.CustomPickler r
                                | _ -> CustomPicklerRegistration.UnRegistered
                else
                    match nonGenericThings.TryGetValue t with   
                        | (true, r) -> CustomPicklerRegistration.CustomPickler r
                        | _ -> CustomPicklerRegistration.UnRegistered
    //                            let pickler = 
    //                                nonGenericThings |> Seq.tryPick (fun (KeyValue(tdecl, pickler)) ->
    //                                    if tdecl.IsAssignableFrom t then
    //                                        let tc = typedefof<CoercePickler<_,_>>.MakeGenericType [| tdecl; t |]
    //                                        let resolve (r : IPicklerResolver) =
    //                                            let res = Activator.CreateInstance(tc, [| pickler r :> obj|])
    //                                            let prop = tc.GetProperty("Pickler", BindingFlags.NonPublic ||| BindingFlags.Public ||| BindingFlags.Instance)
    //                                            let real = prop.GetValue(res) |> unbox<Pickler>
    //                                            real
    //
    //                                        Some resolve
    //                                    else
    //                                        None
    //                                )
    //                            match pickler with
    //                                | Some p -> CustomPicklerRegistration.CustomPickler p
    //                                | _ -> CustomPicklerRegistration.UnRegistered

            interface ICustomPicklerRegistry with
                /// Look up pickler registration for particular type
                member x.GetRegistration(t : Type) : CustomPicklerRegistration = x.GetRegistration t


        type CustomPicklers private() =
            
            static member HSetPickler (r : IPicklerResolver) : Pickler<HashSet<'a>> =
                let l = r.Resolve<list<'a>>()

                let read (rs : ReadState) =
                    l.Read rs "AsList" |> HashSet.ofList
                    
                let write (ws : WriteState) (set : HashSet<'a>) =
                    l.Write ws "AsList" (set |> HashSet.toList)

                Pickler.FromPrimitives(read, write)

            static member MapExtPickler (r : IPicklerResolver) : Pickler<MapExt<'a, 'b>> =
                let l = r.Resolve<list<'a * 'b>>()

                let read (rs : ReadState) =
                    l.Read rs "AsMap" |> MapExt.ofList
                    
                let write (ws : WriteState) (set : MapExt<'a, 'b>) =
                    l.Write ws "AsMap" (set |> MapExt.toList)

                Pickler.FromPrimitives(read, write)

    let internal registry = PicklerExtensions.PicklerRegistry [ typeof<PicklerExtensions.CustomPicklers> ] :> MBrace.FsPickler.ICustomPicklerRegistry
    let internal cache = MBrace.FsPickler.PicklerCache.FromCustomPicklerRegistry(registry)
    let internal pickler = MBrace.FsPickler.FsPickler.CreateBinarySerializer(picklerResolver = cache)

    type private ShaderProgramData =
        {
            glsl        : string
            code        : Map<FShade.ShaderSlot, byte[]>
            iface       : GLSLProgramInterface
            layers      : int
            perLayer    : Set<string>
        }

    let toByteArray (program : ShaderProgram) =
        pickler.Pickle {
            glsl = program.GLSL
            code = program.Shaders |> Seq.map (fun s -> s.Slot, s.SpirV) |> Map.ofSeq
            iface = program.Interface
            layers = program.PipelineLayout.LayerCount
            perLayer = program.PipelineLayout.PerLayerUniforms
        }

    let tryOfByteArray (data : byte[]) (device : Device) =
        try
            let data : ShaderProgramData = pickler.UnPickle data

            let shaders =
                data.code
                |> Map.toArray
                |> Array.map (fun (slot, arr) ->
                    device.CreateShaderModule(slot, arr, data.iface)
                )

            Report.Begin(4, "Interface")
            let str = FShade.GLSL.GLSLProgramInterface.toString data.iface
            for line in str.Split([|"\r\n"|], StringSplitOptions.None) do
                Report.Line(4, "{0}", line)
            Report.End(4) |> ignore

            let layout = device.CreatePipelineLayout(shaders, data.layers, data.perLayer)

            let program = new ShaderProgram(device, shaders, layout, data.glsl, data.iface)
            Some program
        with _ ->
            None
            
    let ofByteArray (data : byte[]) (device : Device) =
        match tryOfByteArray data device with
            | Some p -> p
            | None -> failf "could not load program"

    let internal hashFileName (value : obj) : string =
        let hash = pickler.ComputeHash value
        hash.Hash |> Guid |> string

    let private tryRead (file : string) (device : Device) : Option<ShaderProgram> =
        if File.Exists file then
            Report.Begin(4, "[Vulkan] loading shader {0}", Path.GetFileName file)
            try
                let data = File.ReadAllBytes file
                match tryOfByteArray data device with
                    | Some c -> 
                        Report.End(4) |> ignore
                        Some c
                    | None ->
                        Log.warn "[Vulkan] bad shader cache '%s'" file
                        Report.End(4) |> ignore
                        None
            with exn ->
                Report.End(4) |> ignore
                Log.warn "[Vulkan] Failed to read from shader program file cache '%s': %s" file exn.Message
                None
        else
            None
    
    let private write (file : string) (program : ShaderProgram) =
        try
            let data = toByteArray program
            File.WriteAllBytes(file, data)
        with exn ->
            Log.warn "[Vulkan] Failed to write to shader program file cache '%s': %s" file exn.Message

    let ofModule (module_ : FShade.Imperative.Module) (device : Device) =
        device.GetCached(moduleCache, module_, fun module_ ->
            match device.ShaderCachePath with
                | Some shaderCachePath ->
                    let fileName = hashFileName (module_.hash)

                    let cacheFile = Path.Combine(shaderCachePath, fileName + ".module")
                    match tryRead cacheFile device with
                        | Some p -> p

                        | None ->
                            let glsl = 
                                module_ 
                                |> FShade.Imperative.ModuleCompiler.compile PipelineInfo.fshadeBackend
                                |> FShade.GLSL.Assembler.assemble PipelineInfo.fshadeBackend

                            let res = ofGLSL glsl device
                            write cacheFile res
                            res
                | None ->
                    let glsl = 
                        module_ 
                        |> FShade.Imperative.ModuleCompiler.compile PipelineInfo.fshadeBackend
                        |> FShade.GLSL.Assembler.assemble PipelineInfo.fshadeBackend

                    ofGLSL glsl device

        )

    let ofEffect (effect : FShade.Effect) (mode : IndexedGeometryMode) (pass : RenderPass) (device : Device) =
        let key : EffectCacheKey =
            {
                effect = effect
                layout = pass.Layout
                topology = mode
                deviceCount = pass.Runtime.DeviceCount
            }

        device.GetCached(effectCache, key, fun key ->
            match device.ShaderCachePath with
                | Some shaderCachePath ->
                    let fileName = 
                        let colors = key.layout.ColorAttachments |> Map.map (fun _ att -> att.Name.ToString())
                        let depth = key.layout.DepthStencilAttachment
                        if key.layout.LayerCount > 1 then
                            hashFileName (key.effect.Id, key.topology, colors, depth, key.layout.LayerCount, key.layout.PerLayerUniforms)
                        else
                            hashFileName (key.effect.Id, colors, depth)

                    let cacheFile = Path.Combine(shaderCachePath, fileName + ".effect")
                    match tryRead cacheFile device with
                        | Some p ->
                            if device.DebugConfig.VerifyShaderCacheIntegrity then
                                let glsl = 
                                    key.layout.Link(key.effect, key.deviceCount, PipelineInfo.fshadeConfig.depthRange, PipelineInfo.fshadeConfig.flipHandedness, key.topology)
                                    |> FShade.Imperative.ModuleCompiler.compile PipelineInfo.fshadeBackend
                                    |> FShade.GLSL.Assembler.assemble PipelineInfo.fshadeBackend

                                let temp = ofGLSL glsl device
                                let real = toByteArray p
                                let should = toByteArray temp
                                temp.Destroy()

                                if real <> should then
                                    let tmp = Path.GetTempFileName()
                                    let tmpReal = tmp + ".real"
                                    let tmpShould = tmp + ".should"
                                    File.WriteAllBytes(tmpReal, real)
                                    File.WriteAllBytes(tmpShould, should)
                                    failf "invalid cache for Effect: real: %s vs. should: %s" tmpReal tmpShould

                            p

                        | None ->
                            let glsl = 
                                key.layout.Link(key.effect, key.deviceCount, PipelineInfo.fshadeConfig.depthRange, PipelineInfo.fshadeConfig.flipHandedness, key.topology)
                                |> FShade.Imperative.ModuleCompiler.compile PipelineInfo.fshadeBackend
                                |> FShade.GLSL.Assembler.assemble PipelineInfo.fshadeBackend

                            let res = ofGLSL glsl device
                            write cacheFile res
                            res

                | None ->
                    let glsl = 
                        key.layout.Link(key.effect, key.deviceCount, PipelineInfo.fshadeConfig.depthRange, PipelineInfo.fshadeConfig.flipHandedness, key.topology)
                        |> FShade.Imperative.ModuleCompiler.compile PipelineInfo.fshadeBackend
                        |> FShade.GLSL.Assembler.assemble PipelineInfo.fshadeBackend

                    ofGLSL glsl device
        )




[<AbstractClass; Sealed; Extension>]
type ContextShaderProgramExtensions private() =
    [<Extension>]
    static member CreateShaderProgram(this : Device, pass : RenderPass, effect : FShade.Effect, top : IndexedGeometryMode) =
        ShaderProgram.ofEffect effect top pass this

    [<Extension>]
    static member CreateShaderProgram(this : Device, module_ : FShade.Imperative.Module) =
        ShaderProgram.ofModule module_ this

    [<Extension>]
    static member CreateShaderProgram(this : Device, pass : RenderPass, surface : ISurface) =
        match surface with
        | :? FShadeSurface as s ->
            this.CreateShaderProgram(pass, s.Effect, IndexedGeometryMode.TriangleList)
        | :? ShaderProgram as p ->
            p.AddReference()
            p
        | _ ->
            failf "unknown surface %A" surface