using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RedColorServer
{
    interface IAlertProvider
    {
        void RaiseAlert();
    }

    class PushNotificationAlert : IAlertProvider
    {

        public void RaiseAlert()
        {
            throw new NotImplementedException();
        }
    }
}
