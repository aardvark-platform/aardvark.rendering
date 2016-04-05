/*

This example demonstrates how to setup a rendering application in C#. The file is a 1:1 copy of 
https://github.com/vrvis/aardvark.rendering/blob/master/src/Demo/Examples/HelloWorld.fs

Feel free to contribute our C# interfaces. You'll win 10 beers if you find a nice way to use
FShade from within C# ;)

*/

using System;
using Aardvark.Base;
using Aardvark.Base.Incremental.CSharp;
using Aardvark.SceneGraph;
using Aardvark.SceneGraph.CSharp;

using Aardvark.Application;
using Aardvark.Application.WinForms;
using Aardvark.Rendering.NanoVg;
using Microsoft.FSharp.Quotations;
using static Aardvark.Base.CSharpInterop;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;
using Aardvark.Base.Incremental;
using System.ComponentModel;

namespace CSharpDemo
{
    public static class UserInterfaceStuff
    {
        public class Test : DockContent
        {
            private PropertyGrid m_grid;

            public Test()
            {
                m_grid = new PropertyGrid();
                m_grid.SelectedObject = this;
                m_grid.Dock = DockStyle.Fill;
                base.Controls.Add(m_grid);
            }
        }

        public class RenderControlContent : DockContent, IRenderControl
        {
            private IApplication m_application;
            private RenderControl m_control;

            public RenderControlContent()
            {
                m_control = new RenderControl();
                this.Text = "RenderControl";
                m_control.Dock = DockStyle.Fill;
                this.Controls.Add(m_control);
            }

            public IFramebufferSignature FramebufferSignature => m_control.FramebufferSignature;
            public IKeyboard Keyboard => m_control.Keyboard;
            public IMouse Mouse => m_control.Mouse;

            public IRenderTask RenderTask
            {
                get { return m_control.RenderTask; }
                set { m_control.RenderTask = value; }
            }

            public IRuntime Runtime => m_control.Runtime;

            public int Samples => m_control.Samples;

            public IMod<V2i> Sizes => m_control.Sizes;

            public IMod<DateTime> Time => m_control.Time;


            public void Init(IApplication app)
            {
                m_application = app;
                app.Initialize(m_control);
            }

        }

        public class RenderTaskControl : DockContent
        {
            public class RenderConfig
            {
                private IModRef<BackendConfiguration> m_config;

                public RenderConfig(IModRef<BackendConfiguration> config)
                {
                    m_config = config;
                }

                internal IModRef<BackendConfiguration> Cell => m_config;

                internal BackendConfiguration Configuration
                {
                    get { return m_config.Value; }
                    set
                    {
                        using (Adaptive.Transaction) m_config.Value = value;
                    }
                }

                [Category("Optimizations")]
                public bool Optimized
                {
                    get { return m_config.Value.redundancy == RedundancyRemoval.Static; }
                    set
                    {
                        using (Adaptive.Transaction)
                        {
                            var old = m_config.Value;
                            var rem = value ? RedundancyRemoval.Static : RedundancyRemoval.None;
                            m_config.Value = new BackendConfiguration(old.execution, rem, old.sharing, old.sorting, old.useDebugOutput);
                        }
                    }
                }

                [Category("Optimizations")]
                public bool Runtime
                {
                    get { return m_config.Value.redundancy == RedundancyRemoval.Runtime; }
                    set
                    {
                        using (Adaptive.Transaction)
                        {
                            var old = m_config.Value;
                            var rem = value ? RedundancyRemoval.Runtime : RedundancyRemoval.None;
                            m_config.Value = new BackendConfiguration(old.execution, rem, old.sharing, old.sorting, old.useDebugOutput);
                        }
                    }
                }

                [Category("Engine")]
                public ExecutionEngine ExecutionEngine
                {
                    get { return m_config.Value.execution; }
                    set
                    {
                        using (Adaptive.Transaction)
                        {
                            var old = m_config.Value;
                            m_config.Value = new BackendConfiguration(value, old.redundancy, old.sharing, old.sorting, old.useDebugOutput);
                        }
                    }
                }


            }

            private PropertyGrid m_grid;

            public RenderTaskControl()
            {
                m_grid = new PropertyGrid();
                m_grid.Dock = DockStyle.Fill;
                Text = "Render Task";
                Controls.Add(m_grid);
            }

