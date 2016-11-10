open System
open Aardvark.Base
open Aardvark.Rendering.Vulkan

[<EntryPoint>]
let main args =
    Log.start "layers"
    for l in Instance.AvailableLayers do
        Log.start "%s" l.name

        Log.line "description:    %s" l.description
        Log.line "specification:  %A" l.specification 
        Log.line "implementation: %A" l.implementation
        
        match l.extensions with
            | [] -> ()
            | _ -> 
                Log.start "extensions"
                for e in l.extensions do
                    Log.line "%s: %A" e.name e.specification
                Log.stop() 

        Log.stop()
    Log.stop()

    Log.start "extensions"
    for e in Instance.GlobalExtensions do
        Log.line "%s: %A" e.name e.specification
    Log.stop() 



    use instance = new Instance(Version(1,0,2), Set.empty, Set.ofList ["VK_KHR_SURFACE"; "VK_KHR_WIN32_SURFACE"])

    for d in instance.Devices do
        Log.start "device %d" d.Index
        Log.line "name:           %s" d.Name
        Log.line "type:           %A" d.Type
        Log.line "api version:    %A" d.APIVersion
        Log.line "driver version: %A" d.DriverVersion
        Log.line "main queue:     { id = %d; count = %d; flags = %A }" d.MainQueue.index d.MainQueue.count d.MainQueue.flags
        match d.TransferQueue with
            | Some q -> Log.line "transfer queue: { id = %d; count = %d; flags = %A }" q.index q.count q.flags
            | _ -> Log.line "transfer queue: None"

        Log.start "layers"
        for l in d.AvailableLayers do
            Log.start "%s" l.name

            Log.line "description:    %s" l.description
            Log.line "specification:  %A" l.specification 
            Log.line "implementation: %A" l.implementation
        
            match l.extensions with
                | [] -> ()
                | _ -> 
                    Log.start "extensions"
                    for e in l.extensions do
                        Log.line "%s: %A" e.name e.specification
                    Log.stop() 

            Log.stop()
        Log.stop()

        Log.start "extensions"
        for e in d.GlobalExtensions do
            Log.line "%s: %A" e.name e.specification
        Log.stop() 


        Log.start "queues"
        for q in d.QueueFamilies do
            Log.start "queue %d" q.index
            Log.line "flags:          %A" q.flags
            Log.line "count:          %A" q.count
            Log.line "imgGranularity: %A" q.minImgTransferGranularity
            Log.line "timestampBits:  %A" q.timestampBits
            Log.stop()
        Log.stop()
 
        
        Log.stop()

    0
