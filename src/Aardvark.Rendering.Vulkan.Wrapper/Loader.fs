namespace Aardvark.Rendering.Vulkan

open System
open Aardvark.Base
open System.Runtime.InteropServices

/// Static class for loading the Vulkan library.
[<Sealed; AbstractClass>]
type VulkanLoader =
    static let [<Literal>] MoltenVK = "MoltenVK"

    /// explicit override (name or absolute path) — e.g. to select a specific
    /// loader/driver build such as KosmicKrisp's SDK loader on macOS, where
    /// DYLD_* variables are stripped by SIP and cannot redirect the default.
    /// WINS over LibraryNames / PreferMoltenVK set by application code.
    static let envOverride =
        match Environment.GetEnvironmentVariable "AARDVARK_VULKAN_LIBRARY" with
        | null | "" -> ValueNone
        | s -> ValueSome s

    static let mutable libraryNames =
        if RuntimeInformation.IsOSPlatform OSPlatform.Windows then [| "vulkan-1"; "vulkan" |]
        elif RuntimeInformation.IsOSPlatform OSPlatform.OSX then [| "vulkan.1"; "vulkan"; MoltenVK |]
        else [| "vulkan.1"; "vulkan" |]

    static let library =
        lazy (
            match envOverride with
            | ValueSome path ->
                let ptr = Aardvark.LoadLibrary(path, typeof<VulkanLoader>.Assembly)
                if ptr = 0n then failwithf "Failed to load Vulkan library '%s' (AARDVARK_VULKAN_LIBRARY)." path
                ptr
            | ValueNone ->
                libraryNames |> Array.tryPickV (fun libraryName ->
                    let ptr = Aardvark.LoadLibrary(libraryName, typeof<VulkanLoader>.Assembly)
                    if ptr <> 0n then ValueSome ptr else ValueNone
                )
                |> ValueOption.defaultWith (fun _ -> failwith "Failed to load Vulkan library.")
        )

    /// Handle of the Vulkan library.
    static member Library = library.Value

    /// Ordered array of candidate Vulkan library names and paths; tried sequentially until the library is loaded successfully.
    static member LibraryNames
        with get() = libraryNames
        and set value =
            if library.IsValueCreated then raise <| InvalidOperationException("Cannot set library names when library has already been loaded.")
            libraryNames <- value

    /// Indicates whether the loader tries to load the MoltenVK library before the regular Vulkan library.
    /// This will result in the bundled MoltenVK library to be loaded over the system Vulkan library (if installed).
    static member PreferMoltenVK
        with get() = VulkanLoader.LibraryNames |> Array.tryHeadV |> ValueOption.contains MoltenVK
        and set value =
            // note ((<>) MoltenVK): the previous ((=) MoltenVK) KEPT ONLY MoltenVK,
            // collapsing the candidate list to the bundled library for either value
            let names = VulkanLoader.LibraryNames |> Array.filter ((<>) MoltenVK)

            if value then
                VulkanLoader.LibraryNames <- Array.append [| MoltenVK |] names
            else
                VulkanLoader.LibraryNames <- names

    /// <summary>
    /// Retrieves the address of the Vulkan function with the given name.
    /// </summary>
    /// <remarks>Only works for core Vulkan functions.</remarks>
    /// <param name="name">The name of the function to load.</param>
    static member GetProcAddress(name: string) = Aardvark.GetProcAddress(library.Value, name)