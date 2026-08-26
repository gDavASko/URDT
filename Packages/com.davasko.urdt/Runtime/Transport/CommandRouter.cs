using System;
using System.Collections.Generic;
using UnityEngine;

namespace KBP.URDT.Transport
{
    /// <summary>
    /// Dispatches a <see cref="Command"/> to the handler registered for its
    /// <c>action</c> (TZ §7.1). Validates the payload, echoes the request id on every
    /// response (FR-15), and isolates a handler exception as
    /// <see cref="ErrorCodes.E_INTERNAL"/> so one failing command never kills the
    /// dispatcher (TZ §7.5). Runs on the main thread only.
    /// </summary>
    public sealed class CommandRouter
    {
        private readonly Dictionary<string, ICommandHandler> _handlers =
            new Dictionary<string, ICommandHandler>();

        public void Register(string action, ICommandHandler handler)
        {
            if (!string.IsNullOrEmpty(action) && handler != null)
            {
                _handlers[action] = handler;
            }
        }

        public bool Unregister(string action)
        {
            return !string.IsNullOrEmpty(action) && _handlers.Remove(action);
        }

        public Response Route(Command command)
        {
            if (command == null)
            {
                return Response.Error(null, ErrorCodes.E_BAD_PAYLOAD, "Null command.");
            }

            if (string.IsNullOrEmpty(command.Action))
            {
                return Response.Error(command.Id, ErrorCodes.E_BAD_PAYLOAD, "Missing action.");
            }

            ICommandHandler handler;
            if (!_handlers.TryGetValue(command.Action, out handler))
            {
                // A game-provided arrange-only seam that was never registered (protocol §4.14).
                if (command.Action.StartsWith("custom:", StringComparison.Ordinal))
                {
                    return Response.Error(
                        command.Id, ErrorCodes.E_UNSUPPORTED, "Unregistered custom command: " + command.Action);
                }

                return Response.Error(
                    command.Id, ErrorCodes.E_UNKNOWN_ACTION, "Unknown action: " + command.Action);
            }

            try
            {
                Response response = handler.Handle(command);
                return response ?? Response.Error(
                    command.Id, ErrorCodes.E_INTERNAL, "Handler returned null.");
            }
            catch (Exception exception)
            {
                // Isolate the fault: log as a warning (handled, not a crash) and convert
                // to a structured error; the dispatcher keeps running.
                Debug.LogWarning("URDT handler '" + command.Action + "' threw: " + exception.Message);
                return Response.Error(command.Id, ErrorCodes.E_INTERNAL, exception.Message);
            }
        }
    }
}
