namespace REC4.Application.Exceptions;

public class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException()
        : base("Login ou senha inválidos.")
    {
    }
}
