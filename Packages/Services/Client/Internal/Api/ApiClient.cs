using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Adecco.WW.Packages.WebApi.Exceptions;
using Adecco.WW.Packages.WebApi.Services.Client.Exceptions;
using Adecco.WW.Packages.WebApi.Services.Client.Internal.Configuration;
using Adecco.WW.Packages.WebApi.Services.Client.Internal.Security;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: InternalsVisibleTo("Adecco.WW.Packages.WebApi.Tests")]
namespace Adecco.WW.Packages.WebApi.Services.Client.Internal.Api
{
    /// <summary>
    /// Delegate for handling HTTP response events.
    /// </summary>
    /// <param name="Request"></param>
    /// <param name="Context"></param>
    public delegate void ResponseDelegate(HttpResponseMessage Request, object Context);
    /// <summary>
    /// Delegate for handling HTTP Request events.
    /// </summary>
    /// <param name="Request"></param>
    /// <param name="Context"></param>
    public delegate void RequestDelegate(HttpRequestMessage Request, object Context);
    /// <summary>
    /// Abstract base class providing core HTTP client functionality for connecting to external services.
    /// Implements token management, request/response handling, and resilience patterns.
    /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
    /// </summary>
    /// <remarks>
    /// This class is designed to be inherited by specific service clients. It handles:
    /// <list type="bullet">
    /// <item><description>HTTP request/response lifecycle</description></item>
    /// <item><description>Response processing and error handling</description></item>
    /// </list>
    /// </remarks>
    public abstract class ApiClient<TConfiguration> : IApiClient
         where TConfiguration : ApiConfiguration
    {

        #region Constants & Fields
        internal readonly SemaphoreSlim _tokenRefreshSemaphoreSlim = new(1, 1);
        internal readonly object _lock = new Object();
        private string _cacheTokenKey = null;
        private string _tokenFieldName = "access_token";
        private string _expirationFieldName = "expires_in";
        private readonly object _cancellationLock = new object();

        /// <summary>
        /// Default token expiration time in seconds.
        /// </summary>
        public int TokenexpiresIn { get; set; }=3600; // Default token expiration time in seconds (1 hour).
        /// <summary>
        /// Memory cache entry options for token caching.
        /// </summary>
        public MemoryCacheEntryOptions TokenCacheOptions { get; internal set; }
        /// <summary>
        /// Memory cache entry options for token key caching.
        /// </summary>
        public MemoryCacheEntryOptions TokenKeyCacheOptions { get; internal set; }
        /// <summary>
        /// private instance of IMemoryCache.
        /// </summary>
        private readonly IMemoryCache _memoryCache;
        /// <summary>
        /// private instance of HttpClient.
        /// </summary>
        private readonly HttpClient _httpClient;
        /// <summary>
        /// Configuration settings for the API client.
        /// This configuration is set during registration in Startup.cs and gets injected in the constructor.
        /// </summary>
        private readonly TConfiguration _configuration;
        /// <summary>
        /// Logger instance for logging messages and errors.
        /// </summary>
        private readonly ILogger _logger;

        #endregion
        #region Properties
        /// <summary>
        /// Gets or sets the key used for caching tokens.
        /// This key is used to store and retrieve tokens from the memory cache.
        /// if you want to use multiple Tokens for the same client :
        /// retrieve the token using different key for each token.
        /// you can set this key to a different value when you make the call.
        /// If not set, a default key will be used.
        /// default key= {ClientName}_AuthToken_{Configuration.BaseUrl}
        /// </summary>
        public string CacheTokenKey
        {
            get => _cacheTokenKey;
            set
            {
                if (string.IsNullOrEmpty(value))
                    _cacheTokenKey = $"{ClientName}_AuthToken_{Configuration.BaseUrl}";
                else
                    _cacheTokenKey = value;
            }
        }
        /// <summary>
        /// Gets the memory cache instance used for caching tokens and other data.
        /// </summary>
        public IMemoryCache MemoryCache => _memoryCache; // Exposed for caching purposes.
        /// <summary>
        /// Gets Logger instance for logging messages and errors.
        /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
        /// </summary>
        public ILogger Logger => _logger; // Exposed for logging purposes.
        /// <summary>
        /// Gets the name of the client Instance used in registration,Exposed readOnly Property only for logging purposes.
        /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
        /// </summary>
        public string ClientName { get; } = string.Empty; // Expose the Client name used during registration and logging purposes.
        /// <summary>
        /// Gets the HTTP client instance. Use this property to access the configured HttpClient.
        /// Use this property with caution, please refer to the documentation for more details.
        /// use SendAsync()/PostAsync()/GetAsync() method to send requests instead of using this property directly.
        /// they automatically handle logging, error handling, and request management.
        /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
        /// </summary>
        public HttpClient HttpClient => _httpClient;
        /// <summary>
        /// Gets this Api Base Type configuration settings of this API client.
        /// This configuration is set during registration in Startup.cs.
        /// this configuration Dynamic object and represents the configuration used at registration time with Method BindApiClient().
        /// Dynamic object is used to allow for flexibility in the configuration structure.
        /// use this property to access the configuration settings after you cast it to the appropriate type.
        /// you can also call GetConfiguration() method to get the configuration settings.
        /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
        /// </summary>
        public TConfiguration Configuration => _configuration;
        /// <summary>
        /// Gets or sets the JSON serializer options for System.Text.Json serialization/deserialization.
        /// When set, this will be used instead of Newtonsoft.Json settings.
        /// </summary>
        public JsonSerializerOptions JsonSerializerOptions { get; set; } = new JsonSerializerOptions
        {
            PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase // Example configuration
        };
        #endregion
        #region Events
        /// <summary>
        /// Occurs when a cached token is evicted (e.g., expired or removed).
        /// </summary>
        /// <remarks>
        /// Parameters:
        /// <list type="bullet">
        /// <item><term>cacheKey</term><description>The key used to store the token in the cache.</description></item>
        /// <item><term>token</term><description>The evicted token value.</description></item>
        /// <item><term>reason</term><description>Reason for eviction (e.g., expired, removed).</description></item>
        /// </list>
        /// Subscribe to this event to handle token expiration logic.
        /// </remarks>
        /// <example>
        /// <code>
        /// apiClient.TokenEvicted += (key, token, reason) => 
        /// {
        ///    Console.WriteLine($"Token evicted: Key={key}, Reason={reason}");
        ///    // Add custom logic (e.g., pre-fetch a new token)
        /// };
        /// </code>
        /// </example>
        public event Action<string, string, EvictionReason> TokenEvicted;
        /// <summary>
        /// Occurs when an HTTP request is about to be sent.
        /// </summary>
        public event RequestDelegate RequestSending;
        /// <summary>
        /// Occurs when an HTTP response is received.
        /// </summary>
        public event ResponseDelegate ResponseReceived;
        /// <summary>
        /// Raises the RequestSending event.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        internal void RaiseRequestSending(HttpRequestMessage request, object context)
    => RequestSending?.Invoke(request, context);
        /// <summary>
        /// Raises the ResponseReceived event.
        /// </summary>
        /// <param name="response"></param>
        /// <param name="context"></param>
        internal void RaiseResponseReceived(HttpResponseMessage response, object context)
            => ResponseReceived?.Invoke(response, context);

        #endregion
        #region Constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="Api"/> class with explicit dependencies.
        /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
        /// </summary>
        /// <param name="Name">
        /// When instantiating the client, this key used in registration is passed to the constructor.
        /// </param>
        /// <param name="httpClientFactory">Factory for creating HTTP clients.</param>
        /// <param name="httpConfigurationFactory"></param>
        /// <param name="memoryCache">Memory cache instance for token caching.</param>
        /// <param name="logger">Logger instance.</param>
        protected ApiClient(string Name,
            IHttpClientFactory httpClientFactory,
            ApiConfigurationFactory httpConfigurationFactory,
            IMemoryCache memoryCache,
            ILogger logger)
        {
            _memoryCache = memoryCache;
            _logger = logger;
            // Check if the name is null or empty
            if (string.IsNullOrEmpty(Name))
                throw new ClientRegistrationException($"Error while trying to create the Instance of the Client Of Type: {GetType().Name}, the Name for the Client used in Registration is null or empty.")
                { HelpLink = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1205/Custom-API-client-Tutorials" };
            // Create the HttpClient instance using the factory
            // and get the configuration for this client
            // using the name passed in the constructor.
            // This is used to configure the HttpClient instance.
            ClientName = Name;
            _httpClient = httpClientFactory.CreateClient(Name);
            _configuration = httpConfigurationFactory.GetConfiguration<TConfiguration>(Name);
            
        }
        /// <summary>
        /// Initializes a new instance of the <see cref="Api"/> class using service provider.
        /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="serviceProvider">Service provider for dependency resolution.</param>
        protected ApiClient(string Name, IServiceProvider serviceProvider)
        {
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var httpConfigurationFactory = serviceProvider.GetRequiredService<ApiConfigurationFactory>();
            _logger = serviceProvider.GetRequiredService<ILogger<ApiClient<TConfiguration>>>();
            _memoryCache = serviceProvider.GetRequiredService<IMemoryCache>();
            // Check if the name is null or empty
            if (string.IsNullOrEmpty(Name))
                throw new ClientRegistrationException($"Error while trying to create the Instance of the Client Of Type: {GetType().Name}, the Name for the Client used in Registration is null or empty.")
                { HelpLink = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1205/Custom-API-client-Tutorials" };
            // Create the HttpClient instance using the factory
            // and get the configuration for this client
            // using the name passed in the constructor.
            // This is used to configure the HttpClient instance.
            ClientName = Name;
            _httpClient = httpClientFactory.CreateClient(Name);
            _configuration = httpConfigurationFactory.GetConfiguration<TConfiguration>(Name);

        }
        #endregion
        #region Core Request Sending and Processing
        #region Asynchronous Send
        /// <summary>
        /// Sends an HTTP request and processes the response.
        /// This method handles the request lifecycle, including error handling and response processing.
        /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
        /// </summary>
        /// <typeparam name="T">Type of the expected response.</typeparam>
        /// <param name="method">HTTP method for the request.</param>
        /// <param name="domain">Target domain for the request. if empty the Configuration.BaseUrl will be used</param>
        /// <param name="path">Endpoint path for the request.</param>
        /// <param name="param">
        /// Request parameters.
        /// The method takes a generic payload input and intelligently wraps it into the appropriate HttpContent type based on the payload's data format.
        /// This ensures compatibility with HTTP request requirements. refer to documentation <see href="https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1220/ApiClient-Documentation?anchor=how-param-or-request-body-is-constructed-%3A">here.</see>
        /// </param>
        /// <param name="context">Additional context for the request.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>
        /// Deserialized response of type T.
        /// This method handles the disposal of the response message.
        /// </returns>
        public async Task<T> SendAsync<T>(HttpMethod method, string path, object param, object context = null, string domain = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(domain))
                domain = Configuration.BaseUrl;
            using (var requestResult = await InternalSendAsync(method, domain, path, param, context, cancellationToken))
            {
                return await ProcessHttpResponseAsync<T>(requestResult, context, cancellationToken);

            }// ProcessHttpResponseAsync now handles disposal
        }
        /// <summary>
        /// Sends an HTTP request using a custom request getter and processes the response.
        /// This method handles the request lifecycle, including error handling and response processing.
        /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
        /// </summary>
        /// <typeparam name="T">Type of the expected response.</typeparam>
        /// <param name="requestGetter">Function that creates the HTTP request message.</param>
        /// <param name="context">Additional context for the request.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Deserialized response of type T.</returns>
        public async Task<T> SendAsync<T>(Func<HttpRequestMessage> requestGetter, object context=null, CancellationToken cancellationToken = default)
        {
            using (var requestResult = await InternalSendAsync(requestGetter, context, cancellationToken))
                return await ProcessHttpResponseAsync<T>(requestResult, context, cancellationToken);// ProcessResponse now handles disposal
        }

        /// <summary>
        /// Sends an HTTP request asynchronously using the provided request message.
        /// Applies ApiClient-specific headers and configurations before sending.
        /// this method don't take context as parameter, it is used in the advanced overloads of SendAsync method.
        /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
        /// </summary>
        /// <param name="request">The HTTP request message to send.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <param name="context"> </param>
        /// <returns>The HTTP response message.</returns>
        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default, object context = null)
        {
            // Send the request as it is
            return await InternalSendAsync(request, context, cancellationToken);
        }
        /// <summary>
        /// Sends an HTTP request and returns the response message.
        /// This method is a wrapper around SendRequestAsync to provide a consistent interface.
        /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
        /// </summary>
        /// <param name="httpMethod">HTTP method for the request.</param>
        /// <param name="domain">Target domain for the request.</param>
        /// <param name="path">Endpoint path for the request. if empty the Configuration.BaseUrl will be used</param>
        /// <param name="requestBody">Request body content.</param>
        /// <param name="context">Additional context for the request.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>HTTP response message.</returns>
        public async Task<HttpResponseMessage> SendAsync(HttpMethod httpMethod, string path, object requestBody, object context=null, string domain = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(domain))
                domain = Configuration.BaseUrl;
            return await InternalSendAsync(httpMethod, domain, path, requestBody, context, cancellationToken);
        }
        /// <summary>
        /// Sends an HTTP request and retrieves the response content as a string.
        /// This method is a wrapper around SendRequestAsync to provide a consistent interface.
        /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
        /// </summary>
        /// <param name="httpMethod">HTTP method for the request.</param>
        /// <param name="domain">Target domain for the request. if empty the Configuration.BaseUrl will be used</param>
        /// <param name="path">Endpoint path for the request.</param>
        /// <param name="requestBody">Request body content.</param>
        /// <param name="context">Additional context for the request.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Response content as string.</returns>
        public async Task<string> RetrieveResponseContentAsStringAsync(HttpMethod httpMethod, string path, object requestBody, object context = null, string domain = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(domain))
                domain = Configuration.BaseUrl;
            var requestResult = await InternalSendAsync(httpMethod, domain, path, requestBody, context, cancellationToken);
            return await ConfigureResponseAsync(requestResult, context, cancellationToken);
        }
        #endregion
        #endregion
        #region Helpers Request Sending and Processing
        /// <summary>
        /// OAuth2 capability for token retrieval, caching and automatic injection into future requests.
        /// default key= {ClientName}_AuthToken_{Configuration.BaseUrl}.
        /// Caching Mechanism: The token is cached using a key based on the client's name and domain, ensuring it's reused for all requests to the same domain.
        /// Automatic Token Injection: The HttpClient.SendAsync() method now checks the cache for a valid token.
        /// and automatically adds it to the Authorization header of every request.
        /// Expiration Handling: The token's expiration is managed by the cache (with a buffer),
        /// so expired tokens are automatically evicted, requiring a fresh fetch on subsequent calls to OAuth2.
        /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
        /// </summary>
        /// <param name="path">Endpoint path for token request.</param>
        /// <param name="Body">Request payload containing credentials.</param>
        /// <param name="domain">Domain of the token service. if empty the Configuration.BaseUrl will be used</param>
        /// <param name="tokenFieldName">JSON field name containing the token value.</param>
        /// <param name="expirationFieldName">JSON field name containing token expiration.</param>
        /// <param name="UserCacheTokenKey"> you can set this key to a different value when you make the call.</param>
        /// <param name="context">Additional context for the request.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <param name="useCachedToken">Whether to attempt cache retrieval first.</param>
        /// <param name="forceTokenValidation">Fore a local validation of the cached token. this paramter is useful for 401 failed requests to be able to detect faulted cached token</param>
        /// <returns>Authentication token string.</returns>
        /// <exception cref="ApiException">Thrown when token request fails or response is invalid.</exception>
        /// <exception cref="Exception">Thrown for general operation failures.</exception>
        /// <example>
        /// <code>
        /// // Fetch and cache the token once
        /// var token = await OAuth2("/oauth/token", new { client_id, client_secret });
        /// // Subsequent requests will automatically include the cached token
        /// var response = await SendAsync(HttpMethod.Get, "/api/resource");
        /// </code>
        /// </example>
        public async Task<string> OAuth2(
            string path = null,
            object Body = null,
            object context = null,
            string domain = null,
            string tokenFieldName = "access_token",
            string expirationFieldName = "expires_in",
            string UserCacheTokenKey = null,
            bool useCachedToken = true,
            bool forceTokenValidation = false,
            CancellationToken cancellationToken = default)
        {
            bool semaphoreAcquired = false;

            try
            {
                // Initialize field names based on user input or defaults and validate configuration
                InitializeFieldNames(tokenFieldName, expirationFieldName);
                ValidateConfiguration(ref path, ref Body);
                domain = GetDomain(domain);
                CacheTokenKey = GetCacheTokenKey(UserCacheTokenKey, domain);
                // Check cache first if requested
                if (useCachedToken && TryGetValidCachedToken(forceTokenValidation, out var cachedToken))
                {
                    return cachedToken;
                }
                // Try to acquire semaphore immediately
                 semaphoreAcquired = await _tokenRefreshSemaphoreSlim.WaitAsync(TimeSpan.Zero, cancellationToken);

                if (!semaphoreAcquired)
                {
                    // If we couldn't acquire the semaphore immediately, wait for either the semaphore or cancellation
                    await _tokenRefreshSemaphoreSlim.WaitAsync(cancellationToken);
                    // After waiting, return any valid cached token if available after waiting
                    if (TryGetValidCachedToken(forceTokenValidation, out var cachedTokenValue))
                    {
                        return cachedTokenValue;
                    }
                }
                // Double-check cache after acquiring lock
                if (useCachedToken && TryGetValidCachedToken(forceTokenValidation, out cachedToken))
                {
                    return cachedToken;
                }
                // Fetch and cache new token
                return await FetchAndCacheNewToken(path, Body, context, domain, cancellationToken);
            }
            catch (Exception ex)
            {
                // Log the exception
                _logger.LogError(ex, "Error while trying to get the token, trying to return any valid cached token if available");
                // If fetching a new token fails, return any valid cached token if available
                if (TryGetValidCachedToken(forceTokenValidation, out var cachedTokenValue))
                {
                    return cachedTokenValue;
                }
                throw; // Rethrow original exception if no valid cached token
            }
            finally
            {
                if (semaphoreAcquired) // Only release if we acquired it
                {
                    _tokenRefreshSemaphoreSlim.Release();// Release the semaphore
                }
            }
        }
        /// <summary>
        /// Initializes field names for token and expiration based on user input or defaults.
        /// </summary>
        /// <param name="tokenFieldName"></param>
        /// <param name="expirationFieldName"></param>
        private void InitializeFieldNames(string tokenFieldName, string expirationFieldName)
        {
            // if user set a different token field name, set it to the _tokenFieldName to use it for future calls
            if (!string.IsNullOrEmpty(tokenFieldName))
                _tokenFieldName = tokenFieldName;

            // if user set a different expiration field name, set it to the _expirationFieldName to use it for future calls
            if (!string.IsNullOrEmpty(expirationFieldName))
                _expirationFieldName = expirationFieldName;
        }
        /// <summary>
        /// Validates the OAuth2 configuration.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="Body"></param>
        /// <exception cref="ConfigurationException"></exception>
        private void ValidateConfiguration(ref string path, ref object Body)
        {
            // if path is null, look for configuration CredentialsPath
            if (string.IsNullOrEmpty(path))
                path = Configuration.CredentialsPath;

            // if Body is null, look for configuration Credentials
            if (Body == null)
                Body = Configuration.CredentialsDictionary;

            // check if any of path or Body still null
            if (string.IsNullOrEmpty(path) || Body == null)
            {
                throw new ConfigurationException(
                    "OAuth2 configuration is incomplete. Ensure OAuth2Path and Credentials are set.", Configuration);
            }

            // if Body Dictionary is still empty
            if (Body is Dictionary<string, string> keyValuePairs && keyValuePairs.Count == 0)
            {
                throw new ConfigurationException(
                    "OAuth2 configuration is incomplete. Ensure CredentialsDictionary is not Empty.", Configuration);
            }
        }
        /// <summary>
        /// Gets the domain for the request.
        /// </summary>
        /// <param name="domain"></param>
        /// <returns></returns>
        private string GetDomain(string domain)
        {
            // if user don't set a different domain, set it from the Configuration.BaseUrl
            if (string.IsNullOrEmpty(domain))
                domain = Configuration.BaseUrl;
            return domain;
        }
        /// <summary>
        /// Generates the cache token key based on user input or defaults.
        /// </summary>
        /// <param name="UserCacheTokenKey"></param>
        /// <param name="domain"></param>
        /// <returns></returns>
        private string GetCacheTokenKey(string UserCacheTokenKey, string domain)
        {
            // if user set a different token key, set it to the CacheTokenKey to use it for future calls
            if (!string.IsNullOrEmpty(UserCacheTokenKey))
                return UserCacheTokenKey;

            // else use default key value "{ClientName}_AuthToken_{domain}"
            return $"{ClientName}_AuthToken_{domain}";
        }
        /// <summary>
        /// Tries to retrieve a valid cached token.
        /// </summary>
        /// <param name="forceTokenValidation"></param>
        /// <param name="cachedToken"></param>
        /// <returns></returns>
        private bool TryGetValidCachedToken(bool forceTokenValidation, out string cachedToken)
        {
            cachedToken = null;
            if (!_memoryCache.TryGetValue(CacheTokenKey, out cachedToken))
                return false;

            if (!forceTokenValidation)
            {
                _logger.LogDebug("Using cached token without validation");
                return true;
            }

            // Validate token if requested
            if (ValidateToken(cachedToken))
            {
                _logger.LogDebug("Using validated cached token");
                return true;
            }

            _logger.LogWarning("Cached token is invalid. Forcing refresh.");
            _memoryCache.Remove(CacheTokenKey); // Clear invalid token
            return false;
        }

        /// <summary>
        /// Fetches a new token from the OAuth2 endpoint and caches it.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="Body"></param>
        /// <param name="context"></param>
        /// <param name="domain"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="ApiException"></exception>
        private async Task<string> FetchAndCacheNewToken(
            string path,
            object Body,
            object context,
            string domain,
            CancellationToken cancellationToken)
        {
            _logger.LogDebug("Fetching token from {path}", path);

            using var response = await SendAsync(
                HttpMethod.Post,
                path,
                Body,
                context,
                domain,
                cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Token response: {statusCode}", response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                var ErrorcontentString = await response.Content.ReadAsStringAsync();
                throw new ApiException($"Cannot get token: [code='{response.StatusCode}'/content='{ErrorcontentString}']");
            }

            var (newToken, expiresIn) = await ParseTokenResponse(response, cancellationToken);
            CacheNewToken(newToken, expiresIn);

            return newToken;
        }
        /// <summary>
        /// Parses the token response from the HTTP response message.
        /// </summary>
        /// <param name="response"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="ApiException"></exception>
        private async Task<(string token, int expiresIn)> ParseTokenResponse(
            HttpResponseMessage response,
            CancellationToken cancellationToken)
        {
            using JsonDocument responseDoc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken));

            var root = responseDoc.RootElement;
            UpdateAuthenticationResponseDictionary(root);

            // Extract token using JSON path
            var tokenElement = root;
            foreach (var segment in _tokenFieldName.Split('.'))
            {
                if (!tokenElement.TryGetProperty(segment, out tokenElement))
                {
                    throw new ApiException($"Token field '{_tokenFieldName}' not found in response");
                }
            }

            var newToken = tokenElement.GetString();
            if (string.IsNullOrEmpty(newToken))
            {
                throw new ApiException($"'{_tokenFieldName}' field was null in back-end response.");
            }

            var expiresIn = ParseExpirationTime(root);
            return (newToken, expiresIn);
        }
        /// <summary>
        /// Updates the authentication response dictionary with key-value pairs from the JSON response.
        /// </summary>
        /// <param name="root"></param>
        private void UpdateAuthenticationResponseDictionary(JsonElement root)
        {
            Configuration.AuthenticationResponseDictionary ??= new Dictionary<string, string>();
            Configuration.AuthenticationResponseDictionary.Clear();

            foreach (var prop in root.EnumerateObject())
            {
                Configuration.AuthenticationResponseDictionary.Add(prop.Name, prop.Value.GetRawText());
            }
        }

        /// <summary>
        /// Parses the expiration time from the JSON response.
        /// </summary>
        /// <param name="root"></param>
        /// <returns></returns>
        private int ParseExpirationTime(JsonElement root)
        {
            try
            {
                var expiresInElement = root;
                foreach (var segment in _expirationFieldName.Split('.'))
                {
                    if (!expiresInElement.TryGetProperty(segment, out expiresInElement))
                    {
                        break;
                    }
                }

                return expiresInElement.ValueKind switch
                {
                    JsonValueKind.Number => expiresInElement.GetInt32(),
                    JsonValueKind.String when int.TryParse(expiresInElement.GetString(), out var parsed) => parsed,
                    _ => TokenexpiresIn // Fallback to default
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to parse expiration time from JSON. Using default 1 hour."
                );
                return 3600; // Default 1 hour
            }
        }
        /// <summary>
        /// Caches the new token with expiration settings.
        /// </summary>
        /// <param name="newToken"></param>
        /// <param name="expiresIn"></param>
        private void CacheNewToken(string newToken, int expiresIn)
        {
            // Set cache Options for token with expiration (with 30-second buffer)
            TokenCacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromSeconds(Math.Max(expiresIn - 30, 30)));

            TokenKeyCacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromSeconds(Math.Max(expiresIn - 30, 30)));

            TokenCacheOptions.PostEvictionCallbacks.Add(new PostEvictionCallbackRegistration
            {
                EvictionCallback = (key, value, reason, state) =>
                {
                    var client = (ApiClient<TConfiguration>)state;
                    client.OnTokenEvicted(key.ToString(), value as string, reason);
                },
                State = this
            });

            lock (_lock)
            {
                _memoryCache.Set(CacheTokenKey, newToken, TokenCacheOptions);
                _memoryCache.Set($"{ClientName}_AuthToken_Key", CacheTokenKey, TokenKeyCacheOptions);
            }

            _logger.LogInformation("Cached new authentication token for {Seconds} seconds", expiresIn);
        }
        /// <summary>
        /// Makes a GET request and deserializes the response.
        /// Retrieves data from a specified resource.
        /// Uses the specified domain and path to construct the request URL.
        /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
        /// </summary>
        /// <typeparam name="T">Type of the expected response.</typeparam>
        /// <param name="domain">Target domain for the request. if empty the Configuration.BaseUrl will be used</param>
        /// <param name="context"></param>
        /// <param name="path">Endpoint path for the request.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Deserialized response of type T.</returns>
        public async Task<T> GetAsync<T>(string path, object context = null, string domain = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(domain))
                domain = Configuration.BaseUrl;
            return await SendAsync<T>(HttpMethod.Get, path, null, context, domain, cancellationToken);
        }
        /// <summary>
        /// Makes a GET request with context and deserializes the response.
        /// Retrieves data from a specified resource.
        /// Uses the specified domain and path to construct the request URL. and adds the context to the request.
        /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
        /// </summary>
        /// <param name="domain">Target domain for the request. if empty the Configuration.BaseUrl will be used</param>
        /// <param name="path">Endpoint path for the request.</param>
        /// <param name="context">Additional context for the request.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Deserialized response of type T.</returns>
        public async Task<HttpResponseMessage> GetAsync(string path, object context=null, string domain = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(domain))
                domain = Configuration.BaseUrl;
            return await InternalSendAsync(HttpMethod.Get, domain, path,null, context, cancellationToken);
        }
        /// <summary>
        /// Makes a POST request and deserializes the response.
        /// Sends data to a server to create or update a resource.
        /// Uses the specified domain and path to construct the request URL and adds the request body.
        /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
        /// </summary>
        /// <typeparam name="T">Type of the expected response.</typeparam>
        /// <param name="domain">Target domain for the request. if empty the Configuration.BaseUrl will be used</param>
        /// <param name="path">Endpoint path for the request.</param>
        /// <param name="requestBody"> Request body content.</param>
        /// <param name="context"></param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Deserialized response of type T.</returns>
        public async Task<T> PostAsync<T>(string path, object requestBody,object context = null, string domain = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(domain))
                domain = Configuration.BaseUrl;
            return await SendAsync<T>(HttpMethod.Post, path, requestBody, context, domain, cancellationToken);
        }
        /// <summary>
        /// Makes a POST request with context and deserializes the response.
        /// Sends data to a server to create or update a resource.
        /// Uses the specified domain and path to construct the request URL.
        /// Adds parameters to the request body.
        /// Adds the context to the request.
        /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
        /// </summary>
        /// <typeparam name="T">Type of the expected response.</typeparam>
        /// <param name="domain">Target domain for the request.</param>
        /// <param name="path">Endpoint path for the request.</param>
        /// <param name="bodyContent">Request parameters.</param>
        /// <param name="context">Additional context for the request.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Deserialized response of type T.</returns>
        public async Task<HttpResponseMessage> PostAsync(string path, object bodyContent, object context=null, string domain = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(domain))
                domain = Configuration.BaseUrl;
            return await InternalSendAsync(HttpMethod.Post, domain, path, bodyContent: bodyContent, context, cancellationToken);
        }
        /// <summary>
        /// Makes a Put request and deserializes the response.
        /// Replaces a target resource with the request payload.
        /// Uses the specified domain and path to construct the request URL and adds the request body.
        /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
        /// </summary>
        /// <typeparam name="T">Type of the expected response.</typeparam>
        /// <param name="domain">Target domain for the request. if empty the Configuration.BaseUrl will be used</param>
        /// <param name="path">Endpoint path for the request.</param>
        /// <param name="requestBody"> Request body content.</param>
        /// <param name="context"></param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Deserialized response of type T.</returns>
        public async Task<T> PutAsync<T>(string path, object requestBody, object context = null, string domain = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(domain))
                domain = Configuration.BaseUrl;
            return await SendAsync<T>(HttpMethod.Put, path, requestBody, context, domain, cancellationToken);
        }
        /// <summary>
        /// Makes a Put request with context and deserializes the response.
        /// Replaces a target resource with the request payload.
        /// Uses the specified domain and path to construct the request URL.
        /// Adds parameters to the request body.
        /// Adds the context to the request.
        /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
        /// </summary>
        /// <typeparam name="T">Type of the expected response.</typeparam>
        /// <param name="domain">Target domain for the request. if empty the Configuration.BaseUrl will be used</param>
        /// <param name="path">Endpoint path for the request.</param>
        /// <param name="bodyContent">Request parameters.</param>
        /// <param name="context">Additional context for the request.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Deserialized response of type T.</returns>
        public async Task<HttpResponseMessage> PutAsync(string path, object bodyContent, object context = null, string domain = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(domain))
                domain = Configuration.BaseUrl;
            return await InternalSendAsync(method: HttpMethod.Put, domain, path, bodyContent: bodyContent, context,  cancellationToken);
        }
        /// <summary>
        /// Makes a Delete request and deserializes the response.
        /// Requests the deletion of a target resource.
        /// Uses the specified domain and path to construct the request URL and adds the request body.
        /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
        /// </summary>
        /// <typeparam name="T">Type of the expected response.</typeparam>
        /// <param name="domain">Target domain for the request. if empty the Configuration.BaseUrl will be used</param>
        /// <param name="path">Endpoint path for the request.</param>
        /// <param name="requestBody"> Request body content.</param>
        /// <param name="context"></param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Deserialized response of type T.</returns>
        public async Task<T> DeleteAsync<T>(string path, object requestBody, object context = null, string domain = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(domain))
                domain = Configuration.BaseUrl;
            return await SendAsync<T>(method: HttpMethod.Delete, path, param: requestBody, context: context, domain: domain, cancellationToken);
        }
        /// <summary>
        /// Makes a Delete request with context and deserializes the response.
        /// Requests the deletion of a target resource.
        /// Uses the specified domain and path to construct the request URL.
        /// Adds parameters to the request body.
        /// Adds the context to the request.
        /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
        /// </summary>
        /// <typeparam name="T">Type of the expected response.</typeparam>
        /// <param name="domain">Target domain for the request. if empty the Configuration.BaseUrl will be used</param>
        /// <param name="path">Endpoint path for the request.</param>
        /// <param name="bodyContent">Request parameters.</param>
        /// <param name="context">Additional context for the request.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Deserialized response of type T.</returns>
        public async Task<HttpResponseMessage> DeleteAsync(string path, object bodyContent, object context=null, string domain = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(domain))
                domain = Configuration.BaseUrl;
            return await InternalSendAsync( method: HttpMethod.Delete,domain: domain, path: path,bodyContent: bodyContent,context: context, cancellationToken);
        }
        /// <summary>
        /// Makes a Connect request and deserializes the response.
        /// Establishes a network connection to a resource (often used with proxies).
        /// Uses the specified domain and path to construct the request URL and adds the request body.
        /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
        /// </summary>
        /// <typeparam name="T">Type of the expected response.</typeparam>
        /// <param name="domain">Target domain for the request. if empty the Configuration.BaseUrl will be used</param>
        /// <param name="path">Endpoint path for the request.</param>
        /// <param name="requestBody"> Request body content.</param>
        /// <param name="context"></param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Deserialized response of type T.</returns>
        public async Task<T> ConnectAsync<T>(string path, object requestBody, object context = null, string domain = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(domain))
                domain = Configuration.BaseUrl;
            return await SendAsync<T>(method: HttpMethod.Connect,path: path, param: requestBody, context: context, domain: domain,cancellationToken: cancellationToken);
        }
        /// <summary>
        /// Makes a Connect request with context and deserializes the response.
        /// Establishes a network connection to a resource (often used with proxies).
        /// Uses the specified domain and path to construct the request URL.
        /// Adds parameters to the request body.
        /// Adds the context to the request.
        /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
        /// </summary>
        /// <typeparam name="T">Type of the expected response.</typeparam>
        /// <param name="domain">Target domain for the request. if empty the Configuration.BaseUrl will be used</param>
        /// <param name="path">Endpoint path for the request.</param>
        /// <param name="bodyContent">Request parameters.</param>
        /// <param name="context">Additional context for the request.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Deserialized response of type T.</returns>
        public async Task<HttpResponseMessage> ConnectAsync(string path, object bodyContent, object context=null, string domain = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(domain))
                domain = Configuration.BaseUrl;
            return await InternalSendAsync(
                               method: HttpMethod.Connect,
                               path: path,
                               bodyContent: bodyContent,
                               context: context,
                               domain: domain,
                               cancellationToken: cancellationToken);
        }
        /// <summary>
        /// Makes a Head request and deserializes the response.
        /// Retrieves metadata (headers) of a resource without the body.
        /// Uses the specified domain and path to construct the request URL and adds the request body.
        /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
        /// </summary>
        /// <typeparam name="T">Type of the expected response.</typeparam>
        /// <param name="domain">Target domain for the request. if empty the Configuration.BaseUrl will be used</param>
        /// <param name="path">Endpoint path for the request.</param>
        /// <param name="requestBody"> Request body content.</param>
        /// <param name="context"></param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Deserialized response of type T.</returns>
        public async Task<T> HeadAsync<T>(string path, object requestBody, object context = null, string domain = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(domain))
                domain = Configuration.BaseUrl;
            return await SendAsync<T>(
                               method: HttpMethod.Head,
                               path: path, 
                               param: requestBody,
                               context: context,
                               domain: domain,
                               cancellationToken: cancellationToken);
        }
        /// <summary>
        /// Makes a Head request with context and deserializes the response.
        /// Retrieves metadata (headers) of a resource without the body.
        /// Uses the specified domain and path to construct the request URL.
        /// Adds parameters to the request body.
        /// Adds the context to the request.
        /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
        /// </summary>
        /// <typeparam name="T">Type of the expected response.</typeparam>
        /// <param name="domain">Target domain for the request. if empty the Configuration.BaseUrl will be used</param>
        /// <param name="path">Endpoint path for the request.</param>
        /// <param name="bodyContent">Request parameters.</param>
        /// <param name="context">Additional context for the request.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Deserialized response of type T.</returns>
        public async Task<HttpResponseMessage> HeadAsync(string path, object bodyContent, object context = null, string domain = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(domain))
                domain = Configuration.BaseUrl;
            return await InternalSendAsync(method: HttpMethod.Head, path: path, bodyContent: bodyContent, context: context, domain: domain, cancellationToken: cancellationToken);
        }
        /// <summary>
        /// Makes a Patch request and deserializes the response.
        /// Applies partial modifications to a resource.
        /// Uses the specified domain and path to construct the request URL and adds the request body.
        /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
        /// </summary>
        /// <typeparam name="T">Type of the expected response.</typeparam>
        /// <param name="domain">Target domain for the request. if empty the Configuration.BaseUrl will be used</param>
        /// <param name="path">Endpoint path for the request.</param>
        /// <param name="requestBody"> Request body content.</param>
        /// <param name="context"> context oblect</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Deserialized response of type T.</returns>
        public async Task<T> PatchAsync<T>(string path, object requestBody, object context=null, string domain = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(domain))
                domain = Configuration.BaseUrl;
            return await SendAsync<T>(method: HttpMethod.Patch, path: path, param: requestBody, context: context, domain: domain, cancellationToken: cancellationToken);
        }
        /// <summary>
        /// Makes a Patch request with context and deserializes the response.
        /// Applies partial modifications to a resource.
        /// Uses the specified domain and path to construct the request URL.
        /// Adds parameters to the request body.
        /// Adds the context to the request.
        /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
        /// </summary>
        /// <typeparam name="T">Type of the expected response.</typeparam>
        /// <param name="domain">Target domain for the request. if empty the Configuration.BaseUrl will be used</param>
        /// <param name="path">Endpoint path for the request.</param>
        /// <param name="bodyContent">Request parameters.</param>
        /// <param name="context">Additional context for the request.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Deserialized response of type T.</returns>
        public async Task<HttpResponseMessage> PatchAsync(string path, object bodyContent, object context=null, string domain = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(domain))
                domain = Configuration.BaseUrl;
            return await InternalSendAsync(method: HttpMethod.Patch, path: path, bodyContent: bodyContent, context: context, domain: domain, cancellationToken: cancellationToken);
        }
        /// <summary>
        /// Makes a Trace request and deserializes the response.
        /// Performs a loop-back test to inspect the request message as received by the server.
        /// Uses the specified domain and path to construct the request URL and adds the request body.
        /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
        /// </summary>
        /// <typeparam name="T">Type of the expected response.</typeparam>
        /// <param name="domain">Target domain for the request. if empty the Configuration.BaseUrl will be used</param>
        /// <param name="path">Endpoint path for the request.</param>
        /// <param name="requestBody"> Request body content.</param>
        /// <param name="context"></param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Deserialized response of type T.</returns>
        public async Task<T> TraceAsync<T>(string path, object requestBody, object context=null, string domain = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(domain))
                domain = Configuration.BaseUrl;
            return await SendAsync<T>(method: HttpMethod.Trace, path: path, param: requestBody, context: context, domain: domain, cancellationToken: cancellationToken);
        }
        /// <summary>
        /// Makes a Trace request and deserializes the response.
        /// Performs a loop-back test to inspect the request message as received by the server.
        /// Uses the specified domain and path to construct the request URL and adds the request body.
        /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
        /// </summary>
        /// <typeparam name="T">Type of the expected response.</typeparam>
        /// <param name="domain">Target domain for the request. if empty the Configuration.BaseUrl will be used</param>
        /// <param name="path">Endpoint path for the request.</param>
        /// <param name="requestBody"> Request body content.</param>
        /// <param name="context"></param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Deserialized response of type T.</returns>
        public async Task<HttpResponseMessage> TraceAsync(string path, object requestBody, object context = null, string domain = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(domain))
                domain = Configuration.BaseUrl;
            return await InternalSendAsync(method: HttpMethod.Trace, path: path, bodyContent: requestBody, context: context, domain: domain, cancellationToken: cancellationToken);
        }
        /// <summary>
        /// Makes a Options request and deserializes the response.
        /// Describes communication options for the target resource.
        /// Uses the specified domain and path to construct the request URL and adds the request body.
        /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
        /// </summary>
        /// <param name="domain">Target domain for the request. if empty the Configuration.BaseUrl will be used</param>
        /// <param name="path">Endpoint path for the request.</param>
        /// <param name="requestBody"> Request body content.</param>
        /// <param name="context"></param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Deserialized response of type T.</returns>
        public async Task<T> OptionsAsync<T>(string path, object requestBody, object context=null, string domain = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(domain))
                domain = Configuration.BaseUrl;
            return await SendAsync<T>(method: HttpMethod.Options, path: path, param: requestBody, context: context, domain: domain, cancellationToken: cancellationToken);
        }
        /// <summary>
        /// Makes a Options request and deserializes the response.
        /// return HttpResponseMessage
        /// </summary>
        /// <param name="path"></param>
        /// <param name="requestBody"></param>
        /// <param name="context"></param>
        /// <param name="domain"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<HttpResponseMessage> OptionsAsync(string path, object requestBody, object context = null, string domain = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(domain))
                domain = Configuration.BaseUrl;
            return await InternalSendAsync(method: HttpMethod.Options, path: path, bodyContent: requestBody, context: context, domain: domain, cancellationToken: cancellationToken);
        }
        /// <summary>
        /// Sends a request and retrieves the response content as a byte array.
        /// This method is useful for downloading binary data or files.
        /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
        /// </summary>
        /// <param name="method">HTTP method for the request.</param>
        /// <param name="domain">Target domain for the request. if empty the Configuration.BaseUrl will be used</param>
        /// <param name="path">Endpoint path for the request.</param>
        /// <param name="param">Request parameters.</param>
        /// <param name="context">Additional context for the request.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <param name="encodingType">Encoding type for string conversion. If null, returns raw bytes.</param>
        /// <returns>Response content as byte array.</returns>
        public async Task<byte[]> SendByteArrayAsync(HttpMethod method, string path, object param, object context, string domain = null, Encoding encodingType = default, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(domain))
                domain = Configuration.BaseUrl;
            var requestResult = await InternalSendAsync(method:method,domain: domain,path: path, bodyContent: param,context: context, cancellationToken: cancellationToken);

            var bin64 = encodingType == null
                ? await requestResult.Content.ReadAsByteArrayAsync(cancellationToken)
                : encodingType.GetBytes(await requestResult.Content.ReadAsStringAsync(cancellationToken));

            return bin64;
        }
        /// <summary>
        /// Sends a request and retrieves the response content as a stream.
        /// This method is useful for downloading large files or binary data.
        /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
        /// </summary>
        /// <param name="method">HTTP method for the request.</param>
        /// <param name="domain">Target domain for the request. if empty the Configuration.BaseUrl will be used</param>
        /// <param name="path">Endpoint path for the request.</param>
        /// <param name="param">Request parameters.</param>
        /// <param name="context">Additional context for the request.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Response content as stream.</returns>
        /// <exception cref="ApiException">Thrown when response indicates failure.</exception>
        public async Task<Stream> SendStreamAsync(HttpMethod method, string path, object param, object context, string domain = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(domain))
                domain = Configuration.BaseUrl;
            var requestResult = await InternalSendAsync(method:method,domain: domain,path: path, bodyContent: param, context: context,cancellationToken: cancellationToken);

            var content = await requestResult.Content.ReadAsStreamAsync(cancellationToken);
            if (!requestResult.IsSuccessStatusCode)
            {
                throw new ApiException(requestResult.StatusCode, requestResult.ReasonPhrase);
            }

            return content;
        }
        #endregion
        #region Internal Sending Methods
        /// <summary>
        /// Sends an HTTP request and returns the response message.
        /// checks for the HTTP method and constructs the request accordingly.
        /// check for Get/Head/Delete methods to add parameters to the Url string.
        /// check for other methods to add parameters to the request body.
        /// Handles different types of payloads :HttpContent, stream content, byte array content, multipart form data, and form data and json. 
        /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
        /// </summary>
        /// <param name="method">
        /// The HTTP method to use for the request (e.g., GET, POST, PUT, DELETE).
        /// </param>
        /// <param name="domain">
        /// The base URL of the API endpoint.
        /// </param>
        /// <param name="path">
        /// The specific path of the API endpoint to call.
        /// </param>
        /// <param name="bodyContent">
        /// The content to be sent in the request body.
        /// Handles different types of payloads :HttpContent, stream content, byte array content, multipart form data, and form data and json. 
        /// The method takes a generic payload input and intelligently wraps it into the appropriate HttpContent type based on the payload's data format.
        /// This ensures compatibility with HTTP request requirements.
        /// </param>
        /// <param name="context">
        /// Additional context for the request, such as headers or authentication information.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token to cancel the request if needed.
        /// </param>
        /// <returns></returns>
        private async Task<HttpResponseMessage> InternalSendAsync(HttpMethod method, string domain, string path, object bodyContent, object context, CancellationToken cancellationToken = default)
        {

            if (string.IsNullOrEmpty(domain))
                domain = Configuration.BaseUrl;

            if (!domain.EndsWith('/'))
                domain += "/";

            if (string.IsNullOrEmpty(path))
                path = "";

            var baseUri = new Uri(domain);           
            var fullUri = new Uri(baseUri, path.TrimStart('/'));

            var content = CreateRequestContent(bodyContent);
            HttpRequestMessage request = new(method, fullUri) { Content = content };


            ApplyRequestHeaders(request, context); // Apply headers
            var response= await HttpClient.SendAsync(request, cancellationToken);

            return response;

        }
        /// <summary>
        /// Sends an HTTP request using a custom request getter and returns the response message.
        /// Applies request headers and handles cancellation.
        /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
        /// </summary>
        /// <param name="requestGetter"></param>
        /// <param name="context"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task<HttpResponseMessage> InternalSendAsync(Func<HttpRequestMessage> requestGetter, object context, CancellationToken cancellationToken = default)
        {
            var request = requestGetter.Invoke();
            ApplyRequestHeaders(request, context); // Apply headers
            RaiseRequestSending(request, context);
            var response = await HttpClient.SendAsync(request, cancellationToken);
            RaiseResponseReceived(response, context);

            return response;
        }
        /// <summary>
        /// Sends an HTTP request using the provided request message and returns the response message.
        /// takes context as parameter to add it in the request headers.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task<HttpResponseMessage> InternalSendAsync(HttpRequestMessage request, object context = null, CancellationToken cancellationToken = default)
        {
            ApplyRequestHeaders(request, context); // Apply headers
            RaiseRequestSending(request, context);
            var response = await HttpClient.SendAsync(request, cancellationToken);
            RaiseResponseReceived(response, context);

            return response;
        }
        #endregion
        #region Response Configuration and Handling
        /// <summary>
        /// Virtual method to read and validate HTTP response content foreach request.
        /// user can override this method to add custom logic for reading and validating the response.
        /// Reads, validates, and logs HTTP response content.
        /// Throws ApiException for non-success status codes.
        /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
        /// </summary>
        /// <param name="response">HTTP response message.</param>
        /// <param name="context">Additional context for the request.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>Response content as string.</returns>
        protected virtual async Task<string> ConfigureResponseAsync(HttpResponseMessage response, object context, CancellationToken cancellationToken = default)
        {
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new ApiException(
                    response.StatusCode,
                    response.ReasonPhrase,
                    content
                );
            }

            return content;
        }
        /// <summary>
        /// Processes an HTTP response by checking status and deserializing content.
        /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
        /// </summary>
        /// <typeparam name="T">Type of the expected response.</typeparam>
        /// <param name="response">HTTP response message to process.</param>
        /// <param name="context">Additional context for the request.</param>
        /// <param name="cancellationToken"> Cancellation token for the operation.</param>
        /// <returns>Deserialized response of type T.</returns>
        /// <exception cref="ApiException">Thrown when response indicates failure.</exception>
        private async Task<T> ProcessHttpResponseAsync<T>(HttpResponseMessage response, object context, CancellationToken cancellationToken = default)
        {
            using (response)
            {
                var content = await ConfigureResponseAsync(response, context, cancellationToken);

                // Throw for all non-success status codes including 401 after retries
                if (!response.IsSuccessStatusCode)
                {
                    throw new ApiException(
                        response.StatusCode,
                        response.ReasonPhrase,
                        content
                    );
                }

                try
                {
                    return JsonSerializer.Deserialize<T>(content, JsonSerializerOptions);
                }
                catch (JsonException ex)
                {
                    Logger.LogWarning("Failed to deserialize response content: {Content}", content);
                    throw new ApiException($"Invalid JSON response: {ex.Message}") { Content = content };
                }
            }
        }
        /// <summary>
        /// Processes an HTTP response by checking status and deserializing content.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="response"></param>
        /// <param name="context"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="ApiException"></exception>
        private T ProcessHttpResponse<T>(HttpResponseMessage response, object context, CancellationToken cancellationToken = default)
        {
            using (response)
            {
                var content = ConfigureResponse(response, context, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    throw new ApiException(response.StatusCode, response.ReasonPhrase, content);
                }

                try
                {
                    return JsonSerializer.Deserialize<T>(content, JsonSerializerOptions);
                }
                catch (JsonException ex)
                {
                    throw new ApiException($"Invalid JSON response{ex.Message}") { Content = content };
                }
            }
        }
        /// <summary>
        /// Virtual method to read and validate HTTP response content foreach request.
        /// user can override this method to add custom logic for reading and validating the response.
        /// Reads, validates, and logs HTTP response content.
        /// </summary>
        /// <param name="response"></param>
        /// <param name="context"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="ApiException"></exception>
        protected virtual string ConfigureResponse(HttpResponseMessage response, object context, CancellationToken cancellationToken = default)
        {
            var content = response.Content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                throw new ApiException(
                    response.StatusCode,
                    response.ReasonPhrase,
                    content
                );
            }
            return content;
        }
        #endregion
        #region Request Configuration and Handling
        /// <summary>
        /// Event triggered when a token is evicted from the cache.
        /// This event can be used to perform custom actions when a token is removed from the cache.
        /// The event provides the cache key, token, and reason for eviction.
        /// User Should subscribe to this event to handle token eviction.
        /// </summary>
        /// <param name="cacheKey"></param>
        /// <param name="token"></param>
        /// <param name="reason"></param>
        protected virtual void OnTokenEvicted(string cacheKey, string token, EvictionReason reason)
        {
            TokenEvicted?.Invoke(cacheKey, token, reason);
        }
        /// <summary>
        /// Virtual method to customize the HTTP request message.
        /// This method can be overridden by derived classes to add custom headers or modify the request.
        /// It is called after the default headers have been applied.
        /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
        /// </summary>
        /// <param name="request">HTTP request message to customize.</param>
        /// <param name="context">Additional context for the request.</param>
        /// <example>
        /// <code> 
        /// public override void ConfigureRequest(HttpRequestMessage request, object context)
        /// {
        ///    request.Headers.Add("Custom-Header", "CustomValue");
        ///   // Add any other custom headers or modifications here
        /// }
        /// </code>
        /// </example>
        protected virtual void ConfigureRequest(HttpRequestMessage request, object context) { }
        /// <summary>
        /// Applies headers to the HTTP request foreach request.
        /// this method will be called before sending the request.
        /// This method adds Salesforce token, correlation ID, and application origin headers.
        /// It also logs the request and calls the virtual ConfigureRequest method for further customization.
        /// User can override this method to personalize the request headers.
        /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
        /// </summary>
        /// <param name="request">HTTP request message to configure.</param>
        /// <param name="context">Additional context for the request.</param>
        /// <remarks>
        /// This method:
        /// <list type="bullet">
        /// <item><description>Adds Salesforce bearer token if available</description></item>
        /// <item><description>Adds correlation ID header</description></item>
        /// <item><description>Adds application origin header</description></item>
        /// <item><description>Logs the request</description></item>
        /// <item><description>Calls any derived class customization</description></item>
        /// </list>
        /// </remarks>
        private void ApplyRequestHeaders(HttpRequestMessage request, object context)
        {
            
            //call virtual HandleRequestMessageCore here to allow developpers to injects their custom Context Object,
            //to inject their headers/auth if they want 
            ConfigureRequest(request, context);
        }
        #endregion
        #region Utility Methods
        /// <summary>
        /// Creates an HTTP content object based on the provided payload.
        /// Handles different types of payloads :HttpContent, stream content, byte array content, multipart form data, and form data and json. 
        /// The method takes a generic payload input and intelligently wraps it into the appropriate HttpContent type based on the payload's data format.
        /// This ensures compatibility with HTTP request requirements.
        /// </summary>
        /// <param name="payload"></param>
        /// <returns></returns>
        private HttpContent CreateRequestContent(object payload)
        {

            if (payload is null) return null; // 👈 Handle form data if null
            // 1. Handle pre-constructed HttpContent
            if (payload is HttpContent httpContent) // 👈 Handle form data if HttpContent
                return httpContent;

            if (payload is StringContent stringContent) // 👈 Handle form data if StringContent
                return stringContent;

            switch (payload)
            {
                case byte[] byteArray: // 👈 Handle form data if byte array
                    return new ByteArrayContent(byteArray);

                case Stream stream:  // 👈 Handle form data if Stream
                    return new StreamContent(stream);

                case StreamContent streamContent: // 👈 Handle form data if StreamContent
                    return streamContent;

                case ByteArrayContent byteArrayContent: // 👈 Handle form data if ByteArrayContent
                    return byteArrayContent;

                case MultipartFormDataContent multipartFormDataContent: // 👈 Handle form data if MultipartFormDataContent
                    return multipartFormDataContent;

                case IDictionary<string, string> formData:
                    {
                        return new FormUrlEncodedContent(formData)
                        {
                            Headers = { ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded") }
                        };
                    }
                case IEnumerable<KeyValuePair<string, string>> formData:
                    {
                        return new FormUrlEncodedContent(formData)
                        {
                            Headers = { ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded") }
                        };
                    }
                case string str when !IsJson(str):
                    return new StringContent(str, Encoding.UTF8, "text/plain");
                case string str when IsJson(str):
                    return new StringContent(str, Encoding.UTF8, "application/json");
                default:
                    {
                        Logger.LogInformation("Processing Unkown type payload of data as json");
                        string json = JsonSerializer.Serialize(payload, JsonSerializerOptions);
                        return new StringContent(json, Encoding.UTF8, "application/json");
                    }
            }
        }
        /// <summary>
        /// return true if the string input is json
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private static bool IsJson(string input)
        {
            input = input.Trim();
            return input.StartsWith('{') && input.EndsWith('}')
                || input.StartsWith('[') && input.EndsWith(']');
        }
        /// <summary>
        /// Deserializes a JSON string to the specified type.
        /// using the JsonSerializerOptions of this Client.
        /// you can set them from property.
        /// refer to documentation<see href = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1198/API-APIM-Client-Architecture-Documentation"> here.</see >
        /// </summary>
        /// <typeparam name="T">Target type for deserialization.</typeparam>
        /// <param name="content">JSON string to deserialize.</param>
        /// <returns>Deserialized object of type T.</returns>
        public T DeserializeString<T>(string content)
        {
            return JsonSerializer.Deserialize<T>(content, JsonSerializerOptions);
        }
        /// <summary>
        /// Serializes an object to a JSON string.
        /// using the JsonSerializerOptions of this Client.
        /// you can set them from property.
        /// </summary>
        /// <param name="Object"></param>
        /// <returns></returns>
        public string SerializeObject(object Object)
        {
            return JsonSerializer.Serialize(Object, JsonSerializerOptions);
        }
        /// <summary>
        /// Validates a JWT token by checking its structure and expiration time.
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public bool ValidateToken(string token)
        {
            return IdentityModelTokensJwtValidator.Validate(token);
        }
        #endregion
       
    }
}
