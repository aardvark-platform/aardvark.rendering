@echo off

call .paket\paket restore
call tools\fsformatting.exe literate --processDirectory --lineNumbers false --inputDirectory  "code" --outputDirectory "_posts"

git add --all .
git commit -a -m %1
git push