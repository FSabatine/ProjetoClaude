namespace REC4.Application.Exceptions;

public class DuplicateGroupNameException : Exception
{
    public DuplicateGroupNameException(string nome)
        : base($"Já existe um grupo com o nome '{nome}'.")
    {
    }
}
