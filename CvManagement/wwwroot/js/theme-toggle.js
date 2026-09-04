window.applyTheme = function (theme) {
    document.documentElement.setAttribute('data-bs-theme', theme);
    localStorage.setItem('cv-theme', theme);
};

window.getStoredTheme = function () {
    return localStorage.getItem('cv-theme') || 'light';
};
