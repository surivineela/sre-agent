confirmDeployment() {
    local parameters_file="$1"
    echo "Contents of $parameters_file:"
    sed 's/^/\t/' "$parameters_file"
    echo
    while true; do
        read -p "Do you want to continue with these parameters? (y/n): " CONFIRM
        case $CONFIRM in
            y) break ;;
            n) echo "Deployment aborted by user"; exit 1 ;;
            *) echo "Please answer y/n" ;;
        esac
    done
}