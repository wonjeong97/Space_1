// URP port of "Procedural Clouds/PC_precompiled"
// Notes:
// - Built-in의 UnityCG/UnityLightingCommon 의존성 제거
// - _WorldSpaceLightPos0, _LightColor0, ShadeSH9 대체
//   -> GetMainLight(), SampleSH 사용
// - tex2D/tex2Dlod -> SAMPLE_TEXTURE2D / SAMPLE_TEXTURE2D_LOD
// - UnityObjectToClipPos -> TransformObjectToHClip
// - Fog: URP MixFog 적용

Shader "Procedural Clouds/PC_precompiled_URP"
{
    Properties
    {
        _MainTex        ("Color (RGB) Alpha (A)", 2D) = "white" {}
        _NoiseTex       ("Color (RGB) Alpha (A)", 2D) = "white" {}
        _NoiseTexPR     ("Color (RGB) Alpha (A)", 2D) = "white" {}

        _Color          ("Clouds Color", Color) = (1,1,1,1)
        _SColor         ("Sunset Color", Color) = (1,1,1,1)
        _Exposure       ("Exposure", Range(0,3)) = 1
        _Density        ("Density", Range(0,2)) = 0.5
        _Height         ("Height", Range(0.1,1)) = 1
        _Cutout         ("Cutout", Range(0.1,8)) = 0

        _Transparency   ("_Opacity", Range(0,1)) = 1
        _Translucency   ("_Translucency", Range(0.1,1)) = 0.75
        _TextureBlend   ("Texture Blending", Range(0,1)) = 1
        _LightK         ("Lighting coefficient", Range(0,1)) = 0.75
        _Tiling         ("Tiling", Range(1,32)) = 1
        _TextureTiling  ("Extra Texture Tiling", Float) = 1
        _WindSpeed_X    ("Wind speed X", Float) = 1
        _WindSpeed_Y    ("Wind speed Y", Float) = 1
        _CloudAnimation ("Clouds Animation", Float) = 1
        _Contrast       ("Contrast", Float) = 1
        _AddNoise       ("Additional Noise [0 or 1]", Float) = 1
        _Scale          ("Height Scaling", Float) = 1
        _Rnd            ("Randomizer", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalRenderPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        LOD 100
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha
        // ZWrite Off   // 구름을 반투명 오브젝트 위로 그리되 ZWrite 필요하면 주석 해제

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            // URP 멀티 컴파일(그림자/라이트/포그 등)
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog

            // URP 핵심 include
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

            // 텍스처 선언 (URP/HLSL 스타일)
            TEXTURE2D(_MainTex);      SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex);     SAMPLER(sampler_NoiseTex);
            TEXTURE2D(_NoiseTexPR);   SAMPLER(sampler_NoiseTexPR);

            float4 _MainTex_TexelSize;
            float4 _NoiseTex_TexelSize;
            float4 _NoiseTexPR_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float  _Transparency;
                float  _Density;
                float  _Tiling;
                float4 _Color;
                float  _Exposure;
                float  _WindSpeed_X;
                float  _WindSpeed_Y;
                float  _CloudAnimation;
                float  _Translucency;
                float  _Cutout;
                float  _LightK;
                float  _Height;
                float4 _SColor;
                float  _TextureBlend;
                float  _TextureTiling;
                float  _NoiseAdd;     // 원본에 존재했으나 실사용은 안함
                float4 _Rnd;
                float  _Contrast;
                float  _AddNoise;
                float  _Scale;

                float4 _MainTex_ST;
                float4 _NoiseTex_ST;
                float4 _NoiseTexPR_ST;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float2 uv2        : TEXCOORD1;
                float2 uv3        : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float2 uv2         : TEXCOORD1;
                float2 uv3         : TEXCOORD2;
                float3 diff        : TEXCOORD3; // 조명 결과(RGB)
                half   fogFactor   : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ===== 유틸 =====
            // 노이즈 샘플링 함수 (원본 f_noise를 URP 텍스처 샘플로 포팅)
            float f_noise(float2 uv, float2 uv2, float p, float details, int a)
            {
                // 시간/바람에 따른 UV 이동
                float2 t = float2(_Time.y * _WindSpeed_X, _Time.y * _WindSpeed_Y);
                float4 timed = float4(uv + _Rnd.xy + t * _Tiling, 0.0, _Time.y) / 8.0;

                float res = -1.1 + (_Density) * p;
                // 프랙탈 노이즈 루프 (간단화)
                [unroll]
                for (int k = 0; k <= 1; ++k)
                {
                    float power = pow(2.0, (float)k);
                    float4 sampleUV = float4(timed.xy * power, timed.zw);
                    float n = SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_NoiseTex, sampleUV.xy, sampleUV.w).r;
                    res += abs(n / power) * 0.5;
                }

                res = saturate(res);
                res = pow(res, _Cutout * 4.0 + 1.0);
                res = saturate(res);

                float4 timed1 = float4(uv + _Rnd.xy + t * 0.75 * pow(_Tiling, 0.75), 0.0, 0.0) / 2.0;
                float npr = SAMPLE_TEXTURE2D_LOD(_NoiseTexPR, sampler_NoiseTexPR, timed1.xy, 0).r;
                res = (res + npr * pow(res, 0.5) * (1 - a) * _AddNoise) / (2.5 - _Density);

                return saturate(res);
            }

            // 높이맵 기반 노멀 계산(원본 로직 유지)
            float3 ComputeNormals(float h_A, float h_B, float h_C, float h_D, float h_N, float heightScale)
            {
                float3 va = float3(0,  1, (h_A - h_N) * heightScale);
                float3 vb = float3(1,  0, (h_B - h_N) * heightScale);
                float3 vc = float3(0, -1, (h_C - h_N) * heightScale);
                float3 vd = float3(-1, 0, (h_D - h_N) * heightScale);
                float3 av_n = (cross(va, vb) + cross(vb, vc) + cross(vc, vd) + cross(vd, va)) / -4.0;
                return normalize(av_n);
            }

            Varyings Vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float4 posOS = v.positionOS;

                // 원본의 반경형 왜곡 UV 계산
                float2 uv = TRANSFORM_TEX(v.uv, _MainTex);
                float2 uvRad = uv;
                float rad = ((uvRad.x - 0.5) * (uvRad.x - 0.5) + (uvRad.y - 0.5) * (uvRad.y - 0.5)) * 16.0;
                uvRad = (uvRad - 0.5) * 2.0;
                uvRad = (uvRad * (rad + 1.0) / 1.5 + 1.0) * 0.5;

                // 버텍스 디스플레이스용 노이즈
                float noise = f_noise(uvRad * _Tiling, v.uv * _Tiling * 0.5, 1, 6 / pow(_Tiling, 0.75), 1);

                // Y 변위 (원본 로직)
                posOS.y -= 400.0 * noise * pow(_Height, 2.0) * _Scale / pow(_Tiling, 1.0);

                // 노멀 샘플링용 보폭
                float normal_cell_size = 16.0;
                float2 du = float2(_MainTex_TexelSize.x * normal_cell_size, 0);
                float2 dv = float2(0, _MainTex_TexelSize.x * normal_cell_size);

                float hC = 1.0 * noise;
                float hR = 1.0 * f_noise((uvRad + du) * _Tiling, v.uv, 1, 6 / pow(_Tiling, 0.75), 1);
                float hU = 1.0 * f_noise((uvRad + dv) * _Tiling, v.uv, 1, 6 / pow(_Tiling, 0.75), 1);
                float hD = 1.0 * f_noise((uvRad - dv) * _Tiling, v.uv, 1, 6 / pow(_Tiling, 0.75), 1);
                float hL = 1.0 * f_noise((uvRad - du) * _Tiling, v.uv, 1, 6 / pow(_Tiling, 0.75), 1);

                // 메인 라이트 정보 (URP)
                Light mainLight = GetMainLight();

                // 라이트 방향/강도와 높이스케일 영향
                float3 n3 = ComputeNormals(
                    hU, hR, hD, hL, hC,
                    pow(saturate(mainLight.direction.y + 0.2), 0.5) * pow(_Height / 1.25, 2.0) * 6.0 / pow(_Tiling, 0.5)
                );

                // 공간 변환
                float3 worldNormal = TransformObjectToWorldDir(n3);
                worldNormal = normalize(float3(worldNormal.x, -worldNormal.y, -worldNormal.z)); // 원본 보정

                float3 worldPos = TransformObjectToWorld(posOS.xyz);
                float3 viewDir  = normalize(worldPos - GetCameraPositionWS());

                // 라이트/노말 내적
                float nl = saturate(dot(worldNormal, -mainLight.direction));
                float nlTop = saturate(dot(float3(0,0,1), -mainLight.direction));

                // Sunset factor (원본 식 근사)
                float l_y = mainLight.direction.y;
                float l_y_sign = sign(l_y == 0 ? 1 : l_y);
                l_y = pow(abs(l_y), 0.25) * l_y_sign;

                float sunsetFactor = (_SColor.r * _SColor.r + _SColor.g * _SColor.g + _SColor.b * _SColor.b) / 3.0;
                float sunset = sunsetFactor * pow(1 - nlTop, 8) * saturate(lerp(0, 1, saturate(l_y * 1.15 + 1))) * min(_Exposure, 1) / max(_Density, 1e-3);

                // 기본 라이트 컬러(URP mainLight.color)
                float3 color_v = mainLight.color * pow(nlTop, 2) + (_SColor.rgb * _SColor.rgb) * (1 - nlTop) / pow(saturate(_Density - 0.85), 2);

                // 산란 항 근사(원본 direct_lighting 로직 근사)
                float thickness = noise * nl / max(_Translucency * _Translucency, 1e-3);
                float g = 0.975;
                float denom = pow(1 + g * g - 2 * g * dot(viewDir, mainLight.direction), 1.5);
                float phase = (1 - g * g) / max(denom, 1e-3) / 3.14;
                float direct_lighting = exp(-thickness);
                direct_lighting *= (1 + phase * sqrt(saturate(max(0, mainLight.color.b - 0.25))));
                direct_lighting *= (1.0 - exp(-2 * pow(thickness, 0.25) * nl));
                direct_lighting = saturate(direct_lighting);

                float3 diff = direct_lighting * color_v * _LightK * (1 + sunset);

                // SH 간접광(URP의 SampleSH)
                float3 sh = SampleSH(worldNormal);
                // 달 빛 근사 항(원본 g_moon)
                float g_moon = 0.9 + 0.125 * nlTop;
                float denomMoon = pow(1 + g_moon * g_moon - 2 * g_moon * dot(viewDir, -mainLight.direction), 1.5);
                float moonPhase = (1 - g_moon * g_moon) / max(denomMoon, 1e-3) / (3.14 * 132);

                diff += max(0, sh / max(_Density, 1e-3) + (_SColor.rgb * _SColor.rgb) / pow(1 + _Density, 2));
                diff += saturate(moonPhase * sqrt(saturate(max(0, mainLight.color.b - 0.05))));

                diff = saturate(diff);

                o.positionHCS = TransformObjectToHClip(posOS);
                o.uv  = v.uv;
                o.uv2 = v.uv2;
                o.uv3 = v.uv3;
                o.diff = diff;

                // URP Fog
                o.fogFactor = ComputeFogFactor(o.positionHCS.z);

                return o;
            }

            half4 Frag (Varyings i) : SV_Target
            {
                // 반경형 왜곡 UV 재계산(원본과 동일)
                float2 uv = i.uv;
                float2 uvRad = uv;
                float rad = ((uvRad.x - 0.5) * (uvRad.x - 0.5) + (uvRad.y - 0.5) * (uvRad.y - 0.5)) * 16.0;
                uvRad = (uvRad - 0.5) * 2.0;
                uvRad = (uvRad * (rad + 1.0) / 1.5 + 1.0) * 0.5;

                float noise = f_noise(uvRad * _Tiling, i.uv * _Tiling * 0.5, 1, 10, 0);

                // 두 장의 텍스처 블렌드(원본 로직)
                float2 t = float2(_Time.y * _WindSpeed_X * 0.96, _Time.y * _WindSpeed_Y * 0.96);
                float2 uvA = (uvRad + _Rnd.xy + t) * (_TextureTiling * _Tiling);
                float2 uvB = (uvRad + _Rnd.xy + t) * (_TextureTiling * _Tiling * 0.5);

                float4 tex1 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvA);
                float4 tex2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvB);
                float4 tex  = abs(lerp(tex1, tex2, 0.5));

                float4 col = (tex * 4 * _TextureBlend + noise) * _Exposure / (5 - (1 - _TextureBlend) * 4);
                col.a = 1;

                // 조명 곱
                col.rgb *= i.diff;

                // 알파
                float tr_coef = max(0, 1 - pow(rad, 1.75) / 12.0);
                col.a = max(noise * _Transparency * lerp(1, tr_coef, _Exposure), 0);

                // 대비 보정
                col.rgb = saturate((lerp(1, _Contrast, max(0, i.diff - 0.1)) * (col.rgb - 0.5) + 0.5));

                // Fog
                col.rgb = MixFog(col.rgb, i.fogFactor);

                // 최종 색상
                col *= _Color;
                return col;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
