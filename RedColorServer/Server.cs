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
using RedColorServer.AlertSources;

namespace RedColorServer
{
    public class Server
    {
        private static readonly log4net.ILog _logger = log4net.LogManager.GetLogger(typeof(Server));
        private static PushBroker _pushBroker;
        private static IStorage _storage;
        private const double LOOP_SECONDS = 0.6666666666666667;
        private static readonly System.Timers.Timer _loopTimer = new System.Timers.Timer(1000 * LOOP_SECONDS);

        public static void StartServer()
        {
            _logger.Info("Starting Server...");
            ConfigurePushEngine();
            ConfigureServiceHost();
            ConfigureStorage();

            //ThreadPool.QueueUserWorkItem(RunLoop);

            _logger.Info("Server Started.");

            _loopTimer.AutoReset = true;

            _loopTimer.Elapsed += (sender, e) =>
            {
                GenerateData2();
            };

            _loopTimer.Start();
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

        static long _id = 0;
        static object lock_object = new object();
        static object lock_urlChooser = new object();
        static int urlChooser = 0;

        const string orefUrl = "http://www.oref.org.il/WarningMessages/alerts.json";
        const string makoUrl = "http://www.mako.co.il/Collab/amudanan/adom.txt";
        private static string GetUrl()
        {
            lock (lock_urlChooser)
            {
                Interlocked.Increment(ref urlChooser);
                Interlocked.CompareExchange(ref urlChooser, 0, 2);

                if (urlChooser == 0)
                    return orefUrl;
                else
                    return makoUrl;
            }
        }

        private static int _rotator = 0;
        private static readonly OrefSource _orefSource = new OrefSource();
        private static readonly MakoSource _makoSource = new MakoSource();
        private static readonly YnetSource _ynetSource = new YnetSource();
        private static IAlertSource GetAlertFromRotatingSource()
        {
            int rot = _rotator % 3;
            //rot = 2;
            Interlocked.Increment(ref _rotator);
            switch (rot)
            {
                case 0:
                    return _orefSource;
                case 1:
                    return _makoSource;
                case 2:
                    return _ynetSource;
                default:
                    return _orefSource;
            }
        }

        private static void GenerateData2()
        {
            try
            {
                IAlertSource source = GetAlertFromRotatingSource();
                //_logger.DebugFormat("IAlert Source: {0}", source.GetType());

                var alert = source.GetAlerts();


                if (alert == AlertMessage.Empty)
                {
                    _lastValues2.Clear();
                    return;
                }

                var iosDevices = new Dictionary<string, List<string>>();
                _lastValues.Clear();

                foreach (var area in alert.Areas)
                {
                    var areaCode = area.Key;
                    var areaString = area.Value;
                    _logger.DebugFormat("ALERT: {0},{1}", areaCode, areaString);

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
                            iosDevices[device.DeviceId].Add(areaString);
                        }
                    }

                    var cursor = _storage.FindDevicesForArea(areaCode);
                    foreach (var device in cursor)
                    {
                        if (device.DeviceType == "ios")
                        {
                            if (iosDevices.ContainsKey(device.DeviceId) == false)
                                iosDevices[device.DeviceId] = new List<string>();
                            iosDevices[device.DeviceId].Add(areaString);
                        }
                    }
                }

                foreach (var iosDeviceId in iosDevices.Keys)
                {
                    try
                    {
                        var sb = new StringBuilder();
                        foreach (var area in iosDevices[iosDeviceId])
                        {
                            sb.AppendFormat("{0}, ", area);
                        }

                        sb.Remove(sb.Length - 2, 1);

                        _pushBroker.QueueNotification(new AppleNotification()
                                                            .ForDeviceToken(iosDeviceId)
                                                            .WithAlert(sb.ToString())
                                                            .WithSound("alert.caf")
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
            catch (Exception ex)
            {
                _logger.Error("Error Occured.", ex);
                Console.WriteLine("{0}\t{1}", DateTime.Now.ToLongTimeString(), ex);
            }
            finally
            {
                if (_loopTimer.AutoReset == false)
                    _loopTimer.Start();
            }
        }

        private static void GenerateData()
        {
            try
            {
                string data = string.Empty;
                using (var webClient = new WebClient())
                {
                    data = webClient.DownloadString(GetUrl());
                    //data = webClient.DownloadString(@"http://www.oref.org.il/WarningMessages/alerts.json");
                    //data = webClient.DownloadString(@"http://www.mako.co.il/Collab/amudanan/adom.txt");
                }
                if (string.IsNullOrEmpty(data) == false)
                {
                    var json = JsonConvert.DeserializeObject<OrefWarningMessage>(data);
                    //var json = JsonConvert.DeserializeObject<Warning>(TestData);

                    long jsonId;
                    if (long.TryParse(json.id, out jsonId) == false)
                        return;

                    lock (lock_object)
                    {
                        if (jsonId <= _id) return;
                        _id = jsonId;
                    }

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

                            _logger.DebugFormat("ALERT: {0},{1}", areaCode, area);


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
                                }
                            }

                            var cursor = _storage.FindDevicesForArea(areaCode);
                            foreach (var device in cursor)
                            {
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
                                var sb = new StringBuilder();
                                foreach (var area in iosDevices[iosDeviceId])
                                {
                                    sb.AppendFormat("{0}, ", area);
                                }

                                sb.Remove(sb.Length - 2, 1);

                                _pushBroker.QueueNotification(new AppleNotification()
                                                                    .ForDeviceToken(iosDeviceId)
                                                                    .WithAlert(sb.ToString())
                                                                    .WithSound("alert.caf")
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
            }
            catch (WebException ex)
            {
                _logger.Error("Error Occured.", ex);
                Console.WriteLine("{0}\tWeb Exception. Renewing webClient\t{1}", DateTime.Now.ToLongTimeString(), ex);
            }
            catch (Exception ex)
            {
                _logger.Error("Error Occured.", ex);
                Console.WriteLine("{0}\t{1}", DateTime.Now.ToLongTimeString(), ex);
            }
            finally
            {
                if (_loopTimer.AutoReset == false)
                    _loopTimer.Start();
            }
        }


        private static void RunLoop(object state)
        {
            //var webClient = new CustomTimeOutWebClient();
            //var webClient = new WebClient();
            long id = 0;

            StreamWriter logCsvWriterStream = File.AppendText(@"c:\log.csv");
            logCsvWriterStream.AutoFlush = true;

            while (true)
            {
                Thread.Sleep(TimeSpan.FromSeconds(LOOP_SECONDS));
                var counter = 0;
                try
                {
                    string data = string.Empty;
                    using (var webClient = new WebClient())
                    {
                        data = webClient.DownloadString(@"http://www.oref.org.il/WarningMessages/alerts.json");
                        //data = webClient.DownloadString(@"http://www.mako.co.il/Collab/amudanan/adom.txt");
                    }
                    if (string.IsNullOrEmpty(data) == false)
                    {
                        var json = JsonConvert.DeserializeObject<OrefWarningMessage>(data);
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
                    //webClient = new WebClient();
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
