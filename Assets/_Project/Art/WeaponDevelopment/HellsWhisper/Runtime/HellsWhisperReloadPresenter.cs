using System;
using System.Collections.Generic;
using Hellfire.Combat;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hellfire.WeaponDevelopment
{
    // Local first-person cosmetic layer. Ammo, duration and cancellation belong to WeaponController.
    [DisallowMultipleComponent,DefaultExecutionOrder(1000)]
    public sealed class HellsWhisperReloadPresenter : MonoBehaviour
    {
        [SerializeField] WeaponController _controller;
        [SerializeField] Camera _camera;
        [SerializeField] HellsWhisperReloadPresentation _presentation;
        [SerializeField] Renderer _fallbackRenderer;
        Transform _root,_pivot;MeshRenderer _model;Material _material;
        ReloadKeyframeDataTask08 _motion;
        readonly HellsWhisperBloodPresence _blood=new HellsWhisperBloodPresence();
        readonly ParticleSystem[] _layers=new ParticleSystem[4];
        readonly ParticleSystem.Particle[] _buffer=new ParticleSystem.Particle[512];
        Vector3 _restPosition,_restScale,_unitsPerLength;Quaternion _restRotation;
        bool _fallbackHidden,_fallbackWasEnabled;
        public Transform PresentationPivot => _pivot;
        public bool IsPresenting {get;private set;}
        public float SampledProgress {get;private set;}
        public int ParticleCount
        {
            get {int count=0;foreach(var ps in _layers)if(ps!=null)count+=ps.particleCount;return count;}
        }
        void Awake()
        {
            if(_controller==null)_controller=GetComponentInParent<WeaponController>();
            if(_camera==null)_camera=GetComponentInParent<Camera>();
        }
        void OnEnable()
        {
            if(_controller==null)return;
            _controller.OnReloadStateChanged+=ReloadChanged;
            _controller.OnWeaponChanged+=WeaponChanged;
        }
        void OnDisable()
        {
            if(_controller!=null)
            {
                _controller.OnReloadStateChanged-=ReloadChanged;
                _controller.OnWeaponChanged-=WeaponChanged;
            }
            ResetPresentation();SetVisible(false);
        }
        void OnDestroy()
        {
            ResetPresentation();SetVisible(false);
            if(_root!=null)DestroyOwned(_root.gameObject);
            if(_material!=null)DestroyOwned(_material);
        }
        static void DestroyOwned(UnityEngine.Object value)
        {if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
        bool Eligible => _controller!=null && _controller.isActiveAndEnabled && GameModeHelper.CanLocalControl(_controller)
            && _controller.CurrentWeaponData!=null && _controller.CurrentWeaponData.WeaponId=="pistol"
            && _camera!=null && _camera.isActiveAndEnabled;
        void ReloadChanged(bool reloading)
        {if(!reloading)ResetPresentation();}
        void WeaponChanged(string _) {ResetPresentation();if(!Eligible)SetVisible(false);}
        void LateUpdate()
        {
            if(!Eligible){ResetPresentation();SetVisible(false);return;}
            if(_root==null)Build();
            SetVisible(true);
            if(!_controller.IsReloading){ResetPresentation();return;}
            // No elapsed time, coroutine or second timer in this component.
            ApplyProgress(_controller.ReloadProgress);
        }
        void Build()
        {
            if(_presentation==null||_presentation.viewmodelPrefab==null||_presentation.bloodMaterial==null
                ||_presentation.surfaceSites==null||_presentation.surfaceSites.Length<3)
                throw new InvalidOperationException("Hell's Whisper approved presentation is not configured");
            _motion=_presentation.LoadMotion();
            _root=new GameObject("HellsWhisper_Approved_Task13_Runtime").transform;_root.SetParent(transform,false);
            _root.localPosition=_presentation.viewmodelOffset;
            var calibration=new GameObject("PresentationCalibration").transform;calibration.SetParent(_root,false);
            calibration.localScale=Vector3.one*_presentation.uniformScale;
            _pivot=new GameObject("ReloadPresentationPivot").transform;_pivot.SetParent(calibration,false);
            var instance=Instantiate(_presentation.viewmodelPrefab,_pivot,false);
            _model=instance.GetComponentInChildren<MeshRenderer>();
            _restPosition=_pivot.localPosition;_restRotation=_pivot.localRotation;_restScale=_pivot.localScale;
            foreach(var node in _root.GetComponentsInChildren<Transform>(true))node.gameObject.layer=gameObject.layer;
            var bounds=_model.localBounds;
            float length=Mathf.Max(_model.transform.TransformVector(Vector3.right*bounds.size.x).magnitude,
                _model.transform.TransformVector(Vector3.up*bounds.size.y).magnitude,_model.transform.TransformVector(Vector3.forward*bounds.size.z).magnitude);
            _unitsPerLength=new Vector3(length/calibration.TransformVector(Vector3.right).magnitude,
                length/calibration.TransformVector(Vector3.up).magnitude,length/calibration.TransformVector(Vector3.forward).magnitude);
            _material=new Material(_presentation.bloodMaterial){name="Task13_Runtime_Instance",hideFlags=HideFlags.DontSave};
            for(int i=0;i<4;i++)
            {
                var go=new GameObject("Task13_VFX_"+i);go.transform.SetParent(_pivot,false);go.layer=gameObject.layer;
                var ps=go.AddComponent<ParticleSystem>();_layers[i]=ps;
                var main=ps.main;main.playOnAwake=false;main.loop=false;main.startSpeed=0;main.maxParticles=512;
                main.simulationSpace=ParticleSystemSimulationSpace.Local;main.scalingMode=ParticleSystemScalingMode.Hierarchy;
                var emission=ps.emission;emission.enabled=false;var shape=ps.shape;shape.enabled=false;
                var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=_material;
                renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
        void ApplyProgress(float progress)
        {
            SampledProgress=Mathf.Clamp01(progress);IsPresenting=true;
            _motion.Evaluate(SampledProgress,out var position,out var rotation);
            var offset=_motion.positionUnits=="ModelLengthFraction"?Vector3.Scale(position,_unitsPerLength):position;
            _pivot.localPosition=_restPosition+offset*_motion.motionIntensity;
            _pivot.localRotation=_restRotation*Quaternion.Euler(rotation*_motion.motionIntensity);
            _material.SetFloat("_Phase",SampledProgress);
            _blood.Sample(SampledProgress,_unitsPerLength.x,_pivot,_camera,_model,_presentation.surfaceSites,Put);
        }
        void Put(int layer,List<ParticleSystem.Particle> points)
        {
            var ps=_layers[layer];
            if(!_motion.vfxEnabled||points.Count==0){ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);return;}
            int count=Mathf.Min(points.Count,_buffer.Length);points.CopyTo(0,_buffer,0,count);
            ps.Pause(true);ps.SetParticles(_buffer,count);
        }
        public void ResetPresentation()
        {
            IsPresenting=false;SampledProgress=0;
            if(_pivot!=null){_pivot.localPosition=_restPosition;_pivot.localRotation=_restRotation;_pivot.localScale=_restScale;}
            foreach(var ps in _layers)if(ps!=null)ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        void SetVisible(bool visible)
        {
            if(_root!=null&&_root.gameObject.activeSelf!=visible)_root.gameObject.SetActive(visible);
            if(_fallbackRenderer==null)return;
            if(visible&&!_fallbackHidden){_fallbackWasEnabled=_fallbackRenderer.enabled;_fallbackRenderer.enabled=false;_fallbackHidden=true;}
            else if(!visible&&_fallbackHidden){_fallbackRenderer.enabled=_fallbackWasEnabled;_fallbackHidden=false;}
        }
    }
}
