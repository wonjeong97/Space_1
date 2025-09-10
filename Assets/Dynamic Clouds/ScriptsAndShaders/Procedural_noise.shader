// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Procedural Clouds/PC_Noise"
{
	Properties
	{
		_MainTex ("Color (RGB) Alpha (A)", 2D) = "white"{}
		  _Tiling ("Tiling", Range(1,32)) = 1
		  _WindSpeed_X("Wind speed X", Float) = 1
		  _WindSpeed_Y("Wind speed Y", Float) = 1
		  _CloudAnimation("Clouds Animation", Float) = 1
		  _Rnd("Randomizer", Vector) = (0,0,0,0)
		  _Density ("Density", Range(0, 2)) = .5
		  	 _Cutout("Cutout", Range(0,2)) = 0
		  	 _Height("Height", Range(0,1))=1
		  	 _Detalization ("Detalization",Range(0.1,1)) = 1
	}
	SubShader
	{
		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "LightMode"="ForwardBase"}
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
		
			float _Tiling;

			float _WindSpeed_X;
			float _WindSpeed_Y;
			float _CloudAnimation;
			float _Density;
			float _Cutout;
			float4 _Rnd;
			float _Height;
			float _Detalization;
		sampler2D _MainTex;
				 uniform float4 _MainTex_TexelSize;
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
//Copyright (C) 2011 by Ashima Arts (Simplex noise)
//Copyright (C) 2011-2016 by Stefan Gustavson (Classic noise and others)

//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in
//all copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
//THE SOFTWARE.

float4 mod289(float4 x)
{
  return x - floor(x * (1.0 / 289.0)) * 289.0;
}

float4 permute(float4 x)
{
  return mod289(((x*34.0)+1.0)*x);
}

float4 taylorInvSqrt(float4 r)
{
  return 1.79284291400159 - 0.85373472095314 * r;
}

float4 fade(float4 t) {
  return t*t*t*(t*(t*6.0-15.0)+10.0);
}

// Classic Perlin noise
float cnoise(float4 P)
{
  float4 Pi0 = floor(P); // Integer part for indexing
  float4 Pi1 = Pi0 + 1.0; // Integer part + 1
  Pi0 = mod289(Pi0);
  Pi1 = mod289(Pi1);
  float4 Pf0 = frac(P); // fracional part for interpolation
  float4 Pf1 = Pf0 - 1.0; // fracional part - 1.0
  float4 ix = float4(Pi0.x, Pi1.x, Pi0.x, Pi1.x);
  float4 iy = float4(Pi0.yy, Pi1.yy);
  float4 iz0 = float4(Pi0.zzzz);
  float4 iz1 = float4(Pi1.zzzz);
  float4 iw0 = float4(Pi0.wwww);
  float4 iw1 = float4(Pi1.wwww);

  float4 ixy = permute(permute(ix) + iy);
  float4 ixy0 = permute(ixy + iz0);
  float4 ixy1 = permute(ixy + iz1);
  float4 ixy00 = permute(ixy0 + iw0);
  float4 ixy01 = permute(ixy0 + iw1);
  float4 ixy10 = permute(ixy1 + iw0);
  float4 ixy11 = permute(ixy1 + iw1);

  float4 gx00 = ixy00 * (1.0 / 7.0);
  float4 gy00 = floor(gx00) * (1.0 / 7.0);
  float4 gz00 = floor(gy00) * (1.0 / 6.0);
  gx00 = frac(gx00) - 0.5;
  gy00 = frac(gy00) - 0.5;
  gz00 = frac(gz00) - 0.5;
  float4 gw00 = float4(0.75,0.75,0.75,0.75) - abs(gx00) - abs(gy00) - abs(gz00);
  float4 sw00 = step(gw00, float4(0.0,0.0,0.0,0.0));
  gx00 -= sw00 * (step(0.0, gx00) - 0.5);
  gy00 -= sw00 * (step(0.0, gy00) - 0.5);

  float4 gx01 = ixy01 * (1.0 / 7.0);
  float4 gy01 = floor(gx01) * (1.0 / 7.0);
  float4 gz01 = floor(gy01) * (1.0 / 6.0);
  gx01 = frac(gx01) - 0.5;
  gy01 = frac(gy01) - 0.5;
  gz01 = frac(gz01) - 0.5;
  float4 gw01 = float4(0.75,0.75,0.75,0.75) - abs(gx01) - abs(gy01) - abs(gz01);
  float4 sw01 = step(gw01, float4(0.0,0.0,0.0,0.0));
  gx01 -= sw01 * (step(0.0, gx01) - 0.5);
  gy01 -= sw01 * (step(0.0, gy01) - 0.5);

  float4 gx10 = ixy10 * (1.0 / 7.0);
  float4 gy10 = floor(gx10) * (1.0 / 7.0);
  float4 gz10 = floor(gy10) * (1.0 / 6.0);
  gx10 = frac(gx10) - 0.5;
  gy10 = frac(gy10) - 0.5;
  gz10 = frac(gz10) - 0.5;
  float4 gw10 = float4(0.75,0.75,0.75,0.75) - abs(gx10) - abs(gy10) - abs(gz10);
  float4 sw10 = step(gw10, float4(0.0,0.0,0.0,0.0));
  gx10 -= sw10 * (step(0.0, gx10) - 0.5);
  gy10 -= sw10 * (step(0.0, gy10) - 0.5);

  float4 gx11 = ixy11 * (1.0 / 7.0);
  float4 gy11 = floor(gx11) * (1.0 / 7.0);
  float4 gz11 = floor(gy11) * (1.0 / 6.0);
  gx11 = frac(gx11) - 0.5;
  gy11 = frac(gy11) - 0.5;
  gz11 = frac(gz11) - 0.5;
  float4 gw11 = float4(0.75,0.75,0.75,0.75) - abs(gx11) - abs(gy11) - abs(gz11);
  float4 sw11 = step(gw11, float4(0.0,0.0,0.0,0.0));
  gx11 -= sw11 * (step(0.0, gx11) - 0.5);
  gy11 -= sw11 * (step(0.0, gy11) - 0.5);

  float4 g0000 = float4(gx00.x,gy00.x,gz00.x,gw00.x);
  float4 g1000 = float4(gx00.y,gy00.y,gz00.y,gw00.y);
  float4 g0100 = float4(gx00.z,gy00.z,gz00.z,gw00.z);
  float4 g1100 = float4(gx00.w,gy00.w,gz00.w,gw00.w);
  float4 g0010 = float4(gx10.x,gy10.x,gz10.x,gw10.x);
  float4 g1010 = float4(gx10.y,gy10.y,gz10.y,gw10.y);
  float4 g0110 = float4(gx10.z,gy10.z,gz10.z,gw10.z);
  float4 g1110 = float4(gx10.w,gy10.w,gz10.w,gw10.w);
  float4 g0001 = float4(gx01.x,gy01.x,gz01.x,gw01.x);
  float4 g1001 = float4(gx01.y,gy01.y,gz01.y,gw01.y);
  float4 g0101 = float4(gx01.z,gy01.z,gz01.z,gw01.z);
  float4 g1101 = float4(gx01.w,gy01.w,gz01.w,gw01.w);
  float4 g0011 = float4(gx11.x,gy11.x,gz11.x,gw11.x);
  float4 g1011 = float4(gx11.y,gy11.y,gz11.y,gw11.y);
  float4 g0111 = float4(gx11.z,gy11.z,gz11.z,gw11.z);
  float4 g1111 = float4(gx11.w,gy11.w,gz11.w,gw11.w);

  float4 norm00 = taylorInvSqrt(float4(dot(g0000, g0000), dot(g0100, g0100), dot(g1000, g1000), dot(g1100, g1100)));
  g0000 *= norm00.x;
  g0100 *= norm00.y;
  g1000 *= norm00.z;
  g1100 *= norm00.w;

  float4 norm01 = taylorInvSqrt(float4(dot(g0001, g0001), dot(g0101, g0101), dot(g1001, g1001), dot(g1101, g1101)));
  g0001 *= norm01.x;
  g0101 *= norm01.y;
  g1001 *= norm01.z;
  g1101 *= norm01.w;

  float4 norm10 = taylorInvSqrt(float4(dot(g0010, g0010), dot(g0110, g0110), dot(g1010, g1010), dot(g1110, g1110)));
  g0010 *= norm10.x;
  g0110 *= norm10.y;
  g1010 *= norm10.z;
  g1110 *= norm10.w;

  float4 norm11 = taylorInvSqrt(float4(dot(g0011, g0011), dot(g0111, g0111), dot(g1011, g1011), dot(g1111, g1111)));
  g0011 *= norm11.x;
  g0111 *= norm11.y;
  g1011 *= norm11.z;
  g1111 *= norm11.w;

  float n0000 = dot(g0000, Pf0);
  float n1000 = dot(g1000, float4(Pf1.x, Pf0.yzw));
  float n0100 = dot(g0100, float4(Pf0.x, Pf1.y, Pf0.zw));
  float n1100 = dot(g1100, float4(Pf1.xy, Pf0.zw));
  float n0010 = dot(g0010, float4(Pf0.xy, Pf1.z, Pf0.w));
  float n1010 = dot(g1010, float4(Pf1.x, Pf0.y, Pf1.z, Pf0.w));
  float n0110 = dot(g0110, float4(Pf0.x, Pf1.yz, Pf0.w));
  float n1110 = dot(g1110, float4(Pf1.xyz, Pf0.w));
  float n0001 = dot(g0001, float4(Pf0.xyz, Pf1.w));
  float n1001 = dot(g1001, float4(Pf1.x, Pf0.yz, Pf1.w));
  float n0101 = dot(g0101, float4(Pf0.x, Pf1.y, Pf0.z, Pf1.w));
  float n1101 = dot(g1101, float4(Pf1.xy, Pf0.z, Pf1.w));
  float n0011 = dot(g0011, float4(Pf0.xy, Pf1.zw));
  float n1011 = dot(g1011, float4(Pf1.x, Pf0.y, Pf1.zw));
  float n0111 = dot(g0111, float4(Pf0.x, Pf1.yzw));
  float n1111 = dot(g1111, Pf1);

  float4 fade_xyzw = fade(Pf0);
  float4 n_0w = lerp(float4(n0000, n1000, n0100, n1100), float4(n0001, n1001, n0101, n1101), fade_xyzw.w);
  float4 n_1w = lerp(float4(n0010, n1010, n0110, n1110), float4(n0011, n1011, n0111, n1111), fade_xyzw.w);
  float4 n_zw = lerp(n_0w, n_1w, fade_xyzw.z);
  float2 n_yzw = lerp(n_zw.xy, n_zw.zw, fade_xyzw.y);
  float n_xyzw = lerp(n_yzw.x, n_yzw.y, fade_xyzw.x);
  return 2.2 * n_xyzw;
}

// Classic Perlin noise, periodic version
float pnoise(float4 P, float4 rep)
{
  float4 Pi0 = fmod(floor(P), rep); // Integer part modulo rep
  float4 Pi1 = fmod(Pi0 + 1.0, rep); // Integer part + 1 mod rep
  Pi0 = mod289(Pi0);
  Pi1 = mod289(Pi1);
  float4 Pf0 = frac(P); // fracional part for interpolation
  float4 Pf1 = Pf0 - 1.0; // fracional part - 1.0
  float4 ix = float4(Pi0.x, Pi1.x, Pi0.x, Pi1.x);
  float4 iy = float4(Pi0.yy, Pi1.yy);
  float4 iz0 = float4(Pi0.zzzz);
  float4 iz1 = float4(Pi1.zzzz);
  float4 iw0 = float4(Pi0.wwww);
  float4 iw1 = float4(Pi1.wwww);

  float4 ixy = permute(permute(ix) + iy);
  float4 ixy0 = permute(ixy + iz0);
  float4 ixy1 = permute(ixy + iz1);
  float4 ixy00 = permute(ixy0 + iw0);
  float4 ixy01 = permute(ixy0 + iw1);
  float4 ixy10 = permute(ixy1 + iw0);
  float4 ixy11 = permute(ixy1 + iw1);

  float4 gx00 = ixy00 * (1.0 / 7.0);
  float4 gy00 = floor(gx00) * (1.0 / 7.0);
  float4 gz00 = floor(gy00) * (1.0 / 6.0);
  gx00 = frac(gx00) - 0.5;
  gy00 = frac(gy00) - 0.5;
  gz00 = frac(gz00) - 0.5;
  float4 gw00 = float4(0.75,0.75,0.75,0.75) - abs(gx00) - abs(gy00) - abs(gz00);
  float4 sw00 = step(gw00, float4(0.0,0.0,0.0,0.0));
  gx00 -= sw00 * (step(0.0, gx00) - 0.5);
  gy00 -= sw00 * (step(0.0, gy00) - 0.5);

  float4 gx01 = ixy01 * (1.0 / 7.0);
  float4 gy01 = floor(gx01) * (1.0 / 7.0);
  float4 gz01 = floor(gy01) * (1.0 / 6.0);
  gx01 = frac(gx01) - 0.5;
  gy01 = frac(gy01) - 0.5;
  gz01 = frac(gz01) - 0.5;
  float4 gw01 = float4(0.75,0.75,0.75,0.75) - abs(gx01) - abs(gy01) - abs(gz01);
  float4 sw01 = step(gw01, float4(0.0,0.0,0.0,0.0));
  gx01 -= sw01 * (step(0.0, gx01) - 0.5);
  gy01 -= sw01 * (step(0.0, gy01) - 0.5);

  float4 gx10 = ixy10 * (1.0 / 7.0);
  float4 gy10 = floor(gx10) * (1.0 / 7.0);
  float4 gz10 = floor(gy10) * (1.0 / 6.0);
  gx10 = frac(gx10) - 0.5;
  gy10 = frac(gy10) - 0.5;
  gz10 = frac(gz10) - 0.5;
  float4 gw10 = float4(0.75,0.75,0.75,0.75) - abs(gx10) - abs(gy10) - abs(gz10);
  float4 sw10 = step(gw10, float4(0.0,0.0,0.0,0.0));
  gx10 -= sw10 * (step(0.0, gx10) - 0.5);
  gy10 -= sw10 * (step(0.0, gy10) - 0.5);

  float4 gx11 = ixy11 * (1.0 / 7.0);
  float4 gy11 = floor(gx11) * (1.0 / 7.0);
  float4 gz11 = floor(gy11) * (1.0 / 6.0);
  gx11 = frac(gx11) - 0.5;
  gy11 = frac(gy11) - 0.5;
  gz11 = frac(gz11) - 0.5;
  float4 gw11 = float4(0.75,0.75,0.75,0.75) - abs(gx11) - abs(gy11) - abs(gz11);
  float4 sw11 = step(gw11, float4(0.0,0.0,0.0,0.0));
  gx11 -= sw11 * (step(0.0, gx11) - 0.5);
  gy11 -= sw11 * (step(0.0, gy11) - 0.5);

  float4 g0000 = float4(gx00.x,gy00.x,gz00.x,gw00.x);
  float4 g1000 = float4(gx00.y,gy00.y,gz00.y,gw00.y);
  float4 g0100 = float4(gx00.z,gy00.z,gz00.z,gw00.z);
  float4 g1100 = float4(gx00.w,gy00.w,gz00.w,gw00.w);
  float4 g0010 = float4(gx10.x,gy10.x,gz10.x,gw10.x);
  float4 g1010 = float4(gx10.y,gy10.y,gz10.y,gw10.y);
  float4 g0110 = float4(gx10.z,gy10.z,gz10.z,gw10.z);
  float4 g1110 = float4(gx10.w,gy10.w,gz10.w,gw10.w);
  float4 g0001 = float4(gx01.x,gy01.x,gz01.x,gw01.x);
  float4 g1001 = float4(gx01.y,gy01.y,gz01.y,gw01.y);
  float4 g0101 = float4(gx01.z,gy01.z,gz01.z,gw01.z);
  float4 g1101 = float4(gx01.w,gy01.w,gz01.w,gw01.w);
  float4 g0011 = float4(gx11.x,gy11.x,gz11.x,gw11.x);
  float4 g1011 = float4(gx11.y,gy11.y,gz11.y,gw11.y);
  float4 g0111 = float4(gx11.z,gy11.z,gz11.z,gw11.z);
  float4 g1111 = float4(gx11.w,gy11.w,gz11.w,gw11.w);

  float4 norm00 = taylorInvSqrt(float4(dot(g0000, g0000), dot(g0100, g0100), dot(g1000, g1000), dot(g1100, g1100)));
  g0000 *= norm00.x;
  g0100 *= norm00.y;
  g1000 *= norm00.z;
  g1100 *= norm00.w;

  float4 norm01 = taylorInvSqrt(float4(dot(g0001, g0001), dot(g0101, g0101), dot(g1001, g1001), dot(g1101, g1101)));
  g0001 *= norm01.x;
  g0101 *= norm01.y;
  g1001 *= norm01.z;
  g1101 *= norm01.w;

  float4 norm10 = taylorInvSqrt(float4(dot(g0010, g0010), dot(g0110, g0110), dot(g1010, g1010), dot(g1110, g1110)));
  g0010 *= norm10.x;
  g0110 *= norm10.y;
  g1010 *= norm10.z;
  g1110 *= norm10.w;

  float4 norm11 = taylorInvSqrt(float4(dot(g0011, g0011), dot(g0111, g0111), dot(g1011, g1011), dot(g1111, g1111)));
  g0011 *= norm11.x;
  g0111 *= norm11.y;
  g1011 *= norm11.z;
  g1111 *= norm11.w;

  float n0000 = dot(g0000, Pf0);
  float n1000 = dot(g1000, float4(Pf1.x, Pf0.yzw));
  float n0100 = dot(g0100, float4(Pf0.x, Pf1.y, Pf0.zw));
  float n1100 = dot(g1100, float4(Pf1.xy, Pf0.zw));
  float n0010 = dot(g0010, float4(Pf0.xy, Pf1.z, Pf0.w));
  float n1010 = dot(g1010, float4(Pf1.x, Pf0.y, Pf1.z, Pf0.w));
  float n0110 = dot(g0110, float4(Pf0.x, Pf1.yz, Pf0.w));
  float n1110 = dot(g1110, float4(Pf1.xyz, Pf0.w));
  float n0001 = dot(g0001, float4(Pf0.xyz, Pf1.w));
  float n1001 = dot(g1001, float4(Pf1.x, Pf0.yz, Pf1.w));
  float n0101 = dot(g0101, float4(Pf0.x, Pf1.y, Pf0.z, Pf1.w));
  float n1101 = dot(g1101, float4(Pf1.xy, Pf0.z, Pf1.w));
  float n0011 = dot(g0011, float4(Pf0.xy, Pf1.zw));
  float n1011 = dot(g1011, float4(Pf1.x, Pf0.y, Pf1.zw));
  float n0111 = dot(g0111, float4(Pf0.x, Pf1.yzw));
  float n1111 = dot(g1111, Pf1);

  float4 fade_xyzw = fade(Pf0);
  float4 n_0w = lerp(float4(n0000, n1000, n0100, n1100), float4(n0001, n1001, n0101, n1101), fade_xyzw.w);
  float4 n_1w = lerp(float4(n0010, n1010, n0110, n1110), float4(n0011, n1011, n0111, n1111), fade_xyzw.w);
  float4 n_zw = lerp(n_0w, n_1w, fade_xyzw.z);
  float2 n_yzw = lerp(n_zw.xy, n_zw.zw, fade_xyzw.y);
  float n_xyzw = lerp(n_yzw.x, n_yzw.y, fade_xyzw.x);
  return 2.2 * n_xyzw;
}

  float3 computeNormals( float h_A, float h_B, float h_C, float h_D, float h_N, float heightScale )
{
    //To make it easier we offset the points such that n is "0" height
    float3 va = { 0, 1, (h_A - h_N)*heightScale };
    float3 vb = { 1, 0, (h_B - h_N)*heightScale };
    float3 vc = { 0, -1, (h_C - h_N)*heightScale };
    float3 vd = { -1, 0, (h_D - h_N)*heightScale };
    //cross products of each vector yields the normal of each tri - return the average normal of all 4 tris
    float3 average_n = ( cross(va, vb) + cross(vb, vc) + cross(vc, vd) + cross(vd, va) ) / -4;
    return normalize( average_n );
}


float f_noise(float2 uv, float p, float details){
  float4 timed =  float4(uv+_Rnd.xy+float2(_Time[0]*_WindSpeed_X,_Time[0]*_WindSpeed_Y)*_Tiling,0.0,_Time[0]*_CloudAnimation);
				//float res = turbulence(timed) + turbulence(timed*2.0)/2.0 + turbulence(timed*4.0)/4.0 + turbulence(timed*8.0)/8.0;
				float res =-1.2+(_Density)*p;// cnoise(timed) + cnoise(timed*2.0)/2.0 + cnoise(timed*4.0)/4.0 + cnoise(timed*8.0)/8.0+ cnoise(timed*16.0)/16.0+ cnoise(timed*32.0)/32.0;
				for (float f = 0.0 ; f <= details*(_Detalization) ; f++ ){
       				float power = pow( 2, f );
        			res +=  abs(pnoise( float4( power * timed ),float4(8,8,8,8) )/power);
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






			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;

			};

			struct v2f
			{
				float2 uv : TEXCOORD0;


				 fixed4 diff : COLOR0;
			float4 vertex : SV_POSITION;

			};

		
			
			v2f vert (appdata v)
			{
			 v2f o;
			   o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;

               	float2 uv =o.uv;
				float rad = ((uv.x-0.5)*(uv.x-0.5)+(uv.y-0.5)*(uv.y-0.5))*16;
				//uv.x = (uv.x-.5)*2;
				//uv.y = (uv.y-.5)*2;
				//uv = (uv*(rad+1)/1.5+1)/2;
                 
                return o;
			}


			fixed4 frag (v2f i) : SV_Target
			{
				// sample the texture


				 
               	float2 uv =i.uv;
				//float rad = ((uv.x-0.5)*(uv.x-0.5)+(uv.y-0.5)*(uv.y-0.5))*16;
				//uv.x = (uv.x-.5)*2;
				//uv.y = (uv.y-.5)*2;
				//uv = (uv*(rad+1)/1.5+1)/2;
        

			
    			 float noise = f_noise(uv*_Tiling,1,4);//i.noise_hr;


                // sample texture
            
				return float4(noise,noise,noise,noise);
			}
			ENDCG
		}
	}
}
