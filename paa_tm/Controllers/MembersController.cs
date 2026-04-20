using Dapper;
using Microsoft.AspNetCore.Mvc;
using paa_tm.Helpers;
using paa_tm.Models;

namespace paa_tm.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("3. Members")]
public class MembersController : ControllerBase
{
    private readonly SqlDbHelper _db;
    public MembersController(SqlDbHelper db) => _db = db;

    // GET ALL
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? name,
                                            [FromQuery] string? email,
                                            [FromQuery] int page = 1,
                                            [FromQuery] int limit = 10)
    {
        var where = new List<string> { "1=1" };
        var p = new DynamicParameters();

        if (!string.IsNullOrEmpty(name)) { where.Add("name ILIKE @Name"); p.Add("Name", $"%{name}%"); }
        if (!string.IsNullOrEmpty(email)) { where.Add("email ILIKE @Email"); p.Add("Email", $"%{email}%"); }

        var wc = string.Join(" AND ", where);
        p.Add("Limit", limit);
        p.Add("Offset", (page - 1) * limit);

        var total = await _db.ExecuteScalarDynamicAsync(
            $"SELECT COUNT(*) FROM members WHERE {wc}", p);

        var data = await _db.QueryDynamicAsync($@"
            SELECT id, name, email, phone, address,
                   created_at AS createdAt,
                   updated_at AS updatedAt
            FROM members
            WHERE {wc}
            ORDER BY created_at DESC
            LIMIT @Limit OFFSET @Offset", p);

        return Ok(ApiResponse<object>.Ok(data, "Data anggota berhasil diambil",
            new { total, page, limit, totalPages = (int)Math.Ceiling((double)total / limit) }));
    }

    // GET BY ID
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var data = await _db.QueryFirstOrDefaultAsync(@"
            SELECT id, name, email, phone, address,
                   created_at AS createdAt, updated_at AS updatedAt
            FROM members WHERE id = @Id", new { Id = id });

        if (data == null)
            return NotFound(ApiResponse<object>.Fail($"Anggota dengan ID {id} tidak ditemukan"));

        return Ok(ApiResponse<object>.Ok(data, "Data anggota berhasil diambil"));
    }

    // CREATE 
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMemberRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Email))
            return BadRequest(ApiResponse<object>.Fail("Validasi gagal", new[]
            {
                new { field = "name",  message = "Nama wajib diisi" },
                new { field = "email", message = "Email wajib diisi" }
            }));

        var emailUsed = await _db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM members WHERE email = @Email", new { req.Email });
        if (emailUsed > 0)
            return Conflict(ApiResponse<object>.Fail("Email sudah terdaftar"));

        var id = await _db.ExecuteScalarAsync<int>(@"
            INSERT INTO members (name, email, phone, address)
            VALUES (@Name, @Email, @Phone, @Address)
            RETURNING id",
            new { req.Name, req.Email, req.Phone, req.Address });

        var data = await _db.QueryFirstOrDefaultAsync(
            "SELECT id, name, email, phone, address, created_at AS createdAt, updated_at AS updatedAt FROM members WHERE id = @Id",
            new { Id = id });

        return CreatedAtAction(nameof(GetById), new { id },
            ApiResponse<object>.Ok(data!, "Anggota berhasil ditambahkan"));
    }

    // UPDATE
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateMemberRequest req)
    {
        var exists = await _db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM members WHERE id = @Id", new { Id = id });
        if (exists == 0)
            return NotFound(ApiResponse<object>.Fail($"Anggota dengan ID {id} tidak ditemukan"));

        if (!string.IsNullOrEmpty(req.Email))
        {
            var emailUsed = await _db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM members WHERE email = @Email AND id != @Id",
                new { req.Email, Id = id });
            if (emailUsed > 0)
                return Conflict(ApiResponse<object>.Fail("Email sudah digunakan anggota lain"));
        }

        await _db.ExecuteAsync(@"
            UPDATE members
            SET name       = COALESCE(@Name,    name),
                email      = COALESCE(@Email,   email),
                phone      = COALESCE(@Phone,   phone),
                address    = COALESCE(@Address, address),
                updated_at = NOW()
            WHERE id = @Id",
            new { req.Name, req.Email, req.Phone, req.Address, Id = id });

        var data = await _db.QueryFirstOrDefaultAsync(
            "SELECT id, name, email, phone, address, created_at AS createdAt, updated_at AS updatedAt FROM members WHERE id = @Id",
            new { Id = id });

        return Ok(ApiResponse<object>.Ok(data!, "Data anggota berhasil diperbarui"));
    }

    // DELETE 
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var exists = await _db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM members WHERE id = @Id", new { Id = id });
        if (exists == 0)
            return NotFound(ApiResponse<object>.Fail($"Anggota dengan ID {id} tidak ditemukan"));

        var pinjamAktif = await _db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM loans WHERE member_id = @Id AND status = 'borrowed'", new { Id = id });
        if (pinjamAktif > 0)
            return Conflict(ApiResponse<object>.Fail("Anggota masih memiliki pinjaman aktif"));

        await _db.ExecuteAsync("DELETE FROM members WHERE id = @Id", new { Id = id });
        return Ok(ApiResponse<object>.Ok(null!, $"Anggota dengan ID {id} berhasil dihapus"));
    }
}

public record CreateMemberRequest(string Name, string Email, string? Phone, string? Address);
public record UpdateMemberRequest(string? Name, string? Email, string? Phone, string? Address);