using UnityEngine;

namespace ByteDance.PICO.IconConfigurator.Editor
{
    /// <summary>
    /// Shared helpers for icon texture processing. Centralizes the square-normalization
    /// logic that import and AI Split preparation both rely on, and uses batched pixel
    /// writes instead of per-pixel <see cref="Texture2D.SetPixel"/> calls.
    /// </summary>
    public static class IconTextureUtility
    {
        /// <summary>
        /// Produces a square RGBA32 texture that contains the source scaled to fit and
        /// centered on a transparent background. The caller owns the returned texture and
        /// is responsible for destroying it.
        /// </summary>
        public static Texture2D NormalizeToSquare(Texture2D sourceTexture)
        {
            int targetSize = Mathf.Max(sourceTexture.width, sourceTexture.height) > 512 ? 1024 : 512;
            Texture2D normalized = new Texture2D(targetSize, targetSize, TextureFormat.RGBA32, false);

            Color[] pixels = new Color[targetSize * targetSize];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.clear;
            }

            float scale = Mathf.Min(
                (float)targetSize / sourceTexture.width,
                (float)targetSize / sourceTexture.height);
            int scaledWidth = Mathf.Max(1, Mathf.RoundToInt(sourceTexture.width * scale));
            int scaledHeight = Mathf.Max(1, Mathf.RoundToInt(sourceTexture.height * scale));
            int offsetX = (targetSize - scaledWidth) / 2;
            int offsetY = (targetSize - scaledHeight) / 2;

            for (int y = 0; y < scaledHeight; y++)
            {
                float v = scaledHeight > 1 ? (float)y / (scaledHeight - 1) : 0f;
                int rowStart = ((offsetY + y) * targetSize) + offsetX;
                for (int x = 0; x < scaledWidth; x++)
                {
                    float u = scaledWidth > 1 ? (float)x / (scaledWidth - 1) : 0f;
                    pixels[rowStart + x] = sourceTexture.GetPixelBilinear(u, v);
                }
            }

            normalized.SetPixels(pixels);
            normalized.Apply();
            return normalized;
        }
    }
}
