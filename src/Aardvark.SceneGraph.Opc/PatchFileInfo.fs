#if INTERACTIVE
#r "../../bin/Debug/Aardvark.Base.dll"
#r "../../bin/Debug/Aardvark.Base.TypeProviders.dll"
#r "../../bin/Debug/Aardvark.Base.FSharp.dll"
#r "System.Xml.Linq.dll"
#else
namespace Aardvark.SceneGraph.Opc
#endif

open System
open System.Xml
open System.Xml.Linq

open Aardvark.Base
open Aardvark.Base.IO

module private XmlHelpers = 
    
    let xname str = XName.op_Implicit str
    let xattr (elem: XElement) sname = elem.Attribute(xname sname).Value
    let elem name (v : XElement) = 
        match v.Elements(xname name) |> Seq.toList with
            | []-> failwithf "could not get value: %s" name
            | [x] -> x
            | _ -> failwithf "could not get value (not unique): %s" name
    let elem' name (v : XElement) = 
        match v.Elements(xname name) |> Seq.toList with
            | []-> None
            | [x] -> Some x
            | _ -> None
    let elems name (p : XElement) = 
        p.Elements (xname name) 

    let children name (p : XElement) =
        p.Descendants (xname name)

    let xvalue (p : XElement) = p.Value.Trim()

    let prop name (p : XElement) = 
        (elem name p).Value.Trim()

type Texture = { fileName : string; weights : string }
type VertexOrder = RowMajor | ColumnMajor 
type GeometryType = QuadList
type DiffuseColorCoordinates = string

