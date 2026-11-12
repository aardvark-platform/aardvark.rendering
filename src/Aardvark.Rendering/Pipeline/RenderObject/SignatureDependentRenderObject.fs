namespace Aardvark.Rendering

open System.Collections.Generic
open Aardvark.Base
open FSharp.Data.Adaptive
open FSharp.Data.Traceable

/// A render object whose actual render objects depend on the framebuffer signature it is
/// compiled into. <c>CompileRender</c> expands it (see <c>SignatureDependentRenderObject.expand</c>)
/// before the normal per-object preparation, so the command task never sees it and, unlike
/// ActivationRenderObject, no backend command handling is needed. It lets nodes whose construction
/// depends on the signature (which attachments and shader outputs exist) defer that construction
/// to compile time instead of guessing a signature up front. <c>IsTransparent</c> is known eagerly
/// so that the transparency split, which runs on the unexpanded set
/// (<c>TransparencyRenderTask.isTransparent</c>), routes each variant correctly.
type SignatureDependentRenderObject(pass : RenderPass, scope : Ag.Scope, isTransparent : bool, isPickable : bool, expand : IFramebufferSignature -> aset<IRenderObject>) =
    let id = RenderObjectId.New()

    member x.Id = id
    member x.RenderPass = pass
    member x.AttributeScope = scope
    member x.IsTransparent = isTransparent
    /// True when the expanded objects write pick ids, so that a pickable object can be
    /// compiled against a signature with a pick-id attachment. Decided before the expansion,
    /// since the signature reaches Expand.
    member x.IsPickable = isPickable
    member x.Expand (signature : IFramebufferSignature) : aset<IRenderObject> = expand signature

    interface IRenderObject with
        member x.Id = id
        member x.RenderPass = pass
        member x.AttributeScope = scope

    override x.GetHashCode() = id.GetHashCode()
    override x.Equals o =
        match o with
        | :? SignatureDependentRenderObject as o -> id = o.Id
        | _ -> false

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SignatureDependentRenderObject =

    /// Forwards the deltas of ordinary render objects unchanged and replaces every
    /// SignatureDependentRenderObject by the objects it expands to, keeping state only for
    /// the latter. Expanded objects are assumed to be fresh, i.e. not contained in the input
    /// set or in another expansion.
    type private ExpandReader(signature : IFramebufferSignature, input : aset<IRenderObject>) =
        inherit AbstractReader<HashSetDelta<IRenderObject>>(HashSetDelta.empty)

        let reader = input.GetReader()
        let inner = Dictionary<SignatureDependentRenderObject, IHashSetReader<IRenderObject>>()

        override x.Compute(token) =
            let mutable result = HashSetDelta.empty

            for d in reader.GetChanges token do
                match d with
                | Add(_, (:? SignatureDependentRenderObject as o)) ->
                    inner.[o] <- (o.Expand signature).GetReader()

                | Rem(_, (:? SignatureDependentRenderObject as o)) ->
                    match inner.TryGetValue o with
                    | true, r ->
                        inner.Remove o |> ignore
                        for e in r.State do result <- result.Add(Rem e)
                        r.Outputs.Remove x |> ignore
                    | _ -> ()

                | d ->
                    result <- result.Add d

            for KeyValue(_, r) in inner do
                for d in r.GetChanges token do result <- result.Add d

            result

    /// Expands the signature-dependent render objects of the set for the given signature.
    /// A constant set without such objects is returned unchanged.
    let expand (signature : IFramebufferSignature) (objects : aset<IRenderObject>) =
        let hasDependent =
            not objects.IsConstant ||
            objects.Content |> AVal.force |> HashSet.exists (fun o -> o :? SignatureDependentRenderObject)

        if hasDependent then ASet.ofReader (fun () -> ExpandReader(signature, objects))
        else objects
