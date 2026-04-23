using Adecco.WW.Packages.WebApi.Services.Client.Internal.Api;
using System;
using System.Collections.Generic;


namespace Adecco.WW.Packages.WebApi.Services.Client.Internal.Configuration
{
    /// <summary>
    /// Class to hold configurations for different clients.
    /// </summary>
    public class ApiConfigurationFactoryOptions
    {
        /// <summary>
        /// Dictionary to hold all configurations for different Clients.
        /// </summary>
        public Dictionary<string, ApiConfiguration> Configurations { get; set; } =
            new Dictionary<string, ApiConfiguration>(StringComparer.OrdinalIgnoreCase);
    }
}
