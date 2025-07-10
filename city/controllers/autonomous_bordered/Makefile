WEBOTS_HOME = /Applications/Webots.app/Contents

CC = gcc
CFLAGS = -O2 -Wall -std=c99 -I"$(WEBOTS_HOME)/include/controller/c"
LDFLAGS = -L"$(WEBOTS_HOME)/lib/controller" -lController -ldriver
TARGET = autonomous_bordered

all:
	$(CC) $(CFLAGS) autonomous_bordered.c -o $(TARGET) $(LDFLAGS)

clean:
	rm -f $(TARGET)
