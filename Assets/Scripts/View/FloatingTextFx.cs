// PURPOSE: A short-lived popup text ("COMBO x3!", "CLEAN SWEEP!"): scale-punches in,
// floats upward, fades out, destroys itself. Spawn-and-forget.
//
// AND THEY QUEUE. Three popups about the same turn used to be spawned in the same frame at
// hand-picked Y offsets, which is a layout that only works for exactly the popups it was tuned
// for: the moment a turn had a combo AND a multiplier line AND a joker naming itself, they
// landed on top of each other and none of them could be read.
//
// So a popup WAITS for the space it wants. A request starts when nothing else is standing in
// that space - either because nothing is near it, or because whatever was there has risen far
// enough to be out of the way. That is the rule the eye already expects from a stack of
// messages, and it costs nothing when there is only one.
//
// IT IS PER PLACE, NOT ONE GLOBAL LINE. Popups far apart on screen do not block each other -
// a payout at the joker bar has nothing to do with a combo over the board, and making them
// take turns would delay messages for no reason. Only popups that would actually collide wait.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Self-animating floating text popup.</summary>
    public sealed class FloatingTextFx : MonoBehaviour
    {
        private const float Duration = 0.95f;
        private const float RiseDistance = 0.7f;

        /// <summary>How near two popups have to be, in world units, to be considered the same
        /// place. A little over a popup's own line height - close enough to collide.</summary>
        private const float ClearRadius = 0.85f;

        /// <summary>How far through its life a popup has to be before the next one may take its
        /// place. It has risen and started to fade by then, so the two read as a sequence rather
        /// than as a pile.</summary>
        private const float ClearFraction = 0.42f;

        /// <summary>A popup that has waited this long goes anyway. Without it a long burst could
        /// hold a message back until after the moment it was about - and a late message is worse
        /// than a crowded one.</summary>
        private const float MaxWait = 1.4f;

        private TextMesh textMesh;
        private Color baseColor;
        private Vector3 basePosition;
        private float age;

        /// <summary>Everything currently on screen, so a new request can ask what is in its way.
        /// Static because popups are spawned from a dozen places that know nothing of each
        /// other, which is exactly why they used to collide.</summary>
        private static readonly List<FloatingTextFx> Live = new List<FloatingTextFx>();

        private sealed class Pending
        {
            public Transform Parent;
            public Vector2 Position;
            public string Text;
            public Color Color;
            public int FontSize;
            public float CharacterSize;
            public float Waited;
        }

        private static readonly List<Pending> Queue = new List<Pending>();

        /// <summary>Pumps the queue. Driven by the popups themselves and by the pump object
        /// below, so a queue with nothing on screen still drains.</summary>
        private static FloatingTextPump pump;

        public static void Spawn(Transform parent, Vector2 position, string text,
            Color color, int fontSize, float characterSize)
        {
            var request = new Pending
            {
                Parent = parent,
                Position = position,
                Text = text,
                Color = color,
                FontSize = fontSize,
                CharacterSize = characterSize
            };
            // A popup with nothing in its way goes straight out - the common case, and it must
            // not cost a frame.
            if (PlaceIsClear(parent, position))
            {
                Emit(request);
                return;
            }
            Queue.Add(request);
            EnsurePump(parent);
        }

        /// <summary>True when nothing on screen is close enough to this spot to collide with a
        /// popup put there, or what is there has moved on.</summary>
        private static bool PlaceIsClear(Transform parent, Vector2 position)
        {
            for (int i = Live.Count - 1; i >= 0; i--)
            {
                FloatingTextFx other = Live[i];
                if (other == null)
                {
                    Live.RemoveAt(i);
                    continue;
                }
                if (other.age / Duration >= ClearFraction)
                {
                    continue; // risen out of the way
                }
                // Compared in WORLD space: the two may hang off different parents (the board,
                // the controller) and their local positions are not in the same frame.
                Vector3 mine = parent != null
                    ? parent.TransformPoint(new Vector3(position.x, position.y, 0f))
                    : new Vector3(position.x, position.y, 0f);
                if (Vector2.Distance(mine, other.transform.position) < ClearRadius)
                {
                    return false;
                }
            }
            return true;
        }

        private static void Emit(Pending request)
        {
            TextMesh tm = ViewUtil.MakeText3D(request.Parent, "FloatingText", request.Position,
                request.Text, request.FontSize, request.CharacterSize, request.Color, 45,
                TextAnchor.MiddleCenter);
            FloatingTextFx fx = tm.gameObject.AddComponent<FloatingTextFx>();
            fx.textMesh = tm;
            fx.baseColor = request.Color;
            fx.basePosition = tm.transform.localPosition;
            Live.Add(fx);
        }

        /// <summary>Lets anything through whose place has come free, oldest first.</summary>
        internal static void PumpQueue(float deltaTime)
        {
            for (int i = 0; i < Queue.Count; i++)
            {
                Pending request = Queue[i];
                request.Waited += deltaTime;
                if (request.Parent == null)
                {
                    Queue.RemoveAt(i);
                    return; // the scene it belonged to is gone
                }
                if (!PlaceIsClear(request.Parent, request.Position)
                    && request.Waited < MaxWait)
                {
                    continue;
                }
                Queue.RemoveAt(i);
                Emit(request);
                // ONE PER PUMP: letting two through in the same frame would put them in the same
                // place, which is the whole thing this exists to stop.
                return;
            }
        }

        private static void EnsurePump(Transform near)
        {
            if (pump != null || near == null)
            {
                return;
            }
            var go = new GameObject("FloatingTextPump");
            go.transform.SetParent(near.root, false);
            pump = go.AddComponent<FloatingTextPump>();
        }

        private void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / Duration);
            transform.localPosition = basePosition + new Vector3(0f, RiseDistance * t, 0f);
            float punch = 1f + 0.25f * Mathf.Sin(Mathf.Min(t * 3f, 1f) * Mathf.PI);
            transform.localScale = new Vector3(punch, punch, 1f);
            Color color = baseColor;
            color.a = 1f - t * t;
            textMesh.color = color;
            if (t >= 1f)
            {
                Live.Remove(this);
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            Live.Remove(this);
        }
    }

    /// <summary>Drains the popup queue. One of these exists once anything has ever queued; it
    /// costs nothing while the queue is empty and is what lets a popup wait for its place even
    /// when no other popup is alive to tick it.</summary>
    public sealed class FloatingTextPump : MonoBehaviour
    {
        private void Update()
        {
            FloatingTextFx.PumpQueue(Time.deltaTime);
        }
    }
}
