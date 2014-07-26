using System;
using System.Collections.Generic;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace RedColorServer
{
    public class MongoDBStorage : IStorage
    {
        private static MongoDBStorage _instance;
        private const string _connectionString = "mongodb://localhost";
        private MongoDatabase _dataBase;

        private MongoDBStorage()
        {
            var mongoClient = new MongoClient(_connectionString);
            var server = mongoClient.GetServer();
            _dataBase = server.GetDatabase("redcolor");
        }

        public static MongoDBStorage Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new MongoDBStorage();
                }
                return _instance;
            }
        }
        public void RegisterDevice(string deviceType, string deviceId, HashSet<int> areas)
        {
            var collection = _dataBase.GetCollection<Device>("devices");
            var query = Query<Device>.EQ(x => x.DeviceId, deviceId);
            var existingDevice = collection.FindOne(query);

            if (existingDevice != null)
            {
                existingDevice.Areas = areas;
                collection.Save(existingDevice);
            }
            else
            {
                var device = new Device() { DeviceType = deviceType, DeviceId = deviceId, Areas = areas };
                collection.Insert(device);
                collection.Save(device);
            }
        }

        public IEnumerable<Device> FindDevicesRegisteredForAll()
        {
            var collection = _dataBase.GetCollection<Device>("devices");
            var query = Query<Device>.EQ(x => x.Areas, -1);
            var results = collection.Find(query);
            return results;
        }

        public IEnumerable<Device> FindDevicesForArea(int area)
        {
            var collection = _dataBase.GetCollection<Device>("devices");
            //var query = Query<Device>.ElemMatch(x => x.Areas, builder => builder.Exists(i=>i==area));
            var query = Query<Device>.EQ(x=>x.Areas, area);
            var results = collection.Find(query);
            return results;
        }
    }
}