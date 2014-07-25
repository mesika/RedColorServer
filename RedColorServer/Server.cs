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
        private static readonly log4net.ILog _logger = log4net.LogManager.GetLogger(typeof(Server));
        private static PushBroker _pushBroker;
        private static IStorage _storage;
        private const int LOOP_SECONDS = 1;

        public class Warning
        {
            public string id { get; set; }
            public string title { get; set; }
            public string[] data { get; set; }
        }
        public static void StartServer()
        {
            _logger.Info("Starting Server...");
            ConfigurePushEngine();
            ConfigureServiceHost();
            ConfigureStorage();

            ThreadPool.QueueUserWorkItem(RunLoop);

            _logger.Info("Server Started.");
        }

        private static void ConfigureStorage()
        {
            _logger.Debug("Configuring Mongo source.");
            _storage = MongoDBStorage.Instance;
            _logger.Debug("Mongo source configured.");
        }

        private static void ConfigureServiceHost()
        {
            var server = new Service();
            var serviceHost = new WebServiceHost(server);
            serviceHost.Open();
        }

        private static void ConfigurePushEngine()
        {
            _logger.Debug("Configuring push engine.");
            _pushBroker = new PushBroker();
            //var appleCert = File.ReadAllBytes(@"Certificates\RedColorPushDev.p12");

            _pushBroker.OnDeviceSubscriptionChanged += pushBroker_OnDeviceSubscriptionChanged;
            _pushBroker.OnNotificationFailed += _pushBroker_OnNotificationFailed;
            _pushBroker.OnServiceException += _pushBroker_OnServiceException;

            var appleCert = File.ReadAllBytes(@"Certificates\RedColorAPNProduction.p12");
            _pushBroker.RegisterAppleService(new ApplePushChannelSettings(true, appleCert, "8340706"));
            _logger.Debug("Push engine configured.");
        }

        static void _pushBroker_OnServiceException(object sender, Exception error)
        {
            _logger.Error("Push - Service Exception.", error);
            Console.WriteLine("Service Exception {0}", error);
        }

        static void _pushBroker_OnNotificationFailed(object sender, PushSharp.Core.INotification notification, Exception error)
        {
            _logger.Error("Push - Notification Failed", error);
            Console.WriteLine("Notification Failed {0}", error);
        }


        static void pushBroker_OnDeviceSubscriptionChanged(object sender, string oldSubscriptionId, string newSubscriptionId, PushSharp.Core.INotification notification)
        {
            
        }

        private static readonly HashSet<int> _lastValues = new HashSet<int>();
        private static readonly HashSet<int> _lastValues2 = new HashSet<int>();

        private const string TestData = "{\"id\" : \"1405098247742\",\"title\" : \"פיקוד העורף התרעה במרחב \",\"data\" : [\"ניו יורק 246\",\"לוס אנג'לס 250\"]}";
        private static void RunLoop(object state)
        {
            //var webClient = new CustomTimeOutWebClient();
            var webClient = new WebClient();
            long id = 0;

            StreamWriter logCsvWriterStream = File.AppendText(@"c:\log.csv");
            logCsvWriterStream.AutoFlush = true;
            
            while (true)
            {
                Thread.Sleep(TimeSpan.FromSeconds(LOOP_SECONDS));
                var counter = 0;
                try
                {
                    
                    //var data = webClient.DownloadString(@"http://www.oref.org.il/WarningMessages/alerts.json");
                    var data = webClient.DownloadString(@"http://www.mako.co.il/Collab/amudanan/adom.txt");
                    if (string.IsNullOrEmpty(data) == false)
                    {
                        var json = JsonConvert.DeserializeObject<Warning>(data);
                        //var json = JsonConvert.DeserializeObject<Warning>(TestData);

                        long jsonId;
                        if (long.TryParse(json.id, out jsonId) == false)
                            continue;

                        if (jsonId <= id) continue;
                        id = jsonId;

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

                                logCsvWriterStream.WriteLine(string.Format("{0},{1},{2}", DateTime.Now.ToString("o"), areaCode, area));

                                _logger.DebugFormat("ALERT: {0},{1}", areaCode, area);
                                    

                                _lastValues.Add(arseaCode);

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
                                    }
                                }
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

                                    sb.Remove(sb.Length - 2, 1);

                                    _pushBroker.QueueNotification(new AppleNotification()
                                                                        .ForDeviceToken(iosDeviceId)
                                                                        .WithAlert(sb.ToString())
                                                                        .WithSound("default")
                                                                        );

                                    Console.WriteLine("{0}\t{1}", DateTime.Now.ToLongTimeString(), sb.ToString());
                                }
                                catch (Exception ex)
                                {
                                    _logger.Error("Error Occured.", ex);
                                    Console.WriteLine("{0}\t{1}", DateTime.Now.ToLongTimeString(), ex);
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

                    //Thread.Sleep(TimeSpan.FromSeconds(LOOP_SECONDS));
                }
                catch (WebException ex)
                {
                    _logger.Error("Error Occured.", ex);
                    Console.WriteLine("{0}\tWeb Exception. Renewing webClient\t{1}", DateTime.Now.ToLongTimeString(), ex);
                    //webClient = new CustomTimeOutWebClient();
                    webClient = new WebClient();
                }
                catch (Exception ex)
                {
                    _logger.Error("Error Occured.", ex);
                    Console.WriteLine("{0}\t{1}", DateTime.Now.ToLongTimeString(), ex);
                    Thread.Sleep(TimeSpan.FromSeconds(LOOP_SECONDS));
                }

            }
        }
    }
}
