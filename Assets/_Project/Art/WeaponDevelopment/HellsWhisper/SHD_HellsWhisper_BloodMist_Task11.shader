Shader "Hellfire/WeaponLab/BloodMistTask11"
{
 Properties { _Tint("Blood tint",Color)=(0.48,0.018,0.025,1) _Phase("Scrub phase",Float)=0 }
 SubShader { Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
 Pass { Blend SrcAlpha One ZWrite Off Cull Off
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 CBUFFER_START(UnityPerMaterial)
 half4 _Tint; float _Phase;
 CBUFFER_END
 struct A { float4 p:POSITION; half4 c:COLOR; float2 uv:TEXCOORD0; };
 struct V { float4 p:SV_POSITION; half4 c:COLOR; float2 uv:TEXCOORD0; };
 V vert(A i) { V o;o.p=TransformObjectToHClip(i.p.xyz);o.c=i.c;o.uv=i.uv;return o; }
 half4 frag(V i):SV_Target {
  float2 q=i.uv*2-1;float r=length(q);
  float haze=0.78+0.12*sin(q.x*8+q.y*5+_Phase*3)+0.1*sin(q.y*11-q.x*4);
  float a=exp(-r*r*4.5)*smoothstep(1,0,r)*haze;
  return half4(i.c.rgb*_Tint.rgb,i.c.a*a*_Tint.a);
 }
 ENDHLSL
 } }
}
