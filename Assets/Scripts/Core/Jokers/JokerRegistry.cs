// PURPOSE: The catalogue of every joker that exists, keyed by DefId. The market will draw
// its offers from here, the debug UI grants from here, and a future save file will restore
// jokers by DefId through here - which is why DefIds must never be renamed.
//
// EXTENSION POINT: adding a joker = one Register call. Keep the list in the order the
// jokers were designed, not alphabetically, so the debug UI's number keys stay stable.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>Name, description and factory for one joker kind.</summary>
    public sealed class JokerDefinition
    {
        public string DefId { get; }
        public string DisplayName { get; }
        public string Description { get; }

        private readonly Func<Joker> factory;

        public JokerDefinition(string defId, string displayName, string description, Func<Joker> factory)
        {
            DefId = defId;
            DisplayName = displayName;
            Description = description;
            this.factory = factory;
        }

        public Joker Create()
        {
            return factory();
        }
    }

    /// <summary>Every implemented joker. Static: the catalogue is content, not run state.</summary>
    public static class JokerRegistry
    {
        private static readonly List<JokerDefinition> definitions = new List<JokerDefinition>();
        private static readonly Dictionary<string, JokerDefinition> byId =
            new Dictionary<string, JokerDefinition>();

        static JokerRegistry()
        {
            Register(() => new RenovasyonJoker());
            Register(() => new IadeJoker());
            Register(() => new InsiderJoker());
            Register(() => new CigJoker());
            Register(() => new DondurmaJoker());
            Register(() => new SiyamJoker());
            Register(() => new BereketJoker());
            Register(() => new HarcamaBonusuJoker());
            Register(() => new DomuzKumbarasiJoker());
            Register(() => new CimriKumbaraJoker());
            Register(() => new AltinKumbaraJoker());
            Register(() => new SeriTetikJoker());
            Register(() => new KaziCalismasiJoker());
            Register(() => new BuldozerJoker());
            Register(() => new RobotSupurgeJoker());
            Register(() => new KayitDefteriJoker());
            Register(() => new KentselDonusumJoker());
            Register(() => new BatakJoker());
            Register(() => new MidasJoker());
            Register(() => new ElmasKazmaJoker());
            Register(() => new TutusturJoker());
            Register(() => new YanginJoker());
            Register(() => new TaskinJoker());
            Register(() => new BuzlukJoker());
            Register(() => new SimyaJoker());
            Register(() => new DamlayaJoker());
            Register(() => new IhaleJoker());
            Register(() => new KaraDelikJoker());
            Register(() => new EnfeksiyonJoker());
        }

        /// <summary>All known jokers, in design order.</summary>
        public static IReadOnlyList<JokerDefinition> All
        {
            get { return definitions; }
        }

        public static JokerDefinition Get(string defId)
        {
            JokerDefinition definition;
            return byId.TryGetValue(defId, out definition) ? definition : null;
        }

        /// <summary>Creates a fresh instance, or null if the id is unknown.</summary>
        public static Joker Create(string defId)
        {
            JokerDefinition definition = Get(defId);
            return definition != null ? definition.Create() : null;
        }

        private static void Register(Func<Joker> factory)
        {
            Joker sample = factory();
            var definition = new JokerDefinition(sample.DefId, sample.DisplayName, sample.Description, factory);
            if (byId.ContainsKey(definition.DefId))
            {
                throw new InvalidOperationException("Duplicate joker DefId: " + definition.DefId);
            }
            definitions.Add(definition);
            byId.Add(definition.DefId, definition);
        }
    }
}
