// PURPOSE: Keeps a world-space text's dark outline copies saying the same thing as the text.
//
// THE OUTLINE IS FOUR EXTRA TextMeshes (see ViewUtil.AddTextOutline) and a TextMesh's content is
// a plain field a dozen callers write straight to - the draw-pile count every turn, the boss
// lab's rows, a contract's value. Built once from the text as it was at creation, the copies
// would go stale the first time any of those changed, and a stale outline is not a subtle bug:
// it is the old word still on screen, in black, under the new one.
//
// SO THEY FOLLOW rather than being pushed. One string compare and one colour compare per text
// per frame, which is nothing next to what it buys - no call site has to know the outline exists,
// and none of them can forget it.

using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Sits on the INK and drives the outline copies under it.</summary>
    public sealed class TextOutlineFx : MonoBehaviour
    {
        private TextMesh ink;
        private TextMesh[] copies;
        private Color outlineInk;
        private string lastText;
        private Color lastColor;
        private MeshRenderer inkRenderer;
        private MeshRenderer[] copyRenderers;
        private int lastOrder = int.MinValue;

        public void Bind(TextMesh text, TextMesh[] outlineCopies, Color ink_)
        {
            ink = text;
            copies = outlineCopies;
            outlineInk = ink_;
            lastText = text != null ? text.text : null;
            lastColor = text != null ? text.color : Color.white;
            inkRenderer = text != null ? text.GetComponent<MeshRenderer>() : null;
            copyRenderers = new MeshRenderer[outlineCopies.Length];
            for (int i = 0; i < outlineCopies.Length; i++)
            {
                copyRenderers[i] = outlineCopies[i] != null
                    ? outlineCopies[i].GetComponent<MeshRenderer>() : null;
            }
        }

        private void LateUpdate()
        {
            if (ink == null || copies == null)
            {
                return;
            }
            // THE ORDER FOLLOWS TOO. A card re-sorts its text after it is built (the hand fan
            // flattens every card to one order, a hovered card is boosted), and copies left at
            // the order they were born with ended up OVER their own ink - a label that looked
            // dark and smudged for no visible reason.
            if (inkRenderer != null && inkRenderer.sortingOrder != lastOrder)
            {
                lastOrder = inkRenderer.sortingOrder;
                for (int i = 0; i < copyRenderers.Length; i++)
                {
                    if (copyRenderers[i] != null)
                    {
                        copyRenderers[i].sortingOrder = lastOrder - 1;
                    }
                }
            }
            bool textChanged = ink.text != lastText;
            bool colorChanged = ink.color != lastColor;
            if (!textChanged && !colorChanged)
            {
                return;
            }
            lastText = ink.text;
            lastColor = ink.color;
            for (int i = 0; i < copies.Length; i++)
            {
                if (copies[i] == null)
                {
                    continue;
                }
                if (textChanged)
                {
                    copies[i].text = lastText;
                }
                if (colorChanged)
                {
                    // The outline follows the ink's ALPHA, so a text fading out fades its outline
                    // with it - a popup that faded to nothing over a black ghost of itself would
                    // be the worst of both.
                    copies[i].color = new Color(outlineInk.r, outlineInk.g, outlineInk.b,
                        outlineInk.a * lastColor.a);
                }
            }
        }
    }
}
