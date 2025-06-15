using System;
using System.Collections.Generic;
using System.Linq;

namespace Modrix.ModElements
{
    /// <summary>
    /// Registry for mod element generators. Supports runtime registration of any custom/special generator.
    /// </summary>
    public class ModElementGeneratorRegistry
    {
        private readonly List<IModElementGenerator> _generators = new();

        /// <summary>
        /// Register a new generator. Can be called at runtime for custom/special generators.
        /// </summary>
        public void Register(IModElementGenerator generator)
        {
            if (generator == null) throw new ArgumentNullException(nameof(generator));
            _generators.Add(generator);
        }

        public IEnumerable<IModElementGenerator> GetAll() => _generators;

        public IEnumerable<IModElementGenerator> GetByModLoader(string modLoader)
            => _generators.Where(g => g.SupportedModLoaders.Contains(modLoader, StringComparer.OrdinalIgnoreCase));

        public IEnumerable<IModElementGenerator> GetByMinecraftVersion(string version)
            => _generators.Where(g => g.SupportedMinecraftVersions.Contains(version));

        public IModElementGenerator? GetByName(string name)
            => _generators.FirstOrDefault(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
