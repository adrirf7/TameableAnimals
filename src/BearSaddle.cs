using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using UnityEngine;

namespace TameableAnimals
{
    /// <summary>
    /// Montura de oso: un objeto nuevo (clon de la montura del lox) con su receta, y la montura del lox
    /// colocada en el lomo del Bjorn. La del lox es una malla con esqueleto propio, así que en el oso se
    /// convierte en una pieza rígida pegada a su cuerpo y se ajusta automáticamente a su tamaño.
    /// </summary>
    internal static class BearSaddle
    {
        internal const string ItemPrefabName = "SaddleBjorn";
        private const string SaddleObjectName = "TA_BearSaddle";
        private const string AttachObjectName = "TA_BearSaddleAttach";

        private static GameObject s_itemPrefab;

        // Ajuste automático calculado sobre el prefab del oso (antes de aplicar las opciones del config).
        private static Vector3 s_basePosition;
        private static Vector3 s_baseScale;
        private static Vector3 s_baseAttach;
        private static bool s_haveLayout;

        internal static bool Enabled => TameableAnimalsPlugin.BearSaddleEnabled.Value;

        // ------------------------------------------------------------- objeto

        internal static GameObject GetOrCreateItem(GameObject loxSaddleItem)
        {
            if (s_itemPrefab != null || loxSaddleItem == null)
            {
                return s_itemPrefab;
            }
            GameObject item = Object.Instantiate(loxSaddleItem, CreatureSetup.GetPrefabHolder().transform, false);
            item.name = ItemPrefabName;
            ItemDrop.ItemData.SharedData shared = item.GetComponent<ItemDrop>().m_itemData.m_shared;
            shared.m_name = "$ta_item_saddlebjorn";
            shared.m_description = "$ta_item_saddlebjorn_desc";
            s_itemPrefab = item;
            return item;
        }

        /// <summary>El objeto montura es un prefab de red: tiene que estar en ZNetScene en cada carga.</summary>
        internal static void EnsureItemRegistered(List<GameObject> prefabs)
        {
            if (Enabled && s_itemPrefab != null && !prefabs.Contains(s_itemPrefab))
            {
                prefabs.Add(s_itemPrefab);
            }
        }

        internal static void RegisterInObjectDB(ObjectDB db)
        {
            if (!Enabled)
            {
                return;
            }
            GameObject item = GetOrCreateItem(db.GetItemPrefab("SaddleLox"));
            if (item == null)
            {
                return;
            }
            if (!db.m_items.Contains(item))
            {
                db.m_items.Add(item);
            }
            AddRecipe(db, item.GetComponent<ItemDrop>());
            db.UpdateRegisters();
        }

        private static void AddRecipe(ObjectDB db, ItemDrop item)
        {
            foreach (Recipe existing in db.m_recipes)
            {
                if (existing != null && existing.m_item == item)
                {
                    return;
                }
            }

            string stationName = TameableAnimalsPlugin.BearSaddleStation.Value.Trim();
            CraftingStation station = null;
            foreach (Recipe other in db.m_recipes)
            {
                if (other != null && other.m_craftingStation != null && other.m_craftingStation.name == stationName)
                {
                    station = other.m_craftingStation;
                    break;
                }
            }
            if (station == null)
            {
                // Sin recetas cargadas aún (p. ej. ObjectDB vacío): se reintentará en la siguiente carga.
                return;
            }

            var requirements = new List<Piece.Requirement>();
            foreach (string entry in (TameableAnimalsPlugin.BearSaddleRecipe.Value ?? "").Split(','))
            {
                string[] parts = entry.Split(':');
                if (parts.Length != 2 || !int.TryParse(parts[1].Trim(), out int amount))
                {
                    continue;
                }
                GameObject resource = db.GetItemPrefab(parts[0].Trim());
                ItemDrop resourceItem = resource ? resource.GetComponent<ItemDrop>() : null;
                if (resourceItem == null)
                {
                    TameableAnimalsPlugin.Log.LogWarning("Unknown bear saddle ingredient: " + parts[0].Trim());
                    continue;
                }
                requirements.Add(new Piece.Requirement { m_resItem = resourceItem, m_amount = amount, m_amountPerLevel = 0, m_recover = true });
            }

            Recipe recipe = ScriptableObject.CreateInstance<Recipe>();
            recipe.name = "Recipe_" + ItemPrefabName;
            recipe.m_item = item;
            recipe.m_amount = 1;
            recipe.m_craftingStation = station;
            recipe.m_repairStation = station;
            recipe.m_minStationLevel = Mathf.Max(1, TameableAnimalsPlugin.BearSaddleStationLevel.Value);
            recipe.m_resources = requirements.ToArray();
            db.m_recipes.Add(recipe);
        }

