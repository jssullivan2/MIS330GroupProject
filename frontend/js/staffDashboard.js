/**
 * Shelter staff dashboard (Bootstrap tabs). Expects `state.staff` and `state.currentEmployee`.
 */

function tabButton(el, id, label, active, onClick) {
  const btn = el('button', {
    class: `nav-link ${active ? 'active' : ''}`,
    type: 'button',
    role: 'tab',
    id: `staff-tab-${id}`,
    'aria-controls': `staff-pane-${id}`,
    'aria-selected': active ? 'true' : 'false',
  }, [label]);
  btn.addEventListener('click', (e) => {
    e.preventDefault();
    onClick(id);
  });
  return el('li', { class: 'nav-item', role: 'presentation' }, [btn]);
}

function filterUsers(users, kw) {
  if (!kw.trim()) return users;
  const q = kw.trim().toLowerCase();
  return users.filter(
    (u) =>
      String(u.userName || '').toLowerCase().includes(q) ||
      String(u.userEmail || '').toLowerCase().includes(q) ||
      String(u.id).includes(q),
  );
}

function filterPets(pets, kw) {
  if (!kw.trim()) return pets;
  const q = kw.trim().toLowerCase();
  return pets.filter(
    (p) =>
      String(p.name || '').toLowerCase().includes(q) ||
      String(p.breed || '').toLowerCase().includes(q) ||
      String(p.species || '').toLowerCase().includes(q) ||
      String(p.status || '').toLowerCase().includes(q) ||
      String(p.shelterName || '').toLowerCase().includes(q),
  );
}

function sortPets(pets, sortKey) {
  const copy = [...pets];
  const cmp = (a, b) => {
    if (sortKey === 'id-desc') return b.id - a.id;
    if (sortKey === 'id-asc') return a.id - b.id;
    if (sortKey === 'status-asc') return String(a.status).localeCompare(String(b.status));
    if (sortKey === 'species-asc') return String(a.species).localeCompare(String(b.species));
    return String(a.name).localeCompare(String(b.name));
  };
  copy.sort(cmp);
  return copy;
}

/** Drops rows whose pet is already fully adopted (no longer needs approve/delete). */
function excludeAppsForAdoptedPets(apps, pets) {
  const statusByPetId = new Map((pets || []).map((p) => [p.id, p.status]));
  return apps.filter((a) => statusByPetId.get(a.petId) !== 'Adopted');
}

function filterApps(apps, statusFilter, kw) {
  let list = apps;
  if (statusFilter === 'pending') list = list.filter((a) => !a.isAdopted);
  if (!kw.trim()) return list;
  const q = kw.trim().toLowerCase();
  return list.filter(
    (a) =>
      String(a.userEmail || '').toLowerCase().includes(q) ||
      String(a.userName || '').toLowerCase().includes(q) ||
      String(a.petName || '').toLowerCase().includes(q) ||
      String(a.userId).includes(q) ||
      String(a.petId).includes(q),
  );
}

