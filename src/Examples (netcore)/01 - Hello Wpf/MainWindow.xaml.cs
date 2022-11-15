using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using FSharp.Data.Adaptive;
using CSharp.Data.Adaptive;
using Microsoft.FSharp.Core;

using Aardvark.Base;
using Aardvark.SceneGraph;
using Aardvark.SceneGraph.CSharp;
using Aardvark.Rendering.CSharp;
using Aardvark.Rendering;
using Effects = Aardvark.Rendering.Effects;

namespace _01___Hello_Wpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            Aardvark.Base.Aardvark.Init(); // initialize aardvark base modules

            var app = new Aardvark.Application.WPF.OpenGlApplication(true);
            InitializeComponent();
            app.Initialize(renderControl, 1);

            var cone = IndexedGeometryPrimitives.Cone.solidCone(V3d.OOO, V3d.OOI, 1.0, 0.2, 20, C4b.Red).ToSg(); // build object from indexgeometry primitives
            var cube = SgPrimitives.Sg.box(AValModule.constant(C4b.Blue), AValModule.constant(Box3d.Unit)); // or directly using scene graph
            var initialViewTrafo = CameraViewModule.lookAt(V3d.III * 3.0, V3d.OOO, V3d.OOI);
            var controlledViewTrafo = Aardvark.Application.DefaultCameraController.control(renderControl.Mouse, renderControl.Keyboard,
                    renderControl.Time, initialViewTrafo);
            var frustum = renderControl.Sizes.Map(size => FrustumModule.perspective(60.0, 0.1, 10.0, size.X / (float)size.Y));

            var whiteShader = Effects.SimpleLighting.Effect;
            var trafo = Effects.Trafo.Effect;

            var currentAngle = 0.0;
            var angle = renderControl.Time.Map(t =>
            {
                if (checkBox.IsChecked!.Value)
                {
                    return currentAngle += 0.001;
                }
                else return currentAngle;
            });
            var rotatingTrafo = angle.Map(Trafo3d.RotationZ);

            var sg =
                new[] { cone.FillMode(AValModule.constant(FillMode.Line)), cube.Trafo(rotatingTrafo) }
                .ToSg()
                .WithEffects(new[] { 
                    trafo, 
                    whiteShader 
                 })
                .ViewTrafo(controlledViewTrafo.Map(c => c.ViewTrafo))
                .ProjTrafo(frustum.Map(f => f.ProjTrafo()));

            renderControl.RenderTask =
                    Aardvark.Rendering.RenderTask.ofArray(
                            new[] {
                                app.Runtime.CompileClear(renderControl.FramebufferSignature, Clear.Color(C4f.Black).Depth(1.0)),
                                app.Runtime.CompileRender(renderControl.FramebufferSignature,sg)
                             }
                     );
        }
    }
}
