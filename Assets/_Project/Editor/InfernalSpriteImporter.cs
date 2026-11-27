using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hellfire.EditorTools
{
    internal sealed class InfernalSpriteImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/_Project/Art/UI/Resources/Infernal/") ||
                !assetPath.EndsWith(".png")) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spriteBorder = Path.GetFileNameWithoutExtension(assetPath) switch
            {
                "Panel" => new Vector4(77, 76, 77, 76),
                "Button" => new Vector4(74, 37, 74, 37),
                "Meter" => new Vector4(68, 27, 68, 27),
                "Boss" => new Vector4(84, 27, 84, 27),
                _ => Vector4.zero
            };
        }
    }
}
