// PURPOSE: The static catalogue of every boss round kind. GameSession draws from here when
// a flagged round starts; a new boss is registered by adding its definition.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>Every implemented boss round. Static: the catalogue is content, not run state.</summary>
    public static class BossRegistry
    {
        private static readonly List<BossDefinition> definitions = new List<BossDefinition>();
        private static readonly Dictionary<string, BossDefinition> byId =
            new Dictionary<string, BossDefinition>();

        // Registered in DESIGN order (the order the bosses were specified), which is also the
        // order the draw pool walks - so a seed's boss sequence only shifts when content is
        // added or reordered, never as a side effect of unrelated changes.
        static BossRegistry()
        {
            Register(() => new AlikoymaBoss());
            Register(() => new MapusBoss());
            Register(() => new VanilyaBoss());
            Register(() => new FedaBoss());
            Register(() => new TukenmislikBoss());
            Register(() => new AnarsiBoss());
            Register(() => new HarcamaVergisiBoss());
            Register(() => new OzelTuketimVergisiBoss());
            Register(() => new UfukBoss());
            Register(() => new KuleBoss());
            Register(() => new OburlukBoss());
            Register(() => new TerslikBoss());
            Register(() => new TitizlikBoss());
            Register(() => new CanaGelecegineMalaBoss());
            Register(() => new TasVeSopaBoss());
            Register(() => new CikmazBoss());
            Register(() => new AlzheimerBoss());
            Register(() => new YuruyenMerdivenBoss());
            Register(() => new KarantinaBoss());
            Register(() => new AlacakaranlikBoss());
            Register(() => new EnflasyonBoss());
            Register(() => new HiclikBoss());
            Register(() => new SaatciBoss());
            Register(() => new KitlikBoss());
            Register(() => new MerkezkacBoss());
            Register(() => new DortKutupBoss());
            Register(() => new KangrenBoss());
            Register(() => new BilinmezlikBoss());
            Register(() => new RehinPuanBoss());
            Register(() => new BurokrasiBatagiBoss());
            Register(() => new BulParayiBoss());
        }

        /// <summary>All known bosses, in design order.</summary>
        public static IReadOnlyList<BossDefinition> All
        {
            get { return definitions; }
        }

        public static BossDefinition Get(string defId)
        {
            BossDefinition definition;
            return byId.TryGetValue(defId, out definition) ? definition : null;
        }

        /// <summary>Creates a fresh instance, or null if the id is unknown.</summary>
        public static BossRound Create(string defId)
        {
            BossDefinition definition = Get(defId);
            return definition != null ? definition.Create() : null;
        }

        private static void Register(Func<BossRound> factory)
        {
            BossRound sample = factory();
            var definition = new BossDefinition(sample, factory);
            if (byId.ContainsKey(definition.DefId))
            {
                throw new InvalidOperationException("Duplicate boss DefId: " + definition.DefId);
            }
            definitions.Add(definition);
            byId.Add(definition.DefId, definition);
        }
    }
}