            public IModRef<BackendConfiguration> Config
            {
                get { return ((RenderConfig)m_grid.SelectedObject).Cell; }
                set { m_grid.SelectedObject = new RenderConfig(value); }
            }

        }
    }

    class Program
    {
        static IModRef<BackendConfiguration> SetScene(IRenderControl win)
        {
            var view = CameraView.LookAt(new V3d(2.0, 2.0, 2.0), V3d.Zero, V3d.OOI);
            var perspective =
                win.Sizes.Select(s => FrustumModule.perspective(60.0, 0.1, 10.0, ((float)s.X / (float)s.Y)));


            var viewTrafo = DefaultCameraController.control(win.Mouse, win.Keyboard, win.Time, view);

            var index = new[] { 0, 1, 2, 0, 2, 3 };
            var positions = new[] { new V3f(-1, -1, 0), new V3f(1, -1, 0), new V3f(1, 1, 0), new V3f(-1, 1, 0) };

            var attributes = new SymbolDict<Array>()
                {
                    { DefaultSemantic.Positions, positions }
                };

            var quad = new IndexedGeometry(IndexedGeometryMode.TriangleList,
                    (Array)index,
                    attributes,
                    new SymbolDict<Object>());

            var quadSg = quad.ToSg();

            // This is one of the hardest parts in C#. Our FShade interface is rather generic and
            // works super awesome in F#. In C# however, without proper type inference doing simple function
            // calls suddenly requires a Phd in CS. Therefore, and especially since shaders cannot be written
            // in C# anyways, write application setup and shader code in F# ;)
            // Or don't use FShade. FShade simply does not work in without functional language 
            // features such as type inference and function currying.
            Func<DefaultSurfaces.Vertex, FSharpExpr<V4d>> whiteShader =
                    HighFun.ApplyArg0<C4f, DefaultSurfaces.Vertex, FSharpExpr<V4d>>(
                        DefaultSurfaces.constantColor, C4f.White);
            Func<DefaultSurfaces.Vertex, FSharpExpr<DefaultSurfaces.Vertex>> trafo = c => DefaultSurfaces.trafo(c);

            var sg =
                quadSg
                .WithEffects(new[] {
                        FShadeSceneGraph.toEffect(FSharpFuncUtil.Create(trafo)),
                        FShadeSceneGraph.toEffect(FSharpFuncUtil.Create(whiteShader)),
                })
                .ViewTrafo(viewTrafo.Select(t => t.ViewTrafo))
                .ProjTrafo(perspective.Select(t => t.ProjTrafo()));

            var config = new ModRef<BackendConfiguration>(BackendConfigurationModule.UnmanagedOptimized);
            var task = ((Aardvark.Rendering.GL.Runtime)win.Runtime).CompileRender(win.FramebufferSignature, config, Aardvark.SceneGraph.Semantics.RenderObjectSemantics.Semantic.renderObjects(sg));

            win.RenderTask = DefaultOverlays.withStatistics(task);

            return config;
        }

        static void RunUI()
        {
            var form = new Form();
            form.IsMdiContainer = true;
            form.Width = 1024;
            form.Height = 768;
            form.Padding = new Padding(0);

            var main = new DockPanel();
            main.Dock = DockStyle.Fill;

            main.DocumentStyle = DocumentStyle.DockingMdi;
            main.Parent = form;
            main.Theme = new WeifenLuo.WinFormsUI.Docking.VS2012LightTheme();
            var t = new UserInterfaceStuff.Test();
            t.Text = "Test Dock Control";
            t.Show(main, DockState.DockRight);

     
            var f = new UserInterfaceStuff.RenderControlContent();

            var app = new OpenGlApplication();
            f.Init(app);
            var config = SetScene(f);

            
            f.Show(main, DockState.Document);


            form.Controls.Add(main);

            var ctrl = new UserInterfaceStuff.RenderTaskControl();
            ctrl.Config = config;
            ctrl.Show(main, DockState.DockRight);


            Application.Run(form);
        }

        static void RunSimple()
        {
            using (var app = new OpenGlApplication())
            {
                var win = app.CreateSimpleRenderWindow(1);
                SetScene(win);
                win.Run();
            }
        }


        [STAThread]
        static void Main(string[] args)
        {
            Ag.initialize();
            Aardvark.Base.Aardvark.Init();
            RunUI();
        }
    }
}
