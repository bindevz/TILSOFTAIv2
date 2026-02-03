using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace TILSOFTAI.Infrastructure.Resilience;

public static class TransientExceptionClassifier
{
    public static bool IsTransient(Exception ex)
    {
        return ex switch
        {
            HttpRequestException httpEx => IsTransientHttp(httpEx),
            SqlException sqlEx => IsTransientSql(sqlEx),
            TimeoutException => true,
            // Redis timeouts often come as TimeoutException or specific client exceptions.
            // StackExchange.Redis specific exceptions might need a package reference or reflection if not available directly.
            // For now, TimeoutException covers general timeouts.
            _ => false
        };
    }

    private static bool IsTransientHttp(HttpRequestException ex)
    {
        if (ex.StatusCode.HasValue)
        {
            int code = (int)ex.StatusCode.Value;
            // 408 Request Timeout
            // 429 Too Many Requests
            // 500 Internal Server Error
            // 502 Bad Gateway
            // 503 Service Unavailable
            // 504 Gateway Timeout
            return code == 408 || code == 429 || code >= 500;
        }
        
        // Network errors w/o status code are often transient
        return true; 
    }

    private static bool IsTransientSql(SqlException ex)
    {
        foreach (SqlError error in ex.Errors)
        {
            // SQL Error Codes for transient failures:
            // 1205: Deadlock
            // -2: Timeout
            // 233: Connection issue
            // 10053: Transport level error
            // 10054: Transport level error
            // 10060: Network error
            // 40613: Azure SQL transient
            if (error.Number == 1205 || 
                error.Number == -2 || 
                error.Number == 233 || 
                error.Number == 10053 || 
                error.Number == 10054 || 
                error.Number == 10060 || 
                error.Number == 40613)
            {
                return true;
            }
        }
        return false;
    }
}
