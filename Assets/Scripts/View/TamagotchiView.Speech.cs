// PURPOSE: "Tamagotchi" SPEAKING - what the pet says comes out of the PET, as a small speech bubble
// with its tail on the pet's mouth, never as a line of system text in the message bar. The bar keeps
// the FACTS (what was eaten, what the rule is - Say); the bubble is the creature's own voice (Speak).
//
// TWO BUBBLES, ONE FAMILY. Normal: soft and round, cream going to pale pink, a berry line, a little
// curved tail; it pops in 0.85 -> 1.04 -> 1. Furious: the same bubble gone wrong - less round, an
// uneven edge, dirty cream, a deep raspberry line, bold capitals; it lands 0.75 -> 1.08 -> 0.98 -> 1
// with a few degrees of turn and never quite stops trembling. Both leave the same way: a fade and a
// few pixels of drift upward. The art is two baked pieces each (TamagotchiArt: bubble / bubble_tail,
// _furious), the body drawn nine-sliced so its line keeps one weight whatever the text's length.
//
// IT DOES NOT TALK MUCH. A line is one or two short rows. A bubble that is not part of a scripted
// moment (an idle remark) is dropped if one was shown in the last 5-10 seconds or one is showing;
// scripted ones (the request, a stage change, the fury's "BEN ALIRIM.") always appear and replace
// what is there. The acting that goes with a line is the caller's - a belly rub with "Acıktım...",
// a paw at the plate with "Bunu istiyorum!", head down and eyes up with "BEN ALIRIM.".
//
// WHERE. Above the pet on the side away from the board when there is room; otherwise beside it, on
// the screen's interior side; never over the board, the plates or the hand if any candidate avoids
// them, and always inside the screen. The tail starts on the bubble's edge nearest the mouth and
// points at it every frame, so the bubble follows a pet that leans, lunges or moves house.
// EXTENSION POINT: a new line is one Speak call beside the acting it belongs to.

