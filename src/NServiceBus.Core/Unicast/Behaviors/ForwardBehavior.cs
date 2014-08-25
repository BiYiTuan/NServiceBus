﻿namespace NServiceBus.Unicast.Behaviors
{
    using System;
    using NServiceBus.Config;
    using Features;
    using Pipeline;
    using Pipeline.Contexts;
    using Transports;
    using Queuing.Installers;

    /// <summary>
    /// Provides message forwarding capabilities
    /// </summary>
    public class ForwardReceivedMessages : Feature
    {
        internal ForwardReceivedMessages()
        {
            // Only enable if the configuration is defined in UnicastBus
            Prerequisite(config => GetConfiguredForwardMessageQueue(config) != null,"No forwarding address was defined in the unicastbus config");
        }

        /// <summary>
        /// Invoked if the feature is activated
        /// </summary>
        /// <param name="context">The feature context</param>
        protected internal override void Setup(FeatureConfigurationContext context)
        {
            context.Pipeline.Register<ForwardBehavior.Registration>();

            var forwardReceivedMessagesQueue = GetConfiguredForwardMessageQueue(context);
            var timeToBeReceived = GetConfiguredTimeToBeReceivedValue(context);

            context.Container.ConfigureComponent<ForwardReceivedMessagesToQueueCreator>(DependencyLifecycle.InstancePerCall)
                .ConfigureProperty(p => p.Enabled, true)
                .ConfigureProperty(t => t.Address, forwardReceivedMessagesQueue);

            context.Container.ConfigureComponent<ForwardBehavior>(DependencyLifecycle.InstancePerCall)
                .ConfigureProperty(p => p.ForwardReceivedMessagesTo, forwardReceivedMessagesQueue)
                .ConfigureProperty(p => p.TimeToBeReceivedOnForwardedMessages, timeToBeReceived);
        }

        TimeSpan? GetConfiguredTimeToBeReceivedValue(FeatureConfigurationContext context)
        {
            var unicastBusConfig = context.Settings.GetConfigSection<UnicastBusConfig>();
            if (unicastBusConfig != null && unicastBusConfig.TimeToBeReceivedOnForwardedMessages > TimeSpan.Zero)
            {
                return unicastBusConfig.TimeToBeReceivedOnForwardedMessages;
            }
            return TimeSpan.MaxValue;
        }
        string GetConfiguredForwardMessageQueue(FeatureConfigurationContext context)
        {
            var unicastBusConfig = context.Settings.GetConfigSection<UnicastBusConfig>();
            if (unicastBusConfig != null && !string.IsNullOrWhiteSpace(unicastBusConfig.ForwardReceivedMessagesTo))
            {
                return unicastBusConfig.ForwardReceivedMessagesTo;
            }
            return null;
        }
    }

    class ForwardBehavior : IBehavior<IncomingContext>
    {
        public IAuditMessages MessageAuditer { get; set; }

        public string ForwardReceivedMessagesTo { get; set; }

        public TimeSpan? TimeToBeReceivedOnForwardedMessages { get; set; }

        public void Invoke(IncomingContext context, Action next)
        {
            next();

            MessageAuditer.Audit(new SendOptions(ForwardReceivedMessagesTo)
            {
                TimeToBeReceived = TimeToBeReceivedOnForwardedMessages
            }, context.PhysicalMessage);

        }

        public class Registration : RegisterStep
        {
            public Registration()
                : base("ForwardMessageTo", typeof(ForwardBehavior), "Forwards message to the specified queue in the UnicastBus config section.")
            {
                InsertBefore(WellKnownStep.ExecuteUnitOfWork);
            }
        }
    }
}