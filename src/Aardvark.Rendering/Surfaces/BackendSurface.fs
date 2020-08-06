namespace Aardvark.Base

open Aardvark.Base
open FSharp.Data.Adaptive
open System.Collections.Generic

type BackendSurface(code: string,
                    entryPoints: Dictionary<ShaderStage, string>,
                    builtIns: Map<ShaderStage, Map<FShade.Imperative.ParameterKind, Set<string>>>,
                    uniforms: SymbolDict<IAdaptiveValue>, samplers: Dictionary<string * int, SamplerDescription>,
                    expectsRowMajorMatrices: bool,
                    iface: obj) =

    interface ISurface
    member x.Code = code
    member x.EntryPoints = entryPoints
    member x.BuiltIns = builtIns
    member x.Uniforms = uniforms
    member x.Samplers = samplers
    member x.ExpectsRowMajorMatrices = expectsRowMajorMatrices
    member x.Interface = iface

    new(code, entryPoints) = BackendSurface(code, entryPoints, Map.empty, SymDict.empty, Dictionary.empty, false, null)

    new(code, entryPoints, builtIns) =
        BackendSurface(code, entryPoints, builtIns, SymDict.empty, Dictionary.empty, false, null)

    new(code, entryPoints, builtIns, uniforms) =
        BackendSurface(code, entryPoints, builtIns, uniforms, Dictionary.empty, false, null)

    new(code, entryPoints, builtIns, uniforms, samplers) =
        BackendSurface(code, entryPoints, builtIns, uniforms, samplers, false, null)