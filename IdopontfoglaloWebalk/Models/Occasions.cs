using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IdopontfoglaloWebalk.Models
{
    public class Occasions
    {
        [Key]
        public int reservation_id { get; set; }

        public int service_id { get; set; }

        [ForeignKey("service_id")]
        public virtual Services? Service { get; set; }

        public int service_category_id { get; set; }

        [ForeignKey("service_category_id")]
        public virtual ServiceCategories? ServiceCategory { get; set; }

        public string? user_id { get; set; }

        [ForeignKey("user_id")]
        public virtual Users? User { get; set; }

        public DateTime date { get; set; }

        public DateTime? reservation_date { get; set; }

        public string? status { get; set; }
        public bool reminder_sent { get; set; } = false;
    }
}
