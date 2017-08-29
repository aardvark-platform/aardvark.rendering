namespace Aardvark.Base

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering

type ICustomRenderCommand =
    abstract member Run : IRuntime * AdaptiveToken * RenderToken * OutputDescription -> unit
    abstract member UsedResources : list<IResource>
    abstract member AddRef : unit -> unit
    abstract member RemoveRef : unit -> unit

type private FunCustomCommand(run : IRuntime -> AdaptiveToken -> RenderToken -> OutputDescription -> unit) =

    interface ICustomRenderCommand with
        member x.AddRef() = ()
        member x.RemoveRef() = ()
        member x.UsedResources = []
        member x.Run(r,t,rt,o) = run r t rt o

    member x.Run = run

    override x.GetHashCode() = Unchecked.hash run
    override x.Equals o =
        match o with
            | :? FunCustomCommand as o -> Unchecked.equals run o.Run
            | _ -> false


[<RequireQualifiedAccess>]
type RenderCommand =
    | RenderC of o : IRenderObject
    | ClearC of colors : Map<Symbol, IMod<C4f>> * depth : Option<IMod<float>> * stencil : Option<IMod<int>>
    | CallC of task : IRenderTask
    | IfThenElseC of condition : IMod<bool> * ifTrue : list<RenderCommand> * ifFalse : list<RenderCommand>
    | CustomC of command : ICustomRenderCommand
    
    static member inline Clear(color : Map<Symbol, IMod<C4f>>, depth : Option<IMod<float>>, stencil : Option<IMod<int>>) =
        RenderCommand.ClearC(color, depth, stencil)
        
    static member inline Clear(color : Map<Symbol, IMod<C4f>>, depth : IMod<float>, stencil : IMod<int>) =
        RenderCommand.ClearC(color, Some depth, Some stencil)
        
    static member inline Clear(color : Map<Symbol, IMod<C4f>>, depth : IMod<float>) =
        RenderCommand.ClearC(color, Some depth, None)
        
    static member inline Clear(color : Map<Symbol, IMod<C4f>>) =
        RenderCommand.ClearC(color, None, None)


    static member inline Clear(color : list<Symbol * IMod<C4f>>, depth : IMod<float>, stencil : IMod<int>) =
        RenderCommand.ClearC(Map.ofList color, Some depth, Some stencil)

    static member inline Clear(color : list<Symbol * IMod<C4f>>, depth : IMod<float>) =
        RenderCommand.ClearC(Map.ofList color, Some depth, None)

    static member inline Clear(color : list<Symbol * IMod<C4f>>) =
        RenderCommand.ClearC(Map.ofList color, None, None)


    static member inline Clear(color : IMod<C4f>, depth : IMod<float>, stencil : IMod<int>) =
        RenderCommand.ClearC(Map.ofList [DefaultSemantic.Colors, color], Some depth, Some stencil)

    static member inline Clear(color : IMod<C4f>, depth : IMod<float>) =
        RenderCommand.ClearC(Map.ofList [DefaultSemantic.Colors, color], Some depth, None)

    static member inline Clear(color : IMod<C4f>) =
        RenderCommand.ClearC(Map.ofList [DefaultSemantic.Colors, color], None, None)

    static member inline Clear(depth : IMod<float>, stencil : IMod<int>) =
        RenderCommand.ClearC(Map.empty, Some depth, Some stencil)

    static member inline Clear(depth : IMod<float>) =
        RenderCommand.ClearC(Map.empty, Some depth, None)

    static member inline Clear(stencil : IMod<int>) =
        RenderCommand.ClearC(Map.empty, None, Some stencil)


    static member inline Render (o : IRenderObject) =
        RenderCommand.RenderC o

    static member inline Render (o : seq<IRenderObject>) =
        RenderCommand.RenderC (MultiRenderObject(Seq.toList o))

    static member Call(t : IRenderTask) =
        RenderCommand.CallC t

    static member IfThenElse(cond : IMod<bool>, ifTrue : list<RenderCommand>, ifFalse : list<RenderCommand>) =
        RenderCommand.IfThenElseC(cond, ifTrue, ifFalse)

    static member IfThenElse(cond : IMod<bool>, ifTrue : RenderCommand, ifFalse : RenderCommand) =
        RenderCommand.IfThenElseC(cond, [ ifTrue ], [ ifFalse ])

    static member When(cond : IMod<bool>, ifTrue : list<RenderCommand>) =
        RenderCommand.IfThenElseC(cond, ifTrue, [])

    static member When(cond : IMod<bool>, ifTrue : RenderCommand) =
        RenderCommand.IfThenElseC(cond, [ ifTrue ], [])
        
    static member WhenNot(cond : IMod<bool>, ifFalse : list<RenderCommand>) =
        RenderCommand.IfThenElseC(cond, [], ifFalse)

    static member WhenNot(cond : IMod<bool>, ifFalse : RenderCommand) =
        RenderCommand.IfThenElseC(cond, [], [ ifFalse ])
          
          
    static member Execute(cmd : ICustomRenderCommand) =
        RenderCommand.CustomC(cmd)

    static member Custom(run : IRuntime -> AdaptiveToken -> RenderToken -> OutputDescription -> unit) =
        let cmd = FunCustomCommand(run)
        RenderCommand.CustomC(cmd)


           
[<RequireQualifiedAccess>]
type RenderProgram =
    | Sequential of alist<RenderProgram>
    | Parallel of aset<RenderProgram>
    | Execute of RenderCommand
    | Empty

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RenderProgram =
        
    let ofObjectSet (set : aset<IRenderObject>) =
        RenderProgram.Parallel(set |> ASet.map (RenderCommand.RenderC >> RenderProgram.Execute))

    let ofSet (set : aset<RenderProgram>) = RenderProgram.Parallel set
    let ofList (list : alist<RenderProgram>) = RenderProgram.Sequential list

    let empty = RenderProgram.Empty

    let toObjectSet (o : RenderProgram) : aset<RenderObject> =
        failwith ""

    let par (l : RenderProgram) (r : RenderProgram) =
        RenderProgram.Parallel (ASet.ofList [l;r])

    let seq (l : RenderProgram) (r : RenderProgram) =
        RenderProgram.Sequential (AList.ofList [l;r])

    let rec toCommandList (projections : IRenderObject -> list<int>) (o : RenderProgram) : alist<RenderCommand> =
        match o with
            | RenderProgram.Empty -> AList.empty
            | RenderProgram.Execute op -> AList.single op
            | RenderProgram.Sequential list -> list |> AList.collect (toCommandList projections)
            | RenderProgram.Parallel set ->
                set |> ASet.choose (fun s ->
                        let o = toCommandList projections s
                        if o.IsConstant then
                            match o.Content |> Mod.force |> PList.toList with
                                | [] -> None
                                | RenderCommand.RenderC h :: _ -> Some (0 :: projections h,o)
                                | _ -> Some([1; o.GetHashCode()], o)
                        else
                            Some ([1; o.GetHashCode()], o)
                    )
                    |> ASet.sortBy fst
                    |> AList.collect snd



