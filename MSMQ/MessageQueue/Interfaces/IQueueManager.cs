using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MessageQueue.Interfaces
{
    public interface IQueueManager
    {
        /// <summary>
        ///  Create a queue per queueAddress passed in if doesn't already exist and return queue.
        /// </summary>
        /// <param name="queueAddress">Path of queue to manage</param>
        /// <returns>MSMQ Message Queue or future implementation</returns>
        dynamic CreateAndGetQueue(string queueAddress);

        /// <summary>
        /// Retrieves existing queue object by path if exists.
        /// </summary>
        /// <param name="queueAddress">Path of queue to retrieve</param>
        /// <returns>MSMQ Message Queue or future implementation</returns>
        dynamic GetQueue(string queueAddress);

        /// <summary>
        /// If queue path passed in exists as a queue, then it will be removed from the machine.
        /// </summary>
        /// <param name="queueAddress">Path of queue to remove</param>
        void TryDeleteQueue(string queueAddress);

        /// <summary>
        /// Inserts a list of message objects onto queue.  Queue will be created if does not already exist.
        /// </summary>
        /// <param name="messages">List of objects representing messages</param>
        /// <param name="queueAddress">Path of queue to remove</param>
        /// <returns>Number of messages successfully inserted into queue</returns>
        int InsertMessagesIntoQueue(List<object> messages, string queueAddress);

        /// <summary>
        /// Grabs the next message (oldest) found on the provided queue path and processes it until there are no more messages or the max number has been reached.
        /// </summary>
        /// <param name="queueAddress">Path of queue to process messages off of.</param>
        /// <param name="retryQueueAddress">Move poisoned message to this queue if failure occurs.</param>
        /// <param name="messageMaxToProcessAtOneTime">Number of total messages to process at one time.</param>
        /// <returns>True if messages were processed and false if none were available to process.</returns>
        bool ProcessMessagesFromQueue(string queueAddress, string retryQueueAddress, int messageMaxToProcessAtOneTime);

        /// <summary>
        /// Attempts to process messages 3 times each upon failure inside the retry queue.
        /// Gets moved to the dead letter queue after 3 failed attempts to process.
        /// </summary>
        /// <param name="retryQueueAddress">Queue where poisoned messages reside.</param>
        /// <param name="deadLetterQueueAddress">Queue where messages go to die until manual intervention.</param>
        /// <param name="messageMaxToProcessAtOneTime">Number of total messages to process at one time.</param>
        /// <returns>True if messages were processed and false if none were available to process.</returns>
        bool ProcessMessagesFromRetryQueue(string retryQueueAddress, string deadLetterQueueAddress, int messageMaxToProcessAtOneTime);


        /// <summary>
        /// Send notification of the failed message to be processed manually.
        /// </summary>
        /// <param name="deadLetterQueuePath">Queue where messages go to die until manual intervention.</param>
        /// <param name="messageMaxToProcessAtOneTime">Number of total messages to process at one time.</param>
        /// <returns>True if messages were processed and false if none were available to process.</returns>
        bool HandleDeadLetterQueueMessages(string deadLetterQueuePath, int messageMaxToProcessAtOneTime);
    }
}