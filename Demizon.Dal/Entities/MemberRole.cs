namespace Demizon.Dal.Entities;

public class MemberRole
{
    // Surrogate key on purpose: the audit log records only the first key column, so with
    // (MemberId, Role) as the key a removed role could not be told apart from the others.
    public int Id { get; set; }

    public int MemberId { get; set; }

    public UserRole Role { get; set; }

    public virtual Member Member { get; set; } = null!;
}
