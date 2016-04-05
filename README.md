[![Build status](https://ci.appveyor.com/api/projects/status/oqg1tw2ax1jl8qjx?svg=true)](https://ci.appveyor.com/project/haraldsteinlechner/aardvark-rendering)
[![Build Status](https://travis-ci.org/vrvis/aardvark.rendering.svg?branch=master)](https://travis-ci.org/vrvis/aardvark.rendering)

# Aardvark.Rendering

How to build:
------

Windows:
- Visual Studio 2015,
- Visual FSharp Tools installed (we use 4.0 now) 
- run build.cmd which will install all dependencies
- msbuild src\Aardvark.sln or use VisualStudio to build the solution

Linux:
- install mono >= 4.2.3.0 (might work in older versions as well)
- install fsharp 4.0 (http://fsharp.org/use/linux/)
- run build.sh which will install all dependencies
- run xbuild src/Aardvark.Rendering.sln

Tutorials can be found here:
https://github.com/vrvis/aardvark.rendering/tree/master/src/Demo/Examples
e.g. interactive rendering "Hello World": https://github.com/vrvis/aardvark.rendering/blob/master/src/Demo/Examples/HelloWorld.fs


[1] https://visualfsharp.codeplex.com/releases/view/161288


