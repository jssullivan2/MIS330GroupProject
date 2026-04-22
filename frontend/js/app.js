import { api } from './api.js';
import { mockPets, mockShelters } from './mockData.js';
import { buildStaffDashboard } from './staffDashboard.js';

/** Adopter browse/home: do not list pets that are already adopted. */
function adopterVisiblePets(pets) {
  if (!Array.isArray(pets)) return [];
  return pets.filter((p) => p.status !== 'Adopted');
}

const STORAGE_KEY = 'pawmatchUser';
const STORAGE_KEY_EMPLOYEE = 'pawmatchEmployee';

function defaultStaffState() {
  return {
    tab: 'profile',
    profile: null,
    users: null,
    pets: null,
    applications: null,
    shelters: null,
    loading: false,
    error: null,
    userKeyword: '',
    petKeyword: '',
    petSort: 'name-asc',
    appStatusFilter: '',
    appKeyword: '',
    selectedUserId: null,
    userDetail: null,
    petEditingId: null,
  };
}

const state = {
  route: 'home',
  pets: adopterVisiblePets([...mockPets]),
  shelters: [...mockShelters],
  summary: null,
  userApplications: null,
  petFilterSpecies: '',
  petFilterShelterId: '',
  loading: false,
  /** Set when /api/pets fails; we fall back to mock pets. */
  error: null,
  /** True after a successful API response (even if the DB returned zero pets). */
  petsFromApi: false,
  /** Adopter (User table) session */
  currentUser: null,
  /** Shelter staff (Employee table) session — separate from adopter */
  currentEmployee: null,
  /** Staff dashboard cache + UI (tabs, search). */
  staff: defaultStaffState(),
  toast: null,
};

function loadSavedUser() {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    const u = JSON.parse(raw);
    if (u && Number.isFinite(u.id) && typeof u.email === 'string') {
      return { id: u.id, email: u.email, userName: typeof u.userName === 'string' ? u.userName : u.email };
    }
  } catch {
    /* ignore */
  }
  return null;
}

function loadSavedEmployee() {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY_EMPLOYEE);
    if (!raw) return null;
    const e = JSON.parse(raw);
    if (e && Number.isFinite(e.id) && typeof e.employeeName === 'string') {
      return {
        id: e.id,
        employeeName: e.employeeName,
        shelterId: Number.isFinite(e.shelterId) ? e.shelterId : null,
      };
    }
  } catch {
    /* ignore */
  }
  return null;
}

function clearUserSession() {
  state.currentUser = null;
  sessionStorage.removeItem(STORAGE_KEY);
}

function clearEmployeeSession() {
  state.currentEmployee = null;
  sessionStorage.removeItem(STORAGE_KEY_EMPLOYEE);
  state.staff = defaultStaffState();
}

function hasActiveSession() {
  return Boolean(state.currentUser || state.currentEmployee);
}

function setToast(kind, text) {
  state.toast = { kind, text };
  render();
}

function el(tag, attrs = {}, children = []) {
  const node = document.createElement(tag);
  Object.entries(attrs).forEach(([k, v]) => {
    if (k === 'class') node.className = v;
    else if (k === 'html') node.innerHTML = v;
    else if (k.startsWith('on') && typeof v === 'function') node.addEventListener(k.slice(2).toLowerCase(), v);
    else if (k === 'disabled' && (node instanceof HTMLButtonElement || node instanceof HTMLInputElement || node instanceof HTMLSelectElement || node instanceof HTMLTextAreaElement)) {
      node.disabled = Boolean(v);
    } else if (v !== null && v !== undefined) node.setAttribute(k, v);
  });
  (Array.isArray(children) ? children : [children]).forEach((c) => {
    if (c == null) return;
    node.appendChild(typeof c === 'string' ? document.createTextNode(c) : c);
  });
  return node;
}

function navigate(route) {
  state.route = route;
  window.history.replaceState({}, '', `#${route}`);
  render();
}

async function loadData() {
  state.loading = true;
  state.error = null;
  state.petsFromApi = false;
  render();

  const requests = [
    api.getPets(state.currentUser?.id),
    api.getShelters(),
    api.getSummary(),
    state.currentUser ? api.getUserApplications(state.currentUser.id) : Promise.resolve(null),
  ];
  const [petsOutcome, sheltersOutcome, summaryOutcome, userAppsOutcome] = await Promise.allSettled(requests);

  if (petsOutcome.status === 'fulfilled') {
    state.pets = adopterVisiblePets(petsOutcome.value);
    state.petsFromApi = true;
  } else {
    const e = petsOutcome.reason;
    state.error = e instanceof Error ? e.message : String(e);
    state.pets = adopterVisiblePets([...mockPets]);
    state.petsFromApi = false;
  }

  if (sheltersOutcome.status === 'fulfilled') {
    state.shelters = sheltersOutcome.value;
  } else {
    state.shelters = [...mockShelters];
  }

  if (summaryOutcome.status === 'fulfilled') {
    state.summary = summaryOutcome.value;
  } else {
    state.summary = null;
  }
  if (userAppsOutcome.status === 'fulfilled') {
    state.userApplications = userAppsOutcome.value;
  } else {
    state.userApplications = null;
  }
  state.loading = false;
  render();
}

