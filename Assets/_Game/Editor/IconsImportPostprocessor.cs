#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FantasyGuildmaster.Editor
{
    public sealed class IconsImportPostprocessor : AssetPostprocessor
    {
        private const string IconsRoot = "Assets/_Game/Resources/Icons/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(IconsRoot))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 256;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.spriteImportMode = SpriteImportMode.Single;
        }
    }
}
#endif
