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

        lines |> Array.mapi (fun i str ->
            let n = (string (i + 1)).PadLeft lineColumns
            $"{n}: {str}"
        )
        |> String.concat Environment.NewLine

    let logLines (code : string) =
        let lines = withLineNumbers code
        Report.Line lines