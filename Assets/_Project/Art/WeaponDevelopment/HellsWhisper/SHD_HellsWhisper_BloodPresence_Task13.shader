Shader "Hellfire/WeaponLab/BloodPresenceTask13"
{
 Properties { _Tint("Blood tint",Color)=(1,0.8,0.9,1) _Intensity("Emissive presence",Float)=1.8 _Phase("Scrub phase",Float)=0 }
 SubShader { Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
 Pass { Blend SrcAlpha One ZWrite Off Cull Off
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 CBUFFER_START(UnityPerMaterial)
 half4 _Tint; float _Intensity; float _Phase;
 CBUFFER_END
 struct A { float4 p:POSITION; half4 c:COLOR; float2 uv:TEXCOORD0; };
 struct V { float4 p:SV_POSITION; half4 c:COLOR; float2 uv:TEXCOORD0; };
 V vert(A i) { V o;o.p=TransformObjectToHClip(i.p.xyz);o.c=i.c;o.uv=i.uv;return o; }
 half4 frag(V i):SV_Target {
  float2 q=i.uv*2-1;float r=length(q);
  float haze=.86+.09*sin(q.x*7+q.y*5+_Phase*5)+.05*sin(q.y*11-q.x*4);
  float a=exp(-r*r*3.2)*(1-smoothstep(.65,1,r))*haze;
  return half4(i.c.rgb*_Tint.rgb*_Intensity,i.c.a*a*_Tint.a);
 }
 ENDHLSL
 } }
}
