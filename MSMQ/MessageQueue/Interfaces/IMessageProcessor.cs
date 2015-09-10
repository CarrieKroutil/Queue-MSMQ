using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MessageQueue.Interfaces
{
    public interface IMessageProcessor
    {
        /// <summary>
        /// Process message passed in and return true/false if processing was successful.
        /// </summary>
        /// <param name="message">Object to process</param>
        /// <returns>True/false if processing was successful</returns>
        bool ProcessMessage(object message);
    }
}