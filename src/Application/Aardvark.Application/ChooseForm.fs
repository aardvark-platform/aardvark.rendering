namespace Aardvark.Application

open System.IO
open System.Reflection
open System.Drawing
open System.Windows.Forms

type private MainForm<'a>(devices : array<string * 'a>) as this =
    inherit Form()
    static let controlPadding = 10



    let mutable result = None
         
    let text = new Label(Text = "Please choose a default \r\nrendering device for Vulkan\r\n \r\n ")
    let popUp = new ComboBox()
    let ok = new Button(Text = "OK")
    let cancel = new Button(Text = "Cancel")

    let popUpPanel = new Panel()
    let controlPanel = new Panel()
    do this.Init()

    member private x.Init() =
        x.SuspendLayout()
        x.AcceptButton <- ok
        x.CancelButton <- cancel
        x.Text <- "Choose Vulkan Device"

        popUpPanel.Padding <- Padding(10)
        popUpPanel.BackColor <- Color.White
        popUpPanel.Dock <- DockStyle.Fill
        controlPanel.Dock <- DockStyle.Bottom

        controlPanel.Controls.Add(ok)
        controlPanel.Controls.Add(cancel)
        popUpPanel.Controls.Add popUp
        popUpPanel.Controls.Add(text)
        x.Controls.Add(popUpPanel)
        x.Controls.Add(controlPanel)


        x.FormBorderStyle <- FormBorderStyle.FixedDialog
        x.MinimizeBox <- false
        x.MaximizeBox <- false
        x.ShowInTaskbar <- false
        x.TopMost <- true

        x.StartPosition <- FormStartPosition.CenterScreen
        x.Width <- 366
        x.Height <- 192


        controlPanel.Width <- x.ClientSize.Width
        controlPanel.Height <- ok.Height + 2 * controlPadding


        ok.Left <- controlPanel.ClientSize.Width - ok.Width - controlPadding
        ok.Top <- controlPanel.ClientSize.Height - ok.Height - controlPadding
        ok.Anchor <- AnchorStyles.Bottom ||| AnchorStyles.Right

        cancel.Left <- ok.Left - cancel.Width - 5
        cancel.Top <- ok.Top
        cancel.Anchor <- AnchorStyles.Bottom ||| AnchorStyles.Right

        cancel.Click.Add (fun _ -> x.Close())
        ok.Click.Add (fun _ -> x.DialogResult <- DialogResult.OK; x.Close())


        text.TextAlign <- ContentAlignment.MiddleCenter
        text.Dock <- DockStyle.Fill
        text.Font <- new Font(text.Font.FontFamily, text.Font.Size * 1.5f)
        popUp.Dock <- DockStyle.Bottom
        for (name,_) in devices do
            popUp.Items.Add(name) |> ignore


        popUp.Font <- new Font(popUp.Font.FontFamily, popUp.Font.Size * 1.5f)
        popUp.SelectedIndex <- 0
        popUp.DropDownStyle <- ComboBoxStyle.DropDownList
        x.ResumeLayout()

    member x.Result = result

    override x.OnClosed(e) =
        base.OnClosed(e)
            
        let index = popUp.SelectedIndex
        if index >= 0 && index < devices.Length then
            let (_, d) = devices.[index]
            result <- Some d
        else
            result <- None


type ChooseForm =

    static member run (entries : array<string * 'a>) =
        use f = new MainForm<_>(entries)
        match f.ShowDialog() with
            | DialogResult.OK -> f.Result
            | _ -> None

    static member run (entries : list<string * 'a>) =
        entries |> List.toArray |> ChooseForm.run

    static member run (entries : array<string>) =
        entries |> Array.map (fun a -> a,a) |> ChooseForm.run

    static member run (entries : list<string>) =
        entries |> List.map (fun a -> a,a) |> ChooseForm.run
