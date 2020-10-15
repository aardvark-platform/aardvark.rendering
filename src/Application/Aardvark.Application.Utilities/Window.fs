namespace Aardvark.Application



open System
open Aardvark.Base
open FSharp.Data.Adaptive

open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.Slim
open FSharp.Data.Adaptive.Operators

open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open Aardvark.Rendering.Text
open Aardvark.Application.OpenVR
open Aardvark.SceneGraph.Semantics

[<RequireQualifiedAccess>]
type Backend =
    | GL 
    | Vulkan 
    
[<RequireQualifiedAccess>]
type Display =
    | Mono
    | Stereo
    | OpenVR

type DebugVerbosity =
    | None = 0
    | Error = 1
    | Warning = 2
    | Information = 3
    | Debug = 4

type DeviceKind =
    | None          = 0x0
    | Unknown       = 0x1
    | Dedicated     = 0x2
    | Integrated    = 0x4
    | Any           = 0x7

type RenderConfig =
    {
        app             : Option<IApplication>
        backend         : Backend
        debug           : DebugVerbosity
        samples         : int
        display         : Display
        scene           : ISg
        deviceKind      : DeviceKind
        initialCamera   : Option<CameraView>
        initialSpeed    : Option<float>
    }

[<AutoOpen>]
module ``FShade Extensions`` =
    open FShade

    type LightDirAttribute() = inherit FShade.SemanticAttribute("LightDirection")
    type CamDirAttribute() = inherit FShade.SemanticAttribute("CameraDirection")
    type SpecularColorAttribute() = inherit FShade.SemanticAttribute("SpecularColor")

    type UniformScope with
        member x.AmbientColor : V4d = x?Material?AmbientColor
        member x.DiffuseColor : V4d = x?Material?DiffuseColor
        member x.EmissiveColor : V4d = x?Material?EmissiveColor
        member x.ReflectiveColor : V4d = x?Material?ReflectiveColor
        member x.SpecularColor : V4d = x?Material?SpecularColor
        member x.Shininess : float = x?Material?Shininess
        member x.BumpScale : float = x?Material?BumpScale



