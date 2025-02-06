
USE [master]
GO

CREATE DATABASE [Agency]
GO

USE [Agency]
GO

-- Drop tables in reverse order of creation due to foreign key dependencies
DROP TABLE IF EXISTS OfferingDetails;
DROP TABLE IF EXISTS Offerings;

-- create tables
CREATE TABLE OfferingDetails (
    Id INT PRIMARY KEY,
    Description NVARCHAR(255) NOT NULL
);

CREATE TABLE Offerings (
    Id INT PRIMARY KEY,
    Location NVARCHAR(255) NOT NULL,
    [ValidFrom] DATE NOT NULL,
    [ValidTo] DATE NOT NULL,
    OfferingDetailsId INT NOT NULL,
    FOREIGN KEY (OfferingDetailsId) REFERENCES OfferingDetails(Id)
);


-- create data
INSERT INTO OfferingDetails (Id, Description) VALUES 
    (1, 'Visit the beatiful location of London. No additional insurance required for europeans. Visit at your own pace.'),
    (2, 'Visit the beatiful location of Paris. No additional insurance required for europeans. Visit at your own pace.'),
    (3, 'Visit the beatiful location of New York. Medical insurance required for europeans. Visit at your own pace.'),
    (4, 'Visit the beatiful location of Ireland. This is an itinerant travel, so an additional insurance for the rental car is needed. Pack light, lots of movement!');

INSERT INTO Offerings (Id, Location, ValidFrom, ValidTo, OfferingDetailsId) VALUES 
    (1, 'London', '2024-01-01', '2024-3-31', 1),
    (2, 'London', '2024-08-01', '2024-12-31', 1),
    (3, 'Paris', '2024-01-01', '2024-12-31', 2),
    (4, 'New York', '2024-04-01', '2024-5-1', 3),
    (5, 'Ireland', '2024-06-01', '2024-08-30', 4)
