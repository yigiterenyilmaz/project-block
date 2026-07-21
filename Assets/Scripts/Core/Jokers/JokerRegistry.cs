// PURPOSE: The static catalogue of every joker kind. The market and debug bar draw
// from here; a new joker is registered by adding its definition.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
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
