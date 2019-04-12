using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace PvPChecks
{
    public class Config
    {
        public List<int> weaponBans = new List<int>();
        public List<int> accsBans = new List<int>();
        public List<int> armorBans = new List<int>();
        public List<int> buffBans = new List<int>();
        public List<int> projBans = new List<int>();

        public static Config ReadOrCreate(string configPath)
        {
            if (File.Exists(configPath))
            {
                return JsonConvert.DeserializeObject<Config>(File.ReadAllText(configPath));
            }
            var config = new Config();
            File.WriteAllText(configPath, JsonConvert.SerializeObject(config, Formatting.Indented));
            return config;
        }

        public void Write(string path)
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
    }
}
