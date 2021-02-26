open System
open Aardvark.Base
open Aardvark.Base.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open System.Runtime.InteropServices
open FShade

#nowarn "9"


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

type Texy(textureCount : aval<int>, rt : IRuntime) =
    inherit AbstractOutputMod<ITexture>()
    let size = V2i(64,64)
    let rand = RandomSystem()
    let mkTexture() = 
        let pimg = PixImage<float32>(Col.Format.RGBA, size)
        let mat = pimg.GetMatrix<C4f>()
        let b() = rand.UniformC3f().ToC4f()
        mat.SetByCoord(fun _ -> b()) |> ignore
        pimg
    let createAndUpload count =
        let t = rt.CreateTextureArray(size, TextureFormat.Rgba32f, 1, 1, count)
        for slice in 0..count-1 do 
            let tex : PixImage<float32> = mkTexture()
            rt.Upload(t,0,slice,tex)
        t
        
    
    let tex = AVal.init None

    let deleteTexture() =
        Log.line "delete"
        tex |> AVal.force |> Option.map fst |> Option.iter rt.DeleteTexture
        transact (fun _ -> tex.Value <- None)
        
    let newTexture count =
        Log.line "new texture %d" count
        deleteTexture()
        let t = createAndUpload count
        transact (fun _ -> tex.Value <- Some (t,count))

    override x.Create() = 
        Log.line "Create"
        newTexture (textureCount.GetValue())

    override x.Compute(token,_) =
        Log.line "Compute"
        let texture = 
            AVal.map2 (fun tex ct ->
                match tex with
                | Some (t, oldct) when oldct = ct -> 
                    t :> ITexture
                | _ -> 
                    newTexture ct
                    let t = tex |> Option.get |> fst
                    t :> ITexture
            ) tex textureCount
        texture.GetValue(token)

    override x.Destroy() =
        Log.line "Destroy" 
        deleteTexture()

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

    let texy = Texy(count,rt)

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
