using MessageQueue.Interfaces;
using System;

namespace MessageQueue
{
    public static class MessageFactory
    {
        public static IMessageProcessor ProcessMessage(string processorType)
        {
            IMessageProcessor messageProcessor = null;

            switch (processorType)
            {
                case ProcessorType.Email:
                    messageProcessor = null; // new EmailMessageProcessor();
                    break;
                case ProcessorType.MassEmail:
                    throw new NotImplementedException();
                case ProcessorType.Billing:
                    throw new NotImplementedException();
                default:
                    break;
            }

            return messageProcessor;
        }

        /* Alternate way to determine type of message to process
        public static IMessageProcessor ProcessMessage(object message) {
            IMessageProcessor messageProcessor = null;

            switch (message.GetType()) {
                case typeof(String):
                    messageProcessor = new EmailMessageProcessor(null);
                    break;
                //case typeof(MassEmailMessage)
                //    throw new NotImplementedException();
                //case typeof(BillingMessage)
                //    throw new NotImplementedException();
                default:
                    break;
            }

            return messageProcessor;
        }
         */
    }

    public static class ProcessorType
    {
        public const string Email = "Email";
        public const string MassEmail = "MassEmail";
        public const string Billing = "Billing";
    }
}
