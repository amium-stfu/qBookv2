using System;
using System.Collections.Generic;
using System.Linq;

namespace Amium.UiEditor.Models;

public sealed class UserLevel
{
    public int Id { get; }
    public string Caption { get; }
    public string Password { get; }
    public int ViewLimit { get; }
    public string? Color { get; }

    public UserLevel(int id, string caption, string password, int viewLimit, string? color)
    {
        Id = id;
        Caption = caption;
        Password = password;
        ViewLimit = viewLimit;
        Color = color;
    }

    public static IReadOnlyList<UserLevel> All { get; } = new List<UserLevel>
    {
        // 0,User,,10, null //Theme gesteuert
        new(0, "User", string.Empty, 10, null),
        // 1,Service,service,20,Blue
        new(1, "Service", "service", 20, "Green"),
        // 2,Admin,admin,30, Red
        new(2, "Admin", "admin", 30, "Red"),
        // 3,Root,root#,100,#ff8c00
        new(3, "Root", "root#", 100, "#ff8c00")
    };

    public static UserLevel Default => All[0];

    public static UserLevel GetByPasswordOrDefault(string? password)
    {
        password ??= string.Empty;
        var match = All.FirstOrDefault(u => string.Equals(GetCurrentPassword(u), password, StringComparison.Ordinal));
        return match ?? Default;
    }

    private static readonly Dictionary<int, string> CurrentPasswords =
        All.ToDictionary(u => u.Id, u => u.Password);

    public static string GetCurrentPassword(UserLevel user)
        => CurrentPasswords.TryGetValue(user.Id, out var value) ? value : user.Password;

    public static bool TryChangePassword(UserLevel user, string oldPassword, string newPassword, out string error)
    {
        oldPassword ??= string.Empty;
        newPassword ??= string.Empty;

        if (!string.Equals(GetCurrentPassword(user), oldPassword, StringComparison.Ordinal))
        {
            error = "Old password does not match.";
            return false;
        }

        CurrentPasswords[user.Id] = newPassword;
        error = string.Empty;
        return true;
    }

    public static void ResetPasswordsToDefaults()
    {
        foreach (var user in All)
        {
            CurrentPasswords[user.Id] = user.Password;
        }
    }
}
