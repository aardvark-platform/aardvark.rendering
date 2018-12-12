#r "netstandard.dll"
#r "System.Xml.dll"
#r "System.Xml.Linq.dll"
#r @"..\..\..\packages\build\FSharp.Data\lib\netstandard2.0\FSharp.Data.dll"

open System
open System.IO
open System.Text
open System.Text.RegularExpressions
open System.Globalization
open FSharp.Data

type GlSpec = XmlProvider<"gl.xml">

type Enum =
    {
        name    : string
        flags   : bool
        values  : Map<string, int>
    }
    
[<RequireQualifiedAccess>]
type Type =
    | Void
    | Enum of string
    | Float of w : int
    | Int of s : bool * w : int
    | NativeInt of s : bool
    | Bool
    | Pointer of Type
    | Char

let errors = System.Collections.Generic.HashSet<string>()
let errorfn fmt = Printf.kprintf (fun str -> errors.Add(str) |> ignore) fmt

module Type =

    let rec pointer (dim : int) (t : Type) =
        if dim <= 0 then t
        elif dim = 1 && t = Type.Void then Type.NativeInt true
        else pointer (dim - 1) (Type.Pointer t)

    let rec ofString (enums : Map<string, Enum>) (name : string) =
        let name = name.ToLower().Trim()
        match name with

        | "struct _cl_context" -> Type.Void
        | "struct _cl_event" -> Type.Void

        | "void" | "glvoid" -> Type.Void

        | "glhalfnv" -> Type.Float 16

        | "glfloat" 
        | "glclampf" -> Type.Float 32

        | "gldouble"
        | "glclampd" -> Type.Float 64

        | "glsizeiptr" -> Type.NativeInt false

        | "glboolean" | "glbool" -> Type.Bool
        | "glchar" | "glchararb" -> Type.Char

        | "glintptr" | "glvdpauSurfacenvv" -> Type.NativeInt true

        | "glubyte" -> Type.Int(false, 8)
        | "glushort" -> Type.Int(false, 16)
        | "gluint" | "glsizei" -> Type.Int(false, 32)
        | "gluint64" | "gluint64ext" -> Type.Int(false, 64)
        
        | "glbyte" -> Type.Int(true, 8)
        | "glshort" -> Type.Int(true, 16)
        | "glfixed" | "glbitfield" | "glint" -> Type.Int(true, 32)
        | "glint64" | "glint64ext" -> Type.Int(true, 64)

        | "glclampx" -> Type.Int(true, 32)


        | "glhandlearb" ->
            Type.Int(true, 32)

        | "glsync" 
        | "glintptrarb"
        | "gleglImageoes"
        | "gldebugproc"
        | "gldebugprocamd"
        | "gleglimageoes"
        | "glvdpausurfacenv"
        | "glvulkanprocnv"
        | "gleglclientbufferext" -> 
            Type.NativeInt true

        | "glenum" -> Type.Int(true, 32)

        | n -> 
            if Map.containsKey n enums then
                Type.Enum n
            else
                if n.Contains "*" then  
                    let dim = n.ToCharArray() |> Seq.sumBy (function '*' -> 1 | _ -> 0)
                    let en = n.Replace("*", "").Replace("const", "").Trim()
                    pointer dim (ofString enums en)
                else
                    errorfn "unknown type %s" n
                    Type.Void

type Command =
    {
        group       : Option<string>
        name        : string
        parameters  : list<string * Type>
        returnType  : Type
    }

type Kind =
    | GL 
    | GLCore 
    | GLES of version : int

module Kind =
    let private glRx = Regex @"^gl(?<version>[0-9]+)?$"
    let private glcoreRx = Regex @"^glcore(?<version>[0-9]+)?$"
    let private glesRx = Regex @"^gles(?<version>[0-9]+)?$"
    
    let private (|GL|_|) (s : string) =
        let m = glRx.Match s 
        if m.Success then 
            let v = m.Groups.["version"]
            if v.Success then Some (Some (Int32.Parse v.Value))
            else Some None
        else 
            None
    let private (|GLCore|_|) (s : string) =
        let m = glcoreRx.Match s 
        if m.Success then 
            let v = m.Groups.["version"]
            if v.Success then Some (Some (Int32.Parse v.Value))
            else Some None
        else 
            None
            
    let private (|GLES|_|) (s : string) =
        let m = glesRx.Match s 
        if m.Success then 
            let v = m.Groups.["version"]
            if v.Success then Some (Int32.Parse v.Value)
            else Some 1
        else 
            None

    let parse (kind : string) =
        kind.Split([| '|' |], StringSplitOptions.RemoveEmptyEntries)
        |> Seq.choose (fun k ->
            match k with
            | GL _ -> Some Kind.GL
            | GLCore _ -> Some Kind.GLCore
            | GLES v -> Some (Kind.GLES v)
            | _ -> None
        )
        |> Set.ofSeq

type FeatureSet =
    {
        name        : string
        version     : Version
        supported   : Set<Kind>
        commands    : Map<string, Command>
        enums       : Map<string, Enum>
        enumNames   : Map<string, string>
    }

