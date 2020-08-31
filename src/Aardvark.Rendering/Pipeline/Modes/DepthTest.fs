namespace Aardvark.Rendering

type DepthTest = ComparisonFunction

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DepthTest =

    /// Alias for DepthTest.Always
    let None : DepthTest = DepthTest.Always