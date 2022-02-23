#!/bin/sh

dotnet tool restore
dotnet paket restore
dotnet build src/Aardvark.Rendering.NonWindows.sln