﻿namespace NServiceBus.Features
{
    using System;
    using System.Diagnostics;
    using Config;
    using Logging;
    using Unicast.Queuing.Installers;
    using Utils;

    /// <summary>
    /// Enabled message auditing for this endpoint.
    /// </summary>
    public class Audit : Feature
    {   
        internal Audit()
        {
            EnableByDefault();
            Prerequisite(config => GetConfiguredAuditQueue(config) != null,"No configured audit queue was found");
        }

        /// <summary>
        /// See <see cref="Feature.Setup"/>
        /// </summary>
        protected internal override void Setup(FeatureConfigurationContext context)
        {
            // If Audit feature is enabled and the value not specified via config and instead specified in the registry:
            // Log a warning when running in the debugger to remind user to make sure the 
            // production machine will need to have the required registry setting.
            if (Debugger.IsAttached && GetAuditQueueAddressFromAuditConfig(context) == null)
            {
                Logger.Warn("Endpoint auditing is configured using the registry on this machine, please ensure that you either run Set-NServiceBusLocalMachineSettings cmdlet on the target deployment machine or specify the QueueName attribute in the AuditConfig section in your app.config file. To quickly add the AuditConfig section to your app.config, in Package Manager Console type: add-NServiceBusAuditConfig.");
            }

            context.MainPipeline.Register<AuditBehavior.Registration>();
            context.MainPipeline.Register<AttachCausationHeadersBehavior.Registration>();

            var auditQueue = GetConfiguredAuditQueue(context);

            context.Container.ConfigureComponent<AuditQueueCreator>(DependencyLifecycle.InstancePerCall)
                .ConfigureProperty(p => p.Enabled, true)
                .ConfigureProperty(t => t.AuditQueue, auditQueue);

            var behaviorConfig = context.Container.ConfigureComponent<AuditBehavior>(DependencyLifecycle.InstancePerCall)
                .ConfigureProperty(p => p.AuditQueue, auditQueue);



            var messageAuditingConfig = context.Settings.GetConfigSection<AuditConfig>();
            if (messageAuditingConfig != null && messageAuditingConfig.OverrideTimeToBeReceived > TimeSpan.Zero)
            {
                behaviorConfig.ConfigureProperty(t => t.TimeToBeReceivedOnForwardedMessages, messageAuditingConfig.OverrideTimeToBeReceived);
            }
        }

        string GetConfiguredAuditQueue(FeatureConfigurationContext context)
        {
            var auditAddress = GetAuditQueueAddressFromAuditConfig(context);
            
            if (auditAddress == null)
            {
                // Check to see if the audit queue has been specified either in the registry as a global setting
                auditAddress = ReadAuditQueueNameFromRegistry();
            }
            return auditAddress;

        }

        string ReadAuditQueueNameFromRegistry()
        {
            var forwardQueue = RegistryReader.Read("AuditQueue");
            if (string.IsNullOrWhiteSpace(forwardQueue))
            {
                return null;
            }            
            return forwardQueue;
        }

        string GetAuditQueueAddressFromAuditConfig(FeatureConfigurationContext context)
        {
            var messageAuditingConfig = context.Settings.GetConfigSection<AuditConfig>();
            if (messageAuditingConfig != null && !string.IsNullOrWhiteSpace(messageAuditingConfig.QueueName))
            {
                return messageAuditingConfig.QueueName;
            }
            return null;
        }

        static ILog Logger = LogManager.GetLogger<Audit>();      
    }
}