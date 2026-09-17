namespace HrPlatform.Api.Middleware;

// lets a domain exception carry its own HTTP mapping so ExceptionHandlingMiddleware
// doesn't need a switch case (and a reference to the throwing module) per exception type
public interface IApiException
{
    int StatusCode { get; }
    string UserMessage { get; }
}
