using System.Collections.Generic;
using KBP.URDT.Driver;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace KBP.URDT.Handlers
{
    /// <summary>
    /// Converts URDT state to protocol JSON, mapping common Unity value types to plain
    /// JSON objects (so a slice value like a Vector3 does not serialize its whole API).
    /// </summary>
    public static class UrdtJson
    {
        public static JObject NodeToJson(NodeState state)
        {
            if (state == null)
            {
                return null;
            }

            JObject json = new JObject
            {
                ["testId"] = state.TestId,
                ["handle"] = state.Handle.Value,
                ["name"] = state.Name,
                ["activeInHierarchy"] = state.ActiveInHierarchy
            };

            if (state.ScreenPosition.HasValue)
            {
                json["screenPosition"] = new JObject
                {
                    ["x"] = state.ScreenPosition.Value.x,
                    ["y"] = state.ScreenPosition.Value.y
                };
            }

            if (state.Components != null)
            {
                JObject components = new JObject();
                foreach (KeyValuePair<string, Dictionary<string, object>> component in state.Components)
                {
                    JObject slice = new JObject();
                    foreach (KeyValuePair<string, object> field in component.Value)
                    {
                        slice[field.Key] = ValueToToken(field.Value);
                    }

                    components[component.Key] = slice;
                }

                json["components"] = components;
            }

            return json;
        }

        public static JToken ValueToToken(object value)
        {
            if (value == null)
            {
                return JValue.CreateNull();
            }

            if (value is Vector3 v3)
            {
                return new JObject { ["x"] = v3.x, ["y"] = v3.y, ["z"] = v3.z };
            }

            if (value is Vector2 v2)
            {
                return new JObject { ["x"] = v2.x, ["y"] = v2.y };
            }

            if (value is Color color)
            {
                return new JObject { ["r"] = color.r, ["g"] = color.g, ["b"] = color.b, ["a"] = color.a };
            }

            if (value is bool || value is int || value is long || value is float || value is double || value is string)
            {
                return new JValue(value);
            }

            return new JValue(value.ToString());
        }
    }
}
