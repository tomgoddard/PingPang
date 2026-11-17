// Semi-transparent ghost skin for the coach.  Plain alpha blending shows
// every layer of a figure made of overlapping parts (limbs, joints, torso)
// through each other.  This draws in two passes: the first only fills the
// depth buffer, the second draws color only where a surface is the nearest
// one, so the ghost reads as one see-through shell.  Shading is a fixed
// soft light from above plus a lighter rim, so the shape shows without
// depending on the scene's lights.  Single pass instanced / multiview
// stereo is set up in both passes, for the Quest.
Shader "GhostCoach/GhostSurface" {
    Properties {
        _Color ("Color", Color) = (0.45, 0.85, 1.0, 0.4)
        _Rim ("Rim light", Range(0, 1)) = 0.5
    }
    SubShader {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass {
            Name "DEPTH"
            ZWrite On
            ColorMask 0
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f {
                float4 pos : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            v2f vert(appdata v) {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }
            fixed4 frag(v2f i) : SV_Target {
                return fixed4(0, 0, 0, 0);
            }
            ENDCG
        }

        Pass {
            Name "COLOR"
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _Color;
            half _Rim;

            struct appdata {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f {
                float4 pos : SV_POSITION;
                half3 normal : TEXCOORD0;
                half3 view : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            v2f vert(appdata v) {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.view = normalize(WorldSpaceViewDir(v.vertex));
                return o;
            }
            fixed4 frag(v2f i) : SV_Target {
                half3 n = normalize(i.normal);
                half light = 0.6 + 0.4 * saturate(dot(n, normalize(half3(0.3, 0.8, 0.5))));
                half rim = pow(1.0 - saturate(dot(n, normalize(i.view))), 2.0) * _Rim;
                fixed4 c;
                c.rgb = _Color.rgb * light + rim * 0.5;
                c.a = saturate(_Color.a + rim * 0.35);
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
