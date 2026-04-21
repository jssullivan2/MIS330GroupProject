namespace PawMatch.Api.Data;

/// <summary>Shared SQL fragments for pet list/detail queries.</summary>
internal static class PetQueryFragments
{
    /// <summary>
    /// Photo URLs keyed by <c>Pet.PetType</c> (Cat/Dog). Rotates a few stock images per pet id for variety.
    /// </summary>
    internal const string SelectPhotoUrlColumn = """
              CASE p.PetType
                WHEN 'Cat' THEN CASE (p.PetID % 3)
                  WHEN 0 THEN 'https://images.unsplash.com/photo-1514888286974-6c03e2ca1dba?w=600'
                  WHEN 1 THEN 'https://images.unsplash.com/photo-1574158622682-e40e69881006?w=600'
                  ELSE 'https://images.unsplash.com/photo-1495360010541-f48722b34f7d?w=600'
                END
                WHEN 'Dog' THEN CASE (p.PetID % 3)
                  WHEN 0 THEN 'https://images.unsplash.com/photo-1587300003388-59208cc962cb?w=600'
                  WHEN 1 THEN 'https://images.unsplash.com/photo-1561037404-61cd46aa615c?w=600'
                  ELSE 'https://images.unsplash.com/photo-1543466835-00a7907e9de1?w=600'
                END
                ELSE 'https://images.unsplash.com/photo-1450778869180-41d0601e046e?w=600'
              END AS PhotoUrl
""";
}
