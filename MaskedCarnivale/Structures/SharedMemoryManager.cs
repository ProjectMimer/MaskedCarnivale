using MaskedCarnivale;
using System.IO.MemoryMappedFiles;

namespace MemoryManager.Structures;

public struct SharedMemoryManager
{
    public MemoryMappedFile? mmf = null;
    public MemoryMappedViewAccessor? mmvAccessor = null;

    public SharedMemoryManager()
    {
    }

    public int CreateSharedMemory(int bufferSize, string bufferName)
    {
        try
        {
            mmf = MemoryMappedFile.CreateNew(bufferName, bufferSize, MemoryMappedFileAccess.ReadWrite);
        }
        catch
        {
            Plugin.Log!.Error($"Could not create shared memory");
            return 0;
        }
        return 1 | MapSharedMemory();
    }

    public int OpenSharedMemory(int bufferSize, string bufferName)
    {
        try
        {
            mmf = MemoryMappedFile.OpenExisting(bufferName, MemoryMappedFileRights.ReadWrite);
        }
        catch
        {
            int retType = CreateSharedMemory(bufferSize, bufferName);
            if (retType != 0)
                return retType;

            Plugin.Log!.Error($"Could not open shared memory");
            return 0;
        }
        return 2 | MapSharedMemory();
    }

    public void CloseSharedMemory()
    {
        mmvAccessor?.Dispose();
        mmf?.Dispose();
    }

    public int MapSharedMemory()
    {
        try
        {
            mmvAccessor = mmf!.CreateViewAccessor();
        }
        catch
        {
            Plugin.Log!.Error($"Could not map shared memory");
            return 0;
        }
        return 4;
    }

    public void Dispose()
    {
        CloseSharedMemory();
    }
}
