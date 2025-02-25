// site.js - Helper functions for the chat component

// Scroll the messages container to the bottom
window.scrollToBottom = function (element) {
    if (element) {
        element.scrollTop = element.scrollHeight;
    }
};

// Automatically adjust the height of the textarea based on content
window.autoAdjustHeight = function (element) {
    if (element) {
        // Reset height to auto to get the correct scrollHeight
        element.style.height = 'auto';
        // Set new height (min 38px, max 150px)
        element.style.height = Math.min(Math.max(element.scrollHeight, 38), 150) + 'px';
    }
};

// Session persistence functions
window.saveThreadId = function (threadId) {
    if (threadId) {
        console.log('Saving thread ID to localStorage:', threadId);
        localStorage.setItem('currentThreadId', threadId);
    }
};

window.loadThreadId = function () {
    const threadId = localStorage.getItem('currentThreadId');
    console.log('Loading thread ID from localStorage:', threadId);
    return threadId || '';
};

window.saveCurrentPath = function (path) {
    if (path) {
        console.log('Saving current path to localStorage:', path);
        localStorage.setItem('currentPath', path);
    }
};

window.loadCurrentPath = function () {
    const path = localStorage.getItem('currentPath');
    console.log('Loading current path from localStorage:', path);
    return path || '/';
};

// Clear session data
window.clearSessionData = function () {
    localStorage.removeItem('currentThreadId');
    localStorage.removeItem('currentPath');
    console.log('Cleared session data from localStorage');
    return true;
};

// Force refresh UI state
window.forceRefresh = function () {
    // This function can be called to force component refresh
    console.log('Forcing UI refresh');
    return true;
};

window.initializeDropdowns = () => {
    var dropdownElementList = [].slice.call(document.querySelectorAll('.dropdown-toggle'));
    dropdownElementList.forEach(function (dropdownToggleEl) {
        // Reinitialize the Bootstrap dropdown
        new bootstrap.Dropdown(dropdownToggleEl);
    });
};

// Initialize bootstrap tooltips and popovers
document.addEventListener('DOMContentLoaded', function () {
    // Initialize dropdown menus manually if needed
    const dropdownToggleList = document.querySelectorAll('.dropdown-toggle');
    if (dropdownToggleList.length > 0 && typeof bootstrap !== 'undefined') {
        dropdownToggleList.forEach(function (dropdownToggle) {
            new bootstrap.Dropdown(dropdownToggle);
        });
    }

    // Initialize Bootstrap components if needed
    if (typeof bootstrap !== 'undefined') {
        // Initialize tooltips
        var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
        tooltipTriggerList.map(function (tooltipTriggerEl) {
            return new bootstrap.Tooltip(tooltipTriggerEl);
        });

        // Initialize popovers
        var popoverTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="popover"]'));
        popoverTriggerList.map(function (popoverTriggerEl) {
            return new bootstrap.Popover(popoverTriggerEl);
        });
    }
});

// Custom dropdown functionality
window.initializeCustomDropdowns = function() {
    // Get all dropdown toggles
    const toggles = document.querySelectorAll('.custom-dropdown-toggle');
    
    // Add click handlers to all toggles
    toggles.forEach(toggle => {
        toggle.addEventListener('click', function(e) {
            e.preventDefault();
            e.stopPropagation();
            
            // Get the dropdown menu
            const dropdown = this.nextElementSibling;
            
            // Toggle the 'show' class
            dropdown.classList.toggle('show');
            
            // Close other dropdowns
            toggles.forEach(otherToggle => {
                if (otherToggle !== toggle) {
                    const otherDropdown = otherToggle.nextElementSibling;
                    if (otherDropdown.classList.contains('show')) {
                        otherDropdown.classList.remove('show');
                    }
                }
            });
        });
    });
    
    // Close dropdowns when clicking outside
    document.addEventListener('click', function(e) {
        const dropdowns = document.querySelectorAll('.custom-dropdown-menu');
        dropdowns.forEach(dropdown => {
            if (dropdown.classList.contains('show') && !dropdown.contains(e.target)) {
                dropdown.classList.remove('show');
            }
        });
    });
};