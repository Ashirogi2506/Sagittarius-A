using System.Collections.Generic;
using UnityEngine;

namespace SagittariusA.Minigames
{
    public enum MinigameCue { Hover, Press, Valid, Invalid, Progress, Success, Partial, Failure }

    // Placeholder audio hooks. Clips load from Resources/Minigames/Audio/<cue>.wav (lowercase cue name);
    // replace those files to change the sound. Every cue also has a visual counterpart in the UI.
    public static class MinigameAudio
    {
        private static AudioSource source;
        private static readonly Dictionary<MinigameCue, AudioClip> clips = new Dictionary<MinigameCue, AudioClip>();
        public static float Volume = .8f;
        public static bool Muted;

        public static void Bind(AudioSource audioSource) { source = audioSource; }

        public static void Play(MinigameCue cue, float volume = 1f)
        {
            if (Muted || source == null) return;
            if (!clips.TryGetValue(cue, out var clip))
                clips[cue] = clip = Resources.Load<AudioClip>("Minigames/Audio/" + cue.ToString().ToLowerInvariant());
            if (clip != null) source.PlayOneShot(clip, volume * Volume);
        }
    }
}
