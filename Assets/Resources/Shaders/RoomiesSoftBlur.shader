Shader "UI/RoomiesSoftBlur"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _BlurSize ("Blur Size", Range(0, 4)) = 1.5
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _BlurSize;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 stepSize = _MainTex_TexelSize.xy * _BlurSize;
                fixed4 color = tex2D(_MainTex, input.uv) * 0.20;
                color += tex2D(_MainTex, input.uv + float2(stepSize.x, 0)) * 0.12;
                color += tex2D(_MainTex, input.uv - float2(stepSize.x, 0)) * 0.12;
                color += tex2D(_MainTex, input.uv + float2(0, stepSize.y)) * 0.12;
                color += tex2D(_MainTex, input.uv - float2(0, stepSize.y)) * 0.12;
                color += tex2D(_MainTex, input.uv + stepSize) * 0.08;
                color += tex2D(_MainTex, input.uv - stepSize) * 0.08;
                color += tex2D(_MainTex, input.uv + float2(stepSize.x, -stepSize.y)) * 0.08;
                color += tex2D(_MainTex, input.uv + float2(-stepSize.x, stepSize.y)) * 0.08;
                return color * input.color;
            }
            ENDCG
        }
    }
}
