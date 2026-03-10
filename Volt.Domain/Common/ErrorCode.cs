using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Domain.Common
{
    public static class ErrorCode
    {
        public const string SERVER_ERROR     = "SERVER_ERROR";
        public const string VALIDATION_ERROR = "VALIDATION_ERROR";
        public const string INVALID_USERNAME = "INVALID_USERNAME";
        public const string INVALID_PASSWORD = "INVALID_PASSWORD";
        public const string ADMIN_NOT_FOUND = "Admin not found";
    }
}
