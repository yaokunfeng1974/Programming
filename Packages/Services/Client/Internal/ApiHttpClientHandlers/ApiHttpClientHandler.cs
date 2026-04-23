﻿using Adecco.WW.Packages.WebApi.Middlewares.Correlation.Contracts;
using Adecco.WW.Packages.WebApi.Services.Client.Apim;
using Adecco.WW.Packages.WebApi.Services.Client.Exceptions;
using Adecco.WW.Packages.WebApi.Services.Client.Internal.Api;
using Adecco.WW.Packages.WebApi.Services.Client.Internal.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Adecco.WW.Packages.WebApi.Services.Client.Internal.HttpClientExtensions
{
    /// <summary>
    /// This class is used to override the default HttpClient SendAsync behavior to add headers and log calls.
    /// It allows you to add custom headers or modify the request before it is sent.
    /// this will be used inside ApiClient class to replace the default HttpClient SendAsync method.
    /// </summary>
    public sealed class ApiHttpClientHandler : DelegatingHandler
    {
        /// <summary>
        /// Api Client using this instance
        /// </summary>
        internal readonly Object _lock = new Object();

        /// <summary>
        /// The name of the client.
        /// </summary>
        internal readonly string ClientName;

        /// <summary>
        /// The cache memory.
        /// </summary>
        private readonly IMemoryCache _memoryCache;

        /// <summary>
        /// The correlation ID context provider.
        /// </summary>
        private readonly ICorrelationIdContextProvider _correlationIdContextProvider;

        /// <summary>
        /// The app origin context provider.
        /// </summary>
        private readonly IAppOriginContextProvider _appOriginContextProvider;

        /// <summary>
        /// The logger for this class.
        /// </summary>
        private readonly ILogger<ApiHttpClientHandler> _logger;

        /// <summary>
        /// The API configuration.
        /// </summary>
        public readonly ApiConfiguration apiConfiguration;

        /// <summary>
        /// Delegate for handling request sending events.
        /// </summary>
        public RequestDelegate OnRequestSending { get; set; }

        /// <summary>
        /// Delegate for handling response received events.
        /// </summary>
        public ResponseDelegate OnResponseReceived { get; set; }

        /// <summary>
        /// Constructor for OverrideHttpClient.
        /// </summary>
        /// <param name="clientName"></param>
        /// <param name="correlationIdContextProvider"></param>
        /// <param name="httpConfigurationFactory"></param>
        /// <param name="appOriginContextProvider"></param>
        /// <param name="logger"></param>
        /// <param name="cache"></param>
        public ApiHttpClientHandler(string clientName,
            ICorrelationIdContextProvider correlationIdContextProvider,
            ApiConfigurationFactory httpConfigurationFactory,
            IAppOriginContextProvider appOriginContextProvider,
            ILogger<ApiHttpClientHandler> logger,
            IMemoryCache cache)
        {
            _correlationIdContextProvider = correlationIdContextProvider;
            _appOriginContextProvider = appOriginContextProvider;
            _logger = logger;
            _memoryCache = cache;

            if (string.IsNullOrEmpty(clientName))
                throw new ClientRegistrationException($"Error while trying to create the Instance of the OverrideHttpClientHandler Of Type: {GetType().Name}, the Name for the Client used in Registration is null or empty.")
                { HelpLink = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1205/Custom-API-client-Tutorials" };

            ClientName = clientName;
            apiConfiguration = httpConfigurationFactory.GetConfiguration<ApiConfiguration>(ClientName);
        }

        /// <summary>
        /// Overrides the SendAsync method of Microsoft HttpClient.SendAsync() to add custom headers and log the request.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;
            HttpResponseMessage response = null;

            // Process request modifications
            InjectAuthToken(request);
            UpdateRetryCountHeader(request);
            AddCustomHeaders(request);

            // Process Request Logging Async
            await ProcessRequestLoggingAsync(request);

            // Trigger RequestSending event
            OnRequestSending?.Invoke(request, null);

            // Send the request
            response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            // Trigger ResponseReceived event
            OnResponseReceived?.Invoke(response, null);

            // Process Response Logging Async
            await ProcessResponseLoggingAsync(response, startTime);

            return response;
        }

        /// <summary>
        /// Injects authentication token from cache into request headers
        /// </summary>
        /// <param name="request"></param>
        private void InjectAuthToken(HttpRequestMessage request)
        {
            lock (_lock)
            {
                _logger.LogInformation("Getting token from cache for client: {ClientName}", ClientName);

                string cacheTokenKey = _memoryCache.Get<string>($"{ClientName}_AuthToken_Key");
                _logger.LogInformation("Cache token key: {CacheTokenKey}", string.IsNullOrEmpty(cacheTokenKey) ? "Null or Empty" : "Not Empty and Not Null");

                string tokenValue = cacheTokenKey != null
                    ? _memoryCache.Get<string>(cacheTokenKey)
                    : null;
                _logger.LogInformation("Token value: {TokenValue}", string.IsNullOrEmpty(tokenValue) ? "Null or Empty" : "Not Empty and Not Null");

                if (!string.IsNullOrEmpty(tokenValue))
                {
                    _logger.LogInformation("Injecting token into request headers for client: {ClientName}", ClientName);
                    request.Headers.Authorization = new AuthenticationHeaderValue(apiConfiguration.CredentialsTokenType ?? "Bearer", tokenValue);
                }
                else
                {
                    _logger.LogWarning("No token found in cache for client: {ClientName}. Request will be sent without authorization header.", ClientName);
                }
            }
        }

        /// <summary>
        /// Updates the retry count header for the request
        /// </summary>
        /// <param name="request"></param>
        private void UpdateRetryCountHeader(HttpRequestMessage request)
        {
            if (!request.Headers.Contains("X-Request-Retry-Count"))
            {
                request.Headers.Add("X-Request-Retry-Count", "0");
            }
            else
            {
                var retryCount = int.Parse(request.Headers.GetValues("X-Request-Retry-Count").FirstOrDefault() ?? "0");
                request.Headers.Remove("X-Request-Retry-Count");
                request.Headers.Add("X-Request-Retry-Count", (retryCount + 1).ToString());
            }
        }

        /// <summary>
        /// Adds custom headers to the request
        /// </summary>
        /// <param name="request"></param>
        private void AddCustomHeaders(HttpRequestMessage request)
        {
            // Add client name header
            if (!string.IsNullOrEmpty(ClientName) && !request.Headers.Contains("X-Client-Name"))
            {
                request.Headers.Add("X-Client-Name", ClientName);
            }

            // Add correlation ID header
            var correlationId = _correlationIdContextProvider.Get();
            if (!string.IsNullOrEmpty(correlationId) && !request.Headers.Contains(_correlationIdContextProvider.GetOptions().Header))
            {
                request.Headers.Add(
                    _correlationIdContextProvider.GetOptions().Header,
                    correlationId
                );
            }

            // Add app origin header
            var appOrigin = _appOriginContextProvider.Get();
            if (!string.IsNullOrEmpty(appOrigin) && !request.Headers.Contains(_appOriginContextProvider.GetOptions().Header))
            {
                request.Headers.Add(
                    _appOriginContextProvider.GetOptions().Header,
                    appOrigin
                );
            }

            _logger.LogInformation("Request Headers successfully added (X-Client-Name, Authorization, correlationId, appOrigin) By the WebApi Client Handler for client: {ClientName}", ClientName);
        }

        /// <summary>
        /// Logs request details including headers and body
        /// </summary>
        /// <param name="request"></param>
        private async Task ProcessRequestLoggingAsync(HttpRequestMessage request)
        {
            LogRequestHeaders(request);

            if (request.Content != null && apiConfiguration.EnableLoggingRequestContent)
            {
                request.Content = await CloneAndLogContentAsync(request.Content, "Request");
            }
        }

        /// <summary>
        /// Processes and logs the response
        /// </summary>
        /// <param name="response"></param>
        /// <param name="startTime"></param>
        private async Task ProcessResponseLoggingAsync(HttpResponseMessage response, DateTime startTime)
        {
            var endTime = DateTime.UtcNow;
            var elapsedMs = (endTime - startTime).TotalMilliseconds;

            LogResponseHeaders(response, elapsedMs);

            if (response.Content != null && apiConfiguration.EnableLoggingResponseContent)
            {
                response.Content = await CloneAndLogContentAsync(response.Content, "Response");
            }
        }
        /// <summary>
        /// Clones the HttpContent and logs it.
        /// </summary>
        /// <param name="originalContent"></param>
        /// <param name="prefix"></param>
        /// <returns></returns>
        private async Task<HttpContent> CloneAndLogContentAsync(HttpContent originalContent, string prefix)
        {
            if (originalContent == null)
                return null;

            var (clonedContent, body) = await CloneContentAsync(originalContent);
            LogContentBody(originalContent, body, prefix);

            return clonedContent;
        }

        /// <summary>
        /// Logs the content body based on media type
        /// </summary>
        /// <param name="originalContent"></param>
        /// <param name="body"></param>
        /// <param name="prefix"></param>
        private void LogContentBody(HttpContent originalContent, string body, string prefix)
        {
            var mediaType = originalContent.Headers.ContentType?.MediaType?.ToLower();

            if (mediaType != null)
            {
                if (mediaType.Contains("json") ||
                    mediaType.Contains("xml") ||
                    mediaType.Contains("text") ||
                    mediaType.Contains("x-www-form-urlencoded") ||
                    mediaType.Contains("octetstream"))
                {
                    string redactedContent;
                    if (apiConfiguration.EnableRedaction)
                         redactedContent = RedactSensitiveData(body);
                    else redactedContent = body;
                    _logger.LogInformation($"Api Client {prefix} Body: {{RedactedContent}}", redactedContent);
                }
                else
                {
                    _logger.LogInformation($"Api Client {prefix} Body: [Binary content - {originalContent.Headers.ContentType}]");
                }
            }
            else
            {
                _logger.LogInformation($"Api Client {prefix} Body: [Unknown content type]");
            }
        }

        /// <summary>
        /// Clones the HttpContent and returns it along with its body as a string.
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        private async Task<(HttpContent, string)> CloneContentAsync(HttpContent content)
        {
            var buffer = await content.ReadAsByteArrayAsync();
            var clone = new ByteArrayContent(buffer);

            foreach (var header in content.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            try
            {
                var body = Encoding.Default.GetString(buffer);
                return (clone, body);
            }
            catch
            {
                return (clone, $"[Binary data - {buffer.Length} bytes]");
            }
        }

        /// <summary>
        /// Redacts sensitive data from content
        /// </summary>
        private string RedactSensitiveData(string content)
        {
            if (string.IsNullOrEmpty(content))
                return content ?? string.Empty;

            // Truncate if too long
            if (content.Length > apiConfiguration.MaxBodyLogLength)
            {
                content = content.Substring(0, apiConfiguration.MaxBodyLogLength) + "...(truncated)";
            }


            // Check if it's JSON first
            bool mightBeJson = content.TrimStart().StartsWith('{') || content.TrimStart().StartsWith('[');

            if (mightBeJson)
            {
                try
                {
                    using var document = JsonDocument.Parse(content);
                    _logger.LogDebug("Content successfully parsed as JSON, using JSON redaction");
                    return RedactJsonSensitiveData(document);
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogDebug("Content looked like JSON but failed to parse: {Message}. Falling back to text redaction.", jsonEx.Message);
                    return RedactTextSensitiveData(content);
                }
            }

            _logger.LogDebug("Content doesn't appear to be JSON, using text redaction");
            return RedactTextSensitiveData(content);
        }

        /// <summary>
        /// Redacts sensitive data from JSON content
        /// </summary>
        private string RedactJsonSensitiveData(JsonDocument document)
        {
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream);

            RedactJsonElement(writer, document.RootElement);
            writer.Flush();

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        /// <summary>
        /// Recursively redacts JSON elements
        /// </summary>
        private void RedactJsonElement(Utf8JsonWriter writer, JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    writer.WriteStartObject();
                    foreach (var property in element.EnumerateObject())
                    {
                        writer.WritePropertyName(property.Name);

                        if (apiConfiguration.SensitiveFieldNames.Contains(property.Name))
                        {
                            writer.WriteStringValue(apiConfiguration.RedactionText);
                        }
                        else
                        {
                            RedactJsonElement(writer, property.Value);
                        }
                    }
                    writer.WriteEndObject();
                    break;

                case JsonValueKind.Array:
                    writer.WriteStartArray();
                    foreach (var item in element.EnumerateArray())
                    {
                        RedactJsonElement(writer, item);
                    }
                    writer.WriteEndArray();
                    break;

                case JsonValueKind.String:
                    writer.WriteStringValue(element.GetString());
                    break;

                case JsonValueKind.Number:
                    writer.WriteNumberValue(element.GetDecimal());
                    break;

                case JsonValueKind.True:
                    writer.WriteBooleanValue(true);
                    break;

                case JsonValueKind.False:
                    writer.WriteBooleanValue(false);
                    break;

                case JsonValueKind.Null:
                    writer.WriteNullValue();
                    break;

                default:
                    writer.WriteNullValue();
                    break;
            }
        }

        /// <summary>
        /// Redacts sensitive data from text content
        /// </summary>
        private string RedactTextSensitiveData(string content)
        {
            if (content.Contains('=') && content.Contains('&'))
            {
                _logger.LogDebug("Detected form URL encoded content");
                var result = RedactFormUrlEncodedData(content);
                return result;
            }

            _logger.LogDebug("Applying pattern-based redaction");
            return ApplyPatternRedaction(content);
        }

        /// <summary>
        /// Applies pattern-based redaction to text content
        /// </summary>
        private string ApplyPatternRedaction(string content)
        {
            _logger.LogDebug("ApplyPatternRedaction - Starting with content length: {Length}", content.Length);

            foreach (var field in apiConfiguration.SensitiveFieldNames)
            {
                
                // Pattern 1: XML tags - <field>value</field>
                var xmlPattern = $@"(<{field}>)(.*?)(</{field}>)";
                content = System.Text.RegularExpressions.Regex.Replace(
                    content,
                    xmlPattern,
                    match =>
                    {
                        return $"{match.Groups[1].Value}{apiConfiguration.RedactionText}{match.Groups[3].Value}";
                    },
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                // Pattern 2: field: value (with colon)
                var colonPattern = $@"({field}\s*:\s*)([^\r\n]*)";
                content = System.Text.RegularExpressions.Regex.Replace(
                    content,
                    colonPattern,
                    match =>
                    {
                        return $"{match.Groups[1].Value}{apiConfiguration.RedactionText}";
                    },
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);

                // Pattern 3: field=value (with equals, no quotes)
                var equalsPattern = $@"({field}\s*=\s*)([^\r\n\s""']+)";
                content = System.Text.RegularExpressions.Regex.Replace(
                    content,
                    equalsPattern,
                    match =>
                    {
                        return $"{match.Groups[1].Value}{apiConfiguration.RedactionText}";
                    },
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);

                // Pattern 4: field="value" (double quotes)
                var doubleQuotePattern = $@"({field}\s*=\s*"")([^""]*)";
                content = System.Text.RegularExpressions.Regex.Replace(
                    content,
                    doubleQuotePattern,
                    match =>
                    {
                        return $"{match.Groups[1].Value}{apiConfiguration.RedactionText}";
                    },
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                // Pattern 5: field='value' (single quotes)
                var singleQuotePattern = $@"({field}\s*=\s*')([^']*)";
                content = System.Text.RegularExpressions.Regex.Replace(
                    content,
                    singleQuotePattern,
                    match =>
                    {
                        return $"{match.Groups[1].Value}{apiConfiguration.RedactionText}";
                    },
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            _logger.LogDebug("ApplyPatternRedaction - Completed");
            return content;
        }

        /// <summary>
        /// Redacts sensitive data from form URL encoded content
        /// </summary>
        private string RedactFormUrlEncodedData(string content)
        {
            try
            {
                var keyValuePairs = content.Split('&')
                    .Select(pair =>
                    {
                        var parts = pair.Split('=');
                        if (parts.Length == 2)
                        {
                            var key = System.Net.WebUtility.UrlDecode(parts[0]);
                            var value = parts[1];

                            if (apiConfiguration.SensitiveFieldNames.Contains(key, StringComparer.OrdinalIgnoreCase))
                            {
                                return $"{System.Net.WebUtility.UrlEncode(key)}={apiConfiguration.RedactionText}";
                            }
                            else
                            {
                                return pair;
                            }
                        }
                        return pair;
                    })
                    .ToArray();

                return string.Join("&", keyValuePairs);
            }
            catch (Exception)
            {
                return ApplyFormUrlPatternRedaction(content);
            }
        }

        /// <summary>
        /// Applies pattern redaction for form URL encoded content as fallback
        /// </summary>
        private string ApplyFormUrlPatternRedaction(string content)
        {
            foreach (var field in apiConfiguration.SensitiveFieldNames)
            {
                var patterns = new[]
                {
                    $@"({field}=)[^&]+",
                    $@"({System.Net.WebUtility.UrlEncode(field)}=)[^&]+"
                };

                foreach (var pattern in patterns)
                {
                    content = System.Text.RegularExpressions.Regex.Replace(
                        content, pattern,
                        $"$1{apiConfiguration.RedactionText}",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                }
            }
            return content;
        }

        /// <summary>
        /// Logs the outgoing HTTP request
        /// </summary>
        private void LogRequestHeaders(HttpRequestMessage request)
        {
            try
            {
                var logMessage = new StringBuilder();
                logMessage.AppendLine($"Api Client Request: {request.Method} {request.RequestUri}");

                if (request.Headers.Any())
                {
                    logMessage.AppendLine("Api Client Request Headers:");
                    foreach (var header in request.Headers)
                    {
                        string value;
                        if (apiConfiguration.EnableRedaction && apiConfiguration.SensitiveHeaders.Contains(header.Key))
                        {
                            value = apiConfiguration.RedactionText;
                        }
                        else
                        {
                            value = string.Join("; ", header.Value);
                        }
                        logMessage.AppendLine($"  {header.Key}: {value}");
                    }
                }

                if (request.Content?.Headers != null)
                {
                    logMessage.AppendLine("Api Client Request Content Headers:");
                    foreach (var header in request.Content.Headers)
                    {
                        string value;
                        if (apiConfiguration.EnableRedaction && apiConfiguration.SensitiveHeaders.Contains(header.Key))
                        {
                            value = apiConfiguration.RedactionText;
                        }
                        else
                        {
                            value = string.Join("; ", header.Value);
                        }
                        logMessage.AppendLine($"  {header.Key}: {value}");
                    }
                }

                _logger.LogInformation("Outgoing HTTP request Headers for client {ClientName}:\n{RequestLog}",
                    ClientName, logMessage.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to log outgoing HTTP request Headers for client: {ClientName}", ClientName);
            }
        }

        /// <summary>
        /// Logs the incoming HTTP response
        /// </summary>
        private void LogResponseHeaders(HttpResponseMessage response, double elapsedMs)
        {
            try
            {
                var logMessage = new StringBuilder();

                logMessage.AppendLine($"Api Client Response: {(int)response.StatusCode} {response.StatusCode} in {elapsedMs}ms");

                if (response.Headers.Any())
                {
                    logMessage.AppendLine("Api Client Response Headers:");
                    foreach (var header in response.Headers)
                    {
                        string value;
                        if (apiConfiguration.EnableRedaction && apiConfiguration.SensitiveHeaders.Contains(header.Key))
                        {
                            value = apiConfiguration.RedactionText;
                        }
                        else
                        {
                            value = string.Join("; ", header.Value);
                        }
                        logMessage.AppendLine($"  {header.Key}: {value}");

                    }
                }

                if (response.Content?.Headers != null && response.Content.Headers.Any())
                {
                    logMessage.AppendLine("Api Client Response Content Headers:");
                    foreach (var header in response.Content.Headers)
                    {
                        string value;
                        if (apiConfiguration.EnableRedaction && apiConfiguration.SensitiveHeaders.Contains(header.Key))
                        {
                            value = apiConfiguration.RedactionText;
                        }
                        else
                        {
                            value = string.Join("; ", header.Value);
                        }
                        logMessage.AppendLine($"  {header.Key}: {value}");

                    }
                }

                _logger.LogInformation("Incoming HTTP response Headers for client {ClientName} : \n {logMessage} ",
                    ClientName, logMessage.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to log incoming HTTP response Headers for client: {ClientName}", ClientName);
            }
        }
    }
}