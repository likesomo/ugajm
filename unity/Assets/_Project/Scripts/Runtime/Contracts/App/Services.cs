using System;
using System.Collections.Generic;

namespace SevenSteps.Contracts
{
    /// <summary>
    /// Minimal service locator. Only the Boot bootstrap (and tests) register; everyone else reads.
    /// Registered services: ISaveStore, SaveSystem, ITelemetry, AppFlow, LaunchOptions, AppConfig (docs/architecture.md 11장).
    /// Rule: Core never touches this. Look services up once (Awake/Start), not every frame.
    /// </summary>
    public static class Services
    {
        static readonly Dictionary<Type, object> map = new Dictionary<Type, object>();

        public static void Register<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            map[typeof(T)] = service;
        }

        public static T Get<T>() where T : class =>
            map.TryGetValue(typeof(T), out var s) ? (T)s : throw new InvalidOperationException($"Service {typeof(T).Name} not registered (did the Boot scene run?)");

        public static bool TryGet<T>(out T service) where T : class
        {
            bool ok = map.TryGetValue(typeof(T), out var s);
            service = ok ? (T)s : null;
            return ok;
        }

        public static bool Has<T>() where T : class => map.ContainsKey(typeof(T));

        /// Tests, and the editor when entering play mode with domain reload disabled.
        public static void Clear() => map.Clear();
    }
}
