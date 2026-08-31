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
        /// <summary>Receives WhatsApp out-of-stock ("Yoxla") interaction alerts.</summary>
        public bool ReceivesYoxlaNotifications { get; set; }
        /// <summary>Receives solar calculator quote-request interaction alerts.</summary>
        public bool ReceivesQiymetlendirmeNotifications { get; set; }
        public ICollection<AdminPagePermission> PagePermissions { get; set; } = new List<AdminPagePermission>();
        public ICollection<AdminAuditLog> AuditLogs { get; set; } = new List<AdminAuditLog>();

        /// <summary>
        /// The "admin" account is the primary/root operator account and must always keep
        /// full access, independent of its IsSuperAdmin DB flag - so it can never be
        /// accidentally restricted, demoted, password-reset, or deleted by another admin.
        /// </summary>
        public static bool IsPrimaryUsername(string username)
            => string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase);

        public bool IsEffectiveSuperAdmin => IsSuperAdmin || IsPrimaryUsername(Username);
    }
}
