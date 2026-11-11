namespace Kontext.Kernel;

public class KontextException : Exception
{
    public KontextException()
    {
    }

    public KontextException(string message)
        : base(message)
    {
    }

    public KontextException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>The raw output behind <see cref="Exception.Message"/>, for a details view. Null when there is none.</summary>
    public virtual string? Details => null;
}
