using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Adecco.WW.Packages.WebApi.Services.Client.Exceptions
{
    /// <summary>
    /// Exception thrown when token retrieval fails
    /// </summary>
    public class TokenRetrievalException : SystemException
    {
        /// <summary>
        /// Exception thrown when token retrieval fails
        /// </summary>
        /// <param name="source"></param>
        /// <param name="inner"></param>
        public TokenRetrievalException(string source, Exception inner)
            : base($"Token retrieval failed for {source}", inner) { }
    }
}
