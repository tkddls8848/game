// 화면 전체에 거는 후처리. 내장 파이프라인이라 URP/PostProcessing 패키지 없이
// 이미지 이펙트로 직접 쓴다(§18-2: 손으로만 만들 수 있는 에셋을 만들지 않는다 —
// 셰이더는 텍스트이므로 안전하다).
//
// 거는 것 넷. Godot 2.5D에서 무게를 만든 것과 같은 항목이다.
//   * 채도 내림 — 색이 튀면 그래픽이 되고, 앉으면 사진이 된다
//   * 대비 올림 + 검정 들어 올림 — 순검정은 화면을 납작하게 만든다
//   * 비네팅 — 가장자리가 떨어지면 렌즈로 본 것처럼 읽힌다
//   * 정지한 필름 입자 — 매 프레임 흔들면 TV 노이즈가 되고, 조용한 저택에서는 산만하다
//
// UI는 Screen Space Overlay 캔버스라 이 효과를 받지 않는다. 의도한 것이다 —
// 세계는 가라앉고 종이 패널의 글자는 읽혀야 한다.
Shader "Hidden/Detective/Grade"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Saturation ("Saturation", Range(0,2)) = 0.72
        _Contrast ("Contrast", Range(0.5,2)) = 1.10
        _Lift ("Black lift", Range(0,0.2)) = 0.035
        _VignetteStrength ("Vignette strength", Range(0,1)) = 0.82
        _VignetteInner ("Vignette inner", Range(0,1)) = 0.30
        _VignetteOuter ("Vignette outer", Range(0,1.5)) = 0.86
        _Grain ("Grain", Range(0,0.15)) = 0.032
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            half _Saturation;
            half _Contrast;
            half _Lift;
            half _VignetteStrength;
            half _VignetteInner;
            half _VignetteOuter;
            half _Grain;

            // 입자용 해시. 화면 픽셀에 고정되므로 프레임이 지나도 같은 자리에 남는다.
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);

                // 채도
                half luma = dot(c.rgb, half3(0.2126, 0.7152, 0.0722));
                c.rgb = lerp(half3(luma, luma, luma), c.rgb, _Saturation);

                // 대비는 중간값을 축으로
                c.rgb = saturate((c.rgb - 0.5) * _Contrast + 0.5);

                // 검정을 들어 올린다
                c.rgb = c.rgb * (1.0 - _Lift) + _Lift;

                // 타원 비네팅. 가로가 넓으므로 세로를 눌러 화면 비율에 맞춘다.
                float2 d = i.uv - 0.5;
                float r = length(d * float2(1.0, 1.18));
                half v = smoothstep(_VignetteInner, _VignetteOuter, r) * _VignetteStrength;
                c.rgb *= (1.0 - v);

                // 정지한 입자
                half g = (hash(floor(i.uv * _ScreenParams.xy * 0.75)) - 0.5) * _Grain;
                c.rgb = saturate(c.rgb + g);

                return c;
            }
            ENDCG
        }
    }

    Fallback Off
}
