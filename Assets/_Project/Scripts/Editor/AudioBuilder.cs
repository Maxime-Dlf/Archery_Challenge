// AudioBuilder : génère les sons manquants et remplit toutes les banques de sons (§P14 du tutoriel).
//
// Menu : Tools → Archery Challenge → Build Audio.
// - Synthétise (AudioSynth) les sons absents des packs : twang, sifflement, ambiances, musique, bips.
// - Crée une banque (SoundBank) par événement dans Assets/_Project/Data/Audio, avec ses variantes de clips,
//   son volume, sa famille (Effets / Ambiance / Interface / Musique), ses distances 3D.
// - Écrit Assets/_Project/Audio/CREDITS.txt (licences des packs et des sons générés).

using System.Collections.Generic;
using System.IO;
using ArcheryChallenge.Audio;
using UnityEditor;
using UnityEngine;

namespace ArcheryChallenge.EditorTools
{
    /// <summary>Générateur des banques de sons.</summary>
    public static class AudioBuilder
    {
        private const string SfxFolder = "Assets/_Project/Audio/SFX";
        private const string AmbienceFolder = "Assets/_Project/Audio/Ambience";
        private const string MusicFolder = "Assets/_Project/Audio/Music";

        [MenuItem("Tools/Archery Challenge/Build Audio")]
        public static void BuildAll()
        {
            BuildUtils.EnsureFolder(SfxFolder);
            BuildUtils.EnsureFolder(AmbienceFolder);
            BuildUtils.EnsureFolder(MusicFolder);
            BuildUtils.EnsureFolder(BuildUtils.DataRoot + "/Audio");

            // --- sons synthétisés ---
            AudioClip[] twang = { Gen(SfxFolder, "Gen_BowRelease_1", AudioSynth.BowRelease(0), false), Gen(SfxFolder, "Gen_BowRelease_2", AudioSynth.BowRelease(1), false), Gen(SfxFolder, "Gen_BowRelease_3", AudioSynth.BowRelease(2), false) };
            AudioClip[] whoosh = { Gen(SfxFolder, "Gen_ArrowWhoosh_1", AudioSynth.ArrowWhoosh(0), false), Gen(SfxFolder, "Gen_ArrowWhoosh_2", AudioSynth.ArrowWhoosh(1), false) };
            AudioClip beep = Gen(SfxFolder, "Gen_Beep", AudioSynth.Beep(880f, 0.22f), false);
            AudioClip go = Gen(SfxFolder, "Gen_Go", AudioSynth.Beep(1320f, 0.6f), false);
            AudioClip forestDay = Gen(AmbienceFolder, "Gen_ForestDay", AudioSynth.ForestDay(), true);
            AudioClip forestNight = Gen(AmbienceFolder, "Gen_ForestNight", AudioSynth.ForestNight(), true);
            AudioClip river = Gen(AmbienceFolder, "Gen_River", AudioSynth.River(), true);
            AudioClip campfire = Gen(AmbienceFolder, "Gen_Campfire", AudioSynth.Campfire(), true);
            AudioClip crowd = Gen(AmbienceFolder, "Gen_Crowd", AudioSynth.Crowd(), true);
            AudioClip musicCamp = Gen(MusicFolder, "Gen_MusicCamp", AudioSynth.MusicCamp(), true, false);
            AudioClip musicVictory = Gen(MusicFolder, "Gen_MusicVictory", AudioSynth.MusicVictory(), false, false);

            // --- banques ---
            Bank("BowDraw", SoundCategory.Sfx, 0.6f, true, 0.2f, 5f, false, Find("creak1"), Find("creak2"), Find("creak3"));
            Bank("BowRelease", SoundCategory.Sfx, 0.9f, true, 0.2f, 8f, false, twang);
            Bank("ArrowPick", SoundCategory.Sfx, 0.6f, true, 0.2f, 5f, false, Find("cloth1"), Find("cloth2"), Find("cloth3"), Find("cloth4"));
            Bank("ArrowNock", SoundCategory.Sfx, 0.55f, true, 0.2f, 5f, false, Find("handleSmallLeather"), Find("handleSmallLeather2"), Find("beltHandle1"));
            Bank("ArrowWhoosh", SoundCategory.Sfx, 0.8f, true, 0.5f, 20f, false, whoosh);
            Bank("ImpactTarget", SoundCategory.Sfx, 0.95f, true, 1f, 40f, false, Find("chop"), Find("knifeSlice"));
            Bank("ImpactWood", SoundCategory.Sfx, 0.9f, true, 1f, 40f, false, Find("39_Block_03"));
            Bank("ImpactGround", SoundCategory.Sfx, 0.9f, true, 1f, 40f, false, Find("dropLeather"));
            Bank("ImpactFlesh", SoundCategory.Sfx, 0.9f, true, 1f, 40f, false, Find("15_Impact_flesh_02"), Find("77_flesh_02"));
            Bank("MonsterSense", SoundCategory.Sfx, 0.8f, true, 1f, 25f, false, Find("55_Encounter_02"));
            Bank("MonsterAttack", SoundCategory.Sfx, 0.85f, true, 1f, 25f, false, Find("03_Claw_03"), Find("08_Bite_04"));
            Bank("MonsterHit", SoundCategory.Sfx, 0.85f, true, 1f, 25f, false, Find("15_Impact_flesh_02"));
            Bank("MonsterDie", SoundCategory.Sfx, 0.9f, true, 1f, 25f, false, Find("69_Enemy_death_01"));
            Bank("PlayerHurt", SoundCategory.Sfx, 1f, true, 1f, 25f, false, Find("61_Hit_03"));
            Bank("Footstep", SoundCategory.Sfx, 0.6f, true, 0.5f, 8f, false, Find("footstep00"), Find("footstep01"), Find("footstep02"), Find("footstep03"), Find("03_Step_grass_03"));
            Bank("GateOpen", SoundCategory.Sfx, 0.9f, true, 1f, 30f, false, Find("doorOpen_1"), Find("doorOpen_2"));
            Bank("Campfire", SoundCategory.Ambience, 0.8f, true, 1f, 15f, true, campfire);
            Bank("River", SoundCategory.Ambience, 0.9f, true, 5f, 40f, true, river);
            Bank("Crowd", SoundCategory.Ambience, 0.8f, true, 10f, 60f, true, crowd);
            Bank("ForestDay", SoundCategory.Ambience, 0.55f, false, 1f, 10f, true, forestDay);
            Bank("ForestNight", SoundCategory.Ambience, 0.55f, false, 1f, 10f, true, forestNight);
            Bank("LevelSuccess", SoundCategory.Ui, 0.9f, false, 1f, 10f, false, Find("16_Atk_buff_04"));
            Bank("LevelFail", SoundCategory.Ui, 0.9f, false, 1f, 10f, false, Find("21_Debuff_01"));
            Bank("Unlock", SoundCategory.Ui, 0.9f, false, 1f, 10f, false, Find("070_Equip_10"));
            Bank("Countdown", SoundCategory.Ui, 0.7f, false, 1f, 10f, false, beep);
            Bank("CountdownGo", SoundCategory.Ui, 0.8f, false, 1f, 10f, false, go);
            Bank("UiHover", SoundCategory.Ui, 0.6f, false, 1f, 10f, false, Find("Button_14_hover"), Find("001_Hover_01"));
            Bank("UiClick", SoundCategory.Ui, 0.8f, false, 1f, 10f, false, Find("Button_22_click"), Find("013_Confirm_03"));
            Bank("MusicCamp", SoundCategory.Music, 0.5f, false, 1f, 10f, true, musicCamp);
            Bank("MusicVictory", SoundCategory.Music, 0.8f, false, 1f, 10f, false, musicVictory);

            WriteCredits();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AudioBuilder] Sons et banques générés.");
        }

