using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Domain.Entities
{
    public class PartnershipRequest
    {
        public int Id { get; set; }
        public string CompanyName { get; set; }
        public string CompanyPerson { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Message { get; set; }
        public DateTime CreateAt { get; set; }
        public DateTime UpdateAt { get; set; }
        public int PartnershiTypeId { get; set; }
        public bool IsActive { get; set; }

        public PartnershipType PartnershipType { get; set; }
    }
}