        // ------------------------------------------------------------ en el oso

        private const string BoneName = "Spine_01";
        private static Matrix4x4 s_boneRestInVisual;
        private static bool s_haveBone;
        private static Sprite s_mountIcon;

        private static GameObject s_loxSaddleSource;
        private static Mesh s_bakedMesh;      // montura del lox tal como queda puesta en él, centrada en su origen
        private static Vector3 s_colliderCenter; // caja para poder montar, en unidades de la malla de la montura
        private static Vector3 s_colliderSize;

        /// <summary>
        /// Prepara el prefab del Bjorn: le asigna el objeto montura, calcula dónde va y lo marca. La montura en sí se
        /// construye en cada oso al aparecer (Tameable_Awake_Patch): Unity no deja colgar objetos del prefab del juego.
        /// </summary>
        internal static void AttachToBear(GameObject bear, Dictionary<string, GameObject> byName, List<GameObject> prefabs)
        {
            if (!Enabled || bear.GetComponent<BearSaddleMarker>() != null)
            {
                return;
            }
            byName.TryGetValue("Lox", out GameObject lox);
            byName.TryGetValue("SaddleLox", out GameObject loxSaddleItem);
            Tameable loxTameable = lox ? lox.GetComponent<Tameable>() : null;
            Tameable bearTameable = bear.GetComponent<Tameable>();
            GameObject item = GetOrCreateItem(loxSaddleItem);
            if (loxTameable == null || loxTameable.m_saddle == null || bearTameable == null || item == null
                || !ComputeLayout(lox, loxTameable.m_saddle, bear))
            {
                TameableAnimalsPlugin.Log.LogWarning("Could not create the bear saddle.");
                return;
            }
            if (!prefabs.Contains(item))
            {
                prefabs.Add(item);
            }
            s_loxSaddleSource = loxTameable.m_saddle.gameObject;
            // Icono que se muestra al montar: el trofeo del oso (el del lox es su trofeo).
            byName.TryGetValue("TrophyBjorn", out GameObject trophy);
            ItemDrop trophyItem = trophy ? trophy.GetComponent<ItemDrop>() : null;
            s_mountIcon = trophyItem != null && trophyItem.m_itemData.m_shared.m_icons.Length > 0
                ? trophyItem.m_itemData.m_shared.m_icons[0]
                : item.GetComponent<ItemDrop>().m_itemData.m_shared.m_icons[0];
            bearTameable.m_saddleItem = item.GetComponent<ItemDrop>();
            bearTameable.m_dropSaddleOnDeath = true;
            bear.AddComponent<BearSaddleMarker>();
        }

