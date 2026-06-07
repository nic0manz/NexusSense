using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace NexusSense;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct MAHMHeader
{
    public uint dwSignature;
    public uint dwVersion;
    public uint dwHeaderSize;
    public uint dwNumEntries;
    public uint dwEntrySize;
    public uint time;
}

public class AfterburnerReader
{
    public record SensorData(int Id, string Name, float Value, string Units);

    private const int DATA_OFFSET = 1300;

    public static List<SensorData> ReadAll()
    {
        var result = new List<SensorData>();
        try
        {
            using var mmf      = MemoryMappedFile.OpenExisting("MAHMSharedMemory");
            using var accessor = mmf.CreateViewAccessor();
            accessor.Read(0, out MAHMHeader header);
            for (int i = 0; i < header.dwNumEntries; i++)
            {
                long   offset = header.dwHeaderSize + i * (long)header.dwEntrySize;
                byte[] buf    = new byte[header.dwEntrySize];
                accessor.ReadArray(offset, buf, 0, buf.Length);
                string srcName  = ReadAnsiString(buf, 0,   260);
                string srcUnits = ReadAnsiString(buf, 260, 260);
                float  value    = BitConverter.ToSingle(buf, DATA_OFFSET);
                result.Add(new SensorData(i, srcName, value, srcUnits));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        return result;
    }

    public static void PrintAll()
    {
        foreach (var s in ReadAll())
            Console.WriteLine($"  [{s.Id:D2}] {s.Name} = {s.Value} {s.Units}");
    }

    private static string ReadAnsiString(byte[] buf, int offset, int maxLen)
    {
        int end = offset;
        while (end < offset + maxLen && end < buf.Length && buf[end] != 0)
            end++;
        return System.Text.Encoding.ASCII.GetString(buf, offset, end - offset);
    }

    public static float? Get(string name)
        => ReadAll().FirstOrDefault(s => s.Name == name)?.Value;

    public static float? GetById(int id)
        => ReadAll().FirstOrDefault(s => s.Id == id)?.Value;
}
