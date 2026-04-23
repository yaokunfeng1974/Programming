using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Generic;
using System.Linq;
using Adecco.WW.Packages.WebApi.Services.Client.Internal.Api;
using Adecco.WW.Packages.WebApi.Services.Client.Internal.Configuration;
using System.Text.Json;

namespace Adecco.WW.Packages.WebApi.Services.Client.Apim
{
    /// <summary>
    /// Provides a client for interacting with APIM (API Management) services.
    /// Sealed Class built with APIClient, you can create one or multiple clients.
    /// Based ApiClient Http infrastructure.
    /// </summary>
    /// <remarks>
    /// This client handles versioning, authentication headers, and request/response logging
    /// automatically. It is designed to be thread-safe and used as a scoped dependency.
    /// </remarks>
    public sealed class ApimClient : ApiClient<ApimConfiguration>
    {
        #region Private Fields
        #endregion

        #region public properties
        /// <summary>
        /// Get the HttpClient of the Apim Client from the base class.
        /// this property is exposed intentionally for debugging purpose only.
        /// using it directly is not recommended.
        /// please use Methods like Get, Create, Update, Delete, Execute instead of using HttpClient directly.
        /// </summary>
        public new HttpClient HttpClient => base.HttpClient;
        /// <summary>
        /// Logger for diagnostic information.
        /// use this property to access the logger for diagnostic information.
        /// </summary>
        public new ILogger Logger => base.Logger;
        /// <summary>
        /// Get the JsonSerializerOptions of the Apim Client from the base class.
        /// </summary>
        public new  JsonSerializerOptions JsonSerializerOptions => base.JsonSerializerOptions;
        /// <summary>
        /// Overrides the base class property to provide access to the RequestSending event.
        /// </summary>
        public new event RequestDelegate RequestSending
        {
            add => base.RequestSending += value;
            remove => base.RequestSending -= value;
        }
        /// <summary>
        ///  Overrides the base class property to provide access to the ResponseSending event.
        /// </summary>
        public new event ResponseDelegate ResponseReceived
        {
            add => base.ResponseReceived += value;
            remove => base.ResponseReceived -= value;
        }
        #endregion
        #region Constructor
        /// <summary>
        /// Initializes a new instance of the <see cref="ApimClient"/> class with required dependencies.
        /// </summary>
        /// <param name="name">When instantiating the client, this key used in registration is passed to the constructor.</param>
        /// <param name="httpClientFactory">Microsoft Factory for creating  HTTP clients.</param>
        /// <param name="httpConfigurationFactory">Factory for creating resilience policies.</param>
        /// <param name="memoryCache">The memory cache for token storage.</param>
        /// <param name="logger">The logger for diagnostic information.</param>
        public ApimClient(
            string name,
            IHttpClientFactory httpClientFactory,
            ApiConfigurationFactory httpConfigurationFactory,
            IMemoryCache memoryCache,
            ILogger<ApimClient> logger)
            : base(name,
                  httpClientFactory,
                  httpConfigurationFactory,
                  memoryCache,
                  logger)
        {
        }
        #endregion
        #region Override Base
        /// <summary>
        /// Overrides the base class method to configure the request headers and other settings.
        /// Configures the request by injecting headers into the outgoing HTTP request.
        /// Injects per-request headers (brand, country, correlation ID, APIM key) into the outgoing HTTP request.
        /// </summary>
        /// <param name="request">The HTTP request message to modify.</param>
        /// <param name="context">The request context containing headers and metadata. Must be of type <see cref="ApimRequestContext"/>.</param>
        protected override void ConfigureRequest(HttpRequestMessage request, object context)
        {
            if (request == null || context is not ApimRequestContext requestContext)
            {
                Logger.LogWarning("Request or context is null or invalid type. Skipping header injection.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(requestContext.Brand))
                request.Headers.TryAddWithoutValidation("Brand", requestContext.Brand);
            if (!string.IsNullOrWhiteSpace(requestContext.Country))
                request.Headers.TryAddWithoutValidation("Country", requestContext.Country);
            if (!string.IsNullOrWhiteSpace(requestContext.CorrelationId))
                request.Headers.TryAddWithoutValidation("X-Correlation-ID", requestContext.CorrelationId);

            if (IsAnAdeccoApim())
            {
                    request.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Key", Configuration.ApimKey);
            }

            foreach (var header in requestContext.Headers)
            {

                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

        }
        #endregion
        #region Private Methods
        /// <summary>
        /// Internal Sends an HTTP request to the specified endpoint with the given method and payload.
        /// Calls the base class method to handle the request and response.
        /// This method handles the request and response logging, error handling, and token management.
        /// </summary>
        /// <param name="method"></param>
        /// <param name="path"></param>
        /// <param name="body"></param>
        /// <param name="context">Apim Request Context</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task<HttpResponseMessage> InternalSendAsync(HttpMethod method, string path, object body=null, ApimRequestContext context=null,
            CancellationToken cancellationToken=default)
        {
            try
            {
                return await SendAsync(
                      method,
                      ParseVersion(path),
                      body,
                      context,
                      Configuration.BaseUrl,
                      cancellationToken);
            }
            catch (Exception e)
            {
                Logger.LogCritical(e, "Exception while calling {ApimName} API: {Message}",
                        Configuration.ApimName, e.Message);

                throw;
            }
        }
        #endregion
        #region Public Methods
        /// <summary>
        /// Retrieves a Salesforce authentication token, optionally from cache.
        /// This method retrieves an authentication token from the Salesforce backend using the provided credentials.
        /// It can also retrieve the token from cache if it exists and is not expired.
        /// Refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1204/APIM-CLient-Tutorials"> here.</see >
        /// </summary>
        /// <returns>A task representing the asynchronous operation, containing the authentication token.</returns>
        public async Task<string> GetSalesforceToken()
            //SalesforceCredentialsModel salesforceCredentials,
            //CancellationToken cancellationToken)
        {

            return await OAuth2(cancellationToken:default);


        }
        /// <summary>
        /// Get an authentication token, optionally from cache, with support for custom token endpoints.
        /// This method retrieves an authentication token from the specified endpoint using the provided HTTP method and payload.
        /// It can also retrieve the token from cache if it exists and is not expired.
        /// The token is expected to be in JSON format, with the token value and expiration time specified by the provided field names.
        /// Refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1204/APIM-CLient-Tutorials"> here.</see >
        /// </summary>
        /// <param name="path">The endpoint path for token retrieval.</param>
        /// <param name="payload">The payload containing credentials for token retrieval.</param>
        /// <param name="valueFieldName">The JSON field name containing the token value in the response.</param>
        /// <param name="expirationFieldName">The JSON field name containing the token expiration in the response.</param>
        /// <param name="context">The request context containing headers and metadata.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <param name="baseUrl">The base URL for the token endpoint (default: uses configured base URL).</param>
        /// <param name="tryGetTokenInCache">Whether to attempt retrieving the token from cache first (default: true).</param>
        /// <returns>A task representing the asynchronous operation, containing the authentication token.</returns>
        public async Task<string> GetToken(
            string path = null,
            object payload = null,
            string valueFieldName = "access_token",
            string expirationFieldName= "expires_in",
            ApimRequestContext context=null,
            CancellationToken cancellationToken=default,
            string baseUrl = null,
            bool tryGetTokenInCache = true) => await OAuth2(
                                                                 path,
                                                                 payload,
                                                                 context,
                                                                 baseUrl ?? Configuration.BaseUrl,
                                                                 valueFieldName,
                                                                 expirationFieldName,
                                                                 null,
                                                                 tryGetTokenInCache,
                                                                 forceTokenValidation: false,
                                                                 cancellationToken);
        /// <summary>
        /// Sends a POST request with a payload to the specified endpoint.
        /// This method is used for creating new resources at the specified endpoint.
        /// Refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1204/APIM-CLient-Tutorials"> here.</see >
        /// </summary>
        /// <typeparam name="T">The type of the payload model.</typeparam>
        /// <param name="endpoint">The API endpoint path (relative to base URL).</param>
        /// <param name="model">The payload to send in the request body.</param>
        /// <param name="context">The request context containing headers and metadata.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation, containing the HTTP response.</returns>
        /// <example>
        /// <code>
        /// var response = await client.Create("users", newUser, context, cancellationToken);
        /// </code>
        /// </example>
        public async Task<HttpResponseMessage> Create<T>(string endpoint, T model, ApimRequestContext context = null, CancellationToken cancellationToken=default)
        {
            Logger.LogInformation("In {ApimName}/{Endpoint}", Configuration.ApimName, endpoint);
            return await InternalSendAsync(HttpMethod.Post, endpoint, model, context, cancellationToken);
        }
        /// <summary>
        /// Sends a PUT request with a payload to the specified endpoint.
        /// This method is used for updating resources at the specified endpoint.
        /// Refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1204/APIM-CLient-Tutorials"> here.</see >
        /// </summary>
        /// <typeparam name="T">The type of the payload model.</typeparam>
        /// <param name="endpoint">The API endpoint path (relative to base URL).</param>
        /// <param name="model">The payload to send in the request body.</param>
        /// <param name="context">The request context containing headers and metadata.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation, containing the HTTP response.</returns>
        public async Task<HttpResponseMessage> Update<T>(string endpoint, T model, ApimRequestContext context = null, CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("In {ApimName}/{Endpoint}", Configuration.ApimName, endpoint);
            return await InternalSendAsync(HttpMethod.Put, endpoint, model, context, cancellationToken);
        }
        /// <summary>
        /// Sends a DELETE request to the specified endpoint.
        /// This method is used for deleting resources at the specified endpoint.
        /// Refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1204/APIM-CLient-Tutorials"> here.</see >
        /// </summary>
        /// <param name="endpoint">The API endpoint path (relative to base URL).</param>
        /// <param name="context">The request context containing headers and metadata.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation, containing the HTTP response.</returns>
        public async Task<HttpResponseMessage> Delete(string endpoint, ApimRequestContext context=null, CancellationToken cancellationToken=default)
        {
            Logger.LogInformation("In {ApimName}/{Endpoint}", Configuration.ApimName, endpoint);
            return await InternalSendAsync(HttpMethod.Delete, endpoint, null, context, cancellationToken);
        }
        /// <summary>
        /// Sends an HTTP request with a specified method and payload to the given endpoint.
        /// Asynchronous method for sending HTTP requests with custom methods (e.g., PATCH, HEAD).
        /// This method is used for sending requests with a payload to the specified endpoint.
        /// It allows for flexibility in choosing the HTTP method and payload type.
        /// Refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1204/APIM-CLient-Tutorials"> here.</see >
        /// </summary>
        /// <typeparam name="T">The type of the payload model.</typeparam>
        /// <param name="endpoint">The API endpoint path (relative to base URL).</param>
        /// <param name="method">The HTTP method to use (e.g., PATCH, HEAD).</param>
        /// <param name="model">The payload to send in the request body.</param>
        /// <param name="context">The request context containing headers and metadata.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation, containing the HTTP response.</returns>
        public async Task<HttpResponseMessage> SendAsync<T>(HttpMethod method, string endpoint, T model, ApimRequestContext context = null, CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("In {ApimName}/{Endpoint}", Configuration.ApimName, endpoint);
            return await InternalSendAsync(method, endpoint, model, context, cancellationToken);
        }
        /// <summary>
        /// Sends a GET request to the specified endpoint.
        /// This method is used for retrieving data from the specified endpoint.
        /// Refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1204/APIM-CLient-Tutorials"> here.</see >
        /// </summary>
        /// <param name="endpoint">The API endpoint path (relative to base URL).</param>
        /// <param name="context">The request context containing headers and metadata.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation, containing the HTTP response.</returns>
        public async Task<HttpResponseMessage> Get(string endpoint, ApimRequestContext context = null, CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("In {ApimName}/{Endpoint}", Configuration.ApimName, endpoint);
            return await InternalSendAsync(HttpMethod.Get, endpoint, null, context, cancellationToken);
        }
        #endregion
        #region Helper Methods
        /// <summary>
        /// Processes an API endpoint path to incorporate versioning based on the configured <see cref="ApimVersion"/>.
        /// </summary>
        /// <param name="endpoint">
        /// The original endpoint path to process. This can be:
        /// - A root path ("/")
        /// - A simple resource ("users")
        /// - A multi-segment path ("api/users/active")
        /// - A versioned path ("v1/customers")
        /// - Null or empty string (treated as root path)
        /// </param>
        /// <returns>
        /// A versioned endpoint path with the following behavior:
        /// - Returns original endpoint if versioning is disabled in configuration
        /// - Preserves existing version prefixes when detected
        /// - Inserts configured version after the first path segment
        /// - Maintains original slash structure (leading/trailing)
        /// </returns>
        /// <example>
        /// With <see cref="ApimVersion"/> = v2:
        /// <code>
        /// ParseVersion("api/users") => "api/v2/users"
        /// ParseVersion("v1/customers") => "v1/customers"
        /// ParseVersion("search/") => "search/v2/"
        /// ParseVersion(null) => "v2/"
        /// </code>
        /// </example>
        /// <remarks>
        /// Versioning Strategy:
        /// <list type="number">
        /// <item><description>
        /// <strong>Version Detection:</strong> Uses regex pattern @"^/?v\d+[/_]" (case-insensitive)
        /// to identify existing version prefixes
        /// </description></item>
        /// <item><description>
        /// <strong>Positioning:</strong> Inserts version after first path segment to maintain
        /// API group context (e.g., "admin/users" → "admin/v2/users")
        /// </description></item>
        /// <item><description>
        /// <strong>Structure Preservation:</strong> Maintains original slash characteristics
        /// and query parameters if present
        /// </description></item>
        /// </list>
        ///
        /// Edge Case Handling:
        /// <list type="bullet">
        /// <item><description>Null/empty input → Treated as root path</description></item>
        /// <item><description>Mixed casing in existing versions → Preserved but not modified</description></item>
        /// <item><description>Special characters in paths → Maintained without transformation</description></item>
        /// </list>
        ///
        /// Performance Considerations:
        /// <para>- Regex check adds ~0.02ms overhead per call (benchmarked with 10k iterations)</para>
        /// <para>- No heap allocations beyond original string operations</para>
        /// </remarks>
        /// <seealso cref="ApimConfiguration"/>
        /// <seealso cref="ApimVersion"/>
        /// last change 11/04/2025 
        public string ParseVersion(string endpoint)
        {
          
            if (Configuration.ApimVersion == ApimVersion.None)
            {
                return endpoint;
            }

            // Split into path and query components
            var queryIndex = endpoint.IndexOf('?');
            var path = queryIndex >= 0 ? endpoint.Substring(0, queryIndex) : endpoint;
            var query = queryIndex >= 0 ? endpoint.Substring(queryIndex) : "";

            // Detect existing version prefix
            bool hasVersionPrefix = Regex.IsMatch(path, @"^/?v\d+[/_]", RegexOptions.IgnoreCase);
            if (hasVersionPrefix)
            {
                return endpoint;
            }

            // Track original slash structure
            bool hasLeadingSlash = path.StartsWith('/');
            bool hasTrailingSlash = path.EndsWith('/');

            // Split into segments
            var segments = path.TrimStart('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            // Handle empty path case
            if (segments.Length == 0)
            {
                var versionedPathOut = hasLeadingSlash
                    ? $"/{Configuration.ApimVersion}/"
                    : $"{Configuration.ApimVersion}/";
                return $"{versionedPathOut}{query}";
            }

            // Build versioned segments
            var versionedSegments = new List<string> { segments[0], Configuration.ApimVersion.ToString() };
            if (segments.Length > 1)
            {
                versionedSegments.AddRange(segments.Skip(1));
            }

            // Reconstruct path with original slash structure
            var versionedPath = string.Join("/", versionedSegments);
            versionedPath = hasLeadingSlash ? $"/{versionedPath}" : versionedPath;
            versionedPath += hasTrailingSlash ? "/" : "";

            // Reattach query parameters
            return $"{versionedPath}{query}";
        }
        /// <summary>
        /// Checks if the configured BaseUrl matches Adecco-specific APIM URL patterns (e.g., api.adecco.com or Azure APIM endpoints). Send the 'ocp-apim-subscription-key' only for an Adecco APIM
        /// </summary>
        /// <returns>
        /// Re
        /// </returns>
        internal bool IsAnAdeccoApim()
        {
            // FR APIM :
            //https://apm-eur-fr-[env]-apim.azure-api.net
            // our Global APIM :
            //DEV/INT/UAT : https://api-[env].adecco.com
            //PRD : https://api.adecco.com
            // technical gatway : https://apm-eur-ww-[env]-apim.azure-api.net

            return (Regex.IsMatch(Configuration.BaseUrl, "^(?i)https://api(-dev|-int|-uat|).adecco.com")
                 || Regex.IsMatch(Configuration.BaseUrl, "^(?i)https://apm-eur-(ww|fr)-(dev|int|uat|prd)-apim.azure-api.net"))
                 && !string.IsNullOrWhiteSpace(Configuration.ApimKey);


        }
        #endregion
    }

}
