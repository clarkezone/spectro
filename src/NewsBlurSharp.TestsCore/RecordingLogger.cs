using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NewsBlurSharp.Logging;

namespace NewsBlurSharp.Tests
{
    internal sealed class RecordingLogger : ILogger
    {
        public List<string> Messages { get; } = new List<string>();

        public void Info(string message, params object[] paramList) => Add(message, paramList);
        public void Error(string message, params object[] paramList) => Add(message, paramList);
        public void Warn(string message, params object[] paramList) => Add(message, paramList);
        public void Debug(string message, params object[] paramList) => Add(message, paramList);
        public void Fatal(string message, params object[] paramList) => Add(message, paramList);
        public void FatalException(string message, Exception exception, params object[] paramList) =>
            Add(message, paramList);
        public void Log(LogSeverity severity, string message, params object[] paramList) =>
            Add(message, paramList);
        public void ErrorException(string message, Exception exception, params object[] paramList) =>
            Add(message, paramList);
        public void LogMultiline(
            string message,
            LogSeverity severity,
            StringBuilder additionalContent) =>
            Messages.Add(message);
        public Task PurgeLogFile() => Task.CompletedTask;
        public Task<Stream> OpenLogFile() => Task.FromResult<Stream>(Stream.Null);

        private void Add(string message, object[] values)
        {
            Messages.Add(values.Length == 0
                ? message
                : string.Format(CultureInfo.InvariantCulture, message, values));
        }
    }
}
