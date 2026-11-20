Shader "Hako/GhostSilhouette"
{
    Properties { _Tint ("Tint", Color) = (0,1,1,.4)
                 _Solid ("Core opacity", Range(0,1)) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            half4 _Tint;
            half _Solid;
            struct Varyings { float4 clip : SV_POSITION; float3 world : TEXCOORD0; half3 normal : TEXCOORD1; };
            Varyings vert(float4 vertex : POSITION, float3 normal : NORMAL)
            {
                Varyings o;
                o.world = TransformObjectToWorld(vertex.xyz);
                o.clip = TransformWorldToHClip(o.world);
                o.normal = TransformObjectToWorldNormal(normal);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                half rim = pow(1 - saturate(abs(dot(normalize(i.normal), GetWorldSpaceNormalizeViewDir(i.world)))), 2);
                half ripple = .5 + .5 * sin(i.world.y * 15 - _Time.y * 2.5);
                half diffuse = saturate(dot(normalize(i.normal), normalize(float3(-.45,.72,-.40))));
                half brightness = .42 + .34 * diffuse + .40 * rim + .11 * ripple;
                return half4(_Tint.rgb * brightness, max(_Solid, _Tint.a * (.38 + .58 * rim + .14 * ripple)));
            }
            ENDHLSL
        }
    }
}
