namespace REC4.Application.Common;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string hash, string providedPassword);
}
