using System;
using UnityEngine;

/// <summary>
/// Fábrica de sprites desenhados em código (triângulo, tecla arredondada, disco e anel),
/// usada pela camada visual do tutorial.
///
/// Por que em código: a UI do tutorial é montada em runtime (nenhuma sessão é dona de prefab
/// aqui), então os ícones não podem depender de assets importados. Os sprites são gerados uma
/// única vez, ficam em cache estático e são marcados com <see cref="HideFlags.HideAndDontSave"/>
/// para sobreviverem a trocas de cena (senão o Unity os descarregaria junto com a cena antiga e
/// os ícones ficariam invisíveis na segunda vez que o tutorial abrisse).
/// </summary>
public static class TutorialUIShapes
{
    private const int ShapeResolution = 192;
    private const float PixelsPerUnit = 100f;
    private const int SubSamples = 3;

    private static Sprite pixelSprite;
    private static Sprite triangleSprite;
    private static Sprite roundedRectSprite;
    private static Sprite circleSprite;
    private static Sprite ringSprite;

    /// <summary>Sprite branco de 1x1, para painéis de cor cheia (escurecimento da tela).</summary>
    public static Sprite Pixel
    {
        get
        {
            if (pixelSprite == null)
            {
                pixelSprite = Build("TutorialShape_Pixel", 4, 4, (x, y, w, h) => true, Vector4.zero);
            }

            return pixelSprite;
        }
    }

    /// <summary>Triângulo apontando para cima. As outras direções são a rotação do RectTransform.</summary>
    public static Sprite Triangle
    {
        get
        {
            if (triangleSprite == null)
            {
                triangleSprite = Build("TutorialShape_Triangle", ShapeResolution, ShapeResolution, IsInsideTriangle, Vector4.zero);
            }

            return triangleSprite;
        }
    }

    /// <summary>Retângulo arredondado com bordas 9-slice: a "capa" da tecla, esticável sem distorcer o canto.</summary>
    public static Sprite RoundedRect
    {
        get
        {
            if (roundedRectSprite == null)
            {
                float radius = ShapeResolution * 0.28f;
                float border = radius + 4f;

                roundedRectSprite = Build(
                    "TutorialShape_RoundedRect",
                    ShapeResolution,
                    ShapeResolution,
                    (x, y, w, h) => IsInsideRoundedRect(x, y, w, h, radius),
                    new Vector4(border, border, border, border));
            }

            return roundedRectSprite;
        }
    }

    /// <summary>Disco cheio: pontos de progresso e base do destaque no mundo.</summary>
    public static Sprite Circle
    {
        get
        {
            if (circleSprite == null)
            {
                circleSprite = Build("TutorialShape_Circle", ShapeResolution, ShapeResolution, IsInsideCircle, Vector4.zero);
            }

            return circleSprite;
        }
    }

    /// <summary>Anel: halo do alvo e onda da comemoração.</summary>
    public static Sprite Ring
    {
        get
        {
            if (ringSprite == null)
            {
                ringSprite = Build("TutorialShape_Ring", ShapeResolution, ShapeResolution, IsInsideRing, Vector4.zero);
            }

            return ringSprite;
        }
    }

    private static bool IsInsideTriangle(float x, float y, float w, float h)
    {
        Vector2 apex = new Vector2(w * 0.5f, h * 0.96f);
        Vector2 left = new Vector2(w * 0.04f, h * 0.08f);
        Vector2 right = new Vector2(w * 0.96f, h * 0.08f);
        Vector2 point = new Vector2(x, y);

        float side1 = Cross(left, apex, point);
        float side2 = Cross(apex, right, point);
        float side3 = Cross(right, left, point);

        return (side1 >= 0f && side2 >= 0f && side3 >= 0f) || (side1 <= 0f && side2 <= 0f && side3 <= 0f);
    }

    private static bool IsInsideRoundedRect(float x, float y, float w, float h, float radius)
    {
        float halfWidth = w * 0.5f;
        float halfHeight = h * 0.5f;
        radius = Mathf.Min(radius, Mathf.Min(halfWidth, halfHeight));

        float dx = Mathf.Abs(x - halfWidth) - (halfWidth - radius);
        float dy = Mathf.Abs(y - halfHeight) - (halfHeight - radius);

        float outsideX = Mathf.Max(dx, 0f);
        float outsideY = Mathf.Max(dy, 0f);

        return Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY) <= radius;
    }

    private static bool IsInsideCircle(float x, float y, float w, float h)
    {
        float radius = Mathf.Min(w, h) * 0.5f - 1f;
        Vector2 offset = new Vector2(x - w * 0.5f, y - h * 0.5f);

        return offset.magnitude <= radius;
    }

    private static bool IsInsideRing(float x, float y, float w, float h)
    {
        float outer = Mathf.Min(w, h) * 0.5f - 1f;
        float inner = outer * 0.74f;
        float distance = new Vector2(x - w * 0.5f, y - h * 0.5f).magnitude;

        return distance <= outer && distance >= inner;
    }

    private static float Cross(Vector2 a, Vector2 b, Vector2 point)
    {
        return (b.x - a.x) * (point.y - a.y) - (b.y - a.y) * (point.x - a.x);
    }

    /// <summary>
    /// Rasteriza a forma com supersampling (3x3 por pixel) e devolve o sprite. O supersampling é
    /// o que deixa a borda do triângulo suave: sem ele o ícone fica serrilhado nos tamanhos
    /// grandes que esse público precisa.
    /// </summary>
    private static Sprite Build(string name, int width, int height, Func<float, float, float, float, bool> isInside, Vector4 border)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color32[] pixels = new Color32[width * height];
        float step = 1f / (SubSamples + 1);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int hits = 0;

                for (int sy = 1; sy <= SubSamples; sy++)
                {
                    for (int sx = 1; sx <= SubSamples; sx++)
                    {
                        if (isInside(x + sx * step, y + sy * step, width, height))
                        {
                            hits++;
                        }
                    }
                }

                byte alpha = (byte)Mathf.RoundToInt(255f * hits / (SubSamples * SubSamples));
                pixels[y * width + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            PixelsPerUnit,
            0,
            SpriteMeshType.FullRect,
            border);

        sprite.name = name;
        sprite.hideFlags = HideFlags.HideAndDontSave;

        return sprite;
    }
}
