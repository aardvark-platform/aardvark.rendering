#!/bin/bash

if [ ! -d "packages/FAKE" ]; then
	echo "downloading FAKE"
	mono --runtime=v4.0 bin/nuget.exe install FAKE -OutputDirectory packages -Version 3.35.2 -ExcludeVersion
	mono --runtime=v4.0 bin/nuget.exe install FSharp.Formatting.CommandTool -OutputDirectory packages -ExcludeVersion -Prerelease 
	mono --runtime=v4.0 bin/nuget.exe install SourceLink.Fake -OutputDirectory packages -ExcludeVersion 
	mono --runtime=v4.0 bin/nuget.exe install Aardvark.Build -OutputDirectory packages -ExcludeVersion 
	mono --runtime=v4.0 bin/nuget.exe install Paket.Core -OutputDirectory packages -Version 1.18.5 -ExcludeVersion 
fi


if [ ! -d "\\\\hobel\\NuGet\\" ]; then
	echo "attempting to mount hobel"
	sudo mkdir "\\\\hobel\\NuGet\\"
	sudo chmod 777 "\\\\hobel\\NuGet\\"
	echo "please enter your VRVis username"
	read user
	echo "mounting hobel"
	sudo mount -t cifs -o user=$user,dom=VRVIS,uid=$(id -u),gid=$(id -u) //hobel.ra1.vrvis.lan/NuGet "\\\\hobel\\NuGet\\"
fi

mono packages/FAKE/tools/FAKE.exe "build.fsx"  $@
