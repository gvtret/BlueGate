using System.IO;

namespace BlueGate.Core.Services
{
    public class FileInvocationCounter
    {
        private readonly string _filePath;

        public FileInvocationCounter(string filePath)
        {
            _filePath = filePath;
        }

        public long Get()
        {
            if (!File.Exists(_filePath))
            {
                return 0;
            }

            var content = File.ReadAllText(_filePath);
            if (long.TryParse(content, out var value))
            {
                return value;
            }

            return 0;
        }

        public void Set(long value)
        {
            File.WriteAllText(_filePath, value.ToString());
        }
    }
}
