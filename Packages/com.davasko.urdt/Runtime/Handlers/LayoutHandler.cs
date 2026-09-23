using KBP.URDT.Transport;
using Newtonsoft.Json.Linq;
using URDT.Runtime.Inspectors;

namespace KBP.URDT.Handlers
{
    /// <summary>
    /// Handles <c>layout</c>: layout/localization defects found by the running layout inspector (text truncation,
    /// bounds overflow, font auto-size below readable size, missing glyphs). Payload: <c>path_contains</c>
    /// (optional filter on the hierarchy path, e.g. a module name), <c>audit</c> (bool, run an audit now), <c>limit</c>.
    /// </summary>
    public sealed class LayoutHandler : ICommandHandler
    {
        public LayoutHandler(UrdtRuntime runtime) { }

        public Response Handle(Command command)
        {
            JObject p = PayloadReader.AsObject(command.Payload) ?? new JObject();
            string filter = p.Value<string>("path_contains") ?? string.Empty;
            int limit = p.Value<int?>("limit") ?? 100;
            UrdtLayoutInspector inspector = UrdtLayoutInspector.Instance;
            if (inspector == null) return Response.Success(command.Id, new JObject { ["available"] = false });
            if (p.Value<bool?>("audit") ?? false) inspector.AuditActiveCanvases();

            var defects = new JArray();
            foreach (LayoutDefect d in inspector.DetectedDefects)
            {
                if (filter.Length > 0 && (d.Path == null || !d.Path.Contains(filter))) continue;
                if (defects.Count >= limit) break;
                defects.Add(new JObject
                {
                    ["type"] = d.DefectType.ToString(),
                    ["object"] = d.GameObjectName,
                    ["path"] = d.Path,
                    ["details"] = d.Details,
                    ["at_ms"] = d.TimestampMs
                });
            }
            return Response.Success(command.Id, new JObject { ["available"] = true, ["count"] = defects.Count, ["defects"] = defects });
        }
    }
}
