namespace Scratch

open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application

module ChangeBufferHandle =
    

    [<Demo("ChangeBufferHandle")>]
    let run() =
        let runtime = App.Runtime

        let b0 = runtime.PrepareBuffer (ArrayBuffer [| V3f.OOO; V3f.IOO; V3f.IIO; V3f.OIO |], BufferUsage.Default) :> IBuffer
        let b1 = runtime.PrepareBuffer (ArrayBuffer [| V3f.OOO; 2.0f * V3f.IOO; 2.0f * V3f.IIO; 2.0f * V3f.OIO |], BufferUsage.Default) :> IBuffer

        let t0 = runtime.PrepareTexture (FileTexture(@"C:\Users\Schorsch\Development\WorkDirectory\pattern.jpg",true)) :> ITexture
        let t1 = runtime.PrepareTexture (FileTexture(@"C:\Users\Schorsch\Development\WorkDirectory\grass_color.jpg",true)) :> ITexture

        let b = Mod.init b0
        let t = Mod.init t0

        let call =
            DrawCallInfo(
                FaceVertexCount = 6,
                InstanceCount = 1
            )

        App.Keyboard.DownWithRepeats.Values.Add(fun k ->
            if k = Keys.C then
                transact (fun () ->
                    t.Value <- 
                        if t.Value = t0 then t1
                        else t0
                )
            elif k = Keys.X then
                transact (fun () ->
                    b.Value <- 
                        if b.Value = b0 then b1
                        else b0
                )
        )

        Sg.render IndexedGeometryMode.TriangleList call
            |> Sg.vertexBuffer DefaultSemantic.Positions (BufferView(b, typeof<V3f>))
            |> Sg.vertexAttribute DefaultSemantic.DiffuseColorCoordinates (Mod.constant [| V2f.OO; V2f.IO; V2f.II; V2f.OI |])
            |> Sg.index (Mod.constant [| 0;1;2; 0;2;3 |])
            |> Sg.diffuseTexture t
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.constantColor C4f.White
                do! DefaultSurfaces.diffuseTexture
               }
        
