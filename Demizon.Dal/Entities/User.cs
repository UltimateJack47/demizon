﻿namespace Demizon.Dal.Entities;

public class User
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Surname { get; set; } = null!;

    public string Login { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public UserRole Role { get; set; } = UserRole.Standard;
}

public enum UserRole
{
    Standard = 0,
    Admin = 1
}
