open System
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open FShade

module Shady =
    let sam =
        sampler2dArray {
            texture uniform?BlaBlub
            filter Filter.MinMagLinear
            addressU WrapMode.Clamp
            addressV WrapMode.Clamp
        }

    let frag (v : Effects.Vertex) =
        fragment {
            let slice : int = uniform?choice
            return sam.Sample(v.tc,slice)
        }

type Texy(runtime : ITextureRuntime, size : V2i, count : aval<int>) =
    inherit AdaptiveResource<IBackendTexture>()

    let mutable handle : Option<IBackendTexture * int> = None

    let rand = RandomSystem()

    let generateTexture() =
        let pimg = PixImage<float32>(Col.Format.RGBA, size)
        let mat = pimg.GetMatrix<C4f>()
        let b() = rand.UniformC3f().ToC4f()
        mat.SetByCoord(fun _ -> b()) |> ignore
        pimg

    let createAndUploadTexture (count : int) =
        Log.warn "Create texture"
        let t = runtime.CreateTexture2DArray(size, TextureFormat.Rgba32f, 1, 1, count)
        for slice in 0.. count - 1 do
            let tex = generateTexture()
            runtime.Upload(t, 0, slice, tex)

        handle <- Some (t, count)
        t

    override x.Create() =
        ()

    override x.Destroy() =
        match handle with
        | Some (h, _) ->
            Log.warn "Delete texture"
            runtime.DeleteTexture h
            handle <- None
        | None ->
            ()

    override x.Compute(token : AdaptiveToken, t : RenderToken) =

        let count = count.GetValue(token)

        match handle with
        | Some (h, c) when count = c ->
            h

        | Some (h, _) ->
            t.ReplacedResource(ResourceKind.Texture)
            runtime.DeleteTexture h
            createAndUploadTexture count

        | None ->
            t.CreatedResource(ResourceKind.Texture)
            createAndUploadTexture count

[<EntryPoint>]
let main argv =
    Aardvark.Init()

    let win =
        window {
            backend Backend.GL
            display Display.Mono
            debug false
            samples 8
        }

    let quadGeometry =
        IndexedGeometry(
            Mode = IndexedGeometryMode.TriangleList,
            IndexArray = ([|0;1;2; 0;2;3|] :> Array),
            IndexedAttributes =
                SymDict.ofList [
                    DefaultSemantic.Positions, [| V3f.OOO; V3f.IOO; V3f.IIO; V3f.OIO |] :> Array
                    DefaultSemantic.Colors, [| C4b.Red; C4b.Green; C4b.Blue; C4b.Yellow |] :> Array
                    DefaultSemantic.DiffuseColorCoordinates, [|V2d.OO; V2d.IO; V2d.II; V2d.OI|] :> Array
                ]
        )

    let rand = RandomSystem()
    // GL_MAX_ARRAY_TEXTURE_LAYERS 2048
    // GL_MAX_3D_TEXTURE_SIZE 16384
    let rt = win.Runtime

    let slice = AVal.init 0

    let count = AVal.init 10
    let onoff = AVal.init true

    let texy = Texy(rt, V2i(64), count)

    let sg =
        onoff |> AVal.map (fun o ->
            if not o then Sg.empty
            else
                quadGeometry
                    |> Sg.ofIndexedGeometry
                    |> Sg.effect [
                        DefaultSurfaces.trafo |> toEffect
                        DefaultSurfaces.vertexColor |> toEffect
                        Shady.frag |> toEffect
                       ]
                    |> Sg.texture (Sym.ofString "BlaBlub") texy
                    |> Sg.uniform "choice" slice
        ) |> Sg.dynamic

    win.Keyboard.DownWithRepeats.Values.Add (fun k ->
        match k with
        | Keys.OemPlus ->  transact(fun _ -> let nv = min (slice.Value + 1) (count.Value-1) in Log.line "slice %d (%d)" nv (count.Value-1); slice.Value <- nv)
        | Keys.OemMinus -> transact(fun _ -> let nv = max (slice.Value - 1) 0 in Log.line "slice %d (%d)" nv (count.Value-1); slice.Value <- nv)
        | Keys.Space -> transact(fun _ -> let nv = rand.UniformInt(10)+1 in Log.line "new count: %d" nv; count.Value <- nv; slice.Value <- 0)
        | Keys.Enter -> transact(fun _ -> onoff.Value <- not onoff.Value)
        | _ -> ()
    )

    win.Scene <- sg
    win.Run()

    0
