using System.Diagnostics;
using System.Runtime.InteropServices;
using MusicMover.Helpers;
using MusicMover.Models;
using Newtonsoft.Json;

namespace MusicMover.Services;

public class FingerPrintService
{
    // Returns a const char* (pointer to ANSI string)
    [DllImport("libchromaprint.so.1", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr chromaprint_get_version();

    // Decodes fingerprint
    [DllImport("libchromaprint.so.1", EntryPoint = "chromaprint_decode_fingerprint", CallingConvention = CallingConvention.Cdecl)]
    public static extern int chromaprint_decode_fingerprint(IntPtr encoded_fp, int encoded_size, out IntPtr fp, out int size, out int algorithm, int base64);

    // Free memory allocated by chromaprint
    [DllImport("libchromaprint.so.1", CallingConvention = CallingConvention.Cdecl)]
    public static extern void chromaprint_dealloc(IntPtr ptr);
    
    public async Task<FpcalcOutput?> GetFingerprintAsync(string filePath)
    {
        // Start a new process for fpcalc
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "fpcalc",  // Command to call
                Arguments = $"-json \"{filePath}\"",  // Path to the audio file
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        // Start the process and read the output
        process.Start();
        string output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        return ParseFingerprint(output);
    }

    private FpcalcOutput? ParseFingerprint(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }
        
        // Deserialize JSON output to FpcalcOutput object
        var result = JsonConvert.DeserializeObject<FpcalcOutput>(output);
        
        if (result == null || string.IsNullOrEmpty(result.Fingerprint))
        {
            Logger.WriteLine("Failed to generate fingerprint, corrupt file?");
            return null;
        }
        
        return result;
    }
    
    private static int[] DecodeFingerprint(byte[] encoded, bool base64, out int algorithm)
    {
        var h = GCHandle.Alloc(encoded, GCHandleType.Pinned);

        try
        {
            var p = h.AddrOfPinnedObject();

            if (chromaprint_decode_fingerprint(p, encoded.Length, out IntPtr fp, out int size, out algorithm, base64 ? 1 : 0) == 1)
            {
                var buffer = new int[size];

                Marshal.Copy(fp, buffer, 0, size);

                chromaprint_dealloc(fp);

                return buffer;
            }

            return null;
        }
        finally
        {
            h.Free();
        }
    }
    
    public int[] DecodeAcoustIdFingerprint(string base64)
    {
        // Convert URL-safe Base64 to standard Base64
        byte[] encoded = DecodeAcoustIdBase64(base64);

        return DecodeFingerprint(encoded, false, out int algo);
    }

    private string GetVersion()
    {
        IntPtr ptr = chromaprint_get_version();
        return Marshal.PtrToStringAnsi(ptr) ?? "Unknown";
    }
    
    private byte[] DecodeAcoustIdBase64(string input)
    {
        // Convert from URL-safe Base64 to standard Base64
        string base64 = input
            .Replace('-', '+')
            .Replace('_', '/');

        // Add padding if missing
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }

        return Convert.FromBase64String(base64);
    }

    public double DTWSimilarity(int[] a, int[] b, double threshold = 0.95)
    {
        int n = a.Length, m = b.Length;
        double maxPossible = Math.Max(n, m) * 4294967295.0; // max 32-bit unsigned
        double maxCost = (1 - threshold) * maxPossible;

        double[] prev = new double[m + 1];
        double[] curr = new double[m + 1];

        for (int j = 0; j <= m; j++)
        {
            prev[j] = double.PositiveInfinity;
        }
        prev[0] = 0;

        for (int i = 1; i <= n; i++)
        {
            curr[0] = double.PositiveInfinity;
            double rowMin = double.PositiveInfinity;

            for (int j = 1; j <= m; j++)
            {
                double cost = Math.Abs((long)a[i - 1] - (long)b[j - 1]);
                curr[j] = cost + Math.Min(prev[j], Math.Min(curr[j - 1], prev[j - 1]));
                rowMin = Math.Min(rowMin, curr[j]);
            }

            if (rowMin > maxCost)
            {
                return 1 - maxCost / maxPossible; // early exit, similarity < threshold
            }

            // Swap rows
            var tmp = prev;
            prev = curr;
            curr = tmp;
        }

        return 1.0 - prev[m] / maxPossible;
    }
}