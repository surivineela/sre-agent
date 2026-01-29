#!/usr/bin/env python3
"""
Kusto Query Helper for Lost Customers Report
Uses Azure CLI credentials (bypasses MCP keychain issues on macOS)
Includes VPN connectivity check and retry logic
"""

import json
import subprocess
import sys
import time
from typing import Optional

def check_vpn_connected() -> bool:
    """Check if VPN is connected by testing connectivity to Kusto cluster"""
    try:
        result = subprocess.run(
            ["nc", "-z", "-w", "3", "sreagent-sec.swedencentral.kusto.windows.net", "443"],
            capture_output=True,
            timeout=5
        )
        return result.returncode == 0
    except:
        return False

def auto_reconnect_vpn() -> bool:
    """Auto-reconnect VPN using GlobalProtect script"""
    import os
    script_dir = os.path.dirname(os.path.abspath(__file__))
    vpn_script = os.path.join(script_dir, "vpn_reconnect.sh")
    
    if os.path.exists(vpn_script):
        print("🔄 Auto-reconnecting VPN...", file=sys.stderr)
        try:
            result = subprocess.run(
                ["/bin/bash", vpn_script],
                capture_output=False,
                timeout=60
            )
            return result.returncode == 0
        except subprocess.TimeoutExpired:
            print("❌ VPN reconnect timed out", file=sys.stderr)
            return False
        except Exception as e:
            print(f"❌ VPN reconnect error: {e}", file=sys.stderr)
            return False
    else:
        # Fallback to manual prompt
        print("⚠️  VPN appears disconnected. Cannot reach Kusto cluster.", file=sys.stderr)
        print("Please connect to VPN and press Enter to retry...", file=sys.stderr)
        try:
            input()
            return check_vpn_connected()
        except:
            return False

def ensure_vpn_connected(max_retries: int = 3) -> bool:
    """Ensure VPN is connected, auto-reconnect if not"""
    if check_vpn_connected():
        return True
    
    # Try auto-reconnect
    if auto_reconnect_vpn():
        return True
    
    # Manual retry loop as fallback
    for attempt in range(max_retries - 1):
        print(f"Retry {attempt + 2}/{max_retries}...", file=sys.stderr)
        time.sleep(2)
        if check_vpn_connected():
            return True
    
    return False

def run_kusto_query(
    query: str,
    cluster: str = "https://sreagent-sec.swedencentral.kusto.windows.net",
    database: str = "sreagent",
    output_format: str = "json"
) -> Optional[dict]:
    """
    Execute a Kusto query using Azure CLI credentials
    
    Args:
        query: KQL query string
        cluster: Kusto cluster URI
        database: Database name
        output_format: "json" or "table"
    
    Returns:
        Query results as dict/list, or None on error
    """
    # Check VPN first
    if not ensure_vpn_connected():
        print("❌ Cannot connect to Kusto cluster. Please check VPN.", file=sys.stderr)
        return None
    
    try:
        from azure.kusto.data import KustoClient, KustoConnectionStringBuilder
        from azure.identity import AzureCliCredential
    except ImportError:
        print("Installing required packages...", file=sys.stderr)
        subprocess.run([sys.executable, "-m", "pip", "install", "-q", 
                       "azure-kusto-data", "azure-identity"])
        from azure.kusto.data import KustoClient, KustoConnectionStringBuilder
        from azure.identity import AzureCliCredential
    
    try:
        # Use Azure CLI credential (no keychain issues)
        cred = AzureCliCredential()
        kcsb = KustoConnectionStringBuilder.with_azure_token_credential(cluster, cred)
        client = KustoClient(kcsb)
        
        # Execute query
        response = client.execute(database, query)
        
        # Convert to list of dicts
        results = []
        if response.primary_results:
            columns = [col.column_name for col in response.primary_results[0].columns]
            for row in response.primary_results[0]:
                row_dict = {}
                for i, col in enumerate(columns):
                    val = row[i]
                    # Handle datetime serialization
                    if hasattr(val, 'isoformat'):
                        val = val.isoformat()
                    row_dict[col] = val
                results.append(row_dict)
        
        return {"success": True, "data": results, "count": len(results)}
    
    except Exception as e:
        error_msg = str(e)
        
        # Check for auth errors
        if "401" in error_msg or "authentication" in error_msg.lower():
            print("❌ Authentication failed. Please run 'az login'", file=sys.stderr)
        elif "timeout" in error_msg.lower() or "connection" in error_msg.lower():
            print("❌ Connection failed. Please check VPN.", file=sys.stderr)
        else:
            print(f"❌ Query error: {error_msg}", file=sys.stderr)
        
        return {"success": False, "error": error_msg}


def run_query_from_file(
    file_path: str,
    start_date: str,
    end_date: str,
    cluster: str = "https://sreagent-sec.swedencentral.kusto.windows.net",
    database: str = "sreagent"
) -> Optional[dict]:
    """
    Run a query from a .kql file with date parameter substitution
    
    Args:
        file_path: Path to .kql file
        start_date: Report start date (YYYY-MM-DD)
        end_date: Report end date (YYYY-MM-DD)
    """
    import re
    
    try:
        with open(file_path, 'r') as f:
            query = f.read()
        
        # Replace date parameters - handle multiple formats
        # Format 1: datetime(2026-01-09T00:00:00.000Z)
        query = re.sub(
            r'let ReportStartDate = datetime\([^)]+\);',
            f'let ReportStartDate = datetime({start_date}T00:00:00.000Z);',
            query
        )
        query = re.sub(
            r'let ReportEndDate = datetime\([^)]+\);',
            f'let ReportEndDate = datetime({end_date}T00:00:00.000Z);',
            query
        )
        
        # Format 2: datetime(2026-01-09) without timestamp
        query = re.sub(
            r'let StartDate = datetime\([^)]+\);',
            f'let StartDate = datetime({start_date}T00:00:00.000Z);',
            query
        )
        query = re.sub(
            r'let EndDate = datetime\([^)]+\);',
            f'let EndDate = datetime({end_date}T00:00:00.000Z);',
            query
        )
        
        return run_kusto_query(query, cluster, database)
    
    except FileNotFoundError:
        print(f"❌ Query file not found: {file_path}", file=sys.stderr)
        return {"success": False, "error": f"File not found: {file_path}"}


if __name__ == "__main__":
    import argparse
    
    parser = argparse.ArgumentParser(description="Run Kusto queries")
    parser.add_argument("--query", "-q", help="KQL query string")
    parser.add_argument("--file", "-f", help="Path to .kql file")
    parser.add_argument("--start-date", "-s", default="2026-01-09", help="Start date (YYYY-MM-DD)")
    parser.add_argument("--end-date", "-e", default="2026-01-17", help="End date (YYYY-MM-DD)")
    parser.add_argument("--cluster", "-c", 
                       default="https://sreagent-sec.swedencentral.kusto.windows.net",
                       help="Kusto cluster URI")
    parser.add_argument("--database", "-d", default="sreagent", help="Database name")
    
    args = parser.parse_args()
    
    if args.file:
        result = run_query_from_file(args.file, args.start_date, args.end_date, 
                                     args.cluster, args.database)
    elif args.query:
        result = run_kusto_query(args.query, args.cluster, args.database)
    else:
        # Test connection
        result = run_kusto_query("print 'connection test successful'")
    
    print(json.dumps(result, indent=2, default=str))
