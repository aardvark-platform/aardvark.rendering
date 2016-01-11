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

namespace CSharpDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            Ag.initialize();
            Aardvark.Base.Aardvark.Init();

            using (var app = new OpenGlApplication())
            {
                var win = app.CreateSimpleRenderWindow(1);

                var view = CameraView.LookAt(new V3d(2.0, 2.0, 2.0), V3d.Zero, V3d.OOI);
                var perspective =
                    win.Sizes.Select(s => FrustumModule.perspective(60.0,0.1,10.0,((float) s.X / (float) s.Y)));


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

                var task = app.Runtime.CompileRender(win.FramebufferSignature, sg);

                win.RenderTask = DefaultOverlays.withStatistics(task);
                win.Run();
             }
        }
    }
}
