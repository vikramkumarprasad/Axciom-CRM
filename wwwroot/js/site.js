// AcxiomCRM Client-side Interactivity

document.addEventListener('DOMContentLoaded', function () {
    // 1. Collapsible Sidebar Controller
    const sidebarToggle = document.getElementById('sidebarToggle');
    const wrapper = document.getElementById('wrapper');

    if (sidebarToggle && wrapper) {
        // Restore user's previous collapse preference on page load (desktop only)
        if (window.innerWidth >= 768 && localStorage.getItem('acxiom_sidebar_toggled') === 'true') {
            wrapper.classList.add('toggled');
        }

        sidebarToggle.addEventListener('click', function (e) {
            e.preventDefault();
            wrapper.classList.toggle('toggled');
            
            // Persist preference
            if (window.innerWidth >= 768) {
                localStorage.setItem('acxiom_sidebar_toggled', wrapper.classList.contains('toggled'));
            }
        });
    }

    // 2. Auto-close mobile sidebar when clicking outside
    document.addEventListener('click', function (e) {
        if (window.innerWidth < 768 && wrapper && wrapper.classList.contains('toggled')) {
            const sidebar = document.getElementById('sidebar-wrapper');
            if (sidebar && !sidebar.contains(e.target) && !sidebarToggle.contains(e.target)) {
                wrapper.classList.remove('toggled');
            }
        }
    });
});
