open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.Application
open Aardvark.SceneGraph


type Octree(bounds : Box3d) =
    let children = 
        lazy (
            let min = bounds.Min
            let max = bounds.Max
            let c = bounds.Center
            MapExt.ofList [
                0, Octree(Box3d(min.X, min.Y, min.Z, c.X, c.Y, c.Z))
                1, Octree(Box3d(min.X, min.Y, c.Z, c.X, c.Y, max.Z))
                2, Octree(Box3d(min.X, c.Y, min.Z, c.X, max.Y, c.Z))
                3, Octree(Box3d(min.X, c.Y, c.Z, c.X, max.Y, max.Z))
                4, Octree(Box3d(c.X, min.Y, min.Z, max.X, c.Y, c.Z))
                5, Octree(Box3d(c.X, min.Y, c.Z, max.X, c.Y, max.Z))
                6, Octree(Box3d(c.X, c.Y, min.Z, max.X, max.Y, c.Z))
                7, Octree(Box3d(c.X, c.Y, c.Z, max.X, max.Y, max.Z))
            ]
        )

    let data =
        async {
            do! Async.Sleep 100
            return bounds
        }
    let rand = RandomSystem()

    member x.Bounds = bounds

    interface LodTreeNode<Octree, Geometry> with
        member x.Load = 
            async {
                let! data = data
                return
                    IndexedGeometryPrimitives.Box.solidBox data (rand.UniformC3f().ToC4b())
                        |> Geometry.ofIndexedGeometry Map.empty
            }
            |> Some

        member x.Children = children.Value


[<EntryPoint>]
let main argv = 
    
    // first we need to initialize Aardvark's core components
    Ag.initialize()
    Aardvark.Init()


    let win =
        window {
            backend Backend.Vulkan
            display Display.Stereo
            verbosity DebugVerbosity.Warning
            samples 8
        }

    // lets define the bounds/color for our box
    // NOTE that the color is going to be ignored since we're using a texture
    let box = Box3d(-0.7 * V3d.III, 0.7 * V3d.III)
    let color = C4b.Red

    let clear = Mod.init false

    win.Keyboard.DownWithRepeats.Values.Add(fun k ->
        match k with
            | Keys.Space ->
                transact (fun () -> clear.Value <- not clear.Value)
            | _ ->
                ()
    )

    let box = 
        // thankfully aardvark defines a primitive box
        Sg.box (Mod.constant color) (Mod.constant box)

            // apply the texture as "DiffuseTexture"
            |> Sg.diffuseTexture DefaultTextures.checkerboard

            // apply a shader ...
            // * transforming all vertices
            // * looking up the DiffuseTexture 
            // * applying a simple lighting to the geometry (headlight)
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.diffuseTexture
                do! DefaultSurfaces.simpleLighting
            }

    let sphere = 
        // thankfully aardvark defines a primitive box
        Sg.unitSphere' 5 C4b.Red
            // apply a shader ...
            // * transforming all vertices
            // * looking up the DiffuseTexture 
            // * applying a simple lighting to the geometry (headlight)
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.simpleLighting
            }

    let desiredLen = Mod.init 1.0
    let lod = 
        let tree = Octree(Box3d(-V3d.III, V3d.III))
        let visible (b : Octree) = true
        let descend (l : float) (b : Octree) = b.Bounds.Size.Length > l
        let root = Mod.constant (Some tree)

        let view =
            {
                root = Mod.constant (Some tree)
                visible = Mod.constant visible
                descend = desiredLen |> Mod.map descend
                showInner = true
            }   

        let config =
            {
                mode = IndexedGeometryMode.TriangleList
                vertexInputTypes = Map.ofList [DefaultSemantic.Positions, typeof<V3f>; DefaultSemantic.Colors, typeof<C4b>]
                perGeometryUniforms = Map.empty
            }   

        RenderCommand.LodTree(config, LodTreeLoader.create view)
            //|> Sg.execute
            //|> Sg.shader {
            //    do! DefaultSurfaces.trafo
            //}   

    let sg =
        Sg.execute (
            RenderCommand.Ordered [
                RenderCommand.Unordered [ box ]
                RenderCommand.When(
                    clear,
                    RenderCommand.Clear(1.0, 0u)
                )
                RenderCommand.Unordered [ sphere ]
                lod
            ]
        )
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            //do! DefaultSurfaces.thickLine
        }  
        |> Sg.uniform "LineWidth" (Mod.constant 3.0)


    // show the scene in a simple window
    win.Scene <- sg
    win.Run()

    0
