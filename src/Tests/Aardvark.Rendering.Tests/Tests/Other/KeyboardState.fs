namespace Aardvark.Rendering.Tests.Other

open System
open Aardvark.Application
open Aardvark.Rendering.Tests
open Expecto
open FSharp.Data.Adaptive

module KeyboardState =

    module private Cases =

        let private modifiers =
            [| "Alt", Keys.LeftAlt, Keys.RightAlt
               "Shift", Keys.LeftShift, Keys.RightShift
               "Control", Keys.LeftCtrl, Keys.RightCtrl |]

        let private keys =
            [| Keys.A; Keys.B; Keys.Space
               Keys.LeftAlt; Keys.RightAlt; Keys.LeftShift; Keys.RightShift; Keys.LeftCtrl; Keys.RightCtrl |]

        let private check (errors : ResizeArray<string>) condition message =
            if not condition then errors.Add message

        let private finish errors =
            Expect.isTrue (Seq.isEmpty errors) (String.concat "\n" errors)

        let private state errors context (held : Set<Keys>) (keyboard : IKeyboard) =
            // Query aggregates first so their first access must also see current membership.
            for name, left, right in modifiers do
                let value =
                    match name with
                    | "Alt" -> keyboard.Alt
                    | "Shift" -> keyboard.Shift
                    | _ -> keyboard.Control
                let actual = AVal.force value
                let expected = held.Contains left || held.Contains right
                check errors (actual = expected) $"{context}: {name}, expected={expected}, actual={actual}"
            for key in keys do
                let actual = keyboard.IsDown key |> AVal.force
                let expected = held.Contains key
                check errors (actual = expected) $"{context}: {key}, expected={expected}, actual={actual}"

        let private dispose (subscriptions : IDisposable list) =
            { new IDisposable with
                member _.Dispose() = subscriptions |> List.iter _.Dispose() }

        let private subscribe (keyboard : IKeyboard) key callback =
            [ keyboard.DownWithRepeats.Values.Subscribe(fun k -> if k = key then callback "repeat")
              keyboard.Down.Values.Subscribe(fun k -> if k = key then callback "down")
              (keyboard.KeyDown key).Values.Subscribe(fun () -> callback "key-down")
              keyboard.Up.Values.Subscribe(fun k -> if k = key then callback "up")
              (keyboard.KeyUp key).Values.Subscribe(fun () -> callback "key-up") ]
            |> dispose

        let callbackState() =
            let errors = ResizeArray()
            for key in keys do
                for phase in ["repeat"; "down"; "key-down"; "up"; "key-up"] do
                    for observed in [false; true] do
                        let source = EventKeyboard()
                        let keyboard = source :> IKeyboard
                        let context = $"key={key}, callback={phase}, previously observed={observed}"
                        let mutable held = Set.empty
                        if observed then state errors context held keyboard
                        let mutable callbacks = 0
                        use subscription = subscribe keyboard key (fun event ->
                            if event = phase then
                                callbacks <- callbacks + 1
                                state errors context held keyboard)
                        source.KeyUp key
                        held <- Set.singleton key
                        source.KeyDown key
                        source.KeyDown key
                        held <- Set.empty
                        source.KeyUp key
                        source.KeyUp key
                        Expect.equal callbacks (if phase = "repeat" then 2 else 1) context
                        state errors (context + ", final") held keyboard
            finish errors

        let modifierSides() =
            let errors = ResizeArray()
            for name, left, right in modifiers do
                for reversed in [false; true] do
                    for observed in [false; true] do
                        let first, second = if reversed then right, left else left, right
                        let source = EventKeyboard()
                        let keyboard = source :> IKeyboard
                        source.KeyDown Keys.A
                        let mutable held = Set.singleton Keys.A
                        let context = $"{name}, first={first}, previously observed={observed}"
                        if observed then state errors context held keyboard
                        let callback event = state errors (context + ", " + event) held keyboard
                        use firstEvents = subscribe keyboard first callback
                        use secondEvents = subscribe keyboard second callback
                        for press, key in [true, first; true, first; true, second; false, first; true, second; false, second] do
                            held <- if press then held.Add key else held.Remove key
                            if press then source.KeyDown key else source.KeyUp key
                            state errors (context + $", returned press={press}, key={key}") held keyboard
                        source.Reset()
                        state errors (context + ", reset") Set.empty keyboard
            finish errors

        let nestedTransitions() =
            let errors = ResizeArray()
            let schedules =
                [| "repeat",   true,  false, ["repeat"; "up"; "key-up"; "down"; "key-down"]
                   "repeat",   true,  true,  ["repeat"; "up"; "key-up"]
                   "down",     true,  false, ["repeat"; "down"; "up"; "key-up"; "key-down"]
                   "key-down", true,  false, ["repeat"; "down"; "key-down"; "up"; "key-up"]
                   "up",       false, false, ["up"; "repeat"; "down"; "key-down"; "key-up"]
                   "key-up",   false, false, ["up"; "key-up"; "repeat"; "down"; "key-down"] |]
            for key in Array.append [| Keys.A |] (modifiers |> Array.collect (fun (_, l, r) -> [| l; r |])) do
                for trigger, pressing, repeating, expectedEvents in schedules do
                    for observed in [false; true] do
                        let source = EventKeyboard()
                        let keyboard = source :> IKeyboard
                        let context = $"key={key}, nested from={trigger}, repeated press={repeating}, previously observed={observed}"
                        let mutable held = Set.empty
                        let mutable armed = false
                        let events = ResizeArray<string>()
                        if observed then state errors context held keyboard
                        use subscription = subscribe keyboard key (fun event ->
                            events.Add event
                            state errors (context + ", " + event) held keyboard
                            if armed && event = trigger then
                                armed <- false
                                held <- if pressing then Set.empty else Set.singleton key
                                if pressing then source.KeyUp key else source.KeyDown key
                                state errors (context + ", nested returned") held keyboard)
                        if not pressing || repeating then
                            held <- Set.singleton key
                            source.KeyDown key
                            events.Clear()
                        armed <- true
                        held <- if pressing then Set.singleton key else Set.empty
                        if pressing then source.KeyDown key else source.KeyUp key
                        check errors (List.ofSeq events = expectedEvents) $"{context}: event order {List.ofSeq events}"
                        state errors (context + ", outer returned") held keyboard
                        let before = events.Count
                        if pressing then
                            source.KeyUp key
                            check errors (events.Count = before) $"{context}: nested release must remove membership"
                            held <- Set.singleton key
                            source.KeyDown key
                            check errors (events.Count = before + 3) $"{context}: a new press must not become a repeat"
                        else
                            source.KeyDown key
                            check errors (events.Count = before + 1) $"{context}: nested press must retain membership"
                            held <- Set.empty
                            source.KeyUp key
                        state errors (context + ", subsequent input") held keyboard
            finish errors

        let forwarding() =
            let errors = ResizeArray()
            for key in [Keys.A; Keys.LeftShift; Keys.RightShift; Keys.LeftAlt; Keys.RightCtrl] do
                for hops in [1; 2] do
                    for trigger in ["none"; "down"; "up"] do
                        let sources = Array.init (hops + 1) (fun _ -> EventKeyboard())
                        let keyboards = sources |> Array.map (fun source -> source :> IKeyboard)
                        let mutable held = Set.empty
                        let mutable armed = trigger <> "none"
                        let context = $"key={key}, hops={hops}, nested from={trigger}"
                        use connections =
                            [for i in 1 .. hops -> sources.[i].Use keyboards.[i - 1]] |> dispose
                        use subscriptions =
                            [for i in 0 .. hops ->
                                subscribe keyboards.[i] key (fun event ->
                                    // Downstream state need not change until forwarding reaches it.
                                    for upstream in 0 .. i do
                                        state errors (context + $", node={i}, upstream={upstream}, {event}") held keyboards.[upstream]
                                    if armed && i = hops && event = trigger then
                                        armed <- false
                                        held <- if trigger = "down" then Set.empty else Set.singleton key
                                        if trigger = "down" then sources.[0].KeyUp key else sources.[0].KeyDown key)]
                            |> dispose
                        held <- Set.singleton key
                        sources.[0].KeyDown key
                        if trigger <> "down" then
                            sources.[0].KeyDown key
                            held <- Set.empty
                            sources.[0].KeyUp key
                        for i in 0 .. hops do state errors (context + $", returned node={i}") held keyboards.[i]
            finish errors

        let eventPolicy() =
            for key in [Keys.A; Keys.LeftAlt; Keys.RightShift; Keys.RightCtrl] do
                let source = EventKeyboard()
                let keyboard = source :> IKeyboard
                let events = ResizeArray<string>()
                let characters = ResizeArray<char>()
                use subscription = subscribe keyboard key events.Add
                use text = keyboard.Press.Values.Subscribe characters.Add
                Expect.isTrue keyboard.ClaimsKeyEvents $"{key}: default claims policy"
                keyboard.ClaimsKeyEvents <- false
                Expect.isFalse source.ClaimsKeyEvents $"{key}: interface claims policy"
                source.KeyUp key
                source.KeyDown key
                let cached = keyboard.IsDown key
                Expect.isTrue (AVal.force cached) $"{key}: first state query after press"
                Expect.isTrue (obj.ReferenceEquals(cached, keyboard.IsDown key)) $"{key}: cached adaptive identity"
                for _ in 1 .. 3 do source.KeyDown key
                for character in ['a'; '\u03BB'; '\000'] do source.KeyPress character
                source.KeyUp key
                source.KeyUp key
                source.KeyDown key
                source.Reset()
                source.Reset()
                Expect.equal (List.ofSeq events)
                    ["repeat"; "down"; "key-down"; "repeat"; "repeat"; "repeat"; "up"; "key-up"
                     "repeat"; "down"; "key-down"; "up"; "key-up"] $"{key}: event order and repeat counts"
                Expect.equal (List.ofSeq characters) ['a'; '\u03BB'; '\000'] $"{key}: text input is independent"
                Expect.isFalse (AVal.force cached) $"{key}: reset clears existing adaptive state"

            let source = EventKeyboard()
            let keyboard = source :> IKeyboard
            let target = EventKeyboard()
            let forwarded = target :> IKeyboard
            let releases = ResizeArray<Keys>()
            let mutable characters = 0
            use ups = forwarded.Up.Values.Subscribe releases.Add
            use text = forwarded.Press.Values.Subscribe(fun _ -> characters <- characters + 1)
            use connection = target.Use keyboard
            for key in keys do source.KeyDown key
            source.KeyDown Keys.A
            source.KeyPress 'x'
            source.Reset()
            source.Reset()
            Expect.equal (releases |> Seq.sort |> Seq.toList) (keys |> Array.sort |> Array.toList) "Forwarded reset releases every key once"
            Expect.equal characters 1 "Use forwards text once"
            for key in keys do Expect.isFalse (forwarded.IsDown key |> AVal.force) $"{key}: forwarded reset state"
            connection.Dispose()
            source.KeyDown Keys.A
            source.KeyUp Keys.A
            source.KeyPress 'y'
            Expect.equal releases.Count keys.Length "Disposed Use stops releases"
            Expect.equal characters 1 "Disposed Use stops text"
            Expect.isFalse (forwarded.IsDown Keys.A |> AVal.force) "Disposed Use stops presses"

    let tests (target : TestTarget) =
        [
            "Callbacks observe current key and modifier state", Cases.callbackState
            "Modifier sides preserve aggregate state",          Cases.modifierSides
            "Nested opposite transitions retain state",         Cases.nestedTransitions
            "Forwarding preserves current and nested state",    Cases.forwarding
            "Event order repeat reset and forwarding controls", Cases.eventPolicy
        ]
        |> prepareCasesCpu "KeyboardState" target