module Utilities =

    module private Shader =
        open FShade

        let stereoTexture =
            sampler2dArray {
                texture uniform?DiffuseColorTexture
                filter Filter.MinMagLinear
                addressU WrapMode.Wrap
                addressV WrapMode.Wrap
            }

        let renderStereo (v : Effects.Vertex) =
            fragment {
                let index =
                    if v.tc.X > 0.5 then 1
                    else 0

                let tc =
                    if v.tc.X > 0.5 then V2d((v.tc.X - 0.5) * 2.0, v.tc.Y)
                    else V2d(v.tc.X * 2.0, v.tc.Y)

                return stereoTexture.SampleLevel(tc, index, uniform?Dependent)
            }


    type ISimpleRenderWindow =
        inherit IDisposable

        abstract member Runtime : IRuntime
        abstract member Sizes : aval<V2i>
        abstract member Samples : int
        abstract member Time : aval<DateTime>
        
        abstract member IsVR : bool
        abstract member Controllers : list<VrDevice> 
        abstract member Keyboard : IKeyboard
        abstract member Mouse : IMouse

        abstract member View : aval<Trafo3d[]>
        abstract member Proj : aval<Trafo3d[]>

        abstract member Scene : ISg with get, set
        abstract member Run : ?preventDisposal:bool -> unit


    [<AbstractClass>]
    type private SimpleRenderWindow(win : IRenderWindow, view : aval<Trafo3d[]>, proj : aval<Trafo3d[]>) =
        let mutable scene = Sg.empty

        let controllers, isVr = 
            match win with
                | :? Aardvark.Application.OpenVR.VulkanVRApplicationLayered as w -> 
                    w.System.Controllers |> Seq.toList, true
                | _ -> [], false
        
        abstract member WrapSg : IRenderWindow * ISg -> ISg
        default x.WrapSg(_,s) = s


        abstract member Compile : IRenderWindow * ISg -> IRenderTask
        abstract member Release : unit -> unit
        default x.Release() = ()

        member x.Dispose() =
            win.TryDispose() |> ignore // Disposes OpenTK.GameWindow and OpenTK.GraphicsContext (depending on backend)
            x.Release() // Application.Dispose / Runtime.Dispose + Context.Dispose

        member x.Runtime = win.Runtime
        member x.Sizes = win.Sizes
        member x.Samples = win.Samples
        member x.Time = win.Time
        member x.Keyboard = win.Keyboard
        member x.Mouse = win.Mouse

        member x.Run(preventDisposal) = 
            win.Run()
            match preventDisposal with
                | Some t when t = true -> ()
                | _ -> x.Dispose()

        member x.RunWithoutDisposing() = 
            win.Run()

        member x.Scene
            with get() = scene
            and set sg = 
                scene <- sg
                match win with
                    | :? OpenVR.VulkanVRApplicationLayered as win ->
                        let sg =  x.WrapSg(win, sg)
                        win.RenderTask <- RuntimeCommand.Render(sg.RenderObjects(Ag.Scope.Root))
                    | _ ->
                        let task = x.Compile(win, sg)
                        win.RenderTask <- task
        
        interface ISimpleRenderWindow with
            member x.Runtime = x.Runtime
            member x.Sizes = x.Sizes
            member x.Samples = x.Samples
            member x.Time = x.Time
            member x.Keyboard = x.Keyboard
            member x.IsVR = isVr
            member x.Controllers = controllers
            member x.Mouse = x.Mouse
            member x.Run(?preventDisposal) = x.Run(preventDisposal)
            
            member x.View = view
            member x.Proj = proj

            member x.Scene
                with get() = x.Scene
                and set sg = x.Scene <- sg

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    let private hookSg (win : IRenderControl) (sg : ISg) =
        let fillMode = AVal.init FillMode.Fill
        let cullMode = AVal.init CullMode.None

        let status = AVal.init ""
        
        win.Keyboard.KeyDown(Aardvark.Application.Keys.X).Values.Add (fun () ->
            if AVal.force win.Keyboard.Alt then
                transact (fun () ->
                    let newFillMode = 
                        match fillMode.Value with
                            | FillMode.Fill -> FillMode.Line
                            | _ -> FillMode.Fill

                    status.Value <- sprintf "fill: %A" newFillMode
                    fillMode.Value <- newFillMode
                )
            ()
        )

        win.Keyboard.KeyDown(Aardvark.Application.Keys.Y).Values.Add (fun () ->
            if AVal.force win.Keyboard.Alt then
                transact (fun () ->
                    let newCull = 
                        match cullMode.Value with
                            | CullMode.None -> CullMode.Back
                            | CullMode.Back -> CullMode.Front
                            | _ -> CullMode.None
                    status.Value <- sprintf "cull: %A" newCull
                    cullMode.Value <- newCull
                )
            ()
        )

        let teaser = 
            "press 'H' for help"

        let helpText = 
            String.concat "\r\n" [
                "Key Bindings:"
                "  H                   toggle this Help"
                "  Alt+X               toggle FillMode"
                "  Alt+Y               toggle CullMode"
                "  WSAD                move camera"
                "  Ctrl+Shift+Return   toggle Fullscreen"
                ""
                "Navigation:"
                "  Left Mouse    look around"
                "  Right Mouse   zoom"
                "  Middle Mouse  pan"
                "  Scroll        zoom"
                ""
            ]

        let status =
            adaptive {
                let! cull = cullMode
                let! fill = fillMode

                return 
                    String.concat "\r\n" [
                        "Status:"
                        sprintf "  CullMode: %A" cull
                        sprintf "  FillMode: %A" fill
                    ]
            }


        let overlay =
            let help = status |> AVal.map (fun s -> helpText + "\r\n" + s)

            let showHelp = AVal.init false

            win.Keyboard.KeyDown(Aardvark.Application.Keys.H).Values.Add (fun () ->
                transact (fun () -> showHelp.Value <- not showHelp.Value)
            )

            let text = showHelp |> AVal.bind (function true -> help | false -> AVal.constant teaser)


        

            let trafo = 
                win.Sizes |> AVal.map (fun s -> 
                    let border = V2d(20.0, 10.0) / V2d s
                    let pixels = 30.0 / float s.Y
                    Trafo3d.Scale(pixels) *
                    Trafo3d.Scale(float s.Y / float s.X, 1.0, 1.0) *
                    Trafo3d.Translation(-1.0 + border.X, 1.0 - border.Y - pixels, -1.0)
                )

            let chars =
                seq {
                    for c in 0 .. 255 do yield char c
                }

            let font = FontSquirrel.Hack.Regular
            use __ = win.Runtime.ContextLock
            win.Runtime.PrepareGlyphs(font, chars)
            Sg.text font C4b.White text
                |> Sg.trafo trafo
                |> Sg.uniform "ViewTrafo" (AVal.constant Trafo3d.Identity)
                |> Sg.uniform "ProjTrafo" (AVal.constant Trafo3d.Identity)
                |> Sg.viewTrafo (AVal.constant Trafo3d.Identity)
                |> Sg.projTrafo (AVal.constant Trafo3d.Identity)
   

        let sg = sg |> Sg.fillMode fillMode |> Sg.cullMode cullMode
        sg, overlay

    module Sg =
        let simpleOverlay (win : IRenderControl) (sg : ISg) =

            let initialView =CameraView.lookAt (V3d(6.0, 6.0, 6.0)) V3d.Zero V3d.OOI
            let view =
                initialView
                    |> DefaultCameraController.control win.Mouse win.Keyboard win.Time
                    |> AVal.map CameraView.viewTrafo

            let proj =
                win.Sizes 
                    |> AVal.map (fun s -> Frustum.perspective 60.0 0.1 1000.0 (float s.X / float s.Y))
                    |> AVal.map Frustum.projTrafo

            let (sg, overlay) = hookSg win sg
            let sg = sg |> Sg.viewTrafo view |> Sg.projTrafo proj
            Sg.ofList [sg; overlay]

    let private toMessageSeverity =
        LookupTable.lookupTable [
            DebugVerbosity.Debug, MessageSeverity.Debug
            DebugVerbosity.Information, MessageSeverity.Information
            DebugVerbosity.Warning, MessageSeverity.Warning
            DebugVerbosity.Error, MessageSeverity.Error
        ]

    let c (debug : bool) (backend : Backend)  =
        match backend with
            | Backend.GL -> new OpenGlApplication(debug) :> IApplication
            | Backend.Vulkan -> new VulkanApplication(debug) :> IApplication

    let private chooseDevice (cfg : RenderConfig) (devices : list<PhysicalDevice>) =
        let filtered =
            devices |> List.filter (fun d ->
                let kind = 
                    match d.Type with
                        | VkPhysicalDeviceType.DiscreteGpu -> 
                            DeviceKind.Dedicated
                        | VkPhysicalDeviceType.IntegratedGpu
                        | VkPhysicalDeviceType.Cpu ->
                            DeviceKind.Integrated
                        | _ ->
                            DeviceKind.Unknown
                            
                kind &&& cfg.deviceKind <> DeviceKind.None
            )

        match filtered with
            | [f] -> ConsoleDeviceChooser.run' (Some f) devices
            | _ -> ConsoleDeviceChooser.run devices
                

    let createApplication (cfg : RenderConfig) =
        match cfg.app with
        | Some app -> 
            app
        | None -> 
            let enableDebug = cfg.debug <> DebugVerbosity.None
            match cfg.backend with
                | Backend.GL -> 
                    new OpenGlApplication(cfg.deviceKind = DeviceKind.Dedicated, enableDebug) :> IApplication
                | Backend.Vulkan -> 
                    let app = new VulkanApplication(enableDebug, chooseDevice cfg) 
                    if enableDebug then
                        app.Runtime.DebugVerbosity <- toMessageSeverity cfg.debug
                    app :> IApplication

    let createGameWindow (app : IApplication) (cfg : RenderConfig) =
        match app with
        | :? OpenGlApplication as app -> app.CreateGameWindow(cfg.samples) :> IRenderWindow
        | :? VulkanApplication as app -> app.CreateGameWindow(cfg.samples) :> IRenderWindow
        | _ -> failwithf "unknown app type: %A" app

    let private createMonoScreen (cfg : RenderConfig) =
        let app = createApplication cfg

        let win = createGameWindow app cfg
        
        let initialView = 
            match cfg.initialCamera with
                | None -> CameraView.lookAt (V3d(6.0, 6.0, 6.0)) V3d.Zero V3d.OOI
                | Some c -> c

        let speed = cfg.initialSpeed |> Option.defaultValue 1.0

        let view =
            initialView
                |> DefaultCameraController.controlExt speed win.Mouse win.Keyboard win.Time
                |> AVal.map CameraView.viewTrafo

        let proj =
            win.Sizes 
                |> AVal.map (fun s -> Frustum.perspective 60.0 0.1 1000.0 (float s.X / float s.Y))
                |> AVal.map Frustum.projTrafo

        { new SimpleRenderWindow(win, view |> AVal.map Array.singleton, proj |> AVal.map Array.singleton) with
            override x.Compile(win, sg) =
                let sg, overlay = sg |> hookSg win
                sg
                |> Sg.viewTrafo view
                |> Sg.projTrafo proj
                |> Sg.andAlso overlay
                |> Sg.compile win.Runtime win.FramebufferSignature

            override x.Release() = 
               
                app.Dispose()

        } :> ISimpleRenderWindow

    let private createStereoScreen (cfg : RenderConfig) =
        let app = createApplication cfg
        let win = createGameWindow app cfg
        let runtime = app.Runtime

        let samples = cfg.samples

        
        let monoSignature =
            runtime.CreateFramebufferSignature(
                SymDict.ofList [
                    DefaultSemantic.Colors, { format = RenderbufferFormat.Rgba8; samples = samples }
                    DefaultSemantic.Depth, { format = RenderbufferFormat.Depth24Stencil8; samples = samples }
                ],
                1, 
                Set.empty
            )  

        let signature =
            runtime.CreateFramebufferSignature(
                SymDict.ofList [
                    DefaultSemantic.Colors, { format = RenderbufferFormat.Rgba8; samples = samples }
                    DefaultSemantic.Depth, { format = RenderbufferFormat.Depth24Stencil8; samples = samples }
                ],
                2, 
                Set.ofList [
                    "ProjTrafo"; 
                    "ViewTrafo"; 
                    "ModelViewTrafo"; 
                    "ViewProjTrafo"; 
                    "ModelViewProjTrafo"
                    
                    "ProjTrafoInv"; 
                    "ViewTrafoInv"; 
                    "ModelViewTrafoInv"; 
                    "ViewProjTrafoInv"; 
                    "ModelViewProjTrafoInv"
                ]
            )  

        let s = win.Sizes |> AVal.map (fun s -> s / V2i(2,1))

        let colors =
            runtime.CreateTextureArray(TextureFormat.Rgba8, samples, s, ~~2)

        let depth =
            runtime.CreateTextureArray(TextureFormat.Depth24Stencil8, samples, s, ~~2)

        let resolved =
            runtime.CreateTextureArray(TextureFormat.Rgba8, 1, s, ~~2)

        let framebuffer =
            runtime.CreateFramebuffer(signature, [
                DefaultSemantic.Colors, runtime.CreateTextureAttachment(colors)
                DefaultSemantic.Depth, runtime.CreateTextureAttachment(depth)
            ])

        let initialView = 
            match cfg.initialCamera with
                | None -> CameraView.lookAt (V3d(6.0, 6.0, 6.0)) V3d.Zero V3d.OOI
                | Some c -> c

        let speed = cfg.initialSpeed |> Option.defaultValue 1.0

        let view = 
            initialView
                |> DefaultCameraController.controlExt speed win.Mouse win.Keyboard win.Time
                |> AVal.map CameraView.viewTrafo

        let near = 0.1
        let far = 1000.0

        let views =
            view |> AVal.map (fun view ->
                [| 
                    view * Trafo3d.Translation(0.05, 0.0, 0.0)
                    view * Trafo3d.Translation(-0.05, 0.0, 0.0)
                |]
            )

        let projs =
            // taken from oculus rift
            let outer = 1.0537801252809621805875367233154
            let inner = 0.77567951104961310377955052031392

            win.Sizes |> AVal.map (fun size ->
                let aspect = float size.X / float size.Y 
                let y = tan (120.0 * Constant.RadiansPerDegree / 2.0) / aspect //(outer + inner) / (2.0 * aspect)

                [|
                    { left = -outer * near; right = inner * near; top = y * near; bottom = -y * near; near = near; far = far; isOrtho = false } |> Frustum.projTrafo 
                    { left = -inner * near; right = outer * near; top = y * near; bottom = -y * near; near = near; far = far; isOrtho = false } |> Frustum.projTrafo 
                |]
            )

        let compile (config : RenderConfig) (sg : ISg) =

            framebuffer.Acquire()
            resolved.Acquire()

            let sg, overlay = sg |> hookSg win

            let stereoTask =
                sg
                |> Sg.uniform "ViewTrafo" views
                |> Sg.uniform "ProjTrafo" projs
                |> Sg.viewTrafo view
                |> Sg.uniform "CameraLocation" (view |> AVal.map (fun t -> t.Backward.C3.XYZ))
                |> Sg.uniform "LightLocation" (view |> AVal.map (fun t -> t.Backward.C3.XYZ))
                |> Sg.compile runtime signature

            let clearTask =
                runtime.CompileClear(signature, ~~C4f.Black, ~~1.0)

            let task =
                Sg.fullScreenQuad
                    |> Sg.uniform "Dependent" (AVal.constant 0.0)
                    |> Sg.diffuseTexture resolved
                    |> Sg.shader {
                        do! Shader.renderStereo
                    }
                    |> Sg.andAlso overlay
                    |> Sg.compile runtime win.FramebufferSignature

            let dummy =
                { new AbstractRenderTask() with
                    member x.FramebufferSignature = Some win.FramebufferSignature
                    member x.Runtime = Some win.Runtime
                    member x.PerformUpdate (_,_) = ()
                    member x.Perform (t,_,_,q) = 
                        let fbo = framebuffer.GetValue t
                        let output = OutputDescription.ofFramebuffer fbo

                        let c = colors.GetValue t |> unbox<IBackendTexture>
                        let r = resolved.GetValue t |> unbox<IBackendTexture>

                        q.Begin()
                        clearTask.Run(t, RenderToken.Empty, output, q)
                        stereoTask.Run(t, RenderToken.Empty, output, q)
                        q.End()

                        runtime.Copy(c, 0, 0, r, 0, 0, 2, 1)

                    member x.Release() =
                        stereoTask.Dispose()
                        clearTask.Dispose()
                        framebuffer.Release()
                        resolved.Release()
                    member x.Use f = f()
                }

            RenderTask.ofList [dummy; task]


        let res =
            { new SimpleRenderWindow(win, views, projs) with
                override x.Compile(win, sg) =
                    compile cfg sg

                override x.Release() =
                    //win.Dispose() <- todo make this disposable
                    runtime.DeleteFramebufferSignature signature
        
                    app.Dispose()
        

            } :> ISimpleRenderWindow

        res

    let private createOpenVR (cfg : RenderConfig) =
        match cfg.backend with
            | Backend.Vulkan ->
                let enableDebug = cfg.debug <> DebugVerbosity.None
                let app = new VulkanVRApplicationLayered(cfg.samples, enableDebug)
                if enableDebug then
                    app.Runtime.DebugVerbosity <- toMessageSeverity cfg.debug

                let hmdLocation = app.Hmd.MotionState.Pose |> AVal.map (fun t -> t.Forward.C3.XYZ)


                let stencilTest =
                    { StencilMode.None with
                        Comparison = ComparisonFunction.Equal }

                { new SimpleRenderWindow(app, app.Info.viewTrafos, app.Info.projTrafos) with

                    override x.WrapSg(win, sg) =
                        sg
                        |> Sg.stencilMode' stencilTest
                        |> Sg.uniform "ViewTrafo" app.Info.viewTrafos
                        |> Sg.uniform "ProjTrafo" app.Info.projTrafos
                        |> Sg.uniform "CameraLocation" hmdLocation
                        |> Sg.uniform "LightLocation" hmdLocation

                    override x.Compile(win, sg) =
                        sg
                        |> Sg.stencilMode' stencilTest
                        |> Sg.uniform "ViewTrafo" app.Info.viewTrafos
                        |> Sg.uniform "ProjTrafo" app.Info.projTrafos
                        |> Sg.uniform "CameraLocation" hmdLocation
                        |> Sg.uniform "LightLocation" hmdLocation
                        |> Sg.compile app.Runtime app.FramebufferSignature
                } :> ISimpleRenderWindow

            | Backend.GL -> 
                let enableDebug = cfg.debug <> DebugVerbosity.None
                let app = new OpenGlVRApplicationLayered(cfg.samples, enableDebug)

                let hmdLocation = app.Hmd.MotionState.Pose |> AVal.map (fun t -> t.Forward.C3.XYZ)


                let stencilTest =
                    { StencilMode.None with
                        Comparison = ComparisonFunction.Equal }

                { new SimpleRenderWindow(app, app.Info.viewTrafos, app.Info.projTrafos) with

                    override x.WrapSg(win, sg) =
                        sg
                        |> Sg.stencilMode' stencilTest
                        |> Sg.uniform "ViewTrafo" app.Info.viewTrafos
                        |> Sg.uniform "ProjTrafo" app.Info.projTrafos
                        |> Sg.uniform "CameraLocation" hmdLocation
                        |> Sg.uniform "LightLocation" hmdLocation

                    override x.Compile(win, sg) =
                        sg
                        |> Sg.stencilMode' stencilTest
                        |> Sg.uniform "ViewTrafo" app.Info.viewTrafos
                        |> Sg.uniform "ProjTrafo" app.Info.projTrafos
                        |> Sg.uniform "CameraLocation" hmdLocation
                        |> Sg.uniform "LightLocation" hmdLocation
                        |> Sg.compile app.Runtime app.FramebufferSignature
                } :> ISimpleRenderWindow
                


    let createWindow (cfg : RenderConfig) =
        match cfg.display with
            | Display.Mono -> createMonoScreen cfg
            | Display.Stereo -> createStereoScreen cfg
            | Display.OpenVR -> createOpenVR cfg

    let runConfig (cfg : RenderConfig) =
        let win = createWindow cfg
        win.Scene <- cfg.scene
        win.Run()

    let run (display : Display) (backend : Backend) (scene : ISg) =
        runConfig {
            app = None
            scene = scene
            display = display
            backend = backend
            debug = DebugVerbosity.Warning
            samples = 8
            deviceKind = DeviceKind.Dedicated
            initialCamera = None
            initialSpeed  = None
        }

