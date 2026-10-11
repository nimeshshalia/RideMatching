using RideMatching.Domain.Common;

namespace RideMatching.Domain.Entities;

public class Rider : BaseEntity<Guid>
{
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public double Rating { get; private set; } = 5.0;

    protected Rider() { }

    public Rider(string name, string email, string phone)
    {
        Id = Guid.NewGuid();
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Email = email ?? throw new ArgumentNullException(nameof(email));
        Phone = phone ?? throw new ArgumentNullException(nameof(phone));
        CreatedAtUtc = DateTime.UtcNow;
    }
}
