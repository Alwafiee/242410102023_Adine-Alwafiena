using Dapper;                  
using Microsoft.AspNetCore.Mvc;
using paa_tm.Helpers;
using paa_tm.Models;

namespace paa_tm.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("1. Authors")]
public class AuthorsController : ControllerBase
{
    private readonly SqlDbHelper _db;
    public AuthorsController(SqlDbHelper db) => _db = db;

    // GET
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? name,
                                            [FromQuery] string? nationality,
                                            [FromQuery] int page = 1,
                                            [FromQuery] int limit = 10)
    {
        var where = new List<string> { "1=1" };
        var p = new DynamicParameters();

        if (!string.IsNullOrEmpty(name))
        {
            where.Add("name ILIKE @Name");          
            p.Add("Name", $"%{name}%");
        }
        if (!string.IsNullOrEmpty(nationality))
        {
            where.Add("nationality = @Nationality");
            p.Add("Nationality", nationality);
        }

        var wc = string.Join(" AND ", where);
        p.Add("Limit", limit);
        p.Add("Offset", (page - 1) * limit);

        var total = await _db.ExecuteScalarDynamicAsync(
            $"SELECT COUNT(*) FROM authors WHERE {wc}", p);

        var data = await _db.QueryDynamicAsync($@"
            SELECT id, name, nationality,
                   birth_year  AS birthYear,
                   bio,
                   created_at  AS createdAt,
                   updated_at  AS updatedAt
            FROM authors
            WHERE {wc}
            ORDER BY created_at DESC
            LIMIT @Limit OFFSET @Offset", p);

        return Ok(ApiResponse<object>.Ok(data, "Data penulis berhasil diambil",
            new { total, page, limit, totalPages = (int)Math.Ceiling((double)total / limit) }));
    }

    // GET BY ID
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var data = await _db.QueryFirstOrDefaultAsync(@"
            SELECT id, name, nationality,
                   birth_year AS birthYear, bio,
                   created_at AS createdAt, updated_at AS updatedAt
            FROM authors WHERE id = @Id", new { Id = id });

        if (data == null)
            return NotFound(ApiResponse<object>.Fail($"Penulis dengan ID {id} tidak ditemukan"));

        return Ok(ApiResponse<object>.Ok(data, "Data penulis berhasil diambil"));
    }

    // CREATE
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAuthorRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(ApiResponse<object>.Fail("Validasi gagal",
                new[] { new { field = "name", message = "Nama wajib diisi" } }));

        var id = await _db.ExecuteScalarAsync<int>(@"
            INSERT INTO authors (name, nationality, birth_year, bio)
            VALUES (@Name, @Nationality, @BirthYear, @Bio)
            RETURNING id",
            new { req.Name, req.Nationality, BirthYear = req.BirthYear, req.Bio });

        var data = await _db.QueryFirstOrDefaultAsync(
            "SELECT id, name, nationality, birth_year AS birthYear, bio, created_at AS createdAt, updated_at AS updatedAt FROM authors WHERE id = @Id",
            new { Id = id });

        return CreatedAtAction(nameof(GetById), new { id },
            ApiResponse<object>.Ok(data!, "Penulis berhasil ditambahkan"));
    }

    // UPDATE
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateAuthorRequest req)
    {
        var exists = await _db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM authors WHERE id = @Id", new { Id = id });
        if (exists == 0)
            return NotFound(ApiResponse<object>.Fail($"Penulis dengan ID {id} tidak ditemukan"));

        await _db.ExecuteAsync(@"
            UPDATE authors
            SET name        = COALESCE(@Name,        name),
                nationality = COALESCE(@Nationality, nationality),
                birth_year  = COALESCE(@BirthYear,   birth_year),
                bio         = COALESCE(@Bio,          bio),
                updated_at  = NOW()
            WHERE id = @Id",
            new { req.Name, req.Nationality, BirthYear = req.BirthYear, req.Bio, Id = id });

        var data = await _db.QueryFirstOrDefaultAsync(
            "SELECT id, name, nationality, birth_year AS birthYear, bio, created_at AS createdAt, updated_at AS updatedAt FROM authors WHERE id = @Id",
            new { Id = id });

        return Ok(ApiResponse<object>.Ok(data!, "Data penulis berhasil diperbarui"));
    }

    // DELETE
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var exists = await _db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM authors WHERE id = @Id", new { Id = id });
        if (exists == 0)
            return NotFound(ApiResponse<object>.Fail($"Penulis dengan ID {id} tidak ditemukan"));

        await _db.ExecuteAsync("DELETE FROM authors WHERE id = @Id", new { Id = id });
        return Ok(ApiResponse<object>.Ok(null!, $"Penulis dengan ID {id} berhasil dihapus"));
    }
}

public record CreateAuthorRequest(string Name, string? Nationality, int? BirthYear, string? Bio);
public record UpdateAuthorRequest(string? Name, string? Nationality, int? BirthYear, string? Bio);