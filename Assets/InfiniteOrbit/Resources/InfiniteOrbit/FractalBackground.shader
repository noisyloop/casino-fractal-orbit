// Animated Julia-set background with orbit-trap glow. Driven from FractalBackground.cs:
// _Intensity 0..1 (calm idle -> full celebration), _TimeOffset (speed-integrated time), _Iterations (quality).
Shader "InfiniteOrbit/FractalBackground"
{
    Properties
    {
        _Intensity ("Intensity", Range(0, 1)) = 0.2
        _Hue ("Hue", Range(0, 1)) = 0.6
        _Iterations ("Iterations", Range(8, 96)) = 48
        _Aspect ("Aspect", Float) = 1.7778
        _TimeOffset ("Time Offset", Float) = 0
        _Tint ("Tint", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Opaque" }
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            float _Intensity;
            float _Hue;
            float _Iterations;
            float _Aspect;
            float _TimeOffset;
            float4 _Tint;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                return o;
            }

            float3 Palette(float t)
            {
                return 0.5 + 0.5 * cos(6.28318 * (t + float3(0.0, 0.33, 0.67) + _Hue));
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _TimeOffset;
                float2 p = (i.uv - 0.5) * float2(_Aspect, 1.0) * 2.6;

                float zoom = 1.0 - 0.18 * _Intensity * sin(t * 0.37);
                p *= zoom;
                float a = t * 0.04;
                float ca = cos(a);
                float sa = sin(a);
                p = float2(ca * p.x - sa * p.y, sa * p.x + ca * p.y);

                // The Julia constant orbits near the Mandelbrot boundary: "Infinite Orbit".
                float2 c = float2(-0.745, 0.186) + (0.02 + 0.05 * _Intensity) * float2(cos(t * 0.21), sin(t * 0.17));

                float2 z = p;
                float trap = 1000.0;
                float n = 0.0;
                float r2 = 0.0;
                [loop]
                for (int k = 0; k < 96; k++)
                {
                    if (k >= (int)_Iterations) break;
                    z = float2(z.x * z.x - z.y * z.y, 2.0 * z.x * z.y) + c;
                    r2 = dot(z, z);
                    trap = min(trap, abs(sqrt(r2) - 0.6));
                    if (r2 > 16.0) break;
                    n += 1.0;
                }

                float smoothN = n - log2(max(0.0001, log2(max(r2, 1.0001))));
                float f = saturate(smoothN / _Iterations);
                float3 col = Palette(f * 1.6 + t * 0.015 * (1.0 + 3.0 * _Intensity));
                float glow = exp(-trap * (14.0 - 9.0 * _Intensity));
                col = col * (0.12 + 0.55 * f) + glow * Palette(0.55 + t * 0.03) * (0.25 + 0.9 * _Intensity);

                float2 d = i.uv - 0.5;
                float vignette = 1.0 - 1.1 * dot(d, d);
                col *= (0.35 + 0.75 * _Intensity) * vignette;
                return fixed4(col * _Tint.rgb, 1.0);
            }
            ENDCG
        }
    }
    FallBack Off
}
