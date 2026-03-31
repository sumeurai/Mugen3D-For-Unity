using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Gsplat;
using UnityEngine;

namespace SumeruAI.Samples
{
    public static class GsplatLoader
    {
        public static bool TryLoadAsset(string localModelPath, out GsplatAsset asset, out string error)
        {
            asset = null;
            error = string.Empty;

            string plyPath = ResolvePlyPath(localModelPath, out error);
            if (string.IsNullOrEmpty(plyPath))
            {
                return false;
            }

            asset = CreateGsplatAsset(plyPath);
            if (asset == null)
            {
                error = $"Failed to create gsplat asset from `{plyPath}`.";
                return false;
            }

            return true;
        }

        private static string ResolvePlyPath(string localModelPath, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(localModelPath) || !File.Exists(localModelPath))
            {
                error = "Model file does not exist.";
                return null;
            }

            string extension = Path.GetExtension(localModelPath).ToLowerInvariant();
            if (extension == ".ply")
            {
                return localModelPath;
            }

            if (extension != ".zip")
            {
                error = $"Unsupported model file format: {extension}";
                return null;
            }

            string extractDirectory = GetExtractDirectory(localModelPath);
            try
            {
                if (Directory.Exists(extractDirectory))
                {
                    Directory.Delete(extractDirectory, true);
                }

                Directory.CreateDirectory(extractDirectory);
                ZipFile.ExtractToDirectory(localModelPath, extractDirectory);
            }
            catch (Exception ex)
            {
                error = $"Failed to extract model archive: {ex.Message}";
                return null;
            }

            string plyPath = Directory
                .GetFiles(extractDirectory, "*.ply", SearchOption.AllDirectories)
                .OrderBy(path => path.Length)
                .FirstOrDefault();

            if (string.IsNullOrEmpty(plyPath))
            {
                error = "No .ply model file was found in the archive.";
                return null;
            }

            return plyPath;
        }

        private static string GetExtractDirectory(string localModelPath)
        {
            string modelName = Path.GetFileNameWithoutExtension(localModelPath);
            return Path.Combine(TaskStore.DownloadDirectory, $"{modelName}_extracted");
        }

        private static GsplatAsset CreateGsplatAsset(string assetPath)
        {
            GsplatAsset gsplatAsset = ScriptableObject.CreateInstance<GsplatAsset>();
            Bounds bounds = new Bounds();

            using (FileStream fs = new FileStream(assetPath, FileMode.Open, FileAccess.Read))
            {
                if (fs.Length >= 2 * 1024 * 1024 * 1024L)
                {
                    if (GsplatSettings.Instance.ShowImportErrors)
                    {
                        Debug.LogError($"{assetPath} import error: currently files larger than 2GB are not supported");
                    }

                    return null;
                }

                PlyHeaderInfo plyInfo = ReadPlyHeader(fs);
                int shCoeffs = plyInfo.SHPropertyCount / 3;
                gsplatAsset.SplatCount = plyInfo.VertexCount;
                gsplatAsset.SHBands = GsplatUtils.CalcSHBandsFromSHPropertyCount(plyInfo.SHPropertyCount);

                if (gsplatAsset.SHBands > 3 ||
                    GsplatUtils.SHBandsToCoefficientCount(gsplatAsset.SHBands) * 3 != plyInfo.SHPropertyCount)
                {
                    if (GsplatSettings.Instance.ShowImportErrors)
                    {
                        Debug.LogError($"{assetPath} import error: unexpected SH property count {plyInfo.SHPropertyCount}");
                    }

                    return null;
                }

                if (plyInfo.PositionOffset == -1 || plyInfo.ColorOffset == -1 || plyInfo.OpacityOffset == -1 ||
                    plyInfo.ScaleOffset == -1 || plyInfo.RotationOffset == -1)
                {
                    if (GsplatSettings.Instance.ShowImportErrors)
                    {
                        Debug.LogError($"{assetPath} import error: missing required properties in PLY header");
                    }

                    return null;
                }

                gsplatAsset.Positions = new Vector3[plyInfo.VertexCount];
                gsplatAsset.Colors = new Vector4[plyInfo.VertexCount];
                if (shCoeffs > 0)
                {
                    gsplatAsset.SHs = new Vector3[plyInfo.VertexCount * shCoeffs];
                }

                gsplatAsset.Scales = new Vector3[plyInfo.VertexCount];
                gsplatAsset.Rotations = new Vector4[plyInfo.VertexCount];

                byte[] buffer = new byte[plyInfo.PropertyCount * sizeof(float)];
                for (uint i = 0; i < plyInfo.VertexCount; i++)
                {
                    int readBytes = fs.Read(buffer);
                    if (readBytes != buffer.Length)
                    {
                        if (GsplatSettings.Instance.ShowImportErrors)
                        {
                            Debug.LogError($"{assetPath} import error: unexpected end of file, got {readBytes} bytes at vertex {i}");
                        }

                        return null;
                    }

                    var properties = MemoryMarshal.Cast<byte, float>(buffer);
                    gsplatAsset.Positions[i] = new Vector3(
                        properties[plyInfo.PositionOffset],
                        properties[plyInfo.PositionOffset + 1],
                        properties[plyInfo.PositionOffset + 2]);
                    gsplatAsset.Colors[i] = new Vector4(
                        properties[plyInfo.ColorOffset],
                        properties[plyInfo.ColorOffset + 1],
                        properties[plyInfo.ColorOffset + 2],
                        GsplatUtils.Sigmoid(properties[plyInfo.OpacityOffset]));

                    for (int j = 0; j < shCoeffs; j++)
                    {
                        gsplatAsset.SHs[i * shCoeffs + j] = new Vector3(
                            properties[j + plyInfo.SHOffset],
                            properties[j + plyInfo.SHOffset + shCoeffs],
                            properties[j + plyInfo.SHOffset + shCoeffs * 2]);
                    }

                    gsplatAsset.Scales[i] = new Vector3(
                        Mathf.Exp(properties[plyInfo.ScaleOffset]),
                        Mathf.Exp(properties[plyInfo.ScaleOffset + 1]),
                        Mathf.Exp(properties[plyInfo.ScaleOffset + 2]));
                    gsplatAsset.Rotations[i] = new Vector4(
                        properties[plyInfo.RotationOffset],
                        properties[plyInfo.RotationOffset + 1],
                        properties[plyInfo.RotationOffset + 2],
                        properties[plyInfo.RotationOffset + 3]).normalized;

                    if (i == 0)
                    {
                        bounds = new Bounds(gsplatAsset.Positions[i], Vector3.zero);
                    }
                    else
                    {
                        bounds.Encapsulate(gsplatAsset.Positions[i]);
                    }
                }
            }

            gsplatAsset.Bounds = bounds;
            return gsplatAsset;
        }

