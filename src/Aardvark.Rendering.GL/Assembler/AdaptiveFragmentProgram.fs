namespace Aardvark.Assembler

open System
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop
open Aardvark.Base
open Aardvark.Base.Runtime
open FSharp.Data.Adaptive

#nowarn "9"

type AdaptiveFragmentProgram<'a> internal(differential : bool, set : aset<'a>, project : 'a -> list<obj>, compile : option<'a> -> 'a -> IAssemblerStream -> unit) =
    inherit AdaptiveObject()
        
    let mutable isDisposed = false
    let mutable trie = OrderMaintenanceTrie<obj, Fragment<'a>>()
    let mutable reader = set.GetReader()
    let mutable program = new FragmentProgram<'a>(differential, compile)

    member x.Update(token : AdaptiveToken) =
        x.EvaluateIfNeeded token () (fun token ->
            if isDisposed then raise <| System.ObjectDisposedException("AdaptiveFragmentProgram")
            let ops = reader.GetChanges token
            for op in ops do
                match op with
                | Add(_, v) ->
                    let ref = trie.AddOrUpdate(project v, function ValueSome old -> old | ValueNone -> null)

                    if isNull ref.Value then
                        // new fragment
                        let prev = match ref.Prev with | ValueSome p -> p.Value | ValueNone -> null
                        //let next = match ref.Next with | ValueSome n -> n.Value | ValueNone -> null

                        let self = program.InsertAfter(prev, v)
                        ref.Value <- self
                    else
                        Log.warn "[AdaptiveFragmentProgram] update should not be possible: is: %A should: %A" ref.Value.Tag v
                | Rem(_, v) ->
                    let key = project v
                    match trie.TryGetReference key with
                    | ValueSome self ->   
                        if not (isNull self.Value) then self.Value.Dispose()
                        trie.TryRemove key |> ignore
                    | ValueNone ->  
                        Log.warn "[AdaptiveFragmentProgram] removing non-existing fragment: %A" v

            program.Update()

        )

    member x.Run(token : AdaptiveToken) =
        lock x (fun () ->
            x.Update token
            program.Run()
        )

    member x.Run() = x.Run AdaptiveToken.Top
    member x.Update() = x.Update AdaptiveToken.Top

    member x.Dispose() =
        lock x (fun () ->
            if not isDisposed then
                isDisposed <- true
                trie.Clear()
                program.Dispose()
                reader <- Unchecked.defaultof<_>
        )


    new(set : aset<'a>, project : 'a -> list<obj>, compile : option<'a> -> 'a -> IAssemblerStream -> unit) =
        new AdaptiveFragmentProgram<'a>(true, set, project, compile)

    new(set : aset<'a>, project : 'a -> list<obj>, compile : 'a -> IAssemblerStream -> unit) =
        new AdaptiveFragmentProgram<'a>(false, set, project, fun _ v -> compile v)