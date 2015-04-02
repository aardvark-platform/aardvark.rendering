namespace Aardvark.Application.UI.WPF

open System
open System.ComponentModel
open System.Windows
open System.Windows.Controls
open System.Windows.Data
open System.Windows.Threading

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
    
    type Dispatcher with
        member x.OnUIThread (f : unit -> 'a) =
            if x.CheckAccess() then
                f()
            else
                x.Invoke(Func<'a>(f))

    type WrappedMod(e : UIElement, m : IMod) as this =
        let changed = Event<_,_>()

        let callback =
            fun () -> e.Dispatcher.OnUIThread (fun () -> changed.Trigger(this, PropertyChangedEventArgs("Value")))


        [<CLIEvent>]
        member x.PropertyChanged = changed.Publish

        interface INotifyPropertyChanged with
            [<CLIEvent>]
            member x.PropertyChanged = changed.Publish

        member x.Value = 
            lock m (fun () ->
                m.MarkingCallbacks.Add callback
                m.GetValue()
            )

    type WrappedModRef<'a>(e : UIElement, m : ModRef<'a>) as this =
        let changed = Event<_,_>()

        let callback =
            fun () -> e.Dispatcher.OnUIThread (fun () -> changed.Trigger(this, PropertyChangedEventArgs("Value")))

        [<CLIEvent>]
        member x.PropertyChanged = changed.Publish


        interface INotifyPropertyChanged with
            [<CLIEvent>]
            member x.PropertyChanged = changed.Publish

        member x.Value
            with get() =
                lock m (fun () ->
                    m.MarkingCallbacks.Add callback 
                    m.GetValue()
                )

            and set v =
                lock m (fun () ->
                    let success = m.MarkingCallbacks.Remove callback
                    transact (fun () -> m.Value <- v)
                    if success then m.MarkingCallbacks.Add callback 
                )

    let set (prop : DependencyProperty) (m : IMod) (e : FrameworkElement) =
        if m.IsConstant then
            e.SetValue(prop, m.GetValue())
        else
            let b = Binding("Value")
            b.Mode <- BindingMode.OneWay
            b.Source <- WrappedMod(e, m)
            BindingOperations.SetBinding(e, prop, b) |> ignore

    let setRef (prop : DependencyProperty) (m : ModRef<'a>) (e : FrameworkElement) =
        let b = Binding("Value")
        let source = WrappedModRef(e, m)
        b.Source <- source
        b.Mode <- BindingMode.TwoWay
        b.UpdateSourceTrigger <- UpdateSourceTrigger.PropertyChanged
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

            res :> obj 

        member x.WPF(t : TextBox) =
            let res = Controls.TextBox()
            res |> setRef Controls.TextBox.TextProperty t.Content

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

