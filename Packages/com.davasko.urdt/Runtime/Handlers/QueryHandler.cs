using System.Collections.Generic;
using KBP.URDT.Driver;
using KBP.URDT.Transport;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace KBP.URDT.Handlers
{
    /// <summary>Handles <c>query</c>: returns the state of registered objects matching a selector
    /// (<c>byComponent</c>/<c>byTag</c>/<c>byName</c>; empty selector = all).</summary>
    public sealed class QueryHandler : ICommandHandler
    {
        private readonly UrdtRuntime _runtime;
        private readonly List<Handle> _buffer = new List<Handle>(32);

        public QueryHandler(UrdtRuntime runtime)
        {
            _runtime = runtime;
        }

        public Response Handle(Command command)
        {
            JObject payload = PayloadReader.AsObject(command.Payload);
            JObject selector = payload["selector"] as JObject ?? payload;
            string byComponent = PayloadReader.GetString(selector, "byComponent");
            string byTag = PayloadReader.GetString(selector, "byTag");
            string byName = PayloadReader.GetString(selector, "byName");
            string byRule = PayloadReader.GetString(selector, "byRule");

            _runtime.Registry.CollectMatching(go => Matches(go, byComponent, byTag, byName), _buffer);

            JArray nodes = new JArray();
            for (int i = 0; i < _buffer.Count; i++)
            {
                NodeState state = _runtime.Driver.InspectNode(_buffer[i]);
                if (state != null)
                {
                    string testId;
                    if (_runtime.Registry.TryGetTestId(_buffer[i], out testId))
                    {
                        state.TestId = testId;
                    }

                    if (string.IsNullOrEmpty(byRule) || MatchesGlob(state.TestId, byRule))
                    {
                        nodes.Add(UrdtJson.NodeToJson(state));
                    }
                }
            }

            return Response.Success(command.Id, new JObject { ["matches"] = nodes });
        }

        private static bool Matches(GameObject gameObject, string byComponent, string byTag, string byName)
        {
            if (!string.IsNullOrEmpty(byComponent))
            {
                return gameObject.GetComponent(byComponent) != null;
            }

            if (!string.IsNullOrEmpty(byTag))
            {
                return gameObject.CompareTag(byTag);
            }

            if (!string.IsNullOrEmpty(byName))
            {
                return gameObject.name == byName;
            }

            return true;
        }

        private static bool MatchesGlob(string value, string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                return true;
            }

            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            return MatchesGlob(value, 0, pattern, 0);
        }

        private static bool MatchesGlob(string value, int valueIndex, string pattern, int patternIndex)
        {
            while (patternIndex < pattern.Length)
            {
                if (pattern[patternIndex] == '*')
                {
                    patternIndex++;
                    if (patternIndex == pattern.Length)
                    {
                        return true;
                    }

                    while (valueIndex < value.Length)
                    {
                        if (MatchesGlob(value, valueIndex, pattern, patternIndex))
                        {
                            return true;
                        }

                        valueIndex++;
                    }

                    return false;
                }

                if (valueIndex >= value.Length || pattern[patternIndex] != value[valueIndex])
                {
                    return false;
                }

                valueIndex++;
                patternIndex++;
            }

            return valueIndex == value.Length;
        }
    }
}
