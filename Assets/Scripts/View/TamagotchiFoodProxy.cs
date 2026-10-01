// PURPOSE: Whatever "Tamagotchi" is EATING, drawn as a copy of its own - the brief's
// TamagotchiFoodProxy. The real thing has already left (Core resolved the meal before the first
// frame of it plays): a fed card is out of the run, a snatched joker out of the bar, a snacked card
// out of its pile. So the pet never touches a real UI object; it eats a proxy that LOOKS like it -
// the card's own face built by CardVisual, a joker's or power's own frame and icon, a chunk of the
// board's own floor - and the proxy carries the bite.
//
// THE BITE IS THE SHADER'S (Resources/Shaders/TamagotchiBite): one shared material, and per
// renderer a property block with the world-space bite line at the pet's mouth. SetBite moves that
// line; the view moves the proxy across it a chomp at a time, so the edge left behind is always the
// scalloped edge of the last bite. Progress is 0 / 0.35 / 0.70 / 1 for a proper meal.
//
// POOLED where it can be. A joker, a power or a chunk of floor is a couple of sprites, so those
// proxies are rented and handed back (Rent / Recycle) and a four-chunk bite allocates nothing
// after the first. A CARD's face is built by CardVisual for that one card - exactly as every card
// flight in the game builds one (the deal, the burn, the shuffle) - so that kind is made and
// destroyed.
//
// Without the shader (or at LOW quality) the proxy falls back to a stepped shrink: still a
// mouthful at a time, never a single-frame disappearance.
// EXTENSION POINT: a new kind of food is a new factory beside ForCard / ForItem / ForChunk.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed class TamagotchiFoodProxy : MonoBehaviour
    {
        // ---- the brief's proxy data ----
        public int SourceId;
        public Color Colour = Color.white;
        public CardValueTier Tier;
        public Rect InitialRect;
        public float BiteProgress;

        /// <summary>Size at scale 1, world units - what a tooth is measured against.</summary>
        public Vector2 Size;

        private readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        private CardVisual card;
        private MaterialPropertyBlock block;
        private static Material biteMaterial;
        private static bool biteLooked;

        private static readonly int BiteId = Shader.PropertyToID("_Bite");
        private static readonly int OnId = Shader.PropertyToID("_On");
        private static readonly int SoftId = Shader.PropertyToID("_Soft");

        /// <summary>True when bites are cut by the shader; false = the stepped-shrink fallback.</summary>
        public bool ShaderBites { get; private set; }

        private static Material BiteMaterial
        {
            get
            {
                if (!biteLooked)
                {
                    biteLooked = true;
                    Shader shader = Shader.Find("ProjectBlock/TamagotchiBite");
                    if (shader == null)
                    {
                        shader = Resources.Load<Shader>("Shaders/TamagotchiBite");
                    }
                    if (shader != null && shader.isSupported)
                    {
                        biteMaterial = new Material(shader) { name = "TamagotchiBite (shared)" };
                    }
                }
                return biteMaterial;
            }
        }

        /// <summary>A block card's own face.</summary>
        public static TamagotchiFoodProxy ForCard(Transform parent, BlockCard card, int order, bool bites)
        {
            var go = new GameObject("Food_" + (card != null ? card.Id : 0));
            go.transform.SetParent(parent, false);
            var proxy = go.AddComponent<TamagotchiFoodProxy>();
            proxy.SourceId = card != null ? card.Id : -1;
            proxy.card = CardVisual.Create(go.transform, "Face", card, true, false, Vector2.zero, order);
            proxy.card.ForEachSprite(r => proxy.renderers.Add(r));
            proxy.Size = new Vector2(CardVisual.BodyWidth, CardVisual.BodyHeight);
            proxy.Colour = CardColour(card);
            proxy.Prepare(bites);
            return proxy;
        }

        // ---- the pool of sprite proxies (items and chunks)
        private static readonly Stack<TamagotchiFoodProxy> pool = new Stack<TamagotchiFoodProxy>();
        private static Transform poolRoot;
        private int used;

        private static TamagotchiFoodProxy Rent(Transform parent, string name)
        {
            TamagotchiFoodProxy proxy = null;
            while (pool.Count > 0 && proxy == null)
            {
                proxy = pool.Pop();
            }
            if (proxy == null)
            {
                var go = new GameObject(name);
                proxy = go.AddComponent<TamagotchiFoodProxy>();
            }
            proxy.name = name;
            proxy.transform.SetParent(parent, false);
            proxy.transform.localPosition = Vector3.zero;
            proxy.transform.localRotation = Quaternion.identity;
            proxy.transform.localScale = Vector3.one;
            proxy.gameObject.SetActive(true);
            proxy.BiteProgress = 0f;
            proxy.used = 0;
            return proxy;
        }

        /// <summary>The next sprite of a rented proxy (made the first time, reused after).</summary>
        private SpriteRenderer Next(string name, Sprite sprite, int order)
        {
            SpriteRenderer r;
            if (used < renderers.Count && renderers[used] != null)
            {
                r = renderers[used];
                r.gameObject.SetActive(true);
            }
            else
            {
                r = Sprite(transform, name, sprite, order);
                if (used < renderers.Count)
                {
                    renderers[used] = r;
                }
                else
                {
                    renderers.Add(r);
                }
            }
            used++;
            r.name = name;
            r.sprite = sprite;
            r.sortingOrder = order;
            r.color = Color.white;
            r.transform.localPosition = Vector3.zero;
            r.transform.localRotation = Quaternion.identity;
            r.transform.localScale = Vector3.one;
            return r;
        }

        private void HideUnused()
        {
            for (int i = used; i < renderers.Count; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].gameObject.SetActive(false);
                }
            }
        }

        /// <summary>Hands a proxy back: a sprite proxy to the pool, a card's face to nothing.</summary>
        public static void Recycle(TamagotchiFoodProxy proxy)
        {
            if (proxy == null)
            {
                return;
            }
            if (proxy.card != null)
            {
                Destroy(proxy.gameObject);
                return;
            }
            if (poolRoot == null)
            {
                poolRoot = new GameObject("TamagotchiProxyPool").transform;
            }
            proxy.gameObject.SetActive(false);
            proxy.transform.SetParent(poolRoot, false);
            pool.Push(proxy);
        }

        /// <summary>A joker or a power: its own card frame with its own icon on it.</summary>
        public static TamagotchiFoodProxy ForItem(Transform parent, bool joker, string defId, Vector2 size,
            int order, bool bites)
        {
            TamagotchiFoodProxy proxy = Rent(parent, "Food_" + defId);
            Sprite frame = ViewUtil.CardSprite(joker ? "card_joker" : "card_power");
            SpriteRenderer body = proxy.Next("Frame", frame, order);
            if (frame != null)
            {
                Vector2 art = frame.bounds.size;
                body.transform.localScale = new Vector3(size.x / Mathf.Max(0.001f, art.x),
                    size.y / Mathf.Max(0.001f, art.y), 1f);
            }
            else
            {
                body.sprite = ViewUtil.RoundedSprite;
                body.color = joker ? new Color(0.20f, 0.22f, 0.30f) : new Color(0.28f, 0.22f, 0.16f);
                body.transform.localScale = new Vector3(size.x, size.y, 1f);
            }
            Sprite iconSprite = joker ? ViewUtil.JokerIcon(defId) : ViewUtil.PowerIcon(defId);
            if (iconSprite != null)
            {
                SpriteRenderer icon = proxy.Next("Icon", iconSprite, order + 1);
                float fit = Mathf.Min(size.x * 0.72f / Mathf.Max(0.001f, iconSprite.bounds.size.x),
                    size.y * 0.55f / Mathf.Max(0.001f, iconSprite.bounds.size.y));
                icon.transform.localScale = new Vector3(fit, fit, 1f);
                icon.transform.localPosition = new Vector3(0f, joker ? size.y * 0.06f : 0f, 0f);
            }
            proxy.HideUnused();
            proxy.Size = size;
            proxy.Colour = joker ? new Color(0.93f, 0.80f, 0.45f) : new Color(0.95f, 0.62f, 0.30f);
            proxy.Prepare(bites);
            return proxy;
        }

        /// <summary>A chunk of the board's own floor (a bite of the arena).</summary>
        public static TamagotchiFoodProxy ForChunk(Transform parent, Sprite sprite, Color colour, float size,
            int order, bool bites)
        {
            TamagotchiFoodProxy proxy = Rent(parent, "Chunk");
            SpriteRenderer r = proxy.Next("Chunk", sprite, order);
            r.color = colour;
            float art = sprite != null ? Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y) : 1f;
            r.transform.localScale = new Vector3(size / art, size / art, 1f);
            proxy.HideUnused();
            proxy.Size = new Vector2(size, size);
            proxy.Colour = colour;
            proxy.Prepare(bites);
            return proxy;
        }

        private static SpriteRenderer Sprite(Transform parent, string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            return r;
        }

        private void Prepare(bool bites)
        {
            if (block == null)
            {
                block = new MaterialPropertyBlock();
            }
            Material shared = bites ? BiteMaterial : null;
            ShaderBites = shared != null;
            foreach (SpriteRenderer r in renderers)
            {
                if (r == null)
                {
                    continue;
                }
                if (ShaderBites)
                {
                    r.sharedMaterial = shared;
                }
                // a rented proxy starts whole, whatever its last meal left on it
                r.GetPropertyBlock(block);
                block.SetFloat(OnId, 0f);
                block.SetFloat(SoftId, 0.004f);
                r.SetPropertyBlock(block);
            }
        }

        /// <summary>
        /// Places the bite line at <paramref name="mouth"/>: everything beyond it (up the
        /// <paramref name="angle"/>'s normal) is gone and three tooth marks hang below it. The
        /// teeth are sized off the proxy, so a big card gets a big mouthful.
        /// </summary>
        public void SetBite(Vector2 mouth, float angleRadians, bool on)
        {
            if (!ShaderBites)
            {
                return;
            }
            float width = Size.x * Mathf.Abs(transform.lossyScale.x);
            float tooth = Mathf.Max(0.012f, width / 5.2f);
            var v = new Vector4(mouth.x, mouth.y, angleRadians, tooth);
            foreach (SpriteRenderer r in renderers)
            {
                if (r == null)
                {
                    continue;
                }
                r.GetPropertyBlock(block);
                block.SetVector(BiteId, v);
                block.SetFloat(OnId, on ? 1f : 0f);
                r.SetPropertyBlock(block);
            }
            if (on && card != null)
            {
                card.SetTextVisible(false);
            }
        }

        public void SetAlpha(float alpha)
        {
            if (card != null)
            {
                card.SetAlpha(alpha);
                return;
            }
            foreach (SpriteRenderer r in renderers)
            {
                if (r != null)
                {
                    Color c = r.color;
                    c.a = alpha;
                    r.color = c;
                }
            }
        }

        /// <summary>Moves every part of the proxy into a sorting band starting at
        /// <paramref name="order"/> - used when it is handed from the flight layer to the pet's paws.</summary>
        public void SetOrder(int order)
        {
            if (card != null)
            {
                card.SetFlattenedOrder(order);
                return;
            }
            for (int i = 0; i < renderers.Count; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].sortingOrder = order + i;
                }
            }
        }

        /// <summary>The proxy's bounds in the world (debug: ShowFoodProxyBounds).</summary>
        public Bounds WorldBounds
        {
            get
            {
                var b = new Bounds(transform.position, Vector3.zero);
                bool any = false;
                foreach (SpriteRenderer r in renderers)
                {
                    if (r == null)
                    {
                        continue;
                    }
                    if (!any)
                    {
                        b = r.bounds;
                        any = true;
                    }
                    else
                    {
                        b.Encapsulate(r.bounds);
                    }
                }
                return b;
            }
        }

        /// <summary>The colour the bits a bite throws off are drawn in: the card's element, or its
        /// face when it is plain.</summary>
        public static Color CardColour(BlockCard card)
        {
            if (card == null)
            {
                return new Color(0.88f, 0.86f, 0.80f);
            }
            IReadOnlyList<BlockElement> shown = ViewUtil.ShownElements(card);
            if (shown.Count > 0)
            {
                return ViewUtil.ElementColor(shown[shown.Count - 1]);
            }
            return Color.Lerp(ViewUtil.ColorForCard(card.Id), new Color(0.88f, 0.86f, 0.80f), 0.35f);
        }
    }
}
