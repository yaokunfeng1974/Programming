using Adecco.WW.Packages.WebApi.Middlewares.Correlation.Contracts;
using Adecco.WW.Packages.WebApi.Services.Client.Exceptions;
using Adecco.WW.Packages.WebApi.Services.Client.Internal.Api;
using Adecco.WW.Packages.WebApi.Services.Client.Internal.HttpClientExtensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Polly;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Adecco.WW.Packages.WebApi.Middlewares.Correlation;
using Adecco.WW.Packages.WebApi.Services.Client.Internal.Configuration;
using Adecco.WW.Packages.WebApi.Services.Client.Internal.ApiResiliencePipeline;
using Adecco.WW.Packages.WebApi.Services.Client.Internal.ApiSocketsHttpHandler;
using Adecco.WW.Packages.WebApi.Services.Client.Internal.ApiConfigurationValidator;
using static System.Formats.Asn1.AsnWriter;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods for configuring API client services in the service collection.
    /// Allows for the registration of named API client configurations and resilience policies.
    /// Allows for the configuration of resilience policies, connection management, and other settings.
    /// </summary>
    public static class ApiClientServiceExtensions
    {
        #region Builder Extensions
        /// <summary>
        /// Binds an API client from the specified section in the appsettings.json file.
        /// This method allows for the configuration of resilience policies, connection management, and other settings directly from the configuration file.
        /// You can resolve the client using Net.8 Keyed Service passing the key name.
        /// You can resolve the client using the IServiceProvider.GetKeyedService&lt;TClient&gt;(Name) method.
        /// User can personalize Error handling and fallback responses of the HttpClient.
        /// </summary>
        /// <typeparam name="TClient"></typeparam>
        /// <typeparam name="TConfiguration"></typeparam>
        /// <param name="builder">
        /// The WebApplicationBuilder instance to which the API client will be added.
        /// </param>
        /// <param name="sectionName">
        /// The name of the configuration section in the appsettings.json file.
        /// </param>
        /// <param name="lifetime">
        /// The lifetime of the service (e.g., Singleton, Scoped, Transient).
        /// </param>
        /// <param name="customErrorPredicate">
        /// A custom error predicate to handle specific HTTP errors.
        /// </param>
        /// <param name="fallbackResponseFactory"></param>
        /// <returns></returns>
        /// <exception cref="ClientRegistrationException">
        /// Thrown when the section name is null or empty.
        /// </exception>
        public static IServiceCollection BindApiClient<TClient, TConfiguration>(
                  this WebApplicationBuilder builder,
                  string sectionName,
                  ServiceLifetime lifetime = ServiceLifetime.Scoped,
                  Func<HttpResponseMessage, bool> customErrorPredicate = null,
                  Func<Context, Task<HttpResponseMessage>> fallbackResponseFactory = null
                  )
            where TClient : ApiClient<TConfiguration>
            where TConfiguration : ApiConfiguration, new()
        {
           
           return builder.Services.ConfigureApiClient<TClient, TConfiguration>(sectionName, builder.Configuration, sectionName, lifetime, customErrorPredicate, fallbackResponseFactory);

        }
        #endregion


        /// <summary>
        /// Configures an API client with a specified lifetime.
        /// Adds an API client to the service collection as a Keyed Service with resilience policies and configuration.
        /// Adds an API client configuration to the HttpConfigurationFactory.
        /// This method binds the configuration from the AppSetting.json file in your project.
        /// You can resolve the client using Net.8 Keyed Service passing the key name.
        /// You can resolve the client using the IServiceProvider.GetKeyedService&lt;TClient&gt;(Name) method.
        /// User can personalize Error handling and fallback responses of the HttpClient.
        /// </summary>
        /// <typeparam name="TClient">
        /// The **Class** type used to Implement this client.
        /// </typeparam>
        /// <typeparam name="TConfiguration">
        /// The **class** type used to bind the configuration settings.
        /// </typeparam>
        /// <param name="services">
        /// The service collection to which the API client will be added.
        /// </param>
        /// <param name="Name">
        /// The key used to identify the API client configuration and HttpClient and the Instance.
        /// </param>
        /// <param name="configuration">
        /// The configuration object that contains the settings for the API client.
        /// </param>
        /// <param name="configurationSectionName">
        /// The name of the configuration section in the appsettings.json file.
        /// </param>
        /// <param name="lifetime"></param>
        /// <param name="customErrorPredicate">
        /// A custom error predicate to handle specific HTTP errors.
        /// </param>
        /// <param name="fallbackResponseFactory">
        /// A fallback response factory to handle circuit breaker scenarios.
        /// </param>
        /// <returns>
        /// The updated service collection with the API client configuration added.
        /// </returns>
        public static IServiceCollection ConfigureApiClient<TClient, TConfiguration>(
         this IServiceCollection services,
         string Name,
         IConfiguration configuration,
         string configurationSectionName,
         ServiceLifetime lifetime = ServiceLifetime.Scoped,
         Func<HttpResponseMessage, bool> customErrorPredicate = null,
         Func<Context, Task<HttpResponseMessage>> fallbackResponseFactory = null)
         where TClient : ApiClient<TConfiguration>
         where TConfiguration : ApiConfiguration, new()
        {
            // Validate the Name parameter
            if (string.IsNullOrEmpty(Name))
                throw new ClientRegistrationException($"Error while trying to create the Instance of the Client Of Type: {typeof(TClient).Name}, the Name for the Client used in Registration is null or empty.")
                { HelpLink = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1205/Custom-API-client-Tutorials" };
            // Add configuration using the section name
            services.AddApiConfiguration<TConfiguration>(Name,
                configuration,
                configurationSectionName);
            // Add client with desired lifetime (e.g., Scoped)
            return services.AddApiClient<TClient, TConfiguration>(Name, lifetime, customErrorPredicate, fallbackResponseFactory);// Default is ServiceLifetime.Scoped
        }
        /// <summary>
        /// Configures an API client with a specified lifetime.
        /// Adds an API client to the service collection as a Keyed Service with resilience policies and configuration.
        /// Adds an API client configuration to the HttpConfigurationFactory.
        /// This method binds the configuration from the specified object.
        /// You can resolve the client using Net.8 Keyed Service passing the key name.
        /// You can resolve the client using the IServiceProvider.GetKeyedService&lt;TClient&gt;(Name) method.
        /// User can personalize Error handling and fallback responses of the HttpClient.
        /// </summary>
        /// <typeparam name="TClient">
        /// The Class Implementation of the client.
        /// </typeparam>
        /// <typeparam name="TConfiguration"></typeparam>
        /// The **class** type used to bind the configuration settings.
        /// <param name="services"></param>
        /// The service collection to which the API client will be added.
        /// <param name="Name"></param>
        /// The key used to identify the API client configuration and HttpClient and the Instance.
        /// <param name="configuration"></param>
        /// The configuration object that contains the settings for the API client.
        /// <param name="lifetime"></param>
        /// The lifetime of the service (e.g., Singleton, Scoped, Transient).
        /// <param name="customErrorPredicate"></param>
        /// A custom error predicate to handle specific HTTP errors.
        /// <param name="fallbackResponseFactory">
        /// A fallback response factory to handle circuit breaker scenarios.
        /// </param>
        /// <returns>
        /// The updated service collection with the API client configuration added.
        /// </returns>
        public static IServiceCollection ConfigureApiClient<TClient, TConfiguration>(
         this IServiceCollection services,
         string Name,
         TConfiguration configuration,
         ServiceLifetime lifetime = ServiceLifetime.Scoped,
         Func<HttpResponseMessage, bool> customErrorPredicate = null,
         Func<Context, Task<HttpResponseMessage>> fallbackResponseFactory = null)
         where TClient : ApiClient<TConfiguration>
         where TConfiguration : ApiConfiguration, new()
        {
            // Validate the Name parameter
            if (string.IsNullOrEmpty(Name))
                throw new ClientRegistrationException($"Error while trying to create the Instance of the Client Of Type: {typeof(TClient).Name}, the Name for the Client used in Registration is null or empty.")
                { HelpLink = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1205/Custom-API-client-Tutorials" };
            // Register configuration using the object
            services.AddApiConfiguration<TConfiguration>(Name,
                configuration);
            // Add client with desired lifetime (e.g., Scoped)
            return services.AddApiClient<TClient, TConfiguration>(Name, lifetime, customErrorPredicate, fallbackResponseFactory);// Default is ServiceLifetime.Scoped
        }
        /// <summary>
        /// Adds an API client configuration to the service collection.
        /// This method binds the configuration from the specified object.
        /// Allows for the configuration of resilience policies, connection management, and other settings.
        /// Use ConfigureApiClient() for Seamless and Simple Integration.
        /// </summary>
        /// <typeparam name="TConfiguration">
        /// The **class** type used to bind the configuration settings.
        /// </typeparam>
        /// <param name="services">
        /// The service collection to which the configuration will be added.
        /// </param>
        /// <param name="Name">
        /// The key used to identify the API client configuration.
        /// </param>
        /// <param name="config">
        /// The configuration object that contains the settings for the API client.
        /// </param>
        /// <returns>
        /// The updated service collection with the API client configuration added.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when essential configuration values are missing.
        /// </exception>
        private static IServiceCollection AddApiConfiguration<TConfiguration>(
         this IServiceCollection services,
         string Name,
         TConfiguration config)
         where TConfiguration : ApiConfiguration, new()
        {

            return services.AddHttpConfiguration<TConfiguration>(Name, config);
        }
        /// <summary>
        /// Adds an API client configuration to the service collection.
        /// This method binds the configuration from the specified section in the appsettings.json file.
        /// Allows for the configuration of resilience policies, connection management, and other settings.
        /// Use ConfigureApiClient() for Seamless and Simple Integration.
        /// </summary>
        /// <typeparam name="TConfiguration"></typeparam>
        /// <param name="services"></param>
        /// <param name="Name">
        /// The key used to identify the API client configuration.
        /// </param>
        /// <param name="configuration"></param>
        /// <param name="configurationSectionName"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        private static IServiceCollection AddApiConfiguration<TConfiguration>(
           this IServiceCollection services,
            string Name,
            IConfiguration configuration,
           string configurationSectionName)
           where TConfiguration : ApiConfiguration, new()

        {
            // Register new configuration from object config using KeyName
            return services.AddHttpConfiguration<TConfiguration>(Name, configuration, configurationSectionName);
        }
        /// <summary>
        /// Adds an API client to the service collection with resilience policies and configuration.
        /// Using this method, you can register a named configuration and a named HttpClient for an API client (Key is TClient.Name).
        /// Allows for the configuration of resilience policies, connection management, and other settings.
        /// Use ConfigureApiClient() for Seamless and Simple Integration.
        /// you can also specify the lifetime of the service (e.g., Singleton, Scoped, Transient).
        /// </summary>
        /// <typeparam name="TClient">
        /// The **Class** type used to Implement this client. 
        /// Must be an ApiClient class.
        /// </typeparam>
        /// <typeparam name="TConfiguration"></typeparam>
        /// <param name="services">
        /// The service collection to which the API client will be added.
        /// </param>
        /// <param name="Name">
        /// The key used to identify the API client configuration and HttpClient and the client.
        /// </param>
        /// <param name="lifetime">
        /// The lifetime of the service (e.g., Singleton, Scoped, Transient).
        /// </param>
        /// <param name="customErrorPredicate"> 
        /// A custom error predicate to handle specific HTTP errors.
        /// </param>
        /// <param name="fallbackResponseFactory">
        /// A fallback response factory to handle circuit breaker scenarios. 
        /// </param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        private static IServiceCollection AddApiClient<TClient, TConfiguration>(
            this IServiceCollection services,
            string Name,
            ServiceLifetime lifetime = ServiceLifetime.Scoped,
            Func<HttpResponseMessage, bool> customErrorPredicate = null,
            Func<Context, Task<HttpResponseMessage>> fallbackResponseFactory = null)
            where TClient : ApiClient<TConfiguration>
            where TConfiguration : ApiConfiguration, new()
        {
            // Try to Add Dependencies of API client for the user
            services.AddMemoryCache();
            services.TryAddTransient<ICorrelationIdContextProvider, CorrelationIdContextProvider>(); 
            services.TryAddTransient<IAppOriginContextProvider, AppOriginContextProvider>();
            //1. ApiHttpclientFactroy is used to create the handler for the HttpClient
            var handlerFactory = ActivatorUtilities.CreateFactory(
                  typeof(ApiHttpClientHandler),
                  new[] { typeof(string) } // Constructor parameter types
              );
            //2. ApiClientFactory is used to create the ApiClient instance
            var factory = ActivatorUtilities.CreateFactory(typeof(TClient),
                                                           [typeof(string)]// Expects a string parameter (the Name)
                                                           );
            // 1. Configure named HttpClient So the HttpClientFactory can resolve it Using The key Name
            //    - we Assume Previously registered a named configuration also using the same key (Refer to AddApiClientConfiguration Method) 
            //    - to resolve the named HttpClient inside ApiClient Constructor : we will use IHttpClientFactory.CreateClient(key)
            //    - HttpClient and Configuration share same Key and they are coupled together, each client has a unique configuration
            var httpClientBuilder = services.AddHttpClient<TClient>(
                name: Name,
                configureClient: (sp, client) =>
                {
                    // Resolve the configuration Factory 
                    var configFactory = sp.GetRequiredService<ApiConfigurationFactory>();
                    // Get the configuration using the key
                    var currentConfig = configFactory.GetConfiguration<TConfiguration>(Name)
                        ?? throw new ConfigurationException($"No configuration registered for client {Name}. " +
                                                            "Verify AddApiConfiguration() or ConfigureApiClient() was called during service registration",
                                                            Name);
                    // Validate configuration first
                    ConfigurationValidator.ValidateConfiguration<TConfiguration>(currentConfig);
                    // Set the base address for the HttpClient
                    client.BaseAddress = new Uri(currentConfig.BaseUrl);
                    // Set the timeout for the HttpClient
                    client.Timeout = currentConfig.Timeout;
                    // Set MaxResponseContentBufferSize for the HttpClient
                    client.MaxResponseContentBufferSize = currentConfig.MaxResponseContentBufferSize;
                    // Set the default request headers for the HttpClient
                    foreach (var header in currentConfig.DefaultRequestHeaders)
                    {
                        // Skip null or empty Headers values 
                        if (string.IsNullOrEmpty(header.Value)) continue;
                        // This will Force Replacement of Headers if they already exist, This Default Behaivor allow Customize Headers
                        if (client.DefaultRequestHeaders.Contains(header.Key)) client.DefaultRequestHeaders.Remove(header.Key);
                        // Add the header to the HttpClient from the configuration 
                        client.DefaultRequestHeaders.Add(header.Key, header.Value);
                    }
                    // Set the default request version for the HttpClient
                    client.DefaultRequestVersion = currentConfig.DefaultRequestVersion;
                    // Set the default request version policy for the HttpClient
                    client.DefaultVersionPolicy = currentConfig.DefaultVersionPolicy;

                });
         
            // 2. Configure the HttpClient with a Primary Socket Http Handler handler
            httpClientBuilder.ConfigurePrimaryHttpMessageHandler(sp => ApiSocketsHttpHandlerBuilder.CreateHandler<TConfiguration>(Name, sp));
            // 3. Configure the HttpClient with resilience policies once at registration this will be called
            // Resilience policies are created once per named client during this and reused for all requests.
            httpClientBuilder.AddPolicyHandler((serviceProvider, request) =>
            {


                // Create the resilience pipeline
                var pipeline = ApiResiliencePipelineBuilder.CreateResiliencePipeline<TClient,TConfiguration>(
                    Name,
                    serviceProvider, // Pass to pipeline creation
                    customErrorPredicate,
                    fallbackResponseFactory
                );

                return pipeline;
            });
            // 4. Add the custom handler to the HttpClient pipeline
            // even when developers use HttpClient directly: the handler will override the Microsoft default SendAsync method
            // this handler will be used to inject the correlationId and appOrigin headers and bearer token
            // Use Microsoft Generic Factory to manage and resolve OverrideHttpClientHandler with a client name parameter

            // Add the custom handler to the HttpClient pipeline
            httpClientBuilder.AddHttpMessageHandler(sp =>
            {
                var handler = handlerFactory.Invoke(sp, [Name]) as ApiHttpClientHandler;

                handler.OnRequestSending += (sender, args) =>
                {
                    var client = sp.GetRequiredKeyedService<TClient>(Name);
                    // Raise the event in the client
                    client.RaiseRequestSending(sender, args);
                };
                handler.OnResponseReceived += (sender, args) =>
                {
                    var client = sp.GetRequiredKeyedService<TClient>(Name);
                    // Raise the event in the client
                    client.RaiseResponseReceived(sender, args);
                };


                return handler;
            });
           // 6. Register the client and wire events to the SAME handler instance
            switch (lifetime)
            {
                case ServiceLifetime.Singleton:
                    services.AddKeyedSingleton<TClient>(Name, (sp, key) =>
                    {
                      
                       return factory.Invoke(sp, [Name]) as TClient;

                    });
                    break;
                case ServiceLifetime.Transient:
                    services.AddKeyedTransient<TClient>(Name, (sp, key) =>
                    {
                        return factory.Invoke(sp, [Name]) as TClient;

                    });
                    break;
                default:
                    services.AddKeyedScoped<TClient>(Name, (sp, key) =>
                    {
                        return factory.Invoke(sp, [Name]) as TClient;

                    });
                    break;
            }
            // 7. Return the updated service collection for method chaining
            return services;
        }
       

       
       
    }
}
