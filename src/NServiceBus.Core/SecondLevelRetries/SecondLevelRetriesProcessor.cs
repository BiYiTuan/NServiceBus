namespace NServiceBus.SecondLevelRetries
{
    using System;
    using System.Globalization;
    using Faults.Forwarder;
    using Helpers;
    using Logging;
    using Satellites;
    using Transports;
    using Unicast;

    class SecondLevelRetriesProcessor : ISatellite
    {
        public SecondLevelRetriesProcessor()
        {
            Disabled = true;
        }

        public ISendMessages MessageSender { get; set; }
        public IDeferMessages MessageDeferrer { get; set; }
        public FaultManager FaultManager { get; set; }
        public Func<TransportMessage, TimeSpan> RetryPolicy { get; set; }
        public string InputAddress { get; set; }

        public bool Disabled
        {
            get; set;
        }

        public void Start()
        {
        }

        public void Stop()
        {
        }

        public bool Handle(TransportMessage message)
        {
            var defer = RetryPolicy.Invoke(message);

            if (defer < TimeSpan.Zero)
            {
                SendToErrorQueue(message);
                return true;
            }

            Defer(defer, message);

            return true;
        }

        void SendToErrorQueue(TransportMessage message)
        {
            logger.ErrorFormat(
                "SLR has failed to resolve the issue with message {0} and will be forwarded to the error queue at {1}",
                message.Id, FaultManager.ErrorQueue);

            message.Headers.Remove(Headers.Retries);

            MessageSender.Send(message, new SendOptions(FaultManager.ErrorQueue));
        }

        void Defer(TimeSpan defer, TransportMessage message)
        {
            var retryMessageAt = DateTime.UtcNow + defer;

            TransportMessageHelpers.SetHeader(message, Headers.Retries,
                (TransportMessageHelpers.GetNumberOfRetries(message) + 1).ToString(CultureInfo.InvariantCulture));

            var addressOfFaultingEndpoint = TransportMessageHelpers.GetAddressOfFaultingEndpoint(message);

            if (!TransportMessageHelpers.HeaderExists(message, SecondLevelRetriesHeaders.RetriesTimestamp))
            {
                TransportMessageHelpers.SetHeader(message, SecondLevelRetriesHeaders.RetriesTimestamp,
                    DateTimeExtensions.ToWireFormattedString(DateTime.UtcNow));
            }

            logger.DebugFormat("Defer message and send it to {0}", addressOfFaultingEndpoint);

            var sendOptions = new SendOptions(addressOfFaultingEndpoint)
            {
                DeliverAt = retryMessageAt
            };


            MessageDeferrer.Defer(message, sendOptions);
        }

        static ILog logger = LogManager.GetLogger<SecondLevelRetriesProcessor>();
    }
}
