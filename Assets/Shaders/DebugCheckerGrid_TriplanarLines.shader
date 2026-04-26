Shader "Custom/DebugCheckerGrid_TriplanarLines"
{
    Properties
    {
        _ColorA ("Checker Color A", Color) = (1,1,1,1)
        _ColorB ("Checker Color B", Color) = (0,0,0,1)

        _LineColor5m ("5m Line Color", Color) = (1,1,0,1)
        _LineColor10m ("10m Line Color", Color) = (1,0,0,1)

        _CellSize ("Cell Size (meters)", Float) = 1.0

        _LineWidth5m ("5m Line Width", Range(0.001, 0.3)) = 0.04
        _LineWidth10m ("10m Line Width", Range(0.001, 0.4)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Geometry"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _ColorA;
                float4 _ColorB;
                float4 _LineColor5m;
                float4 _LineColor10m;
                float _CellSize;
                float _LineWidth5m;
                float _LineWidth10m;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.positionHCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            float CheckerValue(float2 uv, float size)
            {
                int ix = (int)floor(uv.x / size);
                int iy = (int)floor(uv.y / size);
                return ((ix + iy) & 1) ? 1.0 : 0.0;
            }

            float LineMask(float coord, float interval, float width)
            {
                float halfW = width * 0.5;
                float m = fmod(coord, interval);
                if (m < 0.0) m += interval;

                float d = min(m, interval - m);
                return step(d, halfW);
            }

            void BuildPlaneResult(
                float2 uv,
                float checkerSize,
                float lineWidth5,
                float lineWidth10,
                out float checker,
                out float line5,
                out float line10)
            {
                checker = CheckerValue(uv, checkerSize);

                float lx5 = LineMask(uv.x, 5.0, lineWidth5);
                float ly5 = LineMask(uv.y, 5.0, lineWidth5);
                line5 = saturate(max(lx5, ly5));

                float lx10 = LineMask(uv.x, 10.0, lineWidth10);
                float ly10 = LineMask(uv.y, 10.0, lineWidth10);
                line10 = saturate(max(lx10, ly10));
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 w = abs(n);
                w /= (w.x + w.y + w.z + 1e-5);

                float cellSize = max(_CellSize, 0.0001);

                float checkerXZ, line5XZ, line10XZ;
                float checkerXY, line5XY, line10XY;
                float checkerYZ, line5YZ, line10YZ;

                BuildPlaneResult(i.positionWS.xz, cellSize, _LineWidth5m, _LineWidth10m, checkerXZ, line5XZ, line10XZ);
                BuildPlaneResult(i.positionWS.xy, cellSize, _LineWidth5m, _LineWidth10m, checkerXY, line5XY, line10XY);
                BuildPlaneResult(i.positionWS.zy, cellSize, _LineWidth5m, _LineWidth10m, checkerYZ, line5YZ, line10YZ);

                float checker = checkerXZ * w.y + checkerXY * w.z + checkerYZ * w.x;
                float line5 = line5XZ * w.y + line5XY * w.z + line5YZ * w.x;
                float line10 = line10XZ * w.y + line10XY * w.z + line10YZ * w.x;

                float4 col = (checker > 0.5) ? _ColorB : _ColorA;

                col.rgb = lerp(col.rgb, _LineColor5m.rgb, saturate(line5));

                col.rgb = lerp(col.rgb, _LineColor10m.rgb, saturate(line10));

                return half4(col.rgb, 1.0);
            }
            ENDHLSL
        }
    }
}