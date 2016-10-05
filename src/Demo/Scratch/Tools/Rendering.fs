namespace global

open System
open System.Reflection
open System.Windows.Forms
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.SceneGraph.IO
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Rendering.NanoVg

type private ChangeableRenderTask() =
    inherit AbstractRenderTask()

    let mutable current = RenderTask.empty

    member x.Inner
        with get() = current
        and set v =
            current <- v
            transact (fun () -> x.MarkOutdated())

    override x.Run o = current.Run(x, o)

    override x.Dispose() = current <- RenderTask.empty

    override x.Update() = current.Update(x)

    override x.Use(f) = current.Use(f)

    override x.FramebufferSignature = current.FramebufferSignature

    override x.Runtime = current.Runtime

[<AttributeUsage(AttributeTargets.Method)>]
type DemoAttribute(name : Option<string>) = 
    inherit System.Attribute()   
    member x.Name = name
    
    new(name) = DemoAttribute(Some name)
    new() = DemoAttribute(None)    

[<AllowNullLiteral; AttributeUsage(AttributeTargets.Method)>]
type CategoryAttribute(name : string) = 
    inherit System.Attribute()   
    member x.Name = name
     


[<AllowNullLiteral; AttributeUsage(AttributeTargets.Method)>]
type DescriptionAttribute(desc : string) = 
    inherit System.Attribute()   
    member x.Description = desc
    

