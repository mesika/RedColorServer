using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Linq;

namespace RedColorServer
{
    [ServiceContract]
    public interface IServer
    {
        [OperationContract]
        [WebInvoke(UriTemplate = "register", BodyStyle = WebMessageBodyStyle.Wrapped)]
        void RegisterDevice(string deviceType, string deviceId, List<int> areas);

        [WebGet(UriTemplate = "testdata")]
        void TestData();
    }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class Service : IServer
    {
        private static readonly log4net.ILog _logger = log4net.LogManager.GetLogger(typeof(Server));
        private readonly IStorage _storage;

        public Service()
        {
            _storage = MongoDBStorage.Instance;
        }
        public void RegisterDevice(string deviceType, string deviceId, List<int> areas)
        {
            var areasString = string.Join(",", areas.Select(p => p.ToString()).ToArray());

            _logger.InfoFormat("User registered device type:{0} id:{1}: with areas:{2}", deviceType, deviceId, areasString);
            if (areas.Any() == false)
            {
                areas = new List<int> { -1 };
            }
            deviceId = deviceId.Replace(" ", string.Empty).Replace("<", string.Empty).Replace(">", string.Empty);
            _storage.RegisterDevice(deviceType, deviceId, areas);
        }


        public void TestData()
        {
            throw new System.NotImplementedException();
        }
    }
}