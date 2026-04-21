/**
 * Demo data when API_BASE_URL is empty or the API is unreachable.
 * Shape mirrors a typical REST response from a pet-adoption backend.
 */
export const mockPets = [
  { id: 1, name: 'Milo', species: 'Dog', breed: 'Lab mix', ageYears: 2, shelterId: 1, shelterName: 'River City Rescue', status: 'Available', photoUrl: 'https://images.unsplash.com/photo-1587300003388-59208cc962cb?w=600' },
  { id: 2, name: 'Luna', species: 'Cat', breed: 'Domestic shorthair', ageYears: 1, shelterId: 1, shelterName: 'River City Rescue', status: 'Available', photoUrl: 'https://images.unsplash.com/photo-1514888286974-6c03e2ca1dba?w=600' },
  { id: 3, name: 'Bruno', species: 'Dog', breed: 'Pit bull mix', ageYears: 4, shelterId: 2, shelterName: 'Northside Shelter', status: 'Pending', photoUrl: 'https://images.unsplash.com/photo-1561037404-61cd46aa615c?w=600' },
  { id: 4, name: 'Nala', species: 'Cat', breed: 'Siamese mix', ageYears: 6, shelterId: 2, shelterName: 'Northside Shelter', status: 'Available', photoUrl: 'https://images.unsplash.com/photo-1574158622682-e40e69881006?w=600' },
  { id: 5, name: 'Coco', species: 'Dog', breed: 'Corgi', ageYears: 2, shelterId: 3, shelterName: 'Small Paws Haven', status: 'Available', photoUrl: 'https://images.unsplash.com/photo-1543466835-00a7907e9de1?w=600' },
];

/** Fallback when /api/shelters fails — align names/addresses with sql/schema_and_seed.sql. */
export const mockShelters = [
  { id: 1, name: 'River City Rescue', address: '100 Main St, Columbus, OH', petsCount: 0, completedAdoptions: 0, approvalRate: null },
  { id: 2, name: 'Northside Shelter', address: '250 Lake Ave, Cleveland, OH', petsCount: 0, completedAdoptions: 0, approvalRate: null },
  { id: 3, name: 'Small Paws Haven', address: '9 Elm Rd, Cincinnati, OH', petsCount: 0, completedAdoptions: 0, approvalRate: null },
];
