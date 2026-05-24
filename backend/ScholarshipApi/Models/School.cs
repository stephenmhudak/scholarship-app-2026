namespace ScholarshipApi.Models;

public class School
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string AddressLine1 { get; set; } = null!;
    public string AddressLine2 { get; set; } = null!;
    public string City { get; set; } = null!;
    public string State { get; set; } = null!;
    public string Zip { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