        private static string ReadLine(FileStream fs)
        {
            List<byte> byteBuffer = new List<byte>();
            while (true)
            {
                int b = fs.ReadByte();
                if (b == -1 || b == '\n')
                {
                    break;
                }

                byteBuffer.Add((byte)b);
            }

            if (byteBuffer.Count > 0 && byteBuffer[byteBuffer.Count - 1] == '\r')
            {
                byteBuffer.RemoveAt(byteBuffer.Count - 1);
            }

            return Encoding.UTF8.GetString(byteBuffer.ToArray());
        }

        private static PlyHeaderInfo ReadPlyHeader(FileStream fs)
        {
            PlyHeaderInfo info = new PlyHeaderInfo();

            while (ReadLine(fs) is { } line && line != "end_header")
            {
                string[] tokens = line.Split(' ');
                if (tokens.Length == 3 && tokens[0] == "element" && tokens[1] == "vertex")
                {
                    info.VertexCount = uint.Parse(tokens[2]);
                }

                if (tokens.Length != 3 || tokens[0] != "property")
                {
                    continue;
                }

                switch (tokens[2])
                {
                    case "x":
                        info.PositionOffset = info.PropertyCount;
                        break;
                    case "f_dc_0":
                        info.ColorOffset = info.PropertyCount;
                        break;
                    case "f_rest_0":
                        info.SHOffset = info.PropertyCount;
                        break;
                    case "opacity":
                        info.OpacityOffset = info.PropertyCount;
                        break;
                    case "scale_0":
                        info.ScaleOffset = info.PropertyCount;
                        break;
                    case "rot_0":
                        info.RotationOffset = info.PropertyCount;
                        break;
                }

                if (tokens[2].StartsWith("f_rest_"))
                {
                    info.SHPropertyCount++;
                }

                info.PropertyCount++;
            }

            return info;
        }

        private sealed class PlyHeaderInfo
        {
            public uint VertexCount;
            public int PropertyCount;
            public int SHPropertyCount;
            public int PositionOffset = -1;
            public int ColorOffset = -1;
            public int SHOffset = -1;
            public int OpacityOffset = -1;
            public int ScaleOffset = -1;
            public int RotationOffset = -1;
        }
    }
}
