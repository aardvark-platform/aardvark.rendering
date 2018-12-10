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
    let ofString (enums : Map<string, Enum>) (name : string) =
        let name = name.ToLower().Trim()
        match name with
        | "void" -> Type.Void

        | "glhalfnv" -> Type.Float 16

        | "glfloat" 
        | "glclampf" -> Type.Float 32

        | "gldouble" -> Type.Float 64

        | "glsizeiptr" -> Type.NativeInt false

        | "glboolean" | "glbool" -> Type.Bool
        | "glchar" -> Type.Char

        | "glintptr" | "glvdpauSurfacenvv" -> Type.NativeInt true

        | "glubyte" -> Type.Int(false, 8)
        | "glushort" -> Type.Int(false, 16)
        | "gluint" | "glsizei" -> Type.Int(false, 32)
        | "gluint64" | "gluint64ext" -> Type.Int(false, 64)
        
        | "glbyte" -> Type.Int(true, 8)
        | "glshort" -> Type.Int(true, 16)
        | "glfixed" | "glbitfield" | "glint" -> Type.Int(true, 32)
        | "glint64" | "glint64ext" -> Type.Int(true, 64)



        | n -> 
            if Map.containsKey n enums then
                Type.Enum n
            else
                errorfn "unknown type %s" n
                Type.Void

type Command =
    {
        group       : Option<string>
        name        : string
        parameters  : list<string * Type>
    }

let test() = 
    let glspec = GlSpec.Load(Path.Combine(__SOURCE_DIRECTORY__, "gl.xml"))
    
    let values =
        glspec.Enums |> Seq.collect (fun e ->
            e.Enums |> Seq.choose (fun value ->
                let str = value.Value.Value.ToLower()
                if str.StartsWith "0x" then
                    let str = str.Substring 2
                    match Int64.TryParse(str, NumberStyles.HexNumber, CultureInfo.InvariantCulture) with
                        | (true, v) -> Some (value.Name, int v)
                        | _ -> 
                            errorfn "cannot parse value for %s: %A" value.Name str
                            None
                else
                    match Int64.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture) with
                        | (true, v) -> Some (value.Name, int v)
                        | _ -> 
                            errorfn "cannot parse value for %s: %A" value.Name str
                            None
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
        glspec.Groups |> Array.map (fun g ->
            let values = 
                g.Enums |> Array.choose (fun e ->
                    match Map.tryFind e.Name values with 
                    | Some v -> Some (e.Name, v)
                    | None -> 
                        errorfn "no value for %s" e.Name
                        None
                )
            g.Name,
            {
                name    = g.Name
                flags   = Set.contains g.Name bitmasks
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
                        match p.Ptype with
                        | Some t ->
                            if t.ToLower().Trim() = "glenum" then
                                match p.Group with
                                | Some g -> 
                                    if Map.containsKey g enums then 
                                        Some (p.Name, Type.Enum g)
                                    else 
                                        errorfn "could not find enum %s" g
                                        Some (p.Name, Type.Enum g)
                                | None ->
                                    errorfn "no enum name for parameter %s in %s" p.Name proto.Name
                                    Some (p.Name, Type.Int(true, 32))
                            else
                                Some (p.Name, Type.ofString enums t)
                        | None ->
                            errorfn "no type for %s in %s" p.Name proto.Name
                            None
                    )

                Some {
                    name = proto.Name
                    group = proto.Group
                    parameters = parameters
                }
        )


    for (_, e) in Map.toSeq enums do
        if e.flags then printfn "[<Flags>]"
        printfn "type %s =" e.name
        for (n, v) in Map.toSeq e.values do   
            printfn "    | %s = %A" n v

    for c in commands do 
        printfn "%A" c


    if errors.Count > 0 then
        printfn "ERRORS"
        for e in errors do
            printfn "%s" e