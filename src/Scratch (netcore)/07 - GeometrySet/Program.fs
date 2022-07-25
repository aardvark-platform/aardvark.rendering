open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open System

let private rnd = RandomSystem()

module IndexedGeometry =

    let createTetrahedron() =
        let color = rnd.UniformC3d().ToC4b()
        let radius = 0.5 + rnd.UniformDouble()
        let center = rnd.UniformV3d() * 10.0
        IndexedGeometryPrimitives.Tetrahedron.solidTetrahedron center radius color

    let createBox() =
        let color = rnd.UniformC3d().ToC4b()
        let size = 0.5 + rnd.UniformV3d()
        let center = rnd.UniformV3d() * 10.0
        let box = Box3d.FromCenterAndSize(center, size)
        IndexedGeometryPrimitives.Box.solidBox box color

    let createTorus() =
        let color = rnd.UniformC3d().ToC4b()
        let center = rnd.UniformV3d() * 10.0
        let direction = rnd.UniformV3d().Normalized
        let minorRadius = 0.1 + rnd.UniformDouble() * 0.25
        let majorRadius = 0.5 + rnd.UniformDouble()
        let torus = Torus3d(center, direction, majorRadius, minorRadius)
        IndexedGeometryPrimitives.Torus.solidTorus torus color 128 128

    let create() =
        match rnd.UniformInt(3) with
        | 0 -> createBox()
        | 1 -> createTorus()
        | _ -> createTetrahedron()

[<EntryPoint>]
let main argv =
    Aardvark.Init()

    let win =
        window {
            backend Backend.Vulkan
            display Display.Mono
            debug true
            samples 8
        }

    let geometries = cset()

    let attributeTypes =
        Map.ofList [
            DefaultSemantic.Positions, typeof<V3f>
            DefaultSemantic.Normals, typeof<V3f>
            DefaultSemantic.Colors, typeof<V4f>
        ]

    let addGeometry() =
        transact (fun _ ->
            let g = IndexedGeometry.create()
            geometries.Value <- geometries.Value |> HashSet.add g
        )

    let removeGeometry() =
        transact (fun _ ->
            let index = rnd.UniformInt(geometries.Count)
            let value = geometries.Value |> Seq.item index
            geometries.Value <- geometries.Value |> HashSet.remove value
        )

    win.Keyboard.KeyDown(Keys.Enter).Values.Add(fun _ ->
        addGeometry()
    )

    win.Keyboard.KeyDown(Keys.Delete).Values.Add(fun _ ->
        removeGeometry()
    )

    addGeometry()

    let sg =
        Sg.geometrySet IndexedGeometryMode.TriangleList attributeTypes geometries
        |> Sg.scale 0.5
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.vertexColor
            do! DefaultSurfaces.simpleLighting
        }

    win.Scene <- sg
    win.Run()

    0
