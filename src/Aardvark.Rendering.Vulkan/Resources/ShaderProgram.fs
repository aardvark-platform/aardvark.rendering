namespace Aardvark.Rendering.Vulkan

open System
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop
open FSharp.Data.Adaptive

module internal FShadeConfig =
    open FShade.GLSL

    let backend =
        let extensions =
            Set.ofList [
                "GL_ARB_tessellation_shader"
                "GL_ARB_separate_shader_objects"
                "GL_ARB_shading_language_420pack"
            ]

        Backend.Create {
            version                     = GLSLVersion(4,5,0)
            enabledExtensions           = extensions
            createUniformBuffers        = true
            bindingMode                 = BindingMode.Global
            createDescriptorSets        = true
            stepDescriptorSets          = false
            createInputLocations        = true
            createPerStageUniforms      = false
            reverseMatrixLogic          = true
            reverseTessellationWinding  = true
            createOutputLocations       = true
            createPassingLocations      = true
            depthWriteMode              = true
            useInOut                    = true
        }

    /// The target depth range passed to FShade.
    let depthRange =
        match RuntimeConfig.DepthRange with
        | DepthRange.ZeroToOne ->
            // Depth values produced by shaders are already expected to be in the native Vulkan [0, 1]
            // range. No conversion required, so tell FShade to convert from [-1, 1] to [-1, 1].
            Range1d(-1.0, 1.0)

        | DepthRange.MinusOneToOne ->
            // Depth values are not in the valid Vulkan range, so tell FShade to do the conversion.
            Range1d(0.0, 1.0)

        | r -> failf "unknown depth range %A" r


