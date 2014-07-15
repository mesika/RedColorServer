using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceModel.Web;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using PushSharp;
using PushSharp.Android;
using PushSharp.Apple;

namespace RedColorServer
{
    public class Server
    {
        private static PushBroker _pushBroker;
        private static IStorage _storage;

        public class Warning
        {
            public string id { get; set; }
            public string title { get; set; }
            public string[] data { get; set; }
        }
        public static void StartServer()
        {
            ConfigurePushEngine();
            ConfigureServiceHost();
            ConfigureStorage();

            ThreadPool.QueueUserWorkItem(RunLoop);
        }

        private static void ConfigureStorage()
        {
            _storage = MongoDBStorage.Instance;
        }

        private static void ConfigureServiceHost()
        {
            var server = new Service();
            var serviceHost = new WebServiceHost(server);
            serviceHost.Open();
        }

        private static void ConfigurePushEngine()
        {
            _pushBroker = new PushBroker();
            //var appleCert = File.ReadAllBytes(@"Certificates\RedColorPushDev.p12");
            
            _pushBroker.OnDeviceSubscriptionChanged += pushBroker_OnDeviceSubscriptionChanged;
            _pushBroker.OnNotificationFailed += _pushBroker_OnNotificationFailed;

            var appleCert = File.ReadAllBytes(@"Certificates\RedColorAPNProduction.p12");
            _pushBroker.RegisterAppleService(new ApplePushChannelSettings(true, appleCert, "8340706"));
        }

        static void _pushBroker_OnNotificationFailed(object sender, PushSharp.Core.INotification notification, Exception error)
        {
            
        }


        static void pushBroker_OnDeviceSubscriptionChanged(object sender, string oldSubscriptionId, string newSubscriptionId, PushSharp.Core.INotification notification)
        {

        }

        private static readonly HashSet<int> _lastValues = new HashSet<int>();
        private static readonly HashSet<int> _lastValues2 = new HashSet<int>();

        //private const string TestData ="{\"id\" : \"1405095741763\",\"title\" : \"פיקוד העורף התרעה במרחב \",\"data\" : [\"באר שבע 210\",\"באר שבע 211\",\"באר שבע 212\",\"באר שבע 213\",]}";

        private const string TestData =
            "{\"id\" : \"1405098247742\",\"title\" : \"פיקוד העורף התרעה במרחב \",\"data\" : [\"ניו יורק 246\",\"G 250\"]}";
        private static void RunLoop(object state)
        {
            var webClient = new CustomTimeOutWebClient();
            
            while (true)
            {
                var counter = 0;
                try
                {
                    var data = webClient.DownloadString(@"http://www.oref.org.il/WarningMessages/alerts.json");
                    if (string.IsNullOrEmpty(data) == false)
                    {
                        var json = JsonConvert.DeserializeObject<Warning>(data);
                        //var json = JsonConvert.DeserializeObject<Warning>(TestData);
                        
                        if (json.data.Length > 0)
                        {
                            var iosDevices = new Dictionary<string, List<string>>();
                            _lastValues.Clear();
                            foreach (var area in json.data)
                            {
                                int areaCode = 0;
                                var areaSplitted = area.Split(' ');
                                if (areaSplitted.Length > 1)
                                {
                                    if (int.TryParse(areaSplitted.Last(), out areaCode) == false)
                                        continue;
                                }

                                _lastValues.Add(areaCode);

                                if (_lastValues2.Contains(areaCode))
                                {
                                    continue;
                                }
                                else
                                {
                                    _lastValues2.Add(areaCode);
                                }

                                var allDevices = _storage.FindDevicesRegisteredForAll();
                                foreach (var device in allDevices)
                                {
                                    if (device.DeviceType == "ios")
                                    {
                                        if (iosDevices.ContainsKey(device.DeviceId) == false)
                                            iosDevices[device.DeviceId] = new List<string>();
                                        iosDevices[device.DeviceId].Add(area);

                                        //try
                                        //{
                                        //    _pushBroker.QueueNotification(new AppleNotification()
                                        //                                        .ForDeviceToken(device.DeviceId)
                                        //                                        .WithAlert(area)
                                        //                                        .WithSound("default")
                                        //                                        );
                                        //}
                                        //catch (Exception ex)
                                        //{
                                        //    Console.WriteLine(ex);
                                        //}
                                    }
                                }

                                var cursor = _storage.FindDevicesForArea(areaCode);
                                foreach (var device in cursor)
                                {
                                    Interlocked.Increment(ref counter);
                                    if (device.DeviceType == "ios")
                                    {
                                        if (iosDevices.ContainsKey(device.DeviceId) == false)
                                            iosDevices[device.DeviceId] = new List<string>();
                                        iosDevices[device.DeviceId].Add(area);

                                        //try
                                        //{
                                        //    _pushBroker.QueueNotification(new AppleNotification()
                                        //                                        .ForDeviceToken(device.DeviceId)
                                        //                                        .WithAlert(area)
                                        //                                        .WithSound("default")
                                        //                                        );
                                        //}
                                        //catch (Exception ex)
                                        //{
                                        //    Console.WriteLine(ex);
                                        //}
                                    }
                                }

                                //Console.WriteLine("{0}: \t{1}", DateTime.Now.ToLongTimeString(), area);
                                //Console.WriteLine("AreaCode: {0}", areaCode);
                            }

                            foreach (var iosDeviceId in iosDevices.Keys)
                            {
                                try
                                {
                                    Interlocked.Increment(ref counter);
                                    var sb = new StringBuilder();
                                    foreach (var area in iosDevices[iosDeviceId])
                                    { 
                                        sb.AppendFormat("{0}, ", area);
                                    }

                                    _pushBroker.QueueNotification(new AppleNotification()
                                                                        .ForDeviceToken(iosDeviceId)
                                                                        .WithAlert(sb.ToString())
                                                                        .WithSound("default")
                                                                        );

                                    Console.WriteLine("{0}\t{1}", DateTime.Now.ToLongTimeString(), sb.ToString());
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex);
                                }
                            }

                            foreach (var val in _lastValues2.ToList())
                            {
                                if (_lastValues.Contains(val) == false)
                                {
                                    _lastValues2.Remove(val);
                                }
                            }
                        }
                        else
                        {
                            _lastValues2.Clear();
                        }

                    }
                    if (counter > 0)
                        Console.WriteLine("Sent {0} times.", counter);

                    //_pushBroker.StopAllServices(true);

                    Thread.Sleep(TimeSpan.FromSeconds(2));
                }
                catch (Exception)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(2));
                }

            }
        }
    }
}
