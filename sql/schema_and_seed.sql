-- PawMatch schema — aligned with team MySQL DDL (Apr 2025+).
-- Quote `User` everywhere in SQL (reserved word in MySQL).
-- Run: mysql -u ... -p < sql/schema_and_seed.sql

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

DROP DATABASE IF EXISTS pawmatch;
CREATE DATABASE pawmatch CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE pawmatch;

CREATE TABLE Shelter (
    ShelterID INT PRIMARY KEY AUTO_INCREMENT,
    ShelterName VARCHAR(100) NOT NULL,
    ShelterAddress VARCHAR(255) NOT NULL
) ENGINE=InnoDB;

CREATE TABLE `User` (
    UserID INT PRIMARY KEY AUTO_INCREMENT,
    UserName VARCHAR(100) NOT NULL,
    UserEmail VARCHAR(100) NOT NULL UNIQUE,
    Password VARCHAR(255) NOT NULL,
    TypePreference VARCHAR(50) NULL
) ENGINE=InnoDB;

CREATE TABLE Employee (
    EmployeeID INT PRIMARY KEY AUTO_INCREMENT,
    EmployeeName VARCHAR(100) NOT NULL,
    Password VARCHAR(255) NOT NULL,
    ShelterID INT NULL,
    CONSTRAINT fk_employee_shelter FOREIGN KEY (ShelterID) REFERENCES Shelter (ShelterID)
) ENGINE=InnoDB;

CREATE TABLE Pet (
    PetID INT PRIMARY KEY AUTO_INCREMENT,
    PetName VARCHAR(100) NOT NULL,
    PetBreed VARCHAR(100) NULL,
    PetType ENUM('Cat', 'Dog') NOT NULL,
    ShelterID INT NULL,
    EmployeeID INT NULL,
    CONSTRAINT fk_pet_shelter FOREIGN KEY (ShelterID) REFERENCES Shelter (ShelterID),
    CONSTRAINT fk_pet_employee FOREIGN KEY (EmployeeID) REFERENCES Employee (EmployeeID)
) ENGINE=InnoDB;

CREATE TABLE AdoptionApplication (
    UserID INT NOT NULL,
    PetID INT NOT NULL,
    IsAdopted BOOLEAN NOT NULL DEFAULT FALSE,
    PRIMARY KEY (UserID, PetID),
    CONSTRAINT fk_app_user FOREIGN KEY (UserID) REFERENCES `User` (UserID),
    CONSTRAINT fk_app_pet FOREIGN KEY (PetID) REFERENCES Pet (PetID)
) ENGINE=InnoDB;

SET FOREIGN_KEY_CHECKS = 1;

INSERT INTO Shelter (ShelterName, ShelterAddress) VALUES
  ('River City Rescue', '100 Main St, Columbus, OH'),
  ('Northside Shelter', '250 Lake Ave, Cleveland, OH'),
  ('Small Paws Haven', '9 Elm Rd, Cincinnati, OH');

INSERT INTO Employee (EmployeeName, Password, ShelterID) VALUES
  ('Jamie Lee', 'emp-hash-1', 1),
  ('Ravi Patel', 'emp-hash-2', 2),
  ('Sam Rivera', 'emp-hash-3', 3);

INSERT INTO `User` (UserName, UserEmail, Password, TypePreference) VALUES
  ('Alex Jones', 'alex.j@example.com', 'Pass1234', 'Dog'),
  ('Bri Kim', 'brianna.k@example.com', 'Pass1234', 'Cat'),
  ('Carlos M', 'carlos.m@example.com', 'Pass1234', NULL);

-- PetType must be 'Cat' or 'Dog' only (ENUM).
INSERT INTO Pet (PetName, PetBreed, PetType, ShelterID, EmployeeID) VALUES
  ('Milo', 'Lab mix', 'Dog', 1, 1),
  ('Luna', 'Domestic shorthair', 'Cat', 1, 1),
  ('Bruno', 'Pit bull mix', 'Dog', 2, 2),
  ('Nala', 'Siamese mix', 'Cat', 2, 2),
  ('Rex', 'German Shepherd', 'Dog', 3, 3),
  ('Mittens', 'Tabby', 'Cat', 1, 1),
  ('Shadow', 'Domestic longhair', 'Cat', 2, 2),
  ('Bailey', 'Beagle', 'Dog', 2, 2),
  ('Duke', 'Boxer', 'Dog', 3, 3),
  ('Cleo', 'Calico', 'Cat', 1, 1);

INSERT INTO AdoptionApplication (UserID, PetID, IsAdopted) VALUES
  (1, 5, TRUE),
  (2, 6, TRUE),
  (1, 1, FALSE),
  (2, 2, FALSE),
  (3, 3, FALSE);