using ProjectBlock.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectBlock.View
{
    public sealed partial class TamagotchiView
    {
        public const int SpeechOrder = 47;

        private Transform bubbleRoot;
        private SpriteRenderer bubbleBody;
        private SpriteRenderer bubbleTail;
        private TextMesh bubbleText;
        private MeshRenderer bubbleTextRenderer;
        private bool bubbleFurious;
        private float bubbleBornAt = -100f;
        private float bubbleDieAt = -100f;
        private float bubbleHold;
        private bool bubbleShown;
        private Vector2 bubbleSize;
        private Vector2 bubbleCentre;
        private float lastBubbleAt = -100f;
        private float nextBubbleGap = 6.5f;
        private string bubbleLine;

        /// <summary>The line showing now (debug), or null.</summary>
        public string SpeechShowing
        {
            get { return bubbleShown ? bubbleLine : null; }
        }

        /// <summary>The world rect the bubble occupies (debug).</summary>
        public Rect SpeechRect
        {
            get { return new Rect(bubbleCentre - bubbleSize * 0.5f, bubbleSize); }
        }

        /// <summary>The lab's switch: no bubbles (the old message-bar-only behaviour).</summary>
        public bool LabNoSpeech;

        private void BuildSpeech()
        {
            bubbleRoot = new GameObject("PetSpeech").transform;
            bubbleRoot.SetParent(transform, false);
            var group = bubbleRoot.gameObject.AddComponent<SortingGroup>();
            group.sortingOrder = SpeechOrder;
            var tail = new GameObject("Tail");
            tail.transform.SetParent(bubbleRoot, false);
            bubbleTail = tail.AddComponent<SpriteRenderer>();
            bubbleTail.sortingOrder = 0;
            var body = new GameObject("Body");
            body.transform.SetParent(bubbleRoot, false);
            bubbleBody = body.AddComponent<SpriteRenderer>();
            bubbleBody.sortingOrder = 1;
            bubbleText = ViewUtil.MakeText3D(bubbleRoot, "Line", Vector2.zero, string.Empty, 96, 0.02f,
                Color.white, 3, TextAnchor.MiddleCenter);
            bubbleText.alignment = TextAlignment.Center;
            bubbleTextRenderer = bubbleText.GetComponent<MeshRenderer>();
            bubbleRoot.gameObject.SetActive(false);
        }

        private void HideSpeech()
        {
            bubbleShown = false;
            if (bubbleRoot != null)
            {
                bubbleRoot.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// The pet says a line. <paramref name="scripted"/> lines always appear; a passing remark
        /// is dropped when it has spoken in the last few seconds. Returns true when it was shown.
        /// </summary>
        private bool Speak(string tr, string en, bool scripted, float hold = -1f)
        {
            if (!shown || LabNoSpeech || bubbleRoot == null || labFast)
            {
                return false;
            }
            if (!scripted && (bubbleShown || clock - lastBubbleAt < nextBubbleGap))
            {
                return false;
            }
            string line = Loc.Pick(en, tr);
            if (string.IsNullOrEmpty(line))
            {
                return false;
            }
            bubbleFurious = furyNow > 0.5f || (act.Fury ?? 0f) > 0.5f;
            bubbleLine = line;
            bubbleBornAt = clock;
            bubbleHold = hold > 0f ? hold : Tuning.BubbleHold + 0.035f * line.Length;
            bubbleDieAt = clock + bubbleHold;
            bubbleShown = true;
            lastBubbleAt = clock;
            nextBubbleGap = Rand(Mathf.Max(5f, Tuning.BubbleMinGap - 1.5f), Tuning.BubbleMinGap + 3.5f);

            bubbleBody.sprite = TamagotchiArt.GetSliced(bubbleFurious ? "bubble_furious" : "bubble");
            bubbleBody.drawMode = SpriteDrawMode.Sliced;
            bubbleTail.sprite = TamagotchiArt.Get(bubbleFurious ? "bubble_tail_furious" : "bubble_tail");

            // one or two short rows
            string text = Wrap(line);
            bubbleText.text = text;
            bubbleText.fontStyle = bubbleFurious ? FontStyle.Bold : FontStyle.Normal;
            bubbleText.color = bubbleFurious ? new Color(0.33f, 0.05f, 0.16f) : new Color(0.42f, 0.14f, 0.27f);
            // the type is sized to the pet: a line is about a fifth of a body unit tall
            float lineHeight = (bubbleFurious ? 0.215f : 0.19f) * S;
            bubbleText.characterSize = lineHeight * 10f / bubbleText.fontSize * 0.86f;
            bubbleRoot.gameObject.SetActive(true);
            bubbleRoot.localScale = Vector3.one;
            bubbleRoot.rotation = Quaternion.identity;
            Vector3 size = bubbleTextRenderer.bounds.size;
            int rows = text.IndexOf('\n') >= 0 ? 2 : 1;
            float w = Mathf.Max(size.x, 0.5f * S) + 0.42f * S;
            float h = Mathf.Max(size.y, lineHeight * rows) + 0.3f * S;
            bubbleSize = new Vector2(w, h);
            Sound(bubbleFurious ? PetSound.BubbleFurious : PetSound.Bubble);
            PlaceBubble(true);
            return true;
        }

        /// <summary>The line it is saying goes at once (a move, a new beat that would talk over it).</summary>
        private void DismissSpeech()
        {
            if (bubbleShown)
            {
                bubbleDieAt = Mathf.Min(bubbleDieAt, clock);
            }
        }

        private static string Wrap(string line)
        {
            if (line.Length <= 13 || line.IndexOf(' ') < 0)
            {
                return line;
            }
            // break at the space nearest the middle
            int mid = line.Length / 2;
            int best = -1;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == ' ' && (best < 0 || Mathf.Abs(i - mid) < Mathf.Abs(best - mid)))
                {
                    best = i;
                }
            }
            return best < 0 ? line : line.Substring(0, best) + "\n" + line.Substring(best + 1);
        }

        /// <summary>Where the bubble goes this frame: the first candidate that covers nothing it
        /// should not, else the least bad one; always inside the screen.</summary>
        private void PlaceBubble(bool choose)
        {
            Vector2 mouth = rig.MouthWorld;
            Vector2 head = rig.HeadTopWorld;
            Vector2 up = HomeUp;
            if (Home.Inverted)
            {
                up = Vector2.down;
            }
            float gap = 0.2f * S;
            // away from the board in x
            float away = Mathf.Abs(mouth.x - Anchors.BoardCentre.x) > 0.2f ? Mathf.Sign(mouth.x - Anchors.BoardCentre.x) : -F;
            Vector2[] candidates =
            {
                // over the head, a little to the side away from the board
                head + up * (gap + bubbleSize.y * 0.5f) + new Vector2(away * 0.25f * S, 0f),
                // over the head, toward the interior
                head + up * (gap + bubbleSize.y * 0.5f) - new Vector2(away * 0.45f * bubbleSize.x, 0f),
                // beside it, on the interior side, at head height
                (Vector2)rig.BellyWorld + new Vector2(-away * (0.75f * S + bubbleSize.x * 0.5f), 0.55f * S),
                // beside it, on the outer side
                (Vector2)rig.BellyWorld + new Vector2(away * (0.75f * S + bubbleSize.x * 0.5f), 0.55f * S),
                // under it (a pet hanging from the top edge)
                mouth - up * (0.7f * S + bubbleSize.y * 0.5f)
            };
            Rect screen = Anchors.Screen;
            float margin = 0.12f;
            Vector2 best = candidates[0];
            float bestCost = float.MaxValue;
            for (int i = 0; i < candidates.Length; i++)
            {
                Vector2 c = candidates[i];
                c.x = Mathf.Clamp(c.x, screen.xMin + bubbleSize.x * 0.5f + margin, screen.xMax - bubbleSize.x * 0.5f - margin);
                c.y = Mathf.Clamp(c.y, screen.yMin + bubbleSize.y * 0.5f + margin, screen.yMax - bubbleSize.y * 0.5f - margin);
                var r = new Rect(c - bubbleSize * 0.5f, bubbleSize);
                float cost = Overlap(r, Anchors.Board) * 4f + Overlap(r, PlatesWorldRect()) * 2f
                    + Overlap(r, rig.WorldBounds) * 3f + i * 0.02f;
                // pushed back by the screen's edge is worse than fitting
                cost += (c - candidates[i]).magnitude * 0.6f;
                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = c;
                }
            }
            if (choose)
            {
                bubbleCentre = best;
            }
            else
            {
                // it follows the pet without hopping between candidates every frame
                bubbleCentre = Damp(bubbleCentre, best, 0.08f);
            }
        }

        private static float Overlap(Rect a, Rect b)
        {
            float w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
            float h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
            return w > 0f && h > 0f ? w * h : 0f;
        }

        private static float Overlap(Rect a, Bounds b)
        {
            return Overlap(a, new Rect(b.min.x, b.min.y, b.size.x, b.size.y));
        }

        /// <summary>The plates' own world rect (nothing when they are put away).</summary>
        private Rect PlatesWorldRect()
        {
            if (plates.Count == 0 || platesFolded || State.Furious)
            {
                return new Rect(0f, 0f, 0f, 0f);
            }
            float w = PlateArtWidth * PlateWorldScale;
            float h = PlateArtHeight * PlateWorldScale;
            float gap = 0.1f * S;
            Vector2 size = Home.PlatesVertical ? new Vector2(w, plates.Count * h + gap) : new Vector2(plates.Count * w + gap, h);
            return new Rect(Home.PlatesCentre - size * 0.5f, size);
        }

        private void TickSpeech()
        {
            if (bubbleRoot == null || !bubbleShown)
            {
                return;
            }
            float age = clock - bubbleBornAt;
            float dying = clock - bubbleDieAt;
            if (dying > 0.24f || !shown)
            {
                HideSpeech();
                return;
            }
            PlaceBubble(false);

            // IN: 0.85 -> 1.04 -> 1, or the furious 0.75 -> 1.08 -> 0.98 -> 1 with a few degrees
            float scale;
            float turn = 0f;
            if (bubbleFurious)
            {
                float t = Mathf.Clamp01(age / 0.24f);
                scale = t < 0.45f ? Mathf.Lerp(0.75f, 1.08f, EaseOut(t / 0.45f))
                    : t < 0.75f ? Mathf.Lerp(1.08f, 0.98f, Smooth((t - 0.45f) / 0.3f))
                    : Mathf.Lerp(0.98f, 1f, Smooth((t - 0.75f) / 0.25f));
                turn = -4f * (1f - Smooth(t)) + 0.9f * Noise(clock * 7f, 21);
                scale *= 1f + 0.008f * Noise(clock * 9f, 22);
            }
            else
            {
                float t = Mathf.Clamp01(age / 0.18f);
                scale = t < 0.6f ? Mathf.Lerp(0.85f, 1.04f, EaseOut(t / 0.6f)) : Mathf.Lerp(1.04f, 1f, Smooth((t - 0.6f) / 0.4f));
            }
            // OUT: a fade and a few pixels of drift upward
            float alpha = Mathf.Clamp01(age / 0.07f);
            Vector2 drift = Vector2.zero;
            if (dying > 0f)
            {
                float d = Mathf.Clamp01(dying / 0.24f);
                alpha *= 1f - Smooth(d);
                drift = new Vector2(0f, Px(7f) * EaseOut(d));
            }
            alpha *= Mathf.Clamp01(pose.Alpha);

            bubbleRoot.position = bubbleCentre + drift;
            bubbleRoot.rotation = Quaternion.Euler(0f, 0f, turn);
            bubbleRoot.localScale = new Vector3(scale, scale, 1f);
            // the nine-sliced body: its drawn size is in its own units, the art's corners untouched
            float unit = S;
            bubbleBody.transform.localScale = new Vector3(unit, unit, 1f);
            bubbleBody.size = new Vector2(bubbleSize.x / unit + 0.1f, bubbleSize.y / unit + 0.12f);
            bubbleBody.color = new Color(1f, 1f, 1f, alpha);
            Color ink = bubbleText.color;
            ink.a = alpha;
            bubbleText.color = ink;
            bubbleText.transform.localPosition = new Vector3(0f, 0.012f * S, 0f);

            // THE TAIL: from the edge of the bubble nearest the mouth, pointing at it
            Vector2 centre = bubbleCentre + drift;
            Vector2 to = (Vector2)rig.MouthWorld + HomeUp * (Home.Inverted ? -0.14f : 0.14f) * S - centre;
            Vector2 half = bubbleSize * 0.5f;
            Vector2 dir = to.sqrMagnitude > 0.0001f ? to.normalized : Vector2.down;
            // where that direction leaves the bubble's box
            float kx = Mathf.Abs(dir.x) > 0.0001f ? half.x / Mathf.Abs(dir.x) : float.MaxValue;
            float ky = Mathf.Abs(dir.y) > 0.0001f ? half.y / Mathf.Abs(dir.y) : float.MaxValue;
            float reach = Mathf.Min(kx, ky);
            Vector2 exit = dir * reach;
            // keep it off the rounded corners
            exit.x = Mathf.Clamp(exit.x, -half.x + 0.22f * S, half.x - 0.22f * S);
            exit.y = Mathf.Clamp(exit.y, -half.y + 0.02f * S, half.y - 0.02f * S);
            bool inside = to.magnitude < reach + 0.05f * S;
            bubbleTail.enabled = !inside;
            bubbleTail.transform.position = centre + exit * 0.93f;
            Vector2 aim = ((Vector2)rig.MouthWorld - (centre + exit)).normalized;
            bubbleTail.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.x, -aim.y) * Mathf.Rad2Deg);
            bubbleTail.transform.localScale = new Vector3(S * 1.25f / Mathf.Max(0.01f, scale), S * 1.25f / Mathf.Max(0.01f, scale), 1f);
            bubbleTail.color = new Color(1f, 1f, 1f, alpha);
        }

        // ================================================================== the lines

        /// <summary>A passing remark for the mood it is in (the idle scheduler asks; most of the
        /// time the answer is silence).</summary>
        private void MaybeIdleRemark()
        {
            if (bubbleShown || clock - lastBubbleAt < nextBubbleGap || queue.Current != null && queue.Current.Priority > PetPresentationPriority.Idle)
            {
                return;
            }
            // rare: about one idle in five gets a word
            if (idleRng.NextDouble() > (State.Furious ? 0.16 : 0.2))
            {
                return;
            }
            if (State.Furious)
            {
                switch (idleRng.Next(4))
                {
                    case 0: Speak("ACIM.", "HUNGRY.", false); break;
                    case 1: Speak("YETER.", "ENOUGH.", false); break;
                    case 2: Speak("VER ŞUNU.", "GIVE IT.", false); break;
                    default: Speak("HEPSİNİ YERİM.", "I'LL EAT IT ALL.", false); break;
                }
                return;
            }
            if (State.Satisfied)
            {
                if (idleRng.Next(2) == 0)
                {
                    Speak("Mmm...", "Mmm...", false);
                }
                return;
            }
            switch (idleRng.Next(State.Pending == 1 ? 4 : 3))
            {
                case 0: Speak("Acıktım...", "I'm hungry...", false); break;
                case 1: Speak("Hmm?", "Hmm?", false); break;
                case 2: Speak("Nam?", "Nom?", false); break;
                default: Speak("Bir tane daha!", "One more!", false); break;
            }
        }
    }
}
