// 夜空（第六轮，院外风景，2026-11-24）。天空盒材质：Materials/Sky/MX_NightSky（SkyBuilder.cs 生成、每次生成场景时对准月光）。
// <list>
// 云：Poly Haven kloppenheim_07_puresky（CC0，阴天有月），按经纬图自己采样（接缝处用连续的梯度，不出竖线）。
//     压暗 + 去饱和 + 偏冷（城市光害那一圈暖色也去掉：设定是没有一盏灯的废村）。
// 月亮：HDRI 自带的月亮比我们的月光高 10°，从旁边 3.5° 的云“克隆”过来盖掉；在 Moonlight 的反方向画月亮、光晕，
//     一层慢慢飘的薄云从月亮前面过（月亮忽明忽暗）。
// 远山：地平线上三层山脊（近、中、远），剖面是按方位角循环的一维噪声（不用贴图），近处那层山脊上一排杉树尖。
//     开雾（探索者）：比雾色略亮、比天空暗的一层层剪影，山脚有谷雾；雾里的院外模型都是雾色，正好压在这几层前面成黑剪影。
//     不开雾（箱女视角 GhostSight / 审图白天光）：按场景环境光（_GlossyEnvironmentColor）+ 月光打亮，和近处的模型一致。
// </list>
Shader "HakoOnna/NightSky"
{
    Properties
    {
        [NoScaleOffset] _Pano ("Sky HDRI (equirect)", 2D) = "black" {}
        _PanoOffset ("HDRI azimuth offset (turns)", Float) = 0.19646
        _Exposure ("Exposure", Float) = 0.16
        _Desat ("Night grade: desaturate", Range(0, 1)) = 0.8
        _SkyTint ("Night grade: tint (linear, normalised in the shader)", Vector) = (0.78, 0.88, 1.15, 0)
        _PanoMoonDir ("HDRI moon, world direction (SkyBuilder)", Vector) = (0, 1, 0, 0)
        _PanoMoonFix ("HDRI moon clone: radius deg, feather deg, source offset deg", Vector) = (1.15, 0.7, 3.6, 0)
        _MoonDir ("Moon, world direction towards it (SkyBuilder)", Vector) = (0.5064, 0.4695, -0.7233, 0)
        _MoonColor ("Moon colour (linear)", Vector) = (1.0, 0.95, 0.86, 0)
        _MoonIntensity ("Moon disc", Float) = 3.0
        _MoonRadius ("Moon radius (deg)", Float) = 0.62
        _HaloColor ("Halo colour (linear)", Vector) = (0.55, 0.66, 0.92, 0)
        _HaloIntensity ("Halo", Float) = 0.09
        _HaloWidth ("Halo falloff (deg)", Float) = 4.5
        _VeilColor ("Veil colour (linear)", Vector) = (0.62, 0.70, 0.88, 0)
        _VeilAmount ("Veil coverage", Range(0, 1)) = 0.55
        _VeilScale ("Veil scale", Float) = 1.4
        _VeilSpeed ("Veil drift (xy per second)", Vector) = (0.0045, 0.0016, 0, 0)
        _RidgeNear ("Near ridge, fogged (linear)", Vector) = (0.0175, 0.0205, 0.0290, 0)
        _RidgeMid ("Mid ridges, fogged (linear)", Vector) = (0.0215, 0.0255, 0.0355, 0)
        _RidgeFar ("Far ridges, fogged (linear)", Vector) = (0.0270, 0.0315, 0.0430, 0)
        _Mist ("Valley mist (linear)", Vector) = (0.0330, 0.0385, 0.0510, 0)
        _MistAmount ("Mist on near, mid, far ridges; mist depth (deg)", Vector) = (0.35, 0.55, 0.7, 2.2)
        _LandAlbedo ("Lit ridges: albedo near, mid, far; aerial", Vector) = (0.16, 0.13, 0.11, 0.55)
        _Ridges ("Ridge shape: near base, near amp, tree height, tree spacing (deg)", Vector) = (1.0, 3.0, 0.75, 0.34)
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            TEXTURE2D(_Pano);
            SAMPLER(sampler_Pano);

            CBUFFER_START(UnityPerMaterial)
                float _PanoOffset, _Exposure, _Desat;
                float4 _SkyTint, _PanoMoonDir, _PanoMoonFix, _MoonDir, _MoonColor;
                float _MoonIntensity, _MoonRadius;
                float4 _HaloColor;
                float _HaloIntensity, _HaloWidth;
                float4 _VeilColor;
                float _VeilAmount, _VeilScale;
                float4 _VeilSpeed, _RidgeNear, _RidgeMid, _RidgeFar, _Mist, _MistAmount, _LandAlbedo, _Ridges;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz; // 天空盒网格以相机为中心：物体空间坐标就是方向
                return o;
            }

            // ---------------------------------------------------------------- 噪声
            float Hash11(float n) { return frac(sin(n * 12.9898 + 4.1414) * 43758.5453); }
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }
            float Noise2(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i), b = Hash21(i + float2(1, 0)), c = Hash21(i + float2(0, 1)), d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }
            float Fbm2(float2 p)
            {
                float s = 0.0, a = 0.5;
                [unroll] for (int k = 0; k < 5; k++) { s += a * Noise2(p); p = p * 2.03 + 17.1; a *= 0.5; }
                return s / 0.96875;
            }
            // 方位角上循环的一维噪声：x ∈ [0,1)，freq 是整数（绕一圈正好 freq 格）
            float PNoise(float x, float freq, float seed)
            {
                float t = x * freq, i = floor(t), f = t - i;
                float a = Hash11(fmod(i, freq) + seed), b = Hash11(fmod(i + 1.0, freq) + seed);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(a, b, f);
            }
            // 山脊剖面（度）：ridged fbm，越往高频越弱
            float Ridge(float x, float seed, float f0, float rough)
            {
                float h = 0.0, a = 1.0, n = 0.0, f = f0;
                [unroll] for (int k = 0; k < 6; k++)
                {
                    float r = 1.0 - abs(2.0 * PNoise(x, f, seed + k * 31.7) - 1.0);
                    h += a * r * r;
                    n += a;
                    a *= rough;
                    f *= 2.0;
                }
                return h / n;
            }

            // 两个单位向量的夹角（度）；小角度用弦长算，比 acos 准
            float AngleDeg(float3 a, float3 b) { return degrees(2.0 * asin(saturate(length(a - b) * 0.5))); }

            bool FogOn()
            {
                bool on = false;
                #if defined(FOG_LINEAR_KEYWORD_DECLARED)
                if (FOG_LINEAR) on = true;
                #endif
                #if defined(FOG_EXP_KEYWORD_DECLARED)
                if (FOG_EXP) on = true;
                #endif
                #if defined(FOG_EXP2_KEYWORD_DECLARED)
                if (FOG_EXP2) on = true;
                #endif
                return on;
            }

            // ---------------------------------------------------------------- 云（HDRI）
            float3 Pano(float2 uv, float2 dx, float2 dy)
            {
                return SAMPLE_TEXTURE2D_GRAD(_Pano, sampler_Pano, uv, dx, dy).rgb;
            }

            float3 Clouds(float3 d, float az, float el)
            {
                float u = az * (0.5 / PI) + _PanoOffset;
                float2 uv = float2(frac(u), 0.5 + el / PI);
                // 经度在 0/1 接缝处跳变：梯度取连续的那一份，免得 mip 选到最小一级出一条竖线
                float2 uvc = float2(frac(u + 0.5) - 0.5, uv.y);
                float2 dx = ddx(uv), dy = ddy(uv), dxc = ddx(uvc), dyc = ddy(uvc);
                dx.x = abs(dx.x) < abs(dxc.x) ? dx.x : dxc.x;
                dy.x = abs(dy.x) < abs(dyc.x) ? dy.x : dyc.x;
                float3 c = Pano(uv, dx, dy);
                // HDRI 自带的月亮：用旁边一块云盖掉（往东错开 _PanoMoonFix.z 度）
                float a = AngleDeg(d, normalize(_PanoMoonDir.xyz));
                float k = 1.0 - smoothstep(_PanoMoonFix.x, _PanoMoonFix.x + _PanoMoonFix.y, a);
                if (k > 0.0)
                {
                    float du = _PanoMoonFix.z / 360.0 / max(cos(el), 0.2);
                    c = lerp(c, Pano(uv + float2(du, 0.0), dx, dy), k);
                }
                c *= _Exposure;
                float3 tint = _SkyTint.rgb / max(dot(_SkyTint.rgb, float3(0.2126, 0.7152, 0.0722)), 1e-4);
                float lum = dot(c, float3(0.2126, 0.7152, 0.0722));
                return lerp(c, lum * tint, _Desat);
            }

            // ---------------------------------------------------------------- 远山
            float TreeLine(float x, float spacingDeg, float height)
            {
                float n = round(360.0 / spacingDeg);
                float t = x * n;
                float i1 = floor(t), f1 = t - i1;
                float i2 = floor(t + 0.5), f2 = t + 0.5 - i2;
                float h1 = (0.35 + 0.65 * Hash11(fmod(i1, n) + 7.0)) * saturate(1.0 - abs(f1 - 0.5) * 2.0);
                float h2 = (0.25 + 0.6 * Hash11(fmod(i2, n) + 913.0)) * saturate(1.0 - abs(f2 - 0.5) * 2.0);
                float dense = smoothstep(0.25, 0.6, PNoise(x, 37.0, 5.0));
                return height * max(h1, h2) * dense;
            }

            // e：仰角（度）；top：这一层山脊的高度（度）。返回覆盖率（边缘按像素抗锯齿）
            float Cover(float e, float top)
            {
                float s = top - e;
                return saturate(s / max(fwidth(s), 1e-4) + 0.5);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float az = atan2(d.x, d.z);              // 0 = 北（+z），+90° = 东（+x）
                float el = asin(clamp(d.y, -1.0, 1.0));
                float e = degrees(el);
                float x = frac(az * (0.5 / PI));         // 方位角，一圈 0..1（0 北，0.25 东，0.5 南）

                // 云
                float3 col = Clouds(d, az, el);

                // 月亮 + 光晕 + 飘过的薄云
                float3 m = normalize(_MoonDir.xyz);
                float am = AngleDeg(d, m);
                float3 rt = normalize(cross(float3(0, 1, 0), m));
                float3 upm = cross(m, rt);
                float2 q = float2(dot(d, rt), dot(d, upm)) / radians(_MoonRadius);
                float disc = saturate((_MoonRadius - am) / max(fwidth(am), 1e-4) + 0.5);
                float maria = 1.0 - 0.3 * smoothstep(0.48, 0.72, Fbm2(q * 1.6 + 3.1));
                float limb = 1.0 - 0.25 * saturate(dot(q, q));
                float2 drift = _Time.y * _VeilSpeed.xy;
                float2 vp = d.xz / max(d.y + 0.12, 0.05) * _VeilScale + drift;
                float2 vm = m.xz / max(m.y + 0.12, 0.05) * _VeilScale + drift;
                float cut = 1.0 - _VeilAmount;
                float veil = saturate((Fbm2(vp) - cut) * 2.5) * smoothstep(0.02, 0.3, d.y);
                float veilM = saturate((Fbm2(vm) - cut) * 2.5);
                float halo = _HaloIntensity * (exp(-am / _HaloWidth) + 0.3 * exp(-am / (_HaloWidth * 5.0))) * (0.75 + 0.7 * veilM);
                col = col * (1.0 - 0.3 * veil) + _VeilColor.rgb * veil * (0.006 + 0.22 * exp(-am / 8.0));
                col += _HaloColor.rgb * halo;
                col += _MoonColor.rgb * (_MoonIntensity * disc * maria * limb * (1.0 - 0.6 * veilM));

                // 远山：三层剖面（度）。南北两面山高、东西顺着山谷低，看得见远处的山
                float ns = abs(cos(az));
                float hFar = 1.6 + 1.2 * (1.0 - ns) + 3.6 * Ridge(x, 11.0, 2.0, 0.55);
                float hMid = 1.2 + 1.8 * ns + 3.4 * Ridge(x, 47.0, 3.0, 0.55);
                float hNear = _Ridges.x + 2.6 * ns + _Ridges.y * (0.35 + 0.65 * ns) * Ridge(x, 83.0, 4.0, 0.5)
                              + TreeLine(x, _Ridges.w, _Ridges.z);
                float cF = Cover(e, hFar), cM = Cover(e, hMid), cN = Cover(e, hNear);

                if (cF + cM + cN > 0.0)
                {
                    float3 far, mid, near;
                    // 越往下谷雾越浓（每层自己的山脚 = 比它前面那层的顶低一点的地方，这里简化成按仰角）
                    float mist = exp(-max(e + 0.5, 0.0) / _MistAmount.w);
                    if (FogOn())
                    {
                        far = lerp(_RidgeFar.rgb, _Mist.rgb, _MistAmount.z * mist);
                        mid = lerp(_RidgeMid.rgb, _Mist.rgb, _MistAmount.y * mist);
                        near = lerp(_RidgeNear.rgb, _Mist.rgb, _MistAmount.x * mist);
                    }
                    else
                    {
                        // 没有雾：按环境光 + 月光打亮（和近处模型同一套光），越远越被大气冲淡
                        float3 amb = _GlossyEnvironmentColor.rgb;
                        float3 light = amb + _MainLightColor.rgb * 0.35;
                        float3 air = _Mist.rgb + amb * 0.06;
                        float forest = 0.7 + 0.6 * Fbm2(float2(x * 2400.0, e * 9.0));
                        near = lerp(_LandAlbedo.x * forest * light, air, _LandAlbedo.w * 0.35 * (0.5 + mist));
                        mid = lerp(_LandAlbedo.y * light, air, _LandAlbedo.w * 0.65);
                        far = lerp(_LandAlbedo.z * light, air, _LandAlbedo.w * 0.85);
                    }
                    col = lerp(col, far, cF);
                    col = lerp(col, mid, cM);
                    col = lerp(col, near, cN);
                }
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
