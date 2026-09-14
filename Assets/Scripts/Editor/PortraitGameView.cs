// PURPOSE: PUTS THE PHONE RESOLUTIONS IN THE GAME VIEW so nobody has to add them by hand.
//
// WHY THIS EXISTS. Unity does not have a "portrait mode" to switch on: the way you see a phone
// layout is to give the Game view a portrait RESOLUTION, and out of the box it only offers
// landscape ones. That is a settings chore standing between the project and the thing it now
// supports, and a chore is exactly what gets skipped - so the sizes are registered on load and
// the dropdown simply has them.
//
// EDITOR ONLY. It lives in a folder called Editor, so it is stripped from every build by Unity's
// own rule; nothing here ships.
//
// IT REACHES INTO UNITY'S INTERNALS, and that is a deliberate, contained risk: GameViewSizes has
// no public API. Every step is guarded, and a failure logs one line and gives up rather than
// throwing - the sizes can still be added by hand, which is all that is lost. The menu item is
// there to re-run it after a Unity upgrade moves something.

using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ProjectBlock.EditorTools
{
    [InitializeOnLoad]
    public static class PortraitGameView
    {
        /// <summary>The shapes worth having: a common 16:9 phone, a tall modern one, and a
        /// portrait tablet - which is the aspect that squeezes the layout hardest.</summary>
        private static readonly (string Name, int W, int H)[] Sizes =
        {
            ("Phone Portrait 1080x1920", 1080, 1920),
            ("Phone Tall 1080x2340", 1080, 2340),
            ("Tablet Portrait 1536x2048", 1536, 2048),
        };

        static PortraitGameView()
        {
            // Deferred: the Game view's size list is not reliably ready during a static
            // constructor on a domain reload.
            EditorApplication.delayCall += () => Add(false);
        }

        [MenuItem("Block Bonk/Add portrait game view sizes")]
        private static void AddFromMenu()
        {
            Add(true);
        }

        private static void Add(bool loud)
        {
            try
            {
                Assembly editor = typeof(UnityEditor.Editor).Assembly;
                Type sizesType = editor.GetType("UnityEditor.GameViewSizes");
                Type sizeType = editor.GetType("UnityEditor.GameViewSize");
                Type sizeTypeEnum = editor.GetType("UnityEditor.GameViewSizeType");
                Type groupTypeEnum = editor.GetType("UnityEditor.GameViewSizeGroupType");
                if (sizesType == null || sizeType == null || sizeTypeEnum == null
                    || groupTypeEnum == null)
                {
                    Report(loud, "Unity's game view size types are not where they used to be.");
                    return;
                }

                Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                object sizes = singleton.GetProperty("instance",
                    BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (sizes == null)
                {
                    Report(loud, "could not reach the game view size list.");
                    return;
                }

                object currentGroupType = sizesType
                    .GetProperty("currentGroupType", BindingFlags.Public | BindingFlags.Instance)
                    ?.GetValue(sizes);
                object group = sizesType
                    .GetMethod("GetGroup", BindingFlags.Public | BindingFlags.Instance)
                    ?.Invoke(sizes, new[] { currentGroupType });
                if (group == null)
                {
                    Report(loud, "could not reach the current platform's size group.");
                    return;
                }

                Type groupType = group.GetType();
                MethodInfo getTotal = groupType.GetMethod("GetTotalCount");
                MethodInfo getSize = groupType.GetMethod("GetGameViewSize");
                MethodInfo addCustom = groupType.GetMethod("AddCustomSize");
                if (getTotal == null || getSize == null || addCustom == null)
                {
                    Report(loud, "the size group does not expose what it used to.");
                    return;
                }

                int total = (int)getTotal.Invoke(group, null);
                int added = 0;
                foreach ((string name, int w, int h) in Sizes)
                {
                    bool already = false;
                    for (int i = 0; i < total; i++)
                    {
                        object existing = getSize.Invoke(group, new object[] { i });
                        string label = existing?.GetType()
                            .GetProperty("baseText")?.GetValue(existing) as string;
                        if (label == name)
                        {
                            already = true;
                            break;
                        }
                    }
                    if (already)
                    {
                        continue;
                    }
                    // GameViewSize(GameViewSizeType.FixedResolution, width, height, name)
                    object fixedResolution = Enum.Parse(sizeTypeEnum, "FixedResolution");
                    ConstructorInfo make = sizeType.GetConstructor(
                        new[] { sizeTypeEnum, typeof(int), typeof(int), typeof(string) });
                    if (make == null)
                    {
                        Report(loud, "the size constructor changed shape.");
                        return;
                    }
                    addCustom.Invoke(group,
                        new[] { make.Invoke(new object[] { fixedResolution, w, h, name }) });
                    added++;
                }

                if (added > 0 || loud)
                {
                    Debug.Log(added > 0
                        ? "Game view: " + added + " portrait size(s) added. "
                          + "Pick one from the resolution dropdown to see the phone layout."
                        : "Game view: the portrait sizes are already there.");
                }
            }
            catch (Exception e)
            {
                Report(loud, e.Message);
            }
        }

        private static void Report(bool loud, string why)
        {
            if (loud)
            {
                Debug.LogWarning("Could not add the portrait game view sizes (" + why
                    + "). Add one by hand: Game view resolution dropdown -> + -> "
                    + "Fixed Resolution, 1080 x 1920.");
            }
        }
    }
}
