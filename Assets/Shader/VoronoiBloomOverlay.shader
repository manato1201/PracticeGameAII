Shader "FX/VoronoiBloomOverlay"
{
    Properties
    {
        // 全体のブレンド
        _Tint          ("Tint", Color) = (1,1,1,1)
        _Intensity     ("Intensity", Range(0,4)) = 1.5
        [MaterialToggle] _Additive ("Additive Blend (On=Add / Off=Alpha)", Float) = 1

        // 背景ブラー
        _Threshold     ("Bloom Threshold", Range(0,2)) = 1.0
        _BlurRadius    ("Blur Radius", Range(0,6)) = 2.0
        _BlurIter      ("Blur Iterations (1..4)", Range(1,4)) = 3

        // ボロノイ・丸ボケ
        _CellScale     ("Cell Scale (bokeh size)", Range(2,40)) = 12.0
        _Softness      ("Bokeh Softness", Range(0.001,0.5)) = 0.12
        _MoveSpeed     ("Move Speed", Range(0,4)) = 0.8
        _Jitter        ("Jitter Amount", Range(0,1)) = 0.35
        _Seed          ("Seed", Float) = 1.0
        _BokehColor    ("Bokeh Color", Color) = (1,1,1,1)

        [MaterialToggle] _AlphaBlend ("Use Alpha Blend (ON=Alpha / OFF=Add)", Float) = 1
        _BGColor        ("Background Color (alpha=背景の濃さ)", Color) = (0,0,0,0.75)
        _OverlayAlpha   ("Overlay Master Alpha", Range(0,1)) = 1
        _Chromatic      ("Chromatic Aberration", Range(0,1)) = 0.35

    }

    SubShader
    {
        Tags{ "Queue"="Transparent" "RenderType"="Transparent" }
        ZWrite Off
        Cull Off

        // 背景を掴む（Built-in 専用）
        GrabPass{ "_GrabTex" }

        // 合成パス
        Pass
        {
            // ブレンドはキーワードで切替（Add / Alpha）
            Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha
            // デフォは Add、Alphaブレンドに切替は pragma で

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile __ ALPHA_BLEND
            #include "UnityCG.cginc"

            sampler2D _GrabTex; float4 _GrabTex_TexelSize;

            fixed4 _Tint;        float _Intensity;

            // ← ここが不足していると今回のエラーになります
            float  _AlphaBlend;   // 1=Alpha, 0=Add
            fixed4 _BGColor;      // 背景色（A=濃さ）
            float  _OverlayAlpha; // 全体フェード
            float  _Chromatic;    // 色収差量 0..1

            float  _Threshold;
            float  _BlurRadius;   float _BlurIter;

            float  _CellScale, _Softness, _MoveSpeed, _Jitter, _Seed;
            fixed4 _BokehColor;

            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; float4 sp:TEXCOORD0; };

            v2f vert(appdata v){
                v2f o; o.pos = UnityObjectToClipPos(v.vertex);
                o.sp = ComputeScreenPos(o.pos);
                return o;
            }

            // 輝度
            inline float Luma(float3 c){ return dot(c, float3(0.2126,0.7152,0.0722)); }

            // しきい値付き 3x3 ボックスブラー（固定タップ）
            float3 BoxBlurTh(float2 uv, float radius)
            {
                float2 px = _GrabTex_TexelSize.xy * radius;
                float3 acc = 0; float w = 0;

                // 3x3 固定（アンロール安全）
                [unroll] for(int y=-1; y<=1; y++){
                    [unroll] for(int x=-1; x<=1; x++){
                        float2 o = float2(x,y) * px;
                        float3 c = tex2D(_GrabTex, uv + o).rgb;
                        float  m = saturate((Luma(c) - _Threshold) * 4.0);
                        float  ww = (x==0 && y==0)? 1.0 : 0.85; // 中心をやや重く
                        acc += c * m * ww;  w += m * ww;
                    }
                }
                return (w>0)? acc/w : 0;
            }

            // Kawase風の多段（最大4段）ブラー
            float3 MultiBlur(float2 uv, float radius, int iters)
            {
                float3 c = tex2D(_GrabTex, uv).rgb;
                float3 b = 0;

                // 1..4 回に固定（実行時早期終了）
                [unroll(4)]
                for(int k=0;k<4;k++){
                    if(k>=iters) break;
                    float r = radius * (1.0 + 0.6*k);
                    b += BoxBlurTh(uv, r);
                }
                return b / max(1, iters);
            }

            // hash/乱数
            float2 hash2(float2 p){
                // タイルごとに安定した擬似乱数
                float n = sin(dot(p, float2(41.3, 289.1)) + _Seed) * 43758.5453;
                return frac(float2(n, n*1.2154));
            }

            // シームレスに動く Voronoi（最近点距離 d と、その位置 uvMin を返す）
            void Voronoi(float2 uv, float cellScale, out float d, out float2 uvMin)
            {
                // タイル空間
                float2 g = uv * cellScale;     // グリッド座標
                float2 i = floor(g);
                float2 f = frac(g);
                d = 1e5; uvMin = 0;

                // 近傍 3x3 で最小を探索（固定アンロール）
                [unroll] for(int y=-1;y<=1;y++){
                    [unroll] for(int x=-1;x<=1;x++){
                        float2 o = float2(x,y);
                        float2 cell = i + o;

                        // セル内の種点（乱数 + ジッタ + 時間移動）
                        float2 rnd = hash2(cell) - 0.5;
                        float2 p   = rnd;

                        // 時間でシームレスに流す：rnd方向に移動し、fractでループ
                        float2 v = normalize(rnd + 1e-4);
                        p += _MoveSpeed * _Time.y * v;
                        p = frac(p);      // タイル境界で継ぎ目なし
                        p = (p - 0.5) * _Jitter * 2.0; // ジッタ量

                        float2 r = o + p - f;
                        float  di = dot(r,r);
                        if(di < d){ d = di; uvMin = r; }
                    }
                }
                d = sqrt(d); // 距離
            }

            fixed4 frag(v2f i):SV_Target
            {
                float2 uv = i.sp.xy / i.sp.w;

                // 1) 背景ブルーム（MultiBlur）
                int blurIterN = (int)clamp(floor(_BlurIter + 0.5), 1.0, 4.0);
                float3 blur   = MultiBlur(uv, _BlurRadius, blurIterN);

                // 2) ボロノイ・丸ボケ
                float d; float2 uvMin;
                Voronoi(uv, _CellScale, d, uvMin);

                // 基準半径と縁の柔らかさ
                float R = 0.5 / _CellScale;

                // 色収差：RGB でわずかに半径をズラす
                float rR = R * (1.0 + 0.5 * _Chromatic);
                float rG = R;
                float rB = R * (1.0 - 0.5 * _Chromatic);

                float mR = 1.0 - smoothstep(rR - _Softness, rR + _Softness, d);
                float mG = 1.0 - smoothstep(rG - _Softness, rG + _Softness, d);
                float mB = 1.0 - smoothstep(rB - _Softness, rB + _Softness, d);

                // 少しだけハイライト寄りに
                mR = pow(saturate(mR), 1.4);
                mG = pow(saturate(mG), 1.4);
                mB = pow(saturate(mB), 1.4);

                // RGB を別々に合成（白ベース+色収差）
                float3 bokeh = float3(mR, mG, mB) * _BokehColor.rgb;

                // 背景ブルームは今回は控えめに（既存の blur を必要なら足す）
                //int blurIterN = (int)clamp(floor(_BlurIter + 0.5), 1.0, 4.0);
                //float3 blur   = MultiBlur(uv, _BlurRadius, blurIterN);

                // ---- 合成 ----
                float3 addLight = (blur + bokeh) * _Intensity * _Tint.rgb;     // 加算する光
                float  bgA      = saturate(_BGColor.a * _Intensity);           // 黒の濃さ = Intensity連動
                float3 bgRGB    = _BGColor.rgb * bgA;                          // 黒背景色（普通は 0,0,0）

                float3 outRGB = bgRGB + addLight;   // 出力色
                float  outA   = bgA;                // 出力Alpha（背景の濃さだけ）

                return fixed4(outRGB, outA);
            }
            ENDCG
        }
    }
    FallBack Off
}