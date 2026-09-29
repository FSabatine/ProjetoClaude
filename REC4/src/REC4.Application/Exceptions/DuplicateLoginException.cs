namespace REC4.Application.Exceptions;

public class DuplicateLoginException : Exception
{
    public DuplicateLoginException(string login)
        : base($"Já existe um usuário cadastrado com o login '{login}'.")
    {
    }
}
