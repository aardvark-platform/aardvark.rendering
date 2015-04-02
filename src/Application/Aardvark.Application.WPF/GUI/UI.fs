namespace Aardvark.Application.UI.WPF

open System
open System.ComponentModel
open System.Windows
open System.Windows.Controls
open System.Windows.Data
open Aardvark.Base
open Aardvark.Base.Ag
open Aardvark.Base.AgHelpers
open Aardvark.Base.Incremental
open Aardvark.Application.UI

module WPF =
    type Label = System.Windows.Controls.Label

[<AutoOpen>]
module Extensions =
    type IUi with
        member x.WPF() : obj = x?WPF()    

        member x.WPFElement() : FrameworkElement = x.WPF() |> unbox<_>

module Semantics =
    
    type WrappedMod(m : IMod) =
        let changed = Event<_,_>()

        interface INotifyPropertyChanged with
            [<CLIEvent>]
            member x.PropertyChanged = changed.Publish

        member x.Value = 
            lock m (fun () ->
                if m.OutOfDate then
                    m.MarkingCallbacks.Add(fun () -> changed.Trigger(x, PropertyChangedEventArgs("Value")))
                
                m.GetValue()
            )

    type WrappedModRef<'a>(m : ModRef<'a>) =
        let changed = Event<_,_>()
        let s = m.AddMarkingCallback(fun () -> printfn "mark"; changed.Trigger(m, PropertyChangedEventArgs("Value")))

        interface INotifyPropertyChanged with
            [<CLIEvent>]
            member x.PropertyChanged = changed.Publish

        member x.Value
            with get() = printfn "get"; m.GetValue()
            and set v = printfn "change"; transact (fun () -> m.Value <- v)


    let set (prop : DependencyProperty) (m : IMod) (e : FrameworkElement) =
        let b = Binding("Value")
        b.Source <- WrappedMod(m)
        BindingOperations.SetBinding(e, prop, b) |> ignore

    let setRef (prop : DependencyProperty) (m : ModRef<'a>) (e : FrameworkElement) =
        let b = Binding("Value")
        b.Source <- WrappedModRef(m)
        b.Mode <- BindingMode.TwoWay
        BindingOperations.SetBinding(e, prop, b) |> ignore

    let gridLength (s : LayoutSize) =
        match s with
            | Default -> GridLength.Auto
            | Relative r -> GridLength(100.0 * r, GridUnitType.Star)
            | Absolute p -> GridLength(float p, GridUnitType.Pixel)

    [<Semantic>]
    type LabelSemantics() =
        member x.WPF(l : Label) =
            let res = TextBlock()
            res |> set TextBlock.TextProperty l.Content
            res :> obj

        member x.WPF(b : Button) =
            let res = Controls.Button()
            res |> set Button.ContentProperty b.Label

            let clicked = b.Click
            if clicked <> null then
                res.Click.Add (fun e ->
                    clicked.Emit()
                )

            res :> obj

        member x.WPF(c : CheckBox) =
            let res = Controls.CheckBox()
            res |> set Controls.CheckBox.ContentProperty c.Label
            res |> setRef Controls.CheckBox.IsCheckedProperty c.Checked
//
//            let checkedMod = c.Checked
//            
//
//            let cb () =
//                let isChecked = if res.IsChecked.HasValue then res.IsChecked.Value else false
//
//                if isChecked <> checkedMod.Value then
//                    transact (fun () -> checkedMod.Value <- isChecked)
//                                   
//            res.Checked.Add (fun e -> cb())
//            res.Unchecked.Add (fun e -> cb())

            res :> obj 

        member x.WPF(s : HorizontalStack) =
            let res = Grid()

            let sizeAndContent = List.zip s.Sizes s.Content

            let mutable col = 0
            for (s, c) in sizeAndContent do
                res.ColumnDefinitions.Add(ColumnDefinition(Width = gridLength s))

                let content = c.WPFElement()
                content.SetValue(Grid.ColumnProperty, col)
                content.SetValue(Grid.RowProperty, 0)
                res.Children.Add(content) |> ignore
                col <- col + 1

            res :> obj

        member x.WPF(s : VerticalStack) =
            let res = Grid()

            let sizeAndContent = List.zip s.Sizes s.Content

            let mutable row = 0
            for (s, c) in sizeAndContent do
                res.RowDefinitions.Add(RowDefinition(Height = gridLength s))

                let content = c.WPFElement()
                content.SetValue(Grid.ColumnProperty, 0)
                content.SetValue(Grid.RowProperty, row)
                res.Children.Add(content) |> ignore
                row <- row + 1

            res :> obj

