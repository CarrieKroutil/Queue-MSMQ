using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MessageQueue.Interfaces
{
    public interface ILogger
    {
        /// <summary>
        /// Turn off all logging at the application level
        /// </summary>
        bool DisableErrorLogging { get; }

        /// <summary>
        /// Application name or category to log messages under
        /// </summary>
        string Category { get; set; }

        void LogDebug(string message);
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(Exception error);
        void LogFatel(Exception error);
    }
}