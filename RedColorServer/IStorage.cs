using System.Collections.Generic;
using MongoDB.Driver;

namespace RedColorServer
{
    public interface IStorage
    {
        void RegisterDevice(string deviceType, string deviceId, IEnumerable<int> areas);

        IEnumerable<Device> FindDevicesForArea(int area);
        IEnumerable<Device> FindDevicesRegisteredForAll();
    }
}