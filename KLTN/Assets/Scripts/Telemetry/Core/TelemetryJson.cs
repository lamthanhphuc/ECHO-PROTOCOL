using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace EchoProtocol.Telemetry
{
    public sealed class TelemetryJsonObject
    {
        private readonly SortedDictionary<string, string> _members =
            new SortedDictionary<string, string>(StringComparer.Ordinal);

        public int Count => _members.Count;

        public TelemetryJsonObject AddString(string name, string value)
        {
            return AddRaw(name, value == null ? "null" : "\"" + Escape(value) + "\"");
        }

        public TelemetryJsonObject AddInteger(string name, long value)
        {
            return AddRaw(name, value.ToString(CultureInfo.InvariantCulture));
        }

        public TelemetryJsonObject AddNumber(string name, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "JSON numbers must be finite.");
            }

            return AddRaw(name, value.ToString("G17", CultureInfo.InvariantCulture));
        }

        public TelemetryJsonObject AddBoolean(string name, bool value)
        {
            return AddRaw(name, value ? "true" : "false");
        }

        public TelemetryJsonObject AddNull(string name)
        {
            return AddRaw(name, "null");
        }

        public TelemetryJsonObject AddObject(string name, TelemetryJsonObject value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return AddRaw(name, value.ToJson());
        }

        public TelemetryJsonObject AddRawObject(string name, string rawJsonObject)
        {
            ValidateObject(rawJsonObject, nameof(rawJsonObject));
            return AddRaw(name, rawJsonObject.Trim());
        }

        public TelemetryJsonObject Merge(TelemetryJsonObject other)
        {
            if (other == null)
            {
                return this;
            }

            foreach (var pair in other._members)
            {
                AddRaw(pair.Key, pair.Value);
            }

            return this;
        }

        public string ToJson()
        {
            var builder = new StringBuilder();
            builder.Append('{');
            var first = true;

            foreach (var pair in _members)
            {
                if (!first)
                {
                    builder.Append(',');
                }

                first = false;
                builder.Append('"').Append(Escape(pair.Key)).Append("\":").Append(pair.Value);
            }

            return builder.Append('}').ToString();
        }

        internal static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value ?? string.Empty;
            }

            var builder = new StringBuilder(value.Length + 8);
            foreach (var character in value)
            {
                switch (character)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < 0x20)
                        {
                            builder.Append("\\u").Append(((int)character).ToString("x4"));
                        }
                        else
                        {
                            builder.Append(character);
                        }
                        break;
                }
            }

            return builder.ToString();
        }

        internal static void ValidateObject(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A JSON object is required.", parameterName);
            }

            var trimmed = value.Trim();
            if (trimmed[0] != '{' || trimmed[trimmed.Length - 1] != '}')
            {
                throw new ArgumentException("The value must be a JSON object.", parameterName);
            }
        }

        private TelemetryJsonObject AddRaw(string name, string encodedValue)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("JSON member name is required.", nameof(name));
            }

            if (_members.ContainsKey(name))
            {
                throw new InvalidOperationException("Duplicate JSON member: " + name);
            }

            _members.Add(name, encodedValue);
            return this;
        }
    }

    public static class TelemetryWireSerializer
    {
        public static string SerializeEvent(TelemetryEvent telemetryEvent)
        {
            if (telemetryEvent == null)
            {
                throw new ArgumentNullException(nameof(telemetryEvent));
            }

            var builder = new StringBuilder(512);
            builder.Append('{')
                .Append("\"id\":\"").Append(telemetryEvent.Id.ToString("D")).Append("\",")
                .Append("\"matchId\":\"").Append(telemetryEvent.MatchId.ToString("D")).Append("\",")
                .Append("\"userId\":");

            if (telemetryEvent.UserId.HasValue)
            {
                builder.Append('"').Append(telemetryEvent.UserId.Value.ToString("D")).Append('"');
            }
            else
            {
                builder.Append("null");
            }

            builder.Append(",\"eventType\":\"").Append(TelemetryJsonObject.Escape(telemetryEvent.EventType)).Append("\",")
                .Append("\"ts\":\"").Append(telemetryEvent.TimestampUtc.ToString("O", CultureInfo.InvariantCulture)).Append("\",")
                .Append("\"valueJson\":{\"context\":").Append(telemetryEvent.ContextJson)
                .Append(",\"data\":").Append(telemetryEvent.DataJson).Append("},")
                .Append("\"reasonCode\":");

            if (telemetryEvent.ReasonCode == null)
            {
                builder.Append("null");
            }
            else
            {
                builder.Append('"').Append(TelemetryJsonObject.Escape(telemetryEvent.ReasonCode)).Append('"');
            }

            return builder.Append(",\"schemaVersion\":\"")
                .Append(TelemetryJsonObject.Escape(telemetryEvent.SchemaVersion))
                .Append("\"}")
                .ToString();
        }

        public static string SerializeBatch(IReadOnlyList<TelemetryBufferedEvent> events)
        {
            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            var builder = new StringBuilder();
            builder.Append("{\"events\":[");
            for (var index = 0; index < events.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                builder.Append(events[index].SerializedJson);
            }

            return builder.Append("]}").ToString();
        }
    }
}