[<AutoOpen>]
module ``Render Utilities`` =
    open System.Text.RegularExpressions

    let private backendRX = Regex @"(--backend|-b)[ \t]*(?<name>(?i)(vulkan|gl|opengl|vk))"
    let private displayRX = Regex @"(--display|-d)[ \t]*(?<name>(?i)(stereo|mono|vr|openvr))"
    let private samplesRX = Regex @"(--samples|-s)[ \t]*(?<value>[0-9]+)"
    let private debugRX = Regex @"(--debug|-g)[ \t]*(?<state>(?i)(on|off)?)"
    let private dedicatedRX = Regex @"(--dedicated|--integrated)"

    let private cliOverrides (cfg : RenderConfig) =
        let args = System.Environment.GetCommandLineArgs() |> Array.skip 1 |> String.concat " "
        let mutable cfg = cfg
        let m = backendRX.Match args
        if m.Success then 
            match m.Groups.["name"].Value.ToLower() with
                | "vulkan" | "vk" -> 
                    Log.line "[Application] using Vulkan"
                    cfg <- { cfg with backend = Backend.Vulkan }
                | "gl" | "opengl" -> 
                    Log.line "[Application] using GL"
                    cfg <- { cfg with backend = Backend.GL }
                | v -> 
                    Log.warn "[Application] bad backend: %s" v

        let m = displayRX.Match args
        if m.Success then 
            match m.Groups.["name"].Value.ToLower() with
                | "stereo" -> 
                    Log.line "[Application] using Stereo display"
                    cfg <- { cfg with display = Display.Stereo }
                | "mono" -> 
                    Log.line "[Application] using Mono display"
                    cfg <- { cfg with display = Display.Mono }
                | "vr" | "openvr" -> 
                    Log.line "[Application] using OpenVR display"
                    cfg <- { cfg with display = Display.OpenVR }
                | v -> 
                    Log.warn "bad display: %s" v

        let m = samplesRX.Match args
        if m.Success then
            let samples = Int32.Parse(m.Groups.["value"].Value)
            Log.line "[Application] using %d samples" samples
            cfg <- { cfg with samples = samples }

        let m = debugRX.Match args
        if m.Success then    
            let g = m.Groups.["state"]
            let state =
                if g.Success then 
                    match g.Value with
                        | "off" -> false
                        | _ -> true
                else
                    true
            Log.line "[Application] debug: %A" state
            cfg <- { cfg with debug = if state then DebugVerbosity.Information else DebugVerbosity.None }

        let m = dedicatedRX.Match args
        if m.Success then
            let deviceKind =
                match m.Value with
                    | "--dedicated" -> DeviceKind.Dedicated
                    | "--integrated" -> DeviceKind.Integrated
                    | _ -> DeviceKind.Dedicated
            Log.line "[Application] device: %A" deviceKind
            cfg <- { cfg with deviceKind = deviceKind }

        cfg

    type ShowBuilder() =
        member x.Yield(()) =
            {
                app = None
                backend = Backend.Vulkan
                debug = DebugVerbosity.Warning
                samples = 8
                display = Display.Mono
                scene = Sg.empty
                deviceKind = DeviceKind.Dedicated
                initialCamera = None
                initialSpeed = None
            }
            
        [<CustomOperation("app")>]
        member x.Application(s : RenderConfig, a : IApplication) =
            { s with app = Some a }

        [<CustomOperation("backend")>]
        member x.Backend(s : RenderConfig, b : Backend) =
            { s with backend = b }

        [<CustomOperation("debug")>]
        member x.Debug(s : RenderConfig, d : bool) =
            { s with debug = if d then DebugVerbosity.Warning else DebugVerbosity.None }
            
        [<CustomOperation("device")>]
        member x.DeviceKind(s : RenderConfig, k : DeviceKind) =
            { s with deviceKind = k }
            
        [<CustomOperation("verbosity")>]
        member x.Verbosity(s : RenderConfig, d : DebugVerbosity) =
            { s with debug = d }

        [<CustomOperation("samples")>]
        member x.Samples(state : RenderConfig, s : int) =
            { state with samples = s }

        [<CustomOperation("display")>]
        member x.Display(s : RenderConfig, d : Display) =
            { s with display = d }

        [<CustomOperation("scene")>]
        member x.Scene(state : RenderConfig, s : ISg) =
            { state with scene = s }

        [<CustomOperation("initialCamera")>]
        member x.InitialCamera(state : RenderConfig, c : CameraView) =
            { state with initialCamera = Some c }
            
        [<CustomOperation("initialSpeed")>]
        member x.InitialSpeed(state : RenderConfig, s : float) =
            { state with initialSpeed = Some s }

        member x.Run(cfg : RenderConfig) =
            Utilities.runConfig (cliOverrides cfg)

    type WindowBuilder() =

        member x.Yield(()) =
            {
                app = None
                backend = Backend.GL
                debug = DebugVerbosity.Warning
                samples = 8
                deviceKind = DeviceKind.Dedicated
                display = Display.Mono
                scene = Sg.empty
                initialCamera = None
                initialSpeed = None
            }
            
        [<CustomOperation("app")>]
        member x.Application(s : RenderConfig, a : IApplication) =
            { s with app = Some a }

        [<CustomOperation("backend")>]
        member x.Backend(s : RenderConfig, b : Backend) =
            { s with backend = b }
            
        [<CustomOperation("debug")>]
        member x.Debug(s : RenderConfig, d : bool) =
            { s with debug = if d then DebugVerbosity.Warning else DebugVerbosity.None }
            
        [<CustomOperation("device")>]
        member x.DeviceKind(s : RenderConfig, k : DeviceKind) =
            { s with deviceKind = k }

        [<CustomOperation("verbosity")>]
        member x.Verbosity(s : RenderConfig, d : DebugVerbosity) =
            { s with debug = d }

        [<CustomOperation("samples")>]
        member x.Samples(state : RenderConfig, s : int) =
            { state with samples = s }

        [<CustomOperation("display")>]
        member x.Display(s : RenderConfig, d : Display) =
            { s with display = d }

        [<CustomOperation("initialCamera")>]
        member x.InitialCamera(state : RenderConfig, c : CameraView) =
            { state with initialCamera = Some c }
            
        [<CustomOperation("initialSpeed")>]
        member x.InitialSpeed(state : RenderConfig, s : float) =
            { state with initialSpeed = Some s }

        member x.Run(cfg : RenderConfig) =
            Utilities.createWindow (cliOverrides cfg)
            
    let window = WindowBuilder()
    let show = ShowBuilder()