module private FShadeAdapter =
    open FShade
    open FShade.GLSL

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
        let siface = iface.shaders.[shader.Slot]
        match siface.shaderStage with
            | FShade.ShaderStage.Geometry -> geometryInfo <- Some <| FShadeAdapter.geometryInfo siface
            | FShade.ShaderStage.TessControl -> 
                match shaders |> Array.tryFind (fun (s : ShaderModule) -> s.Slot = FShade.ShaderSlot.TessEval) with
                    | Some eval -> 
                        tessInfo <- Some <| FShadeAdapter.tessControlInfo siface iface.shaders.[eval.Slot]
                    | None ->
                        ()
            | FShade.ShaderStage.Fragment-> fragInfo <- Some <| FShadeAdapter.fragmentInfo siface
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
                CStr.malloc iface.shaders.[shader.Slot].shaderEntry,
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
    open FShade

    let private versionRx = System.Text.RegularExpressions.Regex @"\#version.*$"
    let private layoutRx = System.Text.RegularExpressions.Regex @"layout[ \t]*\([ \t]*set[ \t]*\=[ \t]*(?<set>[0-9]+),[ \t]*binding[ \t]*\=[ \t]*(?<binding>[0-9]+)[ \t]*\)[ \t\r\n]*uniform[ \t]+(?<name>[_a-zA-Z0-9]+)[ \t\r\n]*\{"

    let private ofGLSLInteral (iface : FShade.GLSL.GLSLProgramInterface) (inputLayout : Option<EffectInputLayout>)
                              (code : string) (layers : int) (perLayer : Set<string>) (device : Device) =
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
            code |> ShaderCodeReporting.logLines "Compiling shader"

        let shaders =
            iface.shaders.Slots
            |> MapExt.toArray
            |> Array.map (fun (slot, _) ->
                device |> ShaderModule.ofGLSL slot { code = code; iface = iface }
            )

        let layout = PipelineLayout.ofProgramInterface inputLayout iface layers perLayer device
        new ShaderProgram(device, shaders, layout, code, iface)

    let ofGLSL (code : FShade.GLSL.GLSLShader) (device : Device) =
        ofGLSLInteral code.iface None code.code 1 Set.empty device


    type internal EffectCacheKey =
        {
            effect : FShade.Effect
            layout : FramebufferLayout
            topology : IndexedGeometryMode
            deviceCount : int
        }

    let private effectCache = Symbol.Create "effectCache"
    let private moduleCache = Symbol.Create "moduleCache"

    type internal ShaderProgramData =
        {
            shader   : GLSLShader
            binary   : Map<FShade.ShaderSlot, byte[]>
            layers   : int
            perLayer : Set<string>
        }

    module internal FileCacheName =
        let private pickler = MBrace.FsPickler.FsPickler.CreateBinarySerializer()

        let private getHash (value : obj) =
            let hash = pickler.ComputeHash value
            hash.Hash |> Guid |> string

        let ofEffectKey (key : EffectCacheKey) =
            let colors = key.layout.ColorAttachments |> Map.map (fun _ att -> string att.Name)
            let depth = key.layout.DepthStencilAttachment
            if key.layout.LayerCount > 1 then
                getHash (key.effect.Id, key.topology, colors, depth, key.layout.LayerCount, key.layout.PerLayerUniforms, FShadeConfig.depthRange)
            else
                getHash (key.effect.Id, colors, depth, FShadeConfig.depthRange)

        let ofString (value : string) =
            getHash value

    module internal ShaderProgramData =

        type BinaryWriter with
            member inline x.WriteType<'T>(_value : 'T) =
                x.Write(typeof<'T>.FullName)

            member inline x.Write(symbol : Symbol) =
                x.Write (string symbol)

            member x.Write(slot : ShaderSlot) =
                match slot with
                | ShaderSlot.Vertex                       -> x.Write 0uy
                | ShaderSlot.TessControl                  -> x.Write 1uy
                | ShaderSlot.TessEval                     -> x.Write 2uy
                | ShaderSlot.Geometry                     -> x.Write 3uy
                | ShaderSlot.Fragment                     -> x.Write 4uy
                | ShaderSlot.Compute                      -> x.Write 5uy
                | ShaderSlot.RayGeneration                -> x.Write 6uy
                | ShaderSlot.Miss name                    -> x.Write 7uy; x.Write name
                | ShaderSlot.Callable name                -> x.Write 8uy; x.Write name
                | ShaderSlot.AnyHit (name, rayType)       -> x.Write 9uy; x.Write name; x.Write rayType
                | ShaderSlot.ClosestHit (name, rayType)   -> x.Write 10uy; x.Write name; x.Write rayType
                | ShaderSlot.Intersection (name, rayType) -> x.Write 11uy; x.Write name; x.Write rayType

        type BinaryReader with
            member inline x.ReadType<'T>() =
                let expected = typeof<'T>.FullName
                let value = x.ReadString()

                if value <> expected then
                    raise <| InvalidDataException($"Expected value of type {expected} but encountered value of type {value}.")

            member inline x.ReadSym() =
                Sym.ofString <| x.ReadString()

            member x.ReadShaderSlot() =
                match x.ReadByte() with
                | 0uy  -> ShaderSlot.Vertex
                | 1uy  -> ShaderSlot.TessControl
                | 2uy  -> ShaderSlot.TessEval
                | 3uy  -> ShaderSlot.Geometry
                | 4uy  -> ShaderSlot.Fragment
                | 5uy  -> ShaderSlot.Compute
                | 6uy  -> ShaderSlot.RayGeneration
                | 7uy  -> ShaderSlot.Miss (x.ReadSym())
                | 8uy  -> ShaderSlot.Callable (x.ReadSym())
                | 9uy  -> ShaderSlot.AnyHit (x.ReadSym(), x.ReadSym())
                | 10uy -> ShaderSlot.ClosestHit (x.ReadSym(), x.ReadSym())
                | 11uy -> ShaderSlot.Intersection (x.ReadSym(), x.ReadSym())
                | id ->
                    raise <| InvalidDataException($"{id} is not a valid ShaderSlot identifier.")

        let serialize (dst : Stream) (data : ShaderProgramData) =
            use w = new BinaryWriter(dst, System.Text.Encoding.UTF8, true)

            w.WriteType data

            GLSLShader.serialize dst data.shader

            w.WriteType data.binary
            w.Write data.binary.Count

            for KeyValue(slot, binary) in data.binary do
                w.Write slot
                w.Write binary.Length
                w.Write binary

            w.WriteType data.layers
            w.Write data.layers

            w.WriteType data.perLayer
            w.Write data.perLayer.Count

            for name in data.perLayer do
                w.Write name

        let deserialize (src : Stream) =
            use r = new BinaryReader(src, System.Text.Encoding.UTF8, true)

            r.ReadType<ShaderProgramData>()

            let shader = GLSLShader.deserialize src

            r.ReadType<Map<FShade.ShaderSlot, byte[]>>()

            let binary =
                let count = r.ReadInt32()

                List.init count (fun _ ->
                    let slot = r.ReadShaderSlot()
                    let count = r.ReadInt32()
                    let binary = r.ReadBytes count

                    slot, binary
                )
                |> Map.ofList

            r.ReadType<int>()
            let layers = r.ReadInt32()

            r.ReadType<Set<string>>()

            let perLayer =
                let count = r.ReadInt32()

                List.init count (fun _ ->
                    r.ReadString()
                )
                |> Set.ofList

            { shader   = shader
              binary   = binary
              layers   = layers
              perLayer = perLayer }

        let pickle (data : ShaderProgramData) =
            use ms = new MemoryStream()
            serialize ms data
            ms.ToArray()

        let unpickle (data : byte[]) =
            use ms = new MemoryStream(data)
            deserialize ms

    let toByteArray (program : ShaderProgram) =
        ShaderProgramData.pickle {
            shader = { code = program.GLSL; iface = program.Interface }
            binary = program.Shaders |> Seq.map (fun s -> s.Slot, s.SpirV) |> Map.ofSeq
            layers = program.PipelineLayout.LayerCount
            perLayer = program.PipelineLayout.PerLayerUniforms
        }

    let ofByteArray (inputLayout : Option<EffectInputLayout>) (data : byte[]) (device : Device) =
        let data = ShaderProgramData.unpickle data

        let shaders =
            data.binary
            |> Map.toArray
            |> Array.map (fun (slot, arr) ->
                device.CreateShaderModule(slot, arr)
            )

        Report.Begin(4, "Interface")
        let str = FShade.GLSL.GLSLProgramInterface.toString data.shader.iface
        for line in str.Split([|"\r\n"|], StringSplitOptions.None) do
            Report.Line(4, "{0}", line)
        Report.End(4) |> ignore

        let layout = PipelineLayout.ofProgramInterface inputLayout data.shader.iface data.layers data.perLayer device
        new ShaderProgram(device, shaders, layout, data.shader.code, data.shader.iface)

    let tryOfByteArray (inputLayout : Option<EffectInputLayout>) (data : byte[]) (device : Device) =
        try
            Some <| ofByteArray inputLayout data device
        with _ ->
            None

    let private tryRead (inputLayout : Option<EffectInputLayout>) (file : string)  (device : Device) : Option<ShaderProgram> =
        if File.Exists file then
            Report.Begin(4, "[Vulkan] loading shader {0}", Path.GetFileName file)
            try
                try
                    let data = File.ReadAllBytes file
                    Some <| ofByteArray inputLayout data device
                with exn ->
                    Log.warn "[Vulkan] Failed to read from shader program file cache '%s': %s" file exn.Message
                    None
            finally
                Report.End(4) |> ignore
        else
            None

    let private write (file : string) (program : ShaderProgram) =
        try
            let data = toByteArray program
            File.writeAllBytesSafe file data
        with exn ->
            Log.warn "[Vulkan] Failed to write to shader program file cache '%s': %s" file exn.Message

    let ofModule (module_ : FShade.Imperative.Module) (device : Device) =
        let hash = module_.ComputeHash()

        let inputLayout =
            match module_.UserData with
            | :? (obj * EffectInputLayout) as (_, layout) -> Some layout
            | _ -> None

        let compile() =
            let glsl =
                try
                    module_
                    |> FShade.Imperative.ModuleCompiler.compile FShadeConfig.backend
                    |> FShade.GLSL.Assembler.assemble FShadeConfig.backend
                with exn ->
                    Log.error "%s" exn.Message
                    reraise()

            ofGLSLInteral glsl.iface inputLayout glsl.code 1 Set.empty device

        device.GetCached(moduleCache, hash, fun hash ->
            match device.ShaderCachePath with
            | Some shaderCachePath ->
                let fileName = FileCacheName.ofString hash
                let cacheFile = Path.Combine(shaderCachePath, fileName + ".module")
                match tryRead inputLayout cacheFile device with
                | Some p -> p
                | None ->
                    let res = compile()
                    write cacheFile res
                    res
            | None ->
                compile()
        )

    let ofEffect (effect : FShade.Effect) (mode : IndexedGeometryMode) (pass : RenderPass) (device : Device) =
        let key : EffectCacheKey =
            {
                effect = effect
                layout = pass.Layout
                topology = mode
                deviceCount = pass.Runtime.DeviceCount
            }

        let compile() =
            let glsl =
                try
                    key.layout.Link(key.effect, key.deviceCount, FShadeConfig.depthRange, false, key.topology)
                    |> FShade.Imperative.ModuleCompiler.compile FShadeConfig.backend
                    |> FShade.GLSL.Assembler.assemble FShadeConfig.backend
                with exn ->
                    Log.error "%s" exn.Message
                    reraise()

            ofGLSL glsl device

        device.GetCached(effectCache, key, fun key ->
            match device.ShaderCachePath with
            | Some shaderCachePath ->
                let cacheFile =
                    let name = FileCacheName.ofEffectKey key
                    Path.Combine(shaderCachePath, name + ".effect")

                match tryRead None cacheFile device with
                | Some p ->
                    if device.DebugConfig.VerifyShaderCacheIntegrity then
                        let temp = compile()
                        let real = toByteArray p
                        let should = toByteArray temp
                        temp.Destroy()

                        if real <> should then
                            let tmp = Path.GetTempFileName()
                            let tmpReal = tmp + ".real"
                            let tmpShould = tmp + ".should"
                            File.writeAllBytesSafe tmpReal real
                            File.writeAllBytesSafe tmpShould should
                            failf "invalid cache for Effect: real: %s vs. should: %s" tmpReal tmpShould

                    p

                | None ->
                    let res = compile()
                    write cacheFile res
                    res

            | None ->
                compile()
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