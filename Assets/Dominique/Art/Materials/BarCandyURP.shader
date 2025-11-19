Shader "URP/BarCandy"
{
    Properties
    {
        _BaseColor   ("Base Color (Bottom)", Color) = (0.9, 0.1, 0.35, 1) // hot pink/red
        _TopColor    ("Top Color (Highlight)", Color) = (1.0, 0.5, 0.8, 1)
        _RimColor    ("Rim Color", Color) = (1, 0.2, 0.6, 1)
        _RimPower    ("Rim Power", Range(0.1, 8)) = 2.5

        _StripeTiling("Stripe Tiling (V)", Range(0.1, 10)) = 2
        _StripeSpeed ("Stripe Speed", Range(-5, 5)) = 0.6
        _StripeIntensity ("Stripe Intensity", Range(0, 1)) = 0.25

        _Pulse       ("Emission Pulse", Range(0, 1)) = 0.4
        _TopCapBoost ("Top Cap Boost", Range(0, 3)) = 1.5

        _HeightScale ("World Height Scale", Float) = 4.0
        _Alpha       ("Alpha", Range(0,1)) = 1
    }

    SubShader
    {
        Tags{
            "RenderPipeline"="UniversalRenderPipeline"
            "Queue"="Geometry"
            "RenderType"="Opaque"
            "IgnoreProjector"="True"
            "UniversalMaterialType"="Unlit"
        }
        LOD 100

        Pass
        {
            Name "ForwardUnlit"
            Tags{"LightMode"="UniversalForward"}
            Cull Back
            ZWrite On
            ZTest LEqual
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 posCS   : SV_POSITION;
                float3 posWS   : TEXCOORD0;
                float3 normalWS: TEXCOORD1;
                float2 uv      : TEXCOORD2;
                float  heightT : TEXCOORD3; // 0..1 along world Y
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _TopColor;
                half4 _RimColor;
                half  _RimPower;

                half  _StripeTiling;
                half  _StripeSpeed;
                half  _StripeIntensity;

                half  _Pulse;
                half  _TopCapBoost;

                float _HeightScale;
                half  _Alpha;
            CBUFFER_END

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 nrmWS = TransformObjectToWorldNormal(v.normalOS);

                // Normalize height across bars using world Y (tweak _HeightScale to match tallest bar)
                // heightT in 0..1 -> bottom to top
                float h = saturate((posWS.y - _WorldSpaceCameraPos.y + _HeightScale) / (2.0 * _HeightScale)); // stable-ish default
                o.heightT = saturate(h);

                o.posCS = TransformWorldToHClip(posWS);
                o.posWS = posWS;
                o.normalWS = normalize(nrmWS);
                o.uv = v.uv;
                return o;
            }

            // tiny hash noise for soft breakup
            float n3(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            half4 frag(v2f i) : SV_Target
            {
                // base vertical gradient (bottom -> top)
                half3 grad = lerp(_BaseColor.rgb, _TopColor.rgb, i.heightT);

                // moving stripes (screen-constant look not needed; this is subtle)
                float stripe = sin(i.heightT * _StripeTiling * 6.2831 + _Time.y * _StripeSpeed);
                stripe = saturate(stripe * 0.5 + 0.5); // 0..1
                grad += stripe * _StripeIntensity * grad;

                // rim/fresnel to make the bars pop in VR
                float3 V = normalize(_WorldSpaceCameraPos - i.posWS);
                float fres = pow(1.0 - saturate(dot(normalize(i.normalWS), V)), _RimPower);
                grad += _RimColor.rgb * fres;

                // brighten the very top face a bit
                float topCap = saturate(dot(i.normalWS, float3(0,1,0))) * _TopCapBoost;
                grad += topCap * _TopColor.rgb * 0.25;

                // soft micro-breakup to avoid banding
                grad += (n3(i.posWS.xz * 2.7) - 0.5) * 0.03;

                return half4(grad, _Alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
