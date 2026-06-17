Shader "Custom/TopFaceShader"
{
    Properties
    {
        _MainTex ("Side Texture", 2D) = "white" {}
        _TopTex ("Top Texture", 2D) = "white" {}
        _HasTopTex ("Has Top Texture", Float) = 0
        _TopColor ("Top Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        // pass principal — rendering normal
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_TopTex);  SAMPLER(sampler_TopTex);
            float  _HasTopTex;
            float4 _TopColor;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float3 positionWS   : TEXCOORD2;
                float4 shadowCoord  : TEXCOORD3;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = posInputs.positionCS;
                OUT.positionWS  = posInputs.positionWS;
                OUT.uv          = IN.uv;
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                // calcular coordenadas de sombra
                OUT.shadowCoord = GetShadowCoord(posInputs);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                bool isTop = _HasTopTex > 0.5 && IN.normalWS.y > 0.5;
                half4 texColor;
                if (isTop)
                    texColor = SAMPLE_TEXTURE2D(_TopTex, sampler_TopTex, IN.uv) * _TopColor;
                else
                    texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // direção da luz hardcoded — simula luz a vir de cima e da esquerda
                float3 lightDir = normalize(float3(-2,0.7,1));
                float diffuse = saturate(dot(normalize(IN.normalWS), lightDir));

                // mínimo de 0.4 para não ficar completamente preto nas faces de sombra
                float lighting = 0.4 + 2 * diffuse;

                texColor.rgb *= lighting;
                return texColor;
            }
            ENDHLSL
        }

        // pass de sombras — necessário para o objeto projetar sombras noutros objetos
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }
}