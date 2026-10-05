// Additive glow for floor markers, halos and light beams. Cheap: no lighting, no textures.
// _Shape: 0 = radial blob (UV centre bright), 1 = strip (bright along U centre line), 2 = beam (fades up V).
Shader "MazeRunner/Glow"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 0.5, 0.2, 1)
        _Shape ("Shape", Float) = 0
        _Softness ("Softness", Range(0.5, 4)) = 1.6
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Shape;
                half _Softness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float2 p = i.uv * 2.0 - 1.0;
                half a;
                if (_Shape < 0.5)       a = saturate(1.0 - length(p));
                else if (_Shape < 1.5)  a = saturate(1.0 - abs(p.y)) * saturate((1.0 - abs(p.x)) * 6.0);
                else                    a = saturate(1.0 - abs(p.x)) * saturate(1.0 - i.uv.y);
                a = pow(a, _Softness);
                return half4(_Color.rgb * a * _Color.a, 0);
            }
            ENDHLSL
        }
    }
}
