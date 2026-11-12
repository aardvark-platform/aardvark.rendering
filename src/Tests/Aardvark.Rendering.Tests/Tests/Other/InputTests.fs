namespace Aardvark.Rendering.Tests.Other

open Aardvark.Base
open Aardvark.Application
open Aardvark.Rendering.Tests
open Expecto
open FSharp.Data.Adaptive

module Input =

    module private Cases =

        let private position = PixelPosition(10, 20, 640, 480)
        let private buttons =
            [ MouseButtons.Left; MouseButtons.Right; MouseButtons.Middle; MouseButtons.Button4
              MouseButtons.Button5; MouseButtons.Button6; MouseButtons.Button7; MouseButtons.Button8 ]
        let private releasePositions = [position; PixelPosition(12, 23, 800, 600); PixelPosition(14, 24, 640, 480)]
        let private movements = [None; Some(PixelPosition(11, 21, 640, 480)); Some(PixelPosition(200, 300, 640, 480))]

        type private Recorder(mouse : IMouse) =
            let events = ResizeArray<string * MouseButtons * PixelPosition * MouseButtons>()
            let states = buttons |> List.map (fun button -> button, mouse.IsDown button)
            let record kind button =
                let held = states |> List.fold (fun held (b, state) -> if AVal.force state then held ||| b else held) MouseButtons.None
                events.Add(kind, button, AVal.force mouse.Position, held)
            let subscriptions =
                [ mouse.Click.Values.Subscribe(record "Click")
                  mouse.DoubleClick.Values.Subscribe(record "DoubleClick")
                  mouse.Up.Values.Subscribe(record "Up") ]
            member _.Events = List.ofSeq events
            member _.Describe =
                events |> Seq.map (fun (kind, button, p, held) -> $"{kind} {button} at={p.Position} bounds={p.Bounds} held={held}") |> String.concat "; "
            member _.Clear() = events.Clear()
            interface System.IDisposable with
                member _.Dispose() = subscriptions |> List.iter _.Dispose()

        let private collect (failures : ResizeArray<string>) context action =
            try action() with error -> failures.Add(context + ": " + error.Message)

        let private forceMouseButton (mouse : IMouse) button =
            mouse.IsDown button |> AVal.force

        let mouseResetReleasesButtons() =
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

        let mouseResetIsIdempotent() =
            let source = EventMouse(true)
            let mouse = source :> IMouse
            let mutable released = 0
            mouse.Up.Values.Add(fun _ -> released <- released + 1)

            source.Down(position, MouseButtons.Left ||| MouseButtons.Middle)
            source.Reset()
            source.Reset()

            Expect.equal released 2 "repeated reset must not emit additional releases"

        let mouseResetExplicitClicks() =
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

        let firstClickAfterCanceledPress() =
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

        let mouseResetClearsClickHistory() =
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

        let mouseReleaseState() =
            let failures = ResizeArray<string>()
            for button in buttons do
                for released in releasePositions do
                    for movement in movements do
                        let source = EventMouse(true)
                        use recorder = new Recorder(source)
                        for cycle in 0 .. 2 do
                            recorder.Clear()
                            source.Down(position, button)
                            movement |> Option.iter source.Move
                            source.Up(released, button)
                            let gesture = if cycle = 1 then "DoubleClick" else "Click"
                            let context = $"button={button}, released={released.Position}, bounds={released.Bounds}, movement={movement}, cycle={cycle}, actual={recorder.Describe}"
                            collect failures context (fun () ->
                                Expect.equal recorder.Events [gesture, button, released, MouseButtons.None; "Up", button, released, MouseButtons.None]
                                    "gesture callbacks must observe the full release position and cleared state before Up")
            Expect.isEmpty failures (String.concat "\n" failures)

        let combinedMouseReleaseState() =
            let failures = ResizeArray<string>()
            let all = buttons |> List.fold (|||) MouseButtons.None
            for mask in [all; MouseButtons.Left ||| MouseButtons.Right; MouseButtons.Button4 ||| MouseButtons.Button8] do
                for released in releasePositions do
                    let source = EventMouse(true)
                    use recorder = new Recorder(source)
                    source.Down(position, all)
                    for cycle in 0 .. 1 do
                        if cycle = 1 then source.Down(position, mask)
                        recorder.Clear()
                        let mutable held = all
                        let expected =
                            [ for button in buttons do
                                if mask.HasFlag button then
                                    held <- held &&& ~~~button
                                    yield (if cycle = 0 then "Click" else "DoubleClick"), button, released, held
                                    yield "Up", button, released, held ]
                        source.Up(released, mask)
                        let context = $"mask={mask}, released={released.Position}, cycle={cycle}, actual={recorder.Describe}"
                        collect failures context (fun () ->
                            Expect.equal recorder.Events expected "each callback must see only the current and preceding buttons released")
                        for button in buttons do
                            Expect.equal (forceMouseButton source button) (not (mask.HasFlag button)) $"{context}: unrelated button={button}"
            Expect.isEmpty failures (String.concat "\n" failures)

        let forwardedMouseReleaseState() =
            let failures = ResizeArray<string>()
            for button in buttons do
                for released in releasePositions do
                    for movement in movements do
                        let source = EventMouse(true)
                        let target = EventMouse(false)
                        use forwarding = target.Use(source)
                        use recorder = new Recorder(target)
                        for cycle in 0 .. 1 do
                            recorder.Clear()
                            source.Down(position, button)
                            movement |> Option.iter source.Move
                            source.Up(released, button)
                            let gesture = if cycle = 0 then "Click" else "DoubleClick"
                            let context = $"forwarded button={button}, released={released.Position}, bounds={released.Bounds}, movement={movement}, cycle={cycle}, actual={recorder.Describe}"
                            collect failures context (fun () ->
                                Expect.equal recorder.Events [gesture, button, released, button; "Up", button, released, MouseButtons.None]
                                    "explicit-click forwarding must use release coordinates without manufacturing gestures or clearing before its own Up")
            Expect.isEmpty failures (String.concat "\n" failures)

        let mouseReleaseControls() =
            for button in buttons do
                for delta in [V2i(5, 0); V2i(0, 5); V2i(-5, 0); V2i(0, -5); V2i(40, 50)] do
                    let source = EventMouse(true)
                    use recorder = new Recorder(source)
                    let released = PixelPosition(position.Position + delta, 640, 480)
                    source.Down(position, button)
                    source.Up(released, button)
                    source.Up(position, button)
                    Expect.equal recorder.Events ["Up", button, released, MouseButtons.None] $"non-click drag and duplicate release: button={button}, delta={delta}"
                let source = EventMouse(true)
                use recorder = new Recorder(source)
                source.Down(position, button)
                source.Reset()
                Expect.equal recorder.Events ["Up", button, position, MouseButtons.None] $"cancellation does not generate a gesture: button={button}"
                recorder.Clear()
                source.Up(PixelPosition(12, 23, 800, 600), button)
                Expect.isEmpty recorder.Events $"release after cancellation is ignored: button={button}"
                source.Down(position, button)
                source.Down(position, button)
                source.Up(position, button)
                Expect.equal (recorder.Events |> List.map (fun (kind, b, _, _) -> kind, b)) ["Click", button; "Up", button]
                    $"cancellation resets history and duplicate Down is ignored: button={button}"
                Expect.equal (List.last recorder.Events) ("Up", button, position, MouseButtons.None) $"post-cancellation release state: button={button}"
                for automatic in [false; true] do
                    for held in [false; true] do
                        let explicit = EventMouse(automatic)
                        use recording = new Recorder(explicit)
                        if held then explicit.Down(position, button)
                        let first, second = PixelPosition(100, 110, 640, 480), PixelPosition(120, 130, 800, 600)
                        explicit.Click(first, button)
                        explicit.DoubleClick(second, button)
                        explicit.Up(second, button)
                        let expectedHeld = if held then button else MouseButtons.None
                        let expected =
                            [ yield "Click", button, first, expectedHeld
                              yield "DoubleClick", button, second, expectedHeld
                              if held then yield "Up", button, second, MouseButtons.None ]
                        Expect.equal recording.Events expected $"explicit gestures retain state: button={button}, automatic={automatic}, held={held}"

        let keyboardReset() =
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

    let tests (target : TestTarget) =
        [
            "Mouse gestures observe release state",                   Cases.mouseReleaseState
            "Combined releases publish one button at a time",         Cases.combinedMouseReleaseState
            "Forwarded gestures retain release coordinates",          Cases.forwardedMouseReleaseState
            "Mouse release controls preserve gesture policy",         Cases.mouseReleaseControls

            "Mouse reset releases all held buttons without gestures", Cases.mouseResetReleasesButtons
            "Mouse reset is idempotent",                              Cases.mouseResetIsIdempotent
            "Mouse reset preserves explicit-click adapter behavior",  Cases.mouseResetExplicitClicks
            "First completed click after a canceled press is normal", Cases.firstClickAfterCanceledPress
            "Mouse reset clears completed double-click history",      Cases.mouseResetClearsClickHistory
            "Keyboard reset releases every key and adaptive state",   Cases.keyboardReset
        ]
        |> prepareCasesCpu "Input" target
