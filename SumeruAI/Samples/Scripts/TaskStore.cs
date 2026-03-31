using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SumeruAI.Samples
{
    public static class TaskStore
    {
        private const string RootFolderName = "SumeruAI/Mugen3D";
        private const string TasksFileName = "tasks.json";
        private const string PreviewFolderName = "Previews";
        private const string DownloadFolderName = "Downloads";

        public static string RootDirectory => Path.Combine(Application.dataPath.Replace("Assets",""), RootFolderName);

        public static string TasksFilePath => Path.Combine(RootDirectory, TasksFileName);

        public static string PreviewDirectory => Path.Combine(RootDirectory, PreviewFolderName);

        public static string DownloadDirectory => Path.Combine(RootDirectory, DownloadFolderName);

        public static void EnsureDirectories()
        {
            Directory.CreateDirectory(RootDirectory);
            Directory.CreateDirectory(PreviewDirectory);
            Directory.CreateDirectory(DownloadDirectory);
        }

        public static List<TaskData> LoadTasks()
        {
            EnsureDirectories();

            if (!File.Exists(TasksFilePath))
            {
                return new List<TaskData>();
            }

            string json = File.ReadAllText(TasksFilePath);
            if (string.IsNullOrEmpty(json))
            {
                return new List<TaskData>();
            }

            ImageMattingTaskCollection collection = JsonUtility.FromJson<ImageMattingTaskCollection>(json);
            if (collection == null || collection.tasks == null)
            {
                return new List<TaskData>();
            }

            return collection.tasks;
        }

        public static void SaveTasks(List<TaskData> tasks)
        {
            EnsureDirectories();
            ImageMattingTaskCollection collection = new ImageMattingTaskCollection
            {
                tasks = tasks ?? new List<TaskData>()
            };

            string json = JsonUtility.ToJson(collection, true);
            File.WriteAllText(TasksFilePath, json);
        }

        public static string GetPreviewPath(string localId)
        {
            EnsureDirectories();
            return Path.Combine(PreviewDirectory, $"{localId}_preview.png");
        }

        public static string GetDefaultDownloadPath(string localId, string fileName)
        {
            EnsureDirectories();
            return Path.Combine(DownloadDirectory, $"{localId}_{fileName}");
        }
    }
}
