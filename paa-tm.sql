CREATE DATABASE library_db;

DO $$ BEGIN
    CREATE TYPE loan_status AS ENUM ('borrowed', 'returned', 'overdue');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;


--Books
CREATE TABLE authors (
    id          SERIAL PRIMARY KEY,              
    name        VARCHAR(150) NOT NULL,
    nationality VARCHAR(100),
    birth_year  SMALLINT,
    bio         TEXT,
    created_at  TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_authors_name ON authors (name);


--Books
CREATE TABLE IF NOT EXISTS books (
    id           SERIAL PRIMARY KEY,
    author_id    INT NOT NULL,
    title        VARCHAR(255) NOT NULL,
    isbn         VARCHAR(20) UNIQUE,
    genre        VARCHAR(100),
    publish_year SMALLINT,
    stock        INT NOT NULL DEFAULT 0 CHECK (stock >= 0),
    created_at   TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at   TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_books_author FOREIGN KEY (author_id) REFERENCES authors(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_books_title  ON books (title);
CREATE INDEX IF NOT EXISTS idx_books_genre  ON books (genre);
CREATE INDEX IF NOT EXISTS idx_books_author ON books (author_id);


--Members
CREATE TABLE IF NOT EXISTS members (
    id         SERIAL PRIMARY KEY,
    name       VARCHAR(150) NOT NULL,
    email      VARCHAR(150) NOT NULL UNIQUE,
    phone      VARCHAR(20),
    address    TEXT,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_members_email ON members (email);
CREATE INDEX IF NOT EXISTS idx_members_name  ON members (name);


--Loans
CREATE TABLE IF NOT EXISTS loans (
    id          SERIAL PRIMARY KEY,
    book_id     INT NOT NULL,
    member_id   INT NOT NULL,
    loan_date   DATE NOT NULL,
    due_date    DATE NOT NULL,
    return_date DATE,
    status      loan_status NOT NULL DEFAULT 'borrowed',  
    created_at  TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_loans_book   FOREIGN KEY (book_id)   REFERENCES books(id)   ON DELETE CASCADE,
    CONSTRAINT fk_loans_member FOREIGN KEY (member_id) REFERENCES members(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_loans_status   ON loans (status);
CREATE INDEX IF NOT EXISTS idx_loans_book     ON loans (book_id);
CREATE INDEX IF NOT EXISTS idx_loans_member   ON loans (member_id);
CREATE INDEX IF NOT EXISTS idx_loans_due_date ON loans (due_date);


--Trigger
CREATE OR REPLACE FUNCTION update_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE TRIGGER trg_authors_updated_at
    BEFORE UPDATE ON authors
    FOR EACH ROW EXECUTE FUNCTION update_updated_at();

CREATE OR REPLACE TRIGGER trg_books_updated_at
    BEFORE UPDATE ON books
    FOR EACH ROW EXECUTE FUNCTION update_updated_at();

CREATE OR REPLACE TRIGGER trg_members_updated_at
    BEFORE UPDATE ON members
    FOR EACH ROW EXECUTE FUNCTION update_updated_at();

CREATE OR REPLACE TRIGGER trg_loans_updated_at
    BEFORE UPDATE ON loans
    FOR EACH ROW EXECUTE FUNCTION update_updated_at();


--Authors
INSERT INTO authors (name, nationality, birth_year, bio) VALUES
('Andrea Hirata',         'Indonesia',      1967, 'Novelis Indonesia terkenal, penulis Laskar Pelangi.'),
('Pramoedya Ananta Toer', 'Indonesia',      1925, 'Sastrawan besar Indonesia, penulis Tetralogi Buru.'),
('Tere Liye',             'Indonesia',      1979, 'Penulis fiksi populer Indonesia dengan banyak karya bestseller.'),
('J.K. Rowling',          'United Kingdom', 1965, 'Penulis seri Harry Potter yang mendunia.'),
('Haruki Murakami',       'Japan',          1949, 'Novelis Jepang berlevel internasional.'),
('Raditya Dika',          'Indonesia',      1984, 'Penulis dan komedian Indonesia.'),
('Dee Lestari',           'Indonesia',      1976, 'Novelis dan musisi Indonesia, penulis Seri Supernova.');

--Books
INSERT INTO books (author_id, title, isbn, genre, publish_year, stock) VALUES
(1, 'Laskar Pelangi',                                 '978-979-461-235-7', 'Fiksi',         2005, 5),
(1, 'Sang Pemimpi',                                   '978-979-461-256-2', 'Fiksi',         2006, 3),
(2, 'Bumi Manusia',                                   '978-979-407-155-2', 'Fiksi Sejarah', 1980, 4),
(3, 'Hujan',                                          '978-602-03-3015-4', 'Fiksi Ilmiah',  2016, 6),
(3, 'Hafalan Shalat Delisa',                          '978-979-024-957-8', 'Fiksi Religi',  2005, 2),
(4, 'Harry Potter and the Sorcerer''s Stone',         '978-0-439-70818-8', 'Fantasi',       1997, 7),
(5, 'Norwegian Wood',                                 '978-0-375-70427-7', 'Fiksi',         1987, 3),
(7, 'Supernova: Ksatria, Puteri, dan Bintang Jatuh', '978-979-782-071-5', 'Fiksi Ilmiah',  2001, 4);

--Members
INSERT INTO members (name, email, phone, address) VALUES
('Budi Santoso',  'budi.santoso@email.com',  '081234567890', 'Jl. Merdeka No.1, Surabaya'),
('Siti Rahayu',   'siti.rahayu@email.com',   '082345678901', 'Jl. Pahlawan No.5, Malang'),
('Ahmad Fauzi',   'ahmad.fauzi@email.com',   '083456789012', 'Jl. Gajah Mada No.10, Jakarta'),
('Dewi Kusuma',   'dewi.kusuma@email.com',   '084567890123', 'Jl. Sudirman No.22, Bandung'),
('Reza Pratama',  'reza.pratama@email.com',  '085678901234', 'Jl. Diponegoro No.8, Yogyakarta'),
('Lina Marlina',  'lina.marlina@email.com',  '086789012345', 'Jl. Ahmad Yani No.15, Semarang');

--Loans
INSERT INTO loans (book_id, member_id, loan_date, due_date, return_date, status) VALUES
(1, 1, '2025-04-01', '2025-04-15', '2025-04-13', 'returned'),
(2, 2, '2025-04-05', '2025-04-19', NULL,          'borrowed'),
(3, 3, '2025-03-20', '2025-04-03', NULL,          'overdue'),
(4, 4, '2025-04-10', '2025-04-24', NULL,          'borrowed'),
(5, 5, '2025-04-12', '2025-04-26', NULL,          'borrowed'),
(6, 1, '2025-04-08', '2025-04-22', '2025-04-20', 'returned'),
(7, 6, '2025-03-25', '2025-04-08', NULL,          'overdue');