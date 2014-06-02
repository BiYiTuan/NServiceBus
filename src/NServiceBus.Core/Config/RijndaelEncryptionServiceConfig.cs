namespace NServiceBus.Config
{
    using System.Configuration;
    using Encryption.Rijndael;

    /// <summary>
    /// Used to configure <see cref="EncryptionService"/>.
    /// </summary>
    public class RijndaelEncryptionServiceConfig : ConfigurationSection
    {
        /// <summary>
        /// The encryption key.
        /// </summary>
        [ConfigurationProperty("Key", IsRequired = true)]
        public string Key
        {
            get
            {
                return this["Key"] as string;
            }
            set
            {
                this["Key"] = value;
            }
        }
    }
}
