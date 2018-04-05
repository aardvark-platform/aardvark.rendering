namespace Aardvark.Rendering.GL


open System
open Microsoft.FSharp.NativeInterop
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Aardvark.Base
open Aardvark.Base.ShaderReflection

#nowarn "9"

[<AutoOpen>]
module ProgramResourceExtensions = 

    type GraphicsContext with
        static member GetFunction<'a when 'a :> Delegate>(name : string) =
            let ctx = GraphicsContext.CurrentContext |> unbox<IGraphicsContextInternal>
            let ptr = ctx.GetAddress name
            if ptr = 0n then failwithf "[GL] could not get address for %s" name
            System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer(ptr, typeof<'a>) |> unbox<'a>
                    
    module private Impl = 
        type GetProgramResourceName = delegate of int * ProgramInterface * int * int * byref<int> * byte[] -> unit
        type GetProgramResource = delegate of int * ProgramInterface * int * int * byref<ProgramProperty> * int * byref<int> * array<int> -> unit

        let glGetProgramResourceName = lazy ( GraphicsContext.GetFunction<GetProgramResourceName> "glGetProgramResourceName" )
        let glGetProgramResourceiv = lazy ( GraphicsContext.GetFunction<GetProgramResource> "glGetProgramResourceiv" )

    type GL with
        static member GetProgramResource(program : int, iface : ProgramInterface, index : int, prop : ProgramProperty) =
            let mutable length = 0
            let mutable prop = prop
            let mutable res = [| 0 |]
            Impl.glGetProgramResourceiv.Value.Invoke(program, iface, index, 1, &prop, 1, &length, res)
            GL.Check "GetProgramResource[0]"
            res.[0]

        static member GetProgramResource(program : int, iface : ProgramInterface, index : int, prop : ProgramProperty, cnt : int) =
            let mutable length = 0
            let mutable prop = prop
            let mutable res = Array.zeroCreate cnt
            Impl.glGetProgramResourceiv.Value.Invoke(program, iface, index, 1, &prop, cnt, &length, res)
            GL.Check "GetProgramResource"
            res

        static member GetProgramResourceName(program : int, iface : ProgramInterface, index : int) =
            let len = GL.GetProgramResource(program, iface, index, ProgramProperty.NameLength)
            let buffer = Array.zeroCreate len
            let mutable realLength = 0
            Impl.glGetProgramResourceName.Value.Invoke(program, iface, index, len, &realLength, buffer)
            GL.Check "GetProgramResourceName"
            System.Text.Encoding.UTF8.GetString(buffer, 0, realLength)


        static member GetProgramResourceType(program : int, iface : ProgramInterface, index : int) =
            GL.GetProgramResource(program, iface, index, ProgramProperty.Type) |> unbox<ActiveUniformType>
                    
        static member GetProgramResourceLocation(program : int, iface : ProgramInterface, index : int) =
            GL.GetProgramResource(program, iface, index, ProgramProperty.Location)

    type ProgramProperty with
        static member inline ReferencedByComputeShader = 0x930B |> unbox<ProgramProperty>

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ShaderParameterType =
    let ofActiveUniformType =
        LookupTable.lookupTable [
            ActiveUniformType.Bool, Bool
            ActiveUniformType.BoolVec2, Vector(Bool, 2)
            ActiveUniformType.BoolVec3, Vector(Bool, 3)
            ActiveUniformType.BoolVec4, Vector(Bool, 4)

            ActiveUniformType.Double, Double
            ActiveUniformType.DoubleVec2, Vector(Double, 2)
            ActiveUniformType.DoubleVec3, Vector(Double, 3)
            ActiveUniformType.DoubleVec4, Vector(Double, 4)

            ActiveUniformType.Float, Float
            ActiveUniformType.FloatVec2, Vector(Float, 2)
            ActiveUniformType.FloatVec3, Vector(Float, 3)
            ActiveUniformType.FloatVec4, Vector(Float, 4)
            ActiveUniformType.FloatMat2, Matrix(Float, 2, 2, false)
            ActiveUniformType.FloatMat2x3, Matrix(Float, 2, 3, false)
            ActiveUniformType.FloatMat2x4, Matrix(Float, 2, 4, false)
            ActiveUniformType.FloatMat3x2, Matrix(Float, 3, 2, false)
            ActiveUniformType.FloatMat3, Matrix(Float, 3, 3, false)
            ActiveUniformType.FloatMat3x4, Matrix(Float, 3, 4, false)
            ActiveUniformType.FloatMat4, Matrix(Float, 4, 4, false)
            ActiveUniformType.FloatMat4x2, Matrix(Float, 4, 2, false)
            ActiveUniformType.FloatMat4x3, Matrix(Float, 4, 3, false)

            ActiveUniformType.Int,                      Int
            ActiveUniformType.IntVec2,                  Vector(Int, 2)
            ActiveUniformType.IntVec3,                  Vector(Int, 3)
            ActiveUniformType.IntVec4,                  Vector(Int, 4)

            ActiveUniformType.UnsignedInt,              UnsignedInt
            ActiveUniformType.UnsignedIntVec2,          Vector(UnsignedInt, 2)
            ActiveUniformType.UnsignedIntVec3,          Vector(UnsignedInt, 3)
            ActiveUniformType.UnsignedIntVec4,          Vector(UnsignedInt, 4)


            ActiveUniformType.Image1D,                      Image(Float, TextureDimension.Texture1D, false, false)
            ActiveUniformType.Image1DArray,                 Image(Float, TextureDimension.Texture1D, false, true)
            ActiveUniformType.Image2D,                      Image(Float, TextureDimension.Texture2D, false, false)
            ActiveUniformType.Image2DArray,                 Image(Float, TextureDimension.Texture2D, false, true)
            ActiveUniformType.Image2DMultisample,           Image(Float, TextureDimension.Texture2D, true, false)
            ActiveUniformType.Image2DMultisampleArray,      Image(Float, TextureDimension.Texture2D, true, true)
            ActiveUniformType.Image2DRect,                  Image(Float, TextureDimension.Texture2D, false, false)
            ActiveUniformType.Image3D,                      Image(Float, TextureDimension.Texture3D, false, false)
            ActiveUniformType.ImageBuffer,                  Image(Float, TextureDimension.Texture1D, false, false)
            ActiveUniformType.ImageCube,                    Image(Float, TextureDimension.TextureCube, false, false)
            ActiveUniformType.ImageCubeMapArray,            Image(Float, TextureDimension.TextureCube, false, true)

            ActiveUniformType.IntImage1D,                   Image(Int, TextureDimension.Texture1D, false, false)
            ActiveUniformType.IntImage1DArray,              Image(Int, TextureDimension.Texture1D, false, true)
            ActiveUniformType.IntImage2D,                   Image(Int, TextureDimension.Texture2D, false, false)
            ActiveUniformType.IntImage2DArray,              Image(Int, TextureDimension.Texture2D, false, true)
            ActiveUniformType.IntImage2DMultisample,        Image(Int, TextureDimension.Texture2D, true, false)
            ActiveUniformType.IntImage2DMultisampleArray,   Image(Int, TextureDimension.Texture2D, true, true)
            ActiveUniformType.IntImage2DRect,               Image(Int, TextureDimension.Texture2D, false, false)
            ActiveUniformType.IntImage3D,                   Image(Int, TextureDimension.Texture3D, false, false)
            ActiveUniformType.IntImageBuffer,               Image(Int, TextureDimension.Texture1D, false, false)
            ActiveUniformType.IntImageCube,                 Image(Int, TextureDimension.TextureCube, false, false)
            ActiveUniformType.IntImageCubeMapArray,         Image(Int, TextureDimension.TextureCube, false, true)

            ActiveUniformType.IntSampler1D,                 Sampler(Int, TextureDimension.Texture1D, false, false, false) 
            ActiveUniformType.IntSampler1DArray,            Sampler(Int, TextureDimension.Texture1D, false, true, false)
            ActiveUniformType.IntSampler2D,                 Sampler(Int, TextureDimension.Texture2D, false, false, false)
            ActiveUniformType.IntSampler2DArray,            Sampler(Int, TextureDimension.Texture2D, false, true, false)
            ActiveUniformType.IntSampler2DMultisample,      Sampler(Int, TextureDimension.Texture2D, true, false, false)
            ActiveUniformType.IntSampler2DMultisampleArray, Sampler(Int, TextureDimension.Texture2D, true, true, false)
            ActiveUniformType.IntSampler2DRect,             Sampler(Int, TextureDimension.Texture2D, false, false, false)
            ActiveUniformType.IntSampler3D,                 Sampler(Int, TextureDimension.Texture3D, false, false, false)
            ActiveUniformType.IntSamplerBuffer,             Sampler(Int, TextureDimension.Texture1D, false, false, false)
            ActiveUniformType.IntSamplerCube,               Sampler(Int, TextureDimension.TextureCube, false, false, false)
            ActiveUniformType.IntSamplerCubeMapArray,       Sampler(Int, TextureDimension.TextureCube, false, true, false)

            ActiveUniformType.Sampler1D,                    Sampler(Float, TextureDimension.Texture1D, false, false, false) 
            ActiveUniformType.Sampler1DArray,               Sampler(Float, TextureDimension.Texture1D, false, true, false)
            ActiveUniformType.Sampler2D,                    Sampler(Float, TextureDimension.Texture2D, false, false, false)
            ActiveUniformType.Sampler2DArray,               Sampler(Float, TextureDimension.Texture2D, false, true, false)
            ActiveUniformType.Sampler2DMultisample,         Sampler(Float, TextureDimension.Texture2D, true, false, false)
            ActiveUniformType.Sampler2DMultisampleArray,    Sampler(Float, TextureDimension.Texture2D, true, true, false)
            ActiveUniformType.Sampler2DRect,                Sampler(Float, TextureDimension.Texture2D, false, false, false)
            ActiveUniformType.Sampler3D,                    Sampler(Float, TextureDimension.Texture3D, false, false, false)
            ActiveUniformType.SamplerBuffer,                Sampler(Float, TextureDimension.Texture1D, false, false, false)
            ActiveUniformType.SamplerCube,                  Sampler(Float, TextureDimension.TextureCube, false, false, false)
            ActiveUniformType.SamplerCubeMapArray,          Sampler(Float, TextureDimension.TextureCube, false, true, false)
                    

            ActiveUniformType.Sampler1DShadow,                    Sampler(Float, TextureDimension.Texture1D, false, false, true) 
            ActiveUniformType.Sampler1DArrayShadow,               Sampler(Float, TextureDimension.Texture1D, false, true, true)
            ActiveUniformType.Sampler2DShadow,                    Sampler(Float, TextureDimension.Texture2D, false, false, true)
            ActiveUniformType.Sampler2DArrayShadow,               Sampler(Float, TextureDimension.Texture2D, false, true, true)
            ActiveUniformType.Sampler2DRectShadow,                Sampler(Float, TextureDimension.Texture2D, false, false, true)
            ActiveUniformType.SamplerCubeShadow,                  Sampler(Float, TextureDimension.TextureCube, false, false, true)
            ActiveUniformType.SamplerCubeMapArrayShadow,          Sampler(Float, TextureDimension.TextureCube, false, true, true)
                    

            ActiveUniformType.UnsignedIntAtomicCounter,           AtomicCounter UnsignedInt

            ActiveUniformType.UnsignedIntImage1D,                   Image(UnsignedInt, TextureDimension.Texture1D, false, false)
            ActiveUniformType.UnsignedIntImage1DArray,              Image(UnsignedInt, TextureDimension.Texture1D, false, true)
            ActiveUniformType.UnsignedIntImage2D,                   Image(UnsignedInt, TextureDimension.Texture2D, false, false)
            ActiveUniformType.UnsignedIntImage2DArray,              Image(UnsignedInt, TextureDimension.Texture2D, false, true)
            ActiveUniformType.UnsignedIntImage2DMultisample,        Image(UnsignedInt, TextureDimension.Texture2D, true, false)
            ActiveUniformType.UnsignedIntImage2DMultisampleArray,   Image(UnsignedInt, TextureDimension.Texture2D, true, true)
            ActiveUniformType.UnsignedIntImage2DRect,               Image(UnsignedInt, TextureDimension.Texture2D, false, false)
            ActiveUniformType.UnsignedIntImage3D,                   Image(UnsignedInt, TextureDimension.Texture3D, false, false)
            ActiveUniformType.UnsignedIntImageBuffer,               Image(UnsignedInt, TextureDimension.Texture1D, false, false)
            ActiveUniformType.UnsignedIntImageCube,                 Image(UnsignedInt, TextureDimension.TextureCube, false, false)
            ActiveUniformType.UnsignedIntImageCubeMapArray,         Image(UnsignedInt, TextureDimension.TextureCube, false, true)

                    

            ActiveUniformType.UnsignedIntSampler1D,                 Sampler(UnsignedInt, TextureDimension.Texture1D, false, false, false) 
            ActiveUniformType.UnsignedIntSampler1DArray,            Sampler(UnsignedInt, TextureDimension.Texture1D, false, true, false)
            ActiveUniformType.UnsignedIntSampler2D,                 Sampler(UnsignedInt, TextureDimension.Texture2D, false, false, false)
            ActiveUniformType.UnsignedIntSampler2DArray,            Sampler(UnsignedInt, TextureDimension.Texture2D, false, true, false)
            ActiveUniformType.UnsignedIntSampler2DMultisample,      Sampler(UnsignedInt, TextureDimension.Texture2D, true, false, false)
            ActiveUniformType.UnsignedIntSampler2DMultisampleArray, Sampler(UnsignedInt, TextureDimension.Texture2D, true, true, false)
            ActiveUniformType.UnsignedIntSampler2DRect,             Sampler(UnsignedInt, TextureDimension.Texture2D, false, false, false)
            ActiveUniformType.UnsignedIntSampler3D,                 Sampler(UnsignedInt, TextureDimension.Texture3D, false, false, false)
            ActiveUniformType.UnsignedIntSamplerBuffer,             Sampler(UnsignedInt, TextureDimension.Texture1D, false, false, false)
            ActiveUniformType.UnsignedIntSamplerCube,               Sampler(UnsignedInt, TextureDimension.TextureCube, false, false, false)
            ActiveUniformType.UnsignedIntSamplerCubeMapArray,       Sampler(UnsignedInt, TextureDimension.TextureCube, false, true, false)


        ]
    

    open FShade.GLSL

    let rec ofGLSLType (t : GLSLType) =
        match t with
            | GLSLType.Void -> failwithf "[GL] void cannot be a parameter type"
            | GLSLType.Bool -> ShaderParameterType.Bool
            | GLSLType.Int(true,(8|16|32)) -> ShaderParameterType.Int
            | GLSLType.Int(false,(8|16|32)) -> ShaderParameterType.UnsignedInt
            | GLSLType.Float((16|32|64)) -> ShaderParameterType.Float

            | GLSLType.Vec(d,b) -> ShaderParameterType.Vector(ofGLSLType b, d)
            | GLSLType.Mat(r,c,b) -> ShaderParameterType.Matrix(ofGLSLType b, r, c, true)
            | GLSLType.Array(l, b, s) -> ShaderParameterType.FixedArray(ofGLSLType b, s, l)
            | GLSLType.Struct(name, fields, size) ->
                ShaderParameterType.Struct(size, fields |> List.map (fun (name, typ, offset) -> { Name = name; Type = ofGLSLType typ; Offset = offset }))

        
            | _ -> failwithf "[GL] bad parameter type: %A" t


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ShaderInterface =
    [<AutoOpen>]
    module private Helpers = 
        let arrayRx = System.Text.RegularExpressions.Regex @"^(?<name>[^\.\[\]]*)\[[0]\]$"

        let variableIFace =
            Dictionary.ofList [
                ProgramInterface.ShaderStorageBlock, ProgramInterface.BufferVariable
                ProgramInterface.UniformBlock, ProgramInterface.Uniform
            ]

        let rx = System.Text.RegularExpressions.Regex @"^(?<name>[a-zA-Z_0-9]+)((\[(?<index>[0-9]+)\])|(\.)|)(?<rest>.*)$"

        let parsePath path =
            let rec parsePath (parent : Option<ShaderPath>) (path : string) =
                if path.Length = 0 then
                    match parent with
                        | Some path -> path
                        | _ -> ShaderPath.Value ""
                else
                    let m = rx.Match path
                    if m.Success then
                        let name = m.Groups.["name"].Value
                        let rest = m.Groups.["rest"].Value
                        let rest = if rest.StartsWith "." then rest.Substring(1) else rest

                        let path =
                            match parent with
                                | Some p -> ShaderPath.Field(p, name)
                                | None -> ShaderPath.Value name

                        let index = m.Groups.["index"]
                        if index.Success then
                            let index = index.Value |> Int32.Parse

                            let index =
                                match parent with
                                    | None -> index
                                    | _ -> index

                            parsePath (Some (ShaderPath.Item(path, index))) rest
                        else
                            parsePath (Some path) rest
                
                    else
                        failwithf "[GL] bad path: %A" path

            parsePath None path

        let rec collapseFields (changed : byref<bool>) (l : list<ShaderBlockField>) =
            match l with
                | [] -> []
                | [f] -> [f]
                | l :: r :: rest ->
                    let ls = ShaderParameterType.sizeof l.Type
                    match l.Path, r.Path with
                        | ShaderPath.Field(lp,ln), ShaderPath.Field(rp, rn) when lp = rp ->
                            let rs = ShaderParameterType.sizeof r.Type
                            let leftField =
                                {
                                    Name            = ln
                                    Type            = l.Type
                                    Offset          = 0
                                }

                            let rightField =
                                {
                                    Name            = rn
                                    Type            = r.Type
                                    Offset          = r.Offset - l.Offset
                                }


                            let tStruct = Struct(rightField.Offset + rs, [ leftField; rightField ])

                            let newField =
                                {
                                    Path            = lp
                                    Type            = tStruct
                                    Offset          = l.Offset
                                    Referenced      = Set.union l.Referenced r.Referenced
                                }

                            changed <- true

                            collapseFields &changed (newField :: rest)

                        | _ ->
                            match l.Type, r.Path with
                                | Struct(size,fields), ShaderPath.Field(rp, rn) when l.Path = rp && l.Offset + size = r.Offset ->
                                

                                    let rightField =
                                        {
                                            Name            = rn
                                            Type            = r.Type
                                            Offset          = r.Offset - l.Offset
                                        }

                                    let newSize = rightField.Offset + ShaderParameterType.sizeof r.Type
                                    let newType = Struct(newSize, fields @ [rightField])
                                    changed <- true
                                    collapseFields &changed ({ l with Type = newType } :: rest)
                                | _ ->
                                    l :: collapseFields &changed (r :: rest)
       
        let rec collapseArrays (changed : byref<bool>)  (l : list<ShaderBlockField>) =
            match l with
                | [] -> []
                | [f] -> [f]
                | l :: r :: rest ->
                    let ls = ShaderParameterType.sizeof l.Type
                    match l.Path, r.Path with
                        | ShaderPath.Item(lp, 0), ShaderPath.Item(rp, 1) when l.Type = r.Type && lp = rp  ->
                            let stride = r.Offset - l.Offset
                            let newField =
                                {
                                    Path            = lp
                                    Type            = FixedArray(l.Type, stride, 2)
                                    Offset          = l.Offset
                                    Referenced      = Set.union l.Referenced r.Referenced
                                }

                            changed <- true
                            collapseArrays &changed (newField :: rest)

                        | _ ->
                            match l.Type, r.Path with
                                | FixedArray(lt, stride, len), ShaderPath.Item(rp, ri) when lt = r.Type && l.Path = rp && ri = len && r.Offset = l.Offset + ri * stride ->
                                    changed <- true
                                    collapseArrays &changed ({ l with Type = FixedArray(lt, stride, len + 1) } :: rest)

                                | _ ->
                                    l :: collapseArrays &changed (r :: rest)

        let collapse (l : list<ShaderBlockField>) =
            let mutable res = l
            let mutable changed = true
            while changed do
                changed <- false
                res <- res |> collapseFields &changed |> collapseArrays &changed

            res

    module private GL4 = 
        let getReferencingStages (p : int) (iface : ProgramInterface) (id : int) =
            let vs = GL.GetProgramResource(p, iface, id, ProgramProperty.ReferencedByVertexShader) = 1
            let tc = GL.GetProgramResource(p, iface, id, ProgramProperty.ReferencedByTessControlShader) = 1
            let te = GL.GetProgramResource(p, iface, id, ProgramProperty.ReferencedByTessEvaluationShader) = 1
            let gs = GL.GetProgramResource(p, iface, id, ProgramProperty.ReferencedByGeometryShader) = 1
            let fs = GL.GetProgramResource(p, iface, id, ProgramProperty.ReferencedByFragmentShader) = 1
            let cs = GL.GetProgramResource(p, iface, id, ProgramProperty.ReferencedByComputeShader) = 1


            Set.ofList [
                if vs then yield ShaderStage.Vertex
                if tc then yield ShaderStage.TessControl
                if te then yield ShaderStage.TessEval
                if gs then yield ShaderStage.Geometry
                if fs then yield ShaderStage.Fragment
                if cs then yield ShaderStage.Compute
            ]

        let blocks (iface : ProgramInterface) (p : int) =
            let mutable cnt = 0
            GL.GetProgramInterface(p, iface, ProgramInterfaceParameter.ActiveResources, &cnt)
                
            List.init cnt (fun bi ->
                let name = GL.GetProgramResourceName(p, iface, bi)
                let size = GL.GetProgramResource(p, iface, bi, ProgramProperty.BufferDataSize)
                
                //let location = GL.GetProgramResource(p, iface, bi, ProgramProperty.BlockIndex)

                let vCount = GL.GetProgramResource(p, iface, bi, ProgramProperty.NumActiveVariables)
                let vIndices = GL.GetProgramResource(p, iface, bi, ProgramProperty.ActiveVariables, vCount)

                let variables = 
                    let iface = variableIFace.[iface]
                    vIndices |> Array.toList |> List.map (fun vi ->
                        let name        = GL.GetProgramResourceName(p, iface, vi)
                        let _type       = GL.GetProgramResourceType(p, iface, vi) |> ShaderParameterType.ofActiveUniformType
                        let offset      = GL.GetProgramResource(p, iface, vi, ProgramProperty.Offset)
                        let isRowMajor  = GL.GetProgramResource(p, iface, vi, ProgramProperty.IsRowMajor) = 1
                        let stride      = GL.GetProgramResource(p, iface, vi, ProgramProperty.ArrayStride)

                        let _type =
                            if isRowMajor then ShaderParameterType.makeRowMajor _type
                            else _type

                        try
                            let _type, pathName =
                                let m = arrayRx.Match(name) 
                                if m.Success then
                                    let elementName = m.Groups.["name"].Value
                                    let size = GL.GetProgramResource(p, iface, vi, ProgramProperty.ArraySize)
                                    if size <= 0 then
                                        DynamicArray(_type, stride), elementName
                                    else
                                        FixedArray(_type, stride, size), elementName
                                else
                                    _type, name
                            {
                                Path           = parsePath pathName
                                Type           = _type
                                Offset         = offset
                                Referenced     = getReferencingStages p iface vi
                            }
                        with
                        | e -> 
                            Log.warn "error processing shader resource info\n    name = \"%s\"\n    type = %A\n    offset = %d\n    isRowMajor = %A\n    stride = %d" name _type offset isRowMajor stride
                            reraise()
                    )
                    
                match iface with
                    | ProgramInterface.UniformBlock -> GL.UniformBlockBinding(p, bi, bi)
                    | ProgramInterface.ShaderStorageBlock -> GL.ShaderStorageBlockBinding(p, bi, bi)
                    | _ -> ()

                {
                    Index = bi
                    Name = name
                    Fields = variables |> List.sortBy (fun v -> v.Offset) |> collapse
                    Referenced = getReferencingStages p iface bi
                    DataSize = size
                }
            )

        let parameters (textureSlots : ref<int>) (iface : ProgramInterface) (p : int) =
            let mutable cnt = 0
            GL.GetProgramInterface(p, iface, ProgramInterfaceParameter.ActiveResources, &cnt)

            List.init cnt id |> List.collect (fun pi ->
                if (iface <> ProgramInterface.Uniform && iface <> ProgramInterface.BufferVariable) || GL.GetProgramResource(p, iface, pi, ProgramProperty.BlockIndex) = -1 then
                    let name = GL.GetProgramResourceName(p, iface, pi)
                    let _type = GL.GetProgramResourceType(p, iface, pi) |> ShaderParameterType.ofActiveUniformType
                    let location = GL.GetProgramResourceLocation(p, iface, pi)
                    let size = GL.GetProgramResource(p, iface, pi, ProgramProperty.ArraySize)
                    
                    let path, _type =
                        match parsePath name with
                            | ShaderPath.Item(path, 0) ->
                                path, FixedArray(_type, -1, size)
                            | path ->
                                if size > 1 then 
                                    path, FixedArray(_type, -1, size)
                                else
                                    path, _type

                    if iface = ProgramInterface.Uniform then
                        match _type with
                            | Sampler _ | Image _ -> 
                                let slot = !textureSlots
                                textureSlots := slot + 1
                                GL.ProgramUniform1(p, location, slot)
                                List.singleton {
                                    Binding         = slot
                                    Location        = location
                                    Path            = path
                                    Type            = _type
                                }

                            | FixedArray((Sampler _ | Image _) as t, _, length) ->
                                let slots = 
                                    Array.init length (fun i ->
                                        let slot = !textureSlots
                                        textureSlots := slot + 1
                                        slot
                                    )

                                let gc = System.Runtime.InteropServices.GCHandle.Alloc(slots, System.Runtime.InteropServices.GCHandleType.Pinned)

                                GL.ProgramUniform1(uint32 p, location, slots.Length, NativePtr.ofNativeInt<int> (gc.AddrOfPinnedObject()))
                                gc.Free()

                                slots |> Array.toList |> List.mapi (fun i s ->
                                    {
                                        Binding         = s
                                        Location        = location
                                        Path            = ShaderPath.Item(path, i)
                                        Type            = t
                                    }
                                )


                            | _ ->
                                List.singleton {
                                    Location        = location
                                    Binding         = -1
                                    Path            = path
                                    Type            = _type
                                }
                    else
                        List.singleton {
                            Location        = location
                            Binding         = -1
                            Path            = path
                            Type            = _type
                        }

                else 
                    []

            )

        let shaderInterface (baseSlot : int) (p : int) =
            let slot = ref baseSlot
            
            {
                Inputs              = p |> parameters slot ProgramInterface.ProgramInput
                Outputs             = p |> parameters slot ProgramInterface.ProgramOutput
                Uniforms            = p |> parameters slot ProgramInterface.Uniform
                UniformBlocks       = p |> blocks ProgramInterface.UniformBlock
                StorageBlocks       = p |> blocks ProgramInterface.ShaderStorageBlock
                UsedBuiltInOutputs  = Map.empty
                UsedBuiltInInputs   = Map.empty
            }

    module private GL =

        let getInputs (p : int) =
            let cnt = GL.GetProgram(p, GetProgramParameterName.ActiveAttributes)

            List.init cnt (fun i ->
                let mutable length = 0
                let mutable t = ActiveAttribType.None
                let mutable size = 1
                let builder = System.Text.StringBuilder(1024)
                GL.GetActiveAttrib(p, i, 1024, &length, &size, &t, builder)
                let name = builder.ToString()
                let location = GL.GetAttribLocation(p, name)

                let _type = ShaderParameterType.ofActiveUniformType (unbox<_> (int t))

                let _type = 
                    if size > 1 then FixedArray(_type, ShaderParameterType.sizeof _type, size)
                    else _type
                    

                {
                    Location        = location
                    Binding         = -1
                    Path            = parsePath name
                    Type            = _type
                }
            )

        let getUniformField (referenced : Set<ShaderStage>) (ui : int) (p : int) =
            let mutable ui = ui
            let mutable length = 0
            let mutable size = 0
            let mutable uniformType = ActiveUniformType.Float
            let builder = System.Text.StringBuilder(1024)
            GL.GetActiveUniform(p, ui, 1024, &length, &size, &uniformType, builder)
            let path = builder.ToString() |> parsePath
            let uniformType = ShaderParameterType.ofActiveUniformType uniformType

            let mutable isRowMajor = 0
            GL.GetActiveUniforms(p, 1, &ui, ActiveUniformParameter.UniformIsRowMajor, &isRowMajor)
            let uniformType = 
                if isRowMajor = 1 then ShaderParameterType.makeRowMajor uniformType
                else uniformType
                
            let path, uniformType =
                match path with
                    | ShaderPath.Item(path, 0) ->
                        let mutable stride = 0
                        GL.GetActiveUniforms(p, 1, &ui, ActiveUniformParameter.UniformArrayStride, &stride)
                        path, FixedArray(uniformType, stride, size)

                    | _ ->
                        path, uniformType

            let mutable offset = 0
            GL.GetActiveUniforms(p, 1, &ui, ActiveUniformParameter.UniformOffset, &offset)


            {
                Path            = path
                Type            = uniformType
                Offset          = offset
                Referenced      = referenced
            }

        let getNameUB (ui : int) (p : int) =
            let builder = System.Text.StringBuilder(1024)
            let mutable l = 0
            GL.GetActiveUniformBlockName(p, ui, 1024, &l, builder)
            builder.ToString()

        let getUniforms (textureSlot : ref<int>) (p : int) =
            let mutable cnt = 0
            GL.GetProgram(p, GetProgramParameterName.ActiveUniforms, &cnt)

            List.init cnt id |> List.collect (fun i ->
                let mutable i = i
                let field = getUniformField Set.empty i p
                match field.Path with
                    | ShaderPath.Value name -> 
                        let location = GL.GetUniformLocation(p, name)
                        if location >= 0 then
                            let path = parsePath name

                            match field.Type with
                                | (Sampler _ | Image _) ->
                                    let slot = !textureSlot
                                    textureSlot := slot + 1
                                    GL.Uniform1(location, slot)
                                    List.singleton {
                                        Binding         = slot
                                        Location        = location
                                        Path            = path
                                        Type            = field.Type
                                    }
                                | FixedArray((Sampler _ | Image _) as t, _, length) ->
                                    let slots = 
                                        Array.init length (fun i ->
                                            let slot = !textureSlot
                                            textureSlot := slot + 1
                                            slot
                                        )
                                    GL.Uniform1(location, slots.Length, slots)

                                    slots |> Array.toList |> List.mapi (fun i s ->
                                        {
                                            Binding         = s
                                            Location        = location
                                            Path            = ShaderPath.Item(path, i)
                                            Type            = t
                                        }
                                    )
                                | _ -> 

                                    List.singleton {
                                        Binding         = -1
                                        Location        = location
                                        Path            = path
                                        Type            = field.Type
                                    }
                        else
                            []
                    | _ ->
                        []
            )

            

        let getReferencingStagesUB (ui : int) (p : int) =
            let vs = GL.GetActiveUniformBlock(p, ui, ActiveUniformBlockParameter.UniformBlockReferencedByVertexShader) = 1
            let tc = GL.GetActiveUniformBlock(p, ui, ActiveUniformBlockParameter.UniformBlockReferencedByTessControlShader) = 1
            let te = GL.GetActiveUniformBlock(p, ui, ActiveUniformBlockParameter.UniformBlockReferencedByTessEvaluationShader) = 1
            let gs = GL.GetActiveUniformBlock(p, ui, ActiveUniformBlockParameter.UniformBlockReferencedByGeometryShader) = 1
            let fs = GL.GetActiveUniformBlock(p, ui, ActiveUniformBlockParameter.UniformBlockReferencedByFragmentShader) = 1

            Set.ofList [
                if vs then yield ShaderStage.Vertex
                if tc then yield ShaderStage.TessControl
                if te then yield ShaderStage.TessEval
                if gs then yield ShaderStage.Geometry
                if fs then yield ShaderStage.Fragment
            ]

        let blocksUB (p : int) =
            let cnt = GL.GetProgram(p, GetProgramParameterName.ActiveUniformBlocks)

            List.init cnt (fun ui ->
                let name = p |> getNameUB ui
                let referenced = p |> getReferencingStagesUB ui
                let size = GL.GetActiveUniformBlock(p, ui, ActiveUniformBlockParameter.UniformBlockDataSize)

                let cFields = GL.GetActiveUniformBlock(p, ui, ActiveUniformBlockParameter.UniformBlockActiveUniforms)
                let iFields = Array.create cFields -1
                GL.GetActiveUniformBlock(p, ui, ActiveUniformBlockParameter.UniformBlockActiveUniformIndices, iFields)

                let fields =
                    iFields |> Array.toList |> List.map (fun fi ->
                        p |> getUniformField referenced fi
                    )

                GL.UniformBlockBinding(p, ui, ui)

                {
                    Index           = ui
                    Name            = name
                    Fields          = fields |> List.sortBy (fun f -> f.Offset) |> collapse
                    Referenced      = referenced
                    DataSize        = size
                }
            )

        let shaderInterface (baseSlot : int) (p : int) : ShaderInterface =
            let slot = ref baseSlot
            let outputs =
                try p |> GL4.parameters slot ProgramInterface.ProgramOutput
                with _ -> []

            let storage =
                try p |> GL4.blocks ProgramInterface.ShaderStorageBlock
                with _ -> []

            {
                Inputs          = p |> getInputs
                Outputs         = outputs
                Uniforms        = p |> getUniforms slot
                UniformBlocks   = p |> blocksUB
                StorageBlocks   = storage
                UsedBuiltInOutputs  = Map.empty
                UsedBuiltInInputs   = Map.empty
            }

    let ofProgram (baseSlot : int) (ctx : Context) (p : int) =
        use __ = ctx.ResourceLock
        let info = ctx.Driver

        if info.version >= Version(4,0,0) && info.device <> GPUVendor.Intel then
            GL4.shaderInterface baseSlot p
        else
            GL.shaderInterface baseSlot p
