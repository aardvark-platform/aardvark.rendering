namespace Aardvark.SceneGraph

open Aardvark.Base
open FSharp.Data.Adaptive
open Offler
open Aardvark.Application
open System


type BrowserRenderInfo =
    {
        offler      : Offler
        keyboard    : option<IKeyboard>
        mouse       : option<IMouse>
        focus       : cval<bool>
        dispose     : list<IDisposable>
        mipMaps     : bool
    }


module Browser = 
    type internal BrowserBuilderState =
        {
            offler      : option<Offler>
            url         : string
            size        : aval<V2i>
            keyboard    : option<IKeyboard>
            mouse       : option<IMouse>
            focus       : cval<bool>
            mipMaps     : bool
        }


    type Property = internal { update : BrowserBuilderState -> BrowserBuilderState }

type Browser =
    static member keyboard (keyboard : IKeyboard) =
        {
            Browser.update = fun i -> { i with keyboard = Some keyboard }
        }

    static member mouse (mouse : IMouse) =
        {
            Browser.update = fun i -> { i with mouse = Some mouse }
        }

    static member control (ctrl : IRenderControl) =
        {
            Browser.update = fun i -> { i with keyboard = Some ctrl.Keyboard; mouse = Some ctrl.Mouse }
        }

    static member offler (offler : Offler) =
        {
            Browser.update = fun i -> { i with offler = Some offler }
        }
        
    static member url (url : string) =
        {
            Browser.update = fun i -> { i with url = url }
        }
        
    static member size (width : int, height : int) =
        {
            Browser.update = fun i -> { i with size = (AVal.constant (V2i(width, height))) }
        }
    static member size (size : V2i) =
        {
            Browser.update = fun i -> { i with size = (AVal.constant size) }
        }
    static member size (size : aval<V2i>) =
        {
            Browser.update = fun i -> { i with size = size }
        }

    static member focus (focus : cval<bool>) =
        {
            Browser.update = fun i -> { i with focus = focus }
        }
        
    static member mipMaps (mipMaps : bool) =
        {
            Browser.update = fun i -> { i with mipMaps = mipMaps }
        }

module Sg =
    
    type BrowserNode(info : BrowserRenderInfo) =
        interface ISg
        member x.Info = info
        
    type BrowserBuilder() =
        member x.Yield(prop : Browser.Property) =
            [prop]

        member x.Zero() : list<Browser.Property> =
            []

        member x.Combine(l : list<Browser.Property>, r : unit -> list<Browser.Property>) =
            l @ r()

        member x.Delay(action : unit -> list<Browser.Property>) =
            action

        member x.Run(action : unit -> list<Browser.Property>) =
            let mutable props = { Browser.offler = None; Browser.url = "https://aardvarkians.com"; Browser.size = AVal.constant (V2i(1024,768)); Browser.keyboard = None; Browser.mouse = None; Browser.focus = cval false; Browser.mipMaps = true }
            for u in action() do
                props <- u.update props
            

            let config =
                match props.offler with
                | Some o -> 
                    { offler = o; keyboard = props.keyboard; mouse = props.mouse; focus = props.focus; dispose = []; mipMaps = props.mipMaps }
                | None -> 
                    let s = AVal.force props.size
                    let o = new Offler { url = props.url; width = s.X; height = s.Y; incremental = true }

                    let subscriptions =
                        if not props.size.IsConstant then
                            [
                                props.size.AddCallback (fun (s : V2i) ->
                                    o.Resize(s.X, s.Y)
                                )
                            ]
                        else    
                            []

                    { offler = o; keyboard = props.keyboard; mouse = props.mouse; focus = props.focus; dispose = subscriptions; mipMaps = props.mipMaps }
            BrowserNode(config) :> ISg

    let browser = BrowserBuilder()




namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open FSharp.Data.Adaptive
open Offler
open Aardvark.Application
open System
open Aardvark.SceneGraph
open Aardvark.Rendering

