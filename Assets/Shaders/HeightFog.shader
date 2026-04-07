Shader "Custom/HeightFog"
{
    Properties {}

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "HeightFog"
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float4  _HeightFogColor;
            float   _HeightFogDensity;
            float   _HeightFogStart;
            float   _HeightFogEnd;
            float   _DistanceFogDensity;

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float depth = SampleSceneDepth(uv);

                // Skybox: niente nebbia
                #if defined(UNITY_REVERSED_Z)
                    if (depth <= 0.0001) return sceneColor;
                #else
                    if (depth >= 0.9999) return sceneColor;
                #endif

                float3 worldPos = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);

                // Height fog: densa in basso, si dissipa salendo
                float heightRange = max(_HeightFogEnd - _HeightFogStart, 0.001);
                float heightT = saturate((worldPos.y - _HeightFogStart) / heightRange);
                float heightDensity = (1.0 - heightT) * _HeightFogDensity;

                // Distance fog: contributo atmosferico a distanza
                float dist = length(worldPos - _WorldSpaceCameraPos.xyz);
                float distFog = 1.0 - exp(-dist * dist * _DistanceFogDensity);

                float fogFactor = saturate(heightDensity + distFog * (1.0 - heightDensity * 0.5));

                return lerp(sceneColor, _HeightFogColor, fogFactor);
            }
            ENDHLSL
        }
    }
}
