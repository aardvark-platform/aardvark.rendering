using System;
using Aardvark.Base;
using Aardvark.Rendering;
using Aardvark.SceneGraph;
using Aardvark.SceneGraph.CSharp;
using FSharp.Data.Adaptive;
using CSharp.Data.Adaptive;
using Microsoft.FSharp.Collections;

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
            runtime.CreateFramebuffer(signature, attachments);

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
