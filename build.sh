#!/bin/bash

mono .paket/paket.bootstrapper.exe
mono .paket/paket.exe restore
mono packages/FSharp.Formatting.CommandTool/tools/fsformatting.exe literate --processDirectory --inputDirectory src --outputDirectory output
