@echo off
call .paket\paket.exe restore
call packages\FSharp.Formatting.CommandTool\tools\fsformatting.exe literate --processDirectory --lineNumbers true --inputDirectory src --outputDirectory _posts

git add --all .
git commit -a -m $*
REM git push