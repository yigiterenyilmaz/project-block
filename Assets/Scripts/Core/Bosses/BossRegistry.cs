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

        static BossRegistry()
        {
            Register(() => new UfukBoss());
            Register(() => new KuleBoss());
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