async function loadStaffDashboard() {
  if (!state.currentEmployee) return;
  const empId = state.currentEmployee.id;
  state.staff.loading = true;
  state.staff.error = null;
  render();
  try {
    const [profile, users, pets, applications, shelters] = await Promise.all([
      api.staffGetProfile(empId),
      api.staffListUsers(empId),
      api.staffListPets(empId),
      api.staffListApplications(empId),
      api.staffListShelters(empId),
    ]);
    state.staff.profile = profile;
    state.staff.users = users;
    state.staff.pets = pets;
    state.staff.applications = applications;
    state.staff.shelters = shelters;
  } catch (e) {
    state.staff.error = e instanceof Error ? e.message : String(e);
  } finally {
    state.staff.loading = false;
    render();
  }
}

function openRegisterModal() {
  if (hasActiveSession()) {
    setToast('warning', 'Sign out before creating or signing into another account.');
    return;
  }
  const modalEl = document.getElementById('pmRegisterModal');
  if (!modalEl || !window.bootstrap) return;
  const feedback = document.getElementById('pmRegisterFeedback');
  if (feedback) feedback.textContent = '';
  const form = document.getElementById('pmRegisterForm');
  if (form) form.reset();
  window.bootstrap.Modal.getOrCreateInstance(modalEl).show();
}

function openUserLoginModal() {
  if (hasActiveSession()) {
    setToast('warning', 'Sign out before creating or signing into another account.');
    return;
  }
  const modalEl = document.getElementById('pmUserLoginModal');
  if (!modalEl || !window.bootstrap) return;
  const fb = document.getElementById('pmUserLoginFeedback');
  if (fb) fb.textContent = '';
  const form = document.getElementById('pmUserLoginForm');
  if (form) form.reset();
  window.bootstrap.Modal.getOrCreateInstance(modalEl).show();
}

function openEmployeeLoginModal() {
  if (hasActiveSession()) {
    setToast('warning', 'Sign out before creating or signing into another account.');
    return;
  }
  const modalEl = document.getElementById('pmEmployeeLoginModal');
  if (!modalEl || !window.bootstrap) return;
  const fb = document.getElementById('pmEmployeeLoginFeedback');
  if (fb) fb.textContent = '';
  const form = document.getElementById('pmEmployeeLoginForm');
  if (form) form.reset();
  window.bootstrap.Modal.getOrCreateInstance(modalEl).show();
}

