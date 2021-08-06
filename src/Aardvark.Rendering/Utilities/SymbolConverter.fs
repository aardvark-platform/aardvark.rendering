namespace Aardvark.Rendering

open Aardvark.Base

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Symbol =

    module Converters =

        // These utilities make it possible to pass
        // a string, Symbol or TypedSymbol as identifier.
        type Converter =
            static member inline GetSymbol(name : string)            = Sym.ofString name
            static member inline GetSymbol(symbol : Symbol)          = symbol

        type TypedConverter<'T> =
            inherit Converter
            static member inline GetSymbol(symbol : TypedSymbol<'T>) = symbol.Symbol

        let untyped   = Unchecked.defaultof<Converter>
        let typed<'T> = Unchecked.defaultof<TypedConverter<'T>>


    let inline convert (_ : ^Converter) (name : ^Name) =
        ((^Converter or ^Name) : (static member GetSymbol : ^Name -> Symbol) (name))