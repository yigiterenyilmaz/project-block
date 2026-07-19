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

        /// <summary>Whether this joker kind is legendary (at most one held at a time).</summary>
        public bool IsLegendary
        {
            get { return sample.IsLegendary; }
        }

        /// <summary>Graded rarity (from the rarity grader, via RarityTable). Drives market
        /// price and shop appearance odds; keyed by DefId.</summary>
        public Rarity Rarity
        {
            get { return RarityTable.For(DefId); }
        }

        /// <summary>Read LIVE off a sample instance, so it follows the Loc language.</summary>
        public string Description
        {
            get { return sample.Description; }
        }

        private readonly Joker sample;
        private readonly Func<Joker> factory;

        public JokerDefinition(Joker sample, Func<Joker> factory)
        {
            DefId = sample.DefId;
            DisplayName = sample.DisplayName;
            this.sample = sample;
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
            Register(() => new OryantasyonJoker());
            Register(() => new DezenformasyonJoker());
            Register(() => new ImitasyonJoker());
            Register(() => new FraksiyonJoker());
            Register(() => new ParazitJoker());
            Register(() => new PowerbankJoker());
            Register(() => new TutumlulukJoker());
            Register(() => new GenelTemizlikJoker());
            Register(() => new HafizaJoker());
            Register(() => new KolayParaJoker());
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
            var definition = new JokerDefinition(sample, factory);
            if (byId.ContainsKey(definition.DefId))
            {
                throw new InvalidOperationException("Duplicate joker DefId: " + definition.DefId);
            }
            definitions.Add(definition);
            byId.Add(definition.DefId, definition);
        }
    }
}
