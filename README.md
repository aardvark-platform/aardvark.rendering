[![Build status Linux](https://ci.appveyor.com/api/projects/status/oqg1tw2ax1jl8qjx?svg=true)](https://ci.appveyor.com/project/haraldsteinlechner/aardvark-rendering)
[![Build Status Windows](https://travis-ci.org/vrvis/aardvark.rendering.svg?branch=master)](https://travis-ci.org/vrvis/aardvark.rendering)

# Aardvark.Rendering

How to build:
------

Windows:
- Visual Studio 2013,
- FSharp 3.1 (at least Daily Builds Preview 10-27-2014) [1]
- run build.bat
- run msbuild src\Aardvark.sln or use VisualStudio to build the solution

Linux:
- install mono >= 3.2.8 (might work in older versions as well)
- install fsharp 3.1 (http://fsharp.org/use/linux/)
- run ./build.sh
- run xbuild src/Aardvark.Rendering.sln or use MonoDevelop to build src/Aardvark.Rendering


