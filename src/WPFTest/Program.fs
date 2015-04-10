// Learn more about F# at http://fsharp.org
// See the 'F# Tutorial' project for more help.

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Application
open Aardvark.Application.WPF
open System.Windows
open System.Windows.Input
open System.Windows.Controls
open System.Windows.Data
open System.ComponentModel
open System.Collections.Specialized

module UITest = 

    let (</) (f : 'a -> 'b) (l : list<Aardvark.Application.UI.IUi -> unit>, v : 'a) : 'b =
        let r = f v
        for att in l do att r
        r

    let (/>) (l : list<Aardvark.Application.UI.IUi -> unit>) (v : 'a) = (l,v)

    let (|<) (f : 'a -> 'b) (l : list<Aardvark.Application.UI.IUi -> unit>) : 'a -> 'b =
        fun v ->
            let r = f v
            for att in l do att r
            r

    let (>|) (f : 'a -> 'b) (v : 'a) = f v

    open Aardvark.Application.UI
    open Aardvark.Application.UI.WPF
    let myUi() =
        let check = Mod.initMod false
        let c = EventSource<unit>()
        let some = Mod.initMod "Hi There you crazy basterd!"
        let text = Mod.initMod ""

        let treeItems = corderedset [1;2;3]
        let r = Random()

        check |> Mod.registerCallback (fun c ->
            if c then
                let before = r.NextDouble() > 0.5
                let anchor = 1 + r.Next treeItems.Count

                transact (fun () ->
                    if before then treeItems.InsertBefore(anchor, treeItems.Count + 1) |> ignore
                    else treeItems.InsertAfter(anchor, treeItems.Count + 1) |> ignore
                )
            else 
                transact (fun () -> treeItems.Remove (treeItems.Count)) |> ignore

            printfn "checked: %A" c
        ) |> ignore

        text |> Mod.registerCallback (fun c ->
            
            printfn "text: %A" c
        ) |> ignore

        let r = Random()
        c.Values.Subscribe(fun () -> 
            transact (fun () -> 
                some.Value <- if r.NextDouble() > 0.5 then "Hi There you crazy basterd!" else "So what"
                check.Value <- not check.Value
            )
        ) |> ignore



        let content =
            UI.horizontal |< [ sizes &= [80R; 20R]] >| [
                UI.vertical -< [ sizes &= [25R; 50R; 25R]] >- [
                    UI.button -< [ onclick &= c] >-
                        "Click Me"

                    UI.checkbox -< [ isChecked &= check] >- 
                        "Check Me"

                    UI.textBox text
                ]
                UI.tree treeItems (fun a -> AList.empty) (fun i -> UI.label (string i))
            ]

        content.WPF()


[<STAThread; EntryPoint>]
let main argv = 
    Aardvark.Init()
    let app = Application()

    let w = Window()
    w.Content <- UITest.myUi()

    app.Run w
