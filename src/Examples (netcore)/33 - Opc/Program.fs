open Opc
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open System.Runtime.InteropServices
open Aardvark.SceneGraph.Opc
open MBrace.FsPickler


#nowarn "9"
#nowarn "8989"


module Serialization =

  let registry = new CustomPicklerRegistry()    
  let cache = PicklerCache.FromCustomPicklerRegistry registry    

  let binarySerializer = FsPickler.CreateBinarySerializer(picklerResolver = cache)
  //let jsonSerializer = FsPickler.CreateJsonSerializer(indent=true)

module Discover = 
  open System.IO
  
  /// <summary>
  /// checks if "path" is a valid opc folder containing "images", "patches", and patchhierarchy.xml
  /// </summary>
  let isOpcFolder (path : string) = 
      let imagePath = Path.combine [path; "images"]
      let patchPath = Path.combine [path; "patches"]
      (Directory.Exists imagePath) &&
      (Directory.Exists patchPath) && 
       File.Exists(patchPath + "\\patchhierarchy.xml")
  
  /// <summary>
  /// checks if "path" is a valid surface folder
  /// </summary>        
  let isSurfaceFolder (path : string) =
      Directory.GetDirectories(path) |> Seq.forall isOpcFolder
  
  let discover (p : string -> bool) path : list<string> =
    if Directory.Exists path then
      Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories)                     
        |> Seq.filter p            
        |> Seq.toList
    else List.empty
  
  /// returns all valid surface folders in "path"   
  let discoverSurfaces path = 
    discover isSurfaceFolder path          
  
  let discoverOpcs path = 
    discover isOpcFolder path


