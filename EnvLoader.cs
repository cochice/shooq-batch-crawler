using System;
using System.IO;

namespace Marvin.Tmthfh91.Crawling
{
    public static class EnvLoader
    {
        private static bool _loaded = false;

        public static void Load(string? filePath = null)
        {
            if (_loaded) return;

            filePath ??= Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env");

            // 프로젝트 루트에서도 찾기
            if (!File.Exists(filePath))
            {
                var projectRoot = Directory.GetCurrentDirectory();
                filePath = Path.Combine(projectRoot, ".env");
            }

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Warning: .env file not found at {filePath}");
                return;
            }

            foreach (var line in File.ReadAllLines(filePath))
            {
                var trimmedLine = line.Trim();

                // 빈 줄이나 주석 무시
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
                    continue;

                var separatorIndex = trimmedLine.IndexOf('=');
                if (separatorIndex <= 0) continue;

                var key = trimmedLine.Substring(0, separatorIndex).Trim();
                var value = trimmedLine.Substring(separatorIndex + 1).Trim();

                // 따옴표 제거
                if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                    (value.StartsWith("'") && value.EndsWith("'")))
                {
                    value = value.Substring(1, value.Length - 2);
                }

                Environment.SetEnvironmentVariable(key, value);
            }

            _loaded = true;
        }

        public static string Get(string key, string defaultValue = "")
        {
            Load();
            return Environment.GetEnvironmentVariable(key) ?? defaultValue;
        }
    }
}