module FeatureSet =
    let tryRemoveEnumValue (name : string) (s : FeatureSet) =
        match Map.tryFind name s.enumNames with
        | Some enumName ->
            match Map.tryFind enumName s.enums with
            | Some e ->
                let mutable result = None
                let e = { e with values = e.values |> Map.filter (fun ni vi -> if ni = name then result <- Some vi; false else true) }
                match result with
                | Some r ->
                    let s =
                        if Map.isEmpty e.values then { s with enums = Map.remove enumName s.enums }
                        else { s with enums = Map.add enumName e s.enums; }

                    let s = { s with enumNames = Map.remove name s.enumNames }

                    Some (s, (enumName, r))
                | _ ->
                    None
            | _ ->
                None
        | _ ->
            None

    let tryRemoveCommand (name : string) (s : FeatureSet) =
        match Map.tryFind name s.commands with
        | Some cmd ->
            let s = { s with commands = Map.remove name s.commands }
            Some (s, cmd)
        | None ->
            None

    let removeCommand (name : string) (s : FeatureSet) =
        match tryRemoveCommand name s with
        | Some (s,_) -> s
        | _ -> s
        
    let removeEnum (name : string) (s : FeatureSet) =
        match Map.tryFind name s.enums with
        | Some e ->
            let enumNames = e.values |> Map.fold (fun s n _ -> Map.remove n s) s.enumNames
            { s with enumNames = enumNames; enums = Map.remove name s.enums }
        | None ->
            s
            
    let addCommand (cmd : Command) (s : FeatureSet) =
        { s with commands = Map.add cmd.name cmd s.commands }
        
    let addEnum (e : Enum) (s : FeatureSet) =
        let enumNames = e.values |> Map.fold (fun ns n _ -> Map.add n e.name ns) s.enumNames
        { s with 
            enums = Map.add e.name e s.enums 
            enumNames = enumNames
        }
    let addEnumValue (enumName : string) (name : string) (value : int) (s : FeatureSet) =
        let e =
            match Map.tryFind enumName s.enums with
            | Some e -> { e with values = Map.add name value e.values }
            | None -> { values = Map.ofList [name, value]; name = enumName; flags = false }
            
        { s with 
            enums = Map.add enumName e s.enums
            enumNames = Map.add name enumName s.enumNames
        }

    let removeEnumValue (name : string) (s : FeatureSet) =
        match tryRemoveEnumValue name s with
        | Some (s,_) -> s
        | None -> s

    let empty =
        {
            name        = "Empty"
            version     = Version(1,0)
            supported   = Set.empty
            commands    = Map.empty
            enums       = Map.empty
            enumNames   = Map.empty
        }

    let create (name : string) (supported : Set<Kind>) (commands : Map<string, Command>) (enums : Map<string, Enum>) =
        let enumNames =
            Map.toSeq enums
            |> Seq.collect (fun (en, e) -> 
                e.values |> Map.toSeq |> Seq.map (fun (n,_) -> n, en)
            )
            |> Map.ofSeq

        {
            name = name
            version = Version(1,0)
            supported = supported
            commands = commands
            enums = enums
            enumNames = enumNames
        }

let ptrRx = Regex @"^(const)?[ \t]*(?<tname>[a-zA-Z_][a-zA-Z_0-9]*)[ \t]*(?<ptr>([ \*]*|const)*)[ \t]*(?<name>[a-zA-Z_][a-zA-Z_0-9]*)[ \t]*$"
let returnTypeRx = Regex @"^(const)?[ \t]*(?<tname>[a-zA-Z_][a-zA-Z_0-9 \*]*?)[ \t]*(?<name>[a-zA-Z_][a-zA-Z_0-9]*)$"