        /// <summary>Construye la montura (desactivada, como en el lox) en un oso concreto, antes de Tameable.Awake.</summary>
        internal static void BuildOnInstance(Tameable bearTameable)
        {
            if (s_loxSaddleSource == null || s_bakedMesh == null || bearTameable.m_saddle != null)
            {
                return;
            }
            Transform visual = bearTameable.transform.Find("Visual") ?? bearTameable.transform;
            // Colgada de un hueso de la columna para que se mueva con el lomo al andar y galopar.
            Transform parent = (s_haveBone ? FindDeep(visual, BoneName) : null) ?? visual;
            // Se copia el objeto de la montura del lox por su componente Sadle (montar, estamina...),
            // pero su malla con esqueleto se sustituye por la versión horneada y rígida.
            GameObject saddle = Object.Instantiate(s_loxSaddleSource, parent, false);
            saddle.name = SaddleObjectName;
            saddle.SetActive(false);

            Material[] materials = null;
            SkinnedMeshRenderer skinned = saddle.GetComponent<SkinnedMeshRenderer>();
            if (skinned != null)
            {
                materials = skinned.sharedMaterials;
                Object.DestroyImmediate(skinned);
            }
            saddle.AddComponent<MeshFilter>().sharedMesh = s_bakedMesh;
            MeshRenderer renderer = saddle.AddComponent<MeshRenderer>();
            if (materials != null)
            {
                renderer.sharedMaterials = materials;
            }

            // Colisionador al que hay que apuntar para que salga "Montar".
            foreach (Collider old in saddle.GetComponents<Collider>())
            {
                Object.DestroyImmediate(old);
            }
            // Caja que cubre el lomo y la parte alta de los costados: sobresale del cuerpo del oso, así que se puede
            // apuntar fácilmente (una esfera en el asiento quedaba casi entera dentro del colisionador del oso).
            BoxCollider collider = saddle.AddComponent<BoxCollider>();
            collider.center = s_colliderCenter;
            collider.size = s_colliderSize;
            collider.isTrigger = true; // solo para apuntar; no cambia cómo choca el oso con el entorno

            // El jinete va en el mismo hueso, para moverse con la montura.
            var attach = new GameObject(AttachObjectName).transform;
            attach.SetParent(parent, false);
            ApplyLayout(saddle.transform, attach);

            Sadle sadle = saddle.GetComponent<Sadle>();
            sadle.m_attachPoint = attach;
            if (s_mountIcon != null)
            {
                sadle.m_mountIcon = s_mountIcon;
            }
            sadle.m_hoverText = "$ta_item_saddlebjorn"; // el del lox decía "Lox saddle"
            bearTameable.m_saddle = sadle;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }
            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, name);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        /// <summary>
        /// Hornea la montura tal como queda puesta en el lox, mide dónde va respecto a su cuerpo (proporción de largo,
        /// altura sobre el lomo y ancho) y traslada esas proporciones al cuerpo del oso.
        /// </summary>
        private static bool ComputeLayout(GameObject lox, Sadle loxSaddle, GameObject bear)
        {
            if (s_haveLayout)
            {
                return true;
            }
            Transform loxVisual = lox.transform.Find("Visual") ?? lox.transform;
            Transform bearVisual = bear.transform.Find("Visual") ?? bear.transform;
            SkinnedMeshRenderer skinned = loxSaddle.GetComponent<SkinnedMeshRenderer>();
            if (skinned == null || skinned.sharedMesh == null)
            {
                return false;
            }

            // Malla de la montura en el espacio del cuerpo del lox, con la forma que le dan sus huesos.
            // Se calcula a mano (skinning lineal): BakeMesh no funciona bien sobre un prefab que no está en la escena.
            // Solo el asiento (lo que mueve el hueso Mount): la brida y las riendas van a la cabeza y el cuello
            // del lox y en el oso quedarían en el aire.
            Mesh baked = SkinToSpace(skinned, loxVisual, new HashSet<string> { "Mount", "Mount.001" });
            if (baked == null)
            {
                return false;
            }
            Vector3[] vertices = baked.vertices;
            Vector3[] normals = baked.normals;
            var saddleBounds = new Bounds(vertices[0], Vector3.zero);
            foreach (Vector3 vertex in vertices)
            {
                saddleBounds.Encapsulate(vertex);
            }

            // Asiento (punto de montar) del lox, en el mismo espacio.
            Vector3 loxAttach = loxSaddle.m_attachPoint != null
                ? loxVisual.InverseTransformPoint(loxSaddle.m_attachPoint.position)
                : new Vector3(saddleBounds.center.x, saddleBounds.max.y, saddleBounds.center.z);

            // Malla final centrada en su origen.
            Vector3 center = saddleBounds.center;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] -= center;
            }
            baked.vertices = vertices;
            if (normals.Length == vertices.Length)
            {
                baked.normals = normals;
            }
            baked.RecalculateBounds();
            baked.name = "TA_BearSaddle_Mesh";
            s_bakedMesh = baked;

            Bounds loxBody = GetBodyBounds(loxVisual, skinned);
            Bounds bearBody = GetBodyBounds(bearVisual, null);

            // Tamaño: misma proporción de ancho que en el lox.
            float widthRatio = saddleBounds.size.x / Mathf.Max(0.01f, loxBody.size.x);
            float scale = bearBody.size.x * widthRatio / Mathf.Max(0.01f, saddleBounds.size.x);

            // Posición: en el lox el asiento va detrás de su joroba; en el oso se centra en el lomo, algo hacia atrás.
            // La caja del cuerpo del oso es algo mayor que el oso real, por eso el asiento se apoya un poco por debajo.
            float height = saddleBounds.size.y * scale;
            float top = bearBody.max.y - 0.05f * bearBody.size.y;
            s_basePosition = new Vector3(0f, top - height * 0.5f, bearBody.center.z - 0.1f * bearBody.size.z);
            s_baseScale = Vector3.one * scale;
            s_baseAttach = s_basePosition + (loxAttach - center) * scale;
            // Caja para montar (en el espacio del cuerpo): todo el ancho del oso, desde encima del asiento hasta
            // media altura del costado, y algo más larga que el asiento. Se pasa a unidades de la malla.
            float boxTop = s_basePosition.y + height * 0.5f + 0.1f;
            float boxBottom = bearBody.max.y - 0.45f * bearBody.size.y;
            var boxCenter = new Vector3(0f, (boxTop + boxBottom) * 0.5f, s_basePosition.z);
            var boxSize = new Vector3(bearBody.size.x * 0.9f, Mathf.Max(0.3f, boxTop - boxBottom), saddleBounds.size.z * scale * 1.2f);
            s_colliderCenter = (boxCenter - s_basePosition) / scale;
            s_colliderSize = boxSize / scale;

            // Pose de reposo del hueso de la columna, para convertir la colocación (en espacio de Visual) a ese hueso.
            Transform bone = FindDeep(bearVisual, BoneName);
            if (bone != null)
            {
                s_boneRestInVisual = bearVisual.worldToLocalMatrix * bone.localToWorldMatrix;
                s_haveBone = true;
            }
            else
            {
                TameableAnimalsPlugin.Log.LogWarning("Bear saddle: bone " + BoneName + " not found; the saddle will not follow the body animation.");
            }
            s_haveLayout = true;

            TameableAnimalsPlugin.Log.LogInfo("Bear saddle layout: loxBody=" + loxBody + " saddle=" + saddleBounds + " bearBody=" + bearBody
                + " scale=" + scale.ToString("0.###") + " position=" + s_basePosition + " attach=" + s_baseAttach);
            return true;
        }

        /// <summary>
        /// Copia de la malla con esqueleto deformada por la pose actual de sus huesos, expresada en el espacio de "space".
        /// Solo se conservan los triángulos cuyos vértices dependen sobre todo de los huesos indicados.
        /// </summary>
        private static Mesh SkinToSpace(SkinnedMeshRenderer skinned, Transform space, HashSet<string> keepBones)
        {
            Mesh source = skinned.sharedMesh;
            if (source == null || !source.isReadable)
            {
                TameableAnimalsPlugin.Log.LogWarning("Bear saddle: the lox saddle mesh is not readable.");
                return null;
            }
            Transform[] bones = skinned.bones;
            Matrix4x4[] bindposes = source.bindposes;
            BoneWeight[] weights = source.boneWeights;
            Vector3[] vertices = source.vertices;
            Vector3[] normals = source.normals;
            Vector4[] tangents = source.tangents;
            if (vertices.Length == 0 || weights.Length != vertices.Length || bones.Length == 0)
            {
                TameableAnimalsPlugin.Log.LogWarning("Bear saddle: unexpected lox saddle mesh (no bone weights).");
                return null;
            }

            // Matriz de cada hueso: de la malla en reposo al espacio destino.
            Matrix4x4 toSpace = space.worldToLocalMatrix;
            var boneMatrices = new Matrix4x4[bones.Length];
            for (int b = 0; b < bones.Length; b++)
            {
                boneMatrices[b] = bones[b] != null && b < bindposes.Length
                    ? toSpace * bones[b].localToWorldMatrix * bindposes[b]
                    : toSpace * skinned.transform.localToWorldMatrix;
            }

            var outVertices = new Vector3[vertices.Length];
            var outNormals = new Vector3[normals.Length];
            var outTangents = new Vector4[tangents.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                BoneWeight w = weights[i];
                Matrix4x4 m = Blend(boneMatrices, w);
                outVertices[i] = m.MultiplyPoint3x4(vertices[i]);
                if (i < normals.Length)
                {
                    outNormals[i] = m.MultiplyVector(normals[i]).normalized;
                }
                if (i < tangents.Length)
                {
                    Vector3 t = m.MultiplyVector(new Vector3(tangents[i].x, tangents[i].y, tangents[i].z)).normalized;
                    outTangents[i] = new Vector4(t.x, t.y, t.z, tangents[i].w);
                }
            }

            // Vértices que se conservan: los que dependen sobre todo de los huesos pedidos.
            var keep = new bool[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                BoneWeight w = weights[i];
                int dominant = w.boneIndex0;
                float best = w.weight0;
                if (w.weight1 > best) { best = w.weight1; dominant = w.boneIndex1; }
                if (w.weight2 > best) { best = w.weight2; dominant = w.boneIndex2; }
                if (w.weight3 > best) { dominant = w.boneIndex3; }
                Transform bone = dominant < bones.Length ? bones[dominant] : null;
                keep[i] = keepBones == null || (bone != null && keepBones.Contains(bone.name));
            }

            // Triángulos con sus tres vértices conservados, y malla compactada (sin vértices sueltos).
            var remap = new int[vertices.Length];
            for (int i = 0; i < remap.Length; i++)
            {
                remap[i] = -1;
            }
            var newVertices = new List<Vector3>();
            var newNormals = new List<Vector3>();
            var newTangents = new List<Vector4>();
            var newUv = new List<Vector2>();
            var newColors = new List<Color32>();
            Vector2[] uv = source.uv;
            Color32[] colors = source.colors32;
            var triangles = new List<int>[source.subMeshCount];
            for (int s = 0; s < source.subMeshCount; s++)
            {
                triangles[s] = new List<int>();
                int[] tris = source.GetTriangles(s);
                for (int t = 0; t + 2 < tris.Length; t += 3)
                {
                    if (!keep[tris[t]] || !keep[tris[t + 1]] || !keep[tris[t + 2]])
                    {
                        continue;
                    }
                    for (int k = 0; k < 3; k++)
                    {
                        int old = tris[t + k];
                        if (remap[old] < 0)
                        {
                            remap[old] = newVertices.Count;
                            newVertices.Add(outVertices[old]);
                            if (outNormals.Length == vertices.Length) newNormals.Add(outNormals[old]);
                            if (outTangents.Length == vertices.Length) newTangents.Add(outTangents[old]);
                            if (uv.Length == vertices.Length) newUv.Add(uv[old]);
                            if (colors.Length == vertices.Length) newColors.Add(colors[old]);
                        }
                        triangles[s].Add(remap[old]);
                    }
                }
            }
            if (newVertices.Count == 0)
            {
                TameableAnimalsPlugin.Log.LogWarning("Bear saddle: no saddle triangles found in the lox saddle mesh.");
                return null;
            }

            var mesh = new Mesh { name = "TA_BearSaddle_Mesh" };
            mesh.indexFormat = newVertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(newVertices);
            if (newNormals.Count == newVertices.Count) mesh.SetNormals(newNormals);
            if (newTangents.Count == newVertices.Count) mesh.SetTangents(newTangents);
            if (newUv.Count == newVertices.Count) mesh.SetUVs(0, newUv);
            if (newColors.Count == newVertices.Count) mesh.SetColors(newColors);
            mesh.subMeshCount = source.subMeshCount;
            for (int s = 0; s < source.subMeshCount; s++)
            {
                mesh.SetTriangles(triangles[s], s);
            }
            return mesh;
        }

        private static Matrix4x4 Blend(Matrix4x4[] m, BoneWeight w)
        {
            var r = new Matrix4x4();
            AddScaled(ref r, m[w.boneIndex0], w.weight0);
            if (w.weight1 > 0f) AddScaled(ref r, m[w.boneIndex1], w.weight1);
            if (w.weight2 > 0f) AddScaled(ref r, m[w.boneIndex2], w.weight2);
            if (w.weight3 > 0f) AddScaled(ref r, m[w.boneIndex3], w.weight3);
            return r;
        }

        private static void AddScaled(ref Matrix4x4 r, Matrix4x4 m, float f)
        {
            for (int i = 0; i < 16; i++)
            {
                r[i] += m[i] * f;
            }
        }

        internal static void ApplyLayout(Transform saddle, Transform attach)
        {
            if (!s_haveLayout)
            {
                return;
            }
            float scaleMultiplier = Mathf.Max(0.05f, TameableAnimalsPlugin.BearSaddleScale.Value);
            Vector3 offset = TameableAnimalsPlugin.ParseVector3(TameableAnimalsPlugin.BearSaddleOffset.Value);
            // Colocación deseada en el espacio de Visual (oso en reposo)...
            Matrix4x4 saddlePose = Matrix4x4.TRS(s_basePosition + offset, Quaternion.identity, s_baseScale * scaleMultiplier);
            Vector3 attachPosition = s_basePosition + offset + (s_baseAttach - s_basePosition) * scaleMultiplier
                + TameableAnimalsPlugin.ParseVector3(TameableAnimalsPlugin.BearSaddleAttachOffset.Value);
            Matrix4x4 attachPose = Matrix4x4.TRS(attachPosition, Quaternion.identity, Vector3.one);
            // ...convertida al hueso de la columna si cuelga de él (sus ejes están girados y escalados x100).
            if (s_haveBone && saddle.parent != null && saddle.parent.name == BoneName)
            {
                Matrix4x4 toBone = s_boneRestInVisual.inverse;
                saddlePose = toBone * saddlePose;
                attachPose = toBone * attachPose;
            }
            SetLocalPose(saddle, saddlePose);
            SetLocalPose(attach, attachPose);
        }

        private static void SetLocalPose(Transform t, Matrix4x4 m)
        {
            t.localPosition = m.GetColumn(3);
            t.localRotation = m.rotation;
            t.localScale = m.lossyScale;
        }

        /// <summary>
        /// Caja del cuerpo en el espacio de Visual, con la pose actual de los huesos: la caja de cada malla con
        /// esqueleto (localBounds, relativa a su hueso raíz). Se usa la más grande.
        /// </summary>
        private static Bounds GetBodyBounds(Transform visual, SkinnedMeshRenderer exclude)
        {
            Bounds best = new Bounds(new Vector3(0f, 1f, 0f), new Vector3(1f, 1f, 1.5f));
            float bestVolume = 0f;
            foreach (SkinnedMeshRenderer renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer == exclude || renderer.sharedMesh == null || !renderer.gameObject.activeSelf)
                {
                    continue;
                }
                Transform space = renderer.rootBone != null ? renderer.rootBone : renderer.transform;
                Bounds local = renderer.localBounds;
                Vector3 c = local.center, e = local.extents;
                Bounds bounds = new Bounds(visual.InverseTransformPoint(space.TransformPoint(c)), Vector3.zero);
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                    bounds.Encapsulate(visual.InverseTransformPoint(space.TransformPoint(corner)));
                }
                float volume = bounds.size.x * bounds.size.y * bounds.size.z;
                if (volume > bestVolume)
                {
                    best = bounds;
                    bestVolume = volume;
                }
            }
            return best;
        }

        // ------------------------------------------------- ajuste en el juego

        /// <summary>Vuelve a colocar la montura en el prefab y en todos los osos cargados.</summary>
        internal static int ReapplyLayoutEverywhere()
        {
            int count = 0;
            var targets = new List<Transform>();
            foreach (Character character in Character.GetAllCharacters())
            {
                if (Utils.GetPrefabName(character.gameObject) == "Bjorn")
                {
                    targets.Add(character.transform);
                }
            }
            foreach (Transform target in targets)
            {
                Transform saddle = FindDeep(target, SaddleObjectName);
                Transform attach = FindDeep(target, AttachObjectName);
                if (saddle != null && attach != null)
                {
                    ApplyLayout(saddle, attach);
                    count++;
                }
            }
            return count;
        }
    }

    /// <summary>Marca los osos (adultos) que llevan montura; las crías no la tienen.</summary>
    public class BearSaddleMarker : MonoBehaviour
    {
    }

    // Antes de que Tameable registre la montura (en su Awake necesita m_saddle ya asignado).
    [HarmonyPatch(typeof(Tameable), nameof(Tameable.Awake))]
    internal static class Tameable_Awake_Patch
    {
        private static void Prefix(Tameable __instance)
        {
            if (__instance.m_saddle == null && __instance.GetComponent<BearSaddleMarker>() != null)
            {
                BearSaddle.BuildOnInstance(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
    internal static class Terminal_InitTerminal_Patch
    {
        private static void Postfix()
        {
            new Terminal.ConsoleCommand("ta_bearsaddle",
                "[offsetX offsetY offsetZ scale [attachX attachY attachZ]] - TameableAnimals: move the saddle on bears live and save it to the config",
                args =>
                {
                    CultureInfo inv = CultureInfo.InvariantCulture;
                    if (args.Length >= 5)
                    {
                        if (float.TryParse(args[1], NumberStyles.Float, inv, out float x) && float.TryParse(args[2], NumberStyles.Float, inv, out float y)
                            && float.TryParse(args[3], NumberStyles.Float, inv, out float z) && float.TryParse(args[4], NumberStyles.Float, inv, out float s))
                        {
                            TameableAnimalsPlugin.BearSaddleOffset.Value = TameableAnimalsPlugin.FormatVector3(new Vector3(x, y, z));
                            TameableAnimalsPlugin.BearSaddleScale.Value = s;
                        }
                        if (args.Length >= 8 && float.TryParse(args[5], NumberStyles.Float, inv, out float ax)
                            && float.TryParse(args[6], NumberStyles.Float, inv, out float ay) && float.TryParse(args[7], NumberStyles.Float, inv, out float az))
                        {
                            TameableAnimalsPlugin.BearSaddleAttachOffset.Value = TameableAnimalsPlugin.FormatVector3(new Vector3(ax, ay, az));
                        }
                        TameableAnimalsPlugin.MainConfig.Save();
                        int updated = BearSaddle.ReapplyLayoutEverywhere();
                        args.Context.AddString("Bear saddle updated on " + updated + " bear(s) and saved to the config.");
                    }
                    args.Context.AddString("Offset=" + TameableAnimalsPlugin.BearSaddleOffset.Value + "  Scale="
                        + TameableAnimalsPlugin.BearSaddleScale.Value.ToString(inv) + "  AttachOffset=" + TameableAnimalsPlugin.BearSaddleAttachOffset.Value);
                    args.Context.AddString("Usage: ta_bearsaddle <offsetX> <offsetY> <offsetZ> <scale> [attachX attachY attachZ]   e.g. ta_bearsaddle 0 0.1 -0.2 1.1");
                });
        }
    }
}
