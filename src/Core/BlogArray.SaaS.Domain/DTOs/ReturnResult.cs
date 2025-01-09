namespace BlogArray.SaaS.Domain.DTOs;

public class ReturnResult<T> : ReturnResult
{
    public T Result { get; set; }
}

public class ReturnResult
{
    public bool Status { get; set; } = false;
    public string Message { get; set; }
}

