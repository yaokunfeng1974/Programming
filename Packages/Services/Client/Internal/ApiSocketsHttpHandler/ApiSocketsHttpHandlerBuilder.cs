using Adecco.WW.Packages.WebApi.Services.Client.Exceptions;
using Adecco.WW.Packages.WebApi.Services.Client.Internal.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Adecco.WW.Packages.WebApi.Services.Client.Internal.ApiSocketsHttpHandler
{
    /// <summary>
    /// This class is responsible for creating and configuring the SocketsHttpHandler for the API client.
    /// </summary>
    public static class ApiSocketsHttpHandlerBuilder
    {
        /// <summary>
        /// Creates a custom HTTP handler for the API client.
        /// This handler is used to configure connection management, proxy settings, and other HTTP settings.
        /// Allows for the customization of the HTTP client behavior.
        /// </summary>
        /// <typeparam name="TConfiguration"></typeparam>
        /// <param name="sp">
        /// The service provider to resolve dependencies.
        /// </param>
        /// <param name="Name">
        /// The key used to identify the API client configuration.
        /// </param>
        /// <returns>
        /// A configured SocketsHttpHandler instance.
        /// </returns>
        /// <exception cref="ConfigurationException"></exception>
        /// <exception cref="ClientRegistrationException"></exception>
        public static SocketsHttpHandler CreateHandler<TConfiguration>(
        string Name,
        IServiceProvider sp
        )
        where TConfiguration : ApiConfiguration, new()
        {

            // Resolve the configuration Factory 
            var configFactory = sp.GetRequiredService<ApiConfigurationFactory>();
            // Get the configuration using the key Name
            var config = configFactory.GetConfiguration<TConfiguration>(Name)
                ?? throw new ConfigurationException($"No configuration registered for client {Name}. " +
                                                    "Verify ConfigureApiClient() was called during service registration",
                                                    Name);
            // get the logger to write logs from handler socket
            var logger = sp.GetRequiredService<ILogger<TConfiguration>>();

            // Validate proxy configuration
            if (config.UseProxy && string.IsNullOrEmpty(config.ProxyUrl))
            {
                logger.LogError("Proxy enabled but no URL configured");
                throw new ClientRegistrationException("Proxy URL required when UseProxy=true");
            }
            // return a new SocketsHttpHandler with the specified configuration to the client
            var handler = new SocketsHttpHandler
            {
                MaxConnectionsPerServer = config.MaxConnectionsPerServer,
                PooledConnectionLifetime = config.ForceRefreshConnection
                    ? config.RefreshConnectionDelay
                    : config.PooledConnectionLifetime,
                UseProxy = config.UseProxy,
                Proxy = config.UseProxy ? new WebProxy(config.ProxyUrl) : null,
                UseCookies = config.UseCookies,
                AllowAutoRedirect = config.AllowAutoRedirect,
                MaxAutomaticRedirections = config.MaxAutomaticRedirections,
                AutomaticDecompression = config.AutomaticDecompression
            };
            // Load client certificate if configured
            if (config.UseClientCertificate)
            {
                try
                {
                    var cert = new X509Certificate2(
                        config.ClientCertificatePath,
                        config.ClientCertificatePassword
                    );
                    handler.SslOptions.ClientCertificates = new X509Certificate2Collection(cert);
                    logger.LogInformation("Loaded client certificate from {Path}", config.ClientCertificatePath);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to load client certificate from {Path}", config.ClientCertificatePath);
                    throw new ConfigurationException($"Client certificate error: {ex.Message}", ex, config);
                }
            }
            return handler;
        }
    }
}
