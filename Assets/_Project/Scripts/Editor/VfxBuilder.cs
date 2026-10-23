// VfxBuilder : crée les effets de particules du jeu (§P15 du tutoriel) et branche ImpactFeedback.
//
// Menu : Tools → Archery Challenge → Build VFX.
// Contraintes Quest : moins de 50 particules par effet, aucune lumière, aucune ombre, matériau
// transparent URP Particles/Unlit (M_Particle, M_Particle_Additive).

using ArcheryChallenge.Core;
using ArcheryChallenge.VFX;
using UnityEditor;
using UnityEngine;

namespace ArcheryChallenge.EditorTools
{
    /// <summary>Générateur d'effets de particules.</summary>
    public static class VfxBuilder
    {
        private const string Folder = BuildUtils.PrefabRoot + "/VFX";

        [MenuItem("Tools/Archery Challenge/Build VFX")]
        public static void BuildAll()
        {
            BuildUtils.EnsureFolder(Folder);
            Texture2D particleTexture = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
            Material normal = BuildUtils.EnsureMaterial("M_Particle", "Universal Render Pipeline/Particles/Unlit", Color.white, true);
            Material additive = BuildUtils.EnsureMaterial("M_Particle_Additive", "Universal Render Pipeline/Particles/Unlit", Color.white, true);
            if (additive.HasProperty("_Blend")) additive.SetFloat("_Blend", 2f); // Additive
            if (additive.HasProperty("_DstBlend")) additive.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            foreach (Material m in new[] { normal, additive })
                if (m.HasProperty("_BaseMap") && particleTexture != null)
                    m.SetTexture("_BaseMap", particleTexture);
            EditorUtility.SetDirty(normal);
            EditorUtility.SetDirty(additive);

            GameObject wood = Burst("VFX_Impact_Wood", normal, Hex("#B08A5A"), 0.5f, 0.3f, 0.6f, 1f, 3f, 0.01f, 0.03f, 1f, 20, 30f, 0.02f);
            Burst("VFX_Impact_Straw", normal, Hex("#E3C878"), 0.5f, 0.3f, 0.6f, 1f, 3f, 0.02f, 0.05f, 1f, 20, 30f, 0.02f);
            Burst("VFX_Impact_Dirt", normal, Hex("#6B4F35"), 0.5f, 0.3f, 0.6f, 1f, 3f, 0.02f, 0.04f, 2f, 25, 40f, 0.03f);
            Burst("VFX_Impact_Stone", normal, Hex("#8C8C8C"), 0.4f, 0.25f, 0.5f, 2f, 4f, 0.01f, 0.03f, 1f, 12, 35f, 0.02f);
            Burst("VFX_Bullseye", additive, Hex("#FFD54A"), 0.8f, 0.4f, 0.8f, 1f, 2.5f, 0.03f, 0.07f, 0f, 30, 180f, 0.05f);
            Burst("VFX_MonsterHit", normal, Hex("#5DD35D"), 0.5f, 0.3f, 0.6f, 1f, 3f, 0.03f, 0.07f, 1f, 18, 60f, 0.05f);
            Burst("VFX_MonsterDie", normal, Hex("#9AA39A"), 1f, 0.5f, 1f, 0.5f, 2f, 0.08f, 0.2f, 0.3f, 40, 180f, 0.2f);
            GameObject unlock = Burst("VFX_Unlock", additive, Hex("#FFD54A"), 1.5f, 0.8f, 1.4f, 1f, 3f, 0.04f, 0.1f, -0.1f, 40, 15f, 0.5f);
            Burst("VFX_GateDust", normal, Hex("#9C8465"), 1.5f, 1f, 2f, 0.2f, 0.8f, 0.2f, 0.5f, -0.05f, 20, 45f, 0.5f);

            GameObject campfire = Loop("VFX_Campfire", additive, 20f, 0.6f, 1f, 0.5f, 1f, 0.15f, 0.35f, -0.2f, 10f, 0.25f, 40);
            // Braises : petit système enfant.
            GameObject embers = Loop("VFX_Embers", additive, 5f, 1.2f, 1.8f, 1f, 2f, 0.02f, 0.03f, -0.1f, 25f, 0.3f, 15);
            Reparent(embers, campfire, "VFX_Embers");
            // Les braises sont dans le même prefab que le feu ; le prefab seul est supprimé.
            AssetDatabase.DeleteAsset(Folder + "/VFX_Embers.prefab");
            Loop("VFX_TorchFlame", additive, 10f, 0.4f, 0.7f, 0.3f, 0.6f, 0.05f, 0.12f, -0.2f, 10f, 0.06f, 15);
            AssetDatabase.SaveAssets();
            WireImpactFeedback();
            Debug.Log("[VfxBuilder] Effets visuels générés.");
        }

