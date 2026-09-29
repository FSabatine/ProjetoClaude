using Fleet.Application.Auth;
using Fleet.Application.Common;
using Fleet.Domain.Authorization;
using Fleet.Domain.Users;
using Fleet.Domain.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Users;

/// <summary>
/// Users are not tenant-filtered by EF (login needs them), so every query here goes through
/// <see cref="ScopedUsers"/>: own company only, unless the caller is a platform admin.
/// </summary>
public sealed class UserService(
    IFleetDbContext db,
    ICurrentUser currentUser,
    IPasswordHasher passwordHasher,
    IClock clock,
    IValidator<UserCreateRequest> createValidator,
    IValidator<UserUpdateRequest> updateValidator)
{
    private static readonly SortMap<User> Sorts = new SortMap<User>("name")
        .Add("name", u => u.Name)
        .Add("email", u => u.Email)
        .Add("status", u => u.Status)
        .Add("lastLoginAt", u => u.LastLoginAt)
        .Add("createdAt", u => u.CreatedAt);

    private bool IsPlatformAdmin => currentUser.HasPermission(Permissions.Companies.Manage);

    public async Task<PagedResult<UserListItemResponse>> ListAsync(UserListRequest request, CancellationToken ct)
    {
        var query = ScopedUsers();
        if (IsPlatformAdmin && request.CompanyId is { } companyId) query = query.Where(u => u.CompanyId == companyId);
        if (request.Status is { } status) query = query.Where(u => u.Status == status);
        if (request.SearchTerm is { } term)
        {
            var lower = term.ToLowerInvariant();
            query = query.Where(u => u.Name.Contains(term) || u.Email.Contains(lower));
        }

        var now = clock.UtcNow;
        return await Sorts.Apply(query, request.SortBy, request.SortDirection).ToPagedResultAsync(request, u =>
            new UserListItemResponse(
                u.Id, u.Name, u.Email, u.Status, u.CompanyId,
                u.Company.TradeName ?? u.Company.LegalName,
                u.UserRoles.Select(ur => ur.Role.Name).OrderBy(n => n).ToList(),
                u.LastLoginAt,
                u.LockoutEndAt != null && u.LockoutEndAt > now), ct);
    }

    public async Task<UserResponse> GetAsync(Guid id, CancellationToken ct) => ToResponse(await LoadAsync(id, ct));

    public async Task<UserResponse> CreateAsync(UserCreateRequest request, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);

        var companyId = request.CompanyId ?? currentUser.CompanyId!.Value;
        if (companyId != currentUser.CompanyId && !IsPlatformAdmin)
            throw new ForbiddenException("Você só pode criar usuários na sua própria empresa.");
        if (!await db.Companies.AnyAsync(c => c.Id == companyId, ct))
            throw ValidationErrors.ForField(nameof(UserCreateRequest.CompanyId), "Empresa não encontrada.");

        var email = EmailAddress.Normalize(request.Email);
        await EnsureEmailIsFreeAsync(email, exceptUserId: null, ct);
        var roles = await LoadAssignableRolesAsync(request.RoleIds, ct);

        var user = new User
        {
            CompanyId = companyId,
            Name = request.Name!.Trim(),
            Email = email,
            PasswordHash = passwordHasher.Hash(request.Password!),
            PasswordChangedAt = clock.UtcNow,
            Status = request.Status,
            UserRoles = roles.Select(r => new UserRole { RoleId = r.Id }).ToList(),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return await GetAsync(user.Id, ct);
    }

    public async Task<UserResponse> UpdateAsync(Guid id, UserUpdateRequest request, CancellationToken ct)
    {
        await updateValidator.ValidateAndThrowAsync(request, ct);
        var user = await LoadAsync(id, ct);
        await EnsureCanManageAsync(user, ct);

        var isSelf = user.Id == currentUser.UserId;
        var requestedRoleIds = request.RoleIds.Distinct().OrderBy(r => r).ToList();
        var currentRoleIds = user.UserRoles.Select(ur => ur.RoleId).OrderBy(r => r).ToList();
        var rolesChanged = !requestedRoleIds.SequenceEqual(currentRoleIds);

        if (isSelf && request.Status != UserStatus.Active)
            throw new BusinessRuleException("Você não pode inativar o seu próprio usuário. Peça a outro administrador.");
        if (isSelf && rolesChanged)
            throw new BusinessRuleException("Você não pode alterar os seus próprios papéis. Peça a outro administrador.");

        var email = EmailAddress.Normalize(request.Email);
        if (email != user.Email) await EnsureEmailIsFreeAsync(email, user.Id, ct);

        if (rolesChanged)
        {
            var roles = await LoadAssignableRolesAsync(requestedRoleIds, ct);
            user.UserRoles.RemoveAll(ur => !requestedRoleIds.Contains(ur.RoleId));
            foreach (var role in roles.Where(r => !currentRoleIds.Contains(r.Id)))
                user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        }

        user.Name = request.Name!.Trim();
        user.Email = email;
        if (user.Status != request.Status)
        {
            user.Status = request.Status;
            if (request.Status == UserStatus.Inactive)
                await AuthService.RevokeAllSessionsAsync(db, clock.UtcNow, user.Id, null, ct);
        }

        await db.SaveChangesAsync(ct);
        return await GetAsync(user.Id, ct);
    }

    /// <summary>Administrator sets a new password; also unlocks the account and ends all sessions.</summary>
    public async Task ResetPasswordAsync(Guid id, ResetPasswordRequest request, CancellationToken ct)
    {
        var user = await LoadAsync(id, ct);
        await EnsureCanManageAsync(user, ct);

        var violation = PasswordPolicy.GetViolation(request.NewPassword, user.Email);
        if (violation is not null) throw ValidationErrors.ForField(nameof(ResetPasswordRequest.NewPassword), violation);

        user.PasswordHash = passwordHasher.Hash(request.NewPassword!);
        user.PasswordChangedAt = clock.UtcNow;
        user.FailedLoginCount = 0;
        user.LockoutEndAt = null;
        await AuthService.RevokeAllSessionsAsync(db, clock.UtcNow, user.Id, null, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var user = await LoadAsync(id, ct);
        if (user.Id == currentUser.UserId)
            throw new BusinessRuleException("Você não pode excluir o seu próprio usuário.");
        await EnsureCanManageAsync(user, ct);

        await AuthService.RevokeAllSessionsAsync(db, clock.UtcNow, user.Id, null, ct);
        db.Users.Remove(user);
        await db.SaveChangesAsync(ct);
    }

    private IQueryable<User> ScopedUsers() =>
        IsPlatformAdmin ? db.Users : db.Users.Where(u => u.CompanyId == currentUser.CompanyId);

    private async Task<User> LoadAsync(Guid id, CancellationToken ct) =>
        await ScopedUsers()
            .Include(u => u.Company)
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .SingleOrDefaultAsync(u => u.Id == id, ct)
        ?? throw new NotFoundException("Usuário não encontrado. Ele pode ter sido excluído.");

    private async Task EnsureEmailIsFreeAsync(string email, Guid? exceptUserId, CancellationToken ct)
    {
        if (await db.Users.AnyAsync(u => u.Email == email && u.Id != exceptUserId, ct))
            throw new ConflictException("Já existe um usuário com este e-mail. Use outro e-mail.", "email");
    }

    /// <summary>Anti-escalation: roles may only grant permissions the caller already has.</summary>
    private async Task<List<Role>> LoadAssignableRolesAsync(IEnumerable<int> roleIds, CancellationToken ct)
    {
        var ids = roleIds.Distinct().ToList();
        var roles = await db.Roles
            .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .Where(r => ids.Contains(r.Id))
            .ToListAsync(ct);
        if (roles.Count != ids.Count)
            throw ValidationErrors.ForField(nameof(UserCreateRequest.RoleIds), "Um ou mais papéis selecionados não existem.");

        foreach (var role in roles)
        {
            if (role.RolePermissions.Any(rp => !currentUser.HasPermission(rp.Permission.Key)))
                throw new ForbiddenException(
                    $"Você não pode atribuir o papel \"{role.Name}\" porque ele concede permissões que você não possui.");
        }
        return roles;
    }

    /// <summary>Prevents e.g. a company administrator from editing or locking out a platform administrator.</summary>
    private async Task EnsureCanManageAsync(User target, CancellationToken ct)
    {
        var targetPermissions = await PermissionResolver.GetForUserAsync(db, target.Id, ct);
        if (targetPermissions.Any(p => !currentUser.HasPermission(p)))
            throw new ForbiddenException("Você não pode alterar este usuário porque ele tem permissões que você não possui.");
    }

    private static UserResponse ToResponse(User u) => new(
        u.Id, u.Name, u.Email, u.Status, u.CompanyId,
        u.Company.TradeName ?? u.Company.LegalName,
        u.UserRoles.Select(ur => new UserRoleResponse(ur.Role.Id, ur.Role.Key, ur.Role.Name)).OrderBy(r => r.Name).ToList(),
        u.LastLoginAt, u.LockoutEndAt, u.CreatedAt, u.UpdatedAt);
}
