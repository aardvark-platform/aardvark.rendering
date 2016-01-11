How to build:
(1) build the project via build.cmd/build.sh
(2) build the solution in Debug via msbuild,xbuild or visual studio.
(3) On linux: make sure to use a 64bit fsi.exe
    On Windows in VisualStudio: verify that you use a 64bit fsi (Tools/Options/Fsharp tools/64bit interactive active).
    You can verify this step by typing: 
    > System.IntPtr.Size;;
    which should print: val it : int = 8
(4) In the console, invoke fsi which Tutorial.fsx argument, in VisualStudio send the code to interactive shell 
    (right click, send to interactive or ALT+ENTER)
(5) A new window should now spawn. At the end of each example there are some functions for modifying the content.
    Run those lines in order to see the effects. In Tutorial.fsx you can for example modify values in the positions array
    and rerun lines beginning from quadSg till setSg in order to activate the new content.