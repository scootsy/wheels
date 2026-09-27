using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Tabletop.Presentation
{
    /// <summary>
    /// Sculpted figurines for the table (D-033): a KayKit body per unit, posed once and cast in the table's metal so
    /// its bronze / silver / gold finish still shows rank. Missing entries keep the primitive miniature.
    /// </summary>
    [CreateAssetMenu(fileName = "Figurines", menuName = "Tabletop/Figurine Set")]
    public sealed class FigurineSet : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("Unit id (striker, caster, ranger...).")]
            public string unitId;
            public GameObject model;
            [Tooltip("Hand-slot items to keep (others are hidden).")]
            public List<string> keep = new List<string>();
            public bool hat = true;
            public bool cape = true;
            [Tooltip("Animation whose pose the figurine is cast in.")]
            public AnimationClip pose;
            public float poseTime = 0.3f;
        }

        public List<Entry> entries = new List<Entry>();

        public Entry Find(string unitId)
        {
            foreach (var e in entries) if (e != null && e.unitId == unitId && e.model != null) return e;
            return null;
        }

        /// <summary>
        /// Builds the posed figure under <paramref name="parent"/>, standing on local y = 0, <paramref name="height"/>
        /// tall, every renderer drawn with <paramref name="metal"/> (renderers are returned for rank tinting).
        /// </summary>
        public static List<Renderer> Cast(Entry e, Transform parent, float height, Material metal)
        {
            var holder = new GameObject("Sculpt").transform;
            holder.SetParent(parent, false);
            var go = Instantiate(e.model, holder, false);
            go.name = e.model.name;
            foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = mr.name;
                bool on = n.Contains("Helmet") || n.Contains("Hat") ? e.hat : n.Contains("Cape") ? e.cape : e.keep.Contains(n);
                mr.gameObject.SetActive(on);
            }
            if (e.pose != null)
            {
                var animator = go.GetComponent<Animator>();
                if (animator == null) animator = go.AddComponent<Animator>();
                var graph = PlayableGraph.Create("Figurine pose");
                var output = AnimationPlayableOutput.Create(graph, "Pose", animator);
                var clip = AnimationClipPlayable.Create(graph, e.pose);
                clip.SetTime(e.poseTime);
                clip.SetApplyFootIK(false);
                output.SetSourcePlayable(clip);
                graph.Evaluate();
                graph.Destroy();
                animator.enabled = false; // keep the cast pose
            }
            var renderers = new List<Renderer>();
            var bounds = new Bounds();
            bool any = false;
            // Hidden accessories are cast in the same metal too (nothing on the table keeps a pack material).
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = metal;
                r.sharedMaterials = mats;
                if (!r.gameObject.activeInHierarchy) continue;
                if (r is SkinnedMeshRenderer smr)
                {
                    smr.updateWhenOffscreen = true;
                    var baked = new Mesh();
                    smr.BakeMesh(baked, true);
                    Grow(ref bounds, ref any, holder.worldToLocalMatrix * smr.transform.localToWorldMatrix, baked.bounds);
                    Destroy(baked);
                }
                else if (r.GetComponent<MeshFilter>() is MeshFilter mf && mf.sharedMesh != null)
                    Grow(ref bounds, ref any, holder.worldToLocalMatrix * mf.transform.localToWorldMatrix, mf.sharedMesh.bounds);
                renderers.Add(r);
            }
            float s = height / Mathf.Max(0.001f, bounds.size.y);
            go.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            holder.localScale = Vector3.one * s;
            return renderers;
        }

        private static void Grow(ref Bounds b, ref bool any, Matrix4x4 m, Bounds local)
        {
            for (int i = 0; i < 8; i++)
            {
                var c = local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var p = m.MultiplyPoint3x4(c);
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                else b.Encapsulate(p);
            }
        }
    }
}
