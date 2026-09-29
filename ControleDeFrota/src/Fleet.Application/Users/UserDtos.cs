using Fleet.Application.Auth;
using Fleet.Application.Common;
using Fleet.Domain.Users;
using FluentValidation;

namespace Fleet.Application.Users;

public sealed class UserListRequest : ListRequest
{
    public UserStatus? Status { get; set; }
    /// <summary>Only honored for platform administrators (companies.manage).</summary>
    public Guid? CompanyId { get; set; }
}

public sealed record UserCreateRequest
{
    public string? Name { get; init; }
    public string? Email { get; init; }
    public string? Password { get; init; }
    /// <summary>Defaults to the caller's company. Another company requires companies.manage.</summary>
    public Guid? CompanyId { get; init; }
    public UserStatus Status { get; init; } = UserStatus.Active;
    public IReadOnlyList<int> RoleIds { get; init; } = [];
}

public sealed record UserUpdateRequest
{
    public string? Name { get; init; }
    public string? Email { get; init; }
    public UserStatus Status { get; init; } = UserStatus.Active;
    public IReadOnlyList<int> RoleIds { get; init; } = [];
}

public sealed record ResetPasswordRequest
{
    public string? NewPassword { get; init; }
}

public sealed record UserRoleResponse(int Id, string Key, string Name);

public sealed record UserListItemResponse(
    Guid Id,
    string Name,
    string Email,
    UserStatus Status,
    Guid CompanyId,
    string CompanyName,
    IReadOnlyList<string> Roles,
    DateTime? LastLoginAt,
    bool IsLockedOut);

public sealed record UserResponse(
    Guid Id,
    string Name,
    string Email,
    UserStatus Status,
    Guid CompanyId,
    string CompanyName,
    IReadOnlyList<UserRoleResponse> Roles,
    DateTime? LastLoginAt,
    DateTime? LockoutEndAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed class UserCreateRequestValidator : AbstractValidator<UserCreateRequest>
{
    public UserCreateRequestValidator()
    {
        RuleFor(x => x.Name).Required("Nome").MaxLen(User.NameMaxLength);
        RuleFor(x => x.Email).Required("E-mail").ValidEmail();
        RuleFor(x => x.Password).StrongPassword(x => x.Email);
        RuleFor(x => x.Status).IsInEnum().WithMessage("Situação inválida.");
        RuleFor(x => x.RoleIds).NotEmpty().WithMessage("Selecione pelo menos um papel de acesso.");
    }
}

public sealed class UserUpdateRequestValidator : AbstractValidator<UserUpdateRequest>
{
    public UserUpdateRequestValidator()
    {
        RuleFor(x => x.Name).Required("Nome").MaxLen(User.NameMaxLength);
        RuleFor(x => x.Email).Required("E-mail").ValidEmail();
        RuleFor(x => x.Status).IsInEnum().WithMessage("Situação inválida.");
        RuleFor(x => x.RoleIds).NotEmpty().WithMessage("Selecione pelo menos um papel de acesso.");
    }
}
