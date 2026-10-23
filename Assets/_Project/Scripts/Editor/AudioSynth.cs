// AudioSynth : synthétise par calcul les sons qui manquent dans les packs (ambiances, musique, tir).
//
// Les packs fournis (Kenney, Leohpaz, VR Template) n'ont ni « twang » d'arc, ni sifflement de flèche,
// ni ambiance de forêt/rivière/feu/foule, ni musique. Plutôt que de laisser ces événements muets, on les
// génère ici : bruit filtré, oscillateurs et cordes pincées (algorithme de Karplus-Strong). Ce sont des
// sons de remplacement CORRECTS mais simples : l'équipe pourra les remplacer par de vrais enregistrements
// libres de droits (Freesound, Pixabay) sans toucher au code, en changeant seulement les clips des banques.
//
// Les fichiers sont des WAV 16 bits mono écrits dans Assets/_Project/Audio/… (voir CREDITS.txt).

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ArcheryChallenge.EditorTools
{
    /// <summary>Générateur de sons procéduraux.</summary>
    public static class AudioSynth
    {
        private const int Rate = 32000;

        /// <summary>Écrit un tableau d'échantillons en WAV 16 bits mono dans le projet et renvoie le chemin d'asset.</summary>
        public static string Save(string assetPath, float[] samples, int rate = Rate)
        {
            string full = Path.Combine(Path.GetDirectoryName(Application.dataPath), assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            using (FileStream stream = new FileStream(full, FileMode.Create))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                int dataSize = samples.Length * 2;
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataSize);
                writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);       // PCM
                writer.Write((short)1);       // mono
                writer.Write(rate);
                writer.Write(rate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataSize);
                foreach (float s in samples)
                    writer.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * 32767f), -32767, 32767));
            }
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            return assetPath;
        }

        // ---------------------------------------------------------------- briques de base

        private static float[] Noise(int length, System.Random rng)
        {
            float[] n = new float[length];
            for (int i = 0; i < length; i++)
                n[i] = (float)(rng.NextDouble() * 2.0 - 1.0);
            return n;
        }

        // Filtre passe-bas à un pôle.
        private static void LowPass(float[] data, float cutoff)
        {
            float rc = 1f / (2f * Mathf.PI * cutoff);
            float dt = 1f / Rate;
            float alpha = dt / (rc + dt);
            float y = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                y += alpha * (data[i] - y);
                data[i] = y;
            }
        }

        private static void HighPass(float[] data, float cutoff)
        {
            float[] low = (float[])data.Clone();
            LowPass(low, cutoff);
            for (int i = 0; i < data.Length; i++)
                data[i] -= low[i];
        }

        private static void Normalize(float[] data, float peak)
        {
            float max = 0.0001f;
            foreach (float s in data)
                max = Mathf.Max(max, Mathf.Abs(s));
            float gain = peak / max;
            for (int i = 0; i < data.Length; i++)
                data[i] *= gain;
        }

        // Rend la boucle sans clic : les dernières secondes sont fondues avec le début.
        private static float[] MakeLoop(float[] data, int overlap)
        {
            int length = data.Length - overlap;
            float[] loop = new float[length];
            Array.Copy(data, loop, length);
            for (int i = 0; i < overlap; i++)
            {
                float t = (float)i / overlap;
                loop[i] = loop[i] * t + data[length + i] * (1f - t);
            }
            return loop;
        }

        // ---------------------------------------------------------------- effets de tir

        /// <summary>« Twang » de la corde au relâchement : sinus amortis et clic initial.</summary>
        public static float[] BowRelease(int variant)
        {
            System.Random rng = new System.Random(100 + variant);
            int length = (int)(Rate * 0.7f);
            float[] s = new float[length];
            float f = 150f + variant * 25f;
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / Rate;
                float envelope = Mathf.Exp(-t * 7f);
                float pitch = f * (1f + 0.25f * Mathf.Exp(-t * 18f));
                float v = Mathf.Sin(2f * Mathf.PI * pitch * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * pitch * 2.01f * t) + 0.25f * Mathf.Sin(2f * Mathf.PI * pitch * 3.02f * t);
                s[i] = v * envelope;
            }
            float[] click = Noise(400, rng);
            HighPass(click, 2000f);
            for (int i = 0; i < click.Length; i++)
                s[i] += click[i] * Mathf.Exp(-i / 80f) * 1.2f;
            Normalize(s, 0.85f);
            return s;
        }

        /// <summary>Sifflement d'une flèche : bruit passe-bande dont la fréquence glisse.</summary>
        public static float[] ArrowWhoosh(int variant)
        {
            System.Random rng = new System.Random(200 + variant);
            int length = (int)(Rate * 0.8f);
            float[] noise = Noise(length, rng);
            float[] s = new float[length];
            float[] band = (float[])noise.Clone();
            LowPass(band, 3500f + variant * 500f);
            HighPass(band, 700f);
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / length;
                float envelope = Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.7f));
                s[i] = band[i] * envelope;
            }
            Normalize(s, 0.6f);
            return s;
        }

        /// <summary>Bip du compte à rebours.</summary>
        public static float[] Beep(float frequency, float duration)
        {
            int length = (int)(Rate * duration);
            float[] s = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / Rate;
                float envelope = Mathf.Clamp01(t * 60f) * Mathf.Exp(-t * (6f / duration));
                s[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.8f;
            }
            return s;
        }

        // ---------------------------------------------------------------- ambiances

        public static float[] ForestDay()
        {
            System.Random rng = new System.Random(300);
            int length = Rate * 16;
            float[] wind = Noise(length, rng);
            LowPass(wind, 600f);
            float[] s = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / Rate;
                float swell = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * t / 8f + 1f) * Mathf.Sin(2f * Mathf.PI * t / 5.3f);
                s[i] = wind[i] * (0.35f + 0.65f * swell);
            }
            Normalize(s, 0.5f);
            // Oiseaux : petits « pi-pi » aigus à des instants aléatoires.
            for (int b = 0; b < 14; b++)
            {
                int start = rng.Next(0, length - Rate);
                float baseFreq = 2200f + (float)rng.NextDouble() * 1800f;
                int notes = rng.Next(2, 5);
                float volume = 0.12f + (float)rng.NextDouble() * 0.1f;
                for (int n = 0; n < notes; n++)
                {
                    int noteStart = start + n * (int)(Rate * 0.13f);
                    int noteLength = (int)(Rate * 0.09f);
                    for (int i = 0; i < noteLength && noteStart + i < length; i++)
                    {
                        float tt = (float)i / noteLength;
                        float freq = baseFreq * (1f + 0.35f * tt) * (1f + 0.1f * n);
                        s[noteStart + i] += Mathf.Sin(2f * Mathf.PI * freq * i / Rate) * Mathf.Sin(Mathf.PI * tt) * volume;
                    }
                }
            }
            Normalize(s, 0.7f);
            return MakeLoop(s, Rate * 2);
        }

        public static float[] ForestNight()
        {
            System.Random rng = new System.Random(400);
            int length = Rate * 14;
            float[] s = new float[length];
            float[] wind = Noise(length, rng);
            LowPass(wind, 350f);
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / Rate;
                float chirpEnvelope = Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * t * 22f)) * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * t / 3.1f));
                float cricket = Mathf.Sin(2f * Mathf.PI * 4300f * t) * chirpEnvelope * 0.18f;
                float cricket2 = Mathf.Sin(2f * Mathf.PI * 5100f * t) * Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * t * 17f + 1f)) * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * t / 4.3f + 2f)) * 0.12f;
                s[i] = wind[i] * 0.35f + cricket + cricket2;
            }
            Normalize(s, 0.55f);
            return MakeLoop(s, Rate * 2);
        }

        public static float[] River()
        {
            System.Random rng = new System.Random(500);
            int length = Rate * 12;
            float[] n1 = Noise(length, rng);
            float[] n2 = Noise(length, rng);
            LowPass(n1, 1800f);
            HighPass(n1, 250f);
            LowPass(n2, 900f);
            float[] s = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / Rate;
                float ripple = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * t * 1.7f) * Mathf.Sin(2f * Mathf.PI * t * 0.43f);
                s[i] = n1[i] * ripple + n2[i] * 0.5f;
            }
            Normalize(s, 0.6f);
            return MakeLoop(s, Rate * 2);
        }

        public static float[] Campfire()
        {
            System.Random rng = new System.Random(600);
            int length = Rate * 10;
            float[] s = Noise(length, rng);
            LowPass(s, 500f);
            for (int i = 0; i < length; i++)
                s[i] *= 0.25f;
            // Craquements : impulsions brèves aléatoires.
            for (int c = 0; c < 70; c++)
            {
                int start = rng.Next(0, length - 2000);
                float strength = 0.25f + (float)rng.NextDouble() * 0.6f;
                float tone = 800f + (float)rng.NextDouble() * 2500f;
                int duration = rng.Next(150, 900);
                for (int i = 0; i < duration; i++)
                {
                    float e = Mathf.Exp(-i / (duration * 0.25f));
                    s[start + i] += ((float)rng.NextDouble() * 2f - 1f) * e * strength + Mathf.Sin(2f * Mathf.PI * tone * i / Rate) * e * strength * 0.3f;
                }
            }
            Normalize(s, 0.7f);
            return MakeLoop(s, Rate);
        }

        public static float[] Crowd()
        {
            System.Random rng = new System.Random(700);
            int length = Rate * 14;
            float[] voices = Noise(length, rng);
            LowPass(voices, 1100f);
            HighPass(voices, 180f);
            float[] s = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / Rate;
                float murmur = 0.55f + 0.2f * Mathf.Sin(2f * Mathf.PI * t * 0.9f) + 0.15f * Mathf.Sin(2f * Mathf.PI * t * 5.3f);
                float cheer = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * t / 7f)), 4f) * 0.7f;
                s[i] = voices[i] * (murmur + cheer);
            }
            Normalize(s, 0.6f);
            return MakeLoop(s, Rate * 2);
        }

        // ---------------------------------------------------------------- musique (cordes pincées)

        // Corde pincée de Karplus-Strong : un tampon de bruit qui se filtre en boucle.
        private static void Pluck(float[] target, int start, float frequency, float volume, System.Random rng, float decay = 0.996f)
        {
            int delay = Mathf.Max(2, Mathf.RoundToInt(Rate / frequency));
            float[] buffer = new float[delay];
            for (int i = 0; i < delay; i++)
                buffer[i] = (float)(rng.NextDouble() * 2.0 - 1.0);
            int length = Rate * 3;
            int index = 0;
            for (int i = 0; i < length && start + i < target.Length; i++)
            {
                float current = buffer[index];
                float next = buffer[(index + 1) % delay];
                float value = (current + next) * 0.5f * decay;
                buffer[index] = value;
                index = (index + 1) % delay;
                target[start + i] += current * volume * Mathf.Min(1f, i / 40f);
            }
        }

        private static float Note(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

        /// <summary>Musique calme du camp : arpèges de luth sur Am – F – C – G (boucle de 16 s).</summary>
        public static float[] MusicCamp()
        {
            System.Random rng = new System.Random(800);
            float bpm = 90f;
            float beat = 60f / bpm;
            int bars = 8;
            int length = (int)(Rate * beat * 4f * bars) + Rate * 3;
            float[] s = new float[length];
            int[][] chords =
            {
                new[] { 57, 60, 64, 69 }, // Am
                new[] { 53, 57, 60, 65 }, // F
                new[] { 48, 55, 60, 64 }, // C
                new[] { 55, 59, 62, 67 }  // G
            };
            int[] melody = { 76, 72, 74, 71, 72, 69, 71, 67 };
            for (int bar = 0; bar < bars; bar++)
            {
                int[] chord = chords[bar % 4];
                for (int step = 0; step < 8; step++)
                {
                    int start = (int)(Rate * (bar * 4f + step * 0.5f) * beat);
                    Pluck(s, start, Note(chord[step % 4] - (step == 0 ? 12 : 0)), 0.5f, rng);
                }
                // Mélodie : une note par mesure et un écho, au-dessus de l'arpège.
                Pluck(s, (int)(Rate * (bar * 4f + 1f) * beat), Note(melody[bar]), 0.45f, rng, 0.998f);
                if (bar % 2 == 1)
                    Pluck(s, (int)(Rate * (bar * 4f + 2.5f) * beat), Note(melody[bar] + 3), 0.3f, rng, 0.998f);
            }
            Normalize(s, 0.55f);
            // La queue des dernières notes se fond avec le début pour une boucle continue.
            int loopLength = (int)(Rate * beat * 4f * bars);
            float[] loop = new float[loopLength];
            Array.Copy(s, loop, loopLength);
            int tail = s.Length - loopLength;
            for (int i = 0; i < tail && i < loopLength; i++)
                loop[i] += s[loopLength + i];
            Normalize(loop, 0.55f);
            return loop;
        }

        /// <summary>Petit air de victoire (fanfare de luth).</summary>
        public static float[] MusicVictory()
        {
            System.Random rng = new System.Random(900);
            int length = Rate * 5;
            float[] s = new float[length];
            int[] notes = { 60, 64, 67, 72, 67, 72, 76 };
            float[] times = { 0f, 0.25f, 0.5f, 0.75f, 1.2f, 1.5f, 1.9f };
            for (int i = 0; i < notes.Length; i++)
            {
                Pluck(s, (int)(Rate * times[i]), Note(notes[i]), 0.55f, rng, 0.997f);
                Pluck(s, (int)(Rate * times[i]), Note(notes[i] - 12), 0.3f, rng, 0.997f);
            }
            Normalize(s, 0.7f);
            return s;
        }
    }
}
