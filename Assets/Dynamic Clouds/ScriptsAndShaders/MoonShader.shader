// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'
// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Procedural Clouds/Moon" {
Properties {
     _Color ("Color Tint", Color) = (1,1,1,1)  
    _MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
}
 
SubShader {
    Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
    LOD 100
   
    ZWrite Off
    Blend SrcAlpha OneMinusSrcAlpha
   
    Pass {
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
           
            #include "UnityCG.cginc"
           
            struct appdata_t {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
            };
 
            struct v2f {
                float4 vertex : SV_POSITION;
                half2 texcoord : TEXCOORD0;
            };
 
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
           
            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }
           
            fixed4 frag (v2f i) : SV_Target
            {
            half3 frontVec = (0,0,1);                                   //Set front vector
			half3 rotVec =normalize( mul(unity_ObjectToWorld, fixed4(0,1,0,1)).xyz);//- _WorldSpaceCameraPos;                            //Calculate current rotation vector
     
   
			half grad = acos(dot(normalize(rotVec.xz), frontVec.xz));           //Calculate rotational angle
			grad = degrees(grad);  
                fixed4 col = tex2D(_MainTex, i.texcoord);
                col.a = col.a*pow(rotVec.y-.15,.5);
                col.rgb*=1+(1-rotVec.y);
                return clamp(col*_Color,0,1);
            }
        ENDCG
    }
}
 
}