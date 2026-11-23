namespace Aardvark.Rendering

open System
open Aardvark.Base

/// Holds the cached value of one process environment variable.
[<Sealed>]
type private EnvVarCell(name : string) =
    [<VolatileField>]
    let mutable loaded = false

    [<VolatileField>]
    let mutable value : string = null

    member x.Name = name

    /// The value: read from the process environment on first access (null when unset);
    /// setting null forgets the cached value so the environment is read again.
    member x.Value
        with get () =
            if not loaded then
                let v =
                    try Environment.GetEnvironmentVariable name
                    with e ->
                        Log.warn "[Env] Could not read %s: %s" name e.Message
                        null

                if not (isNull v) then
                    Log.line "[Env] %s=%s" name v

                value <- v
                loaded <- true

            value

        and set (v : string) =
            if isNull v then
                loaded <- false
                value <- null
            else
                value <- v
                loaded <- true

/// Registry of the environment variables read by the rendering libraries, one documented
/// string property per variable. A value is read from the process environment on first access
/// and cached; later changes to the process environment are ignored. Setting a property
/// overrides the environment, setting it to null forgets the override so the environment is
/// read again on the next access.
[<AbstractClass; Sealed>]
type EnvVar private () =

    /// Returns whether a switch value enables it: "1", "on", "true" and "yes" (ignoring case
    /// and surrounding whitespace) do, everything else, including null, does not.
    static member IsEnabled (value : string) =
        if isNull value then false
        else
            match value.Trim().ToLowerInvariant() with
            | "1" | "on" | "true" | "yes" -> true
            | _ -> false

    /// Returns whether the value is neither null nor empty.
    static member IsSet (value : string) =
        not (String.IsNullOrEmpty value)
