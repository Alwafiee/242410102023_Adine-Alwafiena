**Library Management System API**

**Deskripsi Project**
Library Management System API adalah RESTful API yang digunakan untuk mengelola sistem perpustakaan. API ini mencakup pengolahan data utama seperti Books (Buku), Authors (Penulis), Members (Anggota), dan Loans (Peminjaman).

**Alasan Pemilihan Domain**
Domain perpustakaan dipilih karena memiliki relasi antar tabel (relational database), cocok untuk implementasi CRUD (Create, Read, Update, Delete), dan mewakili sistem nyata yang sering digunakan.

**Teknologi yang Digunakan**
Bahasa yang digunakan adalah C#.
Framework yang digunakan adalah ASP.NET Core Web API.
Database yang digunakan adalah PostgreSQL.
Untuk akses database digunakan Dapper.
Untuk testing API digunakan Swagger (OpenAPI).

**Instalasi dan Menjalankan Project**
1.	Clone repository dari GitHub
git clone https://github.com/Alwafiee/242410102023_Adine-Alwafiena.git 
2.	Setup database
Pastikan PostgreSQL sudah terinstall, lalu buat database dengan nama:
CREATE DATABASE paa_tm; 
3.	Konfigurasi connection string
Buka file appsettings.json lalu isi:
"ConnectionStrings": {
"WebApiDatabase": "Host=localhost;Port=5432;Database=paa_tm;Username=postgres;Password=1234"
} 
4.	Jalankan project
dotnet run 
5.	Buka Swagger di browser

**Cara Import Database**
Gunakan file SQL (misalnya database.sql), lalu jalankan:
psql -U postgres -d paa_tm -f database.sql
Atau bisa juga import manual melalui pgAdmin.

**Daftar Endpoint API**
Authors
-	GET /api/Authors → Mengambil semua data penulis
-	POST /api/Authors → Menambahkan penulis baru
-	GET /api/Authors/{id} → Mengambil detail penulis
-	PUT /api/Authors/{id} → Mengupdate data penulis
-	DELETE /api/Authors/{id} → Menghapus penulis
Books
-	GET /api/Books → Mengambil semua data buku
-	POST /api/Books → Menambahkan buku
-	GET /api/Books/{id} → Detail buku
-	PUT /api/Books/{id} → Update buku
-	DELETE /api/Books/{id} → Hapus buku
Members
-	GET /api/Members → Mengambil semua anggota
-	POST /api/Members → Menambahkan anggota
-	GET /api/Members/{id} → Detail anggota
-	PUT /api/Members/{id} → Update anggota
-	DELETE /api/Members/{id} → Hapus anggota
Loans
-	GET /api/Loans → Mengambil semua data peminjaman
-	POST /api/Loans → Menambahkan peminjaman
-	GET /api/Loans/{id} → Detail peminjaman
-	PUT /api/Loans/{id} → Update peminjaman
-	DELETE /api/Loans/{id} → Hapus peminjaman
-	GET /api/Loans/stats → Melihat statistik peminjaman

**Contoh Request (POST Book)**
{
"authorId": 1,
"title": "Clean Code",
"isbn": "12345",
"genre": "Programming",
"publishYear": 2020,
"stock": 10
}

**Contoh Response**
{
"success": true,
"message": "Buku berhasil ditambahkan",
"data": {
"id": 1,
"title": "Clean Code"
}
}

**Skenario Error**
400 → Validasi gagal
404 → Data tidak ditemukan
409 → Data duplikat
500 → Server error

**Alur Sistem**
Client → Controller → SqlDbHelper → Database → Controller → Response

**Link Video Presentasi**
Silakan tambahkan link video presentasi di sini, contoh:
https://youtu.be/KrltxZf99do
