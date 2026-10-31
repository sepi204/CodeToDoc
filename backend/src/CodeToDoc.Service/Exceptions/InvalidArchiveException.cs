namespace CodeToDoc.Service.Exceptions;

public class InvalidArchiveException : Exception
{
    public InvalidArchiveException(string message) : base(message)
    {
    }

    public InvalidArchiveException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
