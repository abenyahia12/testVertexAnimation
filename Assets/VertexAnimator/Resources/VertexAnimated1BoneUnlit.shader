Shader "VertexAnimated1BoneUnlit"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
		_OverlayTex("Texture", 2D) = "black" {}
		[HideInInspector] _AnimDataTex("", 2D) = "" {}
		[HideInInspector] _AnimDataInfo("", Vector) = (0, 0, 0, 0)
	}
	SubShader
	{
		//TODO: Correct ZTest/Culling #behavior
		Tags
		{ 
			"RenderType" = "Opaque"
			"DisableBatching" = "True"
		}
		LOD 150

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma instancing_options assumeuniformscaling nolodfade nolightprobe nolightmap
            #pragma multi_compile_instancing
			#pragma multi_compile __ MATERIAL_PREVIEW_GUI VERTEX_ANIMATED
			
			#include "UnityCG.cginc"

			//TODO: Add back buffer splitting.. whatever that was (See Custom cginc file.. I think it had something to do with removing unneccessary information) #Performance

            UNITY_INSTANCING_BUFFER_START(InstanceProperties0)
                UNITY_DEFINE_INSTANCED_PROP(float4, _ClipData) // (start_time, scaled_inverse_duration, normalized_block_width, encoded_block_uv)
            UNITY_INSTANCING_BUFFER_END(InstanceProperties0)

			UNITY_INSTANCING_BUFFER_START(InstanceProperties1)
				UNITY_DEFINE_INSTANCED_PROP(fixed, _OverlayOpacity)
			UNITY_INSTANCING_BUFFER_END(InstanceProperties1)

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0; // (encoded_uv, bone_y_offset + texel_height / 2)
                UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				half2 uv0 : TEXCOORD0;
				fixed3 uv1 : TEXCOORD1;
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;

			sampler2D _OverlayTex;
			float4 _OverlayTex_ST;

            sampler2D _AnimDataTex;
            float4 _AnimDataInfo; // (0, 0, 1 / width / 2 + small_error (0.0001), 1 / height)

			inline float2 DecodeUV(float codeduv)
			{
				float v = frac(codeduv);
				return float2((codeduv - v) / 2047.0, v);
			}

			inline float4 DecodePos(float4 encodedPos1, float4 encodedPos2)
			{
				float4 pos = encodedPos1 * 1.0 + encodedPos2 * (1.0 / 255.0);
				float mag = pos.w * 8.0;
				pos = pos * (mag * 2.0) - float4(mag, mag, mag, mag);
				pos.w = 1.0;
				return pos;
			}

			inline float4 RotateDir(float4 dir, float4 encoded_quat)
			{
				encoded_quat -= float4(0.5, 0.5, 0.5, 0.5);
				dir.xyz += 8.0 * cross(encoded_quat.xyz, cross(encoded_quat.xyz, dir.xyz) + encoded_quat.w * dir.xyz);
				return dir;
			}
			
			v2f vert (appdata v)
			{
				v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
#if VERTEX_ANIMATED
				float4 clipdata = UNITY_ACCESS_INSTANCED_PROP(InstanceProperties0, _ClipData);
				float interpolant = frac((_Time.y - clipdata.x) * clipdata.y);
				float4 blockuv = float4(DecodeUV(clipdata.w), 0, 0);
				blockuv.x += interpolant * clipdata.z + _AnimDataInfo.z;
				blockuv.y += v.uv.y;
				float4 encodedQuat = tex2Dlod(_AnimDataTex, blockuv);
				float4 encodedPos1 = tex2Dlod(_AnimDataTex, blockuv + float4(0, _AnimDataInfo.w, 0, 0));
				float4 encodedPos2 = tex2Dlod(_AnimDataTex, blockuv + float4(0, 2.0 * _AnimDataInfo.w, 0, 0));
				float4 localPos = RotateDir(v.vertex, encodedQuat) + DecodePos(encodedPos1, encodedPos2);
				float2 uv = DecodeUV(v.uv.x);
#elif MATERIAL_PREVIEW_GUI
				float4 localPos = v.vertex;
				float2 uv = v.uv;
#else
				float4 localPos = v.vertex;
				float2 uv = DecodeUV(v.uv.x);
#endif
				o.vertex = UnityObjectToClipPos(localPos);
				o.uv0 = TRANSFORM_TEX(uv, _MainTex);
				o.uv1 = fixed3(TRANSFORM_TEX(uv, _OverlayTex), UNITY_ACCESS_INSTANCED_PROP(InstanceProperties1, _OverlayOpacity));
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 color1 = tex2D(_MainTex, i.uv0);
				fixed4 color2 = tex2D(_OverlayTex, i.uv1.xy);
				return fixed4(color1.xyz * color1.w * (1 - color2.w * i.uv1.z) + color2.xyz * color2.w * i.uv1.z, 1.0);
			}
			ENDCG
		}
	}

	CustomEditor "VertexAnimatedShaderGUI"
}
