using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Infrastructure.Configuration
{
    public class FtpOptions
    {
        public string Host { get; set; }
        public int Port { get; set; } = 21;
        public string Username { get; set; }
        public string Password { get; set; }
        public string BasePath { get; set; }
        public string BaseUrl { get; set; }
    }
}
