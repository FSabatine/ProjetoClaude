namespace REC4.Domain.Enums;

// Classificacao descritiva do usuario. NUNCA deve ser usada para decisao de autorizacao -
// isso e responsabilidade exclusiva de Grupo + permissoes efetivas (ver REC4.Api/Authorization).
public enum TipoUsuario
{
    Administrador,
    Gestor,
    Operador
}
