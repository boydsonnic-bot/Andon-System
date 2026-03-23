using System;
using System.IO;

namespace SharedLib.Services
{
    public class TerminalConfig
    {
        public string FactoryCode { get; set; } = "F1";
        public string LineName { get; set; } = "Line 01";
        public string DbPath { get; set; } = "test.db"; // Mặc định chạy nội bộ, sau này đổi thành \\SERVER\Share\andon.db

        public static TerminalConfig Load(string cfgPath)
        {
            var cfg = new TerminalConfig();

            // Tìm file config ở ngay cạnh file chạy .exe
            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, cfgPath);

            if (!File.Exists(fullPath))
            {
                // Nếu chưa có file, tự tạo ra file mẫu
                File.WriteAllText(fullPath, "FactoryCode=F1\nLineName=Line 01\nDbPath=test.db");
                return cfg;
            }

            foreach (var line in File.ReadAllLines(fullPath))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith(";") || line.StartsWith("#")) continue;

                var parts = line.Split(new[] { '=' }, 2);
                if (parts.Length != 2) continue;

                string key = parts[0].Trim();
                string value = parts[1].Trim();

                switch (key)
                {
                    case "FactoryCode": cfg.FactoryCode = value; break;
                    case "LineName": cfg.LineName = value; break;
                    case "DbPath": cfg.DbPath = value; break;
                }
            }
            return cfg;
        }
    }
}