using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KBP.URDT.Lifecycle
{
    /// <summary>
    /// Scene-level test isolation (reset_state, TZ §8.7). M5 supports the <c>scene</c>
    /// depth — a hard reload of the active scene, optionally re-seeding RNG. Services/
    /// process depths are deferred (M6) and reported unsupported. Emits a scene-loaded
    /// signal so the server can push a <c>scene_loaded</c> event.
    /// </summary>
    public sealed class SceneLifecycleManager : IDisposable
    {
        public const string DEPTH_SCENE = "scene";
        public const string DEPTH_SERVICES = "services";

        /// <summary>Fired after a scene load with (scene name, build index).</summary>
        public event Action<string, int> SceneLoaded;

        private IUrdtServicesReset _servicesReset;
        private bool _subscribed;

        public void EnableSceneEvents()
        {
            if (_subscribed)
            {
                return;
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            _subscribed = true;
        }

        /// <summary>Registers the optional game services-reset hook (enables <c>depth:"services"</c>).</summary>
        public void SetServicesReset(IUrdtServicesReset servicesReset)
        {
            _servicesReset = servicesReset;
        }

        /// <summary>
        /// Reloads the active scene. <c>scene</c> depth reloads only; <c>services</c> depth also
        /// invokes the game reset hook (TZ §8.7). Returns false if a <c>services</c> reset is
        /// requested but no hook is registered (→ E_UNSUPPORTED), or for unknown depths.
        /// </summary>
        public bool ResetScene(string depth, int? seed)
        {
            bool isServices = string.Equals(depth, DEPTH_SERVICES, StringComparison.Ordinal);
            bool isScene = string.IsNullOrEmpty(depth) || string.Equals(depth, DEPTH_SCENE, StringComparison.Ordinal);

            if (!isScene && !isServices)
            {
                return false;
            }

            if (isServices && _servicesReset == null)
            {
                return false;
            }

            if (seed.HasValue)
            {
                UnityEngine.Random.InitState(seed.Value);
            }

            Scene active = SceneManager.GetActiveScene();
            SceneManager.LoadScene(active.buildIndex >= 0 ? active.buildIndex : 0, LoadSceneMode.Single);

            if (isServices)
            {
                _servicesReset.ResetServices();
            }

            return true;
        }

        public void Dispose()
        {
            if (_subscribed)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                _subscribed = false;
            }

            SceneLoaded = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Action<string, int> handler = SceneLoaded;
            if (handler != null)
            {
                handler(scene.name, scene.buildIndex);
            }
        }
    }
}
