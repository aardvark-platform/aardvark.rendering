namespace Aardvark.Rendering.GL

open Aardvark.Base
open Aardvark.Base.Monads.State
open Aardvark.Assembler
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering
open FSharp.Data.Adaptive
open FSharp.NativeInterop
open System
open System.Collections.Generic
open System.Runtime.InteropServices

#nowarn "9"

module internal ComputeTaskInternals =
    open PreparedPipelineState

    type ComputeInputBinding(manager : ResourceManager, shader : IComputeShader, inputs : IUniformProvider) =

        // The ResourceManager already provides reference counting, but immediately adds a reference
        // and allocates resources for some reason. We don't want that here (no IDisposable), so we have to do our own
        // reference counting on top of all that.
        let mutable refCount = 0

        let slots = manager.GetInterfaceSlots shader.Interface
        let mutable uniformBuffers = Array.empty
        let mutable storageBuffers = Array.empty
        let mutable textureBindings = Array.empty
        let mutable imageBindings = Array.empty
        let mutable resources : IResource[] = Array.empty

        let provider = UniformProvider.computeInputs inputs

        let create() =
            uniformBuffers <- manager.CreateUniformBuffers(slots, provider, Ag.Scope.Root)
            storageBuffers <- manager.CreateStorageBuffers(slots, provider, Ag.Scope.Root)
            textureBindings <- manager.CreateTextureBindings(slots, provider, Ag.Scope.Root)
            imageBindings <- manager.CreateImageBindings(slots, provider, Ag.Scope.Root)

            resources <-
                [|
                    for struct (_, b) in uniformBuffers do yield b
                    for struct (_, b) in storageBuffers do yield b

                    for struct (_, tb) in textureBindings do
                        match tb with
                        | ArrayBinding ta -> yield ta
                        | SingleBinding (tex, sam) -> yield tex; yield sam

                    for struct (_, b) in imageBindings do yield b
                |]

        let destroy() =
            let resourceLock =
                try Some manager.Context.ResourceLock
                with :? ObjectDisposedException -> None

            match resourceLock with
            | Some l ->
                use __ = l

                for r in resources do
                    r.Dispose()

                uniformBuffers <- Array.empty
                storageBuffers <- Array.empty
                textureBindings <- Array.empty
                imageBindings <- Array.empty
                resources <- Array.empty

            | _ ->
                ()

        member x.UniformBuffers = uniformBuffers
        member x.StorageBuffers = storageBuffers
        member x.TextureBindings = textureBindings
        member x.ImageBindings = imageBindings
        member x.Resources = resources

        member x.Acquire() =
            lock x (fun _ ->
                inc &refCount

                if refCount = 1 then
                    create()
            )

        member x.Release() =
            lock x (fun _ ->
                dec &refCount

                if refCount = 0 then
                    destroy()
            )

        interface IComputeInputBinding with
            member x.Shader = shader

    [<AutoOpen>]
    module private ResourceInputSetExtensions =

        type ResourceInputSet with
            member x.Add(binding : ComputeInputBinding) =
                for r in binding.Resources do x.Add r

            member x.Remove(binding : ComputeInputBinding) =
                for r in binding.Resources do x.Remove r

    module private MemoryBarrierFlags =

        module private Conversion =

            let ofResourceAccess =
                Map.ofList [
                    ResourceAccess.VertexAttributeRead, MemoryBarrierFlags.VertexAttribArrayBarrierBit

                    ResourceAccess.IndexRead,           MemoryBarrierFlags.ElementArrayBarrierBit

                    ResourceAccess.UniformRead,         MemoryBarrierFlags.UniformBarrierBit

                    ResourceAccess.ShaderRead,          MemoryBarrierFlags.UniformBarrierBit |||
                                                        MemoryBarrierFlags.TextureFetchBarrierBit |||
                                                        MemoryBarrierFlags.ShaderImageAccessBarrierBit |||
                                                        MemoryBarrierFlags.ShaderStorageBarrierBit |||
                                                        MemoryBarrierFlags.AtomicCounterBarrierBit

                    ResourceAccess.ShaderWrite,         MemoryBarrierFlags.ShaderImageAccessBarrierBit |||
                                                        MemoryBarrierFlags.ShaderStorageBarrierBit |||
                                                        MemoryBarrierFlags.AtomicCounterBarrierBit

                    ResourceAccess.IndirectCommandRead, MemoryBarrierFlags.CommandBarrierBit

                    ResourceAccess.HostRead,            MemoryBarrierFlags.PixelBufferBarrierBit |||
                                                        MemoryBarrierFlags.ClientMappedBufferBarrierBit |||
                                                        MemoryBarrierFlags.TextureUpdateBarrierBit |||
                                                        MemoryBarrierFlags.BufferUpdateBarrierBit

                    ResourceAccess.HostWrite,           MemoryBarrierFlags.PixelBufferBarrierBit |||
                                                        MemoryBarrierFlags.ClientMappedBufferBarrierBit |||
                                                        MemoryBarrierFlags.TextureUpdateBarrierBit |||
                                                        MemoryBarrierFlags.BufferUpdateBarrierBit

                    ResourceAccess.TransferRead,        MemoryBarrierFlags.PixelBufferBarrierBit |||
                                                        MemoryBarrierFlags.TextureUpdateBarrierBit |||
                                                        MemoryBarrierFlags.BufferUpdateBarrierBit

                    ResourceAccess.TransferWrite,       MemoryBarrierFlags.PixelBufferBarrierBit |||
                                                        MemoryBarrierFlags.TextureUpdateBarrierBit |||
                                                        MemoryBarrierFlags.BufferUpdateBarrierBit

                    ResourceAccess.ColorRead,           MemoryBarrierFlags.FramebufferBarrierBit

                    ResourceAccess.ColorWrite,          MemoryBarrierFlags.FramebufferBarrierBit

                    ResourceAccess.DepthStencilRead,    MemoryBarrierFlags.FramebufferBarrierBit

                    ResourceAccess.DepthStencilWrite,   MemoryBarrierFlags.FramebufferBarrierBit
                ]

        module private Enum =

            let inline convertFlags< ^T, ^U when ^T : comparison and ^T :> Enum and
                                                 ^U : (static member (|||) : ^U -> ^U -> ^U)> (lookup : Map< ^T, ^U>) (none : ^U) (value : ^T) =
                let mutable result = none

                lookup |> Map.iter (fun src dst ->
                    if value.HasFlag src then result <- result ||| dst
                )

                result

        type MemoryBarrierFlags with
            static member None = unbox<MemoryBarrierFlags> 0

        let ofResourceAccess (access : ResourceAccess) =
            access |> Enum.convertFlags Conversion.ofResourceAccess MemoryBarrierFlags.None

        let ofTextureLayout =
            LookupTable.lookup [
                TextureLayout.Undefined,        MemoryBarrierFlags.None

                TextureLayout.TransferRead,     MemoryBarrierFlags.PixelBufferBarrierBit |||
                                                MemoryBarrierFlags.TextureUpdateBarrierBit

                TextureLayout.TransferWrite,    MemoryBarrierFlags.PixelBufferBarrierBit |||
                                                MemoryBarrierFlags.TextureUpdateBarrierBit

                TextureLayout.ShaderRead,       MemoryBarrierFlags.TextureFetchBarrierBit |||
                                                MemoryBarrierFlags.ShaderImageAccessBarrierBit |||
                                                MemoryBarrierFlags.AtomicCounterBarrierBit

                TextureLayout.ShaderWrite,      MemoryBarrierFlags.ShaderImageAccessBarrierBit |||
                                                MemoryBarrierFlags.AtomicCounterBarrierBit

                TextureLayout.ShaderReadWrite,  MemoryBarrierFlags.TextureFetchBarrierBit |||
                                                MemoryBarrierFlags.ShaderImageAccessBarrierBit |||
                                                MemoryBarrierFlags.AtomicCounterBarrierBit

                TextureLayout.ColorAttachment,  MemoryBarrierFlags.FramebufferBarrierBit

                TextureLayout.DepthStencil,     MemoryBarrierFlags.FramebufferBarrierBit

                TextureLayout.DepthStencilRead, MemoryBarrierFlags.FramebufferBarrierBit

                TextureLayout.General,          MemoryBarrierFlags.TextureFetchBarrierBit |||
                                                MemoryBarrierFlags.TextureUpdateBarrierBit |||
                                                MemoryBarrierFlags.ShaderImageAccessBarrierBit |||
                                                MemoryBarrierFlags.AtomicCounterBarrierBit |||
                                                MemoryBarrierFlags.PixelBufferBarrierBit |||
                                                MemoryBarrierFlags.FramebufferBarrierBit

                TextureLayout.Present,          MemoryBarrierFlags.AllBarrierBits
            ]

    [<AutoOpen>]
    module private Compiler =

        type Array with
            member x.ElementSize = nativeint (Marshal.SizeOf (x.GetType().GetElementType()))

            member inline x.Range(index : int, sizeInBytes : nativeint) =
                let length = x.Length - index
                let elementSize = x.ElementSize
                let offset = nativeint index * elementSize
                let sizeInBytes = min sizeInBytes (nativeint length * elementSize)
                struct (offset, sizeInBytes)

        type AssemblerProgram(debug : bool) =
            let ms = new SystemMemoryStream()
            let asm = AssemblerStream.create ms
            let cmd = asm |> CommandStream.create debug
            do asm.BeginFunction()

            let mutable executable = 0n
            let mutable entry = null

            let pinnable = Dict<obj * nativeint, nativeptr<nativeint>>()
            let pinned = List<GCHandle>()

            let finish() =
                assert (isNull entry)

                asm.EndFunction()
                asm.Ret()

                executable <- JitMem.Alloc(nativeint ms.Length)
                JitMem.Copy(ms.ToMemory(), executable)

                entry <- Marshal.GetDelegateForFunctionPointer<Action> executable

            member x.Stream =
                assert (isNull entry)
                cmd

            member x.AddPinnable(value : obj, offset : nativeint) =
                pinnable.GetOrCreate((value, offset), fun _ ->
                    NativePtr.alloc<nativeint> 1
                )

            member inline x.AddPinnable(value : obj) =
                x.AddPinnable(value, 0n)

            member x.Finish() =
                if isNull entry then finish()

            member x.Run() =
                try
                    for KeyValue((value, offset), ptr) in pinnable do
                        let gc = GCHandle.Alloc(value, GCHandleType.Pinned)
                        (gc.AddrOfPinnedObject() + offset) |> NativePtr.write ptr
                        pinned.Add gc

                    entry.Invoke()

                finally
                    for gc in pinned do gc.Free()
                    pinned.Clear()

            member x.Dispose() =
                if executable <> 0n then
                    JitMem.Free(executable, nativeint ms.Length)
                    executable <- 0n
                    entry <- null

                asm.Dispose()
                ms.Dispose()

            interface IDisposable with
                member x.Dispose() = x.Dispose()

        [<RequireQualifiedAccess>]
        type CompiledCommand =
            | Custom of (Context -> AdaptiveToken -> RenderToken -> unit)
            | Program of AssemblerProgram

            static member inline Execute(task : IComputeTask) =
                Custom (fun _ t rt -> task.Run(t, rt))

            member inline x.Run(context : Context, token : AdaptiveToken, renderToken : RenderToken) =
                match x with
                | Custom f -> f context token renderToken
                | Program asm -> asm.Run()

            member x.Dispose() =
                match x with
                | Program p -> p.Dispose()
                | _ -> ()

            interface IDisposable with
                member x.Dispose() = x.Dispose()

        type private CompilerState =
            { Commands : CompiledCommand list
              Debug    : bool }

        module private CompilerState =

            let empty (debug : bool) =
                { Commands = []; Debug = debug }

            let inline assemble (write : AssemblerProgram -> ICommandStream -> unit) =
                State.modify (fun s ->
                    let cmd, p =
                        match s.Commands with
                        | (CompiledCommand.Program p)::_ -> s.Commands, p
                        | _ ->
                            let p = new AssemblerProgram(s.Debug)
                            CompiledCommand.Program p :: s.Commands, p

                    write p p.Stream
                    { s with Commands = cmd }
                )

            let inline runtime (f : IRuntime -> unit) =
                State.modify (fun s ->
                    { s with Commands = (CompiledCommand.Custom (fun ctx _ _ -> f ctx.Runtime)) :: s.Commands }
                )

            let inline execute (other : IComputeTask) =
                State.modify (fun s ->
                    { s with Commands = (CompiledCommand.Execute other) :: s.Commands }
                )

        module private ComputeCommand =

            let private compileS (cmd : ComputeCommand) : State<CompilerState, unit> =
                state {
                    match cmd with
                    | ComputeCommand.BindCmd shader ->
                        do! CompilerState.assemble (fun _ s ->
                            s.UseProgram shader.Handle
                        )

                    | ComputeCommand.SetInputCmd input ->
                        let input = unbox<ComputeInputBinding> input

                        do! CompilerState.assemble (fun _ s ->
                            for struct (slot, ub) in input.UniformBuffers do
                                s.BindUniformBufferView(slot, ub)

                            for struct (slot, ssb) in input.StorageBuffers do
                                s.BindStorageBuffer(slot, ssb)

                            for struct (slots, binding) in input.TextureBindings do
                                match binding with
                                | SingleBinding (tex, sam) ->
                                    s.SetActiveTexture(slots.Min)
                                    s.BindTexture(tex)
                                    s.BindSampler(slots.Min, sam)
                                | ArrayBinding ta ->
                                    s.BindTexturesAndSamplers(ta)

                            for struct (slot, binding) in input.ImageBindings do
                                s.BindImageTexture(slot, TextureAccess.ReadWrite, binding.Pointer)
                        )

                    | ComputeCommand.DispatchCmd groups ->
                        do! CompilerState.assemble (fun _ s ->
                            s.DispatchCompute(groups.X, groups.Y, groups.Z)
                        )

                    | ComputeCommand.ExecuteCmd other ->
                        do! CompilerState.execute other

                    | ComputeCommand.CopyBufferCmd (src, dst) ->
                        let srcBuffer = unbox<GL.Buffer> src.Buffer
                        let dstBuffer = unbox<GL.Buffer> dst.Buffer
                        let size = min src.SizeInBytes dst.SizeInBytes

                        do! CompilerState.assemble (fun _ s ->
                            if GL.ARB_direct_state_access then
                                s.CopyNamedBufferSubData(srcBuffer.Handle, dstBuffer.Handle, src.Offset, dst.Offset, size)
                            else
                                s.BindBuffer(BufferTarget.CopyReadBuffer, srcBuffer.Handle)
                                s.BindBuffer(BufferTarget.CopyWriteBuffer, dstBuffer.Handle)
                                s.CopyBufferSubData(BufferTarget.CopyReadBuffer, BufferTarget.CopyWriteBuffer, src.Offset, dst.Offset, size)
                        )

                    | ComputeCommand.DownloadBufferCmd (src, dst) ->
                        let srcBuffer = src.Buffer |> unbox<GL.Buffer>

                        do! CompilerState.assemble (fun p s ->
                            match dst with
                            | HostMemory.Unmanaged ptr ->
                                if GL.ARB_direct_state_access then
                                    s.GetNamedBufferSubData(srcBuffer.Handle, src.Offset, src.SizeInBytes, ptr)
                                else
                                    s.BindBuffer(BufferTarget.CopyReadBuffer, srcBuffer.Handle)
                                    s.GetBufferSubData(BufferTarget.CopyReadBuffer, src.Offset, src.SizeInBytes, ptr)

                            | HostMemory.Managed (arr, index) ->
                                let struct (offset, sizeInBytes) = arr.Range(index, src.SizeInBytes)
                                let pDst = p.AddPinnable(arr, offset)

                                if GL.ARB_direct_state_access then
                                    s.GetNamedBufferSubDataPtr(srcBuffer.Handle, src.Offset, sizeInBytes, pDst)
                                else
                                    s.BindBuffer(BufferTarget.CopyReadBuffer, srcBuffer.Handle)
                                    s.GetBufferSubDataPtr(BufferTarget.CopyReadBuffer, src.Offset, sizeInBytes, pDst)
                        )

                    | ComputeCommand.UploadBufferCmd (src, dst) ->
                        let dstBuffer = dst.Buffer |> unbox<GL.Buffer>

                        do! CompilerState.assemble (fun p s ->
                            match src with
                            | HostMemory.Unmanaged ptr ->
                                if GL.ARB_direct_state_access then
                                    s.NamedBufferSubData(dstBuffer.Handle, dst.Offset, dst.SizeInBytes, ptr)
                                else
                                    s.BindBuffer(BufferTarget.CopyWriteBuffer, dstBuffer.Handle)
                                    s.BufferSubData(BufferTarget.CopyWriteBuffer, dst.Offset, dst.SizeInBytes, ptr)

                            | HostMemory.Managed (arr, index) ->
                                let struct (offset, sizeInBytes) = arr.Range(index, dst.SizeInBytes)
                                let pSrc = p.AddPinnable(arr, offset)

                                if GL.ARB_direct_state_access then
                                    s.NamedBufferSubDataPtr(dstBuffer.Handle, dst.Offset, sizeInBytes, pSrc)
                                else
                                    s.BindBuffer(BufferTarget.CopyWriteBuffer, dstBuffer.Handle)
                                    s.BufferSubDataPtr(BufferTarget.CopyWriteBuffer, dst.Offset, sizeInBytes, pSrc)
                        )

                    | ComputeCommand.SetBufferCmd (range, value) ->
                        let buffer = unbox<GL.Buffer> range.Buffer

                        do! CompilerState.assemble (fun p s ->
                            let pValue = p.AddPinnable value

                            if GL.ARB_direct_state_access then
                                s.ClearNamedBufferSubData(
                                    buffer.Handle, PixelInternalFormat.R32ui,
                                    range.Offset, range.SizeInBytes, PixelFormat.RedInteger, PixelType.UnsignedInt,
                                    pValue
                                )
                            else
                                s.BindBuffer(BufferTarget.CopyWriteBuffer, buffer.Handle)
                                s.ClearBufferSubData(
                                    BufferTarget.CopyWriteBuffer, PixelInternalFormat.R32ui,
                                    range.Offset, range.SizeInBytes, PixelFormat.RedInteger, PixelType.UnsignedInt,
                                    pValue
                                )
                        )

                    | ComputeCommand.CopyImageCmd (src, srcOffset, dst, dstOffset, size) ->
                        do! CompilerState.runtime (fun runtime ->
                            runtime.Copy(src, srcOffset, dst, dstOffset, size)
                        )

                    | ComputeCommand.TransformSubLayoutCmd (_, _, layout)
                    | ComputeCommand.TransformLayoutCmd (_, layout) ->
                        do! CompilerState.assemble (fun _ s ->
                            let flags = MemoryBarrierFlags.ofTextureLayout layout
                            s.MemoryBarrier flags
                        )

                    | ComputeCommand.SyncImageCmd (_, _, access)
                    | ComputeCommand.SyncBufferCmd (_, _, access) ->
                        do! CompilerState.assemble (fun _ s ->
                            let flags = MemoryBarrierFlags.ofResourceAccess access
                            s.MemoryBarrier flags
                        )
                }

            let compile (debug : bool) (cmds : seq<ComputeCommand>) =
                let mutable compiled = CompilerState.empty debug

                for cmd in cmds do
                    let c = compileS cmd
                    c.Run(&compiled)

                compiled.Commands |> List.iter (function
                    | CompiledCommand.Program p -> p.Finish()
                    | _ -> ()
                )

                List.rev compiled.Commands

        type CommandCompiler(owner : IAdaptiveObject, manager : ResourceManager, resources : ResourceInputSet, input : alist<ComputeCommand>, debug : bool) =
            let reader = input.GetReader()
            let inputs = Dict<Index, ComputeInputBinding>()
            let nested = Dict<Index, IComputeTask>()
            let hooked = ReferenceCountingSet()
            let mutable commands = IndexList<ComputeCommand>.Empty
            let mutable compiled : CompiledCommand list = []

            let add (index : Index) (command : ComputeCommand) =
                match command with
                | ComputeCommand.SetInputCmd input ->
                    let input =
                        match input with
                        | :? ComputeInputBinding as input ->
                            input

                        | :? MutableComputeInputBinding as binding ->
                            ComputeInputBinding(manager, binding.Shader, binding)

                        | _ ->
                            failf "unknown input binding type %A" (input.GetType())

                    input.Acquire()
                    resources.Add input
                    inputs.[index] <- input

                    ComputeCommand.SetInputCmd input

                | ComputeCommand.BindCmd (:? HookedComputeProgram as p) ->
                    hooked.Add p |> ignore
                    command

                | ComputeCommand.ExecuteCmd other ->
                    nested.[index] <- other
                    command

                | _ -> command

            let remove (removedInputs : System.Collections.Generic.List<_>) (index : Index) =
                match inputs.TryRemove(index) with
                | (true, input) -> removedInputs.Add input
                | _ ->
                    match nested.TryRemove(index) with
                    | (true, other) -> other.Outputs.Remove owner |> ignore
                    | _ -> ()

                match commands.TryGet index with
                | Some (ComputeCommand.BindCmd (:? HookedComputeProgram as p)) ->
                    if hooked.Remove p then
                        p.Outputs.Remove owner |> ignore

                | _ ->
                    ()

            member x.Commands = compiled

            member x.Update(token : AdaptiveToken) =
                let deltas = reader.GetChanges(token)
                let removedInputs = System.Collections.Generic.List<ComputeInputBinding>()

                // Process deltas
                for (index, op) in deltas do
                    remove removedInputs index

                    match op with
                    | Set cmd ->
                        let cmd = cmd |> add index
                        commands <- commands |> IndexList.set index cmd

                    | Remove ->
                        commands <- commands |> IndexList.remove index

                // Delay removing inputs from the resource set
                // This way nothing will be released if the input just moved in the command list
                for input in removedInputs do
                    input.Release()
                    resources.Remove input

                // Update all hooked compute programs
                let mutable changed = deltas.Count > 0

                for p in hooked do
                    if p.Update token then changed <- true

                // Compile updated command list
                if changed then
                    for cmd in compiled do cmd.Dispose()
                    compiled <- commands |> ComputeCommand.compile debug

            member x.Dispose() =
                for KeyValue(_, input) in inputs do
                    resources.Remove input
                    input.Release()
                inputs.Clear()

                for KeyValue(_, task) in nested do
                    task.Outputs.Remove owner |> ignore
                nested.Clear()

                hooked.Clear()
                commands <- IndexList.empty
                for cmd in compiled do cmd.Dispose()
                compiled <- []

            interface IDisposable with
                member x.Dispose() = x.Dispose()

    type ComputeTask(manager : ResourceManager, input : alist<ComputeCommand>, debug : bool) as this =
        inherit AdaptiveObject()

        let ctx = manager.Context
        let resources = new ResourceInputSet()
        let compiler = new CommandCompiler(this, manager, resources, input, debug)

        let update (token : AdaptiveToken) (renderToken : RenderToken) (action : unit -> unit) =
            use __ = renderToken.Use()
            use __ = ctx.ResourceLock
            use __ = GlobalResourceLock.lock()

            ctx.PushDebugGroup(this.Name ||? "Compute Task")

            resources.Update(token, renderToken)
            compiler.Update(token)

            action()

            ctx.PopDebugGroup()

        member val Name : string = null with get, set

        member x.Update(token : AdaptiveToken, renderToken : RenderToken) =
            x.EvaluateIfNeeded token () (fun token ->
                update token renderToken ignore
            )

        member x.Run(token : AdaptiveToken, renderToken : RenderToken) =
            x.EvaluateAlways token (fun token ->
                update token renderToken (fun _ ->
                    for cmd in compiler.Commands do
                        cmd.Run(ctx, token, renderToken)
                )
            )

        member x.Dispose() =
            lock x (fun _ ->
                compiler.Dispose()
                resources.Dispose()
            )

        interface IComputeTask with
            member x.Name with get() = x.Name and set name = x.Name <- name
            member x.Runtime = manager.Context.Runtime
            member x.Update(token, renderToken) = x.Update(token, renderToken)
            member x.Run(token, renderToken) = x.Run(token, renderToken)
            member x.Dispose() = x.Dispose()