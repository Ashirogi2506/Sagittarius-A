using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SagittariusA.Minigames
{
    // Put on a class that implements IMinigame (usually a MinigameBase MonoBehaviour) to register it by ID.
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class MinigameAttribute : Attribute
    {
        public string Id { get; }
        public MinigameAttribute(string id) { Id = id; }
    }

    // Maps minigame IDs to their implementation types and definitions.
    // New minigames register themselves with [Minigame("id")] plus a static "Definition" property; MinigameManager is never edited.
    public sealed class MinigameRegistry
    {
        public sealed class Entry
        {
            public string Id; public Type Type; public MinigameDefinition Definition;
        }

        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        public IEnumerable<Entry> Entries => entries.Values;

        public static MinigameRegistry FromAssemblies(params Assembly[] assemblies)
        {
            var registry = new MinigameRegistry();
            foreach (var assembly in assemblies.Distinct())
            {
                Type[] types;
                try { types = assembly.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
                foreach (var type in types)
                {
                    var attr = type.GetCustomAttribute<MinigameAttribute>();
                    if (attr == null || type.IsAbstract || !typeof(IMinigame).IsAssignableFrom(type)) continue;
                    var definition = type.GetProperty("Definition", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as MinigameDefinition;
                    registry.Register(attr.Id, type, definition);
                }
            }
            return registry;
        }

        public void Register(string id, Type type, MinigameDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Minigame ID is empty.");
            if (type == null || !typeof(IMinigame).IsAssignableFrom(type)) throw new ArgumentException($"{type} does not implement IMinigame.");
            if (entries.TryGetValue(id, out var existing) && existing.Type != type)
                throw new InvalidOperationException($"Minigame ID '{id}' is registered twice ({existing.Type.Name}, {type.Name}).");
            definition ??= new MinigameDefinition { Id = id, Title = id };
            definition.Id = id;
            entries[id] = new Entry { Id = id, Type = type, Definition = definition };
        }

        public bool TryResolve(string id, out Entry entry)
        {
            entry = null;
            return !string.IsNullOrWhiteSpace(id) && entries.TryGetValue(id, out entry);
        }
    }
}
