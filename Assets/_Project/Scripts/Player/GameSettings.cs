// Script: GameSettings.cs
// Mục đích: Quản lý thiết lập trò chơi (FOV, Âm lượng tổng, Giảm rung màn hình, Độ nhạy chuột) (Mục 16.9 đặc tả kỹ thuật).
// Môi trường thực thi: Client-only.

using System;
using UnityEngine;

namespace Hellfire.Player
{
    public static class GameSettings
    {
        public const string KeyFOV = "FieldOfView";
        public const string KeyMasterVolume = "MasterVolume";
        public const string KeyReduceScreenShake = "ReduceScreenShake";
        public const string KeyMouseSensitivity = "MouseSensitivity";

        public const float DefaultFOV = 90.0f;
        public const float MinFOV = 70.0f;
        public const float MaxFOV = 110.0f;

        public const float DefaultVolume = 1.0f;
        public const float DefaultSensitivity = 2.0f;

        private static bool _initialized = false;
        private static float _fieldOfView = DefaultFOV;
        private static float _masterVolume = DefaultVolume;
        private static bool _reduceScreenShake = false;
        private static float _mouseSensitivity = DefaultSensitivity;

        public static event Action<float> OnFOVChanged;
        public static event Action<float> OnMasterVolumeChanged;
        public static event Action<bool> OnReduceScreenShakeChanged;

        public static float FieldOfView
        {
            get
            {
                EnsureInitialized();
                return _fieldOfView;
            }
        }

        public static float MasterVolume
        {
            get
            {
                EnsureInitialized();
                return _masterVolume;
            }
        }

        public static bool ReduceScreenShake
        {
            get
            {
                EnsureInitialized();
                return _reduceScreenShake;
            }
        }

        public static float MouseSensitivity
        {
            get
            {
                EnsureInitialized();
                return _mouseSensitivity;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            _fieldOfView = Mathf.Clamp(PlayerPrefs.GetFloat(KeyFOV, DefaultFOV), MinFOV, MaxFOV);
            _masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMasterVolume, DefaultVolume));
            _reduceScreenShake = PlayerPrefs.GetInt(KeyReduceScreenShake, 0) == 1;
            _mouseSensitivity = Mathf.Max(0.1f, PlayerPrefs.GetFloat(KeyMouseSensitivity, DefaultSensitivity));

            AudioListener.volume = _masterVolume;
        }

        public static void SetFOV(float value)
        {
            EnsureInitialized();
            _fieldOfView = Mathf.Clamp(value, MinFOV, MaxFOV);
            PlayerPrefs.SetFloat(KeyFOV, _fieldOfView);
            PlayerPrefs.Save();
            OnFOVChanged?.Invoke(_fieldOfView);
        }

        public static void SetMasterVolume(float value)
        {
            EnsureInitialized();
            _masterVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(KeyMasterVolume, _masterVolume);
            PlayerPrefs.Save();
            AudioListener.volume = _masterVolume;
            OnMasterVolumeChanged?.Invoke(_masterVolume);
        }

        public static void SetReduceScreenShake(bool value)
        {
            EnsureInitialized();
            _reduceScreenShake = value;
            PlayerPrefs.SetInt(KeyReduceScreenShake, _reduceScreenShake ? 1 : 0);
            PlayerPrefs.Save();
            OnReduceScreenShakeChanged?.Invoke(_reduceScreenShake);
        }

        public static void SetMouseSensitivity(float value)
        {
            EnsureInitialized();
            _mouseSensitivity = Mathf.Max(0.1f, value);
            PlayerPrefs.SetFloat(KeyMouseSensitivity, _mouseSensitivity);
            PlayerPrefs.Save();
        }
    }
}
