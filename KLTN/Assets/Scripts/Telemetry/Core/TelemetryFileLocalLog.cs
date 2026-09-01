using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace EchoProtocol.Telemetry
{
    public sealed class TelemetryFileLocalLog : ITelemetryLocalLog
    {
        private readonly string _filePath;
        private readonly object _sync = new object();

        public TelemetryFileLocalLog(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("Telemetry log path is required.", nameof(filePath));
            }

            _filePath = filePath;
        }

        public string FilePath => _filePath;

        public void Append(string category, Guid? eventId, string detailsJson)
        {
            TelemetryJsonObject.ValidateObject(detailsJson, nameof(detailsJson));
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var line = new StringBuilder()
                .Append("{\"loggedAt\":\"")
                .Append(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture))
                .Append("\",\"category\":\"")
                .Append(TelemetryJsonObject.Escape(category ?? string.Empty))
                .Append("\",\"eventId\":");

            if (eventId.HasValue)
            {
                line.Append('"').Append(eventId.Value.ToString("D")).Append('"');
            }
            else
            {
                line.Append("null");
            }

            line.Append(",\"details\":").Append(detailsJson).Append('}').AppendLine();
            lock (_sync)
            {
                File.AppendAllText(_filePath, line.ToString(), Encoding.UTF8);
            }
        }
    }
}
