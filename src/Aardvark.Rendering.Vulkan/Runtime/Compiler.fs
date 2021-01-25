namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Base

open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop
open FSharp.Data.Adaptive
open System.Diagnostics
open System.Collections.Generic

#nowarn "9"
// #nowarn "51"

type CompilerScope =
    {
        runtimeStats : nativeptr<V2i>
    }

module Compiler =
    
    module private VKVM =
        [<Literal>]
        let lib = "vkvm.dll"

        [<DllImport(lib)>]
        extern void vmBindPipeline(VkCommandBuffer buffer, VkPipeline* pipeline)

        [<DllImport(lib)>]
        extern void vmBindDescriptorSets(VkCommandBuffer buffer, DescriptorSetBinding* binding)

        [<DllImport(lib)>]
        extern void vmBindVertexBuffers(VkCommandBuffer buffer, VertexBufferBinding* binding)
        
        [<DllImport(lib)>]
        extern void vmBindIndexBuffer(VkCommandBuffer buffer, IndexBufferBinding* binding)

        [<DllImport(lib)>]
        extern void vmDraw(VkCommandBuffer buffer, V2i* runtimeStats, int* isActive, DrawCall* call)

        module Pointers =

            let path =
                match Environment.OSVersion with
                    | Windows -> "vkvm.dll"
                    | Linux -> System.IO.Path.Combine(Environment.CurrentDirectory, "vkvm.so") |> System.IO.Path.GetFullPath
                    | Mac -> failwithf "cannot load vkvm on Mac"

            let handle = DynamicLinker.loadLibrary path

            let private getProcAddress (name : string) =
                let f = handle.GetFunction name
                if f.Handle = 0n then
                    failf "could not load function %A" name
                f.Handle

            let vmBindPipeline          = getProcAddress "vmBindPipeline"
            let vmBindDescriptorSets    = getProcAddress "vmBindDescriptorSets"
            let vmBindVertexBuffers     = getProcAddress "vmBindVertexBuffers"
            let vmBindIndexBuffer       = getProcAddress "vmBindIndexBuffer"
            let vmDraw                  = getProcAddress "vmDraw"

    type Instruction = nativeint * obj[]

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Instruction =
        let bindPipeline (r : INativeResourceLocation<VkPipeline>) : Instruction =
            VKVM.Pointers.vmBindPipeline, [| r.Pointer :> obj |]

        let bindDescriptorSets (r : INativeResourceLocation<DescriptorSetBinding>) : Instruction =
            VKVM.Pointers.vmBindDescriptorSets, [| r.Pointer :> obj |]

        let bindVertexBuffers (r : INativeResourceLocation<VertexBufferBinding>) : Instruction =
            VKVM.Pointers.vmBindVertexBuffers, [| r.Pointer :> obj |]
            
        let bindIndexBuffer (r : INativeResourceLocation<IndexBufferBinding>) : Instruction =
            VKVM.Pointers.vmBindIndexBuffer, [| r.Pointer :> obj |]

        let draw (stats : nativeptr<V2i>) (isActive : INativeResourceLocation<int>) (r : INativeResourceLocation<DrawCall>) : Instruction =
            VKVM.Pointers.vmDraw, [| stats :> obj; isActive.Pointer :> obj; r.Pointer :> obj |]

    let compileSingle (scope : CompilerScope) (prev : Option<PreparedRenderObject>) (self : PreparedRenderObject) =
        match prev with
            | None ->
                [
                    yield Instruction.bindPipeline self.Pipeline
                    yield Instruction.bindVertexBuffers self.VertexBuffers
                    yield Instruction.bindDescriptorSets self.DescriptorSets

                    match self.IndexBuffer with
                        | Some ib -> yield Instruction.bindIndexBuffer ib
                        | _ -> ()

                    yield Instruction.draw scope.runtimeStats self.IsActive self.DrawCalls
                ]

            | Some prev ->
                [
                    if prev.Pipeline <> self.Pipeline then
                        yield Instruction.bindPipeline self.Pipeline

                    if prev.VertexBuffers <> self.VertexBuffers then
                        yield Instruction.bindVertexBuffers self.VertexBuffers

                    if prev.DescriptorSets <> self.DescriptorSets || prev.Pipeline <> self.Pipeline then
                        yield Instruction.bindDescriptorSets self.DescriptorSets

                    if prev.IndexBuffer <> self.IndexBuffer then
                        match self.IndexBuffer with
                        | Some ib -> yield Instruction.bindIndexBuffer ib
                        | _ -> ()

                    yield Instruction.draw scope.runtimeStats self.IsActive self.DrawCalls
                ]

    let compile (scope : CompilerScope) (prev : Option<PreparedMultiRenderObject>) (self : PreparedMultiRenderObject) =
        let mutable prev = 
            match prev with
                | Some p -> Some p.Last
                | None -> None

        [
            for o in self.Children do
                yield! compileSingle scope prev o
                prev <- Some o
        ]


