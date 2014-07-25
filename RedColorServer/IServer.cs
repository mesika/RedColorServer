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
        void RegisterDevice(string deviceType, string deviceId, IEnumerable<int> areas);

        [WebGet(UriTemplate = "testdata")]
        void TestData();
    }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class Service : IServer
    {
        private readonly IStorage _storage;

        public Service()
        {
            _storage = MongoDBStorage.Instance;
        }
        public void RegisterDevice(string deviceType, string deviceId, IEnumerable<int> areas)
        {
            if (areas.Any() == false)
            {
                areas = new int[] { -1 };
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