let hardCodedGroups =
    [|
        "GL_RGBA2", "InternalFormat"
        "GL_UNSIGNED_BYTE_2_3_3_REV", "InternalFormat"
        "GL_UNSIGNED_SHORT_5_6_5", "InternalFormat"
        "GL_UNSIGNED_SHORT_5_6_5_REV", "InternalFormat"
        "GL_UNSIGNED_SHORT_4_4_4_4_REV", "InternalFormat"
        "GL_UNSIGNED_SHORT_1_5_5_5_REV", "InternalFormat"
        "GL_UNSIGNED_INT_8_8_8_8_REV", "InternalFormat"
        "GL_COMPRESSED_ALPHA", "InternalFormat"
        "GL_COMPRESSED_LUMINANCE", "InternalFormat"
        "GL_COMPRESSED_LUMINANCE_ALPHA", "InternalFormat"
        "GL_COMPRESSED_INTENSITY", "InternalFormat"
        "GL_DEPTH_COMPONENT24", "InternalFormat"
        "GL_DEPTH_COMPONENT32", "InternalFormat"
        "GL_SLUMINANCE_ALPHA", "InternalFormat"
        "GL_SLUMINANCE8_ALPHA8", "InternalFormat"
        "GL_SLUMINANCE", "InternalFormat"
        "GL_SLUMINANCE8", "InternalFormat"
        "GL_COMPRESSED_SLUMINANCE", "InternalFormat"
        "GL_COMPRESSED_SLUMINANCE_ALPHA", "InternalFormat"
        "GL_RGB32F", "InternalFormat"
        "GL_UNSIGNED_INT_5_9_9_9_REV", "InternalFormat"
        "GL_FLOAT_32_UNSIGNED_INT_24_8_REV", "InternalFormat"
        "GL_UNSIGNED_INT_24_8", "InternalFormat"


        "GL_FRAMEBUFFER_DEFAULT", "FramebufferAttachmentParameterName"
        "GL_UNSIGNED_NORMALIZED", "FramebufferAttachmentParameterName"


        "GL_MIRRORED_REPEAT", "TextureWrapMode"


        "GL_TEXTURE_DEPTH", "GetTextureParameter"
        "GL_TEXTURE_COMPRESSED_IMAGE_SIZE", "GetTextureParameter"
        "GL_TEXTURE_DEPTH_SIZE", "GetTextureParameter"
        "GL_TEXTURE_SHARED_SIZE", "GetTextureParameter"
        "GL_TEXTURE_STENCIL_SIZE", "GetTextureParameter"
        "GL_TEXTURE_RED_TYPE", "GetTextureParameter"
        "GL_TEXTURE_GREEN_TYPE", "GetTextureParameter"
        "GL_TEXTURE_BLUE_TYPE", "GetTextureParameter"
        "GL_TEXTURE_ALPHA_TYPE", "GetTextureParameter"
        "GL_TEXTURE_DEPTH_TYPE", "GetTextureParameter"

        "GL_RESCALE_NORMAL", "EnableCap"
        "GL_MULTISAMPLE", "EnableCap"
        "GL_SAMPLE_ALPHA_TO_COVERAGE", "EnableCap"
        "GL_SAMPLE_ALPHA_TO_ONE", "EnableCap"
        "GL_SAMPLE_COVERAGE", "EnableCap"
        "GL_COLOR_SUM", "EnableCap"
        "GL_VERTEX_PROGRAM_POINT_SIZE", "EnableCap"
        "GL_VERTEX_PROGRAM_TWO_SIDE", "EnableCap"
        "GL_POINT_SPRITE", "EnableCap"
        "GL_RASTERIZER_DISCARD", "EnableCap"


        "GL_CLIENT_ACTIVE_TEXTURE", "GetPName"
        "GL_MAX_TEXTURE_UNITS", "GetPName"
        "GL_TRANSPOSE_MODELVIEW_MATRIX", "GetPName"
        "GL_TRANSPOSE_PROJECTION_MATRIX", "GetPName"
        "GL_TRANSPOSE_TEXTURE_MATRIX", "GetPName"
        "GL_TRANSPOSE_COLOR_MATRIX", "GetPName"
        "GL_CURRENT_SECONDARY_COLOR", "GetPName"
        "GL_SECONDARY_COLOR_ARRAY_SIZE", "GetPName"
        "GL_SECONDARY_COLOR_ARRAY_TYPE", "GetPName"
        "GL_SECONDARY_COLOR_ARRAY_STRIDE", "GetPName"
        "GL_SECONDARY_COLOR_ARRAY_POINTER", "GetPName"
        "GL_SECONDARY_COLOR_ARRAY", "GetPName"
        "GL_FOG_COORDINATE_ARRAY_TYPE", "GetPName"
        "GL_FOG_COORDINATE_ARRAY_STRIDE", "GetPName"
        "GL_FOG_COORDINATE_ARRAY_POINTER", "GetPName"
        "GL_FOG_COORDINATE_ARRAY", "GetPName"
        "GL_FOG_COORD_ARRAY_TYPE", "GetPName"
        "GL_FOG_COORD_ARRAY_STRIDE", "GetPName"
        "GL_FOG_COORD_ARRAY_POINTER", "GetPName"
        "GL_FOG_COORD_ARRAY", "GetPName"
        "GL_BLEND_EQUATION", "GetPName"
        "GL_BUFFER_MAP_POINTER", "GetPName"
        "GL_FOG_COORD_ARRAY_BUFFER_BINDING", "GetPName"
        "GL_VERTEX_ARRAY_BUFFER_BINDING", "GetPName"
        "GL_NORMAL_ARRAY_BUFFER_BINDING", "GetPName"
        "GL_COLOR_ARRAY_BUFFER_BINDING", "GetPName"
        "GL_INDEX_ARRAY_BUFFER_BINDING", "GetPName"
        "GL_TEXTURE_COORD_ARRAY_BUFFER_BINDING", "GetPName"
        "GL_EDGE_FLAG_ARRAY_BUFFER_BINDING", "GetPName"
        "GL_SECONDARY_COLOR_ARRAY_BUFFER_BINDING", "GetPName"
        "GL_FOG_COORDINATE_ARRAY_BUFFER_BINDING", "GetPName"
        "GL_WEIGHT_ARRAY_BUFFER_BINDING", "GetPName"
        "GL_CURRENT_FOG_COORD", "GetPName"
        "GL_DRAW_BUFFER0", "GetPName"
        "GL_DRAW_BUFFER1", "GetPName"
        "GL_DRAW_BUFFER2", "GetPName"
        "GL_DRAW_BUFFER3", "GetPName"
        "GL_DRAW_BUFFER4", "GetPName"
        "GL_DRAW_BUFFER5", "GetPName"
        "GL_DRAW_BUFFER6", "GetPName"
        "GL_DRAW_BUFFER7", "GetPName"
        "GL_DRAW_BUFFER8", "GetPName"
        "GL_DRAW_BUFFER9", "GetPName"
        "GL_DRAW_BUFFER10", "GetPName"
        "GL_DRAW_BUFFER11", "GetPName"
        "GL_DRAW_BUFFER12", "GetPName"
        "GL_DRAW_BUFFER13", "GetPName"
        "GL_DRAW_BUFFER14", "GetPName"
        "GL_DRAW_BUFFER15", "GetPName"
        "GL_MAX_TEXTURE_COORDS", "GetPName"
        "GL_CURRENT_RASTER_SECONDARY_COLOR", "GetPName"
        "GL_MAX_TRANSFORM_FEEDBACK_SEPARATE_COMPONENTS", "GetPName"
        "GL_MAX_TRANSFORM_FEEDBACK_INTERLEAVED_COMPONENTS", "GetPName"
        "GL_MAX_TRANSFORM_FEEDBACK_SEPARATE_ATTRIBS", "GetPName"
        "GL_FRAMEBUFFER_BINDING", "GetPName"
        "GL_FRAMEBUFFER_ATTACHMENT_OBJECT_TYPE", "GetPName"
        "GL_MAX_SAMPLES", "GetPName"

        "GL_INTERLEAVED_ATTRIBS", "TransformFeedbackBufferMode"
        "GL_SEPARATE_ATTRIBS", "TransformFeedbackBufferMode"

        "GL_NORMAL_MAP", "TextureGenParameter"
        "GL_REFLECTION_MAP", "TextureGenParameter"

        "GL_DEPTH_TEXTURE_MODE", "TextureParameterName"

        "GL_COMPARE_R_TO_TEXTURE", "TextureCompareMode"
        "GL_COMPARE_REF_TO_TEXTURE", "TextureCompareMode"
        "GL_NONE", "TextureCompareMode"

        "GL_STENCIL_INDEX1", "Unknown"
        "GL_STENCIL_INDEX2", "Unknown"
        "GL_STENCIL_INDEX4", "Unknown"
        "GL_STENCIL_INDEX8", "Unknown"
        "GL_STENCIL_INDEX16", "Unknown"

        "GL_RGB_SCALE", "TextureEnvTarget"
        "GL_COMBINE", "TextureEnvParameter"
        "GL_COMBINE_RGB", "TextureEnvParameter"
        "GL_COMBINE_ALPHA", "TextureEnvParameter"
        "GL_SOURCE0_RGB", "TextureEnvParameter"
        "GL_SOURCE1_RGB", "TextureEnvParameter"
        "GL_SOURCE2_RGB", "TextureEnvParameter"
        "GL_SOURCE0_ALPHA", "TextureEnvParameter"
        "GL_SOURCE1_ALPHA", "TextureEnvParameter"
        "GL_SOURCE2_ALPHA", "TextureEnvParameter"
        "GL_OPERAND0_RGB", "TextureEnvParameter"
        "GL_OPERAND1_RGB", "TextureEnvParameter"
        "GL_OPERAND2_RGB", "TextureEnvParameter"
        "GL_OPERAND0_ALPHA", "TextureEnvParameter"
        "GL_OPERAND1_ALPHA", "TextureEnvParameter"
        "GL_OPERAND2_ALPHA", "TextureEnvParameter"
        "GL_ADD_SIGNED", "TextureEnvParameter"
        "GL_INTERPOLATE", "TextureEnvParameter"
        "GL_SUBTRACT", "TextureEnvParameter"
        "GL_PREVIOUS", "TextureEnvParameter"
        "GL_DOT3_RGB", "TextureEnvParameter"
        "GL_DOT3_RGBA", "TextureEnvParameter"
        "GL_SRC0_RGB", "TextureEnvParameter"
        "GL_SRC1_RGB", "TextureEnvParameter"
        "GL_SRC2_RGB", "TextureEnvParameter"
        "GL_SRC0_ALPHA", "TextureEnvParameter"
        "GL_SRC2_ALPHA", "TextureEnvParameter"
        "GL_COORD_REPLACE", "TextureEnvParameter"


        "GL_TEXTURE_FILTER_CONTROL", "TextureEnvTarget"

        "GL_FOG_COORDINATE_SOURCE", "FogParameter"
        "GL_FOG_COORDINATE", "FogParameter"
        "GL_FRAGMENT_DEPTH", "FogParameter"
        "GL_CURRENT_FOG_COORDINATE", "FogParameter"
        "GL_FOG_COORD", "FogParameter"

        "GL_BUFFER_MAP_POINTER", "BufferPointerNameARB"

        "GL_VERTEX_ATTRIB_ARRAY_POINTER", "VertexAttribPointerPropertyARB"
        "GL_POINT_SPRITE_COORD_ORIGIN", "PointParameterNameARB"
        "GL_CLAMP_READ_COLOR", "ClampColorTargetARB"
        "GL_FIXED_ONLY", "ClampColorModeARB"

        "GL_SAMPLER_1D_ARRAY", "ActiveUniformType"
        "GL_SAMPLER_2D_ARRAY", "ActiveUniformType"
        "GL_SAMPLER_1D_ARRAY_SHADOW", "ActiveUniformType"
        "GL_SAMPLER_2D_ARRAY_SHADOW", "ActiveUniformType"
        "GL_SAMPLER_CUBE_SHADOW", "ActiveUniformType"
        "GL_UNSIGNED_INT_VEC2", "ActiveUniformType"
        "GL_UNSIGNED_INT_VEC3", "ActiveUniformType"
        "GL_UNSIGNED_INT_VEC4", "ActiveUniformType"
        "GL_INT_SAMPLER_1D", "ActiveUniformType"
        "GL_INT_SAMPLER_2D", "ActiveUniformType"
        "GL_INT_SAMPLER_3D", "ActiveUniformType"
        "GL_INT_SAMPLER_CUBE", "ActiveUniformType"
        "GL_INT_SAMPLER_1D_ARRAY", "ActiveUniformType"
        "GL_INT_SAMPLER_2D_ARRAY", "ActiveUniformType"
        "GL_UNSIGNED_INT_SAMPLER_1D", "ActiveUniformType"
        "GL_UNSIGNED_INT_SAMPLER_2D", "ActiveUniformType"
        "GL_UNSIGNED_INT_SAMPLER_3D", "ActiveUniformType"
        "GL_UNSIGNED_INT_SAMPLER_CUBE", "ActiveUniformType"
        "GL_UNSIGNED_INT_SAMPLER_1D_ARRAY", "ActiveUniformType"
        "GL_UNSIGNED_INT_SAMPLER_2D_ARRAY", "ActiveUniformType"

        "GL_STENCIL_ATTACHMENT", "FramebufferAttachment"

    |]


