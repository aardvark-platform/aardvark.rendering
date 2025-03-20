namespace Aardvark.Rendering.Vulkan

#nowarn "9"

open Aardvark.Base
open System
open System.Runtime.InteropServices
open FSharp.NativeInterop

[<Struct>]
type internal nativeptr =
    {
        Type : Type
        Handle : nativeint
    }

[<AutoOpen>]
module internal UntypedNativePtrExtensions =

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module NativePtr =

        /// Converts the given untyped nativeptr to a nativeptr<'T>
        /// Fails if the wrapped pointer is not of the expected type.
        let typed<'T when 'T : unmanaged> (ptr : nativeptr) : nativeptr<'T> =
            if ptr.Type = typeof<'T> then
                NativePtr.ofNativeInt ptr.Handle
            else
                failwithf "cannot cast nativeptr, expected type %A but has type %A" typeof<'T> ptr.Type

        /// Creates an untyped nativeptr from a nativeptr<'T>
        let untyped (ptr : nativeptr<'T>) =
            { Type = typeof<'T>; Handle = NativePtr.toNativeInt ptr }


/// Utility struct to build chains of Vulkan structs connected via their pNext fields.
type internal VkStructChain =
    val mutable Chain : list<nativeptr>

    new() = { Chain = [] }

    /// Returns the handle of the chain head.
    member x.Handle =
        match x.Chain with
        | [] -> 0n
        | ptr::_ -> ptr.Handle

    /// Returns the length of the chain.
    member x.Length =
        List.length x.Chain

    /// Clears the chain, freeing all handles.
    member x.Clear() =
        x.Chain |> List.iter (fun ptr -> Marshal.FreeHGlobal ptr.Handle )
        x.Chain <- []

    /// Adds a struct to the beginning of the chain.
    /// Returns a pointer to the passed struct, which is valid until the chain is cleared or disposed.
    member inline x.Add< ^T when ^T : unmanaged and ^T : (member set_pNext : nativeint -> unit)>(obj : ^T) =
        let ptr = NativePtr.alloc 1

        let mutable tmp = obj
        (^T : (member set_pNext : nativeint -> unit) (tmp, x.Handle))
        x.Chain <- NativePtr.untyped ptr :: x.Chain

        NativePtr.write ptr tmp
        ptr

    /// Adds an empty struct to the beginning of the chain.
    /// Returns a pointer to the new struct, which is valid until the chain is cleared or disposed.
    member inline x.Add< ^T when ^T : unmanaged and ^T : (member set_pNext : nativeint -> unit) and ^T : (static member Empty : ^T)>() =
        let empty = (^T : (static member Empty : ^T) ())
        x.Add(empty)

    interface IDisposable with
        member x.Dispose() = x.Clear()

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal VkStructChain =

    /// Creats an empty struct chain.
    let empty() =
        new VkStructChain()

    /// Casts the chain to a nativeptr. Fails if type of the head does not match.
    let toNativePtr (chain : VkStructChain) : nativeptr<'T> =
        match chain.Chain with
        | [] -> NativePtr.zero
        | ptr::_ -> NativePtr.typed ptr

    /// Adds a struct to the beginning of the chain.
    let inline add (value : ^T) (chain : VkStructChain) =
        chain.Add(value) |> ignore
        chain

    /// Adds an empty struct to the beginning of the chain.
    let inline addEmpty< ^T when ^T : unmanaged and ^T : (member set_pNext : nativeint -> unit) and ^T : (static member Empty : ^T)> (chain : VkStructChain) =
        chain.Add< ^T>() |> ignore
        chain