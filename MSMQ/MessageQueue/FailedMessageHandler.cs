using MessageQueue.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using msmq = System.Messaging;

namespace MessageQueue
{
    /// <summary>
    /// Interface to handle implementation on how to survive posion messages
    /// 
    /// Code derived from: http://www.codeproject.com/Articles/10841/Surviving-poison-messages-in-MSMQ
    /// </summary>
    public interface IFailedMessageHandler
    {
        TransactionAction HandleFailedMessage(msmq.Message message, msmq.MessageQueueTransaction transaction);
        ILogger Logger { get; set; }
    }

    public enum TransactionAction { ROLLBACK, COMMIT };

    #region Different Ways to Handle a Failed Message Read from Queue - Choose your posion...
    public class DiscardMessageHandler : IFailedMessageHandler
    {
        public ILogger Logger { get; set; }

        public TransactionAction HandleFailedMessage(msmq.Message message, msmq.MessageQueueTransaction transaction)
        {
            Trace.WriteLine("Message discarded");
            return TransactionAction.COMMIT;
        }
    }

    public class AlwaysRollBackHandler : IFailedMessageHandler
    {
        public ILogger Logger { get; set; }
        public TransactionAction HandleFailedMessage(msmq.Message message, msmq.MessageQueueTransaction transaction)
        {
            return TransactionAction.ROLLBACK;
        }
    }

    public class SendToBackHandler : IFailedMessageHandler
    {
        public ILogger Logger { get; set; }
        const int MAX_RETRIES = 3;
        private msmq.MessageQueue deadLetterQueue;
        private string deadLetterQueueAddress;

        /// <summary>
        /// Pass in existing queue to hold open a connection to the deadLetterQueue when needed for the life of the object.
        /// 
        /// </summary>
        /// <param name="deadLetterQueue"></param>
        public SendToBackHandler(msmq.MessageQueue deadLetterQueue, ILogger logger)
        {
            this.deadLetterQueue = deadLetterQueue;
            Logger = logger;
        }

        /// <summary>
        /// Pass in queue address to lazy load the message queue until needed, if every.
        /// </summary>
        /// <param name="deadLetterQueueAddress"></param>
        public SendToBackHandler(string deadLetterQueueAddress, ILogger logger)
        {
            this.deadLetterQueueAddress = deadLetterQueueAddress;
            Logger = logger;
        }

        public TransactionAction HandleFailedMessage(msmq.Message message, msmq.MessageQueueTransaction transaction)
        {
            message.Priority = msmq.MessagePriority.Lowest;
            message.AppSpecific++;
            if (message.AppSpecific > MAX_RETRIES)
            {
                Logger.LogInfo("Sending to dead-letter queue. Message Id: " + message.Id);
                // Create/Get queue if only the queue address was passed into the ctor
                if (deadLetterQueue == null)
                {
                    if (!msmq.MessageQueue.Exists(deadLetterQueueAddress))
                    {
                        msmq.MessageQueue.Create(deadLetterQueueAddress, true);
                    }
                    deadLetterQueue = new msmq.MessageQueue(deadLetterQueueAddress);
                }
                deadLetterQueue.Send(message, transaction);
            }
            else
            {
                Logger.LogInfo("Sending to back of retry queue. Message Id: " + message.Id);
                message.DestinationQueue.Send(message, transaction);
            }
            return TransactionAction.COMMIT;
        }
    }

    public class RetrySendToDeadLetterQueueHandler : IFailedMessageHandler
    {
        public ILogger Logger { get; set; }
        static string lastMessageId = null;
        static int retries = 0;
        const int MAX_RETRIES = 3;
        msmq.MessageQueue deadLetterQueue;
        string deadLetterQueueAddress;

        /// <summary>
        /// Pass in existing queue to hold open a connection to the deadLetterQueue when needed for the life of the object.
        /// </summary>
        /// <param name="deadLetterQueue"></param>
        public RetrySendToDeadLetterQueueHandler(msmq.MessageQueue deadLetterQueue, ILogger logger)
        {
            this.deadLetterQueue = deadLetterQueue;
            this.Logger = logger;
        }

        /// <summary>
        /// Pass in queue address to lazy load the message queue until needed, if every.
        /// </summary>
        /// <param name="deadLetterQueueAddress"></param>
        public RetrySendToDeadLetterQueueHandler(string deadLetterQueueAddress, ILogger logger)
        {
            this.deadLetterQueueAddress = deadLetterQueueAddress;
            this.Logger = logger;
        }

        public TransactionAction HandleFailedMessage(msmq.Message message, msmq.MessageQueueTransaction transaction)
        {
            if (message.Id != lastMessageId)
            {
                retries = 0;
                lastMessageId = message.Id;
            }
            retries++;
            if (retries > MAX_RETRIES)
            {
                Logger.LogInfo("Sending to dead-letter queue");
                // Create/Get queue if only the queue address was passed into the ctor
                if (deadLetterQueue == null)
                {
                    if (!msmq.MessageQueue.Exists(deadLetterQueueAddress))
                    {
                        msmq.MessageQueue.Create(deadLetterQueueAddress, true);
                    }
                    deadLetterQueue = new msmq.MessageQueue(deadLetterQueueAddress);
                }
                deadLetterQueue.Send(message, transaction);
                return TransactionAction.COMMIT;
            }
            else
            {
                Logger.LogInfo("Returning message to queue for retry: " + retries);
                return TransactionAction.ROLLBACK;
            }
        }
    }

    public class SeparateRetryQueueHandler : IFailedMessageHandler
    {
        public ILogger Logger { get; set; }
        msmq.MessageQueue retryQueue;
        string retryQueueAddress;

        public SeparateRetryQueueHandler(msmq.MessageQueue retryQueue, ILogger logger)
        {
            this.retryQueue = retryQueue;
            this.Logger = logger;
        }

        public SeparateRetryQueueHandler(string retryQueueAddress, ILogger logger)
        {
            this.retryQueueAddress = retryQueueAddress;
            this.Logger = logger;
        }

        public TransactionAction HandleFailedMessage(msmq.Message message, msmq.MessageQueueTransaction transaction)
        {
            Logger.LogWarning("Sending message to retry queue. Message Id: " + message.Id);
            // Create/Get queue if only the queue address was passed into the ctor
            if (retryQueue == null)
            {
                if (!msmq.MessageQueue.Exists(retryQueueAddress))
                {
                    msmq.MessageQueue.Create(retryQueueAddress, true);
                }
                retryQueue = new msmq.MessageQueue(retryQueueAddress);
            }
            retryQueue.Send(message, transaction);
            return TransactionAction.COMMIT;
        }
    }
    #endregion
}