type App private () =
    
    static let app = lazy ( new OpenGlApplication() )
    static let mutable win : Option<SimpleRenderWindow> = None
    static let realTask = new ChangeableRenderTask()

    static let getWin() =
        match win with
            | Some w when not w.IsDisposed -> 
                w 
            | _ -> 
                let w = app.Value.CreateSimpleRenderWindow()
                w.RenderTask <- realTask
                win <- Some w
                w 

    static let withCam (sg : ISg) =
        let win = getWin()
        let cam = CameraView.lookAt (V3d(6,6,6)) V3d.Zero V3d.OOI
        let view = cam |> DefaultCameraController.control win.Mouse win.Keyboard win.Time
        let proj = win.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 1000.0 (float s.X / float s.Y))

        sg |> Sg.viewTrafo (view |> Mod.map CameraView.viewTrafo)
           |> Sg.projTrafo (proj |> Mod.map Frustum.projTrafo)

    static member Runtime = app.Value.Runtime :> IRuntime
    static member FramebufferSignature = getWin().FramebufferSignature
    static member Keyboard = getWin().Keyboard
    static member Mouse = getWin().Mouse
    static member Time = getWin().Time

    static member run (task : IRenderTask) =
        realTask.Inner <- task
        System.Windows.Forms.Application.Run(getWin())
        realTask.Inner <- RenderTask.empty
        win <- None

    static member run (sg : ISg) =
        use task = App.Runtime.CompileRender(App.FramebufferSignature, withCam sg)
        App.run task


    static member run() =
        let self = typeof<App>.Assembly
        let allDemos = Introspection.GetAllMethodsWithAttribute<DemoAttribute>(self)
        
        let demos =
            allDemos
                |> Seq.map (fun t -> t.E0, t.E1.[0])
                |> Seq.filter (fun (mi,_) -> mi.IsStatic)
                |> Seq.map (fun (mi, n) ->
                    let att = mi.GetCustomAttribute<DescriptionAttribute>()
                    let desc = if isNull att then None else Some att.Description
                    
                    let cat = mi.GetCustomAttribute<CategoryAttribute>()
                    let cat = if isNull cat then "Global" else cat.Name
                    
                    match n.Name with
                        | Some n -> cat, n, desc, mi
                        | None -> cat, mi.Name, desc, mi
                   )
                |> HashSet.ofSeq
                |> Seq.groupBy (fun (c,_,_,_) -> c)
                |> Seq.sortBy (fun (c,_) -> c)
                |> Seq.map (fun (c,demos) -> c, demos |> Seq.map (fun (_,a,b,c) -> (a,b,c)) |> Seq.sortBy (fun (a,_,_) -> a) |> Seq.toList )
                |> Seq.toList

        let runDemo(mi : MethodInfo) =
            let res = mi.Invoke(null, [||])
            match res with
                | :? ISg as s ->
                    let w = getWin()
                    let task = app.Value.Runtime.CompileRender(w.FramebufferSignature, withCam s)
                    realTask.Inner <- task |> DefaultOverlays.withStatistics
                    w.Show()

                | :? IRenderTask as r -> 
                    realTask.Inner <- r
                    let w = getWin()
                    w.Show()
                | _ -> ()


        let form = new Form(Text = "Demo")
        form.Width <- 280
        form.Height <- 400

        let list = new DataGridView()
        //let list = new ListView(Dock = DockStyle.Fill)
        let panel = new Panel(Dock = DockStyle.Bottom, Height = 30)
        let run = new Button(Text = "Run", Dock = DockStyle.Fill, Height = 30)
        panel.Controls.Add(run)

        form.Controls.Add list
        //form.Controls.Add panel
        form.StartPosition <- FormStartPosition.CenterScreen
        form.AcceptButton <- run
        form.SuspendLayout()
        
        list.Width <- form.ClientSize.Width
        list.MultiSelect <- false
        list.AllowUserToAddRows <- false
        list.AllowUserToDeleteRows <- false
        list.AllowUserToOrderColumns <- false
        list.AllowUserToResizeRows <- false
        list.AllowUserToResizeColumns <- false
        list.ReadOnly <- true
        

        list.Dock <- DockStyle.Fill
        let image = list.Columns.Add("a", "a")
        let col = list.Columns.Add("b", "b")
        list.Columns.[image].AutoSizeMode <- DataGridViewAutoSizeColumnMode.None
        list.Columns.[image].Width <- form.Icon.Width
        list.Columns.[col].AutoSizeMode <- DataGridViewAutoSizeColumnMode.Fill
        list.RowHeadersVisible <- false
        list.ColumnHeadersVisible <- false
        list.BackColor <- System.Drawing.Color.White
        list.BackgroundColor <- System.Drawing.Color.White
        list.SelectionMode <- DataGridViewSelectionMode.FullRowSelect


        let start() =
            if list.SelectedRows.Count = 1 then
                let item = list.SelectedRows.[0].Cells.[1]
                let mi = item.Tag |> unbox<MethodInfo>
                runDemo mi

        list.KeyDown.Add (fun e ->
            if e.KeyCode = System.Windows.Forms.Keys.Enter then
                start()
                e.SuppressKeyPress <- true
            elif e.KeyCode = System.Windows.Forms.Keys.Escape then
                form.Close()
                e.SuppressKeyPress <- true
                
        )

        let newRow() =
            let row = new DataGridViewRow()
            let c0 = new DataGridViewImageCell(true)
            c0.ImageLayout <- DataGridViewImageCellLayout.Normal
            c0.Value <- form.Icon
            row.Cells.Add(c0) |> ignore
            row.Height <- form.Icon.Height
            c0.ReadOnly <- true
            row

        let mutable i = 0
        for cat, demos in demos do
//            let row = newRow()
//
////            let cell = new DataGridViewTextBoxCell()
////            cell.Value <- cat
////            
//            
//            row.Cells.Add(cell) |> ignore
//            cell.ReadOnly <- true


            for (name, desc, mi) in demos do
                let row = newRow()

                let cell = new DataGridViewTextBoxCell()
            
                cell.Tag <- mi
                cell.Value <- name
                let name = mi.DeclaringType.FullName + "." + mi.Name
            
                cell.ToolTipText <-
                    match desc with 
                        | Some desc ->  name + "\r\n\r\n" + desc
                        | _ -> name

            

                row.Cells.Add(cell) |> ignore

            
            

                list.Rows.Add(row) |> ignore
                cell.ReadOnly <- true



        list.CellMouseDoubleClick.Add (fun c ->
            let mi = list.Rows.[c.RowIndex].Cells.[1].Tag |> unbox<MethodInfo>
            runDemo mi
            ()
        )

        run.Click.Add (fun _ -> start())

        form.ResumeLayout()
        Application.Run(form)

        ()
 