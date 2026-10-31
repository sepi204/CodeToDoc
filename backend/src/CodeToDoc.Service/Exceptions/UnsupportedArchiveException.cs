namespace CodeToDoc.Service.Exceptions;

public class UnsupportedArchiveException : Exception
{
    public UnsupportedArchiveException(string message) : base(message)
    {
    }
}
