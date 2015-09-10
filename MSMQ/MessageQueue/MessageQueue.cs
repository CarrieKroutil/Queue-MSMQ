using MessageQueue.Interfaces;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Messaging;
using System.Text;
using System.Threading.Tasks;
using msmq = System.Messaging;

namespace MessageQueue
{
    /// <summary>
    /// Queue Controller allows a way to write messages and read messages from a message queue.
    /// 
    /// MSMQ (Microsoft Message Queue) is one example implementation.
    ///     Enqueue - https://visualstudiomagazine.com/articles/2014/02/01/offloading-work-from-your-application-with-a-queue.aspx
    ///     Dequeue - https://visualstudiomagazine.com/articles/2014/02/01/picking-up-queue-messages.aspx
    /// </summary>
    public class QueueManager : IQueueManager
    {
        public readonly ILogger Logger;

        public QueueManager(ILogger logger)
        {
            Logger = logger;
        }

        #region Create / Delete Queues

        /// <summary>
        /// Add Queue to collection if does not already exist and return queue object
        /// To View Queues on Server, Start--> Run and type "MMC" --> File, Add or Remove Snap-ins --> Select 'Computer Management' and move to box on right --> Click 'OK'
        /// Expand 'Services and Applications' --> Expand 'Message Queuing' --> Expand 'Private Queues' and refresh to see newly created queue
        /// </summary>
        /// <param name="queueName"></param>
        public dynamic CreateAndGetQueue(string queueAddress)
        {
            if (string.IsNullOrWhiteSpace(queueAddress)) { throw new ArgumentException("Missing expected parameter - " + queueAddress); }
            if (!msmq.MessageQueue.Exists(queueAddress))
            {
                msmq.MessageQueue.Create(queueAddress, true);
            }
            var queue = new msmq.MessageQueue(queueAddress);
            queue.DefaultPropertiesToSend.Recoverable = true;
            return queue;
        }

        /// <summary>
        /// Get Queue from collection if exists
        /// NOTE: See CreateAndGetQueue method summary for further details on how to see queues on server
        /// </summary>
        /// <param name="queueName"></param>
        public dynamic GetQueue(string queueAddress)
        {
            if (string.IsNullOrWhiteSpace(queueAddress)) { throw new ArgumentException("Missing expected parameter - " + queueAddress); }
            if (msmq.MessageQueue.Exists(queueAddress))
            {
                var queue = new msmq.MessageQueue(queueAddress);
                queue.DefaultPropertiesToSend.Recoverable = true;
                return queue;
            }
            return null;
        }

        /// <summary>
        /// Remove Queue from collection if exists
        /// NOTE: See CreateAndGetQueue method summary for further details on how to see queues on server
        /// </summary>
        /// <param name="queueName"></param>
        public void TryDeleteQueue(string queueAddress)
        {
            if (string.IsNullOrWhiteSpace(queueAddress)) { throw new ArgumentException("Missing expected parameter - " + queueAddress); }
            if (msmq.MessageQueue.Exists(queueAddress))
            {
                msmq.MessageQueue.Delete(queueAddress);
            }
        }
        #endregion

        /// <summary>
        /// Enqueue list of messages to queue address provided.
        /// </summary>
        /// <param name="messages">List of any type of object.  The queue will label the message per object type.</param>
        /// <param name="queueAddress">Path address of queue.</param>
        /// <returns>Returns the number of messages successfully enqueued; -1 means the queue was not able to be written to</returns>
        public int InsertMessagesIntoQueue(List<object> messages, string queueAddress)
        {
            int sentMessageCounter = 0;

            // Get Queue to send messages to (create if queue doesn't exist already)
            msmq.MessageQueue queue = CreateAndGetQueue(queueAddress);

            if (queue.CanWrite)
            {
                foreach (var msg in messages)
                {
                    // Convert object to json stream to transmit on to queue
                    var message = new msmq.Message();

                    // Content of message
                    message.BodyStream = msg.ToJsonStream();

                    // Used to distinguish type of message to process via factory
                    message.Label = "Email";

                    // Used as a counter on number of times message has been attempted to be processed
                    message.AppSpecific = 0;

                    //queue.Send(message);
                    queue.Send(message, msmq.MessageQueueTransactionType.Single);

                    sentMessageCounter++;
                }
            }
            else
            {
                sentMessageCounter = -1;
            }

            return sentMessageCounter;
        }

