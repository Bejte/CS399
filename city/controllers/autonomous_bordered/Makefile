# Path to Webots installation
WEBOTS_HOME = /Applications/Webots.app/Contents

# SDL2 installed via Homebrew
SDL2_PATH = /usr/local/opt/sdl2
SDL2_INCLUDE = $(SDL2_PATH)/include
SDL2_LIB = $(SDL2_PATH)/lib

# Compiler
CC = gcc

# Compiler flags
CFLAGS = -Wall -O2 -std=c99 \
    -I"$(WEBOTS_HOME)/include/controller/c" \
    -I"$(SDL2_INCLUDE)/SDL2"

# Linker flags
LDFLAGS = -L"$(WEBOTS_HOME)/lib/controller" -lController -ldriver \
    -L"$(SDL2_LIB)" -lSDL2

# Output
TARGET = autonomous_bordered

# Build rule
all:
	$(CC) $(CFLAGS) $(TARGET).c -o $(TARGET) $(LDFLAGS)

# Clean rule
clean:
	rm -f $(TARGET)