function setupAuthModals() {
  const root = document.getElementById('modal-root');
  if (!root || root.dataset.pmAuthModals === '1') return;
  root.dataset.pmAuthModals = '1';

  // --- Adopter registration
  const regModal = el('div', {
    class: 'modal fade',
    id: 'pmRegisterModal',
    tabindex: '-1',
    'aria-labelledby': 'pmRegisterModalLabel',
    'aria-hidden': 'true',
  });
  const regForm = el('form', { id: 'pmRegisterForm', novalidate: 'true' });
  const nameInput = el('input', {
    type: 'text',
    class: 'form-control',
    id: 'pmRegisterName',
    required: 'true',
    autocomplete: 'name',
    maxlength: '100',
  });
  const emailInput = el('input', {
    type: 'email',
    class: 'form-control',
    id: 'pmRegisterEmail',
    required: 'true',
    autocomplete: 'email',
    maxlength: '100',
  });
  const passwordInput = el('input', {
    type: 'password',
    class: 'form-control',
    id: 'pmRegisterPassword',
    required: 'true',
    autocomplete: 'new-password',
    minlength: '4',
    maxlength: '255',
  });
  const typeSelect = el('select', { class: 'form-select', id: 'pmRegisterType' });
  ['', 'Dog', 'Cat'].forEach((v) => {
    const label = v === '' ? 'No preference' : v;
    typeSelect.appendChild(el('option', { value: v }, [label]));
  });
  regForm.appendChild(el('div', { class: 'mb-3' }, [
    el('label', { class: 'form-label', for: 'pmRegisterName' }, ['Display name']),
    nameInput,
  ]));
  regForm.appendChild(el('div', { class: 'mb-3' }, [
    el('label', { class: 'form-label', for: 'pmRegisterEmail' }, ['Email']),
    emailInput,
  ]));
  regForm.appendChild(el('div', { class: 'mb-3' }, [
    el('label', { class: 'form-label', for: 'pmRegisterPassword' }, ['Password']),
    passwordInput,
    el('div', { class: 'form-text' }, ['User table: Password VARCHAR(255). Demo only — hash in production.']),
  ]));
  regForm.appendChild(el('div', { class: 'mb-3' }, [
    el('label', { class: 'form-label', for: 'pmRegisterType' }, ['Type preference']),
    typeSelect,
  ]));
  const regFeedback = el('div', { class: 'text-danger small', id: 'pmRegisterFeedback' });
  regForm.appendChild(regFeedback);
  const regSubmit = el('button', { type: 'button', class: 'btn btn-primary' }, ['Create account']);
  regSubmit.addEventListener('click', async () => {
    regFeedback.textContent = '';
    const userName = nameInput.value.trim();
    const email = emailInput.value.trim();
    const password = passwordInput.value;
    const typePreference = typeSelect.value;
    if (!userName || !email) {
      regFeedback.textContent = 'Please enter a name and email.';
      return;
    }
    if (password.length < 4 || password.length > 255) {
      regFeedback.textContent = 'Password must be 4–255 characters.';
      return;
    }
    regSubmit.disabled = true;
    try {
      const user = await api.registerUser({
        userName,
        userEmail: email,
        password,
        typePreference: typePreference || null,
      });
      clearEmployeeSession();
      state.currentUser = { id: user.id, email: user.userEmail, userName: user.userName };
      sessionStorage.setItem(STORAGE_KEY, JSON.stringify(state.currentUser));
      window.bootstrap.Modal.getOrCreateInstance(document.getElementById('pmRegisterModal')).hide();
      setToast('success', 'Adopter account created. Staff session was cleared if you had one.');
      render();
      await loadData();
    } catch (err) {
      regFeedback.textContent = err.message || 'Could not create account.';
    } finally {
      regSubmit.disabled = false;
    }
  });
  regModal.appendChild(el('div', { class: 'modal-dialog' }, [
    el('div', { class: 'modal-content' }, [
      el('div', { class: 'modal-header' }, [
        el('h5', { class: 'modal-title', id: 'pmRegisterModalLabel' }, ['Adopter — create account']),
        el('button', { type: 'button', class: 'btn-close', 'data-bs-dismiss': 'modal', 'aria-label': 'Close' }),
      ]),
      el('div', { class: 'modal-body' }, [regForm]),
      el('div', { class: 'modal-footer' }, [
        el('button', { type: 'button', class: 'btn btn-outline-secondary', 'data-bs-dismiss': 'modal' }, ['Cancel']),
        regSubmit,
      ]),
    ]),
  ]));
  root.appendChild(regModal);

  // --- Adopter sign in (User table)
  const userModal = el('div', {
    class: 'modal fade',
    id: 'pmUserLoginModal',
    tabindex: '-1',
    'aria-labelledby': 'pmUserLoginLabel',
    'aria-hidden': 'true',
  });
  const userForm = el('form', { id: 'pmUserLoginForm', novalidate: 'true' });
  const uEmail = el('input', {
    type: 'email',
    class: 'form-control',
    id: 'pmUserLoginEmail',
    autocomplete: 'username',
    maxlength: '100',
  });
  const uPass = el('input', {
    type: 'password',
    class: 'form-control',
    id: 'pmUserLoginPassword',
    autocomplete: 'current-password',
    maxlength: '255',
  });
  userForm.appendChild(el('div', { class: 'mb-3' }, [
    el('label', { class: 'form-label', for: 'pmUserLoginEmail' }, ['Email']),
    uEmail,
  ]));
  userForm.appendChild(el('div', { class: 'mb-3' }, [
    el('label', { class: 'form-label', for: 'pmUserLoginPassword' }, ['Password']),
    uPass,
  ]));
  const userFb = el('div', { class: 'text-danger small', id: 'pmUserLoginFeedback' });
  userForm.appendChild(userFb);
  const userSubmit = el('button', { type: 'button', class: 'btn btn-primary' }, ['Sign in']);
  userSubmit.addEventListener('click', async () => {
    userFb.textContent = '';
    const em = uEmail.value.trim();
    const pw = uPass.value;
    if (!em || !pw) {
      userFb.textContent = 'Enter email and password.';
      return;
    }
    userSubmit.disabled = true;
    try {
      const user = await api.loginUser(em, pw);
      clearEmployeeSession();
      state.currentUser = { id: user.id, email: user.userEmail, userName: user.userName };
      sessionStorage.setItem(STORAGE_KEY, JSON.stringify(state.currentUser));
      window.bootstrap.Modal.getOrCreateInstance(document.getElementById('pmUserLoginModal')).hide();
      setToast('success', 'Signed in as adopter.');
      render();
      await loadData();
    } catch (err) {
      userFb.textContent = err.message || 'Sign in failed.';
    } finally {
      userSubmit.disabled = false;
    }
  });
  userModal.appendChild(el('div', { class: 'modal-dialog' }, [
    el('div', { class: 'modal-content' }, [
      el('div', { class: 'modal-header' }, [
        el('h5', { class: 'modal-title', id: 'pmUserLoginLabel' }, ['Adopter — sign in']),
        el('button', { type: 'button', class: 'btn-close', 'data-bs-dismiss': 'modal', 'aria-label': 'Close' }),
      ]),
      el('div', { class: 'modal-body' }, [userForm]),
      el('div', { class: 'modal-footer' }, [
        el('button', { type: 'button', class: 'btn btn-outline-secondary', 'data-bs-dismiss': 'modal' }, ['Cancel']),
        userSubmit,
      ]),
    ]),
  ]));
  root.appendChild(userModal);

  // --- Staff sign in (Employee table)
  const empModal = el('div', {
    class: 'modal fade',
    id: 'pmEmployeeLoginModal',
    tabindex: '-1',
    'aria-labelledby': 'pmEmployeeLoginLabel',
    'aria-hidden': 'true',
  });
  const empForm = el('form', { id: 'pmEmployeeLoginForm', novalidate: 'true' });
  const empName = el('input', {
    type: 'text',
    class: 'form-control',
    id: 'pmEmployeeLoginName',
    autocomplete: 'username',
    maxlength: '100',
  });
  const empPass = el('input', {
    type: 'password',
    class: 'form-control',
    id: 'pmEmployeeLoginPassword',
    autocomplete: 'current-password',
    maxlength: '255',
  });
  empForm.appendChild(el('div', { class: 'mb-3' }, [
    el('label', { class: 'form-label', for: 'pmEmployeeLoginName' }, ['Employee name']),
    empName,
    el('div', { class: 'form-text' }, ['Must match Employee.EmployeeName in MySQL (e.g. seed: Jamie Lee).']),
  ]));
  empForm.appendChild(el('div', { class: 'mb-3' }, [
    el('label', { class: 'form-label', for: 'pmEmployeeLoginPassword' }, ['Password']),
    empPass,
  ]));
  const empFb = el('div', { class: 'text-danger small', id: 'pmEmployeeLoginFeedback' });
  empForm.appendChild(empFb);
  const empSubmit = el('button', { type: 'button', class: 'btn btn-dark' }, ['Staff sign in']);
  empSubmit.addEventListener('click', async () => {
    empFb.textContent = '';
    const n = empName.value.trim();
    const pw = empPass.value;
    if (!n || !pw) {
      empFb.textContent = 'Enter employee name and password.';
      return;
    }
    empSubmit.disabled = true;
    try {
      const emp = await api.loginEmployee(n, pw);
      clearUserSession();
      state.currentEmployee = {
        id: emp.id,
        employeeName: emp.employeeName,
        shelterId: emp.shelterId ?? null,
      };
      sessionStorage.setItem(STORAGE_KEY_EMPLOYEE, JSON.stringify(state.currentEmployee));
      window.bootstrap.Modal.getOrCreateInstance(document.getElementById('pmEmployeeLoginModal')).hide();
      setToast('success', 'Signed in as shelter staff. Adopter session was cleared.');
      navigate('staff');
      await loadData();
      void loadStaffDashboard();
    } catch (err) {
      empFb.textContent = err.message || 'Staff sign in failed.';
    } finally {
      empSubmit.disabled = false;
    }
  });
  empModal.appendChild(el('div', { class: 'modal-dialog' }, [
    el('div', { class: 'modal-content' }, [
      el('div', { class: 'modal-header' }, [
        el('h5', { class: 'modal-title', id: 'pmEmployeeLoginLabel' }, ['Staff — sign in']),
        el('button', { type: 'button', class: 'btn-close', 'data-bs-dismiss': 'modal', 'aria-label': 'Close' }),
      ]),
      el('div', { class: 'modal-body' }, [empForm]),
      el('div', { class: 'modal-footer' }, [
        el('button', { type: 'button', class: 'btn btn-outline-secondary', 'data-bs-dismiss': 'modal' }, ['Cancel']),
        empSubmit,
      ]),
    ]),
  ]));
  root.appendChild(empModal);
}

