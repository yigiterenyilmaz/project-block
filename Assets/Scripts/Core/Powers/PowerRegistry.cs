// PURPOSE: The static catalogue of every power kind. The market and debug bar draw
// from here; a new power is registered by adding its definition.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>Every implemented power. Static: the catalogue is content, not run state.</summary>
    public static class PowerRegistry
    {
        private static readonly List<PowerDefinition> definitions = new List<PowerDefinition>();
        private static readonly Dictionary<string, PowerDefinition> byId =
            new Dictionary<string, PowerDefinition>();

        static PowerRegistry()
        {
            Register(() => new CimbizPower());
            Register(() => new CaprazlamaPower());
            Register(() => new EkoPower());
            Register(() => new CercevePower());
            Register(() => new BuldozerPower());
            Register(() => new KlonPower());
            Register(() => new BuyutecPower());
            Register(() => new TransferPower());
            Register(() => new MayinPower());
            Register(() => new HologramPower());
            Register(() => new HizliCekimSarjoruPower());
            Register(() => new BardaginBosTarafiPower());
            Register(() => new KumSaatiPower());
            Register(() => new OltaPower());
            Register(() => new TilsimPower());
            Register(() => new YatayEnflasyonPower());
            Register(() => new DikeyEnflasyonPower());
            Register(() => new HiperEnflasyonPower());
            Register(() => new AsirmaPower());
            Register(() => new YedeklemePower());
            Register(() => new SogukFuzyonPower());
            Register(() => new IkinciSansPower());
            Register(() => new TotemPower());
            Register(() => new BukulmePower());
            Register(() => new HileliZarPower());
            Register(() => new HalusinasyonPower());
            Register(() => new KarakterOlusturmaPower());
            Register(() => new RetroPower());
            Register(() => new BatakPower());
            Register(() => new KentselDonusumPower());
        }

        /// <summary>All known powers, in design order.</summary>
        public static IReadOnlyList<PowerDefinition> All
        {
            get { return definitions; }
        }

        public static PowerDefinition Get(string defId)
        {
            PowerDefinition definition;
            return byId.TryGetValue(defId, out definition) ? definition : null;
        }

        /// <summary>Creates a fresh instance, or null if the id is unknown.</summary>
        public static Power Create(string defId)
        {
            PowerDefinition definition = Get(defId);
            return definition != null ? definition.Create() : null;
        }

        private static void Register(Func<Power> factory)
        {
            Power sample = factory();
            var definition = new PowerDefinition(sample, factory);
            if (byId.ContainsKey(definition.DefId))
            {
                throw new InvalidOperationException("Duplicate power DefId: " + definition.DefId);
            }
            definitions.Add(definition);
            byId.Add(definition.DefId, definition);
        }
    }
}
