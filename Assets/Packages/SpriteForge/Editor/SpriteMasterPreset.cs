using UnityEngine;

namespace SpriteForge
{
    /// <summary>Reusable SpriteForge workflow configuration.</summary>
    public sealed class SpriteMasterPreset : ScriptableObject
    {
        public int multipleOf = 4, padding = 2, atlasMaxSize = 2048;
        public bool powerOfTwo, trimTransparent = true, autoReverse;
        public float animationFps = 12f;
        public int textureType, compression = 1, spriteMode = 1, filterMode = 1, wrapMode = 1, maxTextureSize = 2048, compressionQuality = 50;
        public bool generateMipMaps, readable, alphaIsTransparency = true, crunchCompression;
        public float pixelsPerUnit = 100f;
        public int sliceColumns = 4, sliceRows = 4, paletteLimit = 16;
    }
}
