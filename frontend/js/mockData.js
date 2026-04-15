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

export const mockShelters = [
  { id: 1, name: 'River City Rescue', city: 'Columbus', state: 'OH', petsCount: 24, adoptionsYtd: 58, approvalRate: 0.42 },
  { id: 2, name: 'Northside Shelter', city: 'Cleveland', state: 'OH', petsCount: 41, adoptionsYtd: 72, approvalRate: 0.38 },
  { id: 3, name: 'Small Paws Haven', city: 'Cincinnati', state: 'OH', petsCount: 12, adoptionsYtd: 31, approvalRate: 0.51 },
];

export const mockSummary = {
  totalPetsListed: 77,
  availableNow: 52,
  applicationsThisMonth: 189,
  newUsersThisMonth: 64,
};