        /// <summary>
        /// Processes existing messages on queue at specified queue address. Poisoned messages will be moved to a retry queue.  
        /// Only the max # of messages provided will be processed at one time.
        /// </summary>
        /// <param name="queueAddress"></param>
        /// <param name="retryQueueAddress"></param>
        /// <param name="messageMaxToProcessAtOneTime"></param>
        /// <returns>True if any messages were processed. False is no message existed to process.</returns>
        public bool ProcessMessagesFromQueue(string queueAddress, string retryQueueAddress, int messageMaxToProcessAtOneTime)
        {
            if (string.IsNullOrEmpty(queueAddress)) { throw new ArgumentNullException("queueAddress"); }
            if (string.IsNullOrEmpty(retryQueueAddress)) { throw new ArgumentNullException("retryQueueAddress"); }

            int messagesSuccessfullyProcessed = 0;

            using (var queue = CreateAndGetQueue(queueAddress))
            {
                IFailedMessageHandler failedMsgHandler = new SeparateRetryQueueHandler(retryQueueAddress, Logger);
                messagesSuccessfullyProcessed = ProcessMessages(queue, retryQueueAddress, messageMaxToProcessAtOneTime, failedMsgHandler);
            }
            Logger.LogInfo(string.Format("Successfully processed {0} messages on queue: {1}", messagesSuccessfullyProcessed, queueAddress));

            return (messagesSuccessfullyProcessed > 0);
        }


        public bool ProcessMessagesFromRetryQueue(string retryQueueAddress, string deadLetterQueueAddress, int messageMaxToProcessAtOneTime)
        {
            if (string.IsNullOrEmpty(retryQueueAddress)) { throw new ArgumentNullException("retryQueueAddress"); }
            if (string.IsNullOrEmpty(deadLetterQueueAddress)) { throw new ArgumentNullException("deadLetterQueueAddress"); }

            int messagesSuccessfullyProcessed = 0;

            using (var retryQueue = CreateAndGetQueue(retryQueueAddress))
            {
                IFailedMessageHandler failedMsgHandler = new SendToBackHandler(retryQueueAddress, Logger);
                messagesSuccessfullyProcessed = ProcessMessages(retryQueue, deadLetterQueueAddress, messageMaxToProcessAtOneTime, failedMsgHandler);
            }
            Logger.LogInfo(string.Format("Successfully processed {0} messages on retry queue: {1}", messagesSuccessfullyProcessed, retryQueueAddress));

            return (messagesSuccessfullyProcessed > 0);
        }

        public bool HandleDeadLetterQueueMessages(string deadLetterQueuePath, int messageMaxToProcessAtOneTime)
        {
            // TODO: Send out notification to support regarding faxes that needs to be manually handled

            return false;
        }

        #region Message processing memebers
        private int ProcessMessages(msmq.MessageQueue queue, string retryQueueAddress, int messageMaxToProcessAtOneTime, IFailedMessageHandler failedMsgHandler)
        {
            int messageCount = 0;

            while (queue.CanRead && messageCount < messageMaxToProcessAtOneTime)
            {
                bool msgReceivedSuccess = ReceiveMessage(queue, failedMsgHandler);

                if (msgReceivedSuccess)
                {
                    messageCount++;
                }
                else
                {
                    return messageCount;
                }
            }
            return messageCount;
        }

        /// <summary>
        /// Receive message wrapped with a transaction to handle failures based on handler passed into ctor
        /// </summary>
        /// <param name="failedMessageHandler">Determines what implementation should be taken in the event of poisoned messages.</param>
        private bool ReceiveMessage(msmq.MessageQueue incomingQueue, IFailedMessageHandler failedMessageHandler)
        {
            incomingQueue.MessageReadPropertyFilter.SetAll();
            using (var transaction = new MessageQueueTransaction())
            {
                transaction.Begin();
                Message message = null;
                try
                {
                    message = incomingQueue.Receive(TimeSpan.Zero, transaction);
                    Logger.LogDebug("Received message ID: " + message.Id);

                    ProcessMessage(message);

                    transaction.Commit();
                    Logger.LogDebug("Message processed OK");
                    return true;
                }
                catch (MessageQueueException msgQueueError)
                {
                    if (msgQueueError.Message == "Time out for the requested operation has expired.")
                    {
                        // No message found to process before TimeSpan expired
                        // {gulp} Swallow exception
                        return false;
                    }
                }
                catch (Exception e)
                {
                    Logger.LogDebug("Message failed");
                    TransactionAction transactionAction = TransactionAction.ROLLBACK;

                    if (message == null)
                    {
                        Logger.LogDebug("Message couldn't be received: " + e.Message);
                    }
                    else
                    {
                        try
                        {
                            transactionAction = failedMessageHandler.HandleFailedMessage(message, transaction);
                        }
                        catch (Exception failureHandlerException)
                        {
                            Logger.LogDebug("Error during failure handling: " + failureHandlerException.Message);
                        }
                    }

                    if (transactionAction == TransactionAction.ROLLBACK)
                    {
                        transaction.Abort();
                        Logger.LogDebug("Transaction rolled back");
                    }
                    else
                    {
                        transaction.Commit();
                        Logger.LogDebug("Transaction committed - message removed from queue");
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Use factory to determine message implementation
        /// </summary>
        /// <param name="message">MSMQ message pulled off queue to be processed.</param>
        private void ProcessMessage(Message message)
        {
            // Convert json stream to string
            var messageBody = message.BodyStream.ReadFromJson(message.Label);

            // Implement processor per message label via factory
            IMessageProcessor processor = MessageFactory.ProcessMessage(message.Label);

            // Perform processing on actual message
            processor.ProcessMessage(messageBody);
        }
        #endregion
    }
}