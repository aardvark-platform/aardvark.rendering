namespace Aardvark.Application.UI

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Ag
open NUnit.Framework

type IUi = interface end

type LayoutSize =
    | Absolute of int
    | Relative of float
    | Default

module NumericLiteralR =
    let FromZero() = Relative 0.0
    let FromOne() = Relative 0.01
    let FromInt32 (i : int) = (float i / 100.0) |> min 1.0 |> max 0.0 |> Relative

module NumericLiteralZ =
    let FromZero() = Absolute 0
    let FromOne = Absolute 1
    let FromInt32 (i : int) = Absolute i

module private Utilities =
    let getSizes (count : int) (e : IUi) =
        lazy (
            match Ag.tryGetAttributeValue e "sizes" with
                | Success sizes ->
                    [ let current = ref sizes
                      for i in 1..count do
                        match !current with
                            | x::xs -> 
                                current := xs
                                yield x
                            | [] ->
                                yield Default
                    ]
                | _ ->
                    List.init count (fun _ -> Default)
        )

    let getAttribute (name : string) (defaultValue : 'a) (e : IUi) =
        lazy (
            match Ag.tryGetAttributeValue e name with
                | Success v -> v
                | _ -> defaultValue
        )

    let tryGetAttribute (name : string) (e : IUi) =
        lazy (
            match Ag.tryGetAttributeValue e name with
                | Success v -> Some v
                | _ -> None
        )


type Label(content : IMod<string>) =
    interface IUi
    member x.Content = content

type Button(label : IMod<string>) as this =
    let click : Lazy<EventSource<unit>> = this |> Utilities.getAttribute "click" null
    
    interface IUi
    member x.Label = label
    member x.Click = click.Value

type CheckBox(label : IMod<string>) as this =
   
    let isChecked : Lazy<ModRef<bool>> = this |> Utilities.getAttribute "checked" (Mod.initMod false)

    interface IUi
    member x.Checked = isChecked.Value
    member x.Label = label
    

type HorizontalStack(content : list<IUi>) as this =
    let sizes = this |> Utilities.getSizes (List.length content)

    interface IUi
    member x.Sizes = sizes.Value
    member x.Content = content

type VerticalStack(content : list<IUi>) as this =
    let sizes = this |> Utilities.getSizes (List.length content)

    interface IUi
    member x.Sizes = sizes.Value
    member x.Content = content

type TreeView<'a> (content : alist<'a>, ui : 'a -> IUi, children : 'a -> alist<'a>) as this =
    let selection = this |> Utilities.tryGetAttribute "selected"
    
    interface IUi

    member x.Selection : Option<ModRef<'a>> = selection.Value
    member x.Content = content
    member x.GetChildren = children
    member x.GetUi = ui

type TextBox (content : ModRef<string>) =
    interface IUi
    member x.Content = content

module UIHelpers =
    let raise (e : IEvent<'a>) (value : 'a) =
        match e with
            | :? EventSource<'a> as e -> e.Emit value
            | _ -> failwithf "cannot raise event of type: %A" (e.GetType())


module UI =
    let label str = Label(Mod.initConstant str) :> IUi
    let button str = Button(Mod.initConstant str) :> IUi
    let checkbox str = CheckBox(Mod.initConstant str) :> IUi

    let horizontal elements = HorizontalStack(elements) :> IUi
    let vertical elements = VerticalStack(elements) :> IUi

    let tree (content : alist<'a>) (children : 'a -> alist<'a>) (ui : 'a -> IUi) =
        TreeView(content, ui, children) :> IUi

    let modLabel str = Label(str) :> IUi
    let textBox str = TextBox(str) :> IUi

[<AutoOpen>]
module Extensions =
    let private rootScope = Ag.getContext()

    let (-<) (f : 'a -> 'b) (l : list<IUi -> unit>) : 'a -> 'b =
        fun v ->
            let r = f v
            for att in l do att r
            r

    let (>-) (f : 'a -> 'b) (v : 'a) = f v


    type Attribute<'a>(name : string) =
        member x.Name = name

    let sizes = Attribute<list<LayoutSize>>("sizes")
    let onclick = Attribute<EventSource<unit>>("click")
    let isChecked = Attribute<ModRef<bool>>("checked")
    
    let (&=) (name : Attribute<'a>) (value : 'a) =
        fun (a : IUi) ->
            rootScope.GetChildScope(a).AddCache name.Name (Some (value :> obj)) |> ignore



module UITest =

    let tryGetAttribte (o : 'a) (name : string) =
        match Ag.tryGetAttributeValue o name with
            | Success v -> Some v
            | Error e -> None

    type MyTree(value : string, children : alist<MyTree>) =
        member x.Value = value
        member x.Children = children

        static member children (a : MyTree) = a.Children

    let treeList : alist<MyTree> = AList.empty

    let ui =
        let buttonClicked = EventSource<unit>()
        let test = Mod.initMod false

        UI.horizontal -< [ sizes &= [10R; Default] ] >- [
            UI.label "label"

            UI.vertical [
                UI.button -< [onclick &= buttonClicked] >- "button"
            ]

            UI.checkbox -< [isChecked &= test] >- "test"

            UI.tree treeList MyTree.children (fun a ->
                UI.vertical [
                    UI.label a.Value
                    UI.button "ClickMe"
                ]
            )
        ]

    [<Test>]
    let test() =
        let u = ui |> unbox<HorizontalStack>

        let s = u.Sizes

        printfn "%A" s

