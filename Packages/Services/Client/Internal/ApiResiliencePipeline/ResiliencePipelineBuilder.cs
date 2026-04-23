using Adecco.WW.Packages.WebApi.Exceptions;
using Adecco.WW.Packages.WebApi.Middlewares.Correlation.Contracts;
using Adecco.WW.Packages.WebApi.Services.Client.Exceptions;
using Adecco.WW.Packages.WebApi.Services.Client.Internal.Api;
using Adecco.WW.Packages.WebApi.Services.Client.Internal.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Bulkhead;
using Polly.CircuitBreaker;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Adecco.WW.Packages.WebApi.Services.Client.Internal.ApiResiliencePipeline
{
    /// <summary>
    /// ResiliencePipelineBuilder is a static class that provides methods to create resilience pipelines for API clients.
    /// </summary>
    public static class ApiResiliencePipelineBuilder
    {
        /// <summary>
        /// Creates a resilience pipeline for the API client.
        /// This pipeline includes retry policies, circuit breakers, bulkhead isolation, and fallback strategies.
        /// Allows for the customization of error handling and fallback responses.
        /// Allows for the configuration of resilience policies to handle transient failures.
        /// </summary>
        /// <typeparam name="TClient">
        /// The **Class** type used to Implement this client.
        /// </typeparam>
        /// <typeparam name="TConfiguration">
        /// The **class** type used to bind the configuration settings.
        /// </typeparam>
        /// <param name="sp">
        /// The service provider to resolve dependencies.
        /// </param>
        /// <param name="customErrorPredicate">
        /// A custom error predicate to handle specific HTTP errors.
        /// </param>
        /// <param name="fallbackResponseFactory">
        /// A fallback response factory to handle circuit breaker scenarios.
        /// </param>
        /// <param name="Name">
        /// The key used to identify the API client configuration.
        /// </param>
        /// <returns></returns>
        /// <exception cref="ConfigurationException"></exception>
        public static IAsyncPolicy<HttpResponseMessage> CreateResiliencePipeline<TClient, TConfiguration>(
            string Name,
            IServiceProvider sp,
            Func<HttpResponseMessage, bool> customErrorPredicate,
            Func<Context, Task<HttpResponseMessage>> fallbackResponseFactory)
             where TClient : ApiClient<TConfiguration>
            where TConfiguration : ApiConfiguration, new()
        {
            // Resolve the configuration Factory 
            var configFactory = sp.GetRequiredService<ApiConfigurationFactory>();
            // Get the configuration using the key
            var config = configFactory.GetConfiguration<TConfiguration>(Name)
                ?? throw new ConfigurationException("No configuration registered for client {Key}. ", Name);
            // get logger to write logs inside the policies
            var logger = sp.GetRequiredService<ILogger<TConfiguration>>();

            // 1. Token-aware retry policy (handles 401 first)
            var tokenRefreshPolicy = Policy
                .HandleResult<HttpResponseMessage>(r => r.StatusCode == HttpStatusCode.Unauthorized)
                .Or<ConfigurationException>() // Handle missing OAuth2 config
                .Or<ApiException>() // Handle token refresh errors
                .RetryAsync(1, async (outcome, retryCount, context) =>
                {


                    // 3. Resolve dependencies
                    var apiClient = sp.GetKeyedService<TClient>(Name);
                    var logger = sp.GetRequiredService<ILogger<TClient>>();

                    try
                    {
                        logger.LogInformation("Refresh Policy 401 Unauthorized : Refreshing token for {Client}", Name);
                        logger.LogDebug("Refresh Policy 401 Unauthorized : Old token: {Token}", outcome.Result.RequestMessage.Headers.Authorization);

                        // 4. force Refresh token with useCachedToken: false
                        if (string.IsNullOrEmpty(apiClient.Configuration.CredentialsPath) || apiClient.Configuration.CredentialsDictionary == null || apiClient.Configuration.CredentialsDictionary.Count == 0)
                        {
                            logger.LogInformation("Refresh Policy 401 Unauthorized : Credentials Not found for {Client}", Name);
                            return;
                        }else
                        {
                            logger.LogInformation("Refresh Policy 401 Unauthorized : Credentials found for {Client}", Name);
                        }
                        // call OAuth2 with useCachedToken = true and forceTokenValidation=true make sure First 401 failed Request make an actual API call for a new token after validating the cached one
                        // others will be forced to use cached one automaticlly, for security reason no request will be lost ever using this flow
                        await apiClient.OAuth2( forceTokenValidation: true);

                        logger.LogInformation("Refresh Policy 401 Unauthorized: Token refreshed successfully");

                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Refresh Policy 401 Unauthorized :Token refresh failed for {ClientName}", Name);
                        throw;
                    }
                });
            // 2. Define error handling strategy
            var errorPredicate = Policy<HttpResponseMessage>
                                                                                .Handle<HttpRequestException>() // Network failures
                                                                                .OrResult(r =>
                                                                                    (customErrorPredicate?.Invoke(r) ?? false) || // Custom user errors Handeling , users can define their own error codes retry 
                                                                                 // (int)r.StatusCode >= 500 || // All 5xx errors not retries , this is a good practice to avoid retrying server errors
                                                                                    r.StatusCode == HttpStatusCode.RequestTimeout || // 408
                                                                                    r.StatusCode == HttpStatusCode.TooManyRequests // 429
                                                                                                                                   // r.StatusCode == HttpStatusCode.Unauthorized // 401 Unauthorized  are managed by tokenRefreshPolicy
                                                                                );
            // 3. Configure retry policy with Retry-After Header check and logging
            var retryPolicy = errorPredicate
                .WaitAndRetryAsync(
                    retryCount: config.FaultedRequestRetryCount,
                    // client-side default backoff (exponential)
                    // The delay between retries increases exponentially based on the retryAttempt number.
                    // Formula: delay = (2^retryAttempt) * config.FaultedRequestRetryDelay
                    // This means the first retry will be 2 seconds, the second 4 seconds, and so on.
                    sleepDurationProvider: (retryAttempt, outcome, context) =>
                    {
                        // Check for server-specified delay FIRST
                        if (outcome?.Result?.StatusCode == HttpStatusCode.TooManyRequests)
                        {
                            var retryAfter = outcome.Result.Headers.RetryAfter;
                            if (retryAfter != null)
                            {
                                TimeSpan serverDelay = TimeSpan.Zero;

                                if (retryAfter.Delta.HasValue)
                                {
                                    serverDelay = retryAfter.Delta.Value;
                                }
                                else if (retryAfter.Date.HasValue)
                                {
                                    var retryAfterDate = retryAfter.Date.Value;
                                    if (retryAfterDate.Offset != TimeSpan.Zero)
                                    {
                                        retryAfterDate = retryAfterDate.ToUniversalTime();
                                    }
                                    serverDelay = retryAfterDate - DateTimeOffset.UtcNow;
                                }

                                if (serverDelay > TimeSpan.Zero)
                                {
                                    // Apply server delay with cap
                                    var actualDelay = serverDelay < config.RetryAfterHeaderMaxInSeconds
                                        ? serverDelay
                                        : config.RetryAfterHeaderMaxInSeconds;

                                    logger.LogInformation("Using server-specified Retry-After delay: {DelayMs}ms",
                                        actualDelay.TotalMilliseconds);
                                    return actualDelay;
                                }
                            }
                        }

                        // Fall back to exponential backoff only if no valid server delay
                        return TimeSpan.FromSeconds(Math.Pow(2, retryAttempt) * config.FaultedRequestRetryDelay);
                    },
                   onRetryAsync: async (outcome, timespan, attempt, context) =>
                   {
                       logger.LogWarning(
                                        "Retry {Attempt} for {PolicyKey} – Delay: {DelayMilliseconds}ms – Status: {StatusCode}",
                                        attempt,
                                        context.PolicyKey,
                                        timespan.TotalMilliseconds,
                                        outcome.Result?.StatusCode
                                    );
                       await Task.CompletedTask;
                   }
                );
            // 4. Circuit breaker with state monitoring
            var circuitBreaker = errorPredicate
                .CircuitBreakerAsync(
                    config.FaultedRequestRetryCount,
                    TimeSpan.FromSeconds(config.CircuitBreakerDurationSeconds),
                    onBreak: (outcome, state, duration, context) =>
                    {
                        logger.LogError(
                                        "Circuit broken! State: {State} – Duration: {DurationSeconds}s – Status: {StatusCode}",
                                        state,
                                        duration.TotalSeconds,
                                        outcome.Result?.StatusCode
                                    );
                    },
                    onReset: context => logger.LogInformation("Circuit reset!"),
                    onHalfOpen: () => logger.LogWarning("Circuit half-open"));
            // 5. Safe bulkhead configuration
            var bulkhead = Policy.BulkheadAsync<HttpResponseMessage>(
                maxParallelization: Math.Max(config.ConcurrentConnectionLimit ?? 10, 1),
                maxQueuingActions: Math.Max(config.MaxQueuingActions ?? 100, 0)
            ).WithPolicyKey("Bulkhead");
            // Get the correlation provider to use in the fallback response
            var correlationProvider = sp.GetRequiredService<ICorrelationIdContextProvider>();
            // 6. Fallback strategy
            var fallback = Policy<HttpResponseMessage>
                .Handle<BrokenCircuitException>()
                .Or<BulkheadRejectedException>()
                .FallbackAsync(
                                fallbackAction: async (delegateContext, ct) =>
                                {
                                    // Get the correlation ID from the provider
                                    var correlation = correlationProvider.Get();
                                    // Create fallback response
                                    var response = fallbackResponseFactory != null
                                        ? await fallbackResponseFactory(delegateContext)
                                        : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                                        {
                                            Content = new StringContent($"Service unavailable for {Name}. Correlation : {correlation}")
                                        };

                                    return response;
                                },
                                onFallbackAsync: (outcome, context) =>
                                {
                                    // Log fallback execution
                                    logger.LogWarning("[{Name}] Fallback triggered: {ExceptionType}",
                                        Name,
                                        outcome.Exception.GetType().Name);

                                    return Task.CompletedTask;
                                }
                            );
            // 7. Compose policies with monitoring
            // in this order : 401 tokenRefreshPolicy → (Retry → Circuit Breaker → Bulkhead → Fallback)
            return Policy.WrapAsync(
                                    tokenRefreshPolicy,  // First layer: Handle 401s
                                   Policy.WrapAsync(            // Second layer: handle other errors
                                                    retryPolicy,
                                                    circuitBreaker,
                                                    bulkhead,
                                                    fallback)       // Last: Final fallback
                                );

        }
    }
}
