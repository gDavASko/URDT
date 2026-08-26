using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace KBP.URDT.Handlers
{
    /// <summary>
    /// Shared reader for pointer-command payloads. It keeps semantic commands
    /// compatible with the base click/drag addressing contract.
    /// </summary>
    internal static class InputPayloadReader
    {
        public static bool TryReadPath(UrdtRuntime runtime, JObject payload, List<Vector2> points)
        {
            points.Clear();

            JArray path = payload["path"] as JArray;
            if (path != null)
            {
                for (int i = 0; i < path.Count; i++)
                {
                    Vector2 point;
                    if (!PayloadReader.TryGetScreenPoint(runtime, path[i], out point))
                    {
                        points.Clear();
                        return false;
                    }

                    points.Add(point);
                }

                return points.Count >= 2;
            }

            Vector2 from;
            Vector2 to;
            JToken fromToken = payload["from"] ?? payload;
            JToken toToken = payload["to"];
            if (PayloadReader.TryGetScreenPoint(runtime, fromToken, out from)
                && PayloadReader.TryGetScreenPoint(runtime, toToken, out to))
            {
                points.Add(from);
                points.Add(to);
                return true;
            }

            points.Clear();
            return false;
        }

        public static bool TryReadPointList(UrdtRuntime runtime, JObject payload, List<Vector2> points)
        {
            points.Clear();

            JArray array = payload["points"] as JArray ?? payload["targets"] as JArray;
            if (array == null || array.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < array.Count; i++)
            {
                Vector2 point;
                if (!PayloadReader.TryGetScreenPoint(runtime, array[i], out point))
                {
                    points.Clear();
                    return false;
                }

                points.Add(point);
            }

            return points.Count > 0;
        }

        public static bool TryReadPathList(
            UrdtRuntime runtime,
            JObject payload,
            List<List<Vector2>> paths)
        {
            paths.Clear();

            JArray pathArray = payload["paths"] as JArray;
            if (pathArray == null || pathArray.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < pathArray.Count; i++)
            {
                JObject pathPayload = pathArray[i] as JObject;
                if (pathPayload == null)
                {
                    paths.Clear();
                    return false;
                }

                List<Vector2> path = new List<Vector2>(8);
                if (!TryReadPath(runtime, pathPayload, path))
                {
                    paths.Clear();
                    return false;
                }

                paths.Add(path);
            }

            return paths.Count > 0;
        }

        public static bool TryBuildDirectionalPath(
            UrdtRuntime runtime,
            JObject payload,
            List<Vector2> points,
            float defaultDistance)
        {
            points.Clear();

            if (TryReadPath(runtime, payload, points))
            {
                return true;
            }

            Vector2 origin;
            if (!PayloadReader.TryGetScreenPoint(runtime, payload, out origin))
            {
                return false;
            }

            string direction = PayloadReader.GetString(payload, "direction", "right");
            float distance = PayloadReader.GetFloat(payload, "distance", defaultDistance);
            Vector2 vector = DirectionToVector(direction) * distance;

            points.Add(origin);
            points.Add(origin + vector);
            return true;
        }

        public static Vector2 DirectionToVector(string direction)
        {
            switch (direction)
            {
                case "left":
                    return Vector2.left;
                case "up":
                    return Vector2.up;
                case "down":
                    return Vector2.down;
                case "right":
                default:
                    return Vector2.right;
            }
        }
    }
}
