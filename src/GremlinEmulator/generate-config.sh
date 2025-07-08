#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GRAPHS_DIR="${SCRIPT_DIR}/graphs"
CONFIG_FILE="${SCRIPT_DIR}/gremlin-server.yaml"

# Function to convert filename to camelCase
to_camel_case() {
    local filename="$1"
    # Remove .graphml extension
    local name="${filename%.graphml}"
    # Split on hyphens and underscores, then camelCase
    echo "$name" | sed -E 's/[-_]([a-z])/\U\1/g'
}

echo "🔄 Generating dynamic gremlin-server.yaml configuration..."

# Start building the config
cat > "$CONFIG_FILE" << 'EOF'
host: 172.17.0.2
port: 8182
graphs: {
  empty: /opt/gremlin-server/custom-conf/tinkergraph-stringid.properties,
EOF

# Add discovered graphs
if [ -d "$GRAPHS_DIR" ]; then
    for file in "$GRAPHS_DIR"/*.graphml; do
        if [ -f "$file" ]; then
            filename=$(basename "$file")
            graphname=$(to_camel_case "$filename")
            echo "  Found: $filename -> $graphname"
            echo "  $graphname: /opt/gremlin-server/custom-conf/tinkergraph-stringid.properties," >> "$CONFIG_FILE"
        fi
    done
fi

# Finish the config
cat >> "$CONFIG_FILE" << 'EOF'
}
serializers:
  - { className: org.apache.tinkerpop.gremlin.util.ser.GraphSONMessageSerializerV2, config: { ioRegistries: [org.apache.tinkerpop.gremlin.tinkergraph.structure.TinkerIoRegistryV2] }}  
scriptEngines: {
  gremlin-groovy: {
    plugins: { 
      org.apache.tinkerpop.gremlin.server.jsr223.GremlinServerGremlinPlugin: {},
      org.apache.tinkerpop.gremlin.tinkergraph.jsr223.TinkerGraphGremlinPlugin: {},
      org.apache.tinkerpop.gremlin.jsr223.ImportGremlinPlugin: {classImports: [java.lang.Math], methodImports: [java.lang.Math#*]},
      org.apache.tinkerpop.gremlin.jsr223.ScriptFileGremlinPlugin: {files: [/opt/gremlin-server/custom-conf/load.graphgroovy]}
    }
  }    
}
EOF

echo "✅ Generated $CONFIG_FILE with dynamically discovered graphs"
