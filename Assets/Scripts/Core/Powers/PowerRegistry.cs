// PURPOSE: The catalogue of every power that exists, keyed by DefId - the power-side twin
// of JokerRegistry. The market will draw its power offers from here, the debug UI grants
// from here, and a future save file restores powers by DefId through here, which is why
// DefIds must never be renamed.
//
// EXTENSION POINT: adding a power = one Register call. Keep the list in design order so the
// debug UI's keys stay stable.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>Name, description and factory for one power kind.</summary>
    public sealed class PowerDefinition
    {
        public string DefId { get; }
        public string DisplayName { get; }

        /// <summary>Read LIVE off a sample instance, so it follows the Loc language.</summary>
        public string Description
        {
            get { return sample.Description; }
        }

        private readonly Power sample;
        private readonly Func<Power> factory;

        public PowerDefinition(Power sample, Func<Power> factory)
        {
            DefId = sample.DefId;
            DisplayName = sample.DisplayName;
            this.sample = sample;
            this.factory = factory;
        }

        public Power Create()
        {
            return factory();
        }
    }

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