module private BrowserIO =
    let ofList (d : list<IDisposable>) =
        { new IDisposable with
            member x.Dispose() =
                for e in d do e.Dispose()
        }

    let tryGetPixel  (mvp : aval<Trafo3d>) (browser : Offler) (p : PixelPosition) =
        let mvp = AVal.force mvp
        let tc = p.NormalizedPosition
        let ndc = V2d(2.0 * tc.X - 1.0, 1.0 - 2.0 * tc.Y)

        let n = mvp.Backward.TransformPosProj(V3d(ndc, -1.0))
        let f = mvp.Backward.TransformPosProj(V3d(ndc, 1.0))

        let ray = Ray3d(n, Vec.normalize (f - n))

        let mutable t = 0.0
        if ray.Intersects(Plane3d.ZPlane, &t) && t >= 0.0 then
            let localPt = ray.GetPointOnRay(t).XY
            if localPt.AllGreaterOrEqual -1.0 && localPt.AllSmallerOrEqual 1.0 then
                let tc = 0.5 * localPt + V2d.Half
                let fpx = tc * V2d(browser.Width, browser.Height)
                let px = round fpx |> V2i
                Some px
            else
                None
        else
            None

    let installMouse (focus : cval<bool>) (mvp : aval<Trafo3d>) (browser : Offler) (keyboard : option<IKeyboard>) (mouse : IMouse) =
            
        let ctrl = match keyboard with | Some k -> AVal.logicalOr [k.IsDown Keys.LeftCtrl; k.IsDown Keys.RightCtrl] | None -> AVal.constant false
        let shift = match keyboard with | Some k -> AVal.logicalOr [k.IsDown Keys.LeftShift; k.IsDown Keys.RightShift] | None -> AVal.constant false
        let alt = match keyboard with | Some k -> AVal.logicalOr [k.IsDown Keys.LeftAlt; k.IsDown Keys.RightAlt] | None -> AVal.constant false
            
        let mutable inside = false
        let mutable lastPixel = V2i.Zero
        let anyButton = [MouseButtons.Left; MouseButtons.Middle; MouseButtons.Right] |> List.existsA (fun b -> mouse.IsDown b)
        ofList [
            //focus.AddCallback (function 
            //    | false -> 
            //        inside <- false
            //        browser.SetFocus false
            //    | true -> 
            //        browser.SetFocus true
            //)
            mouse.Move.Values.Subscribe (fun (_, p) ->
                if not (AVal.force anyButton) || AVal.force focus then
                    match tryGetPixel mvp browser p with
                    | Some p -> 
                        if not inside then browser.MouseEnter(p.X, p.Y, AVal.force ctrl, AVal.force alt, AVal.force shift)
                        inside <- true
                        lastPixel <- p
                        browser.MouseMove(p.X, p.Y, AVal.force ctrl, AVal.force alt, AVal.force shift)
                    | None ->
                        if inside then browser.MouseLeave(lastPixel.X, lastPixel.Y, AVal.force ctrl, AVal.force alt, AVal.force shift)
                        inside <- false
            )
            mouse.Down.Values.Subscribe (fun b ->
                let p = AVal.force mouse.Position
                match tryGetPixel mvp browser p with
                | Some p ->
                    let c = browser.ReadPixel(p)
                    if c.A >= 127uy then
                        transact (fun () -> focus.Value <- true)
                        let button =
                            match b with
                            | MouseButtons.Left -> MouseButton.Left
                            | MouseButtons.Right -> MouseButton.Right
                            | _ -> MouseButton.Middle
                        browser.MouseDown(p.X, p.Y, button, AVal.force ctrl, AVal.force alt, AVal.force shift)
                    else
                        transact (fun () -> focus.Value <- false)
                | None ->
                    transact (fun () -> focus.Value <- false)
                    ()
            )
    
            mouse.Up.Values.Subscribe (fun b ->
                if AVal.force focus then
                    let p = AVal.force mouse.Position
                    match tryGetPixel mvp browser p with
                    | Some p ->
                        let button =
                            match b with
                            | MouseButtons.Left -> MouseButton.Left
                            | MouseButtons.Right -> MouseButton.Right
                            | _ -> MouseButton.Middle
                        browser.MouseUp(p.X, p.Y, button, AVal.force ctrl, AVal.force alt, AVal.force shift)
                    | None ->
                        ()
            )
    
            mouse.Click.Values.Subscribe (fun b ->
                let p = AVal.force mouse.Position
                match tryGetPixel mvp browser p with
                | Some p ->
                    let button =
                        match b with
                        | MouseButtons.Left -> MouseButton.Left
                        | MouseButtons.Right -> MouseButton.Right
                        | _ -> MouseButton.Middle
                    browser.MouseClick(p.X, p.Y, button, 1, AVal.force ctrl, AVal.force alt, AVal.force shift)
                | None ->
                    ()
            )
            mouse.DoubleClick.Values.Subscribe (fun b ->
                let p = AVal.force mouse.Position
                match tryGetPixel mvp browser p with
                | Some p ->
                    let button =
                        match b with
                        | MouseButtons.Left -> MouseButton.Left
                        | MouseButtons.Right -> MouseButton.Right
                        | _ -> MouseButton.Middle
                    browser.MouseClick(p.X, p.Y, button, 2, AVal.force ctrl, AVal.force alt, AVal.force shift)
                | None ->
                    ()
            )

            mouse.Scroll.Values.Subscribe(fun d ->
                if AVal.force focus then
                    let p = AVal.force mouse.Position
                    match tryGetPixel mvp browser p with
                    | Some p ->
                        browser.MouseWheel(p.X, p.Y, 0.0, d, AVal.force ctrl, AVal.force alt, AVal.force shift)
                    | None ->
                        ()
            )
        ]

    let translateKey (keys : Keys) =
        match keys with
        | Keys.D0 -> "0"
        | Keys.D1 -> "1"
        | Keys.D2 -> "2"
        | Keys.D3 -> "3"
        | Keys.D4 -> "4"
        | Keys.D5 -> "5"
        | Keys.D6 -> "6"
        | Keys.D7 -> "7"
        | Keys.D8 -> "8"
        | Keys.D9 -> "9"
            
        | Keys.NumPad0 -> "num0"
        | Keys.NumPad1 -> "num1"
        | Keys.NumPad2 -> "num2"
        | Keys.NumPad3 -> "num3"
        | Keys.NumPad4 -> "num4"
        | Keys.NumPad5 -> "num5"
        | Keys.NumPad6 -> "num6"
        | Keys.NumPad7 -> "num7"
        | Keys.NumPad8 -> "num8"
        | Keys.NumPad9 -> "num9"
            
        | Keys.A -> "A"
        | Keys.B -> "B"
        | Keys.C -> "C"
        | Keys.D -> "D"
        | Keys.E -> "E"
        | Keys.F -> "F"
        | Keys.G -> "G"
        | Keys.H -> "H"
        | Keys.I -> "I"
        | Keys.J -> "J"
        | Keys.K -> "K"
        | Keys.L -> "L"
        | Keys.M -> "M"
        | Keys.N -> "N"
        | Keys.O -> "O"
        | Keys.P -> "P"
        | Keys.Q -> "Q"
        | Keys.R -> "R"
        | Keys.S -> "S"
        | Keys.T -> "T"
        | Keys.U -> "U"
        | Keys.V -> "V"
        | Keys.W -> "W"
        | Keys.X -> "X"
        | Keys.Y -> "Y"
        | Keys.Z -> "Z"
            
        | Keys.F1 -> "F1"
        | Keys.F2 -> "F2"
        | Keys.F3 -> "F3"
        | Keys.F4 -> "F4"
        | Keys.F5 -> "F5"
        | Keys.F6 -> "F6"
        | Keys.F7 -> "F7"
        | Keys.F8 -> "F8"
        | Keys.F9 -> "F9"
        | Keys.F10 -> "F10"
        | Keys.F11 -> "F11"
        | Keys.F12 -> "F12"
        | Keys.F13 -> "F13"
        | Keys.F14 -> "F14"
        | Keys.F15 -> "F15"
        | Keys.F16 -> "F16"
        | Keys.F17 -> "F17"
        | Keys.F18 -> "F18"
        | Keys.F19 -> "F19"
        | Keys.F20 -> "F20"
        | Keys.F21 -> "F21"
        | Keys.F22 -> "F22"
        | Keys.F23 -> "F23"
        | Keys.F24 -> "F24"
            
        | Keys.Space -> "Space"
        | Keys.Tab -> "Tab"
        | Keys.Scroll -> "Scrolllock"
        | Keys.CapsLock -> "Capslock"
        | Keys.NumLock -> "Numlock"
        | Keys.Back -> "Backspace"
        | Keys.Insert -> "Inser"
        | Keys.Delete -> "Delete"
        | Keys.Return -> "Enter"
        | Keys.Home -> "Home"
        | Keys.End -> "End"
        | Keys.PageUp -> "PageUp"
        | Keys.PageDown -> "PageDown"
        | Keys.Escape -> "Escape"
        | Keys.Print -> "PrintScreen"
            
        | Keys.Up -> "Up"
        | Keys.Down -> "Down"
        | Keys.Left -> "Left"
        | Keys.Right -> "Right"

        | Keys.LeftCtrl | Keys.RightCtrl
        | Keys.LeftAlt | Keys.RightAlt 
        | Keys.LWin | Keys.RWin
        | Keys.LeftShift | Keys.RightShift ->
            ""

        | _ -> 
            Log.warn "unknown key: %A" keys
            ""

    let installKeyboard (focus : cval<bool>) (browser : Offler) (keyboard : IKeyboard) =
        let ctrl = AVal.logicalOr [keyboard.IsDown Keys.LeftCtrl; keyboard.IsDown Keys.RightCtrl] 
        let shift = AVal.logicalOr [keyboard.IsDown Keys.LeftShift; keyboard.IsDown Keys.RightShift]
        let super = AVal.logicalOr [keyboard.IsDown Keys.LWin; keyboard.IsDown Keys.RWin]
        let alt = keyboard.IsDown Keys.LeftAlt
        let altGr = keyboard.IsDown Keys.RightAlt
            
        let withModifiers (code : string) =
            let mutable code = code
            if AVal.force alt then code <- "Alt+" + code 
            if AVal.force altGr then code <- "AltGr+" + code 
            if AVal.force shift then code <- "Shift+" + code 
            if AVal.force ctrl then code <- "Ctrl+" + code 
            if AVal.force super then code <- "Super+" + code 
            code

        ofList [
            keyboard.DownWithRepeats.Values.Subscribe(fun k ->
                if AVal.force focus then
                    let code = translateKey k
                    if code <> "" then 
                        let full = withModifiers code
                        if full = "Enter" then
                            browser.Input "\u000d"
                        else
                            browser.KeyDown full
            )

            keyboard.Up.Values.Subscribe(fun k ->
                if AVal.force focus then
                    let code = translateKey k
                    if code <> "" then browser.KeyUp(withModifiers code)
            )

            keyboard.Press.Values.Subscribe(fun c ->
                if AVal.force focus then
                    browser.Input(System.String(c, 1))
            )
        ]


