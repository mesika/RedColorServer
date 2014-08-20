using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace RedColorServer.AlertSources
{
    class OrefSource : BaseOrefSource
    {
        private const string URL = "http://www.oref.org.il/WarningMessages/alerts.json";
        public OrefSource()
            : base(URL)
        {

        }
    }


}
