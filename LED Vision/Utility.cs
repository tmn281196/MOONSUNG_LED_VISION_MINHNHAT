using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LEDVision
{
    public static class Utility
    {
        public static T Clone<T>(this T source)
        {
            var serialized = JsonSerializer.Serialize(source);
            return JsonSerializer.Deserialize<T>(serialized);
        }

        public static void SaveModel<T>(this T source, string filePath, string fileName)
        {
            try
            {
                if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    filePath += ".json";
                }
                string json = JsonSerializer.Serialize(source, new JsonSerializerOptions
                {
                    WriteIndented = true // Makes JSON output more readable
                });

                File.WriteAllText(filePath, json);

                
            }
            catch (Exception ex)
            {
            }
        }

        public static T ConvertFromJson<T>(string jsonStr)
        {
            JsonSerializerOptions options = new JsonSerializerOptions()
            {
                MaxDepth = 1024,
                //WriteIndented = true
            };
            return JsonSerializer.Deserialize<T>(jsonStr, options);
        }
    }
}