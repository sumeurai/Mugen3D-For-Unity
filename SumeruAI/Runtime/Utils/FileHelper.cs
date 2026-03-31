using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;



public static class FileHelper
{
    private static string dataFolder = "SumeruAI";

    private static string dataPath;

    public static string DataPath
    {
        get
        {

#if UNITY_EDITOR || UNITY_EDITOR_WIN
            dataPath = Path.Combine(Application.dataPath.Replace("Assets",""),dataFolder);
#endif

            return dataPath;
        }
    }


    public static bool Exits(string fileName)
    {
        string filepath = Path.Combine(DataPath, fileName);


        if (File.Exists(filepath))
        {
            return true;
        }

        return false;
    }

    public static string CreatePath(string folderName,string fileName)
    {
        string filePath = Path.Combine(DataPath,folderName,fileName);

        string folderPath = Path.GetDirectoryName(filePath);

        if (!Directory.Exists(folderPath))
        {
            if (folderPath != null) Directory.CreateDirectory(folderPath);
        }

        return filePath;
    }


}
