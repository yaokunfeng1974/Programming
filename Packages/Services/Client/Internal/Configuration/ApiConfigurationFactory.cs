using Adecco.WW.Packages.WebApi.Services.Client.Exceptions;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace Adecco.WW.Packages.WebApi.Services.Client.Internal.Configuration
{
    
    /// <summary>
    /// Http Co
    /// </summary>
    public class ApiConfigurationFactory
    {
        private readonly Dictionary<string, ApiConfiguration> _configurations;
 
        /// <summary>
        /// Initializes a new instance of the <see cref="ApiConfigurationFactory"/> class.
        /// </summary>
        public ApiConfigurationFactory(
             IOptions<ApiConfigurationFactoryOptions> options)
        {
    
            _configurations = options.Value.Configurations;

        }
        /// <summary>
        /// Gets the configuration for the specified name.
        /// </summary>
        /// <typeparam name="TConfiguration"></typeparam>
        /// <param name="name"></param>
        /// <returns></returns>
        public TConfiguration GetConfiguration<TConfiguration>(string name) where TConfiguration : ApiConfiguration
        {
            if (_configurations.TryGetValue(name, out var Config))
            {
               
                return Config as TConfiguration;


            }
            throw new ConfigurationException($"No Configuration is registered for key: {name}");

        }
        /// <summary>
        /// adds a new configuration with the specified name.
        /// </summary>
        /// <typeparam name="TConfiguration"></typeparam>
        /// <param name="name"></param>
        /// <param name="config"></param>
        /// <exception cref="ClientRegistrationException"></exception>
        public void AddConfiguration<TConfiguration>(string name, TConfiguration config) where TConfiguration : ApiConfiguration
        {
            if (_configurations.ContainsKey(name))
            {
                throw new ClientRegistrationException($"A Configuration was Registered Already Registred with this name : {name}");
            }
            _configurations.Add(name, config);
        }
    }
}