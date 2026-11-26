using System;
using System.Collections.Generic;
using UnityEngine;
namespace Hellfire.WeaponDevelopment
{
    // Approved Task13 sampling, shared by authoring and runtime. No timer or gameplay writes.
    public sealed class HellsWhisperBloodPresence
    {
        Transform pivot;Camera camera;float frontDepth13;
        static float Ease(float t){t=Mathf.Clamp01(t);return t*t*(3-2*t);}
        static float Pulse(float seconds,float start,float peak,float end)
        {return seconds<start||seconds>end?0:seconds<peak?Ease((seconds-start)/(peak-start)):1-Ease((seconds-peak)/(end-peak));}
        static float FrontDepth(MeshRenderer model,Camera camera)
        {
            var b=model.localBounds;float nearest=float.PositiveInfinity;
            for(int i=0;i<8;i++)
            {
                var point=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                nearest=Mathf.Min(nearest,camera.transform.InverseTransformPoint(model.transform.TransformPoint(point)).z);
            }
            float length=Mathf.Max(model.transform.TransformVector(Vector3.right*b.size.x).magnitude,model.transform.TransformVector(Vector3.up*b.size.y).magnitude,model.transform.TransformVector(Vector3.forward*b.size.z).magnitude);
            return nearest-length*.012f;
        }
        void BloodDot13(List<ParticleSystem.Particle> list,Vector3 point,float size,float alpha,bool hot=false,bool foreground=false)
        {
            if(alpha<.001f||list.Count>=512)return;
            if(foreground)
            {
                // Camera-facing presentation wisps keep the mesh-derived screen position.
                // These are not surface sockets: only the mist/motes are placed ahead of the bounds.
                var cameraPoint=camera.transform.InverseTransformPoint(pivot.TransformPoint(point));
                float ratio=frontDepth13/cameraPoint.z;cameraPoint*=ratio;size*=ratio;
                point=pivot.InverseTransformPoint(camera.transform.TransformPoint(cameraPoint));
            }
            list.Add(new ParticleSystem.Particle{position=point,startSize=size,startLifetime=10,remainingLifetime=10,
                startColor=hot?new Color(1,.19f,.105f,Mathf.Clamp01(alpha)):new Color(1,.035f,.075f,Mathf.Clamp01(alpha)),randomSeed=(uint)(list.Count+1)});
        }
        public void Sample(float t,float L,Transform pivot,Camera camera,MeshRenderer model,Vector3[] sites,Action<int,List<ParticleSystem.Particle>> put)
        {
            this.pivot=pivot;this.camera=camera;
            frontDepth13=FrontDepth(model,camera);
            float s=t*2.2f; // Canonical authored phase only; gameplay supplies normalized progress.
            float first=Pulse(s,.40f,.54f,.78f),second=Pulse(s,.90f,1.07f,1.32f),ritual=Pulse(s,1.36f,1.62f,1.88f);
            float breath=Pulse(s,.20f,1.4f,2.04f)*.10f;
            float presence=first*.34f+second+ritual*1.15f+breath;
            var right=pivot.InverseTransformDirection(camera.transform.right).normalized;
            var up=pivot.InverseTransformDirection(camera.transform.up).normalized;
            var aura=new List<ParticleSystem.Particle>();
            for(int j=0;j<sites.Length;j++)
            {
                var site=sites[j];
                // Broad low-opacity halo and a smaller emissive core. No ring or glyph topology.
                BloodDot13(aura,site,L*.14f,presence*.18f);
                BloodDot13(aura,site,L*.065f,presence*.33f);
                BloodDot13(aura,site,L*.022f,presence*.55f,true);
                BloodDot13(aura,site,L*.16f,presence*.10f,false,true);
                for(int k=0;k<5;k++)
                {
                    float angle=k*2.399963f+j*.9f+s*.7f;
                    var offset=(right*Mathf.Cos(angle)*.025f+up*Mathf.Sin(angle)*.038f)*L;
                    BloodDot13(aura,site+offset,L*.075f,presence*.065f);
                }
            }
            put(0,aura);put(1,new List<ParticleSystem.Particle>());
            var motes=new List<ParticleSystem.Particle>();int count=presence<.01f?0:Mathf.RoundToInt(12+28*Mathf.Clamp01(presence));
            for(int i=0;i<count;i++)
            {
                float age=Mathf.Repeat(s*.55f+i*.618034f,1),angle=i*2.399963f+s*.9f;
                var site=sites[i%sites.Length];
                var offset=(right*Mathf.Cos(angle)*(.018f+.045f*age)+up*(Mathf.Sin(angle)*.035f+age*.035f))*L;
                BloodDot13(motes,site+offset,L*(.0045f+.002f*Mathf.Sin(i*1.7f)),presence*Mathf.Sin(age*Mathf.PI)*.9f,i%5==0,true);
            }
            put(2,motes);
            var mist=new List<ParticleSystem.Particle>();float flow=first*.25f+second*.65f+ritual+breath;
            // Two overlapping wisps follow the grip-to-body path, rather than orbiting empty space.
            for(int strand=0;strand<2;strand++)
                for(int i=0;i<100;i++)
                {
                    float u=i/99f,angle=u*7+s*2+strand*Mathf.PI;
                    var start=sites[Mathf.Min(2,sites.Length-1)];
                    var end=sites[1];
                    var point=Vector3.Lerp(start,end,u)+(right*Mathf.Sin(angle)*.063f+up*Mathf.Cos(angle*1.3f)*.026f)*L;
                    BloodDot13(mist,point,L*.045f,flow*Mathf.Sin(u*Mathf.PI)*.075f,false,true);
                }
            put(3,mist);
        }
    }
}
