using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Hellfire.UI
{
    // Clips the 9-sliced mesh at the four outer corners, preserving its texture coordinates.
    internal sealed class RoundedSpriteCorners : BaseMeshEffect
    {
        private readonly List<UIVertex> _source = new();
        private readonly List<UIVertex> _a = new(20);
        private readonly List<UIVertex> _b = new(20);

        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive() || mesh.currentVertCount == 0) return;
            Rect rect = graphic.GetPixelAdjustedRect();
            float radius = Mathf.Min(8f, rect.width * .15f, rect.height * .15f);
            if (radius <= 0f) return;
            _source.Clear();
            mesh.GetUIVertexStream(_source);
            mesh.Clear();

            for (int triangle = 0; triangle < _source.Count; triangle += 3)
            {
                _a.Clear();
                for (int i = 0; i < 3; i++) _a.Add(_source[triangle + i]);
                for (int corner = 0; corner < 4 && _a.Count > 0; corner++)
                {
                    float x = corner % 2 == 0 ? -1f : 1f;
                    float y = corner < 2 ? -1f : 1f;
                    var centre = new Vector2(x < 0 ? rect.xMin + radius : rect.xMax - radius,
                        y < 0 ? rect.yMin + radius : rect.yMax - radius);
                    for (int segment = 0; segment < 3 && _a.Count > 0; segment++)
                    {
                        float angle = (segment + .5f) * Mathf.PI / 6f;
                        var outward = new Vector2(x * Mathf.Cos(angle), y * Mathf.Sin(angle));
                        Clip(outward, Vector2.Dot(outward, centre) + radius * .9659f);
                    }
                }

                int first = mesh.currentVertCount;
                foreach (var vertex in _a) mesh.AddVert(vertex);
                for (int i = 1; i + 1 < _a.Count; i++) mesh.AddTriangle(first, first + i, first + i + 1);
            }
        }

        private void Clip(Vector2 normal, float edge)
        {
            _b.Clear();
            var prior = _a[_a.Count - 1];
            float priorDistance = Vector2.Dot(prior.position, normal) - edge;
            foreach (var next in _a)
            {
                float distance = Vector2.Dot(next.position, normal) - edge;
                if ((priorDistance > 0f) != (distance > 0f))
                {
                    float t = priorDistance / (priorDistance - distance);
                    var vertex = prior;
                    vertex.position = Vector3.LerpUnclamped(prior.position, next.position, t);
                    vertex.uv0 = Vector4.LerpUnclamped(prior.uv0, next.uv0, t);
                    _b.Add(vertex);
                }
                if (distance <= 0f) _b.Add(next);
                prior = next;
                priorDistance = distance;
            }
            _a.Clear();
            _a.AddRange(_b);
        }
    }
}
