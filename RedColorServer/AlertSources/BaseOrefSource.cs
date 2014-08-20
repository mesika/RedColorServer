using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace RedColorServer.AlertSources
{
    abstract class BaseOrefSource : IAlertSource
    {
        protected static long _lastId;
        protected static readonly object lock_object = new object();
        protected readonly string _url;
        private const string TestData = "{\"id\" : \"1405098247742\",\"title\" : \"פיקוד העורף התרעה במרחב \",\"data\" : [\"ניו יורק 246\",\"לוס אנג'לס 250\"]}";

        public BaseOrefSource(string url)
        {
            _url = url;
        }

        public AlertMessage GetAlerts()
        {
            string data = string.Empty;
            using (var webClient = new WebClient())
            {
                data = webClient.DownloadString(_url);
            }

            //data = TestData;

            if (string.IsNullOrEmpty(data))
                return AlertMessage.Empty;


            var json = JsonConvert.DeserializeObject<OrefWarningMessage>(data);
            //var json = JsonConvert.DeserializeObject<Warning>(TestData);


            long jsonId;
            if (long.TryParse(json.id, out jsonId) == false)
                return AlertMessage.Empty;


            lock (lock_object)
            {
                if (jsonId <= _lastId) return AlertMessage.Empty;
                _lastId = jsonId;
            }

            var alertMessage = new AlertMessage();

            alertMessage.Id = jsonId;
            foreach (var area in json.data)
            {
                int areaCode = 0;
                var areaSplitted = area.Split(' ');
                if (areaSplitted.Length > 1)
                {
                    if (int.TryParse(areaSplitted.Last(), out areaCode) == false)
                        continue;
                }
                alertMessage.Areas.Add(areaCode, area);
            }
            return alertMessage;
        }
    }
}
