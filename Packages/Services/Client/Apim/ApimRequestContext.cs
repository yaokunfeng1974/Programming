using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Adecco.WW.Packages.WebApi.Services.Client.Apim
{
    /// <summary>
    /// Represents the context for an API Management (APIM) request.
    /// This class is used to encapsulate various parameters that can be passed along with the request.
    /// It includes properties for Brand, Country, CorrelationId, and Headers.
    /// </summary>
    public sealed class ApimRequestContext
    {
        /// <summary>
        /// get or set Brand for an Apim Request Context that will be used in Apim Client Request. 
        /// </summary>
        public string Brand { get; set; }
        /// <summary>
        /// get or set Country for an Apim Request Context that will be used in Apim Client Request. 
        /// </summary>
        public string Country { get; set; }
        /// <summary>
        /// get or set Correlation Id for an Apim Request Context that will be used in Apim Client Request. 
        /// </summary>
        public string CorrelationId { get; set; }
        /// <summary>
        /// get or set Headers for an Apim Request Context that will be used in Apim Client Request.
        /// </summary>
        public IDictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
    }
}
