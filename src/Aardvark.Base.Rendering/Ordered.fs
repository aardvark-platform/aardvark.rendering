namespace Aardvark.Base

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering

[<RequireQualifiedAccess>]
type RenderCommand =
    | Render of IRenderObject
    | Clear of color : IMod<C4f> * depth : IMod<float> * stencil : IMod<int>
    //| Custom of (IRuntime -> AdaptiveToken -> RenderToken -> unit)
    
[<RequireQualifiedAccess>]
type RenderProgram =
    | Sequential of alist<RenderProgram>
    | Parallel of aset<RenderProgram>
    | Execute of RenderCommand
    | Empty

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RenderProgram =
        
    let ofObjectSet (set : aset<IRenderObject>) =
        RenderProgram.Parallel(set |> ASet.map (RenderCommand.Render >> RenderProgram.Execute))

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
                                | RenderCommand.Render h :: _ -> Some (0 :: projections h,o)
                                | _ -> Some([1; o.GetHashCode()], o)
                        else
                            Some ([1; o.GetHashCode()], o)
                    )
                    |> ASet.sortBy fst
                    |> AList.collect snd



