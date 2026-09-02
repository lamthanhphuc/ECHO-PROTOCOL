using UnityEngine;

namespace EchoProtocol.UI.HUD
{
    public static class HUDTextureUtility
    {
        private static Sprite _whitePixel;
        private static Sprite _circleFilled;
        private static Sprite _circleRing;
        private static Sprite _roundedBox;
        private static Sprite _panelBorder;
        private static Sprite _soundWave;

        public static Sprite WhitePixel
        {
            get
            {
                if (_whitePixel == null)
                {
                    Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                    tex.filterMode = FilterMode.Point;
                    Color[] colors = new Color[16];
                    for (int i = 0; i < 16; i++) colors[i] = Color.white;
                    tex.SetPixels(colors);
                    tex.Apply();
                    _whitePixel = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
                }
                return _whitePixel;
            }
        }

        public static Sprite CircleFilled
        {
            get
            {
                if (_circleFilled == null)
                {
                    const int size = 256;
                    Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                    tex.filterMode = FilterMode.Bilinear;
                    float center = (size - 1) * 0.5f;
                    float radius = center - 1.5f;

                    Color[] pixels = new Color[size * size];
                    for (int y = 0; y < size; y++)
                    {
                        for (int x = 0; x < size; x++)
                        {
                            float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                            float alpha = Mathf.Clamp01(radius - dist + 1f);
                            pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                        }
                    }
                    tex.SetPixels(pixels);
                    tex.Apply();
                    _circleFilled = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
                }
                return _circleFilled;
            }
        }

        public static Sprite CircleRing
        {
            get
            {
                if (_circleRing == null)
                {
                    const int size = 256;
                    const float thickness = 22f;
                    Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                    tex.filterMode = FilterMode.Bilinear;
                    float center = (size - 1) * 0.5f;
                    float outerRadius = center - 1.5f;
                    float innerRadius = outerRadius - thickness;

                    Color[] pixels = new Color[size * size];
                    for (int y = 0; y < size; y++)
                    {
                        for (int x = 0; x < size; x++)
                        {
                            float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                            float alphaOuter = Mathf.Clamp01(outerRadius - dist + 1f);
                            float alphaInner = Mathf.Clamp01(dist - innerRadius + 1f);
                            float alpha = Mathf.Min(alphaOuter, alphaInner);
                            pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                        }
                    }
                    tex.SetPixels(pixels);
                    tex.Apply();
                    _circleRing = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
                }
                return _circleRing;
            }
        }

        public static Sprite RoundedBox
        {
            get
            {
                if (_roundedBox == null)
                {
                    const int size = 128;
                    const float cornerRadius = 24f;
                    Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                    tex.filterMode = FilterMode.Bilinear;

                    Color[] pixels = new Color[size * size];
                    for (int y = 0; y < size; y++)
                    {
                        for (int x = 0; x < size; x++)
                        {
                            float dx = Mathf.Min(x, size - 1 - x);
                            float dy = Mathf.Min(y, size - 1 - y);

                            float alpha = 1f;
                            if (dx < cornerRadius && dy < cornerRadius)
                            {
                                float dist = Vector2.Distance(new Vector2(dx, dy), new Vector2(cornerRadius, cornerRadius));
                                alpha = Mathf.Clamp01(cornerRadius - dist + 1f);
                            }

                            pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                        }
                    }
                    tex.SetPixels(pixels);
                    tex.Apply();

                    // 9-slice border with 28px margins for ultra-clean scaling
                    Vector4 border = new Vector4(28, 28, 28, 28);
                    _roundedBox = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
                }
                return _roundedBox;
            }
        }

        public static Sprite PanelWithBorder
        {
            get
            {
                if (_panelBorder == null)
                {
                    const int size = 128;
                    const float cornerRadius = 20f;
                    const float borderWidth = 2.5f;
                    Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                    tex.filterMode = FilterMode.Bilinear;

                    Color[] pixels = new Color[size * size];
                    for (int y = 0; y < size; y++)
                    {
                        for (int x = 0; x < size; x++)
                        {
                            float dx = Mathf.Min(x, size - 1 - x);
                            float dy = Mathf.Min(y, size - 1 - y);

                            float alpha = 1f;
                            if (dx < cornerRadius && dy < cornerRadius)
                            {
                                float dist = Vector2.Distance(new Vector2(dx, dy), new Vector2(cornerRadius, cornerRadius));
                                alpha = Mathf.Clamp01(cornerRadius - dist + 1f);
                            }

                            // Brighten border edge
                            bool isBorder = dx < borderWidth || dy < borderWidth;
                            if (dx < cornerRadius && dy < cornerRadius)
                            {
                                float dist = Vector2.Distance(new Vector2(dx, dy), new Vector2(cornerRadius, cornerRadius));
                                isBorder = dist >= (cornerRadius - borderWidth);
                            }

                            float val = isBorder ? 1.0f : 0.82f;
                            pixels[y * size + x] = new Color(val, val, val, alpha);
                        }
                    }
                    tex.SetPixels(pixels);
                    tex.Apply();

                    Vector4 border = new Vector4(24, 24, 24, 24);
                    _panelBorder = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
                }
                return _panelBorder;
            }
        }

        public static Sprite SoundWave
        {
            get
            {
                if (_soundWave == null)
                {
                    const int width = 128;
                    const int height = 96;
                    Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    tex.filterMode = FilterMode.Bilinear;

                    Color[] pixels = new Color[width * height];
                    for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

                    // 4 smooth vertical wave bars
                    float[] barHeights = { 32f, 64f, 84f, 48f };
                    float[] barX = { 24f, 48f, 72f, 96f };
                    float barRadius = 6f;

                    for (int b = 0; b < barHeights.Length; b++)
                    {
                        float cx = barX[b];
                        float bh = barHeights[b];
                        float minY = (height - bh) * 0.5f + barRadius;
                        float maxY = minY + bh - 2f * barRadius;

                        for (int y = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                float clampedY = Mathf.Clamp(y, minY, maxY);
                                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, clampedY));
                                float alpha = Mathf.Clamp01(barRadius - dist + 1f);
                                if (alpha > 0f)
                                {
                                    int idx = y * width + x;
                                    pixels[idx] = new Color(1f, 1f, 1f, Mathf.Max(pixels[idx].a, alpha));
                                }
                            }
                        }
                    }

                    tex.SetPixels(pixels);
                    tex.Apply();
                    _soundWave = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
                }
                return _soundWave;
            }
        }
    }
}
