using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace IdopontfoglaloWebalk.Models
{
    public class Services
    {
        [Key]
        public int service_id { get; set; }
        [Required(ErrorMessage = "A szolgáltatás nevének megadása kötelező!")]
        [StringLength(100, ErrorMessage = "A név legfeljebb {1} karakter hosszú lehet.")]
        public string? name { get; set; }
        [Required(ErrorMessage = "A helyszín / cím megadása kötelező!")]
        public string? address { get; set; }
        [Required(ErrorMessage = "A leírás megadása kötelező!")]
        public string? description { get; set; }
        public double rating { get; set; }
        public double min_guest_rating { get; set; } = 0.0;

        public string? owner_id { get; set; }
        
        public Users? Owner { get; set; }

        public int category_id { get; set; }
        [ForeignKey("category_id")]
        public virtual Categories? Category { get; set; }
        public List<ServiceCategories> ServiceCategories { get; set; } = new();
    }
}
