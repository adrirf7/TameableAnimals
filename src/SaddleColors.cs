using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace TameableAnimals
{
    /// <summary>
    /// Variantes de color de las monturas. El juego ya muestra el selector de estilo al fabricar si el objeto tiene
    /// m_variants > 1 y un icono por variante; aquí se generan esos iconos y las texturas recoloreadas.
    /// El recoloreado solo cambia los píxeles de "tela" (por tono, saturación y brillo); el cuero y el metal no cambian.
    /// </summary>
    internal static class SaddleColors
    {
        // Paleta pedida: azul, rojo, verde, negro, blanco, amarillo y naranja.
        private static readonly Color[] s_palette =
        {
            new Color(0.16f, 0.32f, 0.72f), // azul
            new Color(0.70f, 0.12f, 0.10f), // rojo
            new Color(0.18f, 0.50f, 0.16f), // verde
            new Color(0.10f, 0.10f, 0.11f), // negro
            new Color(0.92f, 0.91f, 0.88f), // blanco
            new Color(0.88f, 0.72f, 0.15f), // amarillo
            new Color(0.90f, 0.42f, 0.10f), // naranja
        };
        private const int Blue = 0, Red = 1;

        internal class Spec
        {
            public float Hue, HueTolerance, MinSaturation, MinValue;
            public int OriginalColor; // índice en la paleta del color original, o -1 si no está en la paleta
            public Color? BaseColor;  // si tiene, la variante 0 también se recolorea (montura de oso: marrón)
            public bool PawBadge;     // huella de oso en el icono, para distinguirla de la del lox
            public bool Reins;        // recolorear también las riendas y la brida (zonas de la textura del lox)

            /// <summary>Variante 0 = color original (o BaseColor); el resto, la paleta sin el color original.</summary>
            public List<Color> Variants()
            {
                var list = new List<Color> { BaseColor ?? Color.clear };
                for (int i = 0; i < s_palette.Length; i++)
                {
                    if (i != OriginalColor)
                    {
                        list.Add(s_palette[i]);
                    }
                }
                return list;
            }
        }

        // Por prefab del objeto montura. Valores ajustados con las texturas reales del juego.
        private static readonly Dictionary<string, Spec> s_specs = new Dictionary<string, Spec>
        {
            ["SaddleLox"] = new Spec { Hue = 0f, HueTolerance = 0.035f, MinSaturation = 0.40f, MinValue = 0.50f, OriginalColor = Red, Reins = true },
            // Misma textura que el lox, pero con su propio color base (marrón cuero) y huella en el icono.
            [BearSaddle.ItemPrefabName] = new Spec { Hue = 0f, HueTolerance = 0.035f, MinSaturation = 0.40f, MinValue = 0.50f, OriginalColor = -1,
                BaseColor = new Color(0.42f, 0.27f, 0.15f), PawBadge = true },
            ["SaddleMoose"] = new Spec { Hue = 0.56f, HueTolerance = 0.08f, MinSaturation = 0.25f, MinValue = 0f, OriginalColor = Blue, Reins = true },
            ["SaddleAsksvin"] = new Spec { Hue = 0.52f, HueTolerance = 0.07f, MinSaturation = 0.25f, MinValue = 0f, OriginalColor = -1, Reins = true },
        };

        private static readonly HashSet<ItemDrop.ItemData.SharedData> s_applied = new HashSet<ItemDrop.ItemData.SharedData>();
        private static readonly Dictionary<Texture, Texture2D> s_readable = new Dictionary<Texture, Texture2D>();
        private static readonly Dictionary<string, Material> s_materials = new Dictionary<string, Material>();

        internal static bool Enabled => TameableAnimalsPlugin.SaddleColorsEnabled.Value;

        private static bool HasGraphics => SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;

        internal static Spec GetSpec(string itemPrefabName)
        {
            return itemPrefabName != null && s_specs.TryGetValue(itemPrefabName, out Spec spec) ? spec : null;
        }

        // ------------------------------------------------------------ objetos

        /// <summary>Da a cada montura sus variantes y un icono recoloreado por variante.</summary>
        internal static void ApplyToItems(ObjectDB db)
        {
            if (!Enabled)
            {
                return;
            }
            foreach (KeyValuePair<string, Spec> entry in s_specs)
            {
                GameObject prefab = db.GetItemPrefab(entry.Key);
                ItemDrop item = prefab ? prefab.GetComponent<ItemDrop>() : null;
                if (item == null)
                {
                    continue;
                }
                ItemDrop.ItemData.SharedData shared = item.m_itemData.m_shared;
                if (s_applied.Contains(shared) || shared.m_icons == null || shared.m_icons.Length == 0)
                {
                    continue;
                }

                Spec spec = entry.Value;
                List<Color> variants = spec.Variants();
                Sprite baseIcon = shared.m_icons[0];
                var icons = new Sprite[variants.Count];
                for (int v = 0; v < variants.Count; v++)
                {
                    bool recolor = v > 0 || spec.BaseColor != null;
                    icons[v] = HasGraphics && (recolor || spec.PawBadge) ? RecolorSprite(baseIcon, spec, recolor ? variants[v] : (Color?)null) : baseIcon;
                }
                shared.m_icons = icons;
                shared.m_variants = variants.Count;
                s_applied.Add(shared);
            }
        }

        // --------------------------------------------------------- materiales

        /// <summary>Materiales recoloreados para la variante indicada (los originales si es 0 o no hay gráficos).</summary>
        internal static Material[] GetMaterials(string itemPrefabName, Material[] originals, int variant)
        {
            Spec spec = GetSpec(itemPrefabName);
            if (variant < 0 || spec == null || !HasGraphics || (variant == 0 && spec.BaseColor == null))
            {
                return originals;
            }
            List<Color> variants = spec.Variants();
            if (variant >= variants.Count)
            {
                return originals;
            }

            var result = new Material[originals.Length];
            for (int i = 0; i < originals.Length; i++)
            {
                Material original = originals[i];
                if (original == null || !original.HasProperty("_MainTex") || original.mainTexture == null)
                {
                    result[i] = original;
                    continue;
                }
                string key = itemPrefabName + "|" + original.GetInstanceID() + "|" + variant;
                if (!s_materials.TryGetValue(key, out Material material))
                {
                    material = new Material(original) { name = original.name + "_TA_v" + variant };
                    material.mainTexture = RecolorTexture(original.mainTexture, spec, variants[variant], itemPrefabName);
                    s_materials[key] = material;
                }
                result[i] = material;
            }
            return result;
        }

        internal static void ApplyToRenderers(GameObject root, string itemPrefabName, int variant)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                {
                    renderer.sharedMaterials = GetMaterials(itemPrefabName, renderer.sharedMaterials, variant);
                }
            }
        }

        // --------------------------------------------------------- recoloreado

        private static Texture2D RecolorTexture(Texture source, Spec spec, Color target, string itemPrefabName)
        {
            Texture2D readable = GetReadable(source);
            Texture2D result = new Texture2D(readable.width, readable.height, TextureFormat.RGBA32, true, false)
            {
                name = source.name + "_TA",
                wrapMode = source.wrapMode,
                filterMode = source.filterMode,
                anisoLevel = source.anisoLevel
            };
            Color[] pixels = Recolor(readable.GetPixels(), spec, target, 1f);
            if (spec.Reins)
            {
                bool[] reinMask = itemPrefabName == "SaddleLox" ? GetLoxReinMask(readable.width, readable.height) : ReinMasks.Get(itemPrefabName, readable.width, readable.height);
                if (reinMask != null)
                {
                    RecolorReins(readable.GetPixels(), pixels, reinMask, target);
                }
            }
            result.SetPixels(pixels);
            result.Apply(true);
            return result;
        }

        /// <summary>Icono recoloreado (target null = sin cambiar el color), con la huella de oso si la montura la lleva.</summary>
        private static Sprite RecolorSprite(Sprite source, Spec spec, Color? target)
        {
            if (source == null)
            {
                return null;
            }
            Texture2D atlas = GetReadable(source.texture);
            Rect rect = source.textureRect;
            int x = Mathf.RoundToInt(rect.x), y = Mathf.RoundToInt(rect.y);
            int w = Mathf.RoundToInt(rect.width), h = Mathf.RoundToInt(rect.height);
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false, false) { name = source.name + "_TA" };
            Color[] pixels = atlas.GetPixels(x, y, w, h);
            if (target != null)
            {
                // Los iconos están más iluminados que la textura: umbral de brillo más suave.
                pixels = Recolor(pixels, spec, target.Value, 0.7f);
            }
            if (spec.PawBadge)
            {
                DrawPawBadge(pixels, w, h);
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
        }

        /// <summary>Huella de oso (almohadilla + 4 dedos) en la esquina inferior derecha del icono.</summary>
        private static void DrawPawBadge(Color[] pixels, int w, int h)
        {
            float s = Mathf.Min(w, h);
            var cx = 0.78f * w;
            var cy = 0.24f * h;
            // (centro x, centro y, radio x, radio y) relativos al tamaño del icono
            var shapes = new List<Vector4>
            {
                new Vector4(cx, cy, 0.10f * s, 0.085f * s),
                new Vector4(cx - 0.105f * s, cy + 0.095f * s, 0.038f * s, 0.045f * s),
                new Vector4(cx - 0.038f * s, cy + 0.14f * s, 0.038f * s, 0.048f * s),
                new Vector4(cx + 0.038f * s, cy + 0.14f * s, 0.038f * s, 0.048f * s),
                new Vector4(cx + 0.105f * s, cy + 0.095f * s, 0.038f * s, 0.045f * s),
            };
            var outline = new Color(0.97f, 0.92f, 0.82f, 1f);
            var fill = new Color(0.20f, 0.12f, 0.07f, 1f);
            float border = Mathf.Max(1.5f, 0.022f * s);
            for (int py = 0; py < h; py++)
            {
                for (int px = 0; px < w; px++)
                {
                    float best = float.MaxValue; // distancia normalizada mínima (<1 = dentro)
                    float bestOutline = float.MaxValue;
                    foreach (Vector4 e in shapes)
                    {
                        float dx = (px + 0.5f - e.x), dy = (py + 0.5f - e.y);
                        best = Mathf.Min(best, Mathf.Sqrt(dx * dx / (e.z * e.z) + dy * dy / (e.w * e.w)));
                        float ox = e.z + border, oy = e.w + border;
                        bestOutline = Mathf.Min(bestOutline, Mathf.Sqrt(dx * dx / (ox * ox) + dy * dy / (oy * oy)));
                    }
                    int i = py * w + px;
                    if (best <= 1f)
                    {
                        pixels[i] = fill;
                    }
                    else if (bestOutline <= 1f)
                    {
                        pixels[i] = outline;
                    }
                }
            }
        }

        /// <summary>Copia legible (CPU) de una textura del juego, que normalmente no lo es.</summary>
        private static Texture2D GetReadable(Texture source)
        {
            if (s_readable.TryGetValue(source, out Texture2D cached))
            {
                return cached;
            }
            RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(source, rt);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, false);
            copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            copy.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            s_readable[source] = copy;
            return copy;
        }

        private static float ClothMask(Color c, Spec spec, float minValueScale)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            float minValue = spec.MinValue * minValueScale;
            if (s < spec.MinSaturation * 0.6f || v < minValue * 0.8f)
            {
                return 0f;
            }
            float dh = Mathf.Abs(h - spec.Hue);
            dh = Mathf.Min(dh, 1f - dh);
            if (dh > spec.HueTolerance * 1.5f)
            {
                return 0f;
            }
            float mask = 1f;
            if (dh > spec.HueTolerance)
            {
                mask *= 1f - (dh - spec.HueTolerance) / (spec.HueTolerance * 0.5f);
            }
            if (s < spec.MinSaturation)
            {
                mask *= (s - spec.MinSaturation * 0.6f) / (spec.MinSaturation * 0.4f);
            }
            if (minValue > 0f && v < minValue)
            {
                mask *= (v - minValue * 0.8f) / (minValue * 0.2f);
            }
            return Mathf.Clamp01(mask);
        }

        // ------------------------------------------------------ riendas del lox

        private static bool[] s_reinMask;
        private static int s_reinMaskWidth, s_reinMaskHeight;
        private static bool s_reinMaskFailed;

        /// <summary>
        /// Píxeles de la textura que usan las riendas y la brida del lox: los triángulos de su montura que no mueve el
        /// hueso Mount (el asiento). Se calcula con la malla real, que es legible.
        /// </summary>
        private static bool[] GetLoxReinMask(int width, int height)
        {
            if (s_reinMask != null && s_reinMaskWidth == width && s_reinMaskHeight == height)
            {
                return s_reinMask;
            }
            if (s_reinMaskFailed || ZNetScene.instance == null)
            {
                return null;
            }
            GameObject lox = ZNetScene.instance.GetPrefab("Lox");
            Tameable tameable = lox ? lox.GetComponent<Tameable>() : null;
            SkinnedMeshRenderer skinned = tameable != null && tameable.m_saddle != null ? tameable.m_saddle.GetComponent<SkinnedMeshRenderer>() : null;
            Mesh mesh = skinned != null ? skinned.sharedMesh : null;
            if (mesh == null || !mesh.isReadable)
            {
                s_reinMaskFailed = true;
                TameableAnimalsPlugin.Log.LogWarning("Saddle colors: lox saddle mesh not readable, reins keep their colour.");
                return null;
            }

            Transform[] bones = skinned.bones;
            BoneWeight[] weights = mesh.boneWeights;
            Vector2[] uv = mesh.uv;
            int[] triangles = mesh.triangles;
            var isRein = new bool[uv.Length];
            for (int i = 0; i < uv.Length && i < weights.Length; i++)
            {
                BoneWeight w = weights[i];
                int dominant = w.boneIndex0;
                float best = w.weight0;
                if (w.weight1 > best) { best = w.weight1; dominant = w.boneIndex1; }
                if (w.weight2 > best) { best = w.weight2; dominant = w.boneIndex2; }
                if (w.weight3 > best) { dominant = w.boneIndex3; }
                string bone = dominant < bones.Length && bones[dominant] != null ? bones[dominant].name : "";
                isRein[i] = bone != "Mount" && bone != "Mount.001";
            }

            var mask = new bool[width * height];
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                if (isRein[a] || isRein[b] || isRein[c])
                {
                    FillTriangle(mask, width, height, uv[a], uv[b], uv[c]);
                }
            }
            s_reinMask = mask;
            s_reinMaskWidth = width;
            s_reinMaskHeight = height;
            return mask;
        }

        private static void FillTriangle(bool[] mask, int width, int height, Vector2 a, Vector2 b, Vector2 c)
        {
            Vector2 pa = new Vector2(a.x * width, a.y * height), pb = new Vector2(b.x * width, b.y * height), pc = new Vector2(c.x * width, c.y * height);
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pa.x, Mathf.Min(pb.x, pc.x))) - 1);
            int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(Mathf.Max(pa.x, Mathf.Max(pb.x, pc.x))) + 1);
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pa.y, Mathf.Min(pb.y, pc.y))) - 1);
            int maxY = Mathf.Min(height - 1, Mathf.CeilToInt(Mathf.Max(pa.y, Mathf.Max(pb.y, pc.y))) + 1);
            float area = Edge(pa, pb, pc);
            if (Mathf.Abs(area) < 1e-6f)
            {
                return;
            }
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    // Se prueba el píxel y un margen de medio píxel, para no dejar bordes sin pintar.
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float w0 = Edge(pb, pc, p) / area, w1 = Edge(pc, pa, p) / area, w2 = Edge(pa, pb, p) / area;
                    if (w0 >= -0.02f && w1 >= -0.02f && w2 >= -0.02f)
                    {
                        mask[y * width + x] = true;
                    }
                }
            }
        }

        private static float Edge(Vector2 a, Vector2 b, Vector2 p)
        {
            return (p.x - a.x) * (b.y - a.y) - (p.y - a.y) * (b.x - a.x);
        }

        /// <summary>
        /// Tiñe las riendas (cuero) del color elegido, algo más oscuro que la tela, conservando su sombreado.
        /// El metal (gris, sin saturación) no se toca.
        /// </summary>
        private static void RecolorReins(Color[] original, Color[] result, bool[] mask, Color target)
        {
            float total = 0f;
            int count = 0;
            for (int i = 0; i < original.Length; i++)
            {
                if (!mask[i])
                {
                    continue;
                }
                Color.RGBToHSV(original[i], out _, out float s, out float v);
                if (s >= 0.15f)
                {
                    total += v;
                    count++;
                }
            }
            if (count == 0)
            {
                return;
            }
            float meanValue = total / count;
            Color.RGBToHSV(target, out float th, out float ts, out float tv);
            for (int i = 0; i < original.Length; i++)
            {
                if (!mask[i])
                {
                    continue;
                }
                Color c = original[i];
                Color.RGBToHSV(c, out _, out float s, out float v);
                if (s < 0.15f)
                {
                    continue; // metal
                }
                float m = Mathf.Clamp01((s - 0.15f) / 0.1f);
                Color dyed = Color.HSVToRGB(th, ts, Mathf.Clamp01(tv * 0.8f * (v / meanValue)));
                Color mixed = Color.Lerp(c, dyed, m);
                mixed.a = c.a;
                result[i] = mixed;
            }
        }

        private static Color[] Recolor(Color[] pixels, Spec spec, Color target, float minValueScale)
        {
            // Brillo medio de la tela, para conservar el sombreado relativo al cambiar de color.
            float total = 0f;
            int count = 0;
            var masks = new float[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                masks[i] = ClothMask(pixels[i], spec, minValueScale);
                if (masks[i] > 0.5f)
                {
                    Color.RGBToHSV(pixels[i], out _, out _, out float v);
                    total += v;
                    count++;
                }
            }
            float meanValue = count > 0 ? total / count : 1f;
            Color.RGBToHSV(target, out float th, out float ts, out float tv);

            var result = new Color[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i];
                float m = masks[i];
                if (m <= 0f)
                {
                    result[i] = c;
                    continue;
                }
                Color.RGBToHSV(c, out _, out _, out float v);
                Color recolored = Color.HSVToRGB(th, ts, Mathf.Clamp01(tv * (v / meanValue)));
                Color mixed = Color.Lerp(c, recolored, m);
                mixed.a = c.a; // el alfa de estas texturas no es transparencia: no se toca
                result[i] = mixed;
            }
            return result;
        }
    }

    /// <summary>
    /// Guarda en el animal el color de la montura que se le pone y lo aplica a su modelo en todos los clientes.
    /// </summary>
    public class SaddleColor : MonoBehaviour
    {
        private static readonly int s_variantKey = "ta_saddle_variant".GetStableHashCode();

        private ZNetView m_nview;
        private Tameable m_tameable;
        private Renderer m_renderer;
        private Material[] m_originalMaterials;
        private string m_itemPrefabName;
        private int m_appliedVariant = -1; // -1: aún sin aplicar (la de oso se recolorea también en la variante 0)
        private float m_checkTimer;

        private void Awake()
        {
            m_nview = GetComponent<ZNetView>();
            m_tameable = GetComponent<Tameable>();
            m_itemPrefabName = m_tameable != null && m_tameable.m_saddleItem != null ? m_tameable.m_saddleItem.name : null;
            if (m_nview != null && m_nview.IsValid())
            {
                m_nview.Register<int>("TA_SaddleVariant", RPC_SetVariant);
            }
        }

        internal int GetVariant()
        {
            return m_nview != null && m_nview.IsValid() ? m_nview.GetZDO().GetInt(s_variantKey, 0) : 0;
        }

        internal void SendVariant(int variant)
        {
            if (m_nview != null && m_nview.IsValid())
            {
                m_nview.InvokeRPC("TA_SaddleVariant", variant);
            }
        }

        private void RPC_SetVariant(long sender, int variant)
        {
            if (m_nview.IsOwner())
            {
                m_nview.GetZDO().Set(s_variantKey, variant);
            }
        }

        private void Update()
        {
            m_checkTimer -= Time.deltaTime;
            if (m_checkTimer > 0f || m_nview == null || !m_nview.IsValid())
            {
                return;
            }
            // La montura del oso se crea en Tameable.Awake, que puede ir después de este componente.
            if (m_renderer == null)
            {
                if (m_tameable == null || m_tameable.m_saddle == null)
                {
                    return;
                }
                m_renderer = m_tameable.m_saddle.GetComponent<Renderer>();
                if (m_renderer == null)
                {
                    return;
                }
                m_originalMaterials = m_renderer.sharedMaterials;
            }
            m_checkTimer = 0.5f;
            int variant = GetVariant();
            if (variant != m_appliedVariant)
            {
                m_appliedVariant = variant;
                m_renderer.sharedMaterials = SaddleColors.GetMaterials(m_itemPrefabName, m_originalMaterials, variant);
            }
        }
    }

    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
    internal static class ObjectDB_Awake_Patch
    {
        private static void Postfix(ObjectDB __instance)
        {
            BearSaddle.RegisterInObjectDB(__instance);
            SaddleColors.ApplyToItems(__instance);
        }
    }

    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
    internal static class ObjectDB_CopyOtherDB_Patch
    {
        private static void Postfix(ObjectDB __instance)
        {
            BearSaddle.RegisterInObjectDB(__instance);
            SaddleColors.ApplyToItems(__instance);
        }
    }

    // Al poner la montura, se envía su color al animal (antes de que vanilla la añada y la quite del inventario).
    [HarmonyPatch(typeof(Tameable), nameof(Tameable.UseItem))]
    internal static class Tameable_UseItem_Patch
    {
        private static void Prefix(Tameable __instance, ItemDrop.ItemData item)
        {
            if (__instance.m_saddleItem != null && item != null && __instance.IsTamed() && !__instance.HaveSaddle()
                && item.m_shared.m_name == __instance.m_saddleItem.m_itemData.m_shared.m_name)
            {
                __instance.GetComponent<SaddleColor>()?.SendVariant(item.m_variant);
            }
        }
    }

    // Al quitar la montura (o al morir el animal), el objeto que cae conserva su color.
    [HarmonyPatch(typeof(Tameable), nameof(Tameable.SpawnSaddle))]
    internal static class Tameable_SpawnSaddle_Patch
    {
        private static bool Prefix(Tameable __instance, Vector3 flyDirection)
        {
            SaddleColor saddleColor = __instance.GetComponent<SaddleColor>();
            if (saddleColor == null)
            {
                return true;
            }
            GameObject drop = Object.Instantiate(__instance.m_saddleItem.gameObject,
                __instance.transform.TransformPoint(__instance.m_dropSaddleOffset), Quaternion.identity);
            ItemDrop itemDrop = drop.GetComponent<ItemDrop>();
            if (itemDrop != null)
            {
                itemDrop.m_itemData.m_variant = saddleColor.GetVariant();
            }
            Rigidbody body = drop.GetComponent<Rigidbody>();
            if (body != null)
            {
                Vector3 up = Vector3.up;
                if (flyDirection.magnitude > 0.1f)
                {
                    flyDirection.y = 0f;
                    flyDirection.Normalize();
                    up += flyDirection;
                }
                body.AddForce(up * __instance.m_dropItemVel, ForceMode.VelocityChange);
            }
            return false;
        }
    }

    // Las monturas tiradas en el suelo se ven con su color.
    [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.Start))]
    internal static class ItemDrop_Start_Patch
    {
        private static void Postfix(ItemDrop __instance)
        {
            if (!SaddleColors.Enabled)
            {
                return;
            }
            int variant = __instance.m_itemData.m_variant;
            string prefabName = Utils.GetPrefabName(__instance.gameObject);
            SaddleColors.Spec spec = SaddleColors.GetSpec(prefabName);
            if (spec != null && (variant > 0 || spec.BaseColor != null))
            {
                SaddleColors.ApplyToRenderers(__instance.gameObject, prefabName, variant);
            }
        }
    }
}
