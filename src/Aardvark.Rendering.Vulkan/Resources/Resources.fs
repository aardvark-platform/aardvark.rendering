namespace Aardvark.Rendering.Vulkan

open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive

#nowarn "9"
// #nowarn "51"

[<AbstractClass; Sealed; Extension>]
type IResourceExtensions private() =

    [<Extension>]
    static member UpdateAndGetHandle(this : IResource<'a>, caller : AdaptiveToken, t : RenderToken) =
        this.Update(caller, t)
        this.Handle.GetValue()


type CustomResource<'a, 'v when 'a : equality and 'v : unmanaged>(owned : list<IResource>, create : AdaptiveToken -> RenderToken -> Option<'a> -> 'a, destroy : 'a -> unit, view : 'a -> 'v) =
    inherit Resource<'a, 'v>(ResourceKind.Unknown)
 
    override x.Create (token : AdaptiveToken, rt : RenderToken, old : Option<'a>) =
        let stats = owned |> List.iter (fun o -> o.Update(token, rt))
        
        let handle = create token rt old       
        handle

    override x.Destroy (h : 'a) =
        destroy h
        for o in owned do
            o.RemoveRef()
            o.Outputs.Remove x |> ignore

    override x.GetInfo _ =
        owned |> List.sumBy (fun r -> r.Info)

    override x.View h =
        view h

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Resource =

    let custom (create : AdaptiveToken -> RenderToken -> Option<'a> -> 'a) (destroy : 'a -> unit) (view : 'a -> 'v) (inputs : list<IResource>) =
        new CustomResource<'a, 'v>(inputs, create, destroy, view) :> IResource<'a, 'v>

//type VulkanResource<'a, 'b when 'a : equality and 'b : unmanaged>(input : IResource<'a>, f : 'a -> 'b) =
//    inherit Aardvark.Base.Rendering.Resource<'a, 'b>(ResourceKind.Unknown)
//    do input.AddRef()
//
//    member x.ManagedHandle = input.Handle
//
//    override x.View h = f h
//
//    override x.Create (token : AdaptiveToken, rt : RenderToken, old : Option<'a>) =
//        input.UpdateAndGetHandle(token, rt)
//
//    override x.Destroy (old : 'a) =
//        input.RemoveRef()
//        input.RemoveOutput x
//
//    override x.GetInfo (ptr) =
//        ResourceInfo.Zero

