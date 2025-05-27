namespace Aardvark.Rendering

open System
open Aardvark.Base

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ShaderCodeReporting =

    let logLines (message : string) (code : string) =
        let nl = Environment.NewLine
        let numberedLines = String.withLineNumbers code
        Report.Line $"{message}:{nl}{nl}{numberedLines}{nl}"