function toastBanner() {
  if (!state.toast) return null;
  const { kind, text } = state.toast;
  const close = el('button', {
    type: 'button',
    class: 'btn-close',
    'data-bs-dismiss': 'alert',
    'aria-label': 'Close',
  });
  close.addEventListener('click', () => {
    state.toast = null;
    render();
  });
  return el('div', { class: 'container mt-3' }, [
    el('div', { class: `alert alert-${kind} alert-dismissible fade show`, role: 'alert' }, [text, close]),
  ]);
}

function navBar() {
  const items = [
    { id: 'home', label: 'Home', icon: 'bi-house' },
    { id: 'pets', label: 'Browse pets', icon: 'bi-heart' },
    { id: 'shelters', label: 'Shelters', icon: 'bi-building' },
    ...(state.currentUser
      ? [{ id: 'profile', label: 'My profile', icon: 'bi-person-badge' }]
      : []),
    ...(state.currentEmployee
      ? [{ id: 'staff', label: 'Staff dashboard', icon: 'bi-speedometer2' }]
      : []),
  ];
  const brand = el('a', { class: 'navbar-brand fw-semibold', href: '#' }, [
    el('i', { class: 'bi bi-paw-fill brand-paw me-1' }),
    document.createTextNode(' Paw & Homes'),
  ]);
  brand.addEventListener('click', (e) => {
    e.preventDefault();
    navigate('home');
  });

  const ul = el('ul', { class: 'navbar-nav ms-auto mb-2 mb-lg-0 align-items-lg-center' });
  items.forEach((item) => {
    const a = el('a', {
      class: `nav-link ${state.route === item.id ? 'active' : ''}`,
      href: '#',
    }, [el('i', { class: `bi ${item.icon} me-1` }), document.createTextNode(item.label)]);
    a.addEventListener('click', (e) => {
      e.preventDefault();
      navigate(item.id);
      if (item.id === 'staff') loadStaffDashboard();
    });
    ul.appendChild(el('li', { class: 'nav-item' }, [a]));
  });

  ul.appendChild(el('li', { class: 'nav-item' }, [el('span', { class: 'nav-link disabled d-none d-lg-inline px-1' }, ['|'])]));

  if (state.currentEmployee) {
    const sh = state.currentEmployee.shelterId != null ? ` · Shelter ${state.currentEmployee.shelterId}` : '';
    ul.appendChild(el('li', { class: 'nav-item ms-lg-1' }, [
      el('span', { class: 'navbar-text small' }, [
        el('span', { class: 'badge bg-dark me-1' }, ['Staff']),
        `${state.currentEmployee.employeeName}${sh}`,
      ]),
    ]));
    const empOut = el('a', { class: 'nav-link', href: '#' }, [el('i', { class: 'bi bi-box-arrow-right me-1' }), 'Staff sign out']);
    empOut.addEventListener('click', async (e) => {
      e.preventDefault();
      clearEmployeeSession();
      navigate('home');
      await loadData();
    });
    ul.appendChild(el('li', { class: 'nav-item' }, [empOut]));
  } else if (!state.currentUser) {
    const staffIn = el('a', { class: 'nav-link', href: '#' }, [el('i', { class: 'bi bi-shield-lock me-1' }), 'Staff login']);
    staffIn.addEventListener('click', (e) => {
      e.preventDefault();
      openEmployeeLoginModal();
    });
    ul.appendChild(el('li', { class: 'nav-item' }, [staffIn]));
  }

  ul.appendChild(el('li', { class: 'nav-item' }, [el('span', { class: 'nav-link disabled d-none d-lg-inline px-1' }, ['|'])]));

  if (state.currentUser) {
    const label = state.currentUser.userName || state.currentUser.email;
    ul.appendChild(el('li', { class: 'nav-item ms-lg-1' }, [
      el('span', { class: 'navbar-text small text-secondary' }, [
        el('span', { class: 'badge bg-primary me-1' }, ['Adopter']),
        label,
      ]),
    ]));
    const signOut = el('a', { class: 'nav-link', href: '#' }, [el('i', { class: 'bi bi-box-arrow-right me-1' }), 'Adopter sign out']);
    signOut.addEventListener('click', async (e) => {
      e.preventDefault();
      clearUserSession();
      navigate('home');
      await loadData();
    });
    ul.appendChild(el('li', { class: 'nav-item' }, [signOut]));
  } else if (!state.currentEmployee) {
    const reg = el('a', { class: 'nav-link', href: '#' }, [el('i', { class: 'bi bi-person-plus me-1' }), 'Create account']);
    reg.addEventListener('click', (e) => {
      e.preventDefault();
      openRegisterModal();
    });
    ul.appendChild(el('li', { class: 'nav-item' }, [reg]));
    const adopterIn = el('a', { class: 'nav-link', href: '#' }, [el('i', { class: 'bi bi-box-arrow-in-right me-1' }), 'Adopter sign in']);
    adopterIn.addEventListener('click', (e) => {
      e.preventDefault();
      openUserLoginModal();
    });
    ul.appendChild(el('li', { class: 'nav-item' }, [adopterIn]));
  }

  return el('nav', { class: 'navbar navbar-expand-lg navbar-light bg-white border-bottom shadow-sm sticky-top' }, [
    el('div', { class: 'container' }, [
      brand,
      el('button', {
        class: 'navbar-toggler',
        type: 'button',
        'data-bs-toggle': 'collapse',
        'data-bs-target': '#mainNav',
      }, [el('span', { class: 'navbar-toggler-icon' })]),
      el('div', { class: 'collapse navbar-collapse', id: 'mainNav' }, [ul]),
    ]),
  ]);
}

