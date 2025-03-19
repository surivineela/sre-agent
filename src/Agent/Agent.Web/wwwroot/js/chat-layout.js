// Function to initialize chat layout
window.initializeChatLayout = () => {
    // Adjust heights for full viewport coverage
    function adjustLayout() {
        const windowHeight = window.innerHeight;
        const mainContent = document.querySelector('.main-content');
        const footer = document.querySelector('.app-footer');

        if (mainContent && footer) {
            const footerHeight = footer.offsetHeight;
            mainContent.style.height = `${windowHeight - footerHeight}px`;
        }

        // Ensure message container takes available space
        const chatMain = document.querySelector('.chat-main');
        if (chatMain) {
            const chatHeader = chatMain.querySelector('.chat-header');
            const chatInput = chatMain.querySelector('.chat-input-container');
            const messagesContainer = chatMain.querySelector('.messages-container');

            if (chatHeader && chatInput && messagesContainer) {
                const availableHeight = chatMain.offsetHeight - chatHeader.offsetHeight - chatInput.offsetHeight;
                messagesContainer.style.height = `${availableHeight}px`;
            }
        }
    }

    // Call initially
    adjustLayout();

    // Listen for window resize
    window.addEventListener('resize', adjustLayout);

    // Return cleanup function
    return () => {
        window.removeEventListener('resize', adjustLayout);
    };
};

// Auto-resize textarea as user types
window.autoAdjustHeight = (element) => {
    if (element) {
        element.style.height = 'auto';
        element.style.height = Math.min(element.scrollHeight, 150) + 'px';

        // Also adjust the message container size when textarea resizes
        const chatMain = document.querySelector('.chat-main');
        if (chatMain) {
            const chatHeader = chatMain.querySelector('.chat-header');
            const chatInput = chatMain.querySelector('.chat-input-container');
            const messagesContainer = chatMain.querySelector('.messages-container');

            if (chatHeader && chatInput && messagesContainer) {
                const availableHeight = chatMain.offsetHeight - chatHeader.offsetHeight - chatInput.offsetHeight;
                messagesContainer.style.height = `${availableHeight}px`;
            }
        }
    }
};

// Scroll to bottom of messages
window.scrollToBottom = (element) => {
    if (element) {
        element.scrollTop = element.scrollHeight;
    }
};

// Save and load chat session from localStorage
window.saveChatId = (chatId) => {
    localStorage.setItem('currentChatId', chatId);
};

window.saveCurrentPath = (path) => {
    localStorage.setItem('currentPath', path);
};

window.loadChatId = () => {
    return localStorage.getItem('currentChatId') || '';
};

window.loadCurrentPath = () => {
    return localStorage.getItem('currentPath') || '/';
};

window.downloadFile = function (fileName, dataUrl) {
    const link = document.createElement('a');
    link.href = dataUrl;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};