let test() = 
    let str = StringBuilder()

    let printfn fmt = Printf.kprintf (fun s -> str.AppendLine s |> ignore) fmt
    let printf fmt = Printf.kprintf (fun s -> str.Append s |> ignore) fmt

    let glspec = GlSpec.Load(Path.Combine(__SOURCE_DIRECTORY__, "gl.xml"))
    
    let values =
        glspec.Enums |> Seq.collect (fun e ->
            e.Enums |> Seq.collect (fun value ->
                let str = value.Value.Value.ToLower()
                let names = value.Name :: Option.toList value.Alias
                
                if str.StartsWith "0x" then
                    let str = str.Substring 2
                    match Int64.TryParse(str, NumberStyles.HexNumber, CultureInfo.InvariantCulture) with
                        | (true, v) -> 
                            names |> Seq.map (fun n -> n, int v)
                        | _ -> 
                            errorfn "cannot parse value for %s: %A" value.Name str
                            Seq.empty
                else
                    match Int64.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture) with
                        | (true, v) -> 
                            names |> Seq.map (fun n -> n, int v)
                        | _ -> 
                            errorfn "cannot parse value for %s: %A" value.Name str
                            Seq.empty
            )
        )
        |> Map.ofSeq

    let bitmasks =
        glspec.Enums |> Seq.choose (fun e ->
            match e.Group, e.Type with
            | Some g, Some "bitmask" ->
                Some g
            | Some g, Some b ->
                errorfn "%s: unknown enum-type %A" g b
                None
            | _ ->
                None
        )
        |> Set.ofSeq

    let enums =
        let all = 
            Array.concat [
                hardCodedGroups |> Array.map (fun (a,b) -> b,a)
                glspec.Groups |> Array.collect (fun g -> g.Enums |> Array.map (fun e -> g.Name, e.Name))
            ]
        let groups =
            Array.groupBy fst all
            |> Array.map (fun (g, es) -> g, Array.map snd es)


        groups |> Array.map (fun (g,es) ->
            let values = 
                es |> Array.choose (fun e ->
                    match Map.tryFind e values with 
                    | Some v -> 
                        Some (e, v)
                    | None -> 
                        errorfn "no value for %s" e
                        None
                )
            g,
            {
                name    = g
                flags   = Set.contains g bitmasks
                values  = Map.ofArray values
            }
        )
        |> Map.ofSeq

    let commands = 
        glspec.Commands.Commands |> Seq.choose (fun cmd ->
            match cmd.Alias with
            | Some a -> 
                None
            | None ->
                let proto = cmd.Proto
                
                let parameters = 
                    cmd.Params |> Array.toList |> List.choose (fun p -> 
                        let m = ptrRx.Match p.XElement.Value
                        if m.Success then
                            let tname = m.Groups.["tname"].Value
                            let dim = m.Groups.["ptr"].Value.ToCharArray() |> Array.sumBy (function '*' -> 1 | _ -> 0)
                            let t = Type.pointer dim (Type.ofString enums tname)
                            Some (p.Name, t)
                        else
                            match p.Ptype with
                            | Some t ->
                                if t.ToLower().Trim() = "glenum" then
                                    match p.Group with
                                    | Some g -> 
                                        if Map.containsKey g enums then 
                                            Some (p.Name, Type.Enum g)
                                        else 
                                            //errorfn "could not find enum %s" g
                                            Some (p.Name, Type.Int(true, 32))
                                    | None ->
                                        //errorfn "no enum name for parameter %s in %s" p.Name proto.Name
                                        Some (p.Name, Type.Int(true, 32))
                                else
                                    Some (p.Name, Type.ofString enums t)
                            | None ->
                                errorfn "no type for %s in %s (%A)" p.Name proto.Name p.XElement.Value
                                None
                    )
                    
                let m = returnTypeRx.Match cmd.Proto.XElement.Value
                let returnType = 
                    if m.Success then 
                        Type.ofString enums m.Groups.["tname"].Value
                    else 
                        errorfn "could not parse return type for %A" cmd.Proto.XElement.Value
                        Type.Void

                let cmd =
                    {
                        name = proto.Name
                        group = proto.Group
                        parameters = parameters
                        returnType = returnType
                    }

                Some (proto.Name, cmd)
        )
        |> Map.ofSeq
        
    let baseSet =
        FeatureSet.create "GL" (Set.ofList [Kind.GL; Kind.GLCore; Kind.GLES 1; Kind.GLES 2]) commands enums


    let _, featureSets =
        glspec.Features |> Array.fold (fun (set, all) f ->
            if f.Api.ToLower().Trim() = "gl" then
                // remove everything specified
                let set = 
                    f.Removes |> Seq.fold (fun set r ->
                        let set = r.Commands |> Seq.fold (fun set r -> FeatureSet.removeCommand r.Name set) set
                        let set = r.Enums |> Seq.fold (fun set r -> FeatureSet.removeEnum r.Name set) set
                        set
                    ) set


                let set =
                    f.Requires |> Seq.fold (fun set r ->
                        let set = 
                            r.Commands |> Seq.fold (fun set r -> 
                                match FeatureSet.tryRemoveCommand r.Name baseSet with
                                | Some(_,cmd) -> 
                                    FeatureSet.addCommand cmd set
                                | None -> 
                                    errorfn "%f could not get command %s" f.Number r.Name
                                    set
                            ) set
                        
                        let set = 
                            r.Enums |> Seq.fold (fun set r -> 
                                match Map.tryFind r.Name baseSet.enums with
                                | Some e -> 
                                    FeatureSet.addEnum e set
                                | None -> 
                                    match FeatureSet.tryRemoveEnumValue r.Name baseSet with
                                    | Some (_,(en,v)) ->
                                        FeatureSet.addEnumValue en r.Name v set
                                    | None ->
                                        let hard = baseSet.enums |> Map.tryPick (fun en e -> e.values |> Map.tryPick (fun n v -> if n = r.Name then Some (en,v) else None))
                                        errorfn "%f could not get enum %s (%A)" f.Number r.Name hard
                                        set
                            ) set

                        set
                    ) set
            
                let set =
                    { set with name = f.Name; version = Version(int f.Number, int (f.Number * 10.0m) % 10) }

                (set, Map.add f.Number set all)
            else
                (set, all)

        ) (FeatureSet.empty, Map.empty)


    let rec typeName (t : Type) =
        match t with
        | Type.Void -> "unit"
        | Type.Int(false, 8) -> "byte"
        | Type.Int(false, 16) -> "uint16"
        | Type.Int(false, 32) -> "uint32"
        | Type.Int(false, 64) -> "uint64"
        | Type.Int(true, 8) -> "sbyte"
        | Type.Int(true, 16) -> "int16"
        | Type.Int(true, 32) -> "int"
        | Type.Int(true, 64) -> "int64"
        | Type.Int _ -> failwith "unknown int type"

        | Type.Float(32) -> "float32"
        | Type.Float(64) -> "float"
        | Type.Float(16) -> "float16"
        | Type.Float _ -> failwith "unknown int type"

        | Type.NativeInt true -> "nativeint"
        | Type.NativeInt false -> "unativeint"
        | Type.Bool -> "bool"
        | Type.Char -> "byte"
        | Type.Enum n -> n
        | Type.Pointer Type.Void -> "nativeint"
        | Type.Pointer t -> sprintf "nativeptr<%s>" (typeName t)

    let typeSize (t : Type) =
        match t with
        | Type.Void -> Some 0
        | Type.Int(_,b) -> Some (b / 8)
        | Type.Float b  -> Some (b / 8)
        | Type.NativeInt _ -> None
        | Type.Bool -> Some 4
        | Type.Char -> Some 1
        | Type.Enum _ -> Some 4
        | Type.Pointer _ -> None


    let featureSets = featureSets |> Map.filter (fun _ cmd -> cmd.version >= Version(4,6))

    for (_,ftr) in Map.toSeq featureSets do
        printfn "module GL%d%d = " ftr.version.Major ftr.version.Minor 

        printfn "    module private Pointers ="
        for (_,cmd) in Map.toSeq ftr.commands do
            let cleanName = cmd.name.Substring 2
            printfn "        let %s = getProcAddress \"%s\"" cleanName cmd.name
            
        printfn "    type CommandStream(s : BinaryWriter) ="
        
        let mutable i = 0
        
        let id (name : string) = "_" + name

        let rec allCombinations (is : list<list<'x>>) =
            match is with
            | [] -> 
                [[]]
            | h::t ->
                let rest = allCombinations t
                h |> List.collect ( fun h ->
                    rest |> List.map (fun r ->
                        h::r
                    )
                )

        let allCmds = //ftr.commands |> Map.toSeq |> Seq.map snd
            ftr.commands |> Map.toSeq |> Seq.collect (fun (_,cmd) -> 
                if List.length cmd.parameters > 7 || cmd.name.StartsWith "glGet" || cmd.name.StartsWith "glReadPixels" || cmd.name.StartsWith "glTextureParameter" || cmd.name.StartsWith "glTextureStorage" || cmd.name.StartsWith "glTexStorage" || cmd.name.StartsWith "glTextureBuffer" || cmd.name.StartsWith "glTextureSubImage" || cmd.name.StartsWith "glTextureImage" || cmd.name.StartsWith "glTexSubImage" || cmd.name.StartsWith "glTexImage" || cmd.name.StartsWith "glTransformFeedback" || cmd.name.StartsWith "glVertexArrayAttrib" || cmd.name.StartsWith "glVertexArray" ||  cmd.name.StartsWith "glViewport" || cmd.name.StartsWith "glProgramUniform" || cmd.name.StartsWith "glUniform" || cmd.name.StartsWith "glVertexAttrib" then 
                    Seq.ofList [
                        cmd
                        { cmd with parameters = cmd.parameters |> List.map (fun (n,t) -> n, Type.Pointer t) }
                    ]
                else
                    cmd.parameters 
                    |> List.map (fun (n,t) -> 
                        if n = "target" || n = "flags" then [n,t]
                        elif t = Type.Pointer Type.Char || t = Type.Bool then [n,t]
                        else [(n,t); (n, Type.Pointer t)]
                    ) 
                    |> allCombinations
                    |> Seq.map (fun pars ->
                        { cmd with parameters = pars }
                    )
            )


        for cmd in allCmds do
            let cleanName = cmd.name.Substring 2
            let args = cmd.parameters |> List.map (fun (n,t) -> sprintf "%s : %s" (id n) (typeName t)) |> String.concat ", "

            let mutable fixedSize = 0
            let mutable ptrCount = 0
            for (_,t) in cmd.parameters do
                match typeSize t with
                | Some s -> fixedSize <- fixedSize + s
                | None -> ptrCount <- ptrCount + 1


            printf "        member x.%s(%s) = " cleanName args
            let size = 
                match ptrCount, fixedSize with
                    | 0, 0 -> sprintf "8"
                    | 0, s -> sprintf "%d" (8 + s)
                    | 1, 0 -> sprintf "8 + sizeof<nativeint>"

                    | c, 0 -> sprintf "8 + %d * sizeof<nativeint>" c
                    | 1, s -> sprintf "%d + sizeof<nativeint>" (8 + s)
                    | c, s -> sprintf "%d + %d * sizeof<nativeint>" (8 + s) c
                
            printf "s.Write(%s); s.Write(%d)" size i

            for (n, t) in cmd.parameters do
                let n = id n
                let data =
                    match t with
                    | Type.Bool -> sprintf "if %s then 1 else 0" n
                    | Type.Pointer _ -> sprintf "NativePtr.toNativeInt %s" n
                    | Type.Enum _ -> sprintf "int %s" n
                    | _ -> n
                printf "; s.Write(%s)" data

            printfn ""


            i <- i + 1




        //for (_,e) in Map.toSeq ftr.enums do
        //    printfn "type %s =" e.name
        //    for (n,v) in Map.toSeq e.values do
        //        printfn "    | %s = %d" n v

        //printfn ""

        //for (_,cmd) in Map.toSeq ftr.commands do
            
        //    let args = 
        //        match cmd.parameters with
        //        | [] -> "unit"
        //        | p -> p |> List.map (fun (_,t) -> typeName t) |> String.concat " * "
        //    let ret = typeName cmd.returnType

        //    printfn "    type private %sDel = delegate of %s -> %s" cmd.name args ret


        //for (_,cmd) in Map.toSeq ftr.commands do
        //    printfn "    let private _%s = import<%sDel> \"%s\"" cmd.name cmd.name cmd.name
            
        //for (_,cmd) in Map.toSeq ftr.commands do
        //    let dargs = cmd.parameters |> List.map (fun (n,t) -> sprintf "%s : %s" n (typeName t)) |> String.concat ", "
        //    let uargs = cmd.parameters |> List.map (fun (n,t) -> n) |> String.concat ", "
        //    let ret = typeName cmd.returnType
        //    printfn "    let %s(%s) : %s = _%s.Invoke(%s)" cmd.name dargs ret cmd.name uargs


        

        //printfn "%A" ftr.commands
        //printfn "%A" ftr.enums


    //let containingEnums =
    //    enums |> Map.toSeq |> Seq.collect (fun (n,e) ->
    //        e.values |> Map.toSeq |> Seq.map (fun (vn, _) ->
    //            vn, n
    //        )
    //    )
    //    |> Map.ofSeq

    //let mutable enums = enums
    //let mutable commands = commands

    //let tryGetEnumValue (name : string) =
    //    match Map.tryFind name containingEnums with
    //    | Some enumName ->
    //        match Map.tryFind enumName enums with
    //        | Some e ->
    //            let mutable value = None
    //            let e = { e with values = Map.filter (fun n v -> if n = name then value <- Some v; false else true) e.values }
    //            match value with
    //            | Some v -> 
    //                if Map.isEmpty e.values then enums <- Map.remove enumName enums
    //                else enums <- Map.add enumName e enums
    //                Some (enumName, (name, v))
    //            | None -> None
    //        | None ->
    //            None
    //    | None -> 
    //        None
    
    //let tryGetCommand (name : string) =
    //    match Map.tryFind name commands with
    //    | Some c ->
    //        commands <- Map.remove name commands
    //        Some c
    //    | None ->
    //        None

    //let union (l : Map<_,_>) (r : Map<_,_>) =
    //    let mutable res = l
    //    for (KeyValue(k,v)) in r do
    //        res <- Map.add k v res
    //    res




    //let proto (cmd : Command) =
    //    let args = cmd.parameters |> List.map (fun (n,t) -> sprintf "%s : %s" n (typeName t)) |> String.concat ", "
    //    let ret = cmd.returnType |> typeName
    //    sprintf "let %s(%s) : %s = failwith\"%s\"" cmd.name args ret cmd.name
        
    //let features = 
    //    glspec.Features 
    //    |> Array.sortBy (fun f -> f.Number)


    //let enums, commands = 
    //    features |> Seq.fold (fun (oe, oc) f ->
    //        let enums, commands = 
    //            f.Requires |> Array.choose (fun r ->
    //                let enums = 
    //                    r.Enums 
    //                    |> Seq.choose (fun r -> tryGetEnumValue r.Name)
    //                    |> Seq.groupBy fst
    //                    |> Seq.map (fun (g, fs) -> g, { name = g; values = Map.ofSeq (Seq.map snd fs); flags = false })
    //                    |> Map.ofSeq
                  
    //                let commands =
    //                    r.Commands
    //                    |> Seq.choose (fun c -> tryGetCommand c.Name |> Option.map (fun cmd -> c.Name, cmd))
    //                    |> Map.ofSeq

    //                if Map.isEmpty enums && Map.isEmpty commands then
    //                    None
    //                else
    //                    Some (enums, commands)
    //            )
    //            |> Array.unzip
    //        let enums = enums |> Seq.fold union Map.empty
    //        let commands = commands |> Seq.fold union Map.empty
            
    //        let ne = union oe enums
    //        let nc = union oc commands

    //        let (ne, nc) = 
    //            f.Removes |> Array.fold (fun (e, c) r -> 
    //                let c = r.Commands |> Seq.fold (fun oc c -> Map.remove c.Name oc) c
    //                let e = r.Enums |> Seq.fold (fun oe c -> Map.remove c.Name oe) e
    //                (e,c)
    //            ) (ne, nc)

    //        (ne, nc)
    //    ) (Map.empty, Map.empty)

    //let exts =
    //    glspec.Extensions |> Seq.choose (fun e ->
    //        let name = e.Name
    //        let enums, commands = 
    //            e.Requires |> Array.choose (fun r ->
    //                let enums = 
    //                    r.Enums 
    //                    |> Seq.choose (fun r -> tryGetEnumValue r.Name)
    //                    |> Seq.groupBy fst
    //                    |> Seq.map (fun (g, fs) -> g, { name = g; values = Map.ofSeq (Seq.map snd fs); flags = false })
    //                    |> Map.ofSeq
                  
    //                let commands =
    //                    r.Commands
    //                    |> Seq.choose (fun c -> tryGetCommand c.Name |> Option.map (fun cmd -> c.Name, cmd))
    //                    |> Map.ofSeq

    //                if Map.isEmpty enums && Map.isEmpty commands then
    //                    None
    //                else
    //                    Some (enums, commands)
    //            )
    //            |> Array.unzip

    //        let enums = enums |> Seq.fold union Map.empty
    //        let commands = commands |> Seq.fold union Map.empty


    //        Some (name, (enums, commands))
    //    )
    //    |> Map.ofSeq

    //for (_, e) in Map.toSeq enums do
    //    if e.flags then printfn "[<Flags>]"
    //    printfn "type %s =" e.name
    //    for (n, v) in Map.toSeq e.values do   
    //        printfn "    | %s = %A" n v

    //for (_,c) in Map.toSeq commands do 
    //    printfn "%s" (proto c)

    //for (en, (eenums, commands)) in Map.toSeq exts do
    //    if not (Map.isEmpty eenums) || not (Map.isEmpty commands) then
    //        printfn "module %s = " en

    //        printfn "    let Name = \"%s\"" en

    //        for (n, vs) in Map.toSeq eenums do
    //            if Map.containsKey n enums then
    //                printfn "    type %s with" n
    //                for (fn,v) in Map.toSeq vs.values do
    //                    printfn "        static member inline %s = unbox<%s> %d" fn n v

    //            else
    //                printfn "    type %s =" n
    //                for (n,v) in Map.toSeq vs.values do
    //                    printfn "        | %s = %d" n v

                
    //            for (n, cmd) in Map.toSeq commands do
    //                printfn "    %s" (proto cmd)
                
    File.WriteAllText(Path.Combine(__SOURCE_DIRECTORY__, "blubb.fs"), str.ToString())
    if errors.Count > 0 then
        printfn "ERRORS"
        if errors.Count > 10 then
            for e in Seq.take 10 errors do
                printfn "%s" e
            printfn "... (%d more)" (errors.Count - 10)
        else
            for e in errors do
                printfn "%s" e