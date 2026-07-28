using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Domain.Enums;

namespace Volt.Domain.Entities
{
    public class AdminUser
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string DisplayName { get; set; }
        public byte[] PasswordHash { get; set; }
        public byte[] PasswordSalt { get; set; }
        public Role Role { get; set; }
        public bool IsActive { get; set; }
        public bool IsSuperAdmin { get; set; }
        public bool CanDeleteProjects { get; set; }
        public bool CanEditProjects { get; set; }
        public bool CanApproveWarehouseMovements { get; set; }
        /// <summary>Receives accepted-project notifications without changing this admin's access rights.</summary>
        public bool IsStakeholder { get; set; }
        /// <summary>Confidential monthly salary. Returned only by the Accounting API.</summary>
        public decimal? MonthlySalary { get; set; }
        /// <summary>Employment start date maintained by Human Resources.</summary>
        public DateTime? EmploymentStartDate { get; set; }
        /// <summary>Usual monthly salary payment date maintained by Human Resources.</summary>
        public DateTime? SalaryPaymentDate { get; set; }
        /// <summary>Private Telegram chat ID collected after the staff member starts the internal bot.</summary>
        public long? TelegramChatId { get; set; }
        public ICollection<AdminPagePermission> PagePermissions { get; set; } = new List<AdminPagePermission>();
        public ICollection<AdminAuditLog> AuditLogs { get; set; } = new List<AdminAuditLog>();
    }
}
