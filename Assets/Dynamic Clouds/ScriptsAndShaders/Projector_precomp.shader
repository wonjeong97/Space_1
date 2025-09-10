// Upgrade NOTE: replaced '_Projector' with 'unity_Projector'
// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Projector_precomp" {
    Properties {
        
        _NoiseTex ("Color (RGB) Alpha (A)", 2D) = "white"{}
          _Distance ("Distance", Float) = 0.0
        _Transparency("Transparency",Range (0,1.0)) = 0.5
        _Color ("Color", Color) = (1,1,1,1)
        _ShadowTex ("Projected Image", 2D) = "white" {}

		 _Density ("Density", Range(0, 2)) = .5
	
		 _Cutout("Cutout", Range(0,8)) = 0
		
		  _Tiling ("Tiling", Float) = 1
		  _WindSpeed_X("Wind speed X", Float) = 1
		  _WindSpeed_Y("Wind speed Y", Float) = 1
		  _CloudAnimation("Clouds Animation", FLoat) = 1
		  _Rnd("Randomizer", Vector) = (0,0,0,0)
		   _Detalization ("Detalization",Range(0.1,1)) = 1
    }
    SubShader {
        Pass {  
        Tags {"Queue"="Transparent"}
         ZWrite Off
			Fog { Color (1, 1, 1) }
			AlphaTest Greater 0
			ColorMask RGB
			Blend DstColor Zero
			Offset -1, -1
            CGPROGRAM
 
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
       #pragma target 3.0
          
            fixed _Distance;
           // fixed _Transparency;
            fixed4 _Color;
            uniform sampler2D _ShadowTex;
 			float _Transparency;
			float _Density;
			float _Tiling;
			float4 _Rnd;
			float _WindSpeed_X;
			float _WindSpeed_Y;
			float _CloudAnimation;
			float _Cutout;
				 uniform float4 _MainTex_TexelSize;
				 sampler2D _NoiseTex;
			float4 _NoiseTex_ST;
			float _Detalization;
			//
// GLSL textureless classic 4D noise "cnoise",
// with an RSL-style periodic variant "pnoise".
// Author:  Stefan Gustavson (stefan.gustavson@liu.se)
// Version: 2011-08-22
//
// Many thanks to Ian McEwan of Ashima Arts for the
// ideas for permutation and gradient selection.
//
// Copyright (c) 2011 Stefan Gustavson. All rights reserved.
// Distributed under the MIT license. See LICENSE file.
// https://github.com/stegu/webgl-noise
//

float f_noise(float2 uv, float p, float details){
  float4 timed =  float4(uv+_Rnd.xy+float2(_Time[0]*_WindSpeed_X,_Time[0]*_WindSpeed_Y)*_Tiling,0.0,_Time[0]*_CloudAnimation)/8;
				//float res = turbulence(timed) + turbulence(timed*2.0)/2.0 + turbulence(timed*4.0)/4.0 + turbulence(timed*8.0)/8.0;
				float res =-1.1+(_Density)*p;// cnoise(timed) + cnoise(timed*2.0)/2.0 + cnoise(timed*4.0)/4.0 + cnoise(timed*8.0)/8.0+ cnoise(timed*16.0)/16.0+ cnoise(timed*32.0)/32.0;
				for (float f = 0.0 ; f <= details*_Detalization ; f++ ){
       				float power = pow( 2, f );
        			res +=   abs(tex2Dlod(_NoiseTex, ( float4( power * timed ) )).r/power)/2;
    			}	
    			res = min(1,res);
    			res = max(0,res);
    			//res = step(_Cutout,res)*res;
    			res = pow(res, _Cutout*2+1);

    		//	if (res>1) res=1;
    		//	if (res<0) res=0;
    		//	if (res<_Cutout) res = 0;
    			return abs(res);
}
            uniform fixed4x4 unity_Projector; // transformation matrix
 
            struct vertexInput {
                fixed4 vertex : POSITION;
            };
       
            struct vertexOutput {
                fixed4 pos : SV_POSITION;
                fixed4 posProj : TEXCOORD0;
                fixed fade:TEXCOORD1;
            };
 
            vertexOutput vert(vertexInput input) {
                vertexOutput output;
                output.posProj = mul(unity_Projector, input.vertex);
                output.pos = UnityObjectToClipPos(input.vertex);
                float fading = dot( float3(0,1,0),UnityWorldToObjectDir(_WorldSpaceLightPos0.xyz));
                output.fade= fading;
                return output;
            }
 
            fixed4 frag(vertexOutput input) : COLOR{
               // if (input.posProj.w > 0.0)  { // in front of projector?
              //      fixed2 anim;
               half offsetcl_x =  dot(float3(0,0,1), UnityWorldToObjectDir(_WorldSpaceLightPos0.xyz));
           		 half offsetcl_y =  dot(float3(1,0,0), UnityWorldToObjectDir(_WorldSpaceLightPos0.xyz));

           		float2 uv= clamp((fixed2(input.posProj.xy) / input.posProj.w) - fixed2(offsetcl_y/10, offsetcl_x/10),0,1) ;
           		//float rad = ((uv.x-0.5)*(uv.x-0.5)+(uv.y-0.5)*(uv.y-0.5))*16;
				//uv.x = (uv.x-.5)*2;
				//uv.y = (uv.y-.5)*2;
				//uv = (uv*(rad+1)/1.5+1)/2;
           		
            	float noise = f_noise(uv*_Tiling,1,10);

                    
                    return  _Color*lerp(fixed4(1,1,1,0),float4(1-noise,1-noise,1-noise,1),_Transparency*(1-abs(uv.x-.5)*2)*(1-abs(uv.y-.5)*2)*input.fade);
              //  }
               // else { // behind projector
                    //return fixed4(0.0,0.0,0.0,0.0);
                //}
            }
 
            ENDCG
        }
    }
   // Fallback "Projector/Multiply"
}