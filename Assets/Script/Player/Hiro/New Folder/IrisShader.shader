Shader "UI/IrisShader"
{
    Properties
    {
        // UIのImageが要求するため _MainTex を用意(警告対策)。使わないので白でOK。
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Overlay Color", Color) = (0,0,0,1)
        // くり抜く円の半径(0=全部隠れる / 大きい=全部見える)。スクリプトで動かす。
        _Radius ("Radius", Range(0, 2)) = 1.5
        // 円の縁のぼかし幅。
        _Softness ("Softness", Range(0.0001, 0.5)) = 0.02
        // 画面アスペクト比補正(width/height)。スクリプトで設定。
        _Aspect ("Aspect", Float) = 1.7777
        // 円の中心(UV空間 0..1)。基本は0.5,0.5。
        _Center ("Center", Vector) = (0.5, 0.5, 0, 0)
    }

    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _Radius;
            float _Softness;
            float _Aspect;
            float4 _Center;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 中心からの距離をアスペクト補正して円形に
                float2 d = i.uv - _Center.xy;
                d.x *= _Aspect;
                float dist = length(d);

                // dist < Radius : 内側(透明=見える) / 外側(黒)
                // Softnessで縁をぼかす
                float a = smoothstep(_Radius - _Softness, _Radius + _Softness, dist);

                return fixed4(_Color.rgb, _Color.a * a);
            }
            ENDCG
        }
    }
}
