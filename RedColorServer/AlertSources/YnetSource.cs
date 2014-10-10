using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace RedColorServer.AlertSources
{
    
    public class YnetWarningMessage
    {
        public YnetAlerts alerts { get; set; }
    }

    public class YnetAlerts
    {
        public IEnumerable<YnetItem> items = new List<YnetItem>();
    }

    public class YnetItem
    {
        public Item item { get; set; }
    }

    public class Item
    {
        public string guid { get; set; }
        public string pubdate { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        public string link { get; set; }

    }

    public static class StringExt
    {
        public static string TrimStart(this string target, string trimString)
        {
            string result = target;
            while (result.StartsWith(trimString))
            {
                result = result.Substring(trimString.Length);
            }

            return result;
        }

        public static string TrimEnd(this string target, string trimString)
        {
            string result = target;
            while (result.EndsWith(trimString))
            {
                result = result.Substring(0, result.Length - trimString.Length);
            }

            return result;
        }
    }
    class YnetSource : IAlertSource
    {
        private static readonly log4net.ILog _logger = log4net.LogManager.GetLogger(typeof(YnetSource));
        private static string _sample = "jsonCallback({\"alerts\":{\"items\":[{\"item\":{\"guid\":\"77039\",\"pubdate\":\"15:20\",\"title\":\"×?×¨×—×‘ × ×©×§×?×•×? 246\",\"description\":\"× ×©×§×?×•×?, ×‘× ×¨ ×’× ×™× \",\"link\":\"http://www.oref.org.il\"}},{\"item\":{\"guid\":\"77040\",\"pubdate\":\"15:20\",\"title\":\"×?×¨×—×‘ × ×©×§×?×•×? 247\",\"description\":\"× .×?×¢×©×™×” × ×©×§×?×•×?\",\"link\":\"http://www.oref.org.il\"}},{\"item\":{\"guid\":\"77041\",\"pubdate\":\"15:20\",\"title\":\"×?×¨×—×‘ × ×©×§×?×•×? 250\",\"description\":\"×‘×™×? ×©×§×?×”, ×‘×? ×”×“×¨, ×’×™× ×”, ×?×‘×§×™×¢×™× , ×?×?×?×™ ×™×₪×”\",\"link\":\"http://www.oref.org.il\"}}]}});";
        private static string _sample2 = "{\"alerts\":{\"items\":[{\"item\":{\"guid\":\"77039\",\"pubdate\":\"15:20\",\"title\":\"David 246\",\"description\":\"Wallak\",\"link\":\"http://www.oref.org.il\"}},{\"item\":{\"guid\":\"77040\",\"pubdate\":\"15:20\",\"title\":\"Moshe 247\",\"description\":\"Bdallak\",\"link\":\"http://www.oref.org.il\"}},{\"item\":{\"guid\":\"77041\",\"pubdate\":\"15:20\",\"title\":\"Tzion 250\",\"description\":\"Smallak\",\"link\":\"http://www.oref.org.il\"}}]}}";

        private static long _lastId = -1;
        private static object lock_object = new object();

        public AlertMessage GetAlerts()
        {
            string data = string.Empty;
            using (var webClient = new WebClient())
            {
                data = webClient.DownloadString("http://alerts.ynet.co.il/alertsrss/YnetPicodeHaorefAlertFiles.js");
            }

            //data = _sample;

            var trimmedData = data.TrimStart("jsonCallback(").TrimEnd(");");

            YnetWarningMessage json;
            try
            {
                json = JsonConvert.DeserializeObject<YnetWarningMessage>(trimmedData);
            }
            catch (Exception ex)
            {
                _logger.ErrorFormat("JSON: {0}", trimmedData);
                throw;
            }

            if (json.alerts == null)
                return AlertMessage.Empty;

            var alertMessage = new AlertMessage();
            foreach (var item in json.alerts.items)
            {
                long jsonId;
                if (long.TryParse(item.item.guid, out jsonId) == false)
                    return AlertMessage.Empty;

                lock (lock_object)
                {
                    if (jsonId <= _lastId) return AlertMessage.Empty;
                    _lastId = jsonId;
                }

                
                alertMessage.Id = jsonId;

                int areaCode = 0;
                var areaSplitted = item.item.title.Split(' ');
                if (areaSplitted.Length > 1)
                {
                    if (int.TryParse(areaSplitted.Last(), out areaCode) == false)
                        continue;
                }
                alertMessage.Areas.Add(areaCode, item.item.title);
            }

            return alertMessage;
        }


    }
}
