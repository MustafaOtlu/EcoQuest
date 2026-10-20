using UnityEditor;
using UnityEngine;

// Every PNG under Resources/EcoIcons is a UI sprite: crisp, uncompressed, with mipmaps so 128 px art stays smooth at 18–48 px.
public sealed class EcoIconImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').Contains("/Resources/EcoIcons/")) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
        importer.filterMode = FilterMode.Trilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 256;
    }
}
