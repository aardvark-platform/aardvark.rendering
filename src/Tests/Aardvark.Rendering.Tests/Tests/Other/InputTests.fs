namespace Aardvark.Rendering.Tests

open Aardvark.Base
open Aardvark.Application
open Expecto
open FSharp.Data.Adaptive

module ``Input Tests`` =

    let private position = PixelPosition(10, 20, 640, 480)

    let private forceMouseButton (mouse : IMouse) button =
        mouse.IsDown button |> AVal.force

    let tests =
        testList "Input" [
            testCase "Mouse reset releases all held buttons without gestures" (fun () ->
                let source = EventMouse(true)
                let mouse = source :> IMouse
                let mutable released = []
                let mutable clickCount = 0
                let mutable doubleClickCount = 0
                let mutable releasedAfterClear = true
                let held =
                    MouseButtons.Left ||| MouseButtons.Right ||| MouseButtons.Middle |||
                    MouseButtons.Button4 ||| MouseButtons.Button5 ||| MouseButtons.Button6 |||
                    MouseButtons.Button7 ||| MouseButtons.Button8
                let expected =
                    [ MouseButtons.Left; MouseButtons.Right; MouseButtons.Middle; MouseButtons.Button4
                      MouseButtons.Button5; MouseButtons.Button6; MouseButtons.Button7; MouseButtons.Button8 ]

                mouse.Up.Values.Add(fun button ->
                    released <- button :: released
                    releasedAfterClear <- releasedAfterClear && not (forceMouseButton mouse button)
                )
                mouse.Click.Values.Add(fun _ -> clickCount <- clickCount + 1)
                mouse.DoubleClick.Values.Add(fun _ -> doubleClickCount <- doubleClickCount + 1)

                source.Down(position, held)
                source.Reset()

                Expect.equal (List.sort released) (List.sort expected)
                    "each held button must be released exactly once"
                Expect.isTrue releasedAfterClear "adaptive button state must be cleared before Up is emitted"
                for button in expected do
                    Expect.isFalse (forceMouseButton mouse button) $"{button} must be cleared"
                Expect.equal clickCount 0 "reset must not synthesize clicks"
                Expect.equal doubleClickCount 0 "reset must not synthesize double-clicks"
            )

            testCase "Mouse reset is idempotent" (fun () ->
                let source = EventMouse(true)
                let mouse = source :> IMouse
                let mutable released = 0
                mouse.Up.Values.Add(fun _ -> released <- released + 1)

                source.Down(position, MouseButtons.Left ||| MouseButtons.Middle)
                source.Reset()
                source.Reset()

                Expect.equal released 2 "repeated reset must not emit additional releases"
            )

            testCase "Mouse reset preserves explicit-click adapter behavior" (fun () ->
                let source = EventMouse(false)
                let mouse = source :> IMouse
                let mutable released = []
                let mutable clicks = 0
                mouse.Up.Values.Add(fun button -> released <- button :: released)
                mouse.Click.Values.Add(fun _ -> clicks <- clicks + 1)

                source.Down(position, MouseButtons.Left ||| MouseButtons.Right)
                source.Reset()

                Expect.equal (List.sort released) [MouseButtons.Left; MouseButtons.Right]
                    "explicit-click adapters must still receive cancellation releases"
                Expect.equal clicks 0 "reset must not manufacture explicit click events"
            )

            testCase "First completed click after a canceled press is normal" (fun () ->
                let source = EventMouse(true)
                let mouse = source :> IMouse
                let mutable clicks = 0
                let mutable doubleClicks = 0
                mouse.Click.Values.Add(fun _ -> clicks <- clicks + 1)
                mouse.DoubleClick.Values.Add(fun _ -> doubleClicks <- doubleClicks + 1)

                source.Down(position, MouseButtons.Left)
                source.Reset()
                source.Down(position, MouseButtons.Left)
                source.Up(position, MouseButtons.Left)

                Expect.equal clicks 1 "the first completed click after reset must be a click"
                Expect.equal doubleClicks 0 "the canceled press must not contribute to a double-click"
            )

            testCase "Mouse reset clears completed double-click history" (fun () ->
                let source = EventMouse(true)
                let mouse = source :> IMouse
                let mutable clicks = 0
                let mutable doubleClicks = 0
                mouse.Click.Values.Add(fun _ -> clicks <- clicks + 1)
                mouse.DoubleClick.Values.Add(fun _ -> doubleClicks <- doubleClicks + 1)

                source.Down(position, MouseButtons.Left)
                source.Up(position, MouseButtons.Left)
                source.Reset()
                source.Down(position, MouseButtons.Left)
                source.Up(position, MouseButtons.Left)

                Expect.equal clicks 2 "focus loss must start a fresh click sequence"
                Expect.equal doubleClicks 0 "pre-reset click history must not produce a double-click"
            )

            testCase "Keyboard reset releases every key and adaptive state" (fun () ->
                let source = EventKeyboard()
                let keyboard = source :> IKeyboard
                let keys = [Keys.A; Keys.LeftShift; Keys.RightCtrl]
                let mutable released = []

                keyboard.Up.Values.Add(fun key ->
                    released <- key :: released
                )

                for key in keys do source.KeyDown key
                source.KeyDown Keys.A
                Expect.isTrue (keyboard.Shift |> AVal.force) "shift must be set before reset"
                Expect.isTrue (keyboard.Control |> AVal.force) "control must be set before reset"

                source.Reset()

                Expect.equal (List.sort released) (List.sort keys) "each held key must be released once"
                for key in keys do
                    Expect.isFalse (keyboard.IsDown key |> AVal.force) $"{key} must be cleared"
                Expect.isFalse (keyboard.Shift |> AVal.force) "shift aggregate must be cleared"
                Expect.isFalse (keyboard.Control |> AVal.force) "control aggregate must be cleared"

                source.Reset()
                Expect.equal released.Length keys.Length "repeated reset must not emit additional releases"
            )
        ]
