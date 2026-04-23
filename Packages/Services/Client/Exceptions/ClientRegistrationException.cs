using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Adecco.WW.Packages.WebApi.Services.Client.Exceptions
{
    /// <summary>
    /// Client Registration Exception
    /// </summary>
    public class ClientRegistrationException : InvalidOperationException
    {
        /// <summary>
        /// Client Registration Exception
        /// </summary>
        /// <param name="message"></param>
        /// <param name="inner"></param>
        public ClientRegistrationException(string message, Exception inner = null)
            : base(message, inner) { }
    }
}
