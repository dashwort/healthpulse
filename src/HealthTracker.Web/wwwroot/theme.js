window.healthTrackerTheme = {
    get: () => localStorage.getItem("healthtracker.theme"),
    set: value => localStorage.setItem("healthtracker.theme", value),
    prefersDark: () => window.matchMedia("(prefers-color-scheme: dark)").matches,
    isMobile: () => window.matchMedia("(max-width: 959.98px)").matches
};
