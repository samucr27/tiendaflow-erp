// Presentation-only helper for the mobile navigation drawer.
// The checkbox keeps the drawer usable without a framework; this script
// closes it after a navigation link or with Escape so the next screen is
// visible immediately. It does not touch authentication or routing.
(() => {
    const toggle = document.getElementById("nav-drawer");
    if (!toggle) {
        return;
    }

    const close = () => {
        toggle.checked = false;
    };

    document.addEventListener("click", (event) => {
        const link = event.target.closest(".sidebar a.nav-item, .sidebar button.nav-item");
        if (link) {
            close();
        }
    });

    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape") {
            close();
        }
    });
})();