        /// <summary>Branche les effets dans le composant ImpactFeedback du prefab Systems (s'il existe).</summary>
        public static void WireImpactFeedback()
        {
            string systemsPath = BuildUtils.PrefabRoot + "/Systems/Systems.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(systemsPath) == null)
                return;
            GameObject systems = PrefabUtility.LoadPrefabContents(systemsPath);
            ImpactFeedback feedback = systems.GetComponent<ImpactFeedback>();
            if (feedback != null)
            {
                SerializedObject so = new SerializedObject(feedback);
                SerializedProperty entries = so.FindProperty("entries");
                (SurfaceType, string)[] map =
                {
                    (SurfaceType.Wood, "VFX_Impact_Wood"), (SurfaceType.Straw, "VFX_Impact_Straw"),
                    (SurfaceType.Dirt, "VFX_Impact_Dirt"), (SurfaceType.Stone, "VFX_Impact_Stone"),
                    (SurfaceType.Flesh, "VFX_MonsterHit"), (SurfaceType.Metal, "VFX_Impact_Stone")
                };
                entries.arraySize = map.Length;
                for (int i = 0; i < map.Length; i++)
                {
                    SerializedProperty e = entries.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("surface").enumValueIndex = (int)map[i].Item1;
                    e.FindPropertyRelative("effect").objectReferenceValue = Load(map[i].Item2);
                }
                so.FindProperty("bullseyeEffect").objectReferenceValue = Load("VFX_Bullseye");
                so.FindProperty("unlockEffect").objectReferenceValue = Load("VFX_Unlock");
                so.FindProperty("monsterHitEffect").objectReferenceValue = Load("VFX_MonsterHit");
                so.FindProperty("monsterDieEffect").objectReferenceValue = Load("VFX_MonsterDie");
                so.FindProperty("gateDustEffect").objectReferenceValue = Load("VFX_GateDust");
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(systems, systemsPath);
            PrefabUtility.UnloadPrefabContents(systems);
        }

        private static GameObject Load(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/{name}.prefab");

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }

        // Rafale unique (impact) : Duration courte, non bouclée, désactivée à la fin.
        private static GameObject Burst(string name, Material material, Color color, float duration, float lifeMin, float lifeMax,
            float speedMin, float speedMax, float sizeMin, float sizeMax, float gravity, int count, float angle, float radius)
        {
            GameObject go = new GameObject(name);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = duration;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = color;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Min(count + 10, 49);
            main.stopAction = ParticleSystemStopAction.Disable;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Min(count, 40)) });

            var shape = ps.shape;
            shape.shapeType = angle >= 90f ? ParticleSystemShapeType.Sphere : ParticleSystemShapeType.Cone;
            shape.angle = Mathf.Min(angle, 89f);
            shape.radius = radius;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = fade;

            ConfigureRenderer(go, material);
            return BuildUtils.SaveAsPrefab(go, $"{Folder}/{name}.prefab");
        }

        // Effet en boucle (feu, flamme, braises).
        private static GameObject Loop(string name, Material material, float rate, float lifeMin, float lifeMax, float speedMin, float speedMax,
            float sizeMin, float sizeMax, float gravity, float angle, float radius, int maxParticles)
        {
            GameObject go = new GameObject(name);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;

            var emission = ps.emission;
            emission.rateOverTime = rate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = angle;
            shape.radius = radius;
            shape.rotation = new Vector3(-90f, 0f, 0f); // vers le haut

            var color = ps.colorOverLifetime;
            color.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Hex("#FFD27A"), 0f), new GradientColorKey(Hex("#FF7A2A"), 0.5f), new GradientColorKey(Hex("#B02010"), 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
            color.color = g;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.1f)));

            ConfigureRenderer(go, material);
            return BuildUtils.SaveAsPrefab(go, $"{Folder}/{name}.prefab");
        }

        private static void ConfigureRenderer(GameObject go, Material material)
        {
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        // Insère le contenu d'un prefab d'effet comme enfant d'un autre prefab (feu + braises).
        private static void Reparent(GameObject child, GameObject parentPrefab, string childName)
        {
            string parentPath = AssetDatabase.GetAssetPath(parentPrefab);
            GameObject contents = PrefabUtility.LoadPrefabContents(parentPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(child);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = childName;
            instance.transform.SetParent(contents.transform, false);
            PrefabUtility.SaveAsPrefabAsset(contents, parentPath);
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }
}
