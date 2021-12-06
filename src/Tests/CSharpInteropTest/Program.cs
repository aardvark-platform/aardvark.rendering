using System;
using Aardvark.Base;
using Aardvark.Rendering;
using Aardvark.Rendering.CSharp;
using Aardvark.SceneGraph;
using Aardvark.SceneGraph.CSharp;
using FSharp.Data.Adaptive;
using CSharp.Data.Adaptive;
using Microsoft.FSharp.Collections;
using System.Collections.Generic;

// This is just a place to test if and how F# patterns used in rendering can be accessed in C#

namespace CSharpInteropTest
{
    class Program
    {
        static void Main(string[] args)
        {
            IRuntime runtime = null;
            IFramebufferSignature signature = null;
            FSharpMap<Symbol, IAdaptiveValue<IFramebufferOutput>> attachments = null;
            IBackendTexture texture = null;
            runtime.CreateFramebuffer(signature, attachments);
            runtime.CompileClear(signature, C4b.White);
            runtime.ClearColor(texture, C3b.White);

            var colorsMap =
                MapModule.OfArray(new[] {
                    Tuple.Create(DefaultSemantic.Colors, (ClearColor) C3b.Turquoise),
                    Tuple.Create(DefaultSemantic.Normals, (ClearColor) V4f.Zero),
                });

            ClearValues values =
                ClearValues.Empty
                .Color(C3us.Azure)
                .Colors(colorsMap)
                .Color(DefaultSemantic.Positions, V3f.Zero)
                .Color(DefaultSemantic.DiffuseColorUTangents, V3d.Zero)
                .Depth(1.0)
                .Stencil(0);

            Console.WriteLine("Hello World!");

            var geo = IndexedGeometryPrimitives.Box.solidBox(Box3d.Infinite, C4b.Red);

            var adaptiveBuffer = (IAdaptiveValue<V3f[]>)AdaptiveValue.Init(new V3f[5]);
            var adaptiveArray = (IAdaptiveValue<Array>)AdaptiveValue.Init((Array)new V3f[5]);

            var blendMode = new BlendMode { SourceColorFactor = BlendFactor.ConstantAlpha };
            
            ISg sg = new Sg.RenderNode(0, IndexedGeometryMode.PointList);

            var test = 
                    sg 
                    .VertexAttribute(DefaultSemantic.Positions, adaptiveBuffer)
                    .VertexAttribute(DefaultSemantic.Positions, adaptiveArray)
                    .VertexAttribute(DefaultSemantic.Positions, new V3f[10])
                    .VertexAttribute(DefaultSemantic.Positions, (Array)new V4f[10])
                    .VertexIndices(geo.IndexArray);
        }
    }
}
