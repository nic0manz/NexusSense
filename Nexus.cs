using System.Text;
using HidApi;

namespace NexusSense;

/// <summary>
/// Provides control over the Corsair K100 LCD panel via HID.
/// </summary>
public class Nexus
{
    private readonly Device _device;

    public Nexus(Device device) => _device = device;

    /// <summary>Sets LCD backlight brightness (0–100).</summary>
    public void SetBrightness(int brightness)
    {
        if (brightness < 0 || brightness > 100)
            throw new ArgumentOutOfRangeException(nameof(brightness));
        _device.SendFeatureReport([3, 1, (byte)brightness]);
    }

    /// <summary>
    /// Uploads a raw RGBA byte array directly to the LCD.
    /// </summary>
    public void UploadImage(byte[] image)
    {
        byte[] packet = new byte[1024];
        packet[0] = 0x02;
        packet[1] = 0x05;
        packet[2] = 0x40;

        int remaining = image.Length;
        int offset    = 0;
        int blockno   = 0;

        while (remaining > 0)
        {
            int packlen = Math.Min(remaining, 1016);
            Buffer.BlockCopy(image, offset, packet, 8, packlen);
            packet[3] = (byte)(remaining - packlen == 0 ? 1 : 0);
            packet[4] = (byte)blockno;
            packet[6] = (byte)(packlen & 0xFF);
            packet[7] = (byte)(packlen >> 8);
            remaining -= packlen;
            offset    += packlen;
            blockno++;
            _device.Write(packet);
        }
    }

    /// <summary>
    /// Waits up to <paramref name="timeoutsecs"/> seconds for a touch/swipe and returns a classification string.
    /// </summary>
    public string WaitForTouchResult(int timeoutsecs)
    {
        if (timeoutsecs < 0) throw new ArgumentOutOfRangeException(nameof(timeoutsecs));

        int   first   = -1;
        int   last    = -1;
        int   lastX   = -1;
        bool? touched = null;

        DateTime expiry = DateTime.UtcNow + TimeSpan.FromSeconds(timeoutsecs);

        while (DateTime.UtcNow < expiry || touched == true)
        {
            int X = -1;
            ReadOnlySpan<byte> bytes = _device.ReadTimeout(64, 2000);

            if (bytes.Length > 9)
            {
                if (bytes[0] != 0x01 || bytes[1] != 0x02 || bytes[2] != 0x21) break;
                bool touch = bytes[5] != 0;
                X = bytes[6] + (bytes[7] << 8);
                if (touch != touched)
                {
                    if (touch) first = X;
                    else       last  = lastX;
                    touched = touch;
                }
            }

            if (touched == false) break;
            lastX = X;
        }

        if (first < 0) return "--";
        if (last  < 0) last = first;
        int diff = last - first;

        if (diff >  200) return "->";
        if (diff < -200) return "<-";
        if (diff < -50 || diff > 50) return $"+- {first}";
        return $"++ {first}";
    }
}
