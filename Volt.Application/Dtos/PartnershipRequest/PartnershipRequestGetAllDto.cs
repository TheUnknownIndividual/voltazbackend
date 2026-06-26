using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Application.Dtos.PartnershipRequest
{
    public sealed record PartnershipRequestGetAllDto(
        int Id,
        string CompanyName,
        string PhoneNumber,
        string PartnershipTypeName,
        byte Status,
        DateTime CreatedAt
    );
}
