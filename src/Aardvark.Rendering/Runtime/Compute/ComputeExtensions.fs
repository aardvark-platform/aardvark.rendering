namespace Aardvark.Rendering

open FSharp.Data.Adaptive
open Aardvark.Base
open System
open System.Runtime.CompilerServices

[<AbstractClass; Sealed; Extension>]
type IComputeTaskExtensions private() =

    [<Extension>]
    static member Update(task : IComputeTask, token : AdaptiveToken) =
        task.Update(token, RenderToken.Empty)

    [<Extension>]
    static member Update(task : IComputeTask, renderToken : RenderToken) =
        task.Update(AdaptiveToken.Top, renderToken)

    [<Extension>]
    static member Update(task : IComputeTask) =
        task.Update(AdaptiveToken.Top, RenderToken.Empty)

    [<Extension>]
    static member Run(task : IComputeTask, token : AdaptiveToken) =
        task.Run(token, RenderToken.Empty)

    [<Extension>]
    static member Run(task : IComputeTask, renderToken : RenderToken) =
        task.Run(AdaptiveToken.Top, renderToken)

    [<Extension>]
    static member Run(task : IComputeTask) =
        task.Run(AdaptiveToken.Top, RenderToken.Empty)


[<AbstractClass; Sealed; Extension>]
type IComputeRuntimeExtensions private() =

    [<Extension>]
    static member CreateComputeShader(runtime : IComputeRuntime, shader : 'a -> 'b) =
        runtime.CreateComputeShader(
            FShade.ComputeShader.ofFunction runtime.MaxLocalSize shader
        )

    [<Extension>]
    static member DeleteComputeShader(runtime : IComputeRuntime, shader : IComputeShader) =
        shader.Dispose()

    [<Extension>]
    static member CompileCompute(runtime : IComputeRuntime, commands : aval<ComputeCommand list>) =
        runtime.CompileCompute(commands |> AList.ofAVal)

    [<Extension>]
    static member CompileCompute(runtime : IComputeRuntime, commands : list<ComputeCommand>) =
        runtime.CompileCompute(commands |> AList.ofList)

    [<Extension>]
    static member Run(runtime : IComputeRuntime, commands : list<ComputeCommand>, renderToken : RenderToken) =
        use task = runtime.CompileCompute(commands)
        task.Run(AdaptiveToken.Top, renderToken)

    [<Extension>]
    static member Run(runtime : IComputeRuntime, commands : list<ComputeCommand>) =
        runtime.Run(commands, RenderToken.Empty)


[<AbstractClass; Sealed; Extension>]
type IComputeShaderExtensions private() =

    [<Extension>]
    static member CreateInputBinding(shader : IComputeShader, inputs : IUniformProvider) =
        shader.Runtime.CreateInputBinding(shader, inputs)

    [<Extension>]
    static member Invoke(shader : IComputeShader, groupCount : V3i, input : IComputeInputBinding, renderToken : RenderToken) =
        shader.Runtime.Run([
            ComputeCommand.Bind shader
            ComputeCommand.SetInput input
            ComputeCommand.Dispatch groupCount
        ], renderToken)

    [<Extension>]
    static member Invoke(shader : IComputeShader, groupCount : V2i, input : IComputeInputBinding, renderToken : RenderToken) =
        shader.Invoke(V3i(groupCount, 1), input, renderToken)

    [<Extension>]
    static member Invoke(shader : IComputeShader, groupCount : int, input : IComputeInputBinding, renderToken : RenderToken) =
        shader.Invoke(V3i(groupCount, 1, 1), input, renderToken)

    [<Extension>]
    static member Invoke(shader : IComputeShader, groupCount : V3i, input : IComputeInputBinding) =
        shader.Invoke(groupCount, input, RenderToken.Empty)

    [<Extension>]
    static member Invoke(shader : IComputeShader, groupCount : V2i, input : IComputeInputBinding) =
        shader.Invoke(groupCount, input, RenderToken.Empty)

    [<Extension>]
    static member Invoke(shader : IComputeShader, groupCount : int, input : IComputeInputBinding) =
        shader.Invoke(groupCount, input, RenderToken.Empty)


[<AutoOpen>]
module ``InputBinding Builder`` =

    type InputBindingBuilder(shader : IComputeShader) =
        inherit UniformMapBuilder()

        member x.Run(map : UniformMap) =
            shader.CreateInputBinding map

    type IComputeShader with
        member x.inputBinding = InputBindingBuilder x

[<AutoOpen>]
module ``UniformProvider Compute Extensions`` =

    type UniformProvider with

        /// Creates a uniform provider for compute inputs that looks up values
        /// by removing a cs_ prefix in the name, if no value was found for the original name.
        static member computeInputs (inputs : IUniformProvider) =
            { new IUniformProvider with
                member x.Dispose() = inputs.Dispose()
                member x.TryGetUniform(scope, name) =
                    match inputs.TryGetUniform(scope, name) with
                    | None ->
                        let name = name.ToString()
                        if name.StartsWith "cs_" then
                            let sem = Sym.ofString <| name.Substring(3)
                            inputs.TryGetUniform(scope, sem)
                        else
                            None

                    | res -> res
            }