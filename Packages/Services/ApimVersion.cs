using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Adecco.WW.Packages.WebApi.Services
{
    public enum ApimVersion
    {
        None,
        v1,
        v2,
        v3,
        v4,
        it,
        au,
        ms,
        [EnumMember(Value = "ms-au")]
        msau
    }
}
