using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace KBP.URDT.Inspect
{
    /// <summary>
    /// Read-only reflection snapshot of the gameplay components that share a GameObject with a beacon.
    /// Only public simple-typed members (bool, numbers, string, enum, Vector2/3) declared by game code
    /// are read, so a beacon exposes the live semantic state (IsLocked, SlotId, CurrentCount, ...) without
    /// the game having to push updates. Nothing is ever written back.
    /// </summary>
    public static class UrdtLiveStateReader
    {
        private const BindingFlags FLAGS = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        private static readonly Dictionary<Type, MemberInfo[]> CACHE = new Dictionary<Type, MemberInfo[]>();
        private static readonly List<MonoBehaviour> BUFFER = new List<MonoBehaviour>(8);

        public static Dictionary<string, object> Read(GameObject gameObject)
        {
            Dictionary<string, object> state = new Dictionary<string, object>();
            if (gameObject == null)
            {
                return state;
            }

            gameObject.GetComponents(BUFFER);
            for (int i = 0; i < BUFFER.Count; i++)
            {
                MonoBehaviour behaviour = BUFFER[i];
                if (behaviour == null || IsUrdtType(behaviour.GetType()))
                {
                    continue;
                }

                MemberInfo[] members = GetMembers(behaviour.GetType());
                for (int m = 0; m < members.Length; m++)
                {
                    string key = members[m].Name;
                    if (state.ContainsKey(key))
                    {
                        continue;
                    }

                    try
                    {
                        state[key] = Normalize(members[m] is PropertyInfo property
                            ? property.GetValue(behaviour)
                            : ((FieldInfo)members[m]).GetValue(behaviour));
                    }
                    catch (Exception exception)
                    {
                        state[key] = "E_READ: " + exception.GetType().Name;
                    }
                }
            }

            BUFFER.Clear();
            return state;
        }

        /// <summary>Returns the first readable boolean among the given member names, or the fallback.</summary>
        public static bool ReadBool(GameObject gameObject, bool fallback, params string[] names)
        {
            object value;
            return TryReadFirst(gameObject, names, out value) && value is bool flag ? flag : fallback;
        }

        public static int ReadInt(GameObject gameObject, int fallback, params string[] names)
        {
            object value;
            return TryReadFirst(gameObject, names, out value) && value is int number ? number : fallback;
        }

        private static bool TryReadFirst(GameObject gameObject, string[] names, out object value)
        {
            value = null;
            if (gameObject == null)
            {
                return false;
            }

            gameObject.GetComponents(BUFFER);
            try
            {
                for (int n = 0; n < names.Length; n++)
                {
                    for (int i = 0; i < BUFFER.Count; i++)
                    {
                        MonoBehaviour behaviour = BUFFER[i];
                        if (behaviour == null || IsUrdtType(behaviour.GetType()))
                        {
                            continue;
                        }

                        PropertyInfo property = behaviour.GetType().GetProperty(names[n], BindingFlags.Public | BindingFlags.Instance);
                        if (property != null && property.CanRead && property.GetIndexParameters().Length == 0)
                        {
                            value = property.GetValue(behaviour);
                            return true;
                        }
                    }
                }
            }
            catch (Exception)
            {
                value = null;
            }
            finally
            {
                BUFFER.Clear();
            }

            return false;
        }

        /// <summary>True for components that are not game code: URDT beacons and engine/UI/TMP components.</summary>
        private static bool IsUrdtType(Type type)
        {
            string ns = type.Namespace;
            if (ns == null)
            {
                return false;
            }

            if (ns.StartsWith("UnityEngine", StringComparison.Ordinal)
                || ns.StartsWith("UnityEditor", StringComparison.Ordinal)
                || ns.StartsWith("TMPro", StringComparison.Ordinal)
                || ns.StartsWith("Unity.", StringComparison.Ordinal))
            {
                return true;
            }

            return ns.StartsWith("KBP.URDT", StringComparison.Ordinal)
                && !ns.StartsWith("KBP.URDT.TestPoligon", StringComparison.Ordinal);
        }

        private static MemberInfo[] GetMembers(Type type)
        {
            MemberInfo[] cached;
            if (CACHE.TryGetValue(type, out cached))
            {
                return cached;
            }

            List<MemberInfo> members = new List<MemberInfo>();
            for (Type current = type; current != null && current != typeof(MonoBehaviour); current = current.BaseType)
            {
                if (IsUrdtType(current))
                {
                    break;
                }

                PropertyInfo[] properties = current.GetProperties(FLAGS);
                for (int i = 0; i < properties.Length; i++)
                {
                    if (properties[i].CanRead
                        && properties[i].GetIndexParameters().Length == 0
                        && IsSimple(properties[i].PropertyType))
                    {
                        members.Add(properties[i]);
                    }
                }

                FieldInfo[] fields = current.GetFields(FLAGS);
                for (int i = 0; i < fields.Length; i++)
                {
                    if (IsSimple(fields[i].FieldType))
                    {
                        members.Add(fields[i]);
                    }
                }
            }

            cached = members.ToArray();
            CACHE[type] = cached;
            return cached;
        }

        private static bool IsSimple(Type type)
        {
            return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(Vector2) || type == typeof(Vector3);
        }

        private static object Normalize(object value)
        {
            if (value is Enum)
            {
                return value.ToString();
            }

            if (value is Vector2 v2)
            {
                return new Dictionary<string, float> { { "x", v2.x }, { "y", v2.y } };
            }

            if (value is Vector3 v3)
            {
                return new Dictionary<string, float> { { "x", v3.x }, { "y", v3.y }, { "z", v3.z } };
            }

            return value;
        }
    }
}
