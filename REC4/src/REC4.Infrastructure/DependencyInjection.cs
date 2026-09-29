using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using REC4.Application.Auth;
using REC4.Application.Common;
using REC4.Application.Escritorios;
using REC4.Application.Grupos;
using REC4.Application.Pessoas;
using REC4.Application.Permissoes;
using REC4.Application.Usuarios;
using REC4.Infrastructure.Data;
using REC4.Infrastructure.Security;

namespace REC4.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddRec4Infrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<Rec4DbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Rec4Database")));

        services.AddScoped<IRec4DbContext>(sp => sp.GetRequiredService<Rec4DbContext>());

        services.AddScoped<IPessoaService, PessoaService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUsuarioService, UsuarioService>();
        services.AddScoped<IGrupoService, GrupoService>();
        services.AddScoped<IPermissaoService, PermissaoService>();
        services.AddScoped<IEscritorioService, EscritorioService>();

        services.AddSingleton<IPasswordHasher, Rec4PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        return services;
    }
}
