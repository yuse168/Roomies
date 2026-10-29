// 遠景用の軽いトゥーンシェーダー（URP）。V2b：霞の距離を「マップの中心からの水平距離」で測れる（_HazeWorldRadius = 1）。
// カメラがどこにいても、マップの近くの地面や町は霞まず、外側の山ほど空の色に溶ける。
// ・光は2段＋やわらかい境目（影は落とさない・受けない）
// ・高さのグラデーション（ふもと暗め → 上明るめ）。UV0.y に 0..1 の高さが入っている前提（無ければ一定）
// ・雪（UV0.y が _SnowLine より上を白へ）
// ・距離の霞み：カメラから遠いほど _HazeColor（空の地平線の色）へ寄せる。シーンのフォグ設定は使わない
// ・昼夜：メインライトの明るさ（DayManager が朝 1.2／夜 0.12 に切り替える）を見て、夜は暗く・霞は夜の色にする
Shader "Roomies/FarBackdropToonV2b"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.36, 0.55, 0.55, 1)
        _TopTint ("Top Tint (高い所の色の乗算)", Color) = (1.15, 1.15, 1.1, 1)
        _BottomTint ("Bottom Tint (ふもとの色の乗算)", Color) = (0.78, 0.82, 0.86, 1)
        _UseHeightGradient ("Use Height Gradient (UV0.y)", Range(0,1)) = 1
        _ShadowColor ("Shadow Color (陰の色の乗算)", Color) = (0.72, 0.76, 0.9, 1)
        _ShadeThreshold ("Shade Threshold", Range(-1,1)) = 0.05
        _ShadeSoftness ("Shade Softness", Range(0.001,1)) = 0.35
        _SnowColor ("Snow Color", Color) = (0.96, 0.97, 1, 1)
        _SnowLine ("Snow Line (UV0.y, 1以上で無効)", Range(0,1.01)) = 1.01
        _SnowSoftness ("Snow Softness", Range(0.001,0.3)) = 0.06
        _HazeColor ("Haze Color (地平線の空の色)", Color) = (0.74, 0.82, 0.92, 1)
        _HazeStart ("Haze Start (m)", Float) = 60
        _HazeEnd ("Haze End (m)", Float) = 900
        _HazeMax ("Haze Max", Range(0,1)) = 0.85
        _HazePower ("Haze Curve", Range(0.3,3)) = 0.8
        _HazeWorldRadius ("Haze By Map Radius (1=マップ中心から / 0=カメラから)", Range(0,1)) = 1
        _HazeCenter ("Haze Center (マップの中心 XZ)", Vector) = (0, 0, 0, 0)
        _DayLightLuminance ("Day Light Luminance (この明るさ以上で昼)", Float) = 1.0
        _NightHazeColor ("Night Haze Color (夜の地平線の色)", Color) = (0.08, 0.11, 0.20, 1)
        _NightBrightness ("Night Brightness (夜の暗さ)", Range(0,1)) = 0.06
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _TopTint, _BottomTint, _ShadowColor, _SnowColor, _HazeColor;
                half _UseHeightGradient, _ShadeThreshold, _ShadeSoftness, _SnowLine, _SnowSoftness;
                float _HazeStart, _HazeEnd; half _HazeMax, _HazePower;
                half4 _NightHazeColor; half _DayLightLuminance, _NightBrightness;
                half _HazeWorldRadius; float4 _HazeCenter;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; float2 uv : TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID };

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                Light mainLight = GetMainLight();
                float3 n = normalize(i.normalWS);
                half ndl = dot(n, mainLight.direction);
                half lit = smoothstep(_ShadeThreshold - _ShadeSoftness * 0.5, _ShadeThreshold + _ShadeSoftness * 0.5, ndl);

                half h = saturate(i.uv.y);
                half3 grad = lerp(half3(1,1,1), lerp(_BottomTint.rgb, _TopTint.rgb, h), _UseHeightGradient);
                half3 col = _BaseColor.rgb * grad;

                half snow = smoothstep(_SnowLine - _SnowSoftness, _SnowLine + _SnowSoftness, h + (n.y - 0.5) * 0.08);
                col = lerp(col, _SnowColor.rgb, snow);

                half lum = dot(mainLight.color.rgb, half3(0.2126, 0.7152, 0.0722));
                half day = saturate(lum / max(_DayLightLuminance, 0.001));
                day *= day;                                                         // 1 昼 → 0 夜（夜側を早めに暗く）
                half3 tint = mainLight.color.rgb / max(lum, 0.001);               // 明るさを除いた光の色
                half3 lightCol = lerp(half3(1,1,1), tint, lerp(0.9, 0.35, day));   // 夜は光の色（青み）を強く
                col *= lerp(_ShadowColor.rgb, lightCol, lit) * lerp(_NightBrightness, 1.0, day);
                col += SampleSH(n) * 0.12 * col;

                float camDist = distance(i.positionWS, GetCameraPositionWS());
                float mapDist = length(i.positionWS.xz - _HazeCenter.xz);
                float dist = lerp(camDist, mapDist, _HazeWorldRadius);
                half haze = saturate((dist - _HazeStart) / max(1.0, _HazeEnd - _HazeStart));
                haze = pow(haze, _HazePower) * _HazeMax;
                col = lerp(col, lerp(_NightHazeColor.rgb, _HazeColor.rgb, day), haze);
                return half4(col, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 p : POSITION; };
            float4 vert (A v) : SV_POSITION { return TransformObjectToHClip(v.p.xyz); }
            half frag () : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack Off
}
