namespace Aardvark.Rendering.Vulkan

open System
open Aardvark.Base
open System.Runtime.InteropServices

/// Static class for loading the Vulkan library.
[<Sealed; AbstractClass>]
type VulkanLoader =
    static let [<Literal>] MoltenVK = "MoltenVK"

    static let mutable libraryNames =
        if RuntimeInformation.IsOSPlatform OSPlatform.Windows then [| "vulkan-1"; "vulkan" |]
        elif RuntimeInformation.IsOSPlatform OSPlatform.OSX then [| "vulkan.1"; "vulkan"; MoltenVK |]
        else [| "vulkan.1"; "vulkan" |]

    static let library =
        lazy (
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
            let names = VulkanLoader.LibraryNames |> Array.filter ((=) MoltenVK)

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