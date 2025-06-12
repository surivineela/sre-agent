// Console SignalR Test Script
// Copy and paste this into your browser console to test SignalR functionality

// Load SignalR library if not already loaded
if (typeof signalR === 'undefined') {
    const script = document.createElement('script');
    script.src = 'https://unpkg.com/@microsoft/signalr@latest/dist/browser/signalr.js';
    document.head.appendChild(script);
    script.onload = function() {
        console.log('✅ SignalR loaded!');
        setupSignalRTest();
    };
} else {
    setupSignalRTest();
}

function setupSignalRTest() {
    // Create connection
    window.agentConnection = new signalR.HubConnectionBuilder()
        .withUrl("/agentHub")
        .configureLogging(signalR.LogLevel.Debug)
        .build();

    // Event handlers
    window.agentConnection.on("ThreadUpdate", (result) => {
        console.log("🧵 Thread Update:", result);
    });

    window.agentConnection.on("MessageUpdate", (result) => {
        console.log("💬 Message Update:", result);
    });

    window.agentConnection.on("TextUpdate", (text) => {
        console.log("📝 Text Update:", text);
    });

    window.agentConnection.on("Error", (error) => {
        console.error("❌ Error:", error);
    });

    window.agentConnection.on("Pong", (timestamp) => {
        console.log("🏓 Pong:", timestamp);
    });

    // Connection lifecycle
    window.agentConnection.onclose(() => {
        console.log("🔌 Connection closed");
    });

    window.agentConnection.onreconnecting(() => {
        console.log("🔄 Reconnecting...");
    });

    window.agentConnection.onreconnected(() => {
        console.log("🔌 Reconnected");
    });

    // Helper functions
    window.connectToAgent = async function() {
        try {
            await window.agentConnection.start();
            console.log("✅ Connected to SignalR hub");
            console.log("Connection ID:", window.agentConnection.connectionId);
            return true;
        } catch (err) {
            console.error("❌ Connection failed:", err);
            return false;
        }
    };

    window.disconnectFromAgent = async function() {
        if (window.agentConnection) {
            await window.agentConnection.stop();
            console.log("🔌 Disconnected");
        }
    };

    window.pingAgent = async function() {
        try {
            await window.agentConnection.invoke("Ping");
            console.log("🏓 Ping sent");
        } catch (err) {
            console.error("❌ Ping failed:", err);
        }
    };

    window.createThread = async function(message = "Hello, I need help with Azure subscriptions") {
        try {
            const streamId = "console-test-" + Date.now();
            const request = {
                startMessage: {
                    text: message,
                    userId: "console-user",
                    displayName: "Console Tester"
                },
                source: 1 // Conversation
            };
            
            console.log(`🚀 Creating thread with message: "${message}"`);
            await window.agentConnection.invoke("CreateThread", request, streamId, false);
        } catch (err) {
            console.error("❌ Create thread failed:", err);
        }
    };

    window.sendMessage = async function(threadId, message = "Tell me more about Azure") {
        try {
            const streamId = "console-msg-" + Date.now();
            const request = {
                text: message,
                userId: "console-user",
                displayName: "Console Tester"
            };
            
            console.log(`🚀 Sending message to thread ${threadId}: "${message}"`);
            await window.agentConnection.invoke("CreateMessage", threadId, request, streamId, false);
        } catch (err) {
            console.error("❌ Send message failed:", err);
        }
    };

    // Test with text-only mode
    window.createThreadTextOnly = async function(message = "Hello, I need help with Azure subscriptions") {
        try {
            const streamId = "console-text-" + Date.now();
            const request = {
                startMessage: {
                    text: message,
                    userId: "console-user",
                    displayName: "Console Tester"
                },
                source: 1
            };
            
            console.log(`🚀 Creating thread (text-only) with message: "${message}"`);
            await window.agentConnection.invoke("CreateThread", request, streamId, true);
        } catch (err) {
            console.error("❌ Create thread (text-only) failed:", err);
        }
    };

    window.checkConnection = function() {
        console.log("Connection State:", window.agentConnection.state);
        console.log("Connection ID:", window.agentConnection.connectionId);
    };

    // Auto-connect
    console.log("🔌 Auto-connecting to SignalR...");
    window.connectToAgent();

    // Show available functions
    console.log(`
📋 Available test functions:
• connectToAgent() - Connect to SignalR hub
• disconnectFromAgent() - Disconnect from hub
• pingAgent() - Test ping/pong
• createThread(message?) - Create a new thread
• sendMessage(threadId, message?) - Send message to existing thread
• createThreadTextOnly(message?) - Create thread with text-only responses
• checkConnection() - Check connection status

Example usage:
await createThread("Help me with Azure storage")
await sendMessage("your-thread-id-here", "What are the pricing options?")
    `);
}

// If SignalR is already loaded, setup immediately
if (typeof signalR !== 'undefined') {
    setupSignalRTest();
} 