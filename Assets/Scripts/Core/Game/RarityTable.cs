// PURPOSE: Baked DefId -> Rarity of every joker and power, GENERATED from
// Tools/RarityGrader/rarities.json. Regenerate when grades change. Unknown ids
// are Common. Keyed by stable DefId, so it survives display-name changes.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>DefId -> rarity, baked from the rarity grader. Unknown ids are Common.</summary>
    public static class RarityTable
    {
        private static readonly Dictionary<string, Rarity> byDefId =
            new Dictionary<string, Rarity>
        {
            { "renovasyon", Rarity.Common },
            { "iade", Rarity.Common },
            { "insider", Rarity.Common },
            { "cig", Rarity.Common },
            { "dondurma", Rarity.Common },
            { "siyam", Rarity.Common },
            { "bereket", Rarity.Common },
            { "harcama_bonusu", Rarity.Common },
            { "domuz_kumbarasi", Rarity.Common },
            { "cimri_kumbara", Rarity.Common },
            { "altin_kumbara", Rarity.Rare },
            { "seri_tetik", Rarity.Rare },
            { "kazi_calismasi", Rarity.Rare },
            { "robot_supurge", Rarity.Rare },
            { "deprem", Rarity.Rare },
            { "hazine", Rarity.Rare },
            { "meydan_okuma", Rarity.Common },
            { "kayit_defteri", Rarity.Common },
            { "midas", Rarity.Common },
            { "elmas_kazma", Rarity.Common },
            { "tutustur", Rarity.Common },
            { "yangin", Rarity.Common },
            { "taskin", Rarity.Common },
            { "buzluk", Rarity.Common },
            { "simya", Rarity.Rare },
            { "damlaya", Rarity.Common },
            { "ihale", Rarity.Common },
            { "kara_delik", Rarity.Rare },
            { "enfeksiyon", Rarity.Common },
            { "oryantasyon", Rarity.Legendary },
            { "dezenformasyon", Rarity.Legendary },
            { "imitasyon", Rarity.Legendary },
            { "fraksiyon", Rarity.Legendary },
            { "parazit", Rarity.Rare },
            { "powerbank", Rarity.Common },
            { "tutumluluk", Rarity.Common },
            { "genel_temizlik", Rarity.Common },
            { "hafiza", Rarity.Common },
            { "kolay_para", Rarity.Common },
            { "cimbiz", Rarity.Common },
            { "caprazlama", Rarity.Common },
            { "eko", Rarity.Rare },
            { "cerceve", Rarity.Common },
            { "klon", Rarity.Common },
            { "buyutec", Rarity.Common },
            { "transfer", Rarity.Common },
            { "mayin", Rarity.Rare },
            { "hologram", Rarity.Common },
            { "hizli_cekim_sarjoru", Rarity.Common },
            { "bardagin_bos_tarafi", Rarity.Rare },
            { "kum_saati", Rarity.Rare },
            { "olta", Rarity.Rare },
            { "tilsim", Rarity.Common },
            { "yatay_enflasyon", Rarity.Common },
            { "dikey_enflasyon", Rarity.Common },
            { "hiper_enflasyon", Rarity.Common },
            { "asirma", Rarity.Common },
            { "yedekleme", Rarity.Common },
            { "soguk_fuzyon", Rarity.Common },
            { "ikinci_sans", Rarity.Common },
            { "totem", Rarity.Common },
            { "bukulme", Rarity.Rare },
            { "hileli_zar", Rarity.Common },
            { "buldozer", Rarity.Common },
            { "kentsel_donusum", Rarity.Rare },
            { "halusinasyon", Rarity.Legendary },
            { "karakter_olusturma", Rarity.Legendary },
            { "retro", Rarity.Legendary },
            { "batak", Rarity.Legendary },
        };

        /// <summary>The graded rarity of a joker/power DefId (Common if ungraded).</summary>
        public static Rarity For(string defId)
        {
            Rarity r;
            return defId != null && byDefId.TryGetValue(defId, out r) ? r : Rarity.Common;
        }
    }
}