        /// <summary>Toutes les banques de Data/Audio (pour le prefab Systems).</summary>
        public static SoundBank[] LoadBanks()
        {
            List<SoundBank> list = new List<SoundBank>();
            foreach (string guid in AssetDatabase.FindAssets("t:SoundBank", new[] { BuildUtils.DataRoot + "/Audio" }))
                list.Add(AssetDatabase.LoadAssetAtPath<SoundBank>(AssetDatabase.GUIDToAssetPath(guid)));
            return list.ToArray();
        }

        // ---------------------------------------------------------------- outils

        private static AudioClip Gen(string folder, string name, float[] samples, bool streaming, bool mono = true)
        {
            string path = $"{folder}/{name}.wav";
            AudioSynth.Save(path, samples);
            AudioImporter importer = (AudioImporter)AssetImporter.GetAtPath(path);
            if (importer != null)
            {
                importer.forceToMono = mono;
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = streaming ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = streaming ? 0.6f : 0.7f;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        private static AudioClip Find(string name)
        {
            foreach (string guid in AssetDatabase.FindAssets(name + " t:AudioClip"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == name)
                    return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
            Debug.LogWarning($"[AudioBuilder] Son introuvable : {name}");
            return null;
        }

        private static void Bank(string name, SoundCategory category, float volume, bool spatial, float min, float max, bool loop, params AudioClip[] clips)
        {
            SoundBank bank = BuildUtils.LoadOrCreate<SoundBank>($"{BuildUtils.DataRoot}/Audio/{name}.asset");
            List<AudioClip> valid = new List<AudioClip>();
            foreach (AudioClip clip in clips)
                if (clip != null)
                    valid.Add(clip);
            bank.clips = valid.ToArray();
            bank.category = category;
            bank.volume = volume;
            bank.spatial = spatial;
            bank.minDistance = min;
            bank.maxDistance = max;
            bank.loop = loop;
            bank.pitchMin = loop || category == SoundCategory.Music ? 1f : 0.95f;
            bank.pitchMax = loop || category == SoundCategory.Music ? 1f : 1.05f;
            EditorUtility.SetDirty(bank);
        }

        private static void WriteCredits()
        {
            string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Assets/_Project/Audio/CREDITS.txt");
            File.WriteAllText(path,
                "CRÉDITS AUDIO : Archery Challenge\n" +
                "================================\n\n" +
                "Sons fournis avec le projet\n" +
                "- Kenney « RPG Audio » (CC0) : creak, cloth, chop, footstep, door, dropLeather, handleSmallLeather… https://kenney.nl\n" +
                "- Leohpaz « RPG Essentials SFX Free » : sons de combat, de bonus et d'interface (voir la licence du pack).\n" +
                "- Unity XR « VR Template » : Button_14_hover, Button_22_click.\n\n" +
                "Sons générés par le projet (domaine public, créés par code, voir Scripts/Editor/AudioSynth.cs)\n" +
                "- Gen_BowRelease_*, Gen_ArrowWhoosh_*, Gen_Beep, Gen_Go\n" +
                "- Ambiances : Gen_ForestDay, Gen_ForestNight, Gen_River, Gen_Campfire, Gen_Crowd\n" +
                "- Musiques : Gen_MusicCamp, Gen_MusicVictory (cordes pincées, algorithme de Karplus-Strong)\n\n" +
                "Ces sons générés sont des remplaçants simples : ils peuvent être remplacés par des enregistrements libres\n" +
                "(Freesound CC0, Pixabay) en changeant seulement les clips des banques de Data/Audio.\n" +
                "Notez ici titre, auteur, licence et lien de chaque son téléchargé.\n");
            AssetDatabase.ImportAsset("Assets/_Project/Audio/CREDITS.txt");
        }
    }
}
