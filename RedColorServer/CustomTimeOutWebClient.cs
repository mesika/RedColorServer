using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace RedColorServer
{
    class CustomTimeOutWebClient : WebClient
    {
        private const int TIME_OUT = 2000;

        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest webRequest = base.GetWebRequest(address);
            webRequest.Timeout = TIME_OUT;
            return webRequest;
        }
    }
}
