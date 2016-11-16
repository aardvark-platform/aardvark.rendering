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
        extern void vmDraw(VkCommandBuffer buffer, V2i* runtimeStats, int* isActive, DrawCall* call)

        module Pointers =
            let handle = DynamicLinker.loadLibrary lib

            let private getProcAddress (name : string) =
                let f = handle.GetFunction name
                if f.Handle = 0n then
                    failf "could not load function %A" name
                f.Handle

            let vmBindPipeline          = getProcAddress "vmBindPipeline"
            let vmBindDescriptorSets    = getProcAddress "vmBindDescriptorSets"
            let vmBindVertexBuffers     = getProcAddress "vmBindVertexBuffers"
            let vmDraw                  = getProcAddress "vmDraw"

    type Instruction = nativeint * obj[]

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Instruction =
        let bindPipeline (r : VulkanResource<Pipeline, VkPipeline>) : Instruction =
            VKVM.Pointers.vmBindPipeline, [| r.Pointer :> obj |]

        let bindDescriptorSets (r : IResource<nativeptr<DescriptorSetBinding>>) : Instruction =
            VKVM.Pointers.vmBindDescriptorSets, [| r.Handle.GetValue() :> obj |]

        let bindVertexBuffers (r : IResource<nativeptr<VertexBufferBinding>>) : Instruction =
            VKVM.Pointers.vmBindVertexBuffers, [| r.Handle.GetValue() :> obj |]

        let draw (stats : nativeptr<V2i>) (isActive : IResource<nativeint>) (r : IResource<nativeptr<DrawCall>>) : Instruction =
            VKVM.Pointers.vmDraw, [| stats :> obj; isActive.Handle.GetValue() :> obj; r.Handle.GetValue() :> obj |]

    let compileSingle (scope : CompilerScope) (prev : Option<PreparedRenderObject>) (self : PreparedRenderObject) =
        match prev with
            | None ->
                [
                    Instruction.bindPipeline self.pipeline
                    Instruction.bindVertexBuffers self.vertexBuffers
                    Instruction.bindDescriptorSets self.descriptorSets
                    Instruction.draw scope.runtimeStats self.isActive self.drawCalls
                ]

            | Some prev ->
                [
                    if prev.pipeline <> self.pipeline then
                        yield Instruction.bindPipeline self.pipeline

                    if prev.vertexBuffers <> self.vertexBuffers then
                        yield Instruction.bindVertexBuffers self.vertexBuffers

                    if prev.descriptorSets <> self.descriptorSets || prev.pipeline <> self.pipeline then
                        yield Instruction.bindDescriptorSets self.descriptorSets

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


