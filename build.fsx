
#r "paket: groupref Build //"
#load ".fake/build.fsx/intellisense.fsx"
#load @"paket-files/build/aardvark-platform/aardvark.fake/DefaultSetup.fsx"

open System
open System.IO
open System.Diagnostics
open Aardvark.Fake
open Fake.Core
open Fake.Tools
open Fake.IO.Globbing.Operators
open System.Runtime.InteropServices
open Fake.Core.TargetOperators

//do MSBuildDefaults <- { MSBuildDefaults with Verbosity = Some Minimal }
do Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
    DefaultSetup.install ["src/Aardvark.Rendering.sln"]
else
    DefaultSetup.install ["src/Aardvark.Rendering.NonWindows.sln"]

#if DEBUG
do System.Diagnostics.Debugger.Launch() |> ignore
#endif


Target.create "PushDev" (fun _ -> 

    DefaultSetup.push ["https://vrvis.myget.org/F/aardvark_public/api/v2" ,"public.key"]
    
)

"CreatePackage" ==> "PushDev"


entry()
