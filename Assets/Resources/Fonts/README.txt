Fredoka (SIL OFL 1.1) - Bold for buttons and headings, SemiBold for everything else.
Loaded by ViewUtil.UiFont / UiFontBold. OFL.txt is the licence and must stay next to them.

THESE TWO FILES ARE PATCHED and differ from the upstream download. Fredoka ships without
Turkish: I, g, G, s, S with their Turkish marks (U+0130 U+011E U+011F U+015E U+015F) have no
glyphs, so "Sasirtmaca" and "kayitli" came out with holes in them. The marks themselves were
already drawn by the type designer, so the patch only assembles the five composite glyphs and
adds the cmap entries - nothing is hand-drawn.

Tools/FontPatch/add_turkish_glyphs.py is that patch. RE-RUN IT if the font is ever updated:
a fresh download from Google Fonts will be missing the same five letters again.

The upstream copyright line declares no Reserved Font Name, so the patched files keep the
Fredoka name. They stay under the OFL, as the licence requires.
