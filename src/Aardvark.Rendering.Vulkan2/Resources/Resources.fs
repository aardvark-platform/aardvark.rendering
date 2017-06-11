namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Collections.Generic
open System.Collections.Concurrent
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Microsoft.FSharp.NativeInterop

#nowarn "9"
#nowarn "51"


    
[<AbstractClass; Sealed; Extension>]
type IResourceExtensions private() =

    [<Extension>]
    static member UpdateAndGetHandle(this : IResource<'a>, caller : AdaptiveToken, t : RenderToken) =
        this.Update(caller, t)
        this.Handle.GetValue()


type CustomResource<'a, 'v when 'a : equality and 'v : unmanaged>(owned : list<IResource>, create : AdaptiveToken -> RenderToken -> Option<'a> -> 'a, destroy : 'a -> unit, view : 'a -> 'v) =
    inherit Aardvark.Base.Rendering.Resource<'a, 'v>(ResourceKind.Unknown)
 
    override x.Create (token : AdaptiveToken, rt : RenderToken, old : Option<'a>) =
        let stats = owned |> List.iter (fun o -> o.Update(token, rt))
        
        let handle = create token rt old       
        handle

    override x.Destroy (h : 'a) =
        destroy h
        for o in owned do
            o.RemoveRef()
            o.RemoveOutput x

    override x.GetInfo _ =
        owned |> List.sumBy (fun r -> r.Info)

    override x.View h =
        view h

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Resource =
    let map (f : AdaptiveToken -> RenderToken -> Option<'b> -> 'a -> 'b) (destroy : 'b -> unit) (view : 'b -> 'v) (r : IResource<'a>) =
        let create (token : AdaptiveToken) (t : RenderToken) (old : Option<'b>) =
            let a = r.Handle |> Mod.force
            f token t old a

        new CustomResource<'b, 'v>([r :> IResource], create, destroy, view) :> IResource<'b, 'v>
    
    let custom (create : AdaptiveToken -> RenderToken -> Option<'a> -> 'a) (destroy : 'a -> unit) (view : 'a -> 'v) (inputs : list<IResource>) =
        new CustomResource<'a, 'v>(inputs, create, destroy, view) :> IResource<'a, 'v>

type VulkanResource<'a, 'b when 'a : equality and 'b : unmanaged>(input : IResource<'a>, f : 'a -> 'b) =
    inherit Aardvark.Base.Rendering.Resource<'a, 'b>(ResourceKind.Unknown)
    do input.AddRef()

    member x.ManagedHandle = input.Handle

    override x.View h = f h

    override x.Create (token : AdaptiveToken, rt : RenderToken, old : Option<'a>) =
        input.UpdateAndGetHandle(token, rt)

    override x.Destroy (old : 'a) =
        input.RemoveRef()
        input.RemoveOutput x

    override x.GetInfo (ptr) =
        ResourceInfo.Zero

//[<AbstractClass; Sealed; Extension>]
//type ResourceCacheExtensions private() =
//
//    [<Extension>]
//    static member inline GetOrCreateVulkan(this : ResourceCache<'h, 'h>, dataMod : IMod<'a>, additionalKeys : list<obj>, desc : ResourceDescription<'a, 'x, 'h>) =
//        let handle v = (^x : (member Handle : ^h) (v))
//        let res = this.GetOrCreateWrapped(dataMod, additionalKeys, desc, fun r -> new VulkanResource<'x, 'h>(r, handle) :> Rendering.Resource<_,_>)
//        unbox<VulkanResource<'x, 'h>> res
//
//    [<Extension>]
//    static member inline GetOrCreateVulkan(this : ResourceCache< ^h, ^h >, keys : list<obj>, create : unit -> Rendering.Resource< ^x, ^y >) =
//        let handle v = (^x : (member Handle : ^h) (v))
//        let wrap (v : Rendering.Resource<'x, 'y>) =
//            new VulkanResource<'x, 'h>(v, handle) :> Rendering.Resource<_,_>
//        let res : IResource<'h, 'h> = this.GetOrCreateWrapped(keys, create, wrap)
//        unbox<VulkanResource<'x, 'h>> res