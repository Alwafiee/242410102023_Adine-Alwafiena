using Dapper;
using Microsoft.AspNetCore.Mvc;
using paa_tm.Helpers;
using paa_tm.Models;

namespace paa_tm.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("2. Books")]
public class BooksController : ControllerBase
{
    private readonly SqlDbHelper _db;
    public BooksController(SqlDbHelper db) => _db = db;

    // GET ALL
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? title,
                                            [FromQuery] string? genre,
                                            [FromQuery] int? authorId,
                                            [FromQuery] int page = 1,
                                            [FromQuery] int limit = 10)
    {
        var where = new List<string> { "1=1" };
        var p = new DynamicParameters();

        if (!string.IsNullOrEmpty(title)) { where.Add("b.title ILIKE @Title"); p.Add("Title", $"%{title}%"); }
        if (!string.IsNullOrEmpty(genre)) { where.Add("b.genre = @Genre"); p.Add("Genre", genre); }
        if (authorId.HasValue) { where.Add("b.author_id = @AuthorId"); p.Add("AuthorId", authorId.Value); }

        var wc = string.Join(" AND ", where);
        p.Add("Limit", limit);
        p.Add("Offset", (page - 1) * limit);

        var total = await _db.ExecuteScalarDynamicAsync(
            $"SELECT COUNT(*) FROM books b WHERE {wc}", p);

        var data = await _db.QueryDynamicAsync($@"
            SELECT b.id, b.author_id AS authorId, b.title, b.isbn, b.genre,
                   b.publish_year AS publishYear, b.stock,
                   b.created_at   AS createdAt,
                   b.updated_at   AS updatedAt,
                   a.name         AS authorName
            FROM books b
            JOIN authors a ON b.author_id = a.id
            WHERE {wc}
            ORDER BY b.created_at DESC
            LIMIT @Limit OFFSET @Offset", p);

        return Ok(ApiResponse<object>.Ok(data, "Data buku berhasil diambil",
            new { total, page, limit, totalPages = (int)Math.Ceiling((double)total / limit) }));
    }

    // GET BY ID
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var data = await _db.QueryFirstOrDefaultAsync(@"
            SELECT b.id, b.author_id AS authorId, b.title, b.isbn, b.genre,
                   b.publish_year    AS publishYear, b.stock,
                   b.created_at      AS createdAt,
                   b.updated_at      AS updatedAt,
                   a.name            AS authorName,
                   a.nationality     AS authorNationality
            FROM books b
            JOIN authors a ON b.author_id = a.id
            WHERE b.id = @Id", new { Id = id });

        if (data == null)
            return NotFound(ApiResponse<object>.Fail($"Buku dengan ID {id} tidak ditemukan"));

        return Ok(ApiResponse<object>.Ok(data, "Data buku berhasil diambil"));
    }

    // CREATE
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBookRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            return BadRequest(ApiResponse<object>.Fail("Validasi gagal",
                new[] { new { field = "title", message = "Judul wajib diisi" } }));

        var authorExists = await _db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM authors WHERE id = @Id", new { Id = req.AuthorId });
        if (authorExists == 0)
            return NotFound(ApiResponse<object>.Fail($"Penulis dengan ID {req.AuthorId} tidak ditemukan"));

        if (!string.IsNullOrEmpty(req.Isbn))
        {
            var isbnUsed = await _db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM books WHERE isbn = @Isbn", new { req.Isbn });
            if (isbnUsed > 0)
                return Conflict(ApiResponse<object>.Fail("ISBN sudah digunakan buku lain"));
        }

        var id = await _db.ExecuteScalarAsync<int>(@"
            INSERT INTO books (author_id, title, isbn, genre, publish_year, stock)
            VALUES (@AuthorId, @Title, @Isbn, @Genre, @PublishYear, @Stock)
            RETURNING id",
            new { req.AuthorId, req.Title, req.Isbn, req.Genre, req.PublishYear, Stock = req.Stock ?? 0 });

        var data = await _db.QueryFirstOrDefaultAsync(
            "SELECT b.*, a.name AS authorName FROM books b JOIN authors a ON b.author_id = a.id WHERE b.id = @Id",
            new { Id = id });

        return CreatedAtAction(nameof(GetById), new { id },
            ApiResponse<object>.Ok(data!, "Buku berhasil ditambahkan"));
    }

    // UPDATE 
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateBookRequest req)
    {
        var exists = await _db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM books WHERE id = @Id", new { Id = id });
        if (exists == 0)
            return NotFound(ApiResponse<object>.Fail($"Buku dengan ID {id} tidak ditemukan"));

        if (!string.IsNullOrEmpty(req.Isbn))
        {
            var isbnUsed = await _db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM books WHERE isbn = @Isbn AND id != @Id",
                new { req.Isbn, Id = id });
            if (isbnUsed > 0)
                return Conflict(ApiResponse<object>.Fail("ISBN sudah digunakan buku lain"));
        }

        await _db.ExecuteAsync(@"
            UPDATE books
            SET author_id    = COALESCE(@AuthorId,    author_id),
                title        = COALESCE(@Title,        title),
                isbn         = COALESCE(@Isbn,         isbn),
                genre        = COALESCE(@Genre,        genre),
                publish_year = COALESCE(@PublishYear,  publish_year),
                stock        = COALESCE(@Stock,        stock),
                updated_at   = NOW()
            WHERE id = @Id",
            new { req.AuthorId, req.Title, req.Isbn, req.Genre, req.PublishYear, req.Stock, Id = id });

        var data = await _db.QueryFirstOrDefaultAsync(
            "SELECT b.*, a.name AS authorName FROM books b JOIN authors a ON b.author_id = a.id WHERE b.id = @Id",
            new { Id = id });

        return Ok(ApiResponse<object>.Ok(data!, "Data buku berhasil diperbarui"));
    }

    // DELETE 
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var exists = await _db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM books WHERE id = @Id", new { Id = id });
        if (exists == 0)
            return NotFound(ApiResponse<object>.Fail($"Buku dengan ID {id} tidak ditemukan"));

        var dipinjam = await _db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM loans WHERE book_id = @Id AND status = 'borrowed'", new { Id = id });
        if (dipinjam > 0)
            return Conflict(ApiResponse<object>.Fail("Buku tidak dapat dihapus karena sedang dipinjam"));

        await _db.ExecuteAsync("DELETE FROM books WHERE id = @Id", new { Id = id });
        return Ok(ApiResponse<object>.Ok(null!, $"Buku dengan ID {id} berhasil dihapus"));
    }
}

public record CreateBookRequest(int AuthorId, string Title, string? Isbn, string? Genre, int? PublishYear, int? Stock);
public record UpdateBookRequest(int? AuthorId, string? Title, string? Isbn, string? Genre, int? PublishYear, int? Stock);