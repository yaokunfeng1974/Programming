using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Adecco.WW.Packages.WebApi.Services.Client.Exceptions
{
    /// <summary>
    /// Configuration Exception
    /// </summary>
    public class ConfigurationException : InvalidOperationException
    {

        /// <summary>
        /// Configuration Exception 
        /// </summary>
        /// <param name="message"></param>
        public ConfigurationException(string message)
         : base($"Configuration Error : {message}") { }
        /// <summary>
        /// Configuration Exception
        /// </summary>
        /// <param name="message"></param>
        /// <param name="config"></param>
        public ConfigurationException(string message, object config)
            : base($"Configuration Error ({config.GetType()}): {message}") { }
        /// <summary>
        /// Configuration Exception
        /// </summary>
        /// <param name="message"></param>
        /// <param name="e"></param>
        /// <param name="config"></param>
        public ConfigurationException(string message,Exception e ,object config)
            : base($"Configuration Error ({config.GetType()}): {message}",e) { }
    }
}
