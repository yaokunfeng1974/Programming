using Adecco.WW.Packages.WebApi.Services.Client.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Polly;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using ApimClient = Adecco.WW.Packages.WebApi.Services.Client.Apim.ApimClient;

namespace Adecco.WW.Packages.WebApi.Services.Client.Apim
{
    /// <summary>
    /// extension methods for adding APIM client services to the service collection.
    /// Directly used in Startup.cs because they are implemented as part of Microsoft.Extensions.DependencyInjection namespace.
    /// This allows for easy integration and configuration of APIM client services in ASP.NET Core applications.
    /// </summary>
    public static class ApimClientServiceExtensions
    {
        #region Builder Extensions
        /// <summary>
        /// Binds an APIM client from the specified section in the appsettings.json file.
        /// This method allows for the configuration of resilience policies, connection management, and other settings directly from the configuration file.
        /// You can resolve the client using Net.8 Keyed Service passing the key name.
        /// You can resolve the client using the IServiceProvider.GetKeyedService&lt;TClient&gt;(Name) method.
        /// User can personalize Error handling and fallback responses of the HttpClient.
        /// </summary>
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
        /// <param name="fallbackResponseFactory">A factory method to create a fallback response in case of an error.</param>
        /// <returns></returns>
        /// <exception cref="ClientRegistrationException">
        /// Thrown when the section name is null or empty.
        /// </exception>
        public static IServiceCollection BindApimClient(
                  this WebApplicationBuilder builder,
                  string sectionName,
                  ServiceLifetime lifetime = ServiceLifetime.Scoped,
                  Func<HttpResponseMessage, bool> customErrorPredicate = null,
                  Func<Context, Task<HttpResponseMessage>> fallbackResponseFactory = null
                  )   
        {

            return builder.Services.ConfigureApimClient(sectionName, builder.Configuration, sectionName, lifetime, customErrorPredicate, fallbackResponseFactory);

        }
        #endregion
        /// <summary>
        /// Configures an APIM client and its configuration.
        /// Using the key name.
        /// Adds an APIM client to Internal Factory with resilience policies and configuration.
        /// Adds an APIM client configuration to Internal Factory.
        /// This method binds the configuration from the specified section in the appsettings.json file.
        /// Allows for the configuration of resilience policies, connection management, and other settings.
        /// Allows for the configuration of resilience policies to handle transient failures.
        /// Allows for the customization of error handling and fallback responses.
        /// </summary>
        /// <param name="services">
        /// The service collection to which the client is added.
        /// </param>
        /// <param name="Name">
        /// The key used to identify the client configuration and client and http client.
        /// </param>
        /// <param name="configuration">
        /// The configuration object of your project.
        /// </param>
        /// <param name="configurationSectionName">
        /// The name of the configuration section in the appsettings.json file.
        /// </param>
        /// <param name="lifetime">
        /// The lifetime of the service (e.g., Scoped, Singleton, Transient).
        /// </param>
        /// <param name="customErrorPredicate">
        /// A custom predicate to determine if a response is considered an error.
        /// </param>
        /// <param name="fallbackResponseFactory">
        /// A factory method to create a fallback response in case of an error.
        /// </param>
        /// <returns>
        /// </returns>
        public static IServiceCollection ConfigureApimClient(
         this IServiceCollection services,
         string Name,
         IConfiguration configuration,
         string configurationSectionName,
         ServiceLifetime lifetime = ServiceLifetime.Scoped,
         Func<HttpResponseMessage, bool> customErrorPredicate = null,
         Func<Context, Task<HttpResponseMessage>> fallbackResponseFactory = null)

        {
            // configure An ApimClient and his configuration
            return services.ConfigureApiClient<ApimClient, ApimConfiguration>(
                 Name,
                 configuration,
                 configurationSectionName,
                 lifetime,
                 customErrorPredicate,
                 fallbackResponseFactory);

        }

        /// <summary>
        /// Configures an APIM client and its configuration.
        /// Using the key name and a configuration Object.
        /// registers an APIM client and its configuration Using Internal the Factories.
        /// Use The Api Factory object to create the client.
        /// This method allows you to configure the APIM client with custom settings and resilience policies.
        /// Allows for the customization of error handling and fallback responses.
        /// This method is useful for setting up the APIM client with specific configurations and behaviors.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="Name"></param>
        /// <param name="configuration"></param>
        /// <param name="lifetime"></param>
        /// <param name="customErrorPredicate"></param>
        /// <param name="fallbackResponseFactory"></param>
        /// <returns></returns>
        public static IServiceCollection ConfigureApimClient(
         this IServiceCollection services,
         string Name,
         ApimConfiguration configuration,
         ServiceLifetime lifetime = ServiceLifetime.Scoped,
         Func<HttpResponseMessage, bool> customErrorPredicate = null,
         Func<Context, Task<HttpResponseMessage>> fallbackResponseFactory = null)

        {


            // configure An ApimClient and his configuration 
            return services.ConfigureApiClient<ApimClient, ApimConfiguration>(
                 Name,
                 configuration,
                 lifetime,
                 customErrorPredicate,
                 fallbackResponseFactory);

        }


    }
}