using System;
using Aardvark.Base;
using Aardvark.Rendering;
using Aardvark.SceneGraph;
using FSharp.Data.Adaptive;
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
        }
    }
}
