namespace Aardvark.GLTF

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph

    
module internal EnvironmentMap =
        
    [<ReflectedDefinition>]
    module Shader =
        open FShade
        
        // r = 1
        // l = -1
        // n = 1
        // M44d(
        // 1.0,                     0.0,                       0.0,        0.0,
        // 0.0,                     1.0,                       0.0,        0.0,
        // 0.0,                     0.0,                       0.0,       -1.0,
        // 0.0,                     0.0,                      a,  b
        // )

      
        let skybox =
            samplerCube {
                texture uniform?Skybox
                filter Filter.MinMagLinearMipPoint
                addressU WrapMode.Wrap
                addressV WrapMode.Wrap
                addressW WrapMode.Wrap
            }
        
        let rand =
            sampler2d {
                texture uniform?Random
                filter Filter.MinMagMipPoint
                addressU WrapMode.Wrap
                addressV WrapMode.Wrap
            }
        
        type UniformScope with
            member x.SourceLevel : int = uniform?SourceLevel
            member x.LevelCount : int = uniform?LevelCount
            member x.IsDiffuse : bool = uniform?IsDiffuse
            
        [<GLSLIntrinsic("uint({0})")>]
        let suint32 (_v : uint32) : uint32 = onlyInShaderCode "asda"
        
        let radicalInverse (bits : uint32) =
            let bits = (bits <<< 16) ||| (bits >>> 16)
            let bits = ((bits &&& suint32 0x55555555u) <<< 1) ||| ((bits &&& suint32 0xAAAAAAAAu) >>> 1)
            let bits = ((bits &&& suint32 0x33333333u) <<< 2) ||| ((bits &&& suint32 0xCCCCCCCCu) >>> 2)
            let bits = ((bits &&& suint32 0x0F0F0F0Fu) <<< 4) ||| ((bits &&& suint32 0xF0F0F0F0u) >>> 4)
            let bits = ((bits &&& suint32 0x00FF00FFu) <<< 8) ||| ((bits &&& suint32 0xFF00FF00u) >>> 8)
            float bits * 2.3283064365386963e-10
            // float RadicalInverse_VdC(uint bits) 
            // {
            //     bits = (bits << 16u) | (bits >> 16u);
            //     bits = ((bits & 0x55555555u) << 1u) | ((bits & 0xAAAAAAAAu) >> 1u);
            //     bits = ((bits & 0x33333333u) << 2u) | ((bits & 0xCCCCCCCCu) >> 2u);
            //     bits = ((bits & 0x0F0F0F0Fu) << 4u) | ((bits & 0xF0F0F0F0u) >> 4u);
            //     bits = ((bits & 0x00FF00FFu) << 8u) | ((bits & 0xFF00FF00u) >> 8u);
            //     return float(bits) * 2.3283064365386963e-10; // / 0x100000000
            // }
        
        let hammersley (i : int) (n : int) =
            V2d(float i / float n, radicalInverse (uint32 i))
        
        let linearToSrgb (v : V4d) =
            let e = 1.0 / 2.2
            V4d(v.X ** e, v.Y ** e, v.Z ** e, v.W)
            
        let srgbToLinear (v : V4d) =
            let e = 2.2
            V4d(v.X ** e, v.Y ** e, v.Z ** e, v.W)
            
        let sampleEnv (worldDir : V3d) (roughness : float) =
            //let worldDir = uniform.ViewTrafoInv.TransformDir viewDir |> Vec.normalize
            
            let z = worldDir
            let x =
                if abs z.Z > 0.999 then Vec.cross z V3d.OIO |> Vec.normalize
                else Vec.cross z V3d.OOI |> Vec.normalize
            let y = Vec.cross z x
            
            let mutable sum = V4d.Zero
            let v = worldDir
            let n = worldDir
            let randSize = rand.Size
            let a = roughness * roughness
            let aSq = a*a
            for i in 0 .. 4096 do
                //let o = V2d.Zero //hammersley i 1024
                let h =
                    let o = rand.[V2i(i % randSize.X, i / randSize.X)].XY
                    let phi = o.X * Constant.PiTimesTwo
                    let mutable cosTheta = 0.0
                    let mutable sinTheta = 0.0
                    if uniform.IsDiffuse then
                        let t = o.Y * Constant.Pi - Constant.PiHalf
                        cosTheta <- cos t
                        sinTheta <- sin t
                    else
                        cosTheta <- sqrt((1.0 - o.Y) / (1.0 + (aSq - 1.0) * o.Y)) |> clamp -1.0 1.0
                        sinTheta <- sqrt (1.0 - cosTheta * cosTheta)
                    let fx = cos phi * sinTheta
                    let fy = sin phi * sinTheta
                    let fz = cosTheta
                    Vec.normalize (x * fx + y * fy + z * fz)
                
                let l = Vec.normalize (2.0 * Vec.dot v h * h - v)
                let nl = Vec.dot n l |> max 0.0
                
                if nl > 0.0 then
                    let c = skybox.SampleLevel(h, 0.0) |> srgbToLinear |> Vec.xyz
                    sum <- sum + V4d(c * nl, nl)
                
                
            sum.XYZ / sum.W
            
                   
        let sampleFace (v : Effects.Vertex) =
            fragment {
                // let dx = V2d(1.0 / (float uniform.ViewportSize.X), 0.0)
                // let dy = V2d(0.0, 1.0 / (float uniform.ViewportSize.Y))
                
                let p00 = v.pos.XY / v.pos.W
                // let p01 = p00 + dy
                // let p10 = p00 + dx
                // let p11 = p00 + dx + dy
                
                let s00 = uniform.ViewTrafoInv.TransformDir (V3d(p00, -1.0)) |> Vec.normalize
                // let s01 = uniform.ViewTrafoInv.TransformDir (V3d(p01, -1.0)) |> Vec.normalize
                // let s10 = uniform.ViewTrafoInv.TransformDir (V3d(p10, -1.0)) |> Vec.normalize
                // let s11 = uniform.ViewTrafoInv.TransformDir (V3d(p11, -1.0)) |> Vec.normalize
                //
                // let c00 = skybox.SampleLevel(s00, float uniform.SourceLevel)
                // let c01 = skybox.SampleLevel(s01, float uniform.SourceLevel)
                // let c10 = skybox.SampleLevel(s10, float uniform.SourceLevel)
                // let c11 = skybox.SampleLevel(s11, float uniform.SourceLevel)
                // return (c00 + c01 + c10 + c11) * 0.25
                
                let roughness = float (uniform.SourceLevel + 1) / float (uniform.LevelCount - 1)
                let res = sampleEnv s00 roughness
                return linearToSrgb (V4d(res, 1.0))
            }
    
          
        let pano =
            sampler2d {
                texture uniform?Panorama
                filter Filter.MinMagMipLinear
                addressU WrapMode.Wrap
                addressV WrapMode.Wrap
            }
         
        let samplePano (v : Effects.Vertex) =
            fragment {
                // let dx = V2d(1.0 / (float uniform.ViewportSize.X), 0.0)
                // let dy = V2d(0.0, 1.0 / (float uniform.ViewportSize.Y))
                
                let p00 = v.pos.XY / v.pos.W
                // let p01 = p00 + dy
                // let p10 = p00 + dx
                // let p11 = p00 + dx + dy
                
                let s00 = uniform.ViewTrafoInv.TransformDir (V3d(p00, -1.0)) |> Vec.normalize
    
                let x = (atan2 s00.Y s00.X) / Constant.PiTimesTwo + 0.5
                let y = asin s00.Z / Constant.Pi + 0.5
                
                return pano.SampleLevel(V2d(x,y), 0.0)
            }
    
    let private faces =
        [|
            0, CameraView.lookAt V3d.Zero V3d.IOO V3d.ONO
            1, CameraView.lookAt V3d.Zero V3d.NOO V3d.ONO
            2, CameraView.lookAt V3d.Zero V3d.OIO V3d.OOI
            3, CameraView.lookAt V3d.Zero V3d.ONO V3d.OON
            4, CameraView.lookAt V3d.Zero V3d.OOI V3d.ONO
            5, CameraView.lookAt V3d.Zero V3d.OON V3d.ONO
        |]
    
    let levelCount = 8

    let prepare (runtime : IRuntime) (src : ITexture) =
        let src = runtime.PrepareTexture src
        let diffuse = runtime.CreateTextureCube(src.Size.X, src.Format, src.MipMapLevels)
        
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, src.Format
            ]
            
        let view = cval (CameraView.lookAt V3d.Zero V3d.IOO V3d.OOI)
        let viewportSize = cval V2i.II
        let sourceLevel = cval 0
        let isDiffuse = cval false
        
        
        let random =
            let img = PixImage<float32>(Col.Format.RGBA, V2i(128, 128))
            let rand = RandomSystem()
            img.GetMatrix<C4f>().SetByIndex (fun _ ->
                rand.UniformV4f().ToC4f()
            ) |> ignore
            PixTexture2d(PixImageMipMap [| img :> PixImage |], TextureParams.empty) :> ITexture
        
        
        let task =
            Sg.fullScreenQuad
            |> Sg.shader {
                do! Shader.sampleFace
            }
            |> Sg.viewTrafo (view |> AVal.map CameraView.viewTrafo)
            |> Sg.texture' "Skybox" src
            |> Sg.uniform "IsDiffuse" isDiffuse
            |> Sg.uniform' "LevelCount" levelCount
            |> Sg.texture' "Random" random
            |> Sg.uniform "ViewportSize" viewportSize
            |> Sg.uniform "SourceLevel" sourceLevel
            |> Sg.depthTest' DepthTest.None
            |> Sg.compile runtime signature
            
        let mutable dstSize = max V2i.II (src.Size.XY / 2)
        let mutable dstLevel = 1
        while dstLevel < src.MipMapLevels do
            let srcLevel = dstLevel - 1
            transact (fun () ->
                viewportSize.Value <- dstSize
                sourceLevel.Value <- srcLevel
            )
             
            for index, cam in faces do
                use fbo = runtime.CreateFramebuffer(signature, [DefaultSemantic.Colors, src.[TextureAspect.Color, dstLevel, index] :> IFramebufferOutput])
                transact (fun () -> view.Value <- cam)
                task.Run(fbo)
                
            
            dstSize <- max V2i.II (dstSize / 2)
            dstLevel <- dstLevel + 1
        
        // diffuse
        transact (fun () ->
            viewportSize.Value <- src.Size.XY
            sourceLevel.Value <- 0
            isDiffuse.Value <- true
        )
         
        for index, cam in faces do
            use fbo = runtime.CreateFramebuffer(signature, [DefaultSemantic.Colors, diffuse.[TextureAspect.Color, 0, index] :> IFramebufferOutput])
            transact (fun () -> view.Value <- cam)
            task.Run(fbo)
        runtime.GenerateMipMaps(diffuse)
        
        // for level in 0 .. src.MipMapLevels - 1 do
        //     for face in 0 .. 5 do
        //     runtime.Download(src, level, face).Save (sprintf "/Users/schorsch/Desktop/textures/f%d_%d.jpg" face level)
        src, diffuse

    let ofPanorama (runtime : IRuntime) (img : ITexture) =
        let img = runtime.PrepareTexture img
        
        let cubeSize = Fun.NextPowerOfTwo img.Size.Y
        let result = runtime.CreateTextureCube(cubeSize, img.Format, int (floor (log2 (float cubeSize)) + 1.0))
        
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, img.Format
            ]
            
        let view = cval (CameraView.lookAt V3d.Zero V3d.IOO V3d.OOI)
        
    
        let task =
            Sg.fullScreenQuad
            |> Sg.shader {
                do! Shader.samplePano
            }
            |> Sg.viewTrafo (view |> AVal.map CameraView.viewTrafo)
            |> Sg.texture' "Panorama" img
            |> Sg.depthTest' DepthTest.None
            |> Sg.compile runtime signature
            
        for index, cam in faces do
            use fbo = runtime.CreateFramebuffer(signature, [DefaultSemantic.Colors, result.[TextureAspect.Color, 0, index] :> IFramebufferOutput])
            transact (fun () -> view.Value <- cam)
            task.Run(fbo)
 
        prepare runtime result
