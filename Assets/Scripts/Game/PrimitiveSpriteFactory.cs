using System.Collections.Generic;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>Icon shapes the map view can rasterise without any imported art asset.</summary>
    public enum PrimitiveShape
    {
        Circle = 0,
        Ring = 1,
        Square = 2,
        Diamond = 3,
        Triangle = 4,
        Hexagon = 5,
    }

    /// <summary>
    /// Builds every sprite the map needs procedurally, in code. Story 002 forbids external art
    /// assets, and the project runs the Built-in Render Pipeline with no URP package, so the view
    /// uses plain <see cref="SpriteRenderer"/>s: a runtime <see cref="SpriteRenderer"/> picks up
    /// the engine's built-in sprite material by itself, which cannot be shader-stripped from a
    /// player build the way a <c>Shader.Find</c> lookup can.
    /// </summary>
    /// <remarks>
    /// Every sprite is generated once and cached for the lifetime of the process, so nothing here
    /// allocates on a per-frame path. Shapes are rasterised white and tinted per instance through
    /// <see cref="SpriteRenderer.color"/>, so one texture serves every colour.
    /// </remarks>
    public static class PrimitiveSpriteFactory
    {
        private const int ShapeTextureSize = 64;

        /// <summary>Supersampling grid per axis when rasterising a shape edge (3 => 9 samples per pixel).</summary>
        private const int EdgeSamplesPerAxis = 3;

        private static readonly Dictionary<PrimitiveShape, Sprite> ShapeCache = new Dictionary<PrimitiveShape, Sprite>();
        private static Sprite _whitePixel;

        /// <summary>
        /// A 1x1 white sprite exactly one world unit across. Scale it to draw grid lines and links:
        /// <c>localScale = new Vector3(length, thickness, 1f)</c>.
        /// </summary>
        public static Sprite GetWhitePixel()
        {
            if (_whitePixel != null)
            {
                return _whitePixel;
            }

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "NightShift_WhitePixel",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            _whitePixel = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            _whitePixel.name = "NightShift_WhitePixel";
            return _whitePixel;
        }

        /// <summary>
        /// A white sprite of the requested shape, one world unit across, with an antialiased edge.
        /// Cached per shape.
        /// </summary>
        public static Sprite GetShape(PrimitiveShape shape)
        {
            if (ShapeCache.TryGetValue(shape, out Sprite cached) && cached != null)
            {
                return cached;
            }

            Sprite created = BuildShapeSprite(shape);
            ShapeCache[shape] = created;
            return created;
        }

        private static Sprite BuildShapeSprite(PrimitiveShape shape)
        {
            int size = ShapeTextureSize;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "NightShift_" + shape,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[size * size];
            int samples = EdgeSamplesPerAxis * EdgeSamplesPerAxis;
            float step = 1f / (EdgeSamplesPerAxis + 1);

            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    int hits = 0;
                    for (int sy = 1; sy <= EdgeSamplesPerAxis; sy++)
                    {
                        for (int sx = 1; sx <= EdgeSamplesPerAxis; sx++)
                        {
                            // Map the sample into [-1, 1] on both axes.
                            float u = (px + sx * step) / size * 2f - 1f;
                            float v = (py + sy * step) / size * 2f - 1f;
                            if (IsInside(shape, u, v))
                            {
                                hits++;
                            }
                        }
                    }

                    byte alpha = (byte)(255 * hits / samples);
                    pixels[py * size + px] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            // pixelsPerUnit == texture width makes the sprite exactly one world unit across, so a
            // caller sizes an icon purely through transform scale.
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = "NightShift_" + shape;
            return sprite;
        }

        /// <summary>Shape membership test in normalised [-1, 1] coordinates.</summary>
        private static bool IsInside(PrimitiveShape shape, float u, float v)
        {
            switch (shape)
            {
                case PrimitiveShape.Circle:
                    return u * u + v * v <= 0.92f * 0.92f;

                case PrimitiveShape.Ring:
                {
                    float r = Mathf.Sqrt(u * u + v * v);
                    return r <= 0.92f && r >= 0.55f;
                }

                case PrimitiveShape.Square:
                    return Mathf.Abs(u) <= 0.84f && Mathf.Abs(v) <= 0.84f;

                case PrimitiveShape.Diamond:
                    return Mathf.Abs(u) + Mathf.Abs(v) <= 0.98f;

                case PrimitiveShape.Triangle:
                {
                    // Apex at (0, 0.92), base at v = -0.72 with half-width 0.92.
                    const float apexY = 0.92f;
                    const float baseY = -0.72f;
                    const float halfBase = 0.92f;
                    if (v < baseY || v > apexY)
                    {
                        return false;
                    }
                    float halfWidthHere = halfBase * (apexY - v) / (apexY - baseY);
                    return Mathf.Abs(u) <= halfWidthHere;
                }

                case PrimitiveShape.Hexagon:
                {
                    // Flat-top regular hexagon.
                    const float radius = 0.90f;
                    float au = Mathf.Abs(u);
                    float av = Mathf.Abs(v);
                    return av <= radius && au * 0.8660254f + av * 0.5f <= radius;
                }

                default:
                    return false;
            }
        }
    }
}
