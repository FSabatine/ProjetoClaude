namespace REC4.Application.Exceptions;

public class DuplicateDocumentException : Exception
{
    public DuplicateDocumentException(string cpfCnpj)
        : base($"Já existe uma pessoa ativa cadastrada com o documento '{cpfCnpj}'.")
    {
    }
}
