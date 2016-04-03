namespace Aardvark.Rendering.Vulkan

open SpirV
open FShade.SpirV

type Width = int
type ShaderType = FShade.SpirV.Type
  

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ShaderType =

    let glslDecoration (decorations : list<Decoration * uint32[]>) =
        if List.isEmpty decorations then
            ""
        else
            let decorations =
                decorations |> List.map (fun t ->
                    match t with
                        | Decoration.Location, [|loc|] -> sprintf "location = %d" (int loc)
                        | Decoration.Binding, [|b|] -> sprintf "binding = %d" (int b)
                        | Decoration.DescriptorSet, [|s|] -> sprintf "set = %d" (int s)
                        | Decoration.Index, [|i|] -> sprintf "index = %d" (int i)
                        | Decoration.BuiltIn, [|id|] -> sprintf "builtin = %A" (unbox<BuiltIn> id)

                        | Decoration.NonReadable, _ -> "write_only"
                        | Decoration.NonWritable, _ -> "read_only"
                        | Decoration.RowMajor, _ -> "row_major"
                        | Decoration.ColMajor, _ -> "col_major"
                        | Decoration.NoPerspective, _ -> "noperspective"
                        | Decoration.Flat, _ -> "flat"

                        | d, [||] -> sprintf "%A" d
                        | d, args -> sprintf "%A = { %s }" d (args |> Array.map string |> String.concat ", ")
                )
            decorations |> String.concat ", " |> sprintf "layout(%s)"

    let rec glslName (t : ShaderType) =
        match t with
            | Ptr(_,t) -> glslName t
            | Array(t, l) -> sprintf "%s[%d]" (glslName t) l

            | Bool -> "bool"
            | Int(32, true) -> "int"
            | Int(32, false) -> "uint"
            | Float 32 -> "float"
            | Float 64 -> "double"

            | Vector(Bool, d) -> sprintf "bvec%d" d
            | Vector(Int(32, true), d) -> sprintf "ivec%d" d
            | Vector(Int(32, false), d) -> sprintf "uvec%d" d
            | Vector(Float 32, d) -> sprintf "vec%d" d
            | Vector(Float 64, d) -> sprintf "dvec%d" d

            | Matrix(Vector(Float 32, r), c) -> 
                if r = c then sprintf "mat%d" r
                else sprintf "mat%dx%d" r c

            | Matrix(Vector(Float 64, r), c) -> 
                if r = c then sprintf "dmat%d" r
                else sprintf "dmat%dx%d" r c

            | Image(st,dim,d,a,m, x, y) -> 
                let st =
                    match st with
                        | Float 32 -> ""
                        | Int(32, true) -> "i"
                        | Int(32, false) -> "u"
                        | _ -> "g"

                let dim =
                    match dim with
                        | Dim.Dim1D -> "1D"
                        | Dim.Dim2D -> "2D"
                        | Dim.Dim3D -> "3D"
                        | Dim.Buffer -> "Buffer"
                        | Dim.Cube -> "Cube"
                        | Dim.Rect -> "Rect"
                        | _ -> failwithf "unsupported sampler dimension: %A" dim
                let d = if d > 0 then "Shadow" else ""
                let a = if a then "Array" else ""
                let m = if m > 1 then "MS" else ""
                st + "sampler" + dim + m + a + d



            | Struct(name, fields) ->
                
                let strs =
                    fields |> List.map (fun (t, n, dec) ->
                        let t = glslName t
                        let layout = glslDecoration dec

                        if layout.Length = 0 then
                            sprintf "    %s %s;" t n
                        else
                            sprintf "    %s %s %s;" layout t n
                    )

                strs |> String.concat "\r\n" |> sprintf "%s {\r\n%s\r\n }" name

            | _ -> sprintf "unknown type: %A" t

    let Bool = ShaderType.Bool
    let UnsignedInt = ShaderType.Int(32, false)
    let Double = ShaderType.Float(64)
    let Half = ShaderType.Float(16)
    let Int = ShaderType.Int(32, true)
    let Float =ShaderType. Float(32)

    let IntVec2 = ShaderType.Vector(Int, 2)
    let IntVec3 = ShaderType.Vector(Int, 3)
    let IntVec4 = ShaderType.Vector(Int, 4)

    let UnsignedIntVec2 = ShaderType.Vector(UnsignedInt, 2)
    let UnsignedIntVec3 = ShaderType.Vector(UnsignedInt, 3)
    let UnsignedIntVec4 = ShaderType.Vector(UnsignedInt, 4)

    let BoolVec2 = ShaderType.Vector(Bool, 2)
    let BoolVec3 = ShaderType.Vector(Bool, 3)
    let BoolVec4 = ShaderType.Vector(Bool, 4)

    let FloatVec2 = ShaderType.Vector(Float, 2)
    let FloatVec3 = ShaderType.Vector(Float, 3)
    let FloatVec4 = ShaderType.Vector(Float, 4)
    let FloatMat2 = ShaderType.Matrix(FloatVec2, 2)
    let FloatMat3 = ShaderType.Matrix(FloatVec3, 3)
    let FloatMat4 = ShaderType.Matrix(FloatVec4, 4)
    let FloatMat2x2 = ShaderType.Matrix(FloatVec2, 2)
    let FloatMat3x2 = ShaderType.Matrix(FloatVec3, 2)
    let FloatMat4x2 = ShaderType.Matrix(FloatVec4, 2)
    let FloatMat2x3 = ShaderType.Matrix(FloatVec2, 3)
    let FloatMat3x3 = ShaderType.Matrix(FloatVec3, 3)
    let FloatMat4x3 = ShaderType.Matrix(FloatVec4, 3)
    let FloatMat2x4 = ShaderType.Matrix(FloatVec2, 4)
    let FloatMat3x4 = ShaderType.Matrix(FloatVec3, 4)
    let FloatMat4x4 = ShaderType.Matrix(FloatVec4, 4)


    let DoubleVec2 = ShaderType.Vector(Double, 2)
    let DoubleVec3 = ShaderType.Vector(Double, 3)
    let DoubleVec4 = ShaderType.Vector(Double, 4)
    let DoubleMat2 = ShaderType.Matrix(DoubleVec2, 2)
    let DoubleMat3 = ShaderType.Matrix(DoubleVec3, 3)
    let DoubleMat4 = ShaderType.Matrix(DoubleVec4, 4)
    let DoubleMat2x2 = ShaderType.Matrix(DoubleVec2, 2)
    let DoubleMat3x2 = ShaderType.Matrix(DoubleVec3, 2)
    let DoubleMat4x2 = ShaderType.Matrix(DoubleVec4, 2)
    let DoubleMat2x3 = ShaderType.Matrix(DoubleVec2, 3)
    let DoubleMat3x3 = ShaderType.Matrix(DoubleVec3, 3)
    let DoubleMat4x3 = ShaderType.Matrix(DoubleVec4, 3)
    let DoubleMat2x4 = ShaderType.Matrix(DoubleVec2, 4)
    let DoubleMat3x4 = ShaderType.Matrix(DoubleVec3, 4)
    let DoubleMat4x4 = ShaderType.Matrix(DoubleVec4, 4)

