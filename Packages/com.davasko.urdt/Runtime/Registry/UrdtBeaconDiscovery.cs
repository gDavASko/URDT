using System.Collections.Generic;
using UnityEngine;

namespace KBP.URDT.Registry
{
    /// <summary>
    /// Collects beacons that wake up after the initial scene scan (runtime-built UI such as
    /// catalog cards, spawned gameplay objects). Registration is deferred to the server's
    /// main-thread tick so the creator has already assigned the beacon's TargetId.
    /// </summary>
    public static class UrdtBeaconDiscovery
    {
        private static readonly List<Component> PENDING = new List<Component>(64);
        private static readonly List<Component> DEFERRED = new List<Component>(64);

        public static void NotifyAwake(Component beacon)
        {
            if (beacon != null)
            {
                PENDING.Add(beacon);
            }
        }

        /// <summary>Registers every pending beacon that is active; keeps inactive ones for later ticks.</summary>
        public static void Drain(TestIdRegistry registry)
        {
            if (registry == null || PENDING.Count == 0)
            {
                return;
            }

            DEFERRED.Clear();
            for (int i = 0; i < PENDING.Count; i++)
            {
                Component beacon = PENDING[i];
                if (beacon == null)
                {
                    continue;
                }

                if (beacon.gameObject.activeInHierarchy)
                {
                    registry.Register(beacon.gameObject, RegistrationSource.Incremental);
                }
                else
                {
                    DEFERRED.Add(beacon);
                }
            }

            PENDING.Clear();
            PENDING.AddRange(DEFERRED);
            DEFERRED.Clear();
        }

        public static void Clear()
        {
            PENDING.Clear();
            DEFERRED.Clear();
        }
    }
}
