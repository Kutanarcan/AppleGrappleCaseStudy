Shader "Custom/2D/SpriteFillHealthBar"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FillAmount ("Fill Amount", Range(0,1)) = 1
        _FillDirection ("Fill Direction (0=Left->Right, 1=Right->Left, 2=Bottom->Top, 3=Top->Bottom)", Float) = 0
        _SoftEdge ("Soft Edge Width", Range(0, 0.1)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
            "RenderPipeline"="UniversalPipeline"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color       : COLOR;
                float2 uv          : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _FillAmount;
                float _FillDirection;
                float _SoftEdge;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half4 col = texColor * IN.color;

                float coord;
                if (_FillDirection < 0.5)      coord = IN.uv.x;              // Left -> Right
                else if (_FillDirection < 1.5) coord = 1.0 - IN.uv.x;        // Right -> Left
                else if (_FillDirection < 2.5) coord = IN.uv.y;              // Bottom -> Top
                else                           coord = 1.0 - IN.uv.y;        // Top -> Down

                float edge = smoothstep(_FillAmount - _SoftEdge, _FillAmount, coord);
                col.a *= (1.0 - edge);

                return col;
            }
            ENDHLSL
        }
    }
}