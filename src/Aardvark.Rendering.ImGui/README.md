Wrapper and Aardvark backend for [Dear ImGui](https://github.com/ocornut/imgui). Allows users to quickly build GUIs for their applications without having to worry about state synchronization between the application and the user interface.

## Integration
1. Reference `Aardvark.Rendering.ImGui` in your project.
1. Initialize ImGui for your window:
    ```fsharp
    use gui = window.InitializeImGui()
    ```
    The window must be created with a GLFW application (`Aardvark.Application.Slim` or `Aardvark.Applcation.Utilities`). If you use the `window` builder, call `window.Control.InitializeImGui()` instead.
1. Define your GUI by setting the render function of ImGui:
    ```fsharp
    gui.Render <- fun () ->
        ImGui.ShowDemoWindow()
    ```
    This function is called every frame to render the GUI. The methods from the `ImGui` class contain all the relevant widgets and controls. For convenience, Aardvark.Rendering.ImGui provides variants for `cval` and other Aardvark-specific types. As a reference, take a look at the demo window and [its source code](https://github.com/ocornut/imgui/blob/master/imgui_demo.cpp).
1. Add the GUI to your scene graph:
    ```fsharp
    let cmd = RenderCommand.Ordered [scene; gui]
    window.Scene <- Sg.execute cmd
    ```
    Note that `Aardvark.Rendering.ImGui.Instance` implements `ISg` and can be added to the scene graph directly. Make sure that the GUI is rendered after the rest of the scene graph by using render commands.
