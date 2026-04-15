-- =============================================================================
-- Task 2: Ten analytical queries — aligned with team schema:
--   Shelter(ShelterID, ShelterName, ShelterAddress)
--   User(UserID, UserName, UserEmail, Password VARCHAR(255), TypePreference)  -- quote `User`
--   Employee(EmployeeID, EmployeeName, Password, ShelterID)
--   Pet(PetID, PetName, PetBreed, PetType ENUM('Cat','Dog') NOT NULL, ShelterID, EmployeeID)
--   AdoptionApplication(UserID, PetID, IsAdopted)  PK (UserID, PetID)
-- =============================================================================

-- 1) Pet demand: pets with the most application rows
SELECT
  p.PetID,
  p.PetName,
  p.PetType,
  p.PetBreed,
  s.ShelterName,
  COUNT(a.UserID) AS application_count
FROM Pet p
LEFT JOIN Shelter s ON s.ShelterID = p.ShelterID
LEFT JOIN AdoptionApplication a ON a.PetID = p.PetID
GROUP BY p.PetID, p.PetName, p.PetType, p.PetBreed, s.ShelterName
ORDER BY application_count DESC
LIMIT 20;

-- 2) Demand by species / type (PetType)
SELECT
  COALESCE(p.PetType, 'Unknown') AS pet_type,
  COUNT(a.UserID) AS applications
FROM AdoptionApplication a
JOIN Pet p ON p.PetID = a.PetID
GROUP BY COALESCE(p.PetType, 'Unknown')
ORDER BY applications DESC;

-- 3) Demand by breed
SELECT
  COALESCE(p.PetType, 'Unknown') AS pet_type,
  COALESCE(p.PetBreed, 'Unknown') AS breed,
  COUNT(a.UserID) AS applications
FROM AdoptionApplication a
JOIN Pet p ON p.PetID = a.PetID
GROUP BY COALESCE(p.PetType, 'Unknown'), COALESCE(p.PetBreed, 'Unknown')
ORDER BY applications DESC
LIMIT 25;

-- 4) Application pipeline: pending vs completed (no age column on Pet)
SELECT
  CASE WHEN a.IsAdopted THEN 'Adopted (completed)' ELSE 'Pending' END AS application_state,
  COUNT(*) AS row_count
FROM AdoptionApplication a
GROUP BY a.IsAdopted
ORDER BY row_count DESC;

-- 5) Supply: availability vs adopted by PetType
--    (available = no application row with IsAdopted = TRUE for that pet)
SELECT
  COALESCE(p.PetType, 'Unknown') AS pet_type,
  SUM(
    CASE
      WHEN NOT EXISTS (
        SELECT 1 FROM AdoptionApplication x
        WHERE x.PetID = p.PetID AND x.IsAdopted = TRUE
      ) THEN 1 ELSE 0
    END
  ) AS available_count,
  SUM(
    CASE
      WHEN EXISTS (
        SELECT 1 FROM AdoptionApplication x
        WHERE x.PetID = p.PetID AND x.IsAdopted = TRUE
      ) THEN 1 ELSE 0
    END
  ) AS adopted_count,
  COUNT(*) AS total_pets
FROM Pet p
GROUP BY COALESCE(p.PetType, 'Unknown')
ORDER BY available_count DESC;

-- 6) Shelter performance: distinct pets that have a completed adoption
SELECT
  s.ShelterID,
  s.ShelterName,
  COUNT(DISTINCT p.PetID) AS adoptions_completed
FROM Shelter s
JOIN Pet p ON p.ShelterID = s.ShelterID
WHERE EXISTS (
  SELECT 1 FROM AdoptionApplication a
  WHERE a.PetID = p.PetID AND a.IsAdopted = TRUE
)
GROUP BY s.ShelterID, s.ShelterName
ORDER BY adoptions_completed DESC;

-- 7) Shelter “approval” rate: share of application rows marked adopted (per shelter’s pets)
SELECT
  s.ShelterID,
  s.ShelterName,
  SUM(CASE WHEN a.IsAdopted THEN 1 ELSE 0 END) AS adopted_rows,
  COUNT(*) AS total_application_rows,
  CASE
    WHEN COUNT(*) = 0 THEN NULL
    ELSE SUM(CASE WHEN a.IsAdopted THEN 1 ELSE 0 END) / COUNT(*)
  END AS adoption_rate
FROM AdoptionApplication a
JOIN Pet p ON p.PetID = a.PetID
JOIN Shelter s ON s.ShelterID = p.ShelterID
GROUP BY s.ShelterID, s.ShelterName
ORDER BY adoption_rate DESC;

-- 8) User accounts (no CreatedAt on User — add column for true monthly signups)
SELECT COUNT(*) AS total_registered_users FROM `User`;

-- 9) User engagement: applications per user
SELECT
  u.UserID,
  u.UserEmail,
  COUNT(a.PetID) AS application_count
FROM `User` u
JOIN AdoptionApplication a ON a.UserID = u.UserID
GROUP BY u.UserID, u.UserEmail
ORDER BY application_count DESC
LIMIT 50;

-- 10) Funnel by shelter: application rows vs distinct pets adopted (subqueries avoid join fan-out)
SELECT
  s.ShelterID,
  s.ShelterName,
  (
    SELECT COUNT(*)
    FROM AdoptionApplication a
    INNER JOIN Pet p ON p.PetID = a.PetID
    WHERE p.ShelterID = s.ShelterID
  ) AS total_application_rows,
  (
    SELECT COUNT(DISTINCT p.PetID)
    FROM AdoptionApplication a
    INNER JOIN Pet p ON p.PetID = a.PetID
    WHERE p.ShelterID = s.ShelterID AND a.IsAdopted = TRUE
  ) AS distinct_pets_adopted,
  CASE
    WHEN (
      SELECT COUNT(*)
      FROM AdoptionApplication a
      INNER JOIN Pet p ON p.PetID = a.PetID
      WHERE p.ShelterID = s.ShelterID
    ) = 0 THEN NULL
    ELSE (
      SELECT COUNT(DISTINCT p.PetID)
      FROM AdoptionApplication a
      INNER JOIN Pet p ON p.PetID = a.PetID
      WHERE p.ShelterID = s.ShelterID AND a.IsAdopted = TRUE
    ) / (
      SELECT COUNT(*)
      FROM AdoptionApplication a
      INNER JOIN Pet p ON p.PetID = a.PetID
      WHERE p.ShelterID = s.ShelterID
    )
  END AS adopted_pets_per_application_row
FROM Shelter s
ORDER BY total_application_rows DESC;
