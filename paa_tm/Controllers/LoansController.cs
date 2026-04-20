using Dapper;
using Microsoft.AspNetCore.Mvc;
using paa_tm.Helpers;
using paa_tm.Models;

namespace paa_tm.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("4. Loans")]
public class LoansController : ControllerBase
{
    private readonly SqlDbHelper _db;
    public LoansController(SqlDbHelper db) => _db = db;

    // GET ALL
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status,
                                            [FromQuery] int? memberId,
                                            [FromQuery] int? bookId,
                                            [FromQuery] int page = 1,
                                            [FromQuery] int limit = 10)
    {
        var where = new List<string> { "1=1" };
        var p = new DynamicParameters();

        if (!string.IsNullOrEmpty(status)) { where.Add("l.status = @Status::loan_status"); p.Add("Status", status); }
        if (memberId.HasValue) { where.Add("l.member_id = @MemberId"); p.Add("MemberId", memberId.Value); }
        if (bookId.HasValue) { where.Add("l.book_id = @BookId"); p.Add("BookId", bookId.Value); }

        var wc = string.Join(" AND ", where);
        p.Add("Limit", limit);
        p.Add("Offset", (page - 1) * limit);

        var total = await _db.ExecuteScalarDynamicAsync(
            $"SELECT COUNT(*) FROM loans l WHERE {wc}", p);

        var data = await _db.QueryDynamicAsync($@"
            SELECT l.id,
                   l.book_id     AS bookId,
                   l.member_id   AS memberId,
                   l.loan_date   AS loanDate,
                   l.due_date    AS dueDate,
                   l.return_date AS returnDate,
                   l.status,
                   l.created_at  AS createdAt,
                   l.updated_at  AS updatedAt,
                   b.title       AS bookTitle,
                   m.name        AS memberName,
                   m.email       AS memberEmail
            FROM loans l
            JOIN books   b ON l.book_id   = b.id
            JOIN members m ON l.member_id = m.id
            WHERE {wc}
            ORDER BY l.created_at DESC
            LIMIT @Limit OFFSET @Offset", p);

        return Ok(ApiResponse<object>.Ok(data, "Data peminjaman berhasil diambil",
            new { total, page, limit, totalPages = (int)Math.Ceiling((double)total / limit) }));
    }

    // STATS
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var stats = new
        {
            totalBooks = await _db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM books"),
            totalMembers = await _db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM members"),
            totalLoans = await _db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM loans"),
            activeLoans = await _db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM loans WHERE status = 'borrowed'"),
            overdueLoans = await _db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM loans WHERE status = 'overdue'"),
            popularBooks = await _db.QueryAsync(@"
                SELECT b.title, COUNT(l.id) AS totalPinjam
                FROM loans l
                JOIN books b ON l.book_id = b.id
                GROUP BY b.id, b.title
                ORDER BY totalPinjam DESC
                LIMIT 5"),
        };
        return Ok(ApiResponse<object>.Ok(stats, "Statistik perpustakaan"));
    }

    // GET BY ID
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var data = await _db.QueryFirstOrDefaultAsync(@"
            SELECT l.id,
                   l.book_id     AS bookId,
                   l.member_id   AS memberId,
                   l.loan_date   AS loanDate,
                   l.due_date    AS dueDate,
                   l.return_date AS returnDate,
                   l.status,
                   l.created_at  AS createdAt,
                   l.updated_at  AS updatedAt,
                   b.title       AS bookTitle,
                   b.isbn,
                   m.name        AS memberName,
                   m.email       AS memberEmail,
                   m.phone       AS memberPhone
            FROM loans l
            JOIN books   b ON l.book_id   = b.id
            JOIN members m ON l.member_id = m.id
            WHERE l.id = @Id", new { Id = id });

        if (data == null)
            return NotFound(ApiResponse<object>.Fail($"Peminjaman dengan ID {id} tidak ditemukan"));

        return Ok(ApiResponse<object>.Ok(data, "Data peminjaman berhasil diambil"));
    }

    // CREATE
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLoanRequest req)
    {
        var book = await _db.QueryFirstOrDefaultAsync(
            "SELECT id, title, stock FROM books WHERE id = @Id", new { Id = req.BookId });
        if (book == null)
            return NotFound(ApiResponse<object>.Fail($"Buku dengan ID {req.BookId} tidak ditemukan"));
        if ((int)book.stock < 1)
            return Conflict(ApiResponse<object>.Fail($"Stok buku '{book.title}' habis"));

        var memberExists = await _db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM members WHERE id = @Id", new { Id = req.MemberId });
        if (memberExists == 0)
            return NotFound(ApiResponse<object>.Fail($"Anggota dengan ID {req.MemberId} tidak ditemukan"));

        if (req.DueDate <= req.LoanDate)
            return BadRequest(ApiResponse<object>.Fail("Tanggal jatuh tempo harus setelah tanggal pinjam"));

        var dupCheck = await _db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM loans WHERE book_id = @BookId AND member_id = @MemberId AND status = 'borrowed'",
            new { req.BookId, req.MemberId });
        if (dupCheck > 0)
            return Conflict(ApiResponse<object>.Fail("Anggota sudah meminjam buku ini dan belum mengembalikannya"));

        int newId = 0;
        await _db.ExecuteTransactionAsync(async (conn, tx) =>
        {
            newId = await conn.ExecuteScalarAsync<int>(@"
                INSERT INTO loans (book_id, member_id, loan_date, due_date, status)
                VALUES (@BookId, @MemberId, @LoanDate, @DueDate, 'borrowed')
                RETURNING id",
                new { req.BookId, req.MemberId, req.LoanDate, req.DueDate }, tx);

            await conn.ExecuteAsync(
                "UPDATE books SET stock = stock - 1, updated_at = NOW() WHERE id = @Id",
                new { Id = req.BookId }, tx);
        });

        var data = await _db.QueryFirstOrDefaultAsync(@"
            SELECT l.*, b.title AS bookTitle, m.name AS memberName
            FROM loans l
            JOIN books   b ON l.book_id   = b.id
            JOIN members m ON l.member_id = m.id
            WHERE l.id = @Id", new { Id = newId });

        return CreatedAtAction(nameof(GetById), new { id = newId },
            ApiResponse<object>.Ok(data!, "Peminjaman berhasil dicatat"));
    }

    // UPDATE 
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateLoanRequest req)
    {
        var loan = await _db.QueryFirstOrDefaultAsync(
            "SELECT id, book_id AS bookId, status FROM loans WHERE id = @Id", new { Id = id });
        if (loan == null)
            return NotFound(ApiResponse<object>.Fail($"Peminjaman dengan ID {id} tidak ditemukan"));

        var validStatuses = new[] { "borrowed", "returned", "overdue" };
        if (!string.IsNullOrEmpty(req.Status) && !validStatuses.Contains(req.Status))
            return BadRequest(ApiResponse<object>.Fail("Status tidak valid. Gunakan: borrowed, returned, overdue"));

        bool isReturning = req.Status == "returned" && (string)loan.status != "returned";

        await _db.ExecuteTransactionAsync(async (conn, tx) =>
        {
            await conn.ExecuteAsync(@"
                UPDATE loans
                SET return_date = COALESCE(@ReturnDate, return_date),
                    status      = COALESCE(@Status::loan_status, status),
                    updated_at  = NOW()
                WHERE id = @Id",
                new { req.ReturnDate, req.Status, Id = id }, tx);

            if (isReturning)
                await conn.ExecuteAsync(
                    "UPDATE books SET stock = stock + 1, updated_at = NOW() WHERE id = @Id",
                    new { Id = (int)loan.bookId }, tx);
        });

        var data = await _db.QueryFirstOrDefaultAsync(@"
            SELECT l.*, b.title AS bookTitle, m.name AS memberName
            FROM loans l
            JOIN books   b ON l.book_id   = b.id
            JOIN members m ON l.member_id = m.id
            WHERE l.id = @Id", new { Id = id });

        return Ok(ApiResponse<object>.Ok(data!, "Data peminjaman berhasil diperbarui"));
    }

    // DELETE
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var loan = await _db.QueryFirstOrDefaultAsync(
            "SELECT id, status FROM loans WHERE id = @Id", new { Id = id });
        if (loan == null)
            return NotFound(ApiResponse<object>.Fail($"Peminjaman dengan ID {id} tidak ditemukan"));

        if ((string)loan.status == "borrowed")
            return Conflict(ApiResponse<object>.Fail("Peminjaman aktif tidak bisa dihapus. Kembalikan buku terlebih dahulu"));

        await _db.ExecuteAsync("DELETE FROM loans WHERE id = @Id", new { Id = id });
        return Ok(ApiResponse<object>.Ok(null!, $"Peminjaman dengan ID {id} berhasil dihapus"));
    }
}

public record CreateLoanRequest(int BookId, int MemberId, DateOnly LoanDate, DateOnly DueDate);
public record UpdateLoanRequest(string? Status, DateOnly? ReturnDate);