using KBP.URDT.Lifecycle;
using KBP.URDT.Transport;
using Newtonsoft.Json.Linq;
using UnityEngine.SceneManagement;

namespace KBP.URDT.Handlers
{
    /// <summary>Handles <c>reset_state</c>: reloads the active scene (<c>scene</c> depth; deeper
    /// depths are M6) and optionally switches time mode / seeds RNG (via <c>time_mode</c>,
    /// <c>seed</c>).</summary>
    public sealed class ResetStateHandler : ICommandHandler
    {
        private const string TIME_DETERMINISTIC = "deterministic";
        private const string TIME_REALTIME = "realtime";

        private readonly UrdtRuntime _runtime;

        public ResetStateHandler(UrdtRuntime runtime)
        {
            _runtime = runtime;
        }

        public Response Handle(Command command)
        {
            if (_runtime.Scenes == null)
            {
                return Response.Error(command.Id, ErrorCodes.E_UNSUPPORTED, "No scene manager.");
            }

            JObject payload = PayloadReader.AsObject(command.Payload);
            string depth = PayloadReader.GetString(payload, "depth", SceneLifecycleManager.DEPTH_SCENE);
            int? seed = payload["seed"] != null ? (int?)PayloadReader.GetInt(payload, "seed") : null;
            string timeMode = PayloadReader.GetString(payload, "time_mode");

            if (_runtime.Time != null && !string.IsNullOrEmpty(timeMode))
            {
                if (string.Equals(timeMode, TIME_DETERMINISTIC, System.StringComparison.Ordinal))
                {
                    _runtime.Time.EnterDeterministic(seed ?? 0);
                }
                else if (string.Equals(timeMode, TIME_REALTIME, System.StringComparison.Ordinal))
                {
                    _runtime.Time.ExitDeterministic();
                }
            }

            string sceneName = SceneManager.GetActiveScene().name;
            if (!_runtime.Scenes.ResetScene(depth, seed))
            {
                return Response.Error(
                    command.Id, ErrorCodes.E_UNSUPPORTED,
                    "Depth '" + depth + "' unsupported (services reset hook not registered, or unknown depth).");
            }

            JObject data = new JObject { ["reset"] = true, ["scene"] = sceneName, ["depth"] = depth };
            if (seed.HasValue)
            {
                data["seed"] = seed.Value;
            }

            return Response.Success(command.Id, data);
        }
    }
}
