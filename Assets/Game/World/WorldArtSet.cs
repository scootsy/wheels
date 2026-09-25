using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tabletop.World
{
    /// <summary>
    /// Which imported model replaces which placeholder in the world (D-026). Edited by hand in the Inspector:
    /// drag a model from Assets/Game/Characters or Assets/Game/Buildings onto a slot. Empty slots keep the
    /// primitive placeholder, so art can arrive one piece at a time.
    /// </summary>
    [CreateAssetMenu(fileName = "WorldArt", menuName = "Tabletop/World Art Set")]
    public sealed class WorldArtSet : ScriptableObject
    {
        [Serializable]
        public sealed class Slot
        {
            [Tooltip("Exactly as the game names it: a person (\"Wren\", \"Player\") or a building (\"Brindlecross/Inn\").")]
            public string who;
            [Tooltip("An imported model (FBX). Leave empty to keep the placeholder.")]
            public GameObject model;
            [Tooltip("Rigged models only: an animation whose frame is used as the standing pose (otherwise the rest pose).")]
            public AnimationClip pose;
            [Tooltip("Seconds into the pose animation.")]
            public float poseTime;
            [Tooltip("Play the animation continuously (an idle) instead of holding one frame.")]
            public bool loop;
            [Tooltip("Turn the model if it faces the wrong way (degrees). Placeholders face +Z.")]
            public float turn;
            [Tooltip("People: height in metres (0 = 1.75). Buildings: 0 = fill the placeholder's footprint, otherwise the width in metres.")]
            public float size;
        }

        public List<Slot> people = new List<Slot>();
        public List<Slot> buildings = new List<Slot>();

        public Slot Person(string name) => Find(people, name);
        public Slot Building(string key) => Find(buildings, key);

        private static Slot Find(List<Slot> slots, string key)
        {
            foreach (var s in slots)
                if (s != null && s.model != null && s.who == key) return s;
            return null;
        }
    }
}
