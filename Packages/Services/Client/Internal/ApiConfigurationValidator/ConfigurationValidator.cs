using Adecco.WW.Packages.WebApi.Services.Client.Apim;
using Adecco.WW.Packages.WebApi.Services.Client.Exceptions;
using Adecco.WW.Packages.WebApi.Services.Client.Internal.Configuration;
using System;
using System.IO;
using System.Net;

namespace Adecco.WW.Packages.WebApi.Services.Client.Internal.ApiConfigurationValidator
{
    /// <summary>
    /// This class is responsible for validating the configuration settings for the API client.
    /// </summary>
    public static class ConfigurationValidator
    {
        /// <summary>
        /// Validates the configuration settings for the API client.
        /// This method checks for essential configuration values and throws exceptions if any are missing or invalid.
        /// Allows for the validation of configuration settings before they are used to create an API client.
        /// </summary>
        /// <typeparam name="TConfiguration"></typeparam>
        /// <param name="currentConfig"></param>
        /// <exception cref="ConfigurationException"></exception>
        public static void ValidateConfiguration<TConfiguration>(TConfiguration currentConfig)
        where TConfiguration : ApiConfiguration
        {
            // APIM Configuration specific validation
            if (currentConfig is ApimConfiguration apimConfig && string.IsNullOrEmpty(apimConfig.ApimKey))
                throw new ConfigurationException("ApimKey is required", currentConfig);
            // API Configuration specific validation
            if (string.IsNullOrEmpty(currentConfig.BaseUrl))
                throw new ConfigurationException("BaseUrl is required", currentConfig);
            if (currentConfig.FaultedRequestRetryCount < 0)
                throw new ConfigurationException("Retry count cannot be negative", currentConfig);
            if (currentConfig.FaultedRequestRetryDelay < 0)
                throw new ConfigurationException("Retry delay cannot be negative", currentConfig);
            if (currentConfig.FaultedRequestRetryCount > 5)
                throw new ConfigurationException("Retry count should not exceed 5", currentConfig);
            if (currentConfig.FaultedRequestRetryDelay > 30)
                throw new ConfigurationException("Retry delay should not exceed 30 seconds", currentConfig);
            if (currentConfig.ConcurrentConnectionLimit.HasValue && currentConfig.ConcurrentConnectionLimit < 1)
                throw new ConfigurationException("Concurrent Connection Limit must be ≥1", currentConfig);
            if (currentConfig.MaxQueuingActions < 0)
                throw new ConfigurationException("Max queuing actions cannot be negative", currentConfig);
            if (currentConfig.Timeout <= TimeSpan.Zero)
                throw new ConfigurationException("Timeout must be > 0", currentConfig);
            if (currentConfig.Timeout.TotalMilliseconds > int.MaxValue) // 24.2 days
                throw new ConfigurationException("Timeout must be ≤ 24.2 days", currentConfig);
            if (currentConfig.CircuitBreakerDurationSeconds <= 0)
                throw new ConfigurationException("Circuit breaker duration must be > 0", currentConfig);
            if (currentConfig.RefreshConnectionDelay <= TimeSpan.Zero)
                throw new ConfigurationException("Refresh connection delay must be > 0", currentConfig);
            if (currentConfig.PooledConnectionLifetime <= TimeSpan.Zero)
                throw new ConfigurationException("Pooled connection lifetime must be > 0", currentConfig);
            if (currentConfig.MaxConnectionsPerServer <= 0)
                throw new ConfigurationException("Max connections per server must be > 0", currentConfig);
            if (currentConfig.UseProxy && string.IsNullOrEmpty(currentConfig.ProxyUrl))
                throw new ConfigurationException("Proxy URL is required when UseProxy is enabled", currentConfig);
            if (currentConfig.MaxAutomaticRedirections < 0)
                throw new ConfigurationException("Max automatic redirections cannot be negative", currentConfig);
            if (currentConfig.MaxResponseContentBufferSize <= 0)
                throw new ConfigurationException("Max response buffer size must be > 0", currentConfig);
            if (currentConfig.DefaultRequestVersion == HttpVersion.Unknown)
                throw new ConfigurationException("HTTP version must be 1.0 or 1.1 or 2.0 or 3.0", currentConfig);
            if (currentConfig.MaxAutomaticRedirections < 0)
                throw new ConfigurationException("MaxAutomaticRedirections cannot be negative", currentConfig);
            if (currentConfig.UseCookies && currentConfig.AllowAutoRedirect && currentConfig.MaxAutomaticRedirections == 0)
                throw new ConfigurationException("MaxAutomaticRedirections must be >0 when using cookies/redirects", currentConfig);
            if (currentConfig.UseClientCertificate && string.IsNullOrEmpty(currentConfig.ClientCertificatePath))
                throw new ConfigurationException("ClientCertificatePath is required when UseClientCertificate is enabled", currentConfig);
            if (currentConfig.UseClientCertificate && !File.Exists(currentConfig.ClientCertificatePath))
                throw new ConfigurationException("Certificate file not found", currentConfig);
            if (currentConfig.RetryAfterHeaderMaxInSeconds < TimeSpan.Zero)
                throw new ConfigurationException("RetryAfterHeaderMaxInSeconds cannot be negative", currentConfig);
            if (currentConfig.RetryAfterHeaderMaxInSeconds > TimeSpan.FromHours(24))
                throw new ConfigurationException("RetryAfterHeaderMaxInSeconds should not exceed 24 hours", currentConfig);
            // Logging validation
            if (string.IsNullOrEmpty(currentConfig.RedactionText))
                throw new ConfigurationException("RedactionText is required", currentConfig);
            if (currentConfig.MaxBodyLogLength < 0)
                throw new ConfigurationException("MaxBodyLogLength cannot be negative", currentConfig);
            if (currentConfig.MaxBodyLogLength > 1024 * 1024) // 1MB max log length
                throw new ConfigurationException("MaxBodyLogLength should not exceed 1MB (1048576 characters)", currentConfig);
            if (currentConfig.SensitiveHeaders == null)
                throw new ConfigurationException("SensitiveHeaders cannot be null", currentConfig);
            if (currentConfig.SensitiveFieldNames == null)
                throw new ConfigurationException("SensitiveFieldNames cannot be null", currentConfig);

        }
    }
}
