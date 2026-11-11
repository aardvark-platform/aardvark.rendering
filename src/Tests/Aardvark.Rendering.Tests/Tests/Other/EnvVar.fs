namespace Aardvark.Rendering.Tests.Other

open System
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

        /// Uses AARDVARK_LEGACY_SG as a representative registry entry.
        let overrideAndReset () =
            let name = "AARDVARK_LEGACY_SG"
            try
                EnvVar.AARDVARK_LEGACY_SG <- "1"
                Expect.equal EnvVar.AARDVARK_LEGACY_SG "1" "override is returned"

                // The process environment is ignored while an override is set ...
                Environment.SetEnvironmentVariable(name, "on")
                Expect.equal EnvVar.AARDVARK_LEGACY_SG "1" "override wins over the process environment"

                // ... and read again after a reset.
                EnvVar.AARDVARK_LEGACY_SG <- null
                Expect.equal EnvVar.AARDVARK_LEGACY_SG "on" "reset reads the process environment"

                // Later changes to the process environment are not seen until the next reset.
                Environment.SetEnvironmentVariable(name, null)
                Expect.equal EnvVar.AARDVARK_LEGACY_SG "on" "value is cached"
                EnvVar.AARDVARK_LEGACY_SG <- null
                Expect.isNull EnvVar.AARDVARK_LEGACY_SG "unset after reset"
            finally
                Environment.SetEnvironmentVariable(name, null)
                EnvVar.AARDVARK_LEGACY_SG <- null

    let tests (target : TestTarget) =
        [
            "IsEnabled",          Cases.isEnabled
            "IsSet",              Cases.isSet
            "Override and reset", Cases.overrideAndReset
        ]
        |> prepareCasesCpu "EnvVar" target
