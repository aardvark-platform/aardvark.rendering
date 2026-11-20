namespace Aardvark.Rendering.Tests.Rendering

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Text
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open Expecto

module ShapeSetOrder =

    module private Shapes =
        let red = C4b(255uy, 0uy, 0uy, 255uy)
        let blue = C4b(0uy, 0uy, 255uy, 255uy)

        /// An opaque rectangle over the whole viewport (identity view and projection) in one colour.
        let fullViewport (color : C4b) : aval<Trafo3d> * aval<ShapeList> =
            AVal.constant Trafo3d.Identity,
            AVal.constant (ShapeList.ofList [ ConcreteShape.fillRectangle color (Box2d(-1.0, -1.0, 1.0, 1.0)) ])

        /// The colour at the centre of what the members draw, as the scene changes.
        let centre (runtime : IRuntime) (content : alist<aval<Trafo3d> * aval<ShapeList>>) =
            let signature = runtime.CreateFramebufferSignature([ DefaultSemantic.Colors, TextureFormat.Rgba8 ])
            let task = Sg.shapesOrdered content |> Sg.compile runtime signature
            let output = task |> RenderTask.renderToColorWithClear (~~V2i(64)) (clear { color C4f.Black })
            output.Acquire()
            let read () =
                let image = output.GetValue().Download().AsPixImage<byte>()
                let c = image.GetMatrix<C4b>().[32, 32]
                C3b(c.R, c.G, c.B)
            let dispose () =
                output.Release()
                task.Dispose()
                signature.Dispose()
            read, dispose

    module Cases =

        let laterCoversEarlier (runtime : IRuntime) =
            let red, blue = Shapes.fullViewport Shapes.red, Shapes.fullViewport Shapes.blue
            let read, dispose = Shapes.centre runtime (AList.ofList [ red; blue ])
            try Expect.equal (read ()) (C3b(0uy, 0uy, 255uy)) "the later member covers the earlier one"
            finally dispose ()

        let reorderingRedraws (runtime : IRuntime) =
            let red, blue = Shapes.fullViewport Shapes.red, Shapes.fullViewport Shapes.blue
            let content = clist [ red; blue ]
            let read, dispose = Shapes.centre runtime content
            try
                Expect.equal (read ()) (C3b(0uy, 0uy, 255uy)) "blue last"
                transact (fun () -> content.Value <- IndexList.ofList [ blue; red ])
                Expect.equal (read ()) (C3b(255uy, 0uy, 0uy)) "red last after reordering"
            finally dispose ()

        let readdedDrawsLast (runtime : IRuntime) =
            let red, blue = Shapes.fullViewport Shapes.red, Shapes.fullViewport Shapes.blue
            let content = clist [ red; blue ]
            let read, dispose = Shapes.centre runtime content
            try
                Expect.equal (read ()) (C3b(0uy, 0uy, 255uy)) "blue last"
                transact (fun () -> content.RemoveAt 0 |> ignore)
                transact (fun () -> content.Add red |> ignore)
                Expect.equal (read ()) (C3b(255uy, 0uy, 0uy)) "red, removed and added again, last"
            finally dispose ()

    let tests (target : TestTarget) =
        [
            "Later members cover earlier ones",     Cases.laterCoversEarlier
            "Reordering the list redraws in order", Cases.reorderingRedraws
            "A member added again draws last",      Cases.readdedDrawsLast
        ]
        |> prepareCasesGpu "Shape set order" target
