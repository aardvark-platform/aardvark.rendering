namespace Aardvark.Rendering.Tests

open Aardvark.Rendering
open FSharp.Data.Adaptive
open Expecto

module ``AdaptiveResource Tests`` =

    [<AutoOpen>]
    module private Utils =

        type Base(token : RenderToken) =
            member x.Token = token

        type Derived(token : RenderToken) =
            inherit Base(token)

        type DummyResource() =
            inherit AdaptiveResource<Derived>()

            let mutable allocated = false
            member x.IsAllocated = allocated

            override x.Create() = allocated <- true
            override x.Destroy() = allocated <- false
            override x.Compute(t, rt) = Derived(rt)

    module private Cases =

        // Relevant for Sg.texture
        let castPreservesEquality() =

            let input = DummyResource()
            let m1 = input |> AdaptiveResource.map unbox<Base>
            let m2 = input |> AdaptiveResource.map unbox<Base>

            Expect.isFalse (m1 = m2) "mapped resources are equal"

            let c1 = input |> AdaptiveResource.cast<Base>
            let c2 = input |> AdaptiveResource.cast<Base>

            Expect.isTrue (c1 = c2) "cast resources are not equal"

        let castPreservesResourceSemantics() =

            let input = DummyResource()
            let output = input |> AdaptiveResource.cast<Base> 
            let token = RenderToken.Empty

            output.Acquire()
            Expect.isTrue input.IsAllocated "input not allocated"

            let result = output.GetValue(AdaptiveToken.Top, token)
            Expect.equal result.Token token "tokens are not equal"

            output.Release()
            Expect.isFalse input.IsAllocated "input still allocated"
            

    [<Tests>]
    let tests =
        testList "Adaptive.AdaptiveResource" [
            testCase "Cast preserves equality"              Cases.castPreservesEquality
            testCase "Cast preserves resource semantics"    Cases.castPreservesResourceSemantics
        ]