type PatchLodTree(globalCenter : V3d, opc : OpcPaths, root : option<ILodTreeNode>, parent : option<ILodTreeNode>, level : int, tree : QTree<Patch>) as this =
    static let source = Symbol.Create "Disk"
    let patch, isLeaf =
        match tree with
        | Leaf p -> p, true
        | Node(p,_) -> p, false

    let cell = Cell patch.info.GlobalBoundingBox


    let localBounds = patch.info.GlobalBoundingBox.Translated(-globalCenter)
          
    let data = lazy (Patch.load opc ViewerModality.XYZ patch.info)

    let getGeometry() = 
        let g, _ = data.Value
        g

    static let isOrtho (proj : Trafo3d) = proj.Forward.R3.ApproximateEquals(V4d.OOOI,1E-8)
  
    static let fov (proj : Trafo3d) =
        2.0 * atan(proj.Backward.M00) * Constant.DegreesPerRadian

    let equivalentAngle60 (view : Trafo3d) (proj : Trafo3d) =
        if isOrtho proj then 
            let width = proj.Backward.M00 * 2.0 
            let avgPointDistance = patch.triangleSize //localBounds.Size.NormMax / 40.0

            60.0 * avgPointDistance / width
        else 
            let cam = view.Backward.C3.XYZ

            let avgPointDistance = patch.triangleSize //localBounds.Size.NormMax / 40.0

            let minDist = localBounds.GetMinimalDistanceTo(cam)
            let minDist = max 0.01 minDist

            let angle = Constant.DegreesPerRadian * atan2 avgPointDistance minDist

            let fov = fov proj

            60.0 * angle / fov
        
    let children() =
        //lazy (
            match tree with
                | Node(_, cs) ->
                    cs |> Array.map (fun c -> 
                        let root = 
                            match root with
                            | Some r -> r
                            | None -> this :> ILodTreeNode
                        PatchLodTree(globalCenter, opc, Some root, Some (this :> ILodTreeNode), level + 1, c) :> ILodTreeNode
                    )
                | Leaf _ -> 
                    Array.empty
        //)

    member x.Patch = patch
    member x.GlobalCenter = globalCenter

    override x.GetHashCode() =
        HashCode.Combine(Unchecked.hash globalCenter, Unchecked.hash patch)

    override x.Equals o =
        match o with
        | :? PatchLodTree as o ->
            o.Patch = patch && globalCenter = o.GlobalCenter
        | _ ->
            false


    interface ILodTreeNode with
        member this.Acquire() = ()
        member this.Release() = ()
        member this.Root = 
            match root with
            | Some r -> r
            | None -> this :> ILodTreeNode
        member this.Parent = parent
        member this.Level = level
        member this.Id = patch.info.Name :> obj
        member this.Name = patch.info.Name
        member this.TotalDataSize = getGeometry().IndexArray.Length / 3
        member this.DataSize = getGeometry().IndexArray.Length / 3
        member this.WorldBoundingBox = patch.info.GlobalBoundingBox

        member this.Cell = cell
        member this.WorldCellBoundingBox = patch.info.GlobalBoundingBox
        member this.DataSource = source

        member this.Children = children() :> seq<_>

        member this.DataTrafo = 
            Trafo3d.Translation(globalCenter)

        member this.GetData(ct, inputs) = 
            let g = getGeometry()

            let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
            let tc = g.IndexedAttributes.[DefaultSemantic.DiffuseColorCoordinates] |> unbox<V2f[]>

            let trafo = 
                (patch.info.Local2Global * Trafo3d.Translation(-globalCenter)).Forward

            for i in 0 .. positions.Length - 1 do
                positions.[i] <- trafo.TransformPos (V3d positions.[i]) |> V3f

            let positions =
                g.IndexArray |> unbox<int[]> |> Array.map (fun i -> positions.[i])
                
            let tc =
                g.IndexArray |> unbox<int[]> |> Array.map (fun i -> tc.[i])

            let g = 
                IndexedGeometry(
                    Mode = IndexedGeometryMode.TriangleList,
                    IndexedAttributes =
                        SymDict.ofList [
                            DefaultSemantic.Positions, positions :> System.Array
                            DefaultSemantic.DiffuseColorCoordinates, tc :> System.Array
                        ]
                )

            let img =
                let path = Patch.extractTexturePath opc patch.info 0
                try PixImage.Create(path).ToPixImage<byte>(Col.Format.RGBA)
                with _ -> DefaultTextures.checkerboardPix

            let tex = 
                { new INativeTexture with   
                    member x.WantMipMaps = false
                    member x.Format = TextureFormat.Rgba8
                    member x.MipMapLevels = 1
                    member x.Count = 1
                    member x.Dimension = TextureDimension.Texture2D
                    member x.Item
                        with get(slice : int, level : int) = 
                            { new INativeTextureData with
                                member x.Size = V3i(img.Size, 1)
                                member x.SizeInBytes = img.Volume.Data.LongLength
                                member x.Use (action : nativeint -> 'a) =
                                    let gc = GCHandle.Alloc(img.Volume.Data, GCHandleType.Pinned)
                                    try action (gc.AddrOfPinnedObject())
                                    finally gc.Free()
                            }
                }
                
                //PixTexture2d(PixImageMipMap [| img :> PixImage |], false)

            let uniforms =
                MapExt.ofList [
                    "DiffuseColorTexture", [| tex |] :> System.Array
                ]

            g, uniforms

       
        member x.ShouldSplit (splitfactor : float, quality : float, view : Trafo3d, proj : Trafo3d) =
            
            not isLeaf && equivalentAngle60 view proj > splitfactor / quality

        member x.ShouldCollapse (splitfactor : float, quality : float, view : Trafo3d, proj : Trafo3d) =
            equivalentAngle60 view proj < (splitfactor * 0.75) / quality
            
        member x.SplitQuality (splitfactor : float, view : Trafo3d, proj : Trafo3d) =
            splitfactor / equivalentAngle60 view proj

        member x.CollapseQuality (splitfactor : float, view : Trafo3d, proj : Trafo3d) =
            (splitfactor * 0.75) / equivalentAngle60 view proj

    new(globalCenter : V3d, paths : OpcPaths, p : QTree<Patch>) = PatchLodTree(globalCenter, paths, None, None, 0, p)


module Shader =
    open FShade
    
    type LodVertex =
        {
            [<Position>] pos : V4d
            [<TexCoord>] tc : V2d
            [<Semantic("ViewPosition")>] vp : V4d
            [<Color>] col : V4d
            [<Normal>] n : V3d
            [<Semantic("TreeId")>] id : int
            [<Semantic("DiffuseColorTextureTrafo")>] textureTrafo : V4d
        }
        
    type UniformScope with
        member x.ModelTrafos : M44d[] = x?StorageBuffer?ModelTrafos
        member x.ModelViewTrafos : M44d[] = x?StorageBuffer?ModelViewTrafos

    let trafo (v : LodVertex) =       
        vertex { 
            let mv = uniform.ModelViewTrafos.[v.id]
            //let f = if magic then 0.07 else 1.0 / 0.3

            let vp = mv * v.pos
            let vn = mv * V4d(v.n, 0.0) |> Vec.xyz |> Vec.normalize
            let pp = uniform.ProjTrafo * vp

            
            let mutable tc = v.tc
            if tc.X < 0.0 then tc.X <- 0.0
            elif tc.X > 1.0 then tc.X <- 1.0
            if tc.Y < 0.0 then tc.Y <- 0.0
            elif tc.Y > 1.0 then tc.Y <- 1.0

            tc.Y <- 1.0 - tc.Y

            let tc =
                let trafo = v.textureTrafo
                if trafo.Z < 0.0 then
                    V2d(-trafo.Z, trafo.W) * V2d(1.0 - tc.Y, tc.X) + trafo.XY
                else
                    trafo.ZW * tc + trafo.XY


            return { v with pos = pp; vp = vp; n = vn; tc = tc }

        }
        
    let normals (v : Triangle<LodVertex>) =       
        triangle { 
            let p0 = v.P0.vp.XYZ
            let p1 = v.P1.vp.XYZ
            let p2 = v.P2.vp.XYZ

            let vn = Vec.cross (p1 - p0) (p2 - p0) |> Vec.normalize

            yield { v.P0 with n = vn }
            yield { v.P1 with n = vn }
            yield { v.P2 with n = vn }

        }
        
    let sam =
        sampler2d {
            texture uniform?DiffuseColorTexture
            filter Filter.MinLinearMagPointMipLinear
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
            maxAnisotropy 16
        }



    let light (v : LodVertex) =    
        fragment {
            
            let tc = 
                let mutable tc = V2d(v.tc.X % 1.0, v.tc.Y % 1.0)
                if tc.X < 0.0 then tc.X <- 1.0 + tc.X
                if tc.Y < 0.0 then tc.Y <- 1.0 + tc.Y
                tc

            if uniform?MipMaps then
                if uniform?Anisotropic then
                    return sam.Sample(tc)
                else
                    let s = V2d sam.Size
                    let maxLevel = sam.MipMapLevels - 1 |> float

                    let dx = s * ddx v.tc
                    let dy = s * ddy v.tc

                    let level = log2 (max (Vec.length dx) (Vec.length dy)) |> clamp 0.0 maxLevel

                    return sam.SampleLevel(v.tc, level)
            else
                return sam.SampleLevel(v.tc, 0.0)
        }

[<EntryPoint>]
let main argv = 
    Aardvark.Init()
    
    let win = 
        window {
            backend Backend.GL
            showHelp false
            display Display.Mono
            device DeviceKind.Dedicated
            debug false
            samples 8
        }


    let mipMaps = cval true
    let anisotropic = cval true
    let fixUp = cval false
    let split = cval 0.4
    win.Keyboard.DownWithRepeats.Values.Add (fun k ->
        match k with
        | Keys.Space -> 
            transact (fun () -> mipMaps.Value <- not mipMaps.Value)
            Log.line "mipMaps: %A" mipMaps.Value
        | Keys.Enter | Keys.Return ->
            transact (fun () -> anisotropic.Value <- not anisotropic.Value)
            Log.line "anisotropic: %A" anisotropic.Value
        | Keys.Escape ->
            transact (fun () -> fixUp.Value <- not fixUp.Value)
            Log.line "fixUp: %A" fixUp.Value
            
        | Keys.OemPlus ->
            transact (fun () -> split.Value <- split.Value + 0.1)
            Log.line "split: %A" split.Value
            
        | Keys.OemMinus ->
            transact (fun () -> split.Value <- max 0.1 (split.Value - 0.1))
            Log.line "split: %A" split.Value

        | _ ->
            ()
    )



    
    let load (path : string) =
        let patchHierarchies = 
            List.concat [
                Discover.discoverOpcs path
            ] |> List.map OpcPaths
        
        
        patchHierarchies 
        |> List.map (fun p -> p, PatchHierarchy.load Serialization.binarySerializer.Pickle Serialization.binarySerializer.UnPickle p)
        |> List.map (fun (p, h) ->  
            let center = 
                match h.tree with
                | QTree.Node(i,_) -> i.info.GlobalBoundingBox.Center
                | QTree.Leaf i -> i.info.GlobalBoundingBox.Center

            {
                root = PatchLodTree(center, p, h.tree) :> ILodTreeNode
                uniforms = MapExt.empty
            }
        )

    let patchHierarchies =
        load @"C:\Users\Schorsch\Desktop\SelzThal"
        |> cset


    win.DropFiles.Add (fun strs ->
        if strs.Length > 0 then 
            Log.line "load %s" strs.[0]
            transact (fun () ->
                patchHierarchies.Value <- HashSet.ofSeq (load strs.[0])
            )
            
    )

    let stats = cval Unchecked.defaultof<LodRendererStats>

    let cfg =
        {
            budget = AVal.constant -1L
            splitfactor = split
            time = win.Time
            maxSplits = AVal.constant 12
            renderBounds = AVal.constant false
            stats = stats
            pickTrees = None
            alphaToCoverage = false
        }

    //let bb = 
    //    res |> ASet.toAVal |> AVal.map (fun s ->
    //        s |> Seq.truncate 1 |> List.map (fun p -> p.root.WorldBoundingBox) |> Box3d
    //    )
    //let bb = res |> List.truncate 1 |> List.map (fun p -> p.root.WorldBoundingBox) |> Box3d




    let bounds = 
        patchHierarchies |> ASet.toAVal |> AVal.map (fun s ->
            s |> Seq.truncate 1 |> Seq.map (fun p -> p.root.WorldBoundingBox) |> Box3d
        )

    let views = 
        bounds |> AVal.bind (fun bb ->
            CameraView.lookAt (bb.Center + V3d(6,6,6)) bb.Center V3d.OOI 
            |> DefaultCameraController.controlExt 1.0 win.Mouse win.Keyboard win.Time
            |> AVal.map CameraView.viewTrafo
        )

    let views =
        fixUp |> AVal.bind (function
            | true -> 
                (bounds, views) ||> AVal.map2 (fun b v ->
                    let n = b.Center |> Vec.normalize
                    let t = Trafo3d.RotateInto(n, V3d.OOI)
                    
                    Trafo3d.Translation(-b.Center) *
                    t *
                    Trafo3d.Translation(b.Center) *
                    v

                )
             
            | false -> 
                views
        )

    let proj = win.Sizes |> AVal.map (fun s -> Frustum.perspective 60.0 1.0 60000000.0 (float s.X / float s.Y) |> Frustum.projTrafo)

    let sg = 
        Sg.lodTree cfg patchHierarchies
        |> Sg.shader {
            do! Shader.trafo
            //do! Shader.normals
            do! Shader.light
        }
        |> Sg.uniform "ViewTrafo" views
        |> Sg.uniform "ProjTrafo" proj
        |> Sg.uniform "MipMaps" mipMaps
        |> Sg.uniform "Anisotropic" anisotropic
        |> Sg.viewTrafo views
        |> Sg.projTrafo proj

    let overlay = LodRendererStats.toSg win stats


    win.Scene <- Sg.ofList [sg; overlay]
    win.Run()

    0
