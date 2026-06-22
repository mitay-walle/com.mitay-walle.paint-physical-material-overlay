using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace LevelDesign
{
    [CreateAssetMenu]
    public class PaintPhysicsMaterialOverlayPalette : ScriptableObject
    {
        public List<Entry2D> Entries2D = new();
        public List<Entry3D> Entries3D = new();

        [Serializable]
        public abstract class EntryBase
        {
            public Color Color = Color.white;
            public abstract Object Value { get; }
            public string Name => Value ? Value.name : "null";
        }

        [Serializable]
        public sealed class Entry3D : EntryBase
        {
            [FormerlySerializedAs("_material")]
            public PhysicsMaterial Material;
            public override Object Value => Material;
        }

        [Serializable]
        public sealed class Entry2D : EntryBase
        {
            [FormerlySerializedAs("_material")]
            public PhysicsMaterial2D Material;
            public override Object Value => Material;
        }
    }

    public static class Utility
    {
        public static Color SetAlpha(this Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}