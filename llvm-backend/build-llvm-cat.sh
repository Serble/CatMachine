#!/bin/bash
# build-llvm-cat.sh - Portable build script for LLVM with Cat backend
# This script downloads, configures, and builds LLVM with the Cat target

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
LLVM_VERSION="15.0.7"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORK_DIR="${WORK_DIR:-$HOME/llvm-cat-build}"
INSTALL_DIR="${INSTALL_DIR:-$HOME/llvm-cat}"
JOBS="${JOBS:-$(nproc 2>/dev/null || sysctl -n hw.ncpu 2>/dev/null || echo 4)}"
BUILD_TYPE="${BUILD_TYPE:-Release}"

# Print functions
print_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

print_section() {
    echo ""
    echo -e "${GREEN}========================================${NC}"
    echo -e "${GREEN}$1${NC}"
    echo -e "${GREEN}========================================${NC}"
}

# Check dependencies
check_dependencies() {
    print_section "Checking Dependencies"
    
    local missing_deps=()
    
    # Check for required tools
    for cmd in cmake git python3; do
        if ! command -v $cmd &> /dev/null; then
            missing_deps+=($cmd)
        else
            print_success "$cmd is installed"
        fi
    done
    
    # Check for C++ compiler
    if command -v g++ &> /dev/null; then
        print_success "g++ is installed"
    elif command -v clang++ &> /dev/null; then
        print_success "clang++ is installed"
    else
        missing_deps+=(g++ or clang++)
    fi
    
    # Check for build tools
    if command -v ninja &> /dev/null; then
        print_success "ninja is installed (preferred)"
        BUILD_TOOL="Ninja"
    elif command -v make &> /dev/null; then
        print_success "make is installed"
        BUILD_TOOL="Unix Makefiles"
    else
        missing_deps+=(ninja or make)
    fi
    
    if [ ${#missing_deps[@]} -ne 0 ]; then
        print_error "Missing dependencies: ${missing_deps[*]}"
        echo ""
        echo "Please install the missing dependencies:"
        echo "  Ubuntu/Debian: sudo apt-get install cmake git python3 g++ ninja-build"
        echo "  Fedora/RHEL:   sudo dnf install cmake git python3 gcc-c++ ninja-build"
        echo "  macOS:         brew install cmake git python3 ninja"
        exit 1
    fi
    
    print_success "All dependencies satisfied"
}

# Create working directory
setup_workspace() {
    print_section "Setting Up Workspace"
    
    mkdir -p "$WORK_DIR"
    cd "$WORK_DIR"
    
    print_info "Working directory: $WORK_DIR"
    print_info "Install directory: $INSTALL_DIR"
}

# Download LLVM
download_llvm() {
    print_section "Downloading LLVM"
    
    if [ -d "llvm-project" ]; then
        print_warning "llvm-project directory already exists"
        read -p "Do you want to delete and re-download? (y/N): " -n 1 -r
        echo
        if [[ $REPLY =~ ^[Yy]$ ]]; then
            rm -rf llvm-project
        else
            print_info "Using existing llvm-project directory"
            return
        fi
    fi
    
    print_info "Cloning LLVM repository (this may take a while)..."
    git clone --depth 1 --branch llvmorg-$LLVM_VERSION https://github.com/llvm/llvm-project.git
    
    print_success "LLVM downloaded successfully"
}

# Integrate Cat backend
integrate_cat_backend() {
    print_section "Integrating Cat Backend"
    
    local cat_src="$SCRIPT_DIR/Cat"
    local cat_dst="$WORK_DIR/llvm-project/llvm/lib/Target/Cat"
    
    if [ ! -d "$cat_src" ]; then
        print_error "Cat backend source not found at: $cat_src"
        print_info "Make sure you're running this script from the llvm-backend directory"
        exit 1
    fi
    
    # Copy Cat backend
    print_info "Copying Cat backend to LLVM tree..."
    cp -r "$cat_src" "$cat_dst"
    
    # Update CMakeLists.txt
    local cmake_file="$WORK_DIR/llvm-project/llvm/lib/Target/CMakeLists.txt"
    if ! grep -q "add_subdirectory(Cat)" "$cmake_file"; then
        print_info "Adding Cat to CMakeLists.txt..."
        echo "add_subdirectory(Cat)" >> "$cmake_file"
    else
        print_info "Cat already in CMakeLists.txt"
    fi
    
    print_success "Cat backend integrated"
}

# Configure LLVM
configure_llvm() {
    print_section "Configuring LLVM"
    
    mkdir -p "$WORK_DIR/build"
    cd "$WORK_DIR/build"
    
    print_info "CMake configuration:"
    print_info "  Build type: $BUILD_TYPE"
    print_info "  Generator: $BUILD_TOOL"
    print_info "  Targets: Cat, X86 (host)"
    print_info "  Install prefix: $INSTALL_DIR"
    
    cmake -G "$BUILD_TOOL" \
        -DCMAKE_BUILD_TYPE=$BUILD_TYPE \
        -DLLVM_TARGETS_TO_BUILD="Cat;X86" \
        -DLLVM_ENABLE_PROJECTS="clang" \
        -DCMAKE_INSTALL_PREFIX="$INSTALL_DIR" \
        -DLLVM_OPTIMIZED_TABLEGEN=ON \
        -DLLVM_ENABLE_ASSERTIONS=ON \
        ../llvm-project/llvm
    
    print_success "LLVM configured successfully"
}

# Build LLVM
build_llvm() {
    print_section "Building LLVM"
    
    cd "$WORK_DIR/build"
    
    print_info "Building with $JOBS parallel jobs..."
    print_warning "This will take a significant amount of time (30-60 minutes or more)"
    
    if [ "$BUILD_TOOL" = "Ninja" ]; then
        ninja -j$JOBS
    else
        make -j$JOBS
    fi
    
    print_success "LLVM built successfully"
}

# Install LLVM
install_llvm() {
    print_section "Installing LLVM"
    
    cd "$WORK_DIR/build"
    
    if [ "$BUILD_TOOL" = "Ninja" ]; then
        ninja install
    else
        make install
    fi
    
    print_success "LLVM installed to: $INSTALL_DIR"
}

# Verify installation
verify_installation() {
    print_section "Verifying Installation"
    
    local llc="$INSTALL_DIR/bin/llc"
    
    if [ ! -f "$llc" ]; then
        print_error "llc not found at: $llc"
        exit 1
    fi
    
    print_info "Checking if Cat target is available..."
    if $llc --version | grep -q "cat"; then
        print_success "Cat target is registered!"
        echo ""
        $llc --version | grep -A 5 "Registered Targets:"
    else
        print_error "Cat target not found in llc"
        exit 1
    fi
}

# Create environment setup script
create_env_script() {
    print_section "Creating Environment Script"
    
    local env_script="$INSTALL_DIR/setup-env.sh"
    
    cat > "$env_script" << EOF
#!/bin/bash
# Source this script to set up the Cat LLVM environment
export PATH="$INSTALL_DIR/bin:\$PATH"
export LLVM_CAT_HOME="$INSTALL_DIR"

echo "Cat LLVM environment configured"
echo "llc location: \$(which llc)"
echo "clang location: \$(which clang)"
EOF
    
    chmod +x "$env_script"
    
    print_success "Environment script created: $env_script"
    print_info "Source it with: source $env_script"
}

# Main installation flow
main() {
    echo ""
    echo "╔════════════════════════════════════════════╗"
    echo "║  LLVM Cat Backend Build Script             ║"
    echo "║  Version: 1.0                              ║"
    echo "╚════════════════════════════════════════════╝"
    echo ""
    
    print_info "Build configuration:"
    print_info "  LLVM Version: $LLVM_VERSION"
    print_info "  Work directory: $WORK_DIR"
    print_info "  Install directory: $INSTALL_DIR"
    print_info "  Parallel jobs: $JOBS"
    print_info "  Build type: $BUILD_TYPE"
    echo ""
    
    check_dependencies
    setup_workspace
    download_llvm
    integrate_cat_backend
    configure_llvm
    build_llvm
    install_llvm
    verify_installation
    create_env_script
    
    print_section "Installation Complete!"
    echo ""
    print_success "LLVM with Cat backend has been successfully built and installed"
    echo ""
    echo "Next steps:"
    echo "  1. Set up your environment:"
    echo "     source $INSTALL_DIR/setup-env.sh"
    echo ""
    echo "  2. Test the installation:"
    echo "     cd $SCRIPT_DIR"
    echo "     ./test-cat-backend.sh"
    echo ""
    echo "  3. Compile a C program:"
    echo "     clang -S -emit-llvm -O2 program.c -o program.ll"
    echo "     llc -march=cat program.ll -o program.s"
    echo ""
}

# Handle script arguments
if [ "$1" = "--help" ] || [ "$1" = "-h" ]; then
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "Build LLVM with Cat backend support"
    echo ""
    echo "Options:"
    echo "  WORK_DIR=<path>     Set working directory (default: ~/llvm-cat-build)"
    echo "  INSTALL_DIR=<path>  Set install directory (default: ~/llvm-cat)"
    echo "  JOBS=<number>       Set parallel jobs (default: auto-detect)"
    echo "  BUILD_TYPE=<type>   Set build type: Release|Debug (default: Release)"
    echo ""
    echo "Examples:"
    echo "  $0"
    echo "  WORK_DIR=/tmp/llvm-build $0"
    echo "  JOBS=8 BUILD_TYPE=Debug $0"
    echo ""
    exit 0
fi

# Run main
main
