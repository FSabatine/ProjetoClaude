namespace REC4.Application.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string entityName, object key)
        : base($"{entityName} com identificador '{key}' não foi encontrado(a).")
    {
    }
}
