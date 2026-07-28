using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Application.Dtos
{
    using System.Text.Json.Serialization;

    public sealed record TokenDto(string AccessToken)
    {
        [JsonIgnore]
        public int UserId { get; init; }
    }
}
