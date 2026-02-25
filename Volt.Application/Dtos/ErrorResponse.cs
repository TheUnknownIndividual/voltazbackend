using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Application.Dtos
{
    public class ErrorResponse<T>
    {
        public string Code { get; set; }
        public T Details { get; set; }
    }
}
