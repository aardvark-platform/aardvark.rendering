open System
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open FShade

module Semantic =
    let DiffuseColorTextureSlice = Sym.ofString "DiffuseColorTextureSlice"

module Shader =

    let MaxTextureSlices = 16

    type UniformScope with
        member x.DiffuseColorTextureSlice : int = x?DiffuseColorTextureSlice

    let private diffuseSamplerArray =
        sampler2d {
            textureArray uniform?DiffuseColorTexture MaxTextureSlices
            filter Filter.MinMagLinear
            addressU WrapMode.Clamp
            addressV WrapMode.Clamp
        }

    let private diffuseSampler =
        sampler2dArray {
            texture uniform?DiffuseColorTexture
            filter Filter.MinMagLinear
            addressU WrapMode.Clamp
            addressV WrapMode.Clamp
        }

    let diffuseTextureArray (v : Effects.Vertex) =
        fragment {
            let slice = uniform.DiffuseColorTextureSlice
            return diffuseSampler.Sample(v.tc, slice)
        }

    let diffuseArrayOfTextures (v : Effects.Vertex) =
        fragment {
            let slice = uniform.DiffuseColorTextureSlice
            return diffuseSamplerArray.[slice].Sample(v.tc)
        }

module Texture =

    let private rand = RandomSystem()

    let generatePix (size : V2i) =
        let pi = PixImage<float32>(Col.Format.RGBA, size)
        let data = pi.GetMatrix<C4f>()
        data.SetByCoord(ignore >> rand.UniformC3f().ToC4f) |> ignore
        pi

    let generate (size : V2i) =
        PixTexture2d(generatePix size, false) :> ITexture

type AdaptiveTexture2DArray(runtime : ITextureRuntime, size : V2i, count : aval<int>) =
    inherit AdaptiveResource<IBackendTexture>()

    let mutable handle : Option<IBackendTexture * int> = None

    let createAndUploadTexture (count : int) =
        Log.warn "Create texture"
        let t = runtime.CreateTexture2DArray(size, TextureFormat.Rgba32f, 1, 1, count)
        for slice in 0.. count - 1 do
            let tex = Texture.generatePix size
            t.Upload(tex, 0, slice)

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
            backend Backend.Vulkan
            display Display.Mono
            debug true
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
                    DefaultSemantic.DiffuseColorCoordinates, [|V2f.OO; V2f.IO; V2f.II; V2f.OI|] :> Array
                ]
        )

    let rand = RandomSystem()
    let runtime = win.Runtime

    let size = V2i 64
    let slice = AVal.init 0
    let count = AVal.init 12
    let onoff = AVal.init true

    // Setting this to true results in an array of samplers with
    // individual textures to be created. These do not need to be the same size
    // as is the case with a real texture array. However, the number of textures
    // must be known in advance, and the maximum supported number is usally
    // much smaller than for texture arrays.
    let useArrayOfSamplers = true

    let applyTexture =
        if useArrayOfSamplers then
            let textures =
                count |> AVal.map (fun count ->
                    Array.init Shader.MaxTextureSlices (fun i ->
                        if i < count then
                            Texture.generate size
                        else
                            NullTexture()
                    )
                )

            // Set the adaptive array aval<#ITexture[]>, alternatively you can also
            // bind each indiviual texture to DiffuseColorTexture0, DiffuseColorTexture1, and so forth.
            // Note: The Vulkan backend also supports amap<int, aval<ITexture>> for a more convenient sparse mapping.
            Sg.textureArray DefaultSemantic.DiffuseColorTexture textures

        else
            let texture = AdaptiveTexture2DArray(runtime, size, count)
            Sg.diffuseTexture texture

    let sg =
        onoff |> AVal.map (fun o ->
            if not o then Sg.empty
            else
                quadGeometry
                |> Sg.ofIndexedGeometry
                |> Sg.effect [
                    DefaultSurfaces.trafo |> toEffect
                    DefaultSurfaces.vertexColor |> toEffect

                    if useArrayOfSamplers then
                        Shader.diffuseArrayOfTextures |> toEffect
                    else
                        Shader.diffuseTextureArray |> toEffect
                ]
                |> Sg.uniform Semantic.DiffuseColorTextureSlice slice
                |> applyTexture
        ) |> Sg.dynamic

    win.Keyboard.DownWithRepeats.Values.Add (fun k ->
        match k with
        | Keys.OemPlus ->  transact(fun _ -> let nv = min (slice.Value + 1) (count.Value-1) in Log.line "slice %d (%d)" nv (count.Value-1); slice.Value <- nv)
        | Keys.OemMinus -> transact(fun _ -> let nv = max (slice.Value - 1) 0 in Log.line "slice %d (%d)" nv (count.Value-1); slice.Value <- nv)
        | Keys.Space -> transact(fun _ -> let nv = rand.UniformInt(Shader.MaxTextureSlices - 1) + 1 in Log.line "new count: %d" nv; count.Value <- nv; slice.Value <- 0)
        | Keys.Enter -> transact(fun _ -> onoff.Value <- not onoff.Value)
        | _ -> ()
    )

    win.Scene <- sg
    win.Run()

    0
