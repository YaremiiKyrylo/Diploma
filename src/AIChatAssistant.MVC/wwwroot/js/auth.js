// JWT helpers for MVC client-side auth (login stores token in localStorage)
const AUTH_TOKEN_KEY = 'aiChatToken';
const AUTH_USER_KEY = 'aiChatUser';

function getAuthToken() {
    return localStorage.getItem(AUTH_TOKEN_KEY);
}

function setAuthSession(token, email) {
    localStorage.setItem(AUTH_TOKEN_KEY, token);
    localStorage.setItem(AUTH_USER_KEY, email || '');
    window.dispatchEvent(new Event('auth-changed'));
}

function clearAuthSession() {
    localStorage.removeItem(AUTH_TOKEN_KEY);
    localStorage.removeItem(AUTH_USER_KEY);
    window.dispatchEvent(new Event('auth-changed'));
}

function isLoggedIn() {
    return !!getAuthToken();
}

function getAuthHeaders(contentTypeJson = true) {
    const headers = {};
    const token = getAuthToken();
    if (token) headers['Authorization'] = 'Bearer ' + token;
    if (contentTypeJson) headers['Content-Type'] = 'application/json';
    return headers;
}

function parseJwtPayload(token) {
    try {
        const base64 = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
        const json = decodeURIComponent(atob(base64).split('').map(c =>
            '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2)).join(''));
        return JSON.parse(json);
    } catch {
        return null;
    }
}

function isAdmin() {
    const token = getAuthToken();
    if (!token) return false;
    const payload = parseJwtPayload(token);
    if (!payload) return false;
    const role = payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']
        || payload.role;
    if (Array.isArray(role)) return role.includes('Admin');
    return role === 'Admin';
}

function updateAuthNav() {
    const loggedIn = isLoggedIn();
    const email = localStorage.getItem(AUTH_USER_KEY) || 'User';

    document.querySelectorAll('[data-auth="guest"]').forEach(el => {
        el.style.display = loggedIn ? 'none' : '';
    });
    document.querySelectorAll('[data-auth="user"]').forEach(el => {
        el.style.display = loggedIn ? '' : 'none';
    });

    const nameEl = document.getElementById('navUserName');
    if (nameEl) nameEl.textContent = email;

    const adminEl = document.getElementById('adminNavItem');
    if (adminEl) adminEl.style.display = (loggedIn && isAdmin()) ? '' : 'none';

    const chatEl = document.getElementById('chatWidgetContainer');
    if (chatEl) chatEl.style.display = loggedIn ? '' : 'none';
}

document.addEventListener('DOMContentLoaded', updateAuthNav);
window.addEventListener('auth-changed', updateAuthNav);
