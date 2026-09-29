namespace REC4.Application.Common;

// Nomes de claim customizados usados no JWT. O handler de autorização de permissões
// (REC4.Api/Authorization) e o gerador de token (Infrastructure) precisam concordar
// exatamente nesses nomes.
public static class Rec4Claims
{
    public const string Login = "login";
    public const string Grupo = "grupo";
    public const string Permission = "perm";
}
