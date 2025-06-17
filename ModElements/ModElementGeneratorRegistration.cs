using System;
using Modrix.ModElements.Generators;

namespace Modrix.ModElements
{
    public static class ModElementGeneratorRegistration
    {
        public static void RegisterAll(ModElementGeneratorRegistry registry)
        {
            registry.Register(new ItemModElementGenerator());
            // Register other generators here
        }
    }
}
