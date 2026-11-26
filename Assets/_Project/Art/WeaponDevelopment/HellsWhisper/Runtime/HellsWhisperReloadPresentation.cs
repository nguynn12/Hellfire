using UnityEngine;

namespace Hellfire.WeaponDevelopment
{
    [CreateAssetMenu(menuName="Hellfire/Hell's Whisper/Approved Reload Presentation")]
    public sealed class HellsWhisperReloadPresentation : ScriptableObject
    {
        public string approval="User approved Task13 Blood Presence in Task14";
        public GameObject viewmodelPrefab;
        public TextAsset motionProfile;
        public Material bloodMaterial;
        public Vector3 viewmodelOffset=new Vector3(-.04f,0,0);
        public float uniformScale=.25f;
        public Vector3[] surfaceSites;
        public ReloadKeyframeDataTask08 LoadMotion()
        {
            var motion=JsonUtility.FromJson<ReloadKeyframeDataTask08>(motionProfile.text);
            if(motion==null || motion.Error()!=null)throw new System.InvalidOperationException("Invalid Hell's Whisper motion profile");
            return motion;
        }
    }
}
