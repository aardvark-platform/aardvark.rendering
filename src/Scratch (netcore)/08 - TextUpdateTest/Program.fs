open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Rendering.Text
open Aardvark.Application
open System.Threading

open Aardvark.Base.Ag
// This example illustrates how to render a simple triangle using aardvark.

module IncrementalExtensions = 
    open System.Threading

    let throttled (name : string) (ms : int) (f : 'a -> unit) (m : aval<'a>) : aval<'a> = 
        let v = MVar.empty()
        let r = AVal.init (AVal.force m)
        let f () = 
            while true do
                let () = MVar.take v
                let v = AVal.force m
                f v 
                transact (fun _ -> r.Value <- v)
                Thread.Sleep ms

        let d = m.AddMarkingCallback(fun _ -> MVar.put v ())

        let t = Thread(ThreadStart f)
        t.IsBackground <- true
        t.Name <- name
        t.Start()
        r :> aval<_>


type WtfRuntimeApplicator(r : IRuntime, child : ISg) =
    inherit Sg.AbstractApplicator(child)
    member x.Runtime = r

[<Rule>]
type RuntimeSem() =
    member x.Runtime(w : WtfRuntimeApplicator, scope : Ag.Scope) = w.Child?Runtime <- w.Runtime

module Sg = 
    let applyRuntime (r : IRuntime) (sg : ISg) = WtfRuntimeApplicator(r,sg) :> ISg

[<EntryPoint>]
let main argv = 

    Aardvark.Rendering.Vulkan.Config.showRecompile <- false
    
    
    Aardvark.Init()

    // window { ... } is similar to show { ... } but instead
    // of directly showing the window we get the window-instance
    // and may show it later.
    let win =
        window {
            backend Backend.GL
            display Display.Mono
            debug true
            samples 1
        }

    let runtime = win.Runtime

    let box = Box3d(-V3d.III, V3d.III)
    let color = C4b.Red


    let cfg = { Aardvark.Rendering.Text.TextConfig.Default with color = C4b.Red; align = TextAlignment.Center }
    let inputText = AVal.init "aaaa"

    let text = inputText |> AVal.map (fun t -> cfg.Layout t)


    let size = V2i(512,512)
    let color = runtime.CreateTexture2D(size, TextureFormat.Rgba8, 1, 1)
    let prepare = 

        let depth = runtime.CreateRenderbuffer(size, TextureFormat.Depth24Stencil8, 1)

        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8
            ]

        let fbo = 
            runtime.CreateFramebuffer(
                signature, 
                Map.ofList [
                    DefaultSemantic.Colors, color.GetOutputView()
                    DefaultSemantic.DepthStencil, (depth :> IFramebufferOutput)
                ]
            )
        let mutable old : Option<IRenderTask> = None
        let t = AVal.init (text |> AVal.force) |> Sg.shape |> Sg.applyRuntime runtime |> Sg.compile runtime signature
        fun (s : ShapeList) ->
            let o = old
            Log.startTimed "compile text"
            let s = Sg.shape (AVal.constant s) 
            s?Runtime <- runtime
            let task = 
                s 
                |> Sg.applyRuntime runtime 
                |> Sg.scale 0.1
                |> Sg.viewTrafo (AVal.constant Trafo3d.Identity)
                |> Sg.projTrafo (Frustum.ortho (Box3d.Unit) |> Frustum.projTrafo |> AVal.constant)
                |> Sg.compile runtime signature
            old <- Some task
            task.Run(RenderToken.Empty, fbo)
            match o with
                | None -> ()
                | Some o -> o.Dispose()
            Log.stop()

    let prepare () = ()


    let text = win.Time  |> AVal.map (fun _ -> 
            Log.startTimed "layout"
            let s = System.Guid.NewGuid() |> string 
            let r = cfg.Layout s
            Log.stop()
            r
          )

    //let text = IncrementalExtensions.throttled "ttext" 400000 prepare text
    let overlay = text |> Sg.shape // |> Sg.scale 0.02

    let changeThread =
        let f () =
            while true do
                Thread.Sleep 100
                transact (fun _ -> inputText.Value <- System.Guid.NewGuid() |> string)
        let t = Thread(ThreadStart f)
        t.IsBackground <- true
        t.Start()
        t
    
    let sw = System.Diagnostics.Stopwatch.StartNew()
    let mutable last = sw.Elapsed.TotalSeconds
    let t = 
        win.Time |> AVal.map (fun _ -> 
            let took = (sw.Elapsed.TotalSeconds - last) * 1000.0
            if took > 6.0 then printfn "last frame took: %A" took
            last <- sw.Elapsed.TotalSeconds
            Trafo3d.RotationZ (sw.Elapsed.TotalSeconds * 0.1)
        )

    let sg = 
        // create a red box with a simple shader
        //Sg.box (AVal.constant color) (AVal.constant box)
            //Sg.fullScreenQuad
            //|> Sg.diffuseTexture (color :> ITexture |> AVal.constant)
            overlay
            |> Sg.trafo t
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.constantColor C4f.Red
                do! DefaultSurfaces.diffuseTexture
            }

    
    // show the window
    win.Scene <- sg
    win.Run()

    0
