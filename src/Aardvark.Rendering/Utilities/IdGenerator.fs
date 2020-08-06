namespace Aardvark.Rendering

[<AutoOpen>]
module IdGenerator =
    open System.Threading

    let mutable private currentId = 0
    let newId() =
        Interlocked.Increment(&currentId)