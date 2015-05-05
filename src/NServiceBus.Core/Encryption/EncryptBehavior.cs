﻿namespace NServiceBus
{
    using System;
    using NServiceBus.Encryption;
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;

    class EncryptBehavior : Behavior<OutgoingContext>
    {
        EncryptionMutator messageMutator;

        public EncryptBehavior(EncryptionMutator messageMutator)
        {
            this.messageMutator = messageMutator;
        }

        public override void Invoke(OutgoingContext context, Action next)
        {
            var currentMessageToSend = context.MessageInstance;
            currentMessageToSend = messageMutator.MutateOutgoing(currentMessageToSend);
            context.MessageInstance = currentMessageToSend;
            next();
        }

        public class EncryptRegistration : RegisterStep
        {
            public EncryptRegistration()
                : base("InvokeEncryption", typeof(EncryptBehavior), "Invokes the encryption logic")
            {
                InsertAfter(WellKnownStep.MutateOutgoingMessages);
            }

        }
    }
}