function heroSection() {
  const canStartAuth = !hasActiveSession();
  return el('section', { class: 'hero-gradient text-white py-5 mb-4' }, [
    el('div', { class: 'container' }, [
      el('div', { class: 'row align-items-center' }, [
        el('div', { class: 'col-lg-7' }, [
          el('h1', { class: 'display-5 fw-bold mb-3' }, ['Find your companion.']),
          el('p', { class: 'lead opacity-90 mb-4' }, [
            'Adopters use the User table; shelter staff use the Employee table — separate sign-in flows.',
          ]),
          el('div', { class: 'd-flex flex-wrap gap-2' }, [
            el('button', { type: 'button', class: 'btn btn-light btn-lg', onclick: () => navigate('pets') }, ['Browse pets']),
            ...(canStartAuth
              ? [
                el('button', {
                  type: 'button',
                  class: 'btn btn-outline-light btn-lg',
                  onclick: () => openRegisterModal(),
                }, ['Create adopter account']),
                el('button', {
                  type: 'button',
                  class: 'btn btn-outline-light btn-lg',
                  onclick: () => openUserLoginModal(),
                }, ['Adopter sign in']),
                el('button', {
                  type: 'button',
                  class: 'btn btn-outline-light border-light text-white btn-lg',
                  onclick: () => openEmployeeLoginModal(),
                }, ['Staff login']),
              ]
              : [
                el('span', { class: 'badge bg-light text-dark px-3 py-2 align-self-center' }, [
                  'Signed in. Sign out to switch accounts.',
                ]),
              ]),
          ]),
        ]),
        el('div', { class: 'col-lg-5 mt-4 mt-lg-0 text-center' }, [
          el('i', { class: 'bi bi-heart-fill display-1 opacity-50' }),
        ]),
      ]),
    ]),
  ]);
}

function statCards() {
  const s = state.summary;
  const cards = [
    { label: 'Pets on platform', value: s?.totalPetsListed, icon: 'bi-tags' },
    { label: 'Available now', value: s?.availableNow, icon: 'bi-check-circle' },
    { label: 'Applications total', value: s?.applicationsThisMonth, icon: 'bi-envelope-open' },
    { label: 'Registered users', value: s?.newUsersThisMonth, icon: 'bi-people' },
  ];
  return el('div', { class: 'row g-3 mb-4' }, cards.map((c) =>
    el('div', { class: 'col-6 col-md-3' }, [
      el('div', { class: 'card border-0 shadow-sm h-100' }, [
        el('div', { class: 'card-body' }, [
          el('div', { class: 'd-flex align-items-center mb-2 text-secondary' }, [
            el('i', { class: `bi ${c.icon} fs-4 me-2` }),
            el('span', { class: 'small text-uppercase fw-semibold stat-pill' }, [c.label]),
          ]),
          el('div', { class: 'fs-3 fw-bold text-dark' }, [c.value == null ? '—' : String(c.value)]),
        ]),
      ]),
    ])
  ));
}

