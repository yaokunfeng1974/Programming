using Adecco.WW.Packages.WebApi.Services.Client.Internal.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Adecco.WW.Packages.WebApi.Services.Client.Apim
{
    /// <summary>
    /// Apim Client Configuration class
    /// </summary>
    public sealed class ApimConfiguration : ApiConfiguration
    {
        /// <summary>
        /// set or get the Api Key for an Apim Client
        /// </summary>
        [Required]
        public string ApimKey { get; set; }=null;
        /// <summary>
        /// set or get the Api Name for an Apim Client
        /// </summary>
        public string ApimName { get; set; }=string.Empty;
        /// <summary>
        /// set or get the Apim Version for an Apim Client, Default is ApimVersion.None.
        /// </summary>
        public ApimVersion ApimVersion { get; set; } = ApimVersion.None;
        /// <summary>
        /// Default Apim Client Configuration
        /// </summary>
        public static ApimConfiguration Default => new ApimConfiguration();

    }
}