[<Rule>]
type BrowserNodeSem() =

    member x.RenderObjects(node : Sg.BrowserNode, scope : Ag.Scope) : aset<IRenderObject> =
        let runtime = scope.Runtime
        let o = RenderObject.ofScope scope
            
        let m = scope.ModelTrafo
        let v = scope.ViewTrafo
        let p = scope.ProjTrafo
        let mvp = (m,v,p) |||> AVal.map3 (fun m v p -> m * v * p)

        let tex = BrowserTexture.create runtime node.Info.offler node.Info.mipMaps

        let activate () =
            let mouse = 
                match node.Info.mouse with
                | Some mouse ->
                    BrowserIO.installMouse node.Info.focus mvp node.Info.offler node.Info.keyboard mouse
                | None ->
                    Disposable.empty

            let keyboard = 
                match node.Info.keyboard with
                | Some keyboard ->
                    BrowserIO.installKeyboard node.Info.focus node.Info.offler keyboard
                | None ->
                    Disposable.empty
                        
            { new IDisposable with 
                member x.Dispose() =
                    mouse.Dispose()
                    keyboard.Dispose()
                    transact (fun () -> node.Info.focus.Value <- false)
            }

        let special =
            UniformProvider.ofList [
                DefaultSemantic.DiffuseColorTexture, tex :> IAdaptiveValue 
            ]


        o.Uniforms <-
            UniformProvider.union special o.Uniforms

        o.VertexAttributes <- 
            AttributeProvider.ofList [
                DefaultSemantic.Positions, [| V3f.NNO; V3f.PNO; V3f.IIO; V3f.NPO |] :> Array
                DefaultSemantic.DiffuseColorCoordinates, [| V2f.OO; V2f.IO; V2f.II; V2f.OI |] :> Array
            ]
        o.Indices <-
            BufferView(AVal.constant (ArrayBuffer [|0;1;2; 0;2;3|] :> IBuffer), typeof<int>) |> Some

        o.DrawCalls <- DrawCalls.Direct (AVal.constant [DrawCallInfo(6)])

        o.Activate <- activate
        ASet.single (o :> IRenderObject)

    member x.LocalBoundingBox(node : Sg.BrowserNode, scope : Ag.Scope) : aval<Box3d> =
        AVal.constant (Box3d(V3d(-1.0, -1.0, -Constant.PositiveTinyValue), V3d(1.0, 1.0, Constant.PositiveTinyValue)))
            
    member x.GlobalBoundingBox(node : Sg.BrowserNode, scope : Ag.Scope) : aval<Box3d> =
        let b = Box3d(V3d(-1.0, -1.0, -Constant.PositiveTinyValue), V3d(1.0, 1.0, Constant.PositiveTinyValue))
        scope.ModelTrafo |> AVal.map (fun t -> b.Transformed t)

