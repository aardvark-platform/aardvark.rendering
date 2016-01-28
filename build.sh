#!/bin/bash

mono .paket/paket.bootstrapper.exe
mono .paket/paket.exe restore group Build

clear

mono packages/build/FAKE/tools/FAKE.exe "build.fsx" Dummy --fsiargs build.fsx $@