export function buildStaffDashboard(state, h) {
  const { el, api, setToast, render, loadStaffDashboard, loadData, openRegisterModal, openUserLoginModal } = h;
  const empId = state.currentEmployee.id;
  const st = state.staff;

  const header = el('div', { class: 'd-flex flex-wrap justify-content-between align-items-center gap-2 mb-3' }, [
    el('div', {}, [
      el('h1', { class: 'h3 mb-1' }, ['Staff dashboard']),
      el('p', { class: 'text-secondary mb-0 small' }, [
        'Signed in as ',
        el('strong', {}, [state.currentEmployee.employeeName]),
        '. Use the tabs below for users, pets, applications, and shelters.',
      ]),
    ]),
    el('div', { class: 'd-flex flex-wrap gap-2' }, [
      el('button', {
        type: 'button',
        class: 'btn btn-outline-primary btn-sm',
        onclick: () => {
          openRegisterModal();
        },
      }, ['Register adopter (modal)']),
      el('button', {
        type: 'button',
        class: 'btn btn-outline-primary btn-sm',
        onclick: () => {
          openUserLoginModal();
        },
      }, ['Adopter sign in (modal)']),
      el('button', {
        type: 'button',
        class: 'btn btn-outline-secondary btn-sm',
        onclick: () => {
          loadStaffDashboard();
        },
      }, [el('i', { class: 'bi bi-arrow-clockwise me-1' }), 'Refresh data']),
      el('button', {
        type: 'button',
        class: 'btn btn-outline-secondary btn-sm',
        onclick: async () => {
          try {
            await loadData();
            setToast('success', 'Public pet list refreshed from API.');
          } catch {
            setToast('danger', 'Could not refresh public pets.');
          }
        },
      }, ['Sync browse pets']),
    ]),
  ]);

  if (st.loading) {
    return el('div', { class: 'container py-4' }, [
      header,
      el('div', { class: 'alert alert-info' }, ['Loading staff data from MySQL…']),
    ]);
  }

  if (st.error) {
    return el('div', { class: 'container py-4' }, [
      header,
      el('div', { class: 'alert alert-danger' }, [
        st.error,
        ' ',
        el('button', { type: 'button', class: 'btn btn-sm btn-outline-danger ms-2', onclick: () => loadStaffDashboard() }, [
          'Retry',
        ]),
      ]),
    ]);
  }

  const users = st.users || [];
  const pets = st.pets || [];
  const applications = st.applications || [];
  const shelters = st.shelters || [];

  const setTab = (id) => {
    st.tab = id;
    render();
  };

  const tabs = el('ul', { class: 'nav nav-tabs mb-3', role: 'tablist' }, [
    tabButton(el, 'users', 'User management', st.tab === 'users', setTab),
    tabButton(el, 'pets', 'Pets (CRUD & search)', st.tab === 'pets', setTab),
    tabButton(el, 'applications', 'Applications', st.tab === 'applications', setTab),
    tabButton(el, 'shelters', 'Shelters', st.tab === 'shelters', setTab),
  ]);

  // --- Users tab
  const userKw = el('input', {
    type: 'search',
    class: 'form-control form-control-sm',
    placeholder: 'Search by name, email, or ID…',
    value: st.userKeyword,
  });
  userKw.addEventListener('input', () => {
    st.userKeyword = userKw.value;
    render();
  });

  const userRows = filterUsers(users, st.userKeyword).map((u) =>
    el('tr', {}, [
      el('td', {}, [String(u.id)]),
      el('td', {}, [u.userName]),
      el('td', {}, [u.userEmail]),
      el('td', {}, [u.typePreference || '—']),
      el('td', {}, [
        el('button', {
          type: 'button',
          class: 'btn btn-sm btn-outline-primary',
          onclick: async () => {
            st.selectedUserId = u.id;
            st.userDetail = null;
            render();
            try {
              st.userDetail = await api.staffGetUser(empId, u.id);
            } catch (err) {
              setToast('danger', err.message || 'Could not load profile.');
            }
            render();
          },
        }, ['Profile']),
      ]),
    ]),
  );

  const userDetailCard =
    st.selectedUserId && st.userDetail
      ? el('div', { class: 'card border-0 shadow-sm mt-3' }, [
          el('div', { class: 'card-body' }, [
            el('h3', { class: 'h6' }, [`User #${st.userDetail.id} profile`]),
            el('dl', { class: 'row small mb-0' }, [
              el('dt', { class: 'col-sm-3' }, ['Name']),
              el('dd', { class: 'col-sm-9' }, [st.userDetail.userName]),
              el('dt', { class: 'col-sm-3' }, ['Email']),
              el('dd', { class: 'col-sm-9' }, [st.userDetail.userEmail]),
              el('dt', { class: 'col-sm-3' }, ['Type preference']),
              el('dd', { class: 'col-sm-9' }, [st.userDetail.typePreference || '—']),
              el('dt', { class: 'col-sm-3' }, ['Applications']),
              el('dd', { class: 'col-sm-9' }, [String(st.userDetail.applicationCount)]),
            ]),
            el('p', { class: 'text-muted small mb-0 mt-2' }, [
              'Passwords are not shown. Adopters sign in with email + password from the navbar.',
            ]),
          ]),
        ])
      : st.selectedUserId && !st.userDetail
        ? el('div', { class: 'text-secondary small mt-2' }, ['Loading profile…'])
        : el('p', { class: 'text-secondary small mt-2 mb-0' }, ['Select a user and click Profile to view details.']);

  const paneUsers = el('div', {
    class: `tab-pane fade ${st.tab === 'users' ? 'show active' : ''}`,
    id: 'staff-pane-users',
    role: 'tabpanel',
  }, [
    el('p', { class: 'text-secondary' }, [
      'View adopters from the ',
      el('code', {}, ['User']),
      ' table. Open ',
      el('strong', {}, ['Register adopter']),
      ' to create accounts; adopters use ',
      el('strong', {}, ['Adopter sign in']),
      ' for sessions.',
    ]),
    el('div', { class: 'row mb-3' }, [
      el('div', { class: 'col-md-6' }, [
        el('label', { class: 'form-label small' }, ['Keyword search']),
        userKw,
      ]),
    ]),
    el('div', { class: 'table-responsive' }, [
      el('table', { class: 'table table-sm table-hover align-middle' }, [
        el('thead', {}, [
          el('tr', {}, [
            el('th', {}, ['ID']),
            el('th', {}, ['Name']),
            el('th', {}, ['Email']),
            el('th', {}, ['Preference']),
            el('th', {}, ['']),
          ]),
        ]),
        el('tbody', {}, userRows.length ? userRows : [el('tr', {}, [el('td', { colspan: '5', class: 'text-secondary' }, ['No users.'])])]),
      ]),
    ]),
    userDetailCard,
  ]);

  // --- Pets tab
  const petKw = el('input', {
    type: 'search',
    class: 'form-control form-control-sm',
    placeholder: 'Search name, breed, species, status…',
    value: st.petKeyword,
  });
  petKw.addEventListener('input', () => {
    st.petKeyword = petKw.value;
    render();
  });

  const petSort = el('select', { class: 'form-select form-select-sm' });
  [
    ['name-asc', 'Name A–Z'],
    ['id-asc', 'ID ascending'],
    ['id-desc', 'ID descending'],
    ['species-asc', 'Species'],
    ['status-asc', 'Status'],
  ].forEach(([v, lab]) => {
    petSort.appendChild(el('option', { value: v }, [lab]));
  });
  petSort.value = st.petSort;
  petSort.addEventListener('change', () => {
    st.petSort = petSort.value;
    render();
  });

  const petFiltered = sortPets(filterPets(pets, st.petKeyword), st.petSort);

  const petFormName = el('input', { type: 'text', class: 'form-control', id: 'staffPetName', maxlength: '100' });
  const petFormBreed = el('input', { type: 'text', class: 'form-control', id: 'staffPetBreed', maxlength: '100' });
  const petFormType = el('select', { class: 'form-select', id: 'staffPetType' }, [
    el('option', { value: 'Dog' }, ['Dog']),
    el('option', { value: 'Cat' }, ['Cat']),
  ]);
  const petFormShelter = el('select', { class: 'form-select', id: 'staffPetShelter' });
  const myShelterId = state.currentEmployee.shelterId;
  if (myShelterId != null) {
    const s = shelters.find((x) => x.id === myShelterId);
    if (s) {
      petFormShelter.appendChild(el('option', { value: String(s.id) }, [`#${s.id} ${s.name}`]));
    } else {
      petFormShelter.appendChild(el('option', { value: '' }, ['Shelter not in directory']));
    }
    petFormShelter.disabled = true;
  } else {
    petFormShelter.appendChild(
      el('option', { value: '', disabled: 'true' }, ['No shelter on your account — cannot manage pets']),
    );
    petFormShelter.disabled = true;
  }

  const petFormFeedback = el('div', { class: 'text-danger small', id: 'staffPetFormFb' });

  const resetPetForm = () => {
    st.petEditingId = null;
    petFormName.value = '';
    petFormBreed.value = '';
    petFormType.value = 'Dog';
    petFormShelter.value = myShelterId != null ? String(myShelterId) : '';
    petFormFeedback.textContent = '';
  };

  if (st.petEditingId) {
    const editing = pets.find((p) => p.id === st.petEditingId);
    if (editing) {
      petFormName.value = editing.name;
      petFormBreed.value = editing.breed || '';
      petFormType.value = editing.species === 'Cat' ? 'Cat' : 'Dog';
      petFormShelter.value = editing.shelterId ? String(editing.shelterId) : '';
    }
  }

  const savePetBtn = el('button', { type: 'button', class: 'btn btn-primary' }, ['Save pet']);
  savePetBtn.addEventListener('click', async () => {
    petFormFeedback.textContent = '';
    const name = petFormName.value.trim();
    if (!name) {
      petFormFeedback.textContent = 'Pet name is required.';
      return;
    }
    const breed = petFormBreed.value.trim() || null;
    const petType = petFormType.value;
    if (myShelterId == null) {
      petFormFeedback.textContent = 'Your account has no shelter; you cannot save pets.';
      return;
    }
    const sid = myShelterId;
    const body = {
      petName: name,
      petBreed: breed,
      petType,
      shelterId: sid,
      employeeId: empId,
    };
    savePetBtn.disabled = true;
    try {
      if (st.petEditingId) {
        await api.staffUpdatePet(empId, st.petEditingId, body);
        setToast('success', 'Pet updated.');
      } else {
        await api.staffCreatePet(empId, body);
        setToast('success', 'Pet created.');
      }
      resetPetForm();
      await loadStaffDashboard();
      await loadData();
    } catch (err) {
      petFormFeedback.textContent = err.message || 'Save failed.';
    } finally {
      savePetBtn.disabled = false;
    }
  });

  const cancelEditBtn = el('button', { type: 'button', class: 'btn btn-outline-secondary' }, ['Clear form']);
  cancelEditBtn.addEventListener('click', () => {
    resetPetForm();
    render();
  });

  const petFormCard = el('div', { class: 'card border-0 shadow-sm mb-4' }, [
    el('div', { class: 'card-body' }, [
      el('h3', { class: 'h6 mb-3' }, [st.petEditingId ? `Edit pet #${st.petEditingId}` : 'Create pet listing']),
      el('div', { class: 'row g-3' }, [
        el('div', { class: 'col-md-4' }, [
          el('label', { class: 'form-label small', for: 'staffPetName' }, ['Name']),
          petFormName,
        ]),
        el('div', { class: 'col-md-4' }, [
          el('label', { class: 'form-label small', for: 'staffPetBreed' }, ['Breed']),
          petFormBreed,
        ]),
        el('div', { class: 'col-md-2' }, [
          el('label', { class: 'form-label small', for: 'staffPetType' }, ['Type']),
          petFormType,
        ]),
        el('div', { class: 'col-md-2' }, [
          el('label', { class: 'form-label small', for: 'staffPetShelter' }, ['Shelter']),
          petFormShelter,
        ]),
      ]),
      petFormFeedback,
      el('div', { class: 'mt-3 d-flex gap-2' }, [savePetBtn, cancelEditBtn]),
    ]),
  ]);

  const petRows = petFiltered.map((p) =>
    el('tr', {}, [
      el('td', {}, [String(p.id)]),
      el('td', {}, [p.name]),
      el('td', {}, [p.species]),
      el('td', {}, [p.breed || '—']),
      el('td', {}, [p.shelterName]),
      el('td', {}, [
        el('span', {
          class: `badge ${p.status === 'Available' ? 'bg-success' : p.status === 'Pending' ? 'bg-warning text-dark' : 'bg-secondary'}`,
        }, [p.status]),
      ]),
      el('td', {}, [
        el('div', { class: 'btn-group btn-group-sm' }, [
          el('button', {
            type: 'button',
            class: 'btn btn-outline-primary',
            onclick: () => {
              st.petEditingId = p.id;
              render();
            },
          }, ['Edit']),
          el('button', {
            type: 'button',
            class: 'btn btn-outline-danger',
            onclick: async () => {
              if (!window.confirm(`Delete ${p.name} and related application rows?`)) return;
              try {
                await api.staffDeletePet(empId, p.id);
                setToast('success', 'Pet deleted.');
                if (st.petEditingId === p.id) resetPetForm();
                await loadStaffDashboard();
                await loadData();
              } catch (err) {
                setToast('danger', err.message || 'Delete failed.');
              }
            },
          }, ['Delete']),
        ]),
      ]),
    ]),
  );

  const panePets = el('div', {
    class: `tab-pane fade ${st.tab === 'pets' ? 'show active' : ''}`,
    id: 'staff-pane-pets',
    role: 'tabpanel',
  }, [
    el('p', { class: 'text-secondary' }, [
      'Create, edit, or remove pets for ',
      el('strong', {}, ['your shelter only']),
      '. Search and sort the table below.',
    ]),
    petFormCard,
    el('div', { class: 'row g-3 mb-3' }, [
      el('div', { class: 'col-md-6' }, [
        el('label', { class: 'form-label small' }, ['Keyword search']),
        petKw,
      ]),
      el('div', { class: 'col-md-4' }, [
        el('label', { class: 'form-label small' }, ['Sort']),
        petSort,
      ]),
    ]),
    el('div', { class: 'table-responsive' }, [
      el('table', { class: 'table table-sm table-hover align-middle' }, [
        el('thead', {}, [
          el('tr', {}, [
            el('th', {}, ['ID']),
            el('th', {}, ['Name']),
            el('th', {}, ['Type']),
            el('th', {}, ['Breed']),
            el('th', {}, ['Shelter']),
            el('th', {}, ['Status']),
            el('th', {}, ['']),
          ]),
        ]),
        el('tbody', {}, petRows.length ? petRows : [el('tr', {}, [el('td', { colspan: '7', class: 'text-secondary' }, ['No pets match.'])])]),
      ]),
    ]),
  ]);

  // --- Applications tab
  const appStatus = el('select', { class: 'form-select form-select-sm' });
  if (st.appStatusFilter === 'adopted') st.appStatusFilter = '';
  [['', 'All open requests'], ['pending', 'Pending only']].forEach(([v, lab]) => {
    appStatus.appendChild(el('option', { value: v }, [lab]));
  });
  appStatus.value = st.appStatusFilter === 'pending' ? 'pending' : '';
  appStatus.addEventListener('change', () => {
    st.appStatusFilter = appStatus.value;
    render();
  });

  const appKw = el('input', {
    type: 'search',
    class: 'form-control form-control-sm',
    placeholder: 'Search user, pet, IDs…',
    value: st.appKeyword,
  });
  appKw.addEventListener('input', () => {
    st.appKeyword = appKw.value;
    render();
  });

  const newAppUser = el('input', { type: 'number', class: 'form-control', id: 'staffNewAppUser', min: '1', step: '1' });
  const newAppPet = el('input', { type: 'number', class: 'form-control', id: 'staffNewAppPet', min: '1', step: '1' });
  const newAppAdopted = el('input', { type: 'checkbox', class: 'form-check-input', id: 'staffNewAppAdopted' });
  const newAppFb = el('div', { class: 'text-danger small' });
  const submitAppBtn = el('button', { type: 'button', class: 'btn btn-primary btn-sm' }, ['Submit / update application']);
  submitAppBtn.addEventListener('click', async () => {
    newAppFb.textContent = '';
    const userId = Number(newAppUser.value);
    const petId = Number(newAppPet.value);
    if (!Number.isFinite(userId) || userId < 1 || !Number.isFinite(petId) || petId < 1) {
      newAppFb.textContent = 'Enter valid User ID and Pet ID.';
      return;
    }
    submitAppBtn.disabled = true;
    try {
      await api.staffUpsertApplication(empId, {
        userId,
        petId,
        isAdopted: Boolean(newAppAdopted.checked),
      });
      setToast('success', 'Application saved.');
      newAppUser.value = '';
      newAppPet.value = '';
      newAppAdopted.checked = false;
      await loadStaffDashboard();
      await loadData();
    } catch (err) {
      newAppFb.textContent = err.message || 'Failed.';
    } finally {
      submitAppBtn.disabled = false;
    }
  });

  const appsForManage = excludeAppsForAdoptedPets(applications, pets);
  const appRows = filterApps(appsForManage, st.appStatusFilter, st.appKeyword).map((a) =>
    el('tr', {}, [
      el('td', {}, [String(a.userId)]),
      el('td', {}, [a.userEmail]),
      el('td', {}, [String(a.petId)]),
      el('td', {}, [a.petName]),
      el('td', {}, [a.petType]),
      el('td', {}, [
        el('span', { class: `badge ${a.isAdopted ? 'bg-secondary' : 'bg-warning text-dark'}` }, [
          a.isAdopted ? 'Adopted (1)' : 'Pending (0)',
        ]),
      ]),
      el('td', {}, [a.shelterId != null ? String(a.shelterId) : '—']),
      el('td', {}, [
        el('div', { class: 'btn-group btn-group-sm' }, [
          el('button', {
            type: 'button',
            class: 'btn btn-outline-success',
            disabled: Boolean(a.isAdopted),
            onclick: async () => {
              try {
                await api.staffUpsertApplication(empId, { userId: a.userId, petId: a.petId, isAdopted: true });
                setToast('success', 'Marked adopted.');
                await loadStaffDashboard();
                await loadData();
              } catch (err) {
                setToast('danger', err.message || 'Failed.');
              }
            },
          }, ['Approve']),
          el('button', {
            type: 'button',
            class: 'btn btn-outline-danger',
            onclick: async () => {
              if (!window.confirm('Remove this application row?')) return;
              try {
                await api.staffDeleteApplication(empId, { userId: a.userId, petId: a.petId });
                setToast('success', 'Application removed.');
                await loadStaffDashboard();
                await loadData();
              } catch (err) {
                setToast('danger', err.message || 'Failed.');
              }
            },
          }, ['Delete']),
        ]),
      ]),
    ]),
  );

  const paneApps = el('div', {
    class: `tab-pane fade ${st.tab === 'applications' ? 'show active' : ''}`,
    id: 'staff-pane-applications',
    role: 'tabpanel',
  }, [
    el('p', { class: 'text-secondary' }, [
      'Open requests from ',
      el('code', {}, ['AdoptionApplication']),
      ' for pets that are not yet fully adopted. ',
      el('strong', {}, ['Pending (0)']),
      ' / ',
      el('strong', {}, ['Adopted (1)']),
      ' in the State column reflect ',
      el('code', {}, ['IsAdopted']),
      ' for that row. Approve sets it to ',
      el('code', {}, ['1']),
      '; use Delete to remove a request. Pets already adopted no longer appear here. ',
      'You only see applications for pets at ',
      el('strong', {}, ['your shelter']),
      ' (Employee.ShelterID = Pet.ShelterID).',
    ]),
    el('div', { class: 'card border-0 shadow-sm mb-4' }, [
      el('div', { class: 'card-body' }, [
        el('h3', { class: 'h6 mb-3' }, ['Submit or update an application']),
        el('div', { class: 'row g-3 align-items-end' }, [
          el('div', { class: 'col-md-3' }, [
            el('label', { class: 'form-label small', for: 'staffNewAppUser' }, ['User ID']),
            newAppUser,
          ]),
          el('div', { class: 'col-md-3' }, [
            el('label', { class: 'form-label small', for: 'staffNewAppPet' }, ['Pet ID']),
            newAppPet,
          ]),
          el('div', { class: 'col-md-3' }, [
            el('div', { class: 'form-check mt-4' }, [
              newAppAdopted,
              el('label', { class: 'form-check-label', for: 'staffNewAppAdopted' }, ['Mark adopted now']),
            ]),
          ]),
          el('div', { class: 'col-md-3' }, [submitAppBtn]),
        ]),
        newAppFb,
      ]),
    ]),
    el('div', { class: 'row g-3 mb-3' }, [
      el('div', { class: 'col-md-4' }, [
        el('label', { class: 'form-label small' }, ['Status filter']),
        appStatus,
      ]),
      el('div', { class: 'col-md-6' }, [
        el('label', { class: 'form-label small' }, ['Keyword search']),
        appKw,
      ]),
    ]),
    el('div', { class: 'table-responsive' }, [
      el('table', { class: 'table table-sm table-hover align-middle' }, [
        el('thead', {}, [
          el('tr', {}, [
            el('th', {}, ['User']),
            el('th', {}, ['Email']),
            el('th', {}, ['Pet']),
            el('th', {}, ['Pet name']),
            el('th', {}, ['Type']),
            el('th', {}, ['State']),
            el('th', {}, ['Shelter']),
            el('th', {}, ['Manage']),
          ]),
        ]),
        el('tbody', {}, appRows.length ? appRows : [el('tr', {}, [el('td', { colspan: '8', class: 'text-secondary' }, ['No applications.'])])]),
      ]),
    ]),
  ]);

  // --- Shelters tab
  const shelterRows = shelters.map((s) =>
    el('tr', {}, [
      el('td', {}, [String(s.id)]),
      el('td', {}, [s.name]),
      el('td', {}, [s.address]),
      el('td', {}, [String(s.petCount)]),
      el('td', {}, [String(s.applicationCount)]),
    ]),
  );

  const paneShelters = el('div', {
    class: `tab-pane fade ${st.tab === 'shelters' ? 'show active' : ''}`,
    id: 'staff-pane-shelters',
    role: 'tabpanel',
  }, [
    el('p', { class: 'text-secondary' }, [
      'Shelter directory with live counts of pets and applications linked through those pets.',
    ]),
    el('div', { class: 'table-responsive' }, [
      el('table', { class: 'table table-sm table-hover align-middle' }, [
        el('thead', {}, [
          el('tr', {}, [
            el('th', {}, ['ID']),
            el('th', {}, ['Name']),
            el('th', {}, ['Address']),
            el('th', {}, ['Pets']),
            el('th', {}, ['Applications']),
          ]),
        ]),
        el('tbody', {}, shelterRows.length ? shelterRows : [el('tr', {}, [el('td', { colspan: '5', class: 'text-secondary' }, ['No shelters.'])])]),
      ]),
    ]),
  ]);

  const tabContent = el('div', { class: 'tab-content' }, [paneUsers, panePets, paneApps, paneShelters]);

  return el('div', { class: 'container py-4' }, [header, tabs, tabContent]);
}
