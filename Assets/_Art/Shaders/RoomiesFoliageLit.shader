// 葉と草の URP Lit（アルファの切り抜き・両面・風で揺れる）。2026/11/13 自然物の作り直し。
// 揺れは頂点だけ（軽い）。_WindMode 0：木（物体の高さと幹からの距離で揺れが大きくなる）、1：草（UV の上ほど揺れる）。
// カメラから _WindFadeDistance より遠いと揺れは止まる（遠い LOD はほぼ止まって見える）。
Shader "Roomies/Foliage Lit"
{
    Properties
    {
        [MainColor] _BaseColor ("Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap ("Albedo (alpha = cutout)", 2D) = "white" {}
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0, 2)) = 1
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _Smoothness ("Smoothness", Range(0, 1)) = 0.2
        _Translucency ("Back Light", Range(0, 1)) = 0.35
        [Enum(Tree, 0, Grass, 1)] _WindMode ("Wind Mode", Float) = 0
        _WindStrength ("Wind Strength (m)", Range(0, 1)) = 0.12
        _WindSpeed ("Wind Speed", Range(0, 5)) = 1.3
        _WindScale ("Wind Wave Size", Range(0, 4)) = 1
        _WindHeight ("Tree Height For Wind (m)", Float) = 8
        _WindFadeDistance ("Wind Fade Distance (m)", Float) = 60
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _BumpScale;
            half _Cutoff;
            half _Smoothness;
            half _Translucency;
            float _WindMode;
            float _WindStrength;
            float _WindSpeed;
            float _WindScale;
            float _WindHeight;
            float _WindFadeDistance;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

        // 風で動かしたワールド座標
        float3 WindWS(float3 positionOS, float2 uv)
        {
            float3 positionWS = TransformObjectToWorld(positionOS);
            float w;
            if (_WindMode > 0.5) w = uv.y * uv.y;
            else
            {
                float h = saturate(positionOS.y / max(_WindHeight, 0.01));
                w = h * h + length(positionOS.xz) * 0.08;
            }
            float t = _Time.y * _WindSpeed;
            float phase = dot(positionWS.xz, float2(0.13, 0.09)) * _WindScale;
            float sway = sin(t + phase) * 0.7 + sin(t * 2.3 + phase * 1.7) * 0.3;
            float flutter = sin(t * 6.1 + dot(positionWS, float3(1.7, 2.3, 1.1)));
            float fade = saturate(1 - distance(positionWS, _WorldSpaceCameraPos) / max(_WindFadeDistance, 0.01));
            float3 dir = float3(0.86, 0, 0.51);
            float3 offset = dir * (sway + 0.25 * flutter) + float3(0, 0.1 * flutter, 0);
            return positionWS + offset * w * _WindStrength * fade;
        }

        half Alpha(float2 uv) { return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a * _BaseColor.a; }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float2 uv = TRANSFORM_TEX(input.uv, _BaseMap);
                float3 positionWS = WindWS(input.positionOS.xyz, input.uv);
                o.positionWS = positionWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.uv = uv;
                VertexNormalInputs n = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                o.normalWS = n.normalWS;
                o.tangentWS = half4(n.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip(tex.a * _BaseColor.a - _Cutoff);

                half3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                half3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                float3 normalWS = normalize(TransformTangentToWorld(nTS, half3x3(input.tangentWS.xyz, bitangent, input.normalWS)));

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = _BaseColor.rgb * tex.rgb;
                surface.metallic = 0;
                surface.smoothness = _Smoothness;
                surface.specular = half3(0, 0, 0);
                surface.normalTS = half3(0, 0, 1);
                surface.occlusion = 1;
                surface.alpha = 1;

                half4 color = UniversalFragmentPBR(inputData, surface);
                // 葉の裏から日が透ける分（太陽と向き合うほど明るく）
                Light mainLight = GetMainLight(inputData.shadowCoord);
                half back = saturate(dot(-inputData.viewDirectionWS, mainLight.direction));
                color.rgb += surface.albedo * mainLight.color * back * back * _Translucency * mainLight.shadowAttenuation;
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return half4(color.rgb, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings ShadowVert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = WindWS(input.positionOS.xyz, input.uv);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - positionWS);
            #else
                float3 lightDir = _LightDirection;
            #endif
                o.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir)));
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }
            half4 ShadowFrag(Varyings input) : SV_Target { clip(Alpha(input.uv) - _Cutoff); return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings DepthVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformWorldToHClip(WindWS(input.positionOS.xyz, input.uv));
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }
            half DepthFrag(Varyings input) : SV_Target { clip(Alpha(input.uv) - _Cutoff); return input.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex NormalsVert
            #pragma fragment NormalsFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half3 normalWS : TEXCOORD0; float2 uv : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings NormalsVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformWorldToHClip(WindWS(input.positionOS.xyz, input.uv));
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }
            void NormalsFrag(Varyings input, out half4 outNormalWS : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                clip(Alpha(input.uv) - _Cutoff);
                float3 normalWS = normalize(input.normalWS);
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct = PackNormalOctQuadEncode(normalWS);
                outNormalWS = half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0);
            #else
                outNormalWS = half4(normalWS, 0);
            #endif
            #ifdef _WRITE_RENDERING_LAYERS
                outRenderingLayers = EncodeMeshRenderingLayer();
            #endif
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
