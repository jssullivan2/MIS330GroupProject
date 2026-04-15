import { API_BASE_URL } from './config.js';

function parseErrorMessage(text, data) {
  if (data) {
    if (typeof data.detail === 'string') return data.detail;
    if (typeof data.title === 'string') return data.title;
    if (data.errors && typeof data.errors === 'object') {
      const first = Object.values(data.errors).flat()[0];
      if (first) return String(first);
    }
  }
  return text || 'Request failed.';
}

/** Used for mutations; throws with a readable message on failure. */
async function jsonRequest(path, options = {}) {
  const url = `${API_BASE_URL}${path}`;
  const res = await fetch(url, {
    headers: { 'Content-Type': 'application/json', ...options.headers },
    ...options,
  });
  const text = await res.text();
  let data = null;
  if (text) {
    try {
      data = JSON.parse(text);
    } catch {
      /* plain text body */
    }
  }
  if (!res.ok) {
    throw new Error(parseErrorMessage(text, data));
  }
  return data;
}

async function staffJsonGet(path, employeeId) {
  const url = `${API_BASE_URL}${path}`;
  const res = await fetch(url, {
    headers: {
      Accept: 'application/json',
      'X-Staff-Employee-Id': String(employeeId),
    },
  });
  const text = await res.text();
  let data = null;
  if (text) {
    try {
      data = JSON.parse(text);
    } catch {
      /* ignore */
    }
  }
  if (!res.ok) throw new Error(parseErrorMessage(text, data));
  return data;
}

function staffMutationHeaders(employeeId) {
  return {
    Accept: 'application/json',
    'Content-Type': 'application/json',
    'X-Staff-Employee-Id': String(employeeId),
  };
}

/**
 * @param {{ userName: string, userEmail: string, password: string, typePreference?: string }} payload
 */
export const api = {
  /**
   * Loads pets from MySQL via the API. Throws if the request fails (network, CORS, 503, etc.).
   * Returns a real array (possibly empty) on 200 OK.
   */
  async getPets() {
    const url = `${API_BASE_URL}/api/pets`;
    let res;
    try {
      res = await fetch(url, { headers: { Accept: 'application/json' } });
    } catch (err) {
      const msg = err instanceof Error ? err.message : String(err);
      throw new Error(
        `Cannot reach API at ${url}. Is "dotnet run" running? (${msg})`,
      );
    }
    if (!res.ok) {
      const text = await res.text();
      throw new Error(text || `HTTP ${res.status} from /api/pets`);
    }
    const data = await res.json();
    if (!Array.isArray(data)) {
      throw new Error('Invalid response: expected a JSON array of pets.');
    }
    return data;
  },

  registerUser: (payload) =>
    jsonRequest('/api/users', {
      method: 'POST',
      body: JSON.stringify({
        userName: payload.userName.trim(),
        userEmail: payload.userEmail.trim(),
        password: payload.password,
        typePreference: payload.typePreference?.trim() || null,
      }),
    }),

  loginUser: (userEmail, password) =>
    jsonRequest('/api/auth/adopter', {
      method: 'POST',
      body: JSON.stringify({ userEmail: userEmail.trim(), password }),
    }),

  loginEmployee: (employeeName, password) =>
    jsonRequest('/api/auth/staff', {
      method: 'POST',
      body: JSON.stringify({ employeeName: employeeName.trim(), password }),
    }),

  adoptPet: (petId, userId) =>
    jsonRequest(`/api/pets/${petId}/adopt`, {
      method: 'POST',
      body: JSON.stringify({ userId }),
    }),

  async staffListUsers(employeeId) {
    const data = await staffJsonGet('/api/staff/users', employeeId);
    if (!Array.isArray(data)) throw new Error('Invalid staff users response.');
    return data;
  },

  async staffGetUser(employeeId, userId) {
    return staffJsonGet(`/api/staff/users/${userId}`, employeeId);
  },

  async staffListPets(employeeId) {
    const data = await staffJsonGet('/api/staff/pets', employeeId);
    if (!Array.isArray(data)) throw new Error('Invalid staff pets response.');
    return data;
  },

  staffCreatePet(employeeId, body) {
    return jsonRequest('/api/staff/pets', {
      method: 'POST',
      headers: staffMutationHeaders(employeeId),
      body: JSON.stringify(body),
    });
  },

  staffUpdatePet(employeeId, petId, body) {
    return jsonRequest(`/api/staff/pets/${petId}`, {
      method: 'PUT',
      headers: staffMutationHeaders(employeeId),
      body: JSON.stringify(body),
    });
  },

  staffDeletePet(employeeId, petId) {
    return jsonRequest(`/api/staff/pets/${petId}`, {
      method: 'DELETE',
      headers: staffMutationHeaders(employeeId),
    });
  },

  async staffListApplications(employeeId) {
    const data = await staffJsonGet('/api/staff/applications', employeeId);
    if (!Array.isArray(data)) throw new Error('Invalid staff applications response.');
    return data;
  },

  staffUpsertApplication(employeeId, body) {
    return jsonRequest('/api/staff/applications', {
      method: 'POST',
      headers: staffMutationHeaders(employeeId),
      body: JSON.stringify(body),
    });
  },

  staffDeleteApplication(employeeId, body) {
    return jsonRequest('/api/staff/applications', {
      method: 'DELETE',
      headers: staffMutationHeaders(employeeId),
      body: JSON.stringify(body),
    });
  },

  async staffListShelters(employeeId) {
    const data = await staffJsonGet('/api/staff/shelters', employeeId);
    if (!Array.isArray(data)) throw new Error('Invalid staff shelters response.');
    return data;
  },
};
