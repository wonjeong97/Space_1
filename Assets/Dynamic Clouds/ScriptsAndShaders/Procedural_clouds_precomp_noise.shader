// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'
// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Procedural Clouds/PC_precompiled"
{
	Properties
	{
		 _MainTex ("Color (RGB) Alpha (A)", 2D) = "white"{}
		 _NoiseTex ("Color (RGB) Alpha (A)", 2D) = "white"{}
		 _NoiseTexPR ("Color (RGB) Alpha (A)", 2D) = "white"{}
		
		 _Color("Clouds Color",Color) = (1,1,1,1)
		 _SColor("Sunset Color",Color) = (1,1,1,1)
		 _Exposure("Exposure",Range(0, 3)) = 1
		 _Density ("Density", Range(0, 2)) = .5
		_Height("Height", Range(0.1,1))=1
		 _Cutout("Cutout", Range(0.1,8)) = 0
		 	
		 _Transparency("_Opacity", Range(0, 1)) = 1
		 _Translucency("_Translucency", Range(0.1, 1)) = .75
		 _TextureBlend("Texture Blending", Range(0,1)) = 1
		 _LightK("Lighting coefficient", Range(0,1))=.75
		  _Tiling ("Tiling", Range(1,32)) = 1
		  _TextureTiling ("Extra Texture Tiling", Float) = 1
		  _WindSpeed_X("Wind speed X", Float) = 1
		  _WindSpeed_Y("Wind speed Y", Float) = 1
		  _CloudAnimation("Clouds Animation", Float) = 1
		  _Contrast("Contrast",Float) = 1
		  _AddNoise("Additional Noise [0 or 1]",Float) = 1
		  _Scale("Height Scaling",Float) = 1
		  _Rnd("Randomizer", Vector) = (0,0,0,0)
		  // _Detalization ("Detalization",Range(0.1,1)) = 1
	}
	SubShader
	{
		Tags { "Queue"="Transparent" "RenderType"="Transparent" "LightMode"="ForwardBase"}
		LOD 100
		Cull off
		  // ZWrite Off
     Blend SrcAlpha OneMinusSrcAlpha
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			// make fog work
			#pragma multi_compile_fog
			#pragma target 3.0
			#include "UnityCG.cginc"
			     #include "UnityLightingCommon.cginc" // for _LightColor0
			float _Transparency;
			float _Density;
			float _Tiling;
			float4 _Color;
			float _Exposure;
			float _WindSpeed_X;
			float _WindSpeed_Y;
			float _CloudAnimation;
			float _Translucency;
			float _Cutout;
			float _LightK;
			float _Height;
			float4 _SColor;
			float _TextureBlend;
			float _TextureTiling;
			float _NoiseAdd;
			float4 _Rnd;
			float _Contrast;
			float _AddNoise;
			float _Scale;
				 uniform float4 _MainTex_TexelSize;
				 uniform float4 _NoiseTex_TexelSize;
				uniform float4 _NoiseTexPR_TexelSize;
			sampler2D _MainTex;

			float4 _MainTex_ST;
			sampler2D _NoiseTex;
			float4 _NoiseTex_ST;
			sampler2D _NoiseTexPR;
			float4 _NoiseTexPR_ST;
			//float _Detalization;

float f_noise(float2 uv,float2 uv2,float p, float details, int a){
  float4 timed =  float4(uv+_Rnd.xy+float2(_Time[0]*_WindSpeed_X,_Time[0]*_WindSpeed_Y)*_Tiling,0.0,_Time[0])/8;
				
				float res =-1.1+(_Density)*p ;
				float resLR =0;

				for (float f = 0.0 ; f <= 1; f++ ){
       				float power = pow( 2, f );
        			res +=   abs(tex2Dlod(_NoiseTex, ( float4( power * timed ) )).r/power)/2;
    			}	

    			res = min(1,res);
    			res = max(0,res);
    	
    		
    			res = pow(res, _Cutout*4+1);
				res = clamp(res,0,1);
    				float t = (res+1);

    				  float4 timed1 =  float4(uv+_Rnd.xy+float2(_Time[0]*_WindSpeed_X,_Time[0]*_WindSpeed_Y)*.75*pow(_Tiling,.75),0.0,0)/2;
			
    			res =(res+tex2Dlod(_NoiseTexPR, timed1).r*pow(res,.5)*(1-a)*_AddNoise)/(2.5-_Density);

    			
				res = clamp(res,0,1);

		

    			return abs(res);
}


  float3 computeNormals( float h_A, float h_B, float h_C, float h_D, float h_N, float heightScale )
{
    
    float3 va = { 0, 1, (h_A - h_N)*heightScale };
    float3 vb = { 1, 0, (h_B - h_N)*heightScale };
    float3 vc = { 0, -1, (h_C - h_N)*heightScale };
    float3 vd = { -1, 0, (h_D - h_N)*heightScale };
  
    float3 av_n = ( cross(va, vb) + cross(vb, vc) + cross(vc, vd) + cross(vd, va) ) / -4;
    return normalize( av_n );
}



			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				float2 uv2 : TEXCOORD1;
				float2 uv3: TEXCOORD2;
				float3 normal: NORMAL;

			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				 fixed4 diff : COLOR0;
		float2 uv2 : TEXCOORD1;
		float2 uv3 : TEXCOORD2;
				UNITY_FOG_COORDS(3)
				float4 vertex : SV_POSITION;

			};


			
			v2f vert (appdata v)
			{
			 v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                float4 rand= _Rnd;
                o.uv = v.uv;
               	float2 uv =o.uv;
				float rad = ((uv.x-0.5)*(uv.x-0.5)+(uv.y-0.5)*(uv.y-0.5))*16;
				uv.x = (uv.x-.5)*2;
				uv.y = (uv.y-.5)*2;
				uv = (uv*(rad+1)/1.5+1)/2;
				//radial uvs
  
                 float noise = f_noise(uv*_Tiling,v.uv*_Tiling/2,1,6/pow(_Tiling,.75),1);
              
                  float4 vertexPos = v.vertex;
        
               o.vertex.y-=400*(noise)*pow(_Height,2)*_Scale/(pow(_Tiling,1));//*(_Cutout+1),1)+2);//+ tex.r)/2;
               // o.vertex = mul(UNITY_MATRIX_MVP, vertexPos);

                           float normal_cell_size =16;//4*pow(_Cutout+1,.5)*pow(_Tiling,.25)*_Translucency;
  float heightSampleCenter = 1*(noise);


            float heightSampleRight =1*(f_noise((uv + float2(_MainTex_TexelSize.x*normal_cell_size+0*unity_DeltaTime[0], 0))*_Tiling,v.uv,1,6/pow(_Tiling,.75),1));// tex2D (_HeightMap, IN.uv_MainTex + float2(_HeightMap_TexelSize.x, 0)).r;
            float heightSampleUp = 1*(f_noise((uv+ float2(0,_MainTex_TexelSize.x*normal_cell_size+0*unity_DeltaTime[0]))*_Tiling,v.uv,1,6/pow(_Tiling,.75),1));//tex2D (_HeightMap, IN.uv_MainTex + float2(0, _HeightMap_TexelSize.y)).r;
     		float heightSampleDn = 1*(f_noise((uv- float2(0,_MainTex_TexelSize.x*normal_cell_size+0*unity_DeltaTime[0]))*_Tiling,v.uv,1,6/pow(_Tiling,.75),1));//tex2D (_HeightMap, IN.uv_MainTex + float2(0, _HeightMap_TexelSize.y)).r;
      		float heightSampleLeft =1*(f_noise((uv- float2(_MainTex_TexelSize.x*normal_cell_size+0*unity_DeltaTime[0], 0))*_Tiling,v.uv,1,6/pow(_Tiling,.75),1));// tex2D (_HeightMap, IN.uv_MainTex + float2(_HeightMap_TexelSize.x, 0)).r;

      		half3 l = _WorldSpaceLightPos0.xyz;
      		l.y+=.2;
      		l = normalize(l);
      		half nl1 = max(0, dot(float3(0,0,1), -UnityObjectToWorldDir(l)));
      		half nl1_ = max(0, dot(float3(0,0,1), -UnityObjectToWorldDir( _WorldSpaceLightPos0.xyz)));

            float3 normal3 =computeNormals( heightSampleUp, heightSampleRight, heightSampleDn, heightSampleLeft, heightSampleCenter, pow(nl1,.5)*pow(_Height/1.25,2)*6.0/pow(_Tiling,.5) ) ;//normalize(normal_);
            //calculating normals

                // get vertex normal in world space
                half3 worldNormal = UnityObjectToWorldNormal(normal3);
                half3 worldNormalMesh = UnityObjectToWorldNormal(v.normal);



              			   float3 n = float3(worldNormal.x,-worldNormal.y,-worldNormal.z);
              

    			     half nl = max(0, dot((n), -(l)));
      
                float l_y = (_WorldSpaceLightPos0.xyz).y;
                l_y = pow(abs(l_y ),.25) *(l_y/abs(l_y));

                float4 sunset_factor =_SColor*_SColor*pow(1-nl1_,8)*max(0,lerp(0,1,min(1,l_y*1.15+1)))*min(_Exposure,1)/_Density;// (_SColor*pow(.95-(_WorldSpaceLightPos0.xyz).y,2))*max(0,lerp(0,1,min(1,l_y*1.15+.88)))*min(_Exposure,1);
          
             	float4 color_v = _LightColor0*pow(nl1,2) +_SColor* _SColor*(1-nl1)/pow(clamp(_Density-.85,1,2),2);
                float translucency =_Translucency;//(1.1-pow(noise,.25))*_Translucency;
           
    			                 float3 worldpos = mul(unity_ObjectToWorld, v.vertex);
                 float3 viewDirection = normalize(worldpos - _WorldSpaceCameraPos);
                 float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
               float thickness = noise*nl/pow(_Translucency,2);/// clamp(asin(nl),0.0,1.0);

                 float g =  .975;//+ 0.025*(1-nl1);//clamp(nl1+1,0.95,.995);//*nl;//*abs(nl-.5);//*nl);//0.995; //
                float g_moon =  .9+ 0.125*(nl1);//clamp(nl1+1,0.95,.995);//*nl;//*abs(nl-.5);//*nl);//0.995; //
                
                 float direct_lighting = exp(-thickness);
               direct_lighting *=1+(1-g*g)/pow(1+g*g-2*g*dot(viewDirection,lightDirection),1.5)/3.14*pow(max(0,_LightColor0.z-.25),.5);

           
               direct_lighting *= 1.0 -exp(-2*pow(thickness,.25)*nl);//max(0,(1.0 -exp(-2*noise)));
			
				direct_lighting = clamp(direct_lighting,0.0,1.0);
				o.diff =(direct_lighting *color_v*_LightK*(1+sunset_factor)); 

				 o.diff.rgb += max(0,(ShadeSH9(half4(n,.5))/_Density+sunset_factor/pow(1+_Density,2)))+clamp((1-g_moon*g_moon)/pow(1+g_moon*g_moon-2*g_moon*dot(viewDirection,-lightDirection),1.5)/(3.14*132)*pow(max(0,_LightColor0.z-.05),.5),0,1);
          		  o.diff = min(o.diff,1);

          		

                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
			}


			fixed4 frag (v2f i) : SV_Target
			{
				// sample the texture
		
				float2 uv =i.uv;
				float rad = ((uv.x-0.5)*(uv.x-0.5)+(uv.y-0.5)*(uv.y-0.5))*16;
				uv.x = (uv.x-.5)*2;
				uv.y = (uv.y-.5)*2;
				uv = (uv*(rad+1)/1.5+1)/2;
    			  float noise = f_noise(uv*_Tiling,i.uv*_Tiling/2,1,10,0);





                // sample texture
                float4 tex_1 = tex2D(_MainTex, (uv+_Rnd.xy+float2(_Time[0]*_WindSpeed_X*.96,_Time[0]*_WindSpeed_Y*.96))*_TextureTiling*_Tiling);
                float4 tex_2 = tex2D(_MainTex, (uv+_Rnd.xy+float2(_Time[0]*_WindSpeed_X*.96,_Time[0]*_WindSpeed_Y*.96))*_TextureTiling*_Tiling*.5);

                float4 tex =abs(lerp(tex_1,tex_2,.5));
            	

                fixed4 col = (tex*4*_TextureBlend+noise)*_Exposure/(5-(1-_TextureBlend)*4);//+_Exposure-1;
                col.w = 1;
                	UNITY_APPLY_FOG(i.fogCoord, col);			
            	
                col.rgb *= (i.diff.rgb);

                //col.w =pow(1-( pow(abs(i.uv.x-.5)*2,2)+pow(abs(i.uv.y-.5)*2,2)),.75)*noise*_Transparency;
                float tr_coef = max(0,1-pow(rad,1.75)/12);//pow((1-abs(i.uv.x-.5)*2)*(1-abs(i.uv.y-.5)*2),.5);
                 col.w=max(noise*_Transparency*lerp(1,tr_coef,_Exposure),0);//lerp(0.75,tr_coef,_Exposure)
                 col.rgb = clamp( col.rgb,0,1);
    			col.rgb =(lerp(1,_Contrast,max(0,i.diff.rgb-.1))*(col.rgb-.5)+.5);

 
				return  col*_Color;
			}
			ENDCG
		}
	}
}