type Parameter = 
    { 
        paramName : string
        paramType : ShaderType
        paramDecorations : list<Decoration * uint32[]> 
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Parameter =
    let tryGetLocation (p : Parameter) =
        p.paramDecorations |> List.tryPick (fun (d,args) ->
            match d with
                | Decoration.Location -> Some (args.[0] |> int)
                | _ -> None
        ) 

    let tryGetBinding (p : Parameter) =
        p.paramDecorations |> List.tryPick (fun (d,args) ->
            match d with
                | Decoration.Binding -> Some (args.[0] |> int)
                | _ -> None
        ) 

    let tryGetDescriptorSet (p : Parameter) =
        p.paramDecorations |> List.tryPick (fun (d,args) ->
            match d with
                | Decoration.DescriptorSet -> Some (args.[0] |> int)
                | _ -> None
        ) 

    let tryGetBuiltInSemantic (p : Parameter) =
        p.paramDecorations |> List.tryPick (fun (d,args) ->
            match d with
                | Decoration.BuiltIn -> Some (args.[0] |> int |> unbox<BuiltIn>)
                | _ -> None
        )


    let getArraySize (p : Parameter) =
        1

    let inline paramName (p : Parameter) = p.paramName
    let inline paramType (p : Parameter) = p.paramType
    let inline paramDecorations (p : Parameter) = p.paramDecorations


type ShaderInterface = 
    { 
        executionModel : ExecutionModel
        entryPoint : string
        inputs : list<Parameter>
        outputs : list<Parameter>
        uniforms : list<Parameter>
        images : list<Parameter> 
    }