function petStatusBadge(p) {
  const global = p.status;
  const tips = {
    Available: 'No completed adoption yet (no row with IsAdopted = 1).',
    Pending: 'Open adoption request(s): IsAdopted = 0 until staff approves.',
    Adopted: 'This pet has an approved adoption (IsAdopted = 1).',
  };
  const badgeClass =
    global === 'Available' ? 'bg-success' : global === 'Pending' ? 'bg-warning text-dark' : 'bg-secondary';
  const label =
    global === 'Pending' ? 'Pending (0)' : global === 'Adopted' ? 'Adopted (1)' : global;
  return el('span', {
    class: `badge ${badgeClass}`,
    title: tips[global] || '',
  }, [label]);
}

function petCard(p) {
  const imgSrc = p.photoUrl || 'https://images.unsplash.com/photo-1450778869180-41d0601e046e?w=600';
  const mine = p.myApplicationStatus;
  const openForRequests = p.status === 'Available' || p.status === 'Pending';

  let actionBtn;
  if (p.status === 'Adopted' && mine !== 'adopted') {
    actionBtn = el('button', { type: 'button', class: 'btn btn-outline-secondary btn-sm mt-2', disabled: true }, [
      'Already adopted',
    ]);
  } else if (mine === 'adopted') {
    actionBtn = el('button', { type: 'button', class: 'btn btn-success btn-sm mt-2', disabled: true }, [
      'Your adoption approved',
    ]);
  } else if (mine === 'pending') {
    actionBtn = el('button', { type: 'button', class: 'btn btn-outline-secondary btn-sm mt-2', disabled: true }, [
      'Request submitted (awaiting approval)',
    ]);
  } else if (openForRequests && state.currentUser) {
    actionBtn = el('button', { type: 'button', class: 'btn btn-primary btn-sm mt-2' }, ['Submit adoption request']);
    actionBtn.addEventListener('click', async () => {
      if (!window.confirm(
        `Submit an adoption request for ${p.name}? This saves IsAdopted = 0 in AdoptionApplication; staff sets 1 when approved.`,
      )) return;
      actionBtn.disabled = true;
      try {
        await api.submitAdoptionRequest(p.id, state.currentUser.id);
        setToast('success', `Request saved for ${p.name}. Await staff approval (IsAdopted → 1).`);
        await loadData();
      } catch (err) {
        setToast('danger', err.message || 'Request failed. Is the API running?');
        actionBtn.disabled = false;
      }
    });
  } else if (openForRequests && state.currentEmployee) {
    actionBtn = el('button', { type: 'button', class: 'btn btn-outline-secondary btn-sm mt-2' }, ['Adopter sign in to apply']);
    actionBtn.addEventListener('click', () => openUserLoginModal());
  } else if (openForRequests) {
    actionBtn = el('button', { type: 'button', class: 'btn btn-outline-primary btn-sm mt-2' }, ['Create adopter account']);
    actionBtn.addEventListener('click', () => openRegisterModal());
  } else {
    actionBtn = el('button', { type: 'button', class: 'btn btn-outline-secondary btn-sm mt-2', disabled: true }, [
      'Not available',
    ]);
  }

  const myNote =
    state.currentUser && (mine === 'pending' || mine === 'adopted')
      ? el('div', { class: 'small text-muted mt-1' }, [
        mine === 'pending'
          ? 'Your row: IsAdopted = 0 (pending).'
          : 'Your row: IsAdopted = 1 (approved).',
      ])
      : null;

  return el('div', { class: 'col-md-6 col-lg-4' }, [
    el('div', { class: 'card card-pet border-0 shadow-sm h-100' }, [
      el('img', { class: 'card-img-top', src: imgSrc, alt: p.name }),
      el('div', { class: 'card-body d-flex flex-column' }, [
        el('div', { class: 'd-flex justify-content-between align-items-start mb-2' }, [
          el('h5', { class: 'card-title mb-0' }, [p.name]),
          petStatusBadge(p),
        ]),
        el('p', { class: 'card-text text-secondary small mb-2' }, [
          `${p.species} · ${p.breed} · ${p.ageYears} yr`,
        ]),
        el('p', { class: 'card-text small mt-auto text-muted' }, [
          el('i', { class: 'bi bi-geo-alt' }),
          ` ${p.shelterName}`,
        ]),
        actionBtn,
        myNote,
      ]),
    ]),
  ]);
}

function viewHome() {
  const featured = state.pets.slice(0, 3);
  return el('div', {}, [
    heroSection(),
    el('div', { class: 'container pb-5' }, [
      state.loading ? el('div', { class: 'alert alert-info' }, ['Loading pets from the API…']) : null,
      state.error
        ? el('div', { class: 'alert alert-warning' }, [
            el('strong', {}, ['Could not load pets from MySQL. ']),
            `Showing demo data instead. ${state.error}`,
          ])
        : null,
      state.petsFromApi && state.pets.length === 0
        ? el('div', { class: 'alert alert-secondary' }, [
            'The API responded successfully but there are ',
            el('strong', {}, ['no rows in the Pet table']),
            ' yet. Add pets in MySQL (or run your seed script), then refresh.',
          ])
        : null,
      el('h2', { class: 'h4 mb-3' }, ['At a glance']),
      statCards(),
      state.currentUser ? el('div', { class: 'mb-4' }, [
        el('div', { class: 'card border-0 shadow-sm' }, [
          el('div', { class: 'card-body d-flex justify-content-between align-items-center' }, [
            el('div', {}, [
              el('h3', { class: 'h6 mb-1' }, ['Adopter profile']),
              el('p', { class: 'small text-secondary mb-0' }, ['Track your pet applications and statuses in one place.']),
            ]),
            el('button', { type: 'button', class: 'btn btn-outline-primary btn-sm', onclick: () => navigate('profile') }, [
              'Open profile',
            ]),
          ]),
        ]),
      ]) : null,
      el('div', { class: 'd-flex justify-content-between align-items-center mb-3' }, [
        el('h2', { class: 'h4 mb-0' }, ['Featured pets']),
        el('button', { type: 'button', class: 'btn btn-link', onclick: () => navigate('pets') }, ['See all']),
      ]),
      el('div', { class: 'row g-4' }, featured.map(petCard)),
    ]),
  ]);
}

