using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Domain.Entities
{
    public class Product
    {
        public int Id { get; set; }
        [StringLength(15, MinimumLength = 5, ErrorMessage = "İstifadəçi adı 5-15 simvol arasında olmalıdır.")]
        public string Name { get; set; }
    }
}
