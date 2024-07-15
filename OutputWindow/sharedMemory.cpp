#include "sharedMemory.h"

int CreateSharedMemory(HANDLE *hMapFile, unsigned char **sharedBuffer, int bufferSize, std::string bufferName)
{
	// Shared Memory Initilization
	*hMapFile = CreateFileMapping(INVALID_HANDLE_VALUE, NULL, PAGE_READWRITE, 0, bufferSize, bufferName.c_str());
	if(*hMapFile == NULL)
    {
		MessageBox(0, "Could not create shared memory", "Error", MB_OK);
		return 0;
	}
	return 1 | MapSharedMemory(hMapFile, sharedBuffer, bufferSize);
}

int OpenSharedMemory(HANDLE *hMapFile, unsigned char **sharedBuffer, int bufferSize, std::string bufferName)
{
	*hMapFile = OpenFileMapping(FILE_MAP_ALL_ACCESS, FALSE, bufferName.c_str());
	if(*hMapFile == NULL)
    {
        int retVal = CreateSharedMemory(hMapFile, sharedBuffer, bufferSize, bufferName);
        if (retVal != 0)
            return retVal;
        MessageBox(0, "Could not open shared memory", "Error", MB_OK);
        return 0;
	}
    return 2 | MapSharedMemory(hMapFile, sharedBuffer, bufferSize);
}

void CloseSharedMemory(HANDLE *hMapFile, unsigned char **sharedBuffer)
{
	if(*sharedBuffer)
		UnmapViewOfFile(*sharedBuffer);
	*sharedBuffer = nullptr;
	if(*hMapFile)
		CloseHandle(*hMapFile);
	*hMapFile = nullptr;
}

int MapSharedMemory(HANDLE *hMapFile, unsigned char **sharedBuffer, int bufferSize)
{
	*sharedBuffer = (unsigned char*)MapViewOfFile(*hMapFile, FILE_MAP_ALL_ACCESS, 0, 0, bufferSize);
	if(*sharedBuffer == NULL)
    {
		MessageBox(0, "Could not map shared memory", "Error", MB_OK);
		CloseHandle(hMapFile);
        return 0;
	}
	return 4;
}

bool CreateExtraMemory(HANDLE *bufferHandle, DWORD *bufferLocation)
{
	*bufferHandle = HeapCreate(HEAP_CREATE_ENABLE_EXECUTE, 1024, 1024);
	*bufferLocation = (DWORD)HeapAlloc(*bufferHandle, HEAP_ZERO_MEMORY, 1024);

	if(*bufferHandle == 0)
        return false;
	return true;
}

void CloseExtraMemory(HANDLE *bufferHandle, DWORD *bufferLocation)
{
	if(*bufferHandle)
		HeapFree(*bufferHandle, 0, bufferLocation);
	*bufferHandle = nullptr; 
	*bufferLocation = 0;
}
