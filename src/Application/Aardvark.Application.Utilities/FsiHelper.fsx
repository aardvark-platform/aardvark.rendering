
(* prerequisites: 
    - use 64bit interactive (e.g. in visual studio: 
        tools/options/f# tools/64bit interactive must be true)
    - Aardvark.Base.dll referenced
*)

module InteractiveHelper = 
    open System.Reflection
    open Aardvark.Base
    open Aardvark.Base.Incremental

    let loadPluginsManually others = 
        let assemblies =
            [
                Assembly.Load("Aardvark.SceneGraph")
                Assembly.Load("Aardvark.SceneGraph.IO")
                Assembly.Load("AssimpNet") 
                Assembly.Load("DevILSharp") 
            ] @ others
        for a in assemblies do
            Introspection.RegisterAssembly a

    let inTemp (f : string -> 'a) =
        let last = System.Environment.CurrentDirectory
        let guid = System.Guid.NewGuid()
        let current = System.IO.Path.Combine(System.IO.Path.GetTempPath(), string guid)
        System.IO.Directory.CreateDirectory current |> ignore
        System.Environment.CurrentDirectory <- current
        let r = f current
        System.Environment.CurrentDirectory <- last
        r

    let init() =
        if not System.Environment.Is64BitProcess then
            failwith "F# interactive shell must be 64bit (e.g. in visual studio: tools/options/f# tools/64bit interactive must be true)."
        inTemp (fun currentDir ->
            printfn "temp for native dependencies: %s" currentDir
            loadPluginsManually []
            Mod.initialize()
            Ag.initialize()
            let gl = Assembly.Load("Aardvark.Rendering.GL")
            let vk = Assembly.Load("Aardvark.Rendering.Vulkan")
            Aardvark.Init(currentDir)
            printfn "glvm ~> %A" (Aardvark.Base.DynamicLinker.tryLoadLibrary "glvm")
            printfn "vkvm ~> %A" (Aardvark.Base.DynamicLinker.tryLoadLibrary "vkvm")
            Assimp.Unmanaged.AssimpLibrary.Instance.LoadLibrary "Assimp64"
            DevILSharp.IL.Init()
        )
