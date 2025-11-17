using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TinyOrm.Examples;

[Table("cities")]
public sealed class City
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")] public int Id { get; set; }
    [Column("name")] public string? Name { get; set; }
    [Column("person_id")] public int PersonId { get; set; }
    [Column("province_id")] public int ProvinceId { get; set; }
}