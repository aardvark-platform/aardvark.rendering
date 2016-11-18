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
    static member UpdateAndGetHandle(this : IResource<'a>, caller : IResource) =
        let stats = this.Update(caller)
        this.Handle.GetValue(), stats


type CustomResource<'a when 'a : equality>(owned : list<IResource>, create : Option<'a> -> 'a, destroy : 'a -> unit) =
    inherit Aardvark.Base.Rendering.Resource<'a>(ResourceKind.Unknown)
 
    override x.Create (old : Option<'a>) =
        let stats = owned |> List.sumBy (fun o -> o.Update x)
        
        let handle = create old       
        handle, stats

    override x.Destroy (h : 'a) =
        destroy h
        for o in owned do
            o.RemoveRef()
            o.RemoveOutput x

    override x.GetInfo _ =
        owned |> List.sumBy (fun r -> r.Info)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Resource =
    let map (f : Option<'b> -> 'a -> 'b) (destroy : 'b -> unit) (r : IResource<'a>) =
        let create (old : Option<'b>) =
            let a = r.Handle |> Mod.force
            f old a

        new CustomResource<'b>([r :> IResource],create, destroy) :> IResource<'b>
    
    let custom (create : Option<'a> -> 'a) (destroy : 'a -> unit) (inputs : list<IResource>) =
        new CustomResource<'a>(inputs, create, destroy) :> IResource<'a>

type VulkanResource<'a, 'b when 'a : equality and 'b : unmanaged>(input : IResource<'a>, f : 'a -> 'b) =
    inherit Aardvark.Base.Rendering.Resource<nativeptr<'b>>(ResourceKind.Unknown)
    do input.AddRef()

    member x.ManagedHandle = input.Handle
    member x.Pointer = x.Handle.GetValue()

    override x.Create (old : Option<nativeptr<'b>>) =
        let ptr =
            match old with
                | Some ptr -> ptr
                | None -> NativePtr.alloc 1

        let handle, stats = input.UpdateAndGetHandle(x)
        NativePtr.write ptr (f handle)

        ptr, stats

    override x.Destroy (old : nativeptr<'b>) =
        NativePtr.free old
        input.RemoveRef()
        input.RemoveOutput x

    override x.GetInfo (ptr) =
        ResourceInfo.Zero

[<AbstractClass; Sealed; Extension>]
type ResourceCacheExtensions private() =

    [<Extension>]
    static member inline GetOrCreateVulkan(this : ResourceCache<nativeptr<'h>>, dataMod : IMod<'a>, additionalKeys : list<obj>, desc : ResourceDescription<'a, 'x>) =
        let handle v = (^x : (member Handle : ^h) (v))
        let res = this.GetOrCreateWrapped(dataMod, additionalKeys, desc, fun r -> new VulkanResource<'x, 'h>(r, handle) :> Rendering.Resource<_>)
        unbox<VulkanResource<'x, 'h>> res

    [<Extension>]
    static member inline GetOrCreateVulkan(this : ResourceCache<nativeptr< ^h >>, keys : list<obj>, create : unit -> Rendering.Resource< ^x >) =
        let handle v = (^x : (member Handle : ^h) (v))
        let wrap (v : Rendering.Resource<'x>) =
            new VulkanResource<'x, 'h>(v, handle) :> Rendering.Resource<nativeptr<'h>>
        let res : IResource<nativeptr<'h>> = this.GetOrCreateWrapped(keys, create, wrap)
        unbox<VulkanResource<'x, 'h>> res