function viewAdopterProfile() {
  if (!state.currentUser) {
    return el('div', { class: 'container py-5' }, [
      el('div', { class: 'alert alert-warning' }, ['Sign in as an adopter to view your profile dashboard.']),
      el('button', { type: 'button', class: 'btn btn-primary', onclick: () => openUserLoginModal() }, ['Adopter sign in']),
    ]);
  }

  const apps = Array.isArray(state.userApplications) ? state.userApplications : [];
  const accepted = apps.filter((a) => a.applicationStatus === 'Accepted').length;
  const pending = apps.filter((a) => a.applicationStatus === 'Pending').length;
  const denied = apps.filter((a) => a.applicationStatus === 'Denied').length;

  const stat = (label, value, icon) => el('div', { class: 'col-6 col-md-3' }, [
    el('div', { class: 'card border-0 shadow-sm h-100' }, [
      el('div', { class: 'card-body' }, [
        el('div', { class: 'small text-uppercase text-secondary fw-semibold mb-2' }, [el('i', { class: `bi ${icon} me-1` }), label]),
        el('div', { class: 'fs-4 fw-bold' }, [String(value)]),
      ]),
    ]),
  ]);

  const statusBadge = (status) => {
    if (status === 'Accepted') return el('span', { class: 'badge bg-success' }, ['Accepted']);
    if (status === 'Denied') return el('span', { class: 'badge bg-danger' }, ['Denied']);
    return el('span', { class: 'badge bg-warning text-dark' }, ['Pending']);
  };

  const rows = apps.map((a) => el('tr', {}, [
    el('td', {}, [String(a.petId)]),
    el('td', {}, [a.petName]),
    el('td', {}, [a.petType]),
    el('td', {}, [a.shelterName]),
    el('td', {}, [statusBadge(a.applicationStatus)]),
  ]));

  return el('div', { class: 'container py-4' }, [
    el('h1', { class: 'h3 mb-2' }, ['Adopter profile']),
    el('p', { class: 'text-secondary mb-4' }, [
      'Track pets you applied for and monitor each application status.',
    ]),
    el('div', { class: 'row g-3 mb-4' }, [
      stat('Applications submitted', apps.length, 'bi-inboxes'),
      stat('Accepted', accepted, 'bi-check2-circle'),
      stat('Pending', pending, 'bi-hourglass-split'),
      stat('Denied', denied, 'bi-x-circle'),
    ]),
    el('div', { class: 'card border-0 shadow-sm' }, [
      el('div', { class: 'card-body' }, [
        el('h2', { class: 'h6 mb-3' }, ['Application history']),
        el('div', { class: 'table-responsive' }, [
          el('table', { class: 'table table-sm table-hover align-middle mb-0' }, [
            el('thead', {}, [
              el('tr', {}, [
                el('th', {}, ['Pet ID']),
                el('th', {}, ['Pet']),
                el('th', {}, ['Type']),
                el('th', {}, ['Shelter']),
                el('th', {}, ['Status']),
              ]),
            ]),
            el('tbody', {}, rows.length ? rows : [el('tr', {}, [el('td', { colspan: '5', class: 'text-secondary' }, ['No applications submitted yet.'])])]),
          ]),
        ]),
      ]),
    ]),
  ]);
}

function viewPets() {
  const speciesOptions = [...new Set(state.pets.map((p) => p.species))].sort();
  const shelterOptions = state.shelters;

  const speciesSelect = el('select', { class: 'form-select' });
  speciesSelect.appendChild(el('option', { value: '' }, ['All species']));
  speciesOptions.forEach((sp) => {
    speciesSelect.appendChild(el('option', { value: sp }, [sp]));
  });
  speciesSelect.value = state.petFilterSpecies;
  speciesSelect.addEventListener('change', () => {
    state.petFilterSpecies = speciesSelect.value;
    render();
  });

  const shelterSelect = el('select', { class: 'form-select' });
  shelterSelect.appendChild(el('option', { value: '' }, ['All shelters']));
  shelterOptions.forEach((sh) => {
    shelterSelect.appendChild(el('option', { value: String(sh.id) }, [sh.name]));
  });
  shelterSelect.value = state.petFilterShelterId;
  shelterSelect.addEventListener('change', () => {
    state.petFilterShelterId = shelterSelect.value;
    render();
  });

  let filtered = state.pets;
  if (state.petFilterSpecies) filtered = filtered.filter((p) => p.species === state.petFilterSpecies);
  if (state.petFilterShelterId) filtered = filtered.filter((p) => String(p.shelterId) === state.petFilterShelterId);

  return el('div', { class: 'container py-4' }, [
    el('h1', { class: 'h3 mb-3' }, ['Browse pets']),
    state.error
      ? el('div', { class: 'alert alert-warning mb-3' }, [
          el('strong', {}, ['API error — showing demo pets. ']),
          state.error,
        ])
      : null,
    state.petsFromApi && state.pets.length === 0
      ? el('div', { class: 'alert alert-secondary mb-3' }, [
          'No pets in the database yet. Seed the ',
          el('code', {}, ['Pet']),
          ' table, then refresh.',
        ])
      : null,
    el('div', { class: 'row g-3 mb-4' }, [
      el('div', { class: 'col-md-4' }, [
        el('label', { class: 'form-label small' }, ['Species']),
        speciesSelect,
      ]),
      el('div', { class: 'col-md-4' }, [
        el('label', { class: 'form-label small' }, ['Shelter']),
        shelterSelect,
      ]),
      el('div', { class: 'col-md-4 d-flex align-items-end' }, [
        el('button', {
          type: 'button',
          class: 'btn btn-outline-secondary',
          onclick: () => {
            state.petFilterSpecies = '';
            state.petFilterShelterId = '';
            render();
          },
        }, ['Reset filters']),
      ]),
    ]),
    el('div', { class: 'row g-4' }, filtered.map(petCard)),
  ]);
}

function viewShelters() {
  return el('div', { class: 'container py-4' }, [
    el('h1', { class: 'h3 mb-4' }, ['Shelter performance snapshot']),
    state.shelters.length === 0
      ? el('p', { class: 'text-secondary' }, [
          'No shelters in the database yet. Seed ',
          el('code', {}, ['schema_and_seed.sql']),
          ' and refresh.',
        ])
      : el('div', { class: 'row g-4' }, state.shelters.map((s) => {
        const loc = s.address || [s.city, s.state].filter(Boolean).join(', ');
        const listed = s.petsCount ?? 0;
        const completed =
          s.completedAdoptions ?? s.adoptionsYtd ?? 0;
        const rateText =
          s.approvalRate == null || Number.isNaN(s.approvalRate)
            ? 'N/A'
            : `${Math.round(Number(s.approvalRate) * 100)}%`;
        return el('div', { class: 'col-md-4' }, [
          el('div', { class: 'card border-0 shadow-sm h-100' }, [
            el('div', { class: 'card-body' }, [
              el('h5', { class: 'card-title' }, [s.name]),
              el('p', { class: 'text-secondary small mb-3' }, [loc]),
              el('ul', { class: 'list-unstyled small mb-0' }, [
                el('li', {}, ['Pets listed: ', el('strong', {}, [String(listed)])]),
                el('li', {}, ['Completed adoptions: ', el('strong', {}, [String(completed)])]),
                el('li', {}, ['Approval rate: ', el('strong', {}, [rateText])]),
              ]),
            ]),
          ]),
        ]);
      })),
  ]);
}

function viewStaff() {
  if (!state.currentEmployee) {
    return el('div', { class: 'container py-5' }, [
      el('div', { class: 'alert alert-warning' }, [
        'The staff dashboard requires a shelter employee session. Use ',
        el('strong', {}, ['Staff login']),
        ' in the navbar.',
      ]),
      el('button', { type: 'button', class: 'btn btn-dark', onclick: () => openEmployeeLoginModal() }, ['Open staff login']),
    ]);
  }
  return buildStaffDashboard(state, {
    el,
    api,
    setToast,
    render,
    loadStaffDashboard,
    loadData,
    openRegisterModal,
    openUserLoginModal,
  });
}

function render() {
  const app = document.getElementById('app');
  if (!app) return;
  app.replaceChildren();

  const main = el('main', {});
  const tb = toastBanner();
  if (tb) main.appendChild(tb);
  if (state.route === 'home') main.appendChild(viewHome());
  else if (state.route === 'pets') main.appendChild(viewPets());
  else if (state.route === 'shelters') main.appendChild(viewShelters());
  else if (state.route === 'profile') main.appendChild(viewAdopterProfile());
  else if (state.route === 'staff') main.appendChild(viewStaff());
  else main.appendChild(viewHome());

  app.appendChild(navBar());
  app.appendChild(main);
}

function resolveRouteFromHash() {
  const hash = (window.location.hash || '#home').replace('#', '');
  const allowed = ['home', 'pets', 'shelters', 'profile', 'staff'];
  let route = allowed.includes(hash) ? hash : 'home';
  if (route === 'profile' && !state.currentUser) {
    route = 'home';
    window.history.replaceState({}, '', '#home');
  }
  if (route === 'staff' && !state.currentEmployee) {
    route = 'home';
    window.history.replaceState({}, '', '#home');
  }
  return route;
}

function init() {
  state.currentUser = loadSavedUser();
  state.currentEmployee = loadSavedEmployee();
  if (state.currentUser && state.currentEmployee) {
    clearEmployeeSession();
  }
  setupAuthModals();
  state.route = resolveRouteFromHash();
  window.addEventListener('hashchange', () => {
    state.route = resolveRouteFromHash();
    render();
    if (state.route === 'staff' && state.currentEmployee) loadStaffDashboard();
  });
  render();
  loadData();
  if (state.route === 'staff' && state.currentEmployee) loadStaffDashboard();
}

init();