type PatchFileInfo =
    {
        Name                : string
        TagList             : list<string>
        GeometryType        : GeometryType
        QuadVertexSortOrder : VertexOrder

        Local2Global        : Trafo3d
        GlobalBoundingBox   : Box3d
        LocalBoundingBox    : Box3d

        Local2Global2d      : Trafo3d
        GlobalBoundingBox2d : Box3d
        LocalBoundingBox2d  : Box3d

        Positions           : string
        Positions2d         : option<string>
        Normals             : string
        Offsets             : string

        Coordinates         : list<DiffuseColorCoordinates>
        Textures            : list<Texture>
        Attributes          : list<string>
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module PatchFileInfo =
    open XmlHelpers
    open Aardvark.Prinziple

    [<AutoOpen>]
    module Parsers =
        let majority = function
            | "RowMajor" -> RowMajor
            | "ColumnMajor" -> ColumnMajor
            | s -> failwithf "could not parse majority: %s" s

        let geomtryType = function
            | "QuadList" -> QuadList
            | s -> failwithf "could not parse geometry type: %s" s

        let (|Int32|_|) (s : string) = 
            match System.Int32.TryParse ( s.Trim() ) with
                | (true,v) -> Some v
                | _ -> None

        let tup2V2i (s : string) = 
            match s.Split(',') with
                | [|Int32 x; Int32 y|] -> V2i(x,y)
                | _ -> failwithf "Could not parse: %A" s 

    let node (name : string) (node : XmlNode) =
        node.SelectSingleNode(name)

    let nodes (name : string) (node : XmlNode) =        
        node.SelectNodes(name) |> Seq.cast<XmlNode>

    let childNodes (name : string) (node : XmlNode) =        
        node.SelectSingleNode(name).ChildNodes |> Seq.cast<XmlNode>

    let childNodes' (name : string) (node : XmlNode) =        
        let n = node.SelectSingleNode(name)
        if (n = null) then
            [] |> Seq.cast<XmlNode>
        else
            n.ChildNodes |> Seq.cast<XmlNode>

    let get (name : string) (node : XmlNode)=
        node.SelectSingleNode(name).InnerText.Trim()

    let tryGet (name : string) (node : XmlNode)=
        let n = node.SelectSingleNode(name)
        if (n = null) then
          None
        else
          Some (n.InnerText.Trim())

    let xvalue (p : XmlNode) = p.Value.Trim()

    let inner (node : XmlNode) = 
        node.InnerText.Trim()
    
    let private ofXDoc (doc : XmlDocument) (name : string) (hasDeviations : bool) =        

        let trafo (m : M44d) =
            Trafo3d(m, m.Inverse)

        let mkTexture tupel = 
            let t,w = tupel
            { fileName = inner t; weights = inner w }

        let patch =            
          doc.SelectSingleNode "/Aardvark"
            |> Seq.cast<XmlNode>             
            |> Seq.head
    
        let textures = 
            patch 
              |> childNodes "Textures" 
              |> Seq.chunkBySize 2 |> Seq.map(fun x -> x.[0], x.[1])
              |> Seq.map mkTexture
              |> Seq.toList

        let coords = 
            patch 
              |> nodes "Coordinates" 
              |> Seq.toList 
              |> List.map (string << get "DiffuseColor1Coordinates")
        
        let attributes =
          //if hasDeviations then
            patch 
              |> childNodes' "Attributes"
              |> Seq.map inner
              |> Seq.toList 

        let pos2d = patch |> tryGet "Positions2d"
        let attributes =
          match pos2d with
          | Some _ ->  "Positions2d.aara" :: attributes
          | None   -> attributes
        
        let split (s:string) =
            (s.Split ' ') |> Array.toList

        {
            Name                = name
            TagList             = patch |> get "TagList" |> split

            GeometryType        = patch |> get "GeometryType"        |> geomtryType
            QuadVertexSortOrder = VertexOrder.ColumnMajor //patch |> prop "QuadVertexSortOrder" |> majority
                                                                      
            Local2Global        = patch |> get "Local2Global"        |> M44d.Parse |> trafo
            GlobalBoundingBox   = patch |> get "GlobalBoundingBox"   |> Box3d.Parse
            LocalBoundingBox    = patch |> get "LocalBoundingBox"    |> Box3d.Parse

            Local2Global2d      = patch |> get "Local2Global"        |> M44d.Parse |> trafo
            GlobalBoundingBox2d = patch |> get "GlobalBoundingBox"   |> Box3d.Parse
            LocalBoundingBox2d  = patch |> get "LocalBoundingBox"    |> Box3d.Parse

            Positions           = patch |> get "Positions"
            Positions2d         = patch |> tryGet "Positions2d"
            Normals             = ""
            Offsets             = ""
            Textures            = textures
            Coordinates         = coords              
            Attributes          = attributes
        }    

    let load (opcFolder : string) (patchName : string) =
        let path = Path.combine [opcFolder; "Patches"; patchName; "Patch.xml"]
        let doc = Prinziple.readXmlDoc path
        ofXDoc doc patchName false

    /// <summary>
    /// Loads patchfileinfo with positions2d.aara as attribute
    /// </summary>    
    let load' (opcFolder : string) (patchName : string) =
        let path = Path.combine [opcFolder; "Patches"; patchName; "Patch.xml"]
        let doc = Prinziple.readXmlDoc path
        ofXDoc doc patchName true

type QTree<'a> = Node of 'a * array<QTree<'a>>
                | Leaf of 'a

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module QTree =
    let rec map f t = 
        match t with
            | Leaf v -> Leaf (f v)
            | Node(v,children) ->
                Node(f v, children |> Array.map (map f))

    let rec mapLevel (l : int) f t = 
        match t with
            | Leaf v -> Leaf (f l v)
            | Node(v,children) ->
                Node(f l v, children |> Array.map (mapLevel (l + 1) f))

    let rec getLeaves (tree : QTree<'a>) =      
        match tree with
                | Leaf v -> Seq.singleton v
                | Node(v,children) -> children |> Seq.collect getLeaves
        
    let getRoot (tree : QTree<'a>) =
        match tree with
            | Leaf v -> v
            | Node(v,_) -> v
