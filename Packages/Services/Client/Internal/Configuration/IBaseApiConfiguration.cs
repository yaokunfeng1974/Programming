using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;

namespace Adecco.WW.Packages.WebApi.Services.Client.Internal.Configuration
{
    /// <summary>
    /// Interface for configuring HTTP client settings.
    /// </summary>
    internal interface IBaseApiConfiguration
    {
        #region Core Configuration
        /// <summary>
        /// Base address for the HTTP requests (e.g., "https://api.example.com")
        /// </summary>
        string BaseUrl { get; set; }

        /// <summary>
        /// Maximum time to wait for a response before timing out
        /// Default: 100 seconds
        /// </summary>
        TimeSpan Timeout { get; set; }

        /// <summary>
        /// Default headers to include with every request
        /// </summary>
        Dictionary<string, string> DefaultRequestHeaders { get; set; }
        #endregion

        #region Credentials
        /// <summary>
        /// Path to the token endpoint for OAuth2 authentication
        /// </summary>
        public string CredentialsPath { get; set; }
        /// <summary>
        /// Token type for authentication (e.g., "Bearer")
        /// </summary>
        public string CredentialsTokenType { get; set; }
        /// <summary>
        /// Credentials for authentication
        /// </summary>
        public Dictionary<string, string> CredentialsDictionary { get; set; }
        /// <summary>
        /// Token Response Fields
        /// </summary>
        public Dictionary<string, string> AuthenticationResponseDictionary { get; set; }

        #endregion

        #region mTLS/Client Certificate Configuration
        /// <summary>
        /// Enables client certificate authentication for mTLS. Default is false.
        /// </summary>
        public bool UseClientCertificate { get; set; }

        /// <summary>
        /// Path to the client certificate file (e.g., .pfx or .p12).
        /// Required when <see cref="UseClientCertificate"/> is true.
        /// </summary>
        public string ClientCertificatePath { get; set; }

        /// <summary>
        /// Password for the client certificate file. Leave empty if not password-protected.
        /// </summary>
        public string ClientCertificatePassword { get; set; }
        #endregion

        #region Resilience Policies
        /// <summary>
        /// Retry After Header Max In Seconds. Default is 3600.
        /// </summary>
        TimeSpan RetryAfterHeaderMaxInSeconds { get; set; } 
        /// <summary>
        /// Duration in seconds to keep the circuit breaker open after tripping
        /// Default: 30 seconds
        /// </summary>
        int CircuitBreakerDurationSeconds { get; set; }

        /// <summary>
        /// Number of retry attempts for transient failures
        /// Default: 5 retries
        /// </summary>
        int FaultedRequestRetryCount { get; set; }

        /// <summary>
        /// Delay in seconds between retry attempts
        /// Default: 2 seconds
        /// </summary>
        int FaultedRequestRetryDelay { get; set; }
        #endregion

        #region Concurrency & Throttling
        /// <summary>
        /// Maximum number of concurrent requests allowed
        /// Default: 100 concurrent requests
        /// </summary>
        int? ConcurrentConnectionLimit { get; set; }

        /// <summary>
        /// Maximum number of requests to queue when limit is reached
        /// Default: 0 (no queuing)
        /// </summary>
        int? MaxQueuingActions { get; set; }
        #endregion

        #region Connection Pooling & Lifetime
        /// <summary>
        /// Force connection refresh regardless of DNS TTL
        /// Default: false
        /// </summary>
        bool ForceRefreshConnection { get; set; }

        /// <summary>
        /// Time interval after which connections are forcibly refreshed
        /// Default: 5 minutes
        /// </summary>
        TimeSpan RefreshConnectionDelay { get; set; }

        /// <summary>
        /// Gets or sets the maximum concurrent connections allowed per server.
        /// </summary>
        int MaxConnectionsPerServer { get; set; }

        /// <summary>
        /// Maximum lifetime of pooled TCP connections
        /// Default: 2 minutes
        /// </summary>
        TimeSpan PooledConnectionLifetime { get; set; }
        #endregion

        #region HTTP Protocol Settings
        /// <summary>
        /// Preferred HTTP protocol version
        /// Default: HTTP/2
        /// </summary>
        Version DefaultRequestVersion { get; set; }

        /// <summary>
        /// Policy for handling HTTP version negotiation
        /// Default: RequestVersionOrLower
        /// </summary>
        HttpVersionPolicy DefaultVersionPolicy { get; set; }
        #endregion

        #region Content Handling
        /// <summary>
        /// Maximum buffer size for response content
        /// Default: 2GB (2147483647 bytes)
        /// </summary>
        long MaxResponseContentBufferSize { get; set; }

        /// <summary>
        /// Supported decompression methods for compressed responses
        /// Default: All (GZip, Deflate, Brotli)
        /// </summary>
        DecompressionMethods AutomaticDecompression { get; set; }
        #endregion

        #region SocketsHttpHandler Configuration
        /// <summary>
        /// Enable cookie handling for HttpClient
        /// Default: false
        /// </summary>
        bool UseCookies { get; set; }

        /// <summary>
        /// Allow automatic redirect following
        /// Default: true
        /// </summary>
        bool AllowAutoRedirect { get; set; }

        /// <summary>
        /// Maximum number of redirects to follow
        /// Default: 10 redirects
        /// </summary>
        int MaxAutomaticRedirections { get; set; }

        /// <summary>
        /// Enable proxy usage
        /// Default: false
        /// </summary>
        bool UseProxy { get; set; }

        /// <summary>
        /// Proxy server URL when UseProxy is true
        /// Format: http://proxy.example.com:8080
        /// </summary>
        string ProxyUrl { get; set; }
        #endregion

        #region Advanced Logging Setting
        /// <summary>
        /// Enable Logging Request Content. Default is true.
        /// </summary>
        public bool EnableLoggingRequestContent { get; set; } 
        /// <summary>
        /// Enable Logging Response Content. Default is true.
        /// </summary>
        public bool EnableLoggingResponseContent { get; set; }
        /// <summary>
        /// Enable Redaction of Sensitive Data in Logs. Default is false. When enabled, sensitive data in headers and body will be replaced with the value specified in <see cref="RedactionText"/>.
        /// </summary>
        public bool EnableRedaction { get; set; }
        /// <summary>
        /// Set or get Redaction Text to be used to replace Sensitive data.Default is ***REDACTED***.
        /// </summary>
        public string RedactionText { get; set; } 
        /// <summary>
        /// Set or get Max Body string Log Length. if content exceed this value, i will be truncated with prefix : ...(truncated). 
        /// </summary>
        public int MaxBodyLogLength { get; set; }
        /// <summary>
        /// Set Or Get Sensitive Headers HashSet to be used while logging Headers. Default is not null.
        /// </summary>
        public HashSet<string> SensitiveHeaders { get; set; }
        /// <summary>
        /// Set Or Get Sensitive Field Names HashSet to be used while logging.Content Default is not null.
        /// </summary>
        public HashSet<string> SensitiveFieldNames { get; set; }
        #endregion
    }

}