namespace paa_tm.Models;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public T? Data { get; set; }
    public object? Meta { get; set; }
    public object? Errors { get; set; }

    public static ApiResponse<T> Ok(T data, string msg = "Berhasil", object? meta = null)
        => new() { Success = true, Message = msg, Data = data, Meta = meta };

    public static ApiResponse<T> Fail(string msg, object? errors = null)
        => new() { Success = false, Message = msg, Errors = errors };
}