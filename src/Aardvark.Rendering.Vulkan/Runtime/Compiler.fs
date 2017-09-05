namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop
open Aardvark.Base.Incremental
open System.Diagnostics
open System.Collections.Generic

#nowarn "9"
#nowarn "51"

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
            VKVM.Pointers.vmBindPipeline, [| r.Update(AdaptiveToken.Top).handle :> obj |]

        let bindDescriptorSets (r : INativeResourceLocation<DescriptorSetBinding>) : Instruction =
            VKVM.Pointers.vmBindDescriptorSets, [| r.Update(AdaptiveToken.Top).handle :> obj |]

        let bindVertexBuffers (r : INativeResourceLocation<VertexBufferBinding>) : Instruction =
            VKVM.Pointers.vmBindVertexBuffers, [| r.Update(AdaptiveToken.Top).handle :> obj |]
            
        let bindIndexBuffer (r : INativeResourceLocation<IndexBufferBinding>) : Instruction =
            VKVM.Pointers.vmBindIndexBuffer, [| r.Update(AdaptiveToken.Top).handle :> obj |]

        let draw (stats : nativeptr<V2i>) (isActive : INativeResourceLocation<int>) (r : INativeResourceLocation<DrawCall>) : Instruction =
            VKVM.Pointers.vmDraw, [| stats :> obj; isActive.Update(AdaptiveToken.Top).handle :> obj; r.Update(AdaptiveToken.Top).handle :> obj |]

    let compileSingle (scope : CompilerScope) (prev : Option<PreparedRenderObject>) (self : PreparedRenderObject) =
        match prev with
            | None ->
                [
                    yield Instruction.bindPipeline self.pipeline
                    yield Instruction.bindVertexBuffers self.vertexBuffers
                    yield Instruction.bindDescriptorSets self.descriptorSets

                    match self.indexBuffer with
                        | Some ib -> yield Instruction.bindIndexBuffer ib
                        | _ -> ()

                    yield Instruction.draw scope.runtimeStats self.isActive self.drawCalls
                ]

            | Some prev ->
                [
                    if prev.pipeline <> self.pipeline then
                        yield Instruction.bindPipeline self.pipeline

                    if prev.vertexBuffers <> self.vertexBuffers then
                        yield Instruction.bindVertexBuffers self.vertexBuffers

                    if prev.descriptorSets <> self.descriptorSets || prev.pipeline <> self.pipeline then
                        yield Instruction.bindDescriptorSets self.descriptorSets

                    if prev.indexBuffer <> self.indexBuffer then
                        match self.indexBuffer with
                            | Some ib -> yield Instruction.bindIndexBuffer ib
                            | _ -> ()

                    yield Instruction.draw scope.runtimeStats self.isActive self.drawCalls
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


