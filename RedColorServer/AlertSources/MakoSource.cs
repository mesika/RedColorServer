using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RedColorServer.AlertSources
{
    class MakoSource : BaseOrefSource
    {
        private const string URL = "http://www.mako.co.il/Collab/amudanan/adom.txt";
        public MakoSource()
            : base(URL)
        {

        }
    }
}
