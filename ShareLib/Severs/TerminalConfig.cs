using System;
using System.Collections.Generic;  // ← THÊM dòng này
using System.IO;
using System.Security.Cryptography; // ← THÊM dòng này (cho hash mật khẩu)
using System.Text;                  // ← THÊM dòng này

namespace SharedLib.Services
{
    public class TerminalConfig
    {
        public string FactoryCode { get; set; } = "F1";
        public string LineName { get; set; } = "Line 01";
        public string DbPath { get; set; } = "test.db";
        public string AdminPasswordHash { get; set; } = HashPassword("admin"); // ← THÊM
        public List<string> Stations { get; set; } = new List<string>();       // ← THÊM

        public static TerminalConfig Load(string cfgPath)
        {
            var cfg = new TerminalConfig();
            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, cfgPath);

            if (!File.Exists(fullPath))
            {
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
                    case "AdminPasswordHash": cfg.AdminPasswordHash = value; break; // ← THÊM
                    case "Station":                                                  // ← THÊM
                        if (!string.IsNullOrWhiteSpace(value))                      // ← THÊM
                            cfg.Stations.Add(value);                                // ← THÊM
                        break;                                                      // ← THÊM
                }
            }
            return cfg;
        }

        // ── THÊM 3 method này vào ────────────────────────────────────
        public void Save(string cfgPath)
        {
            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, cfgPath);
            using var w = new StreamWriter(fullPath, append: false, Encoding.UTF8);
            w.WriteLine($"FactoryCode={FactoryCode}");
            w.WriteLine($"LineName={LineName}");
            w.WriteLine($"DbPath={DbPath}");
            w.WriteLine($"AdminPasswordHash={AdminPasswordHash}");
            w.WriteLine();
            w.WriteLine("# Danh sách trạm");
            foreach (var s in Stations)
                w.WriteLine($"Station={s}");
        }

        public bool VerifyPassword(string input)
            => HashPassword(input) == AdminPasswordHash;

        public void SetPassword(string newPassword)
            => AdminPasswordHash = HashPassword(newPassword);

        public static string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            var sb = new StringBuilder();
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
        // ─────────────────────────────────────────────────────────────
    }
}