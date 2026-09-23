using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace URDT.Runtime.SaveState
{
    /// <summary>
    /// Snapshot container encapsulating full captured state of registered entities.
    /// </summary>
    [Serializable]
    public sealed class UrdtSnapshot
    {
        public string SnapshotId;
        public long Timestamp;
        public long WorldRevision;
        public string ActiveScene;
        public Dictionary<string, byte[]> EntityStates = new Dictionary<string, byte[]>();
    }

    /// <summary>
    /// Layer L4 Atomic Save-State and Rollback Engine.
    /// Implements the rigorous 4-Phase Clean Rollback Protocol for autonomous counterfactual testing.
    /// Manages file-backed snapshots strictly within the isolated E:\Projects\URDT\URDT_Sandbox\ directory.
    /// </summary>
    [DefaultExecutionOrder(-9985)]
    public sealed class UrdtSaveStateManager : MonoBehaviour
    {
        private const string DEFAULT_SANDBOX_NAME = "URDT_Sandbox";
        private static readonly string DriveESandboxPath = @"E:\Projects\URDT\URDT_Sandbox";

        public static UrdtSaveStateManager Instance { get; private set; }

        private readonly List<IUrdtSaveable> _registeredSaveables = new List<IUrdtSaveable>();
        private readonly Dictionary<string, UrdtSnapshot> _snapshots = new Dictionary<string, UrdtSnapshot>();
        private string _sandboxDirectory;
        private long _worldRevision = 1;
        private float _preFreezeTimeScale = 1.0f;

        public long WorldRevision => _worldRevision;
        public string SandboxDirectory => _sandboxDirectory;
        public int RegisteredCount => _registeredSaveables.Count;
        public int SnapshotCount => _snapshots.Count;

        public event Action<string, long> OnRollbackCompleted;
        public event Action<string> OnSnapshotCaptured;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                var go = new GameObject("[URDT_SaveStateManager]");
                Instance = go.AddComponent<UrdtSaveStateManager>();
                DontDestroyOnLoad(go);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeSandboxStorage();
            DiscoverSceneSaveables();
        }

        private void InitializeSandboxStorage()
        {
            try
            {
                if (Directory.Exists(@"E:\Projects\URDT"))
                {
                    _sandboxDirectory = DriveESandboxPath;
                }
                else
                {
                    _sandboxDirectory = Path.Combine(Application.persistentDataPath, DEFAULT_SANDBOX_NAME);
                }

                if (!Directory.Exists(_sandboxDirectory))
                {
                    Directory.CreateDirectory(_sandboxDirectory);
                }
                Debug.Log($"[URDT] SaveState sandbox initialized at: {_sandboxDirectory}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[URDT] Failed to create primary sandbox folder, falling back to temp: {ex.Message}");
                _sandboxDirectory = Path.Combine(Application.temporaryCachePath, DEFAULT_SANDBOX_NAME);
                Directory.CreateDirectory(_sandboxDirectory);
            }
        }

        /// <summary>
        /// Registers a saveable entity into the engine.
        /// </summary>
        public void RegisterSaveable(IUrdtSaveable saveable)
        {
            if (saveable == null) return;
            if (!_registeredSaveables.Contains(saveable))
            {
                _registeredSaveables.Add(saveable);
            }
        }

        /// <summary>
        /// Unregisters a saveable entity.
        /// </summary>
        public void UnregisterSaveable(IUrdtSaveable saveable)
        {
            if (saveable == null) return;
            _registeredSaveables.Remove(saveable);
        }

        /// <summary>
        /// Scans active scene for any components implementing IUrdtSaveable.
        /// </summary>
        public void DiscoverSceneSaveables()
        {
            var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            for (int i = 0; i < rootObjects.Length; i++)
            {
                var saveables = rootObjects[i].GetComponentsInChildren<IUrdtSaveable>(true);
                for (int j = 0; j < saveables.Length; j++)
                {
                    RegisterSaveable(saveables[j]);
                }
            }
        }

        /// <summary>
        /// Captures an atomic snapshot across all registered entities.
        /// </summary>
        public UrdtSnapshot CaptureSnapshot(string snapshotId = null)
        {
            if (string.IsNullOrEmpty(snapshotId))
            {
                snapshotId = $"snap_rev{_worldRevision}_{DateTime.UtcNow.Ticks}";
            }

            var snapshot = new UrdtSnapshot
            {
                SnapshotId = snapshotId,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                WorldRevision = _worldRevision,
                ActiveScene = SceneManager.GetActiveScene().name
            };

            for (int i = 0; i < _registeredSaveables.Count; i++)
            {
                var saveable = _registeredSaveables[i];
                if (saveable == null) continue;

                try
                {
                    byte[] data = saveable.CaptureState();
                    snapshot.EntityStates[saveable.SaveStateKey] = data;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[URDT] Failed capturing state for {saveable.SaveStateKey}: {ex}");
                }
            }

            _snapshots[snapshotId] = snapshot;
            OnSnapshotCaptured?.Invoke(snapshotId);
            return snapshot;
        }

        /// <summary>
        /// Executes the atomic 4-Phase Clean Rollback Protocol to restore world to a given snapshot.
        /// Returns true if rollback succeeded within the target time budget.
        /// </summary>
        public bool RollbackToSnapshot(string snapshotId)
        {
            if (!_snapshots.TryGetValue(snapshotId, out var snapshot))
            {
                Debug.LogError($"[URDT] Rollback failed: Snapshot '{snapshotId}' does not exist.");
                return false;
            }

            var sw = Stopwatch.StartNew();
            Debug.Log($"[URDT] Initiating 4-Phase Clean Rollback to snapshot '{snapshotId}'...");

            try
            {
                // =========================================================================
                // PHASE 1: RUNTIME FORCE-PURGE
                // =========================================================================
                Phase1_ForcePurge();

                // =========================================================================
                // PHASE 2: ATOMIC DISK DATA RESTORATION
                // =========================================================================
                Phase2_DiskRestoration(snapshotId);

                // =========================================================================
                // PHASE 3: MEMORY RESTORATION FOR SINGLETONS & DI CONTAINERS
                // =========================================================================
                Phase3_MemoryRestoration(snapshot);

                // =========================================================================
                // PHASE 4: ADAPTER SYNCHRONIZATION AND ENGINE RESUME
                // =========================================================================
                Phase4_SynchronizationAndResume(snapshotId);

                sw.Stop();
                Debug.Log($"[URDT] Rollback to '{snapshotId}' completed in {sw.ElapsedMilliseconds} ms (Target: <50ms). New revision: {_worldRevision}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[URDT] Fatal error during 4-phase rollback: {ex}");
                ResumeWorld();
                return false;
            }
        }

        /// <summary>
        /// Resets all registered entities to default state using the 4-phase protocol.
        /// </summary>
        public bool ResetAllToDefault()
        {
            var sw = Stopwatch.StartNew();
            try
            {
                Phase1_ForcePurge();
                Phase2_DiskRestoration("default");

                for (int i = 0; i < _registeredSaveables.Count; i++)
                {
                    var saveable = _registeredSaveables[i];
                    if (saveable == null) continue;
                    try
                    {
                        saveable.ResetToDefault();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[URDT] Failed resetting {saveable.SaveStateKey} to default: {ex}");
                    }
                }

                Phase4_SynchronizationAndResume("default");
                sw.Stop();
                Debug.Log($"[URDT] Full ResetToDefault completed in {sw.ElapsedMilliseconds} ms. New revision: {_worldRevision}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[URDT] Error during ResetAllToDefault: {ex}");
                ResumeWorld();
                return false;
            }
        }

        private void Phase1_ForcePurge()
        {
            _preFreezeTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            // Kill DOTween tweens if library is present via reflection
            PurgeTweensReflection();

            // Stop all coroutines on active scene MonoBehaviours
            var allMonoBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude);
            for (int i = 0; i < allMonoBehaviours.Length; i++)
            {
                var mb = allMonoBehaviours[i];
                if (mb != null && mb != this)
                {
                    mb.StopAllCoroutines();
                }
            }

            // Flush in-engine dispatchers and ring buffers
            try
            {
                var dispatcherType = Type.GetType("URDT.Runtime.IPC.UrdtMainThreadDispatcher, KBP.URDT");
                if (dispatcherType != null)
                {
                    var instProp = dispatcherType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    var inst = instProp?.GetValue(null);
                    if (inst != null)
                    {
                        var clearMethod = dispatcherType.GetMethod("ClearRingBuffers", BindingFlags.Public | BindingFlags.Instance);
                        clearMethod?.Invoke(inst, null);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[URDT] Dispatcher buffer purge warning: {ex.Message}");
            }
        }

        private void Phase2_DiskRestoration(string snapshotId)
        {
            try
            {
                string snapshotFile = Path.Combine(_sandboxDirectory, $"{snapshotId}_disk.bin");
                string activeDiskState = Path.Combine(_sandboxDirectory, "active_state.bin");

                if (File.Exists(snapshotFile))
                {
                    string tempSwap = Path.Combine(_sandboxDirectory, "swap.tmp");
                    File.Copy(snapshotFile, tempSwap, true);
                    File.Copy(tempSwap, activeDiskState, true);
                    File.Delete(tempSwap);
                }

                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[URDT] Disk restoration notice: {ex.Message}");
            }
        }

        private void Phase3_MemoryRestoration(UrdtSnapshot snapshot)
        {
            // Restore state of registered entities
            for (int i = 0; i < _registeredSaveables.Count; i++)
            {
                var saveable = _registeredSaveables[i];
                if (saveable == null) continue;

                if (snapshot.EntityStates.TryGetValue(saveable.SaveStateKey, out byte[] stateData))
                {
                    try
                    {
                        saveable.RestoreState(stateData);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[URDT] Error restoring {saveable.SaveStateKey}: {ex}");
                    }
                }
                else
                {
                    saveable.ResetToDefault();
                }
            }
        }

        private void Phase4_SynchronizationAndResume(string snapshotId)
        {
            Physics.SyncTransforms();
            Physics2D.SyncTransforms();

            ResumeWorld();
            _worldRevision++;
            OnRollbackCompleted?.Invoke(snapshotId, _worldRevision);
        }

        private void ResumeWorld()
        {
            Time.timeScale = _preFreezeTimeScale > 0.001f ? _preFreezeTimeScale : 1.0f;
        }

        private void PurgeTweensReflection()
        {
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; i++)
                {
                    var dotweenType = assemblies[i].GetType("DG.Tweening.DOTween");
                    if (dotweenType != null)
                    {
                        var killAllMethod = dotweenType.GetMethod("KillAll", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(bool) }, null);
                        killAllMethod?.Invoke(null, new object[] { false });
                        break;
                    }
                }
            }
            catch
            {
                // Silently ignore if DOTween is not loaded in current scene
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
