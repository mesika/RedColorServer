using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Web;

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
            deviceId = deviceId.Replace(" ", string.Empty).Replace("<", string.Empty).Replace(">", string.Empty);
            _storage.RegisterDevice(deviceType, deviceId, areas);
        }


        public void TestData()
        {
            throw new System.NotImplementedException();
        }
    }
}