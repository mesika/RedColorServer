using System.Collections.Generic;
using MongoDB.Bson;

namespace RedColorServer
{
    public class Device
    {
        public ObjectId Id { get; set; }
        public string Name { get; set; }
        public string DeviceType { get; set; }
        public string DeviceId { get; set; }
        public IEnumerable<int> Areas { get; set; }
    }
}