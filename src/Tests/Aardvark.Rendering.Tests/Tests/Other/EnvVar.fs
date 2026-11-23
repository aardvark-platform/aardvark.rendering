namespace Aardvark.Rendering.Tests.Other

open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Expecto

module EnvVar =

    module private Cases =

        let isEnabled () =
            for value in [ "1"; "on"; "ON"; "true"; "True"; "yes"; " yes "; "\ton" ] do
                Expect.isTrue (EnvVar.IsEnabled value) $"'{value}' enables"

            for value in [ null; ""; " "; "0"; "off"; "false"; "no"; "2"; "enabled"; "on off" ] do
                Expect.isFalse (EnvVar.IsEnabled value) $"'{value}' does not enable"

        let isSet () =
            Expect.isFalse (EnvVar.IsSet null) "null is not set"
            Expect.isFalse (EnvVar.IsSet "") "empty is not set"
            Expect.isTrue (EnvVar.IsSet " ") "whitespace is set"
            Expect.isTrue (EnvVar.IsSet "0") "zero is set"

    let tests (target : TestTarget) =
        [
            "IsEnabled", Cases.isEnabled
            "IsSet",     Cases.isSet
        ]
        |> prepareCasesCpu "EnvVar" target
