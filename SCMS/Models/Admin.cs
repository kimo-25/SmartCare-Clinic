using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SCMS.Models
{
    public class Admin
    {
        [Key]
        public int AdminId { get; set; }

        [ForeignKey(nameof(StaffId))]
        public int StaffId { get; set; }

        public string AccessLevel { get; set; } = null!;

        public Staff Staff { get; set; } = null!;
    }
}
