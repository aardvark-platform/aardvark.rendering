
#if WINDOWS

using Aardvark.Base;
using Aardvark.Base.Incremental.CSharp;
using Aardvark.SceneGraph;
using System.Windows;
using Aardvark.SceneGraph.CSharp;
using Aardvark.Base.Rendering;
using Effects = Aardvark.Base.Rendering.Effects;

namespace WpfDemo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            Ag.initialize(); // initialize scenegraph
            Aardvark.Base.Aardvark.Init(); // initialize aardvark base modules

            var app = new Aardvark.Application.WPF.OpenGlApplication(true);
            InitializeComponent();
            app.Initialize(renderControl, 1);

            var cone = IndexedGeometryPrimitives.solidCone(V3d.OOO, V3d.OOI, 1.0, 0.2, 12, C4b.Red).ToSg(); // build object from indexgeometry primitives
            var cube = SgPrimitives.Sg.box(Mod.Init(C4b.Blue), Mod.Init(Box3d.Unit)); // or directly using scene graph
            var initialViewTrafo = CameraViewModule.lookAt(V3d.III * 3.0, V3d.OOO, V3d.OOI);
            var controlledViewTrafo = Aardvark.Application.DefaultCameraController.control(renderControl.Mouse, renderControl.Keyboard, 
                    renderControl.Time, initialViewTrafo);
            var frustum = renderControl.Sizes.Select(size => FrustumModule.perspective(60.0, 0.1, 10.0, size.X / (float)size.Y));

            var whiteShader = Aardvark.Base.Rendering.Effects.SimpleLighting.Effect;
            var trafo = Effects.Trafo.Effect;

            var currentAngle = 0.0;
            var angle = renderControl.Time.Select(t =>
            {
                if (checkBox.IsChecked.Value)
                {
                    return currentAngle += 0.001;
                }
                else return currentAngle;
            });
            var rotatingTrafo = angle.Select((whyCantShadowName) => Trafo3d.RotationZ(whyCantShadowName));

            var sg =
                new[] { cone, cube.Trafo(rotatingTrafo) }
                .ToSg()
                .WithEffects(new[] { trafo, whiteShader })
                .ViewTrafo(controlledViewTrafo.Select(c => c.ViewTrafo))
                .ProjTrafo(frustum.Select(f => f.ProjTrafo()));

            renderControl.RenderTask =
                    Aardvark.Base.RenderTask.ofArray(
                            new[] {
                                app.Runtime.CompileClear(renderControl.FramebufferSignature, Mod.Init(C4f.Red)),
                                app.Runtime.CompileRender(renderControl.FramebufferSignature,sg)
                             }
                     );
        }
    }
}

#endif