using System;
using KBP.URDT.Transport;
using Newtonsoft.Json.Linq;
using URDT.Runtime.Inspectors;

namespace KBP.URDT.Handlers
{
    /// <summary>
    /// Handles <c>audio</c>: returns the game's own audio mix since a sample cursor (16 kHz mono PCM16,
    /// base64) plus AudioSource playback events. Payload: since_sample (long, default "last max_seconds"),
    /// max_seconds (float, default 10), pcm (bool, default true).
    /// </summary>
    public sealed class AudioHandler : ICommandHandler
    {
        public AudioHandler(UrdtRuntime runtime)
        {
        }

        public Response Handle(Command command)
        {
            UrdtAudioTap.Ensure();
            UrdtAudioTap tap = UrdtAudioTap.Instance;
            if (tap == null)
            {
                return Response.Error(command.Id, ErrorCodes.E_UNSUPPORTED, "No AudioListener in the scene.");
            }

            JObject payload = PayloadReader.AsObject(command.Payload);
            long since = payload != null && payload["since_sample"] != null ? payload["since_sample"].Value<long>() : 0L;
            float maxSeconds = PayloadReader.GetFloat(payload, "max_seconds", 10f);
            bool wantPcm = PayloadReader.GetBool(payload, "pcm", true);

            float[] samples = tap.Read(since, maxSeconds, out long from, out long to);
            JObject result = new JObject
            {
                ["sample_rate"] = UrdtAudioTap.TARGET_RATE,
                ["from_sample"] = from,
                ["to_sample"] = to,
            };

            if (wantPcm)
            {
                byte[] pcm = new byte[samples.Length * 2];
                float peak = 0f;
                for (int i = 0; i < samples.Length; i++)
                {
                    float v = Math.Max(-1f, Math.Min(1f, samples[i]));
                    peak = Math.Max(peak, Math.Abs(v));
                    short s = (short)(v * 32767f);
                    pcm[2 * i] = (byte)(s & 0xFF);
                    pcm[2 * i + 1] = (byte)((s >> 8) & 0xFF);
                }

                result["pcm16_b64"] = Convert.ToBase64String(pcm);
                result["peak"] = peak;
            }

            JArray events = new JArray();
            foreach (UrdtAudioTap.SoundEvent e in tap.EventsSince(since))
            {
                events.Add(new JObject { ["sample"] = e.SampleIndex, ["source"] = e.Source, ["clip"] = e.Clip, ["volume"] = e.Volume });
            }

            result["events"] = events;
            return Response.Success(command.Id, result);
        }
    }
}
