using Adecco.WW.Packages.WebApi.Services.Client.Exceptions;
using Adecco.WW.Packages.WebApi.Services.Client.Internal.Api;
using Adecco.WW.Packages.WebApi.Services.Client.Internal.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Polly;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods for configuring HTTP client settings in the service collection.
    /// This class provides methods to add HTTP client configurations to the service collection.
    /// It allows you to register named configurations for HTTP clients, which can be used to set up the configuration for the HTTP client.
    /// The configurations can be registered using a generic type parameter for the client and configuration.
    /// </summary>
    public static class ApiConfigurationServiceExtensions
    {
        /// <summary>
        /// Gets the HTTP client configuration for a specified name.
        /// </summary>
        /// <typeparam name="TConfiguration"></typeparam>
        /// <param name="builder"></param>
        /// <param name="Name"></param>
        /// <returns></returns>
        public static TConfiguration GetConfiguration<TConfiguration>(
                 this WebApplicationBuilder builder,
                 string Name)
           where TConfiguration : ApiConfiguration, new()
        {
            // create a temporary service provider from the builder's services
            using var serviceProvider = builder.Services.BuildServiceProvider();
            // Get the ApiConfigurationFactory from the service provider
            var factory = serviceProvider.GetRequiredService<ApiConfigurationFactory>();
            // use the factory to get the configuration for the specified name
           return factory.GetConfiguration<TConfiguration>(Name);
       
        }
        /// <summary>
        /// Gets the HTTP client configuration for a specified name.
        /// </summary>
        /// <typeparam name="TConfiguration"></typeparam>
        /// <param name="serviceProvider"></param>
        /// <param name="Name"></param>
        /// <returns></returns>
        public static TConfiguration GetConfiguration<TConfiguration>(
           this IServiceProvider serviceProvider,
           string Name
           )
           where TConfiguration : ApiConfiguration, new()
        {

            // Get the ApiConfigurationFactory from the service provider
            var factory = serviceProvider.GetRequiredService<ApiConfigurationFactory>();
            // Use the factory to get the configuration for the specified name
            return factory.GetConfiguration<TConfiguration>(Name);

        }
        /// <summary>
        /// Adds an HTTP client configuration to the service collection.
        /// Uses a generic type parameter for the client and configuration.
        /// Using this method, you can register a named configuration for an HTTP client (Key used to register the client).
        /// This method is useful for setting up the configuration for the HTTP client.
        /// </summary>
        /// <para>⚠️ Throws <see cref="ArgumentException"/> if a non-interface type is used.</para>
        /// </typeparam>
        /// <typeparam name="TConfiguration"></typeparam>
        /// <param name="services"></param>
        /// <param name="config"></param>
        /// <param name="Name"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static IServiceCollection AddHttpConfiguration<TConfiguration>(
            this IServiceCollection services,
            string Name,
            TConfiguration config
            )
            where TConfiguration : ApiConfiguration, new()
        {
            // Validate required properties
            if (string.IsNullOrEmpty(config.BaseUrl))
            {
                throw new ConfigurationException($"BaseUrl is required in {typeof(TConfiguration).Name} configuration.", Name)
                { HelpLink = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1214/AppSetting.json" };
               
            }
            //  Add the Configuration to the HttpConfigurationFactoryOptions Dictionary
            services.Configure<ApiConfigurationFactoryOptions>(options =>
            {
                if (options.Configurations.ContainsKey(Name))
                {
                    throw new ConfigurationException($"Configuration with key '{Name}' is already registered.", config) 
                    { HelpLink = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1214/AppSetting.json" };
                
                }
                options.Configurations.Add(Name, config);
            });
            // Register HttpConfigurationFactory
            // with dependency on HttpConfigurationFactoryOptions.
            services.TryAddSingleton<ApiConfigurationFactory>(sp =>
                new ApiConfigurationFactory(
                    sp.GetRequiredService<IOptions<ApiConfigurationFactoryOptions>>()
                )
            );
            return services;
        }
        /// <summary>
        /// Adds an HTTP client configuration to the service collection directly from the configuration file Section in appsetting.json .
        /// Uses a generic type parameter for the client and configuration.
        /// USing this method, you can register a named configuration for an HTTP client (Key is TClient.Name).
        /// </summary>
        /// <typeparam name="TConfiguration"></typeparam>
        /// <param name="services"></param>
        /// <param name="Name">
        /// The Key used to identify this Http configuration.
        /// </param>
        /// <param name="configuration"></param>
        /// <param name="configurationSectionName"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static IServiceCollection AddHttpConfiguration< TConfiguration>(
          this IServiceCollection services,
          string Name,
           IConfiguration configuration,
          string configurationSectionName)
          where TConfiguration : ApiConfiguration, new()
        {
           
            // Bind configuration from the specified section
            var config = new TConfiguration();
            try
            {
                configuration.GetSection(configurationSectionName).Bind(config);
            }
            catch (Exception ex)
            {
                throw new ConfigurationException($"Failed to bind configuration section '{configurationSectionName}': {ex.Message}.", configurationSectionName)
                { HelpLink = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1214/AppSetting.json" };
            }

            // Validate essential configuration values
            if (string.IsNullOrEmpty(config.BaseUrl))
            {
                throw new ConfigurationException($"BaseUrl is required in {typeof(TConfiguration).Name} configuration.", configurationSectionName)
                { HelpLink = "https://dev.azure.com/adeccoitww/apim/_wiki/wikis/apim.wiki/1214/AppSetting.json" };
            }
            // Register new configuration using new config object using TClient.Name
            return services.AddHttpConfiguration<TConfiguration>(Name,config);
        }


    }
}
