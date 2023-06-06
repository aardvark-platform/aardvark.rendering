namespace Aardvark.Rendering

open System
open System.Text.RegularExpressions
open Aardvark.Base

// TODO: Replace with String.* utilities when updating to Aardvark.Base >= 5.2.26

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ShaderCodeReporting =

    let normalizeLineEndings (str : string) =
        Regex.Replace(str, @"\r\n?|\n", Environment.NewLine)

    let withLineNumbers (code : string) : string =
        let str = normalizeLineEndings code
        let lines = str.Split([| Environment.NewLine |], StringSplitOptions.None)
        let lineColumns = 1 + int (Fun.Log10 lines.Length)

        lines
        |> Array.indexed
        |> Array.filter (fun (i, str) -> (i > 0 && i < lines.Length - 1) || not <| String.IsNullOrWhiteSpace str)
        |> Array.map (fun (i, str) ->
            let n = (string (i + 1)).PadLeft lineColumns
            $"    {n}: {str}"
        )
        |> String.concat Environment.NewLine

    let logLines (message : string) (code : string) =
        let nl = Environment.NewLine
        let numberedLines = withLineNumbers code
        Report.Line $"{message}:{nl}{nl}{numberedLines}{nl}"