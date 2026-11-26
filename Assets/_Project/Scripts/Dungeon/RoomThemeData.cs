// Script: RoomThemeData.cs
// Purpose: Data-only room palettes and weighted obstacle definitions (section 20.3).
// Execution: Server and clients read identical assets for deterministic local geometry.
using System;
using UnityEngine;

namespace Hellfire.Dungeon
{
    public enum ObstacleShape { Box, Cylinder, Capsule }

    [Serializable]
    public class ObstaclePart
    {
        [SerializeField] private ObstacleShape _shape;
        [SerializeField] private Vector3 _size = Vector3.one;
        [SerializeField] private Vector3 _position;
        [SerializeField] private Vector3 _rotation;
        [SerializeField] private bool _emissive;
        public ObstacleShape Shape => _shape;
        public Vector3 Size => _size;
        public Vector3 Position => _position;
        public Vector3 Rotation => _rotation;
        public bool Emissive => _emissive;
    }

    [Serializable]
    public class ObstacleDef
    {
        [SerializeField] private string _id;
        [SerializeField, Min(0)] private int _weight = 1;
        [SerializeField] private ObstacleShape _shape;
        [SerializeField] private Vector3 _size = Vector3.one;
        [SerializeField] private Vector3 _maxSize = Vector3.one;
        [SerializeField] private Color _color = Color.gray;
        [SerializeField] private Material _material;
        [SerializeField] private Vector2Int _clusterCount = Vector2Int.one;
        [SerializeField, Min(0)] private float _clusterRadius;
        [SerializeField, Range(1, 2)] private int _maxStack = 1;
        [SerializeField] private Vector3 _rotationRange;
        [SerializeField] private Color _lightColor = new Color(1f, 0.2f, 0.03f);
        [SerializeField] private float _lightRange;
        [SerializeField] private float _lightIntensity = 1f;
        [SerializeField] private GameObject _prefab;
        [SerializeField] private ObstaclePart[] _parts = Array.Empty<ObstaclePart>();
        public string Id => _id;
        public int Weight => _weight;
        public ObstacleShape Shape => _shape;
        public Vector3 Size => _size;
        public Vector3 MaxSize => _maxSize;
        public Color Color => _color;
        public Material Material => _material;
        public Vector2Int ClusterCount => _clusterCount;
        public float ClusterRadius => _clusterRadius;
        public int MaxStack => _maxStack;
        public Vector3 RotationRange => _rotationRange;
        public Color LightColor => _lightColor;
        public float LightRange => _lightRange;
        public float LightIntensity => _lightIntensity;
        public GameObject Prefab => _prefab;
        public ObstaclePart[] Parts => _parts;
    }

    [CreateAssetMenu(menuName = "Hellfire/Room Theme", fileName = "RoomTheme")]
    public class RoomThemeData : ScriptableObject
    {
        [SerializeField] private string _themeName;
        [SerializeField] private Color _floorColor;
        [SerializeField] private Color _wallColor;
        [SerializeField] private ObstacleDef[] _obstacles = Array.Empty<ObstacleDef>();
        public string ThemeName => _themeName;
        public Color FloorColor => _floorColor;
        public Color WallColor => _wallColor;
        public ObstacleDef[] Obstacles => _obstacles;
    }
}
