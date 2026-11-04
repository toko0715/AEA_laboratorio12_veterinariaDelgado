-- Solo para SQL Server local; no ejecutar si VeterinariaDB ya tiene tablas.
IF DB_ID(N'VeterinariaDB') IS NULL
    EXEC(N'CREATE DATABASE VeterinariaDB');
GO
USE VeterinariaDB;
GO
-- Ejecutar una sola vez sobre una base de datos vacía.
-- En la nube, seleccionar primero la base asignada por el proveedor.
SET XACT_ABORT ON;
GO
BEGIN TRANSACTION;

CREATE TABLE dbo.Propietarios
(
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Propietarios PRIMARY KEY,
    Nombre NVARCHAR(100) NOT NULL,
    Documento NVARCHAR(20) NOT NULL,
    Telefono NVARCHAR(20) NOT NULL,
    CONSTRAINT UQ_Propietarios_Documento UNIQUE (Documento),
    CONSTRAINT CK_Propietarios_Nombre CHECK (LEN(LTRIM(RTRIM(Nombre))) > 0),
    CONSTRAINT CK_Propietarios_Documento CHECK (LEN(LTRIM(RTRIM(Documento))) > 0),
    CONSTRAINT CK_Propietarios_Telefono CHECK (LEN(LTRIM(RTRIM(Telefono))) > 0)
);

CREATE TABLE dbo.Mascotas
(
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Mascotas PRIMARY KEY,
    Nombre NVARCHAR(100) NOT NULL,
    Especie NVARCHAR(50) NOT NULL,
    Raza NVARCHAR(100) NULL,
    Edad INT NOT NULL,
    PesoKg DECIMAL(6,2) NOT NULL,
    PropietarioId INT NOT NULL,
    CONSTRAINT FK_Mascotas_Propietarios FOREIGN KEY (PropietarioId)
        REFERENCES dbo.Propietarios(Id) ON DELETE NO ACTION ON UPDATE NO ACTION,
    CONSTRAINT CK_Mascotas_Nombre CHECK (LEN(LTRIM(RTRIM(Nombre))) > 0),
    CONSTRAINT CK_Mascotas_Especie CHECK (LEN(LTRIM(RTRIM(Especie))) > 0),
    CONSTRAINT CK_Mascotas_Edad CHECK (Edad BETWEEN 0 AND 100),
    CONSTRAINT CK_Mascotas_PesoKg CHECK (PesoKg BETWEEN 0.01 AND 999.99)
);

CREATE INDEX IX_Mascotas_PropietarioId ON dbo.Mascotas(PropietarioId);

INSERT INTO dbo.Propietarios (Nombre, Documento, Telefono) VALUES
(N'Ana Torres', N'10000001', N'900000001'),
(N'Luis Mendoza', N'10000002', N'900000002'),
(N'Carla Ramos', N'10000003', N'900000003');

INSERT INTO dbo.Mascotas (Nombre, Especie, Raza, Edad, PesoKg, PropietarioId) VALUES
(N'Rocky', N'Perro', N'Labrador', 4, 28.50,
 (SELECT Id FROM dbo.Propietarios WHERE Documento = N'10000001')),
(N'Mía', N'Gato', N'Siamés', 2, 4.20,
 (SELECT Id FROM dbo.Propietarios WHERE Documento = N'10000001')),
(N'Toby', N'Perro', N'Mestizo', 3, 12.80,
 (SELECT Id FROM dbo.Propietarios WHERE Documento = N'10000002')),
(N'Luna', N'Gato', N'Mestizo', 1, 3.10,
 (SELECT Id FROM dbo.Propietarios WHERE Documento = N'10000002')),
(N'Coco', N'Conejo', NULL, 2, 1.80,
 (SELECT Id FROM dbo.Propietarios WHERE Documento = N'10000003'));

COMMIT TRANSACTION;
GO
SELECT Id, Nombre, Documento, Telefono FROM dbo.Propietarios;
SELECT m.Id, m.Nombre, m.Especie, m.Edad, m.PesoKg, m.PropietarioId,
       p.Nombre AS Propietario
FROM dbo.Mascotas m
JOIN dbo.Propietarios p ON p.Id = m.PropietarioId;
GO
