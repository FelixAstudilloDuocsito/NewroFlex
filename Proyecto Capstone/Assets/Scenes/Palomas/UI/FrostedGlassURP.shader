Shader "NeuroFlex/UI/FrostedGlassURP"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite alpha mask", 2D) = "white" {}
        _GlassTint ("Glass tint", Color) = (0.12, 0.22, 0.30, 1)
        _TintStrength ("Tint strength", Range(0,1)) = 0.50
        _GlassOpacity ("Glass opacity", Range(0,1)) = 1.0
        _BlurRadius ("Blur radius in screen pixels", Range(0,8)) = 2.0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            Name "FrostedGlass"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
              half4 _GlassTint;
              half _TintStrength;
              half _GlassOpacity;
              half _BlurRadius;
            CBUFFER_END
            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings {
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD1;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.screenPos.xy / max(input.screenPos.w, 0.00001);
                float2 delta = _BlurRadius / _ScaledScreenParams.xy;
                // Five samples on the already downsampled URP opaque texture.
                half3 scene = SampleSceneColor(uv) * 0.36h;
                scene += SampleSceneColor(uv + float2(delta.x,0)) * 0.16h;
                scene += SampleSceneColor(uv - float2(delta.x,0)) * 0.16h;
                scene += SampleSceneColor(uv + float2(0,delta.y)) * 0.16h;
                scene += SampleSceneColor(uv - float2(0,delta.y)) * 0.16h;
                half3 glass = lerp(scene, _GlassTint.rgb, _TintStrength);
                half alpha = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv).a
                         * input.color.a * _GlassOpacity;
                return half4(glass, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "UI/Default"
}
