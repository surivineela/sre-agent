#!/bin/bash
# VPN Auto-Reconnect Script for GlobalProtect on macOS
# Used by Usage Dashboard agent to ensure connectivity before Kusto queries

KUSTO_HOST="sreagent-sec.swedencentral.kusto.windows.net"
MAX_RETRIES=3
RETRY_DELAY=5

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

check_vpn() {
    # Try to resolve Kusto cluster DNS (faster than ping)
    if host "$KUSTO_HOST" > /dev/null 2>&1; then
        return 0
    fi
    # Fallback: try nc to check port 443
    if nc -z -w 3 "$KUSTO_HOST" 443 > /dev/null 2>&1; then
        return 0
    fi
    return 1
}

reconnect_globalprotect() {
    echo -e "${YELLOW}⏳ Attempting GlobalProtect reconnect...${NC}"
    
    # Check if GlobalProtect is running
    if ! pgrep -x "GlobalProtect" > /dev/null; then
        echo -e "${YELLOW}📱 Starting GlobalProtect...${NC}"
        open -a "GlobalProtect"
        sleep 3
    fi
    
    # Use AppleScript to click the GlobalProtect menu bar icon and trigger connect
    # This opens the GlobalProtect panel which should auto-connect if configured
    osascript <<EOF
tell application "System Events"
    tell process "GlobalProtect"
        -- Click menu bar item to open the panel (triggers reconnect)
        try
            click menu bar item 1 of menu bar 2
            delay 1
            -- Click again to close (the connection attempt will continue in background)
            click menu bar item 1 of menu bar 2
        end try
    end tell
end tell
EOF
    
    # Give it time to connect
    sleep 5
}

main() {
    echo -e "${YELLOW}🔌 Checking VPN connectivity to Kusto cluster...${NC}"
    
    if check_vpn; then
        echo -e "${GREEN}✅ VPN connected - Kusto cluster reachable${NC}"
        exit 0
    fi
    
    echo -e "${RED}❌ VPN disconnected - Cannot reach Kusto cluster${NC}"
    
    for i in $(seq 1 $MAX_RETRIES); do
        echo -e "${YELLOW}🔄 Reconnect attempt $i of $MAX_RETRIES...${NC}"
        reconnect_globalprotect
        
        # Wait and check
        for j in $(seq 1 3); do
            sleep $RETRY_DELAY
            if check_vpn; then
                echo -e "${GREEN}✅ VPN reconnected successfully!${NC}"
                exit 0
            fi
            echo -e "${YELLOW}⏳ Waiting for connection... (${j}/3)${NC}"
        done
    done
    
    echo -e "${RED}❌ Failed to reconnect VPN after $MAX_RETRIES attempts${NC}"
    echo -e "${YELLOW}💡 Please manually connect GlobalProtect and try again${NC}"
    exit 1
